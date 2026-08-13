# ResearchAssistant 实施方案

> 版本：v1.1（2026-08-13）· 状态：M0–M5 完成；M6（前后端分离：Vue3+TS SPA + WebAPI/SSE + PostgreSQL 持久化）实施完成，待端到端联调

## 1. 项目定位

学习/演示用试验品，两个目标并重：

1. **学透 Microsoft Agent Framework（.NET 版）本身**——重点是 2026 年 GA 的 **Agent Harness** 运行时与多智能体编排能力；
2. **演示一个完整的多智能体应用场景**——多智能体研究助手（检索 → 分析 → 写报告）。

路线：**先做一个场景做透**，后续再扩展其他场景（代码审查流水线、客服工单流转、知识库问答）。

## 2. 技术选型（已定）

| 维度 | 决策 | 备注 |
|---|---|---|
| 框架 | Microsoft Agent Framework 1.17.0（`Microsoft.Agents.AI` / `.Harness` / `.Workflows` / `.OpenAI`） | 2026-04 GA 1.0；Build 2026 宣布 Harness 与编排模式稳定 |
| 语言/运行时 | C# / .NET 10（本机 SDK 10.0.301） | 框架要求 .NET 9+ |
| 模型 | DeepSeek（OpenAI 兼容端点 `https://api.deepseek.com`） | 默认 `deepseek-chat`；工具调用兼容性需在 M0 验证 |
| 联网搜索 | 博查 BoCha Web Search API | 封装为 `AIFunction`，接口抽象留 Tavily 切换位 |
| 本地检索 | 自研轻量 BM25 关键词检索 | DeepSeek 无 embedding 接口，向量 RAG 留作扩展 |
| UI | Blazor Server | SignalR 天然适配工作流事件流式推送 |
| 测试 | xUnit + 假 `IChatClient`（无网络单测） | 重点覆盖检索、工具、编排胶水层 |

## 3. 调研结论（关键事实）

- **Harness 定位**：模型只生成文本；让它调工具、跑多步任务直到完成所需的运行时就是 Harness。MAF 1.0 GA（2026-04-02），Harness 与 Foundry Hosted Agents 于 Build 2026（6 月）宣布、8 月初正式 GA。
- **.NET API**：`chatClient.AsHarnessAgent(options)` 一个调用接入。默认启用：函数调用循环、逐次调用的历史持久化、上下文压缩、待办列表（plan/execute 模式）、文件记忆、技能发现、工具审批中间件、OpenTelemetry。`HarnessAgentOptions` 提供 `DisableWebSearch / DisableTodoProvider / DisableFileMemory / DisableAgentModeProvider / DisableAgentSkillsProvider / DisableOpenTelemetry / DisableCompaction` 等单独开关。部分选项带 `[Experimental]` 标记（编译需放行对应诊断 ID）。
- **注意**：Harness 默认启用的 `HostedWebSearchTool` 是**服务端托管工具**，DeepSeek 不支持 → 必须 `DisableWebSearch = true`，用自研博查函数工具替代（本身也是好教材）。
- **编排 API**：`AgentWorkflowBuilder.BuildSequential / BuildConcurrent / CreateHandoffBuilderWith / CreateGroupChatBuilderWith`；流式执行：`InProcessExecution.RunStreamingAsync(workflow, messages)` → `run.WatchStreamAsync()` 逐事件（`AgentResponseUpdateEvent` 含执行者 ID 与文本增量、`WorkflowOutputEvent`、`WorkflowErrorEvent`、`ExecutorFailedEvent`）。
- **DeepSeek 接入**：`new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = new Uri("https://api.deepseek.com") })`，`.GetChatClient(model).AsAIAgent()`。已知坑：旧版组合工具调用曾报 400（林德熙有记录），备选方案为 `Microsoft.Extensions.AI.DeepSeek`（需核实可用性）。M0 最先排雷。

## 4. 架构设计

```
ResearchAssistant.sln
├─ src/ResearchAssistant.Core/        # Agent 定义、工作流编排、检索工具（纯类库，可单测）
├─ src/ResearchAssistant.Web/         # Blazor Server（实时协作面板 + 报告展示）【M3】
├─ tools/SmokeTest/                   # M0 技术验证控制台（事后保留为诊断工具）
├─ data/corpus/                       # 本地文档库（约 30 篇 AI Agent 技术主题 Markdown）【M1 起逐步填充】
├─ tests/ResearchAssistant.Core.Tests/  # xUnit 单测【随 Core 同步建设】
└─ docs/                              # plan.md（本文）/ architecture.md / tutorial.md / demo-script.md【M5】
```

### 4.1 Agent 编排（Sequential 流水线）

```
用户提问 → Researcher（HarnessAgent：联网搜索 + 本地库检索两个工具）
        → Analyst（提炼要点、交叉印证、标注来源）
        → Writer（撰写结构化研究报告）
        → Reviewer（审校，不达标打回 Writer 修订，演示工作流循环）【M4】
```

- **Researcher 用 HarnessAgent**：todo 规划、上下文压缩、工具审批等 Harness 默认能力在此最有戏剧性；演示时开关对比即是学习素材。
- Analyst / Writer 先用普通 `ChatClientAgent`，突出"HarnessAgent 与裸 Agent 的差异"这一教学点。
- Reviewer 修订循环采用官方 07_WriterCriticWorkflow 同款模式（条件边/循环）。

### 4.2 检索双通道

- **联网**：博查 `POST https://api.bochaai.com/v1/web-search`（Bearer 认证），返回标题/摘要/URL 列表 → 封装 `WebSearchTool`。
- **本地**：`data/corpus/` 的 Markdown 语料 → 轻量 BM25 排序 → 封装 `LocalDocsSearchTool`。
- 两工具挂到 Researcher，由模型自主决定何时调用哪个。

### 4.3 本地语料库

主题：**AI Agent 技术**（ReAct、Plan-and-Execute、多智能体编排模式、RAG、工具调用、评估方法、Harness 概念等约 30 篇 Markdown）。演示问题示例："Agent Harness 和 Agent Framework 有什么区别？"——本地库与联网结果互相印证，最能体现双通道价值。

### 4.4 Blazor UI（M3）

- 输入研究问题 → 启动工作流；
- 实时面板：按执行者分栏流式渲染各 Agent 输出，工具调用高亮（名称 + 参数 + 结果摘要）；
- 报告区：Markdown 渲染 + 导出下载；
- 设置区：Harness 特性开关（todo / 文件记忆 / 压缩等），用于演示对比。

## 5. 里程碑

| 里程碑 | 内容 | 验证标准 |
|---|---|---|
| **M0 技术验证** | DeepSeek 连通 + 工具调用冒烟（tools/SmokeTest） | 控制台跑通一次带工具调用的问答 |
| **M1 核心能力** | 两个检索工具 + 单 HarnessAgent（CLI 验证） | Agent 自主检索本地库与网络并作答 |
| **M2 编排** | Researcher→Analyst→Writer 顺序工作流 + 流式事件 | 事件流可见各 Agent 依次输出与工具调用 |
| **M3 Web UI** | Blazor 实时面板、工具调用高亮、报告渲染/导出 | 浏览器完成一次完整研究 |
| **M4 深化** | Reviewer 修订循环、Harness 特性开关、语料补全至 ~30 篇 | 演示对比实验可跑 |
| **M5 文档** | architecture.md / tutorial.md / demo-script.md + 测试补齐 | 四件套齐、测试通过 |

## 6. 风险与对策

1. **DeepSeek 工具调用兼容性**（历史 400 问题）→ M0 最先验证；备选：`Microsoft.Extensions.AI.DeepSeek`，或降级为提示词内检索。
2. **Harness API 实验性标记** → 以 1.17.0 实际行为为准，必要时 `NoWarn` 放行实验诊断。
3. **托管网页搜索不适用于 DeepSeek** → 禁用并以自研博查工具替代（见 §3）。
4. **NuGet 国内镜像同步延迟** → 遇包缺失切官方源 `api.nuget.org`。
5. **密钥管理** → 一律走环境变量 / `appsettings.json`（已 gitignore），不进代码与文档。

## 7. 环境变量约定

| 变量 | 用途 |
|---|---|
| `DEEPSEEK_API_KEY` | DeepSeek 认证 |
| `DEEPSEEK_MODEL` | 可选，默认 `deepseek-chat` |
| `BOCHA_API_KEY` | 博查搜索认证 |

## 8. 参考资料

- [microsoft/agent-framework（GitHub）](https://github.com/microsoft/agent-framework)
- [InfoQ：Agent Framework Harness 与 Hosted Agents GA](https://www.infoq.com/news/2026/08/agent-framework-harness-ga/)
- [InfoQ 中文报道](https://www.infoq.cn/article/aDEJegvNSKwvue2JZ0yI)
- [林德熙：Microsoft Agent Framework 与 DeepSeek 对接](https://blog.lindexi.com/post/Microsoft-Agent-Framework-%E4%B8%8E-DeepSeek-%E5%AF%B9%E6%8E%A5.html)
- [林德熙：DeepSeek 工具调用 400 错误](https://blog.lindexi.com/post/dotnet-%E5%AF%B9%E6%8E%A5-DeepSeek-%E6%A8%A1%E5%9E%8B%E5%B7%A5%E5%85%B7%E8%B0%83%E7%94%A8%E6%97%B6-400-%E9%94%99%E8%AF%AF.html)
- [博查开放平台](https://open.bochaai.com/)
