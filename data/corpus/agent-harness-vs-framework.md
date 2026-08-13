# Agent Harness 与 Agent Framework 的区别

## 一句话概括

Agent Framework 是"造 Agent 的 SDK"，Agent Harness 是"跑 Agent 的运行时"。Framework 提供抽象与构建块（Agent、工具、工作流、中间件），Harness 则把模型包装成一个能持续执行任务的运行体。

## 为什么需要 Harness

大语言模型本身只会生成文本。要让它调用工具、处理多步骤任务、记住自己做过什么、并持续运行直到任务完成，必须在模型外面包一层运行时——这层运行时就是 Harness。业界对多个独立构建的智能体系统（Claude Code、Codex CLI、Aider 等）的分析表明，它们不约而同收敛到了同一种 Harness 形态：模型决策逻辑只占很小一部分，绝大部分代码是权限管理、上下文管理、沙箱、工具路由和恢复机制等运行时基础设施。

## Harness 的典型职责

一个生产级 Harness 通常包含以下组件：

- **函数调用循环**：模型返回工具调用请求时，Harness 执行工具并把结果送回模型，循环直到任务完成；
- **历史持久化**：每次调用的对话历史自动保存，支持会话恢复；
- **上下文压缩**：对话逼近模型上下文窗口上限时，自动压缩/摘要历史，防止溢出；
- **待办列表与模式管理**：内置计划（plan）/执行（execute）模式，先规划再动手；
- **文件记忆**：把工作笔记写入文件系统，跨会话可用；
- **技能发现**：从文件系统发现领域技能（Skills）供模型按需取用；
- **工具审批**：危险操作触发人工批准流程；
- **失控保护**：限制最大循环轮次，防止 Agent 无限运行；
- **可观测性**：内置 OpenTelemetry 遥测，每一次模型调用、工具执行都可追踪。

## 在 Microsoft Agent Framework 中的对应关系

Microsoft Agent Framework 2026 年的版本中，Harness 以独立包形式提供：.NET 侧是 `Microsoft.Agents.AI.Harness`，通过 `IChatClient.AsHarnessAgent(options)` 一个调用即可为任意聊天客户端套上完整运行时；上述各项能力默认启用，并可通过 `HarnessAgentOptions` 上的一系列 `DisableXxx` 开关单独关闭。而 Framework 层提供的是 `AIAgent` 抽象、工作流编排（顺序/并发/移交/群聊）、中间件等构建块——Harness 正是用这些构建块组装出来的一个"开箱即用的最强默认配置"。

## 类比

如果把模型比作发动机，Framework 是造车的零部件体系（底盘、变速箱、方向盘），Harness 则是一辆可以直接上路的整车：点火、换挡、刹车、仪表盘全都内置。你可以继续改装它，但不需要从零件开始拼。
