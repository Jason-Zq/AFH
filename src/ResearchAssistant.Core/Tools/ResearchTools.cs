using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using ResearchAssistant.Core.Search;

namespace ResearchAssistant.Core.Tools;

/// <summary>
/// 暴露给 Agent 的检索工具集：联网搜索 + 本地文档库检索。
/// 输出统一格式化为紧凑文本，方便模型阅读与引用。
/// </summary>
public static class ResearchTools
{
    /// <summary>联网搜索工具（默认博查实现）。</summary>
    public static AIFunction CreateWebSearchTool(IWebSearchProvider provider) =>
        AIFunctionFactory.Create(
            async ([Description("搜索关键词，中英文均可")] string query,
                   [Description("返回结果条数，1-10")] int count = 5) =>
                FormatHits(await provider.SearchAsync(query, Math.Clamp(count, 1, 10))),
            name: "web_search",
            description: "联网搜索最新公开资料。当问题涉及时效性、新闻、官方发布信息，或本地文档库没有覆盖的内容时使用。");

    /// <summary>本地文档库检索工具。</summary>
    public static AIFunction CreateLocalSearchTool(LocalCorpusSearch corpus) =>
        AIFunctionFactory.Create(
            ([Description("检索关键词，尽量用核心术语，如 'ReAct 模式'、'Harness 区别'")] string query,
             [Description("返回结果条数，1-10")] int count = 5) =>
                FormatHits(corpus.Search(query, Math.Clamp(count, 1, 10))),
            name: "local_docs_search",
            description: "在本地 AI Agent 技术文档库中检索。该库收录了 Agent 架构模式（ReAct、Plan-and-Execute）、多智能体编排、RAG、工具调用、Harness 等主题的权威文档，优先使用。");

    private static string FormatHits(IReadOnlyList<SearchHit> hits)
    {
        if (hits.Count == 0)
        {
            return "未找到相关结果。";
        }
        var sb = new StringBuilder();
        for (var i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            sb.AppendLine($"[{i + 1}]（{h.Origin}）{h.Title}");
            sb.AppendLine($"    来源：{h.Source}");
            sb.AppendLine($"    {h.Snippet}");
        }
        return sb.ToString();
    }
}
