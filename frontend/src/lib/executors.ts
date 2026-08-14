/** Executor 节点的展示元数据（与后端 ResearchExecutors.cs 的固定节点一一对应）。 */
export const EXECUTOR_LABELS: Record<string, string> = {
  Researcher: '研究员',
  Analyst: '分析师',
  Writer: '撰稿人',
  Reviewer: '审校人',
  Finalize: '定稿',
}

/** 主流水线顺序（Finalize 不出现打回循环，不列入阶段条）。 */
export const STAGE_ORDER = ['Researcher', 'Analyst', 'Writer', 'Reviewer'] as const
