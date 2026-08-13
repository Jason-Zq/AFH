namespace ResearchAssistant.Core.Search;

/// <summary>
/// 联网搜索提供方抽象。当前实现为博查（BoCha），
/// 如需切换 Tavily 等其他服务，新增一个实现即可，工具层不用动。
/// </summary>
public interface IWebSearchProvider
{
    /// <summary>按查询词搜索，返回按相关度排序的命中列表。</summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults = 8, CancellationToken cancellationToken = default);
}
