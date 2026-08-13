# ReAct 模式：推理与行动的交替

## 定义

ReAct（Reasoning + Acting）是谷歌研究团队在 2022 年提出的智能体架构模式。其核心思想是：让语言模型交替生成"思考"（Thought）与"行动"（Action），通过观察行动结果（Observation）不断推进任务，而不是一次性给出最终答案。

## 工作流程

一个典型的 ReAct 循环如下：

1. **Thought（思考）**：模型分析当前状态，推理下一步该做什么；
2. **Action（行动）**：模型决定调用某个工具，并给出参数；
3. **Observation（观察）**：运行时执行工具，把结果返回给模型；
4. 回到第 1 步，直到模型判断信息充分，输出最终答案（Final Answer）。

## 示例

问题："2026 年 Build 大会上微软发布了哪些 Agent 相关组件？"

- Thought：我需要搜索最新信息。
- Action：web_search("Build 2026 微软 Agent 发布")
- Observation：[搜索结果列表……]
- Thought：结果提到 Agent Harness 与 Hosted Agents，信息充分。
- Final Answer：……

## 优势与局限

**优势**：

- 思考轨迹外显，可解释性强，便于调试；
- 与工具调用天然契合，是现代函数调用 Agent 的思想源头；
- 实现简单，一个循环加工具路由即可。

**局限**：

- 没有全局规划，容易"走一步看一步"，在长任务上可能绕路；
- 每一步都依赖模型自觉，缺乏强制的进度管理（这正是后来 Harness 内置待办列表与计划模式要解决的问题）；
- 循环缺乏刹车时会失控，工程上必须设置最大轮次。

## 与其他模式的关系

ReAct 是"无规划的反应式"基线；Plan-and-Execute 在其上增加了显式的全局计划；而现代 Agent Harness（如 Microsoft Agent Framework 的 HarnessAgent）则把 ReAct 循环与规划、记忆、审批、遥测打包成默认配置，可以视为 ReAct 思想的工业化形态。
