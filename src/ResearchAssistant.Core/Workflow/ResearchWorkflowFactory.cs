using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ResearchAssistant.Core.Search;
using ResearchAssistant.Core.Tools;

namespace ResearchAssistant.Core.Workflow;

/// <summary>
/// 研究工作流工厂：Researcher → Analyst → Writer → Reviewer ─┐
///                                                  ↑（打回修订，最多 2 次）┘
/// 图结构编排 + 条件路由（AddSwitch）+ 共享状态（迭代计数）。
/// </summary>
public static class ResearchWorkflowFactory
{
    /// <summary>用真实依赖构建完整工作流（生产路径）。</summary>
    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        IChatClient chatClient, LocalCorpusSearch corpus, IWebSearchProvider webSearch,
        HarnessFeatureSwitches? switches = null) =>
        Build(
            CreateResearcher(chatClient, corpus, webSearch, switches ?? new HarnessFeatureSwitches()),
            CreateAnalyst(chatClient),
            CreateWriter(chatClient),
            CreateReviewer(chatClient));

    /// <summary>从四个 Agent 组装工作流图。独立出来便于用假 Agent 做无网络单测。</summary>
    public static Microsoft.Agents.AI.Workflows.Workflow Build(AIAgent researcher, AIAgent analyst, AIAgent writer, AIAgent reviewer)
    {
        var researcherExecutor = new ResearcherExecutor(researcher);
        var analystExecutor = new AnalystExecutor(analyst);
        var writerExecutor = new WriterExecutor(writer);
        var reviewerExecutor = new ReviewerExecutor(reviewer);
        var finalizeExecutor = new FinalizeExecutor();

        return new WorkflowBuilder(researcherExecutor)
            .AddEdge(researcherExecutor, analystExecutor)
            .AddEdge(analystExecutor, writerExecutor)
            .AddEdge(writerExecutor, reviewerExecutor)
            .AddSwitch(reviewerExecutor, sw => sw
                .AddCase<ReviewDecision>(d => d?.Approved == true, finalizeExecutor)
                .AddCase<ReviewDecision>(d => d?.Approved == false, writerExecutor))
            .WithOutputFrom(finalizeExecutor)
            .Build();
    }

    /// <summary>
    /// 研究员：唯一的 HarnessAgent——todo 规划、函数调用循环、历史持久化都由 Harness 托管。
    /// 挂载双通道检索工具，负责把原始证据收集齐全。
    /// </summary>
    private static AIAgent CreateResearcher(IChatClient chatClient, LocalCorpusSearch corpus, IWebSearchProvider webSearch, HarnessFeatureSwitches switches) =>
        chatClient.AsHarnessAgent(new HarnessAgentOptions
        {
            Name = "Researcher",
            Description = "携带本地文档库与联网搜索的研究员",
            DisableWebSearch = true,   // Harness 自带的是服务端托管搜索，DeepSeek 不支持；用自研博查工具替代
            DisableTodoProvider = !switches.TodoList,
            DisableAgentModeProvider = !switches.PlanExecuteMode,
            DisableFileMemory = !switches.FileMemory,
            DisableAgentSkillsProvider = !switches.SkillDiscovery,
            ChatOptions = new()
            {
                Instructions = """
                    你是一名严谨的研究员。针对用户的研究问题：
                    1. 先规划检索策略，优先用 local_docs_search 查本地权威文档库；
                    2. 涉及时效性/最新发布/本地库未覆盖的内容时，用 web_search 联网补充；
                    3. 输出一份研究笔记：分要点列出关键事实，每条标注来源（本地文件名或 URL），
                       指出两通道结果是否一致、有无冲突；
                    4. 不要下结论、不要写报告——你的产出只是给下游分析师的素材。
                    """,
                Tools =
                [
                    ResearchTools.CreateLocalSearchTool(corpus),
                    ResearchTools.CreateWebSearchTool(webSearch),
                ],
            },
        });

    /// <summary>分析师：消化研究笔记，提炼论点骨架。</summary>
    private static AIAgent CreateAnalyst(IChatClient chatClient) =>
        chatClient.AsAIAgent(
            name: "Analyst",
            instructions: """
                你是一名分析师。输入是用户的研究问题和研究员整理的研究笔记。请输出：
                1. 核心结论（3-5 条，每条一句话）；
                2. 支撑论据：每个结论对应的关键事实与来源；
                3. 值得注意的分歧或不确定性（如有）。
                要求：只基于研究笔记中的证据，不引入外部知识；笔记证据不足处明确标注"证据不足"。
                """);

    /// <summary>撰稿人：把分析结果写成可直接阅读的研究报告；收到审校意见时负责修订。</summary>
    private static AIAgent CreateWriter(IChatClient chatClient) =>
        chatClient.AsAIAgent(
            name: "Writer",
            instructions: """
                你是一名技术撰稿人。输入是用户的研究问题、研究笔记和分析结论。请输出一篇中文研究报告：
                - 结构：标题、摘要（3 句话以内）、正文分节、结论、参考来源列表；
                - 正文用 Markdown 格式，论述忠实于上游提供的证据，标注来源（本地文件名或 URL）；
                - 语言精炼准确，不堆砌形容词；证据不足之处如实说明，不编造。
                如果输入中包含审校意见，请逐条对照意见修订原草稿，保持报告整体结构。
                """);

    /// <summary>审校人：评审草稿质量，输出 JSON 结论（容错解析在 ReviewDecision.Parse）。</summary>
    private static AIAgent CreateReviewer(IChatClient chatClient) =>
        chatClient.AsAIAgent(
            name: "Reviewer",
            instructions: """
                你是严格的审校人。输入是一份研究报告草稿，请按三条标准评审：
                1. 结构完整（标题、摘要、正文分节、结论、参考来源列表）；
                2. 关键论述均有证据支撑且标注了来源；
                3. 无明显编造或逻辑漏洞。
                只输出一个 JSON 对象，不要输出任何其他内容：
                {"approved": true 或 false, "feedback": "不通过时的具体修改意见；通过时为空字符串"}
                标准把握：存在实质问题才打回；仅措辞瑕疵不应打回。
                """);
}
