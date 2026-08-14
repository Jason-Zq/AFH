using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ResearchAssistant.Core.Search;
using ResearchAssistant.Web.Data;
using ResearchAssistant.Web.Services;

namespace ResearchAssistant.Web.Controllers;

/// <summary>后台运维端点：系统状态、索引重建、种子重导、日志查询。无鉴权——仅限本地演示。</summary>
[ApiController]
[Route("api/admin/ops")]
public class AdminOpsController(ResearchDbContext db, ResearchRunner runner, InMemoryLogStore logStore, ILogger<AdminOpsController> logger) : ControllerBase
{
    // 取进程真实启动时间（静态字段首次访问才初始化的 Stopwatch 会少计）
    private static readonly DateTimeOffset StartedAt = new(Process.GetCurrentProcess().StartTime);

    /// <summary>系统状态总览。DB 挂了也照常返回（dbConnected=false，其余字段尽力而为）——仪表盘的价值恰在故障时体现。</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var dbConnected = false;
        var documentCount = 0;
        var sessionCount = 0;
        IReadOnlyList<string> appliedMigrations = [];
        try
        {
            dbConnected = await db.Database.CanConnectAsync();
            if (dbConnected)
            {
                documentCount = await db.Documents.CountAsync();
                sessionCount = await db.ResearchSessions.CountAsync();
                appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            }
        }
        catch
        {
            // 状态接口绝不能抛
        }

        string? corpusDirectory = null;
        try
        {
            corpusDirectory = CorpusLocator.Find();
        }
        catch (DirectoryNotFoundException)
        {
            // 语料目录缺失也在状态里如实反映（null）
        }

        return Ok(new
        {
            dbConnected,
            documentCount,
            sessionCount,
            appliedMigrations,
            corpusChunkCount = runner.CorpusChunkCount,
            uptimeSeconds = (DateTimeOffset.Now - StartedAt).TotalSeconds,
            isResearchRunning = runner.IsRunning,
            corpusDirectory,
        });
    }

    /// <summary>重建 BM25 语料索引（原子替换，不影响进行中的研究）。</summary>
    [HttpPost("rebuild-index")]
    public async Task<IActionResult> RebuildIndex()
    {
        var sw = Stopwatch.StartNew();
        var chunkCount = await runner.RebuildCorpusAsync();
        sw.Stop();
        logger.LogInformation("索引重建完成：{ChunkCount} 块，耗时 {ElapsedMs}ms", chunkCount, sw.ElapsedMilliseconds);
        return Ok(new { chunkCount, elapsedMs = sw.ElapsedMilliseconds });
    }

    /// <summary>重新导入种子语料：append 幂等跳过重名；replace 清空重灌（研究进行中禁止，防读出空表建空索引）。</summary>
    [HttpPost("reseed-corpus")]
    public async Task<IActionResult> ReseedCorpus([FromBody] ReseedRequest? request)
    {
        var mode = request?.Mode ?? "append";
        if (mode is not ("append" or "replace"))
        {
            return BadRequest(new { message = "mode 只能是 append 或 replace。" });
        }
        if (mode == "replace" && runner.IsRunning)
        {
            return Conflict(new { message = "有研究正在进行，禁止清空语料重导；请等待完成或先取消。" });
        }

        try
        {
            var (imported, skipped, deleted) = await DbInitializer.SeedCorpusAsync(
                db, mode == "replace" ? SeedMode.Replace : SeedMode.Append);
            var chunkCount = await runner.RebuildCorpusAsync();
            logger.LogInformation("种子语料重导（{Mode}）：导入 {Imported}、跳过 {Skipped}、删除 {Deleted}，索引 {ChunkCount} 块",
                mode, imported, skipped, deleted, chunkCount);
            return Ok(new { imported, skipped, deleted, chunkCount });
        }
        catch (DirectoryNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // 两个浏览器并发 append 重导会撞同名唯一索引（前端 busy 防连点只罩得住一个页面）
            return Conflict(new { message = "另一个导入正在写入同名文档，请稍后重试。" });
        }
    }

    /// <summary>内存日志查询：minLevel 级别过滤（默认 Information），take 钳制 1–1000，最新在前。</summary>
    [HttpGet("logs")]
    public IActionResult GetLogs([FromQuery] string? minLevel, [FromQuery] int take = 200)
    {
        if (!Enum.TryParse<LogLevel>(minLevel, ignoreCase: true, out var level))
        {
            level = LogLevel.Information;
        }
        take = Math.Clamp(take, 1, 1000);
        var entries = logStore.Query(level, take)
            .Select(e => new { timestamp = e.Timestamp, level = e.Level.ToString(), category = e.Category, message = e.Message, exception = e.Exception });
        return Ok(entries);
    }
}

/// <summary>种子重导请求体。</summary>
public sealed record ReseedRequest(string? Mode);
