// M1/M2/M4 验证 CLI。
// 用法（仓库根目录先执行 set -a; source .env; set +a）：
//   dotnet run "你的研究问题"            —— 完整工作流：Researcher → Analyst → Writer ⇄ Reviewer（M4）
//   dotnet run -- --single "问题"        —— 单 HarnessAgent 模式（M1，用于对比教学）

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ResearchAssistant.Core.Agents;
using ResearchAssistant.Core.Search;
using ResearchAssistant.Core.Tools;
using ResearchAssistant.Core.Workflow;

var singleMode = args.Contains("--single");
var questionParts = args.Where(a => a != "--single").ToArray();
var question = questionParts.Length > 0
    ? string.Join(' ', questionParts)
    : throw new InvalidOperationException("用法：dotnet run [--single] \"你的研究问题\"");

// 语料目录：从当前目录向上找 data/corpus（兼容在 tools/ResearchCli 或 bin 下运行）
var corpusDir = CorpusLocator.Find();
var corpus = LocalCorpusSearch.Load(corpusDir);
Console.WriteLine($"[初始化] 本地语料库：{corpusDir}（{corpus.ChunkCount} 个检索块）");

var bochaKey = Environment.GetEnvironmentVariable("BOCHA_API_KEY")
    ?? throw new InvalidOperationException("BOCHA_API_KEY 未设置");
var webProvider = new BochaWebSearchProvider(new HttpClient(), bochaKey);
var client = DeepSeekClientFactory.Create();

Console.WriteLine($"[提问] {question}\n");

if (singleMode)
{
    await RunSingleAgentAsync(client, corpus, webProvider, question);
}
else
{
    await RunWorkflowAsync(client, corpus, webProvider, question);
}

// ================= M1：单 HarnessAgent =================
static async Task RunSingleAgentAsync(IChatClient client, LocalCorpusSearch corpus, IWebSearchProvider webProvider, string question)
{
    var agent = client.AsHarnessAgent(new HarnessAgentOptions
    {
        Name = "Researcher",
        DisableWebSearch = true,
        DisableFileMemory = true,   // CLI 验证阶段不在磁盘留痕
        ChatOptions = new()
        {
            Instructions = """
                你是一名严谨的研究员。回答前先规划检索策略：
                1. 优先用 local_docs_search 查本地权威文档库；
                2. 涉及时效性/最新发布/本地库未覆盖的内容时，用 web_search 联网补充；
                3. 两通道结果互相印证，回答中标注来源（本地文件名或 URL）。
                """,
            Tools =
            [
                ResearchTools.CreateLocalSearchTool(corpus),
                ResearchTools.CreateWebSearchTool(webProvider),
            ],
        },
    });

    await foreach (var update in agent.RunStreamingAsync(question))
    {
        PrintUpdate(update);
    }
    Console.WriteLine();
}

// ================= M4：四 Agent 图工作流（含审校回环）=================
static async Task RunWorkflowAsync(IChatClient client, LocalCorpusSearch corpus, IWebSearchProvider webProvider, string question)
{
    var workflow = ResearchWorkflowFactory.Build(client, corpus, webProvider);

    // 工作流起点 Researcher 接收 string；事件流由 WatchStreamAsync 给出（无需 TurnToken）
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, question);

    string? currentExecutor = null;
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case AgentResponseUpdateEvent e:
                if (e.ExecutorId != currentExecutor)
                {
                    currentExecutor = e.ExecutorId;
                    Console.WriteLine($"\n════════ {currentExecutor} ════════");
                }
                PrintUpdate(e.Update);
                break;

            case WorkflowOutputEvent output:
                Console.WriteLine("\n════════ 工作流完成 ════════");
                var final = output.As<ChatMessage>();
                Console.WriteLine($"最终报告长度：{final?.Text.Length ?? 0} 字符");
                break;

            case ExecutorFailedEvent failed:
                Console.WriteLine($"\n[错误] 执行者 {failed.ExecutorId} 失败：{failed.Data}");
                break;

            case WorkflowErrorEvent error:
                Console.WriteLine($"\n[错误] 工作流异常：{error.Exception?.Message ?? "未知"}");
                break;
        }
    }
    Console.WriteLine();
}

// 流式输出：文本增量直接打印，工具调用/结果用醒目行展示
static void PrintUpdate(AgentResponseUpdate update)
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
    foreach (var call in update.Contents.OfType<FunctionCallContent>())
    {
        Console.Write($"\n>>> 调用工具 {call.Name}({string.Join(", ", call.Arguments?.Select(a => $"{a.Key}={a.Value}") ?? [])})\n");
    }
    foreach (var result in update.Contents.OfType<FunctionResultContent>())
    {
        var text = result.Result?.ToString() ?? "";
        Console.Write($"\n<<< 工具返回（{(text.Length > 300 ? text[..300] + "…" : text)}）\n");
    }
}
