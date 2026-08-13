# 演示脚本：多智能体研究助手（10 分钟版）

> 给观众现场演示的台词 + 操作脚本。按时间轴执行，括号内是预期画面与讲解要点。
> 预备：`.env` 已配好两个 key；`dotnet build` 通过；语料库 30 篇就位。

## 开场（1 分钟）

**台词**："这是用微软新发布的 Agent Framework Harness 做的多智能体研究助手。四个 Agent 分工——研究员、分析师、撰稿人、审校人——协作回答一个研究问题，全程可视化。"

**操作**：浏览器打开 `http://localhost:5199`。

（讲解点：页面顶部一句话说清架构——Researcher → Analyst → Writer ⇄ Reviewer 图编排，本地文档库 30 篇 + 博查联网双通道。）

## 第一幕：主流程（3 分钟）

**操作**：输入问题 → `Agent 工作流的检查点机制有什么用？和普通无状态请求有什么区别？` → 点"开始研究"。

**台词**（趁 Researcher 工作时讲）：

- "左栏第一个方块是 Researcher，它是唯一的 **HarnessAgent**——规划、工具调用循环都由 Harness 托管。"
- （出现 `local_docs_search` 工具调用时）"它先查本地文档库——命中了 `workflow-checkpointing.md` 等语料。"
- （若出现 `web_search`）"本地不够它自己决定联网补充——这个'自己决定'就是 Harness 的函数调用循环，不是我们写死的分支。"

（Analyst 出现时）"分析师只基于研究笔记提炼论点，证据不足会明说。"

（Writer 出现时）"撰稿人开始成稿，注意它在标注来源。"

（Reviewer 出现时）"审校人按三条标准评审：结构完整、论述有据、无编造。它输出的是结构化 JSON 结论。"

（报告出现在右栏）"通过！右栏就是定稿的 Markdown 报告，可以下载。"

## 第二幕：审校打回（2 分钟，可选，需要运气）

审校是否打回取决于模型判断，不保证现场出现。**两种方案**：

- **方案 A（现场碰运气）**：换个更宽泛的问题，如 `聊聊多智能体编排`，Reviewer 更可能因"来源不足"打回。若出现 Writer 第二次出现，立刻指着讲："看，Writer 被执行了第二轮——这就是图编排的回环，顺序流水线做不到。"
- **方案 B（保底讲代码）**：打开 `tests/ResearchWorkflowTests.cs`，讲"审校打回一次_Writer执行两次"这个测试怎么用假客户端确定性地复现回环；再指 `ReviewerExecutor` 里的 `MaxRevisions = 2`："循环必须有刹车，共享状态记修订次数。"

## 第三幕：Harness 开关对比（2 分钟）

**操作**：勾掉"plan/execute 模式"和"待办列表"，再问一个检索型问题，如 `BM25 和向量检索怎么融合？`

**台词**："这四个开关映射 Harness 的 `DisableXxx` 选项。关掉 plan/execute 后，注意 Researcher 的行为差异——少了显式的计划-执行分段，行为更'直筒'。Harness 的能力是可以逐项裁剪的。"

（讲解点：联网搜索开关不可见是因为写死禁用——Harness 自带的是服务端托管搜索，DeepSeek 不支持，我们用自研博查工具替代。）

## 第四幕：CLI 与测试（2 分钟）

**操作**：终端执行：

```bash
dotnet test        # 15 个测试全绿
dotnet run --project tools/ResearchCli -- "MCP 协议解决什么问题？"
```

**台词**：

- "编排逻辑有确定性测试——用假 IChatClient，不依赖网络，15 个测试一秒跑完。测的是我们的代码（编排结构），不是模型输出。"
- "同一条工作流代码，CLI 和 Web 共用。Core 项目里没有任何 UI 依赖。"
- "Web 端是正式的 WebAPI + Vue 前后端分离：SSE 流式接口、PostgreSQL 存语料和历史会话——左栏'历史会话'可以看到之前每次研究，点一下就能回放报告。"

## 收尾问答预案

| 观众可能问 | 回答要点 |
|---|---|
| 为什么不用向量检索？ | DeepSeek 没 embedding API；BM25+CJK bigram 是零依赖起点，接口留好了混合检索扩展位 |
| 换模型麻烦吗？ | `IChatClient` 抽象 + `DEEPSEEK_ENDPOINT/MODEL` 环境变量，换 OpenAI 兼容服务改配置即可 |
| 审校死循环怎么办？ | `MaxRevisions = 2` 强制通过；解析失败也默认通过——宁可放过瑕疵不能卡死流程 |
| 能持久化/断点恢复吗？ | MAF 有 checkpoint 能力，本项目未启用，见语料 `workflow-checkpointing.md` |
| 博查额度用完？ | `IWebSearchProvider` 换 Tavily 只需新增一个实现类 |
