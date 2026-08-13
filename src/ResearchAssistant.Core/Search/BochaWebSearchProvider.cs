using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResearchAssistant.Core.Search;

/// <summary>
/// 博查（BoCha）Web Search API 实现。
/// 文档：https://open.bochaai.com —— POST /v1/web-search，Bearer 认证。
/// </summary>
public sealed partial class BochaWebSearchProvider : IWebSearchProvider
{
    private readonly HttpClient _httpClient;

    public BochaWebSearchProvider(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri("https://api.bochaai.com");
        _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults = 8, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/v1/web-search",
            new { query, count = maxResults, summary = true },
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"博查搜索 HTTP {(int)response.StatusCode}：{body}");
        }

        return Parse(body);
    }

    /// <summary>解析博查响应 JSON。独立为静态方法便于无网络单测。</summary>
    internal static IReadOnlyList<SearchHit> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 博查业务错误码：200 为成功，403 余额不足、401 密钥无效等
        if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
        {
            var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : "未知错误";
            throw new InvalidOperationException($"博查搜索返回错误码 {code.GetInt32()}：{msg}");
        }

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("webPages", out var webPages) ||
            !webPages.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var hits = new List<SearchHit>();
        foreach (var item in value.EnumerateArray())
        {
            var title = Normalize(item.GetProperty("name").GetString());
            var url = item.GetProperty("url").GetString() ?? "";
            // summary（大模型摘要）信息量大，优先；缺失时退化为 snippet
            var snippet = item.TryGetProperty("summary", out var s) && !string.IsNullOrWhiteSpace(s.GetString())
                ? Normalize(s.GetString())
                : Normalize(item.TryGetProperty("snippet", out var sn) ? sn.GetString() : "");
            hits.Add(new SearchHit(title, url, snippet, "Web"));
        }
        return hits;
    }

    /// <summary>博查返回的文本在词间夹带大量换行，规范化为正常空白。</summary>
    internal static string Normalize(string? text) =>
        text is null ? "" : WhitespaceRegex().Replace(text.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
