using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ResearchAssistant.Core.Workflow;

/// <summary>
/// 研究工作流的执行者节点。模式取自官方 07_WriterCriticWorkflow 样例：
/// Executor 是工作流的节点抽象，内部包装 AIAgent，
/// 并通过 context.AddEventAsync 把 Agent 的流式增量转发为工作流事件（UI 实时渲染全靠它）。
/// </summary>
internal abstract class StreamingAgentExecutor : Executor
{
    private readonly AIAgent _agent;

    protected StreamingAgentExecutor(string id, AIAgent agent) : base(id) => _agent = agent;

    /// <summary>流式运行内部 Agent，把每个增量转发为工作流事件，返回完整文本。</summary>
    protected async Task<string> RunAndForwardAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await foreach (var update in _agent.RunStreamingAsync(message, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                sb.Append(update.Text);
            }
            await context.AddEventAsync(new AgentResponseUpdateEvent(Id, update), cancellationToken);
        }
        return sb.ToString();
    }
}

/// <summary>研究员：接收研究问题（string），产出研究笔记。</summary>
internal sealed partial class ResearcherExecutor(AIAgent agent) : StreamingAgentExecutor("Researcher", agent)
{
    [MessageHandler]
    public async ValueTask<ChatMessage> HandleAsync(string question, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var notes = await RunAndForwardAsync(new ChatMessage(ChatRole.User, question), context, cancellationToken);
        return new ChatMessage(ChatRole.User, $"研究问题：{question}\n\n研究笔记：\n{notes}");
    }
}

/// <summary>分析师：消化研究笔记，产出论点骨架。</summary>
internal sealed partial class AnalystExecutor(AIAgent agent) : StreamingAgentExecutor("Analyst", agent)
{
    [MessageHandler]
    public async ValueTask<ChatMessage> HandleAsync(ChatMessage researchNotes, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var analysis = await RunAndForwardAsync(researchNotes, context, cancellationToken);
        return new ChatMessage(ChatRole.User, $"{researchNotes.Text}\n\n分析结论：\n{analysis}");
    }
}

/// <summary>撰稿人：两种入口——根据分析写初稿，或根据审校意见修订。</summary>
internal sealed partial class WriterExecutor(AIAgent agent) : StreamingAgentExecutor("Writer", agent)
{
    [MessageHandler]
    public async ValueTask<ChatMessage> HandleAsync(ChatMessage analysis, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var draft = await RunAndForwardAsync(analysis, context, cancellationToken);
        return new ChatMessage(ChatRole.User, draft);
    }

    [MessageHandler]
    public async ValueTask<ChatMessage> HandleAsync(ReviewDecision decision, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var prompt = $"请根据审校意见修订以下研究报告草稿。\n\n审校意见：{decision.Feedback}\n\n原草稿：\n{decision.Draft}";
        var revised = await RunAndForwardAsync(new ChatMessage(ChatRole.User, prompt), context, cancellationToken);
        return new ChatMessage(ChatRole.User, revised);
    }
}

/// <summary>
/// 审校人：评审草稿并输出结构化结论；兼任"刹车"——达到最大修订次数强制通过。
/// </summary>
internal sealed partial class ReviewerExecutor(AIAgent agent) : StreamingAgentExecutor("Reviewer", agent)
{
    public const int MaxRevisions = 2;
    private const string StateScope = "ResearchFlow";
    private const string StateKey = "revisionCount";

    [MessageHandler]
    public async ValueTask<ReviewDecision> HandleAsync(ChatMessage draft, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var output = await RunAndForwardAsync(draft, context, cancellationToken);
        var decision = ReviewDecision.Parse(output, draft.Text ?? "");

        var revisionCount = await context.ReadStateAsync<int>(StateKey, StateScope, cancellationToken);
        if (!decision.Approved && revisionCount >= MaxRevisions)
        {
            decision.Approved = true;   // 刹车：避免审校-修订无限循环
        }
        if (!decision.Approved)
        {
            await context.QueueStateUpdateAsync(StateKey, revisionCount + 1, StateScope, cancellationToken);
        }
        return decision;
    }
}

/// <summary>定稿：把通过审校的草稿作为工作流最终输出（WithOutputFrom 会把返回值自动 Yield，无需显式调 YieldOutputAsync）。</summary>
internal sealed class FinalizeExecutor() : Executor<ReviewDecision, ChatMessage>("Finalize")
{
    public override ValueTask<ChatMessage> HandleAsync(ReviewDecision message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new ChatMessage(ChatRole.Assistant, message.Draft));
}
