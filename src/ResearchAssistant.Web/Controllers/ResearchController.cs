using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using ResearchAssistant.Core.Workflow;
using ResearchAssistant.Web.Services;

namespace ResearchAssistant.Web.Controllers;

/// <summary>
/// 研究接口：POST 发起研究并以 SSE 逐事件推送（前后端唯一流式契约），GET /api/status 给首页副标题。
/// 事件类型：executor-start / text / tool-call / tool-result / report / error / done。
/// </summary>
[ApiController]
public sealed class ResearchController(ResearchRunner runner) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record ResearchRequest(
        string Question,
        bool TodoList = true,
        bool PlanExecuteMode = true,
        bool FileMemory = false,
        bool SkillDiscovery = true);

    [HttpGet("/api/status")]
    public IActionResult Status() => Ok(new { corpusChunkCount = runner.CorpusChunkCount });

    [HttpPost("/api/research")]
    public async Task Research([FromBody] ResearchRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";  // 反向代理（nginx 等）禁缓冲，SSE 标准做法

        var cancellationToken = HttpContext.RequestAborted;  // 客户端断开（含 AbortController）即取消工作流
        var switches = new HarnessFeatureSwitches
        {
            TodoList = request.TodoList,
            PlanExecuteMode = request.PlanExecuteMode,
            FileMemory = request.FileMemory,
            SkillDiscovery = request.SkillDiscovery,
        };

        string? currentExecutor = null;
        try
        {
            await foreach (var evt in runner.RunAsync(request.Question, switches, cancellationToken))
            {
                switch (evt)
                {
                    case AgentResponseUpdateEvent e:
                        if (e.ExecutorId != currentExecutor)
                        {
                            currentExecutor = e.ExecutorId;
                            await WriteSseAsync("executor-start", new { id = e.ExecutorId });
                        }
                        if (!string.IsNullOrEmpty(e.Update.Text))
                        {
                            await WriteSseAsync("text", new { executorId = e.ExecutorId, delta = e.Update.Text });
                        }
                        foreach (var call in e.Update.Contents.OfType<FunctionCallContent>())
                        {
                            await WriteSseAsync("tool-call", new
                            {
                                executorId = e.ExecutorId,
                                name = call.Name,
                                args = call.Arguments is null ? "" : JsonSerializer.Serialize(call.Arguments, Json),
                            });
                        }
                        foreach (var result in e.Update.Contents.OfType<FunctionResultContent>())
                        {
                            await WriteSseAsync("tool-result", new { executorId = e.ExecutorId, text = Truncate(result.Result?.ToString()) });
                        }
                        break;

                    case WorkflowOutputEvent output:
                        await WriteSseAsync("report", new { markdown = output.As<ChatMessage>()?.Text });
                        break;

                    case ExecutorFailedEvent failed:
                        await WriteSseAsync("error", new { message = $"执行者 {failed.ExecutorId} 失败：{failed.Data}" });
                        break;

                    case WorkflowErrorEvent error:
                        await WriteSseAsync("error", new { message = $"工作流异常：{error.Exception?.Message ?? "未知错误"}" });
                        break;
                }
            }
            await WriteSseAsync("done", new { });
        }
        catch (OperationCanceledException)
        {
            // 客户端主动断开，连接安静结束即可
        }

        async Task WriteSseAsync(string eventName, object payload)
        {
            await Response.WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, Json)}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static string Truncate(string? text, int max = 600) =>
        text is null ? "" : text.Length <= max ? text : text[..max] + " …";
}
