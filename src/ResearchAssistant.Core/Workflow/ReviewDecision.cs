using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResearchAssistant.Core.Workflow;

/// <summary>
/// 审校结论（Reviewer → Writer/Finalize 之间流转的消息类型）。
/// 刻意不用 JSON Schema 严格模式（DeepSeek 等 OpenAI 兼容服务支持不一），
/// 改为提示词约束 + 容错解析——这是接第三方模型时的实用经验。
/// </summary>
public sealed class ReviewDecision
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = "";

    /// <summary>被审校的草稿原文（不序列化，仅工作流内部传递）。</summary>
    [JsonIgnore]
    public string Draft { get; set; } = "";

    /// <summary>从审校 Agent 的自由文本输出中稳健解析结论：提取首个 JSON 对象反序列化；解析失败默认通过（防死循环）。</summary>
    public static ReviewDecision Parse(string reviewerOutput, string draft)
    {
        var start = reviewerOutput.IndexOf('{');
        var end = reviewerOutput.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                var decision = JsonSerializer.Deserialize<ReviewDecision>(
                    reviewerOutput[start..(end + 1)],
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (decision is not null)
                {
                    decision.Draft = draft;
                    return decision;
                }
            }
            catch (JsonException)
            {
                // 落入下方兜底
            }
        }
        return new ReviewDecision { Approved = true, Feedback = "", Draft = draft };
    }
}
