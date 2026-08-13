using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ResearchAssistant.Core.Workflow;

namespace ResearchAssistant.Core.Tests;

/// <summary>
/// 工作流编排测试：用假 IChatClient 验证图编排（含 Writer⇄Reviewer 回环）的结构正确性，
/// 不依赖网络与真实模型（编排逻辑才是我们的代码，模型输出不是）。
/// </summary>
public class ResearchWorkflowTests
{
    [Fact]
    public async Task 审校一次通过_四个Agent按序执行_并产出最终报告()
    {
        AIAgent researcher = new FakeChatClient("研究笔记").AsAIAgent(name: "Researcher", instructions: "测试桩");
        AIAgent analyst = new FakeChatClient("分析结论").AsAIAgent(name: "Analyst", instructions: "测试桩");
        AIAgent writer = new FakeChatClient("报告草稿").AsAIAgent(name: "Writer", instructions: "测试桩");
        AIAgent reviewer = new FakeChatClient("""{"approved": true, "feedback": ""}""").AsAIAgent(name: "Reviewer", instructions: "测试桩");
        var workflow = ResearchWorkflowFactory.Build(researcher, analyst, writer, reviewer);

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, "测试问题");

        var executorSequence = new List<string>();
        ChatMessage? finalOutput = null;
        await foreach (var evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent e when e.ExecutorId != executorSequence.LastOrDefault():
                    executorSequence.Add(e.ExecutorId);
                    break;
                case WorkflowOutputEvent output:
                    finalOutput = output.As<ChatMessage>();
                    break;
            }
        }

        // 四个执行者按编排顺序出现，且工作流产出了 Writer 的草稿
        Assert.Equal(["Researcher", "Analyst", "Writer", "Reviewer"], executorSequence);
        Assert.NotNull(finalOutput);
        Assert.Contains("报告草稿", finalOutput!.Text);
    }

    [Fact]
    public async Task 审校打回一次_Writer执行两次_修订稿成为最终输出()
    {
        AIAgent researcher = new FakeChatClient("研究笔记").AsAIAgent(name: "Researcher", instructions: "测试桩");
        AIAgent analyst = new FakeChatClient("分析结论").AsAIAgent(name: "Analyst", instructions: "测试桩");
        // Writer 第一次产出初稿，第二次（收到修订请求）产出修订稿
        AIAgent writer = new FakeChatClient("初稿", "修订稿").AsAIAgent(name: "Writer", instructions: "测试桩");
        // Reviewer 第一次打回，第二次通过
        AIAgent reviewer = new FakeChatClient(
            """{"approved": false, "feedback": "缺少来源标注"}""",
            """{"approved": true, "feedback": ""}""").AsAIAgent(name: "Reviewer", instructions: "测试桩");
        var workflow = ResearchWorkflowFactory.Build(researcher, analyst, writer, reviewer);

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, "测试问题");

        var executorSequence = new List<string>();
        ChatMessage? finalOutput = null;
        await foreach (var evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent e when e.ExecutorId != executorSequence.LastOrDefault():
                    executorSequence.Add(e.ExecutorId);
                    break;
                case WorkflowOutputEvent output:
                    finalOutput = output.As<ChatMessage>();
                    break;
            }
        }

        // Writer 被执行了两次（初稿 + 修订），最终输出是修订稿而非初稿
        Assert.Equal(2, executorSequence.Count(id => id == "Writer"));
        Assert.Equal("Reviewer", executorSequence.Last());
        Assert.NotNull(finalOutput);
        Assert.Contains("修订稿", finalOutput!.Text);
        Assert.DoesNotContain("初稿", finalOutput!.Text);
    }

    /// <summary>按队列依次回话的假聊天客户端，支持流式与非流式两种调用。</summary>
    private sealed class FakeChatClient(params string[] responses) : IChatClient
    {
        private int _callCount;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Next())));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, Next());
            await Task.CompletedTask;
        }

        // 超过预设回复数后重复最后一条（例如 Writer 被多轮调用时）
        private string Next() => responses[Math.Min(_callCount++, responses.Length - 1)];

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
