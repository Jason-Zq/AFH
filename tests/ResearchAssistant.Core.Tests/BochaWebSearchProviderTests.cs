using ResearchAssistant.Core.Search;

namespace ResearchAssistant.Core.Tests;

public class BochaWebSearchProviderTests
{
    // 基于真实 API 响应裁剪的样例（2026-08 实测结构）
    private const string SampleJson = """
        {
          "code": 200,
          "log_id": "abc123",
          "msg": null,
          "data": {
            "_type": "SearchResponse",
            "webPages": {
              "totalEstimatedMatches": 1000,
              "value": [
                {
                  "name": "The Microsoft Agent Framework\nHarness",
                  "url": "https://devblogs.microsoft.com/agent-framework/harness/",
                  "snippet": "stable,\nbatteries-included\nharness",
                  "summary": "Agent\nFramework\nships\na\nready-made\nharness",
                  "siteName": "Microsoft Learn"
                },
                {
                  "name": "另一条结果",
                  "url": "https://example.com/post",
                  "snippet": "只有 snippet 没有 summary 的结果",
                  "summary": null
                }
              ]
            }
          }
        }
        """;

    [Fact]
    public void Parse_正常响应_提取命中并规范化空白()
    {
        var hits = BochaWebSearchProvider.Parse(SampleJson);

        Assert.Equal(2, hits.Count);
        // 词间换行被规范化为单空格
        Assert.Equal("The Microsoft Agent Framework Harness", hits[0].Title);
        // 有 summary 时优先使用
        Assert.Equal("Agent Framework ships a ready-made harness", hits[0].Snippet);
        // 无 summary 时退化为 snippet
        Assert.Equal("只有 snippet 没有 summary 的结果", hits[1].Snippet);
        Assert.All(hits, h => Assert.Equal("Web", h.Origin));
    }

    [Fact]
    public void Parse_业务错误码_抛出带信息的异常()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => BochaWebSearchProvider.Parse("""{"code":403,"msg":"余额不足"}"""));
        Assert.Contains("403", ex.Message);
        Assert.Contains("余额不足", ex.Message);
    }

    [Fact]
    public void Parse_缺少数据字段_返回空列表()
    {
        Assert.Empty(BochaWebSearchProvider.Parse("""{"code":200,"data":{}}"""));
    }

    [Fact]
    public void Normalize_各种空白统一为单空格()
    {
        Assert.Equal("a b c", BochaWebSearchProvider.Normalize("a\n  b\t\nc"));
        Assert.Equal("", BochaWebSearchProvider.Normalize(null));
    }
}
