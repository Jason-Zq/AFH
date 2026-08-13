# Agent 可观测性与 OpenTelemetry

## 为什么需要专门的可观测性

Agent 的行为是非确定性的：同样的输入可能走出不同的工具调用路径。出了问题（答错、死循环、账单爆炸）时，只看日志字符串几乎无法定位——你需要结构化的追踪数据还原每一次模型调用与工具执行。

## OpenTelemetry 与 GenAI 语义约定

OpenTelemetry（OTel）是云原生领域事实上的遥测标准，定义了 Trace / Span / Metrics 的数据模型与采集协议。针对生成式 AI，OTel 社区制定了 GenAI 语义约定，统一规定了 AI 系统的遥测属性命名：

- **Span 类型**：模型推理调用（chat/completion）、工具执行（tool execution）、Agent 运行整体各成 Span，父子嵌套还原调用树；
- **标准属性**：模型名称、输入/输出 token 数、温度等参数、工具名与参数、错误信息；
- **Token 统计**：输入/输出 token 作为标准属性记录，聚合成 Metrics 后可做成本核算与配额告警。

## 一次运行的典型 Trace

一个多步任务的 Trace 大致是：根 Span（Agent 运行）→ 若干子 Span 交替出现（模型调用 → 工具执行 → 模型调用 → …）→ 结束。有了这棵树，"Agent 为什么花了 40 秒、烧了 8 万 token"可以直接定位到具体某次检索或某个失控的循环。

## 在 Microsoft Agent Framework 中的对应关系

MAF 的 Harness 默认内置 OpenTelemetry 遥测：每次模型调用、每次工具执行自动产生符合 GenAI 语义约定的 Span，可接入 Jaeger、Azure Monitor 等任意 OTel 兼容后端，无需自行埋点。这使可观测性从"上线后补装"变成"开箱即有"。

## 实践要点

可观测性数据同时是评估体系的数据源：把 Trace 落库，就能离线做轨迹评估、回归集构建与成本分析——观测与评估应共用一条数据管道。
