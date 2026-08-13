namespace ResearchAssistant.Core.Search;

/// <summary>一条检索命中结果（联网或本地库统一使用）。</summary>
/// <param name="Title">标题（网页标题或文档名）。</param>
/// <param name="Source">来源标识：URL 或本地文件名。</param>
/// <param name="Snippet">摘要/正文片段。</param>
/// <param name="Origin">来源通道：Web 或 Local。</param>
public sealed record SearchHit(string Title, string Source, string Snippet, string Origin);
