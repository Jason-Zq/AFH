namespace ResearchAssistant.Web.Data;

/// <summary>语料文档：本地检索的内容源。data/corpus 的 .md 文件是种子，运行时以此表为准。</summary>
public sealed class Document
{
    public int Id { get; set; }
    public required string Name { get; set; }      // 文件名，如 agent-security.md
    public required string Content { get; set; }   // Markdown 全文
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>一次研究会话：提问、开关快照、终态与定稿报告。</summary>
public sealed class ResearchSession
{
    public long Id { get; set; }
    public required string Question { get; set; }
    public required string SwitchesJson { get; set; }  // HarnessFeatureSwitches 的 JSON 快照
    public string? ReportMarkdown { get; set; }        // 定稿报告；中途取消/失败时为 null
    public required string Status { get; set; }        // Completed / Cancelled / Failed
    public DateTimeOffset CreatedAt { get; set; }
}
