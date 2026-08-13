namespace ResearchAssistant.Core.Workflow;

/// <summary>
/// Researcher（HarnessAgent）的特性开关。Harness 的各项默认能力可独立禁用，
/// 这些开关在 Web UI 上呈现为复选框，用于演示"有没有 Harness 能力"的行为差异。
/// </summary>
public sealed record HarnessFeatureSwitches
{
    /// <summary>待办列表（Harness 内置的任务追踪）。默认开。</summary>
    public bool TodoList { get; init; } = true;

    /// <summary>plan / execute 双模式（Plan-and-Execute 行为）。默认开。</summary>
    public bool PlanExecuteMode { get; init; } = true;

    /// <summary>文件记忆（写入 agent-file-memory 目录）。默认关，避免磁盘留痕，演示时手动开。</summary>
    public bool FileMemory { get; init; }

    /// <summary>技能发现（从工作目录发现 Skills）。默认开。</summary>
    public bool SkillDiscovery { get; init; } = true;
}
