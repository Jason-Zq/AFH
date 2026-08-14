using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ResearchAssistant.Core.Agents;
using ResearchAssistant.Core.Search;
using ResearchAssistant.Core.Workflow;
using ResearchAssistant.Web.Data;

namespace ResearchAssistant.Web.Services;
/// <summary>
/// 研究工作流运行器：封装 DeepSeek 客户端、本地语料库、博查搜索与工作流构建，
/// 以异步事件流的形式把工作流事件交给调用方（SSE 控制器）。
/// 注册为单例：语料索引与 HTTP 客户端在进程内复用；DB 访问通过作用域获取 DbContext。
/// </summary>
public sealed class ResearchRunner(IWebHostEnvironment environment, IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<ResearchRunner> logger) : IDisposable
{
    private readonly IWebHostEnvironment _environment = environment;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ResearchRunner> _logger = logger;
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);

    private IChatClient? _chatClient;
    private LocalCorpusSearch? _corpus;
    private IWebSearchProvider? _webSearch;
    private int _runningCount;

    /// <summary>是否有研究工作流正在运行（危险运维操作的闸门，如种子 replace 重导）。</summary>
    public bool IsRunning => Volatile.Read(ref _runningCount) > 0;

    /// <summary>从数据库重建语料索引并原子替换；进行中的研究持旧索引不受影响。返回新索引块数。</summary>
    public async Task<int> RebuildCorpusAsync()
    {
        await _rebuildLock.WaitAsync();  // 手动重建与语料写操作触发的自动重建可能撞车，串行化
        try
        {
            // 索引构建是同步 CPU + IO，包 Task.Run 让出调用线程
            var corpus = await Task.Run(LoadCorpusFromDatabase);
            Volatile.Write(ref _corpus, corpus);  // 引用赋值本身原子，Volatile 保证跨线程可见性
            return corpus.ChunkCount;
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <summary>本地语料块数（用于页面展示）。未就绪或异常时返回 0。</summary>
    public int CorpusChunkCount
    {
        get
        {
            try { EnsureInitialized(); } catch { return 0; }
            return _corpus?.ChunkCount ?? 0;
        }
    }

    /// <summary>运行研究工作流，逐事件输出（Agent 文本增量、工具调用、最终结果、错误），结束后把会话落库。</summary>
    public async IAsyncEnumerable<WorkflowEvent> RunAsync(
        string question, HarnessFeatureSwitches switches, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureInitialized();
        Interlocked.Increment(ref _runningCount);
        _logger.LogInformation("研究开始：{Question}", question);

        string? report = null;
        var status = "Failed";  // 默认失败；流建立成功才改 Completed，取消由内层 catch 改写
        // 自增之后的一切都必须在外层 finally 视野内——Build/启动流抛错（如客户端恰好此时断开）
        // 若漏掉 Decrement，_runningCount 泄漏会让 IsRunning 永远为真（replace 重导永久 409）
        try
        {
            var workflow = ResearchWorkflowFactory.Build(_chatClient!, _corpus!, _webSearch!, switches);
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, question, cancellationToken: cancellationToken);
            // yield 不能与 catch 同层（CS1626），故手动迭代：内层 try/catch 定状态，外层 yield
            var enumerator = run.WatchStreamAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            status = "Completed";
            try
            {
                while (true)
                {
                    WorkflowEvent? evt;
                    try
                    {
                        evt = await enumerator.MoveNextAsync() ? enumerator.Current : null;
                    }
                    catch (OperationCanceledException) { status = "Cancelled"; throw; }
                    catch { status = "Failed"; throw; }
                    if (evt is null)
                    {
                        break;
                    }
                    if (evt is WorkflowOutputEvent output)
                    {
                        report = output.As<ChatMessage>()?.Text;
                    }
                    yield return evt;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _runningCount);
            // 客户端断开时，MAF 的监听流可能"安静结束"（MoveNextAsync 返回 false）而不是抛
            // OperationCanceledException——不校正的话，被用户取消的研究会记成 Completed
            if (status == "Completed" && cancellationToken.IsCancellationRequested)
            {
                status = "Cancelled";
            }
            _logger.LogInformation("研究结束（{Status}）：{Question}", status, question);
            await SaveSessionAsync(question, switches, report, status);
        }
    }

    /// <summary>惰性初始化：让配置缺失（如忘设 API key）以友好错误出现在响应里，而不是启动即崩溃。</summary>
    private void EnsureInitialized()
    {
        if (_chatClient is not null)
        {
            return;
        }
        // 与 RebuildCorpusAsync 共用同一把锁、锁内加载：否则"初启加载语料 v1"可能覆盖掉
        // 管理端刚刚重建发布的 v2（竞态窗口虽窄，但错了就要到下次写操作才自愈）
        _rebuildLock.Wait();
        try
        {
            if (_chatClient is not null)
            {
                return;  // 等锁期间其他线程已完成初始化
            }
            // 密钥两级回退：配置文件（appsettings.Local.json）→ 环境变量；都缺则报友好错误
            var deepSeekKey = _configuration["DeepSeek:ApiKey"]
                ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                ?? throw new InvalidOperationException("未找到 DeepSeek API key：请在 appsettings.Local.json 配置 DeepSeek:ApiKey，或设置环境变量 DEEPSEEK_API_KEY。");
            var bochaKey = _configuration["Bocha:ApiKey"]
                ?? Environment.GetEnvironmentVariable("BOCHA_API_KEY")
                ?? throw new InvalidOperationException("未找到博查 API key：请在 appsettings.Local.json 配置 Bocha:ApiKey，或设置环境变量 BOCHA_API_KEY。");

            _chatClient = DeepSeekClientFactory.Create(apiKey: deepSeekKey);
            _corpus = LoadCorpusFromDatabase();
            _webSearch = new BochaWebSearchProvider(_httpClient, bochaKey);
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <summary>从 PostgreSQL 读取语料建索引（documents 表由启动时的种子导入填充）。</summary>
    private LocalCorpusSearch LoadCorpusFromDatabase()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();
        var documents = db.Documents
            .OrderBy(d => d.Name)
            .Select(d => new { d.Name, d.Content })
            .AsEnumerable()
            .Select(d => (d.Name, d.Content));
        return LocalCorpusSearch.Load(documents);
    }

    private async Task SaveSessionAsync(string question, HarnessFeatureSwitches switches, string? report, string status)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();
            db.ResearchSessions.Add(new ResearchSession
            {
                Question = question,
                SwitchesJson = JsonSerializer.Serialize(switches),
                ReportMarkdown = report,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // 落库失败不影响主流程（会话历史是附加能力），记警告日志即可
            _logger.LogWarning(ex, "研究会话落库失败");
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
