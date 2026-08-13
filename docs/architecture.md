# 架构讲解：多智能体研究助手

> 本项目是 Microsoft Agent Framework（MAF）+ Agent Harness 的学习/演示样本。
> 本文讲清三个问题：**整体长什么样、每一层为什么这样设计、数据是怎么流起来的。**

## 1. 系统总览

```
用户问题
   │
   ▼
┌─────────────────────────────────────────────────────────────┐
│ Vue 3 SPA（frontend/，Vite + TypeScript）                    │
│  AskBar / TimelinePanel / ReportPanel / HistoryPanel         │
│  fetch + ReadableStream 消费 SSE（POST 场景 EventSource/     │
│  axios 均不可用，原生 fetch 是行业标准做法）                   │
└───────────────────────────────┬─────────────────────────────┘
                                 │ POST /api/research（SSE 事件流）
                                 │ GET  /api/status /api/sessions[/id]
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│ ASP.NET Core WebAPI（src/ResearchAssistant.Web）             │
│  ResearchController ──▶ ResearchRunner（单例）               │
│  SessionsController ──▶ ResearchDbContext（EF Core/Npgsql）  │
└───────────────────────────────┬─────────────────────────────┘
                                 │ WorkflowEvent 流（逐 token）
                                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 研究工作流（src/ResearchAssistant.Core/Workflow）             │
│                                                             │
│   Researcher ──▶ Analyst ──▶ Writer ──▶ Reviewer ─┐         │
│   (HarnessAgent)              ▲          │        │         │
│        ▲                      │          ▼        │         │
│        │                      └── 打回修订 ◀── AddSwitch      │
│        │                      （最多 2 轮）  │通过             │
│        │                                   ▼                │
│        │                                Finalize ──▶ 最终报告 │
│        │                                                    │
│   工具：local_docs_search / web_search                       │
└───────┬──────────────────────────────┬──────────────────────┘
        ▼                              ▼
┌───────────────────┐        ┌────────────────────┐
│ 本地语料检索        │        │ 博查联网搜索         │
│ LocalCorpusSearch  │        │ BochaWebSearchProvider │
│ （BM25+CJK 分词）   │        │ （HTTP API 封装）     │
│ 语料来自 PostgreSQL │        │ api.bochaai.com    │
│ Documents 表       │        └────────────────────┘
└─────────┬─────────┘                  ▲
          ▼                            │
┌──────────────────────┐    LLM：DeepSeek（deepseek-chat）
│ PostgreSQL            │        经 OpenAI 兼容端点接入 IChatClient
│ Documents（语料）      │
│ ResearchSessions（会话）│   种子：data/corpus/*.md（启动时导入）
└──────────────────────┘
```

## 2. 项目结构与职责

| 项目 | 职责 | 关键文件 |
|---|---|---|
| `src/ResearchAssistant.Core` | 全部领域逻辑：检索、工具、Agent、工作流 | `Search/`、`Tools/`、`Agents/`、`Workflow/` |
| `src/ResearchAssistant.Web` | WebAPI 宿主：SSE 控制器、EF Core/PostgreSQL、SPA 静态托管 | `Controllers/`、`Data/`、`Services/ResearchRunner.cs` |
| `frontend` | Vue 3 + Vite + TypeScript 前端，产物输出到 Web 的 wwwroot | `src/components/`、`src/lib/sse.ts` |
| `tools/ResearchCli` | 命令行运行器，便于无浏览器验证与脚本化演示 | `Program.cs` |
| `tools/SmokeTest` | M0 冒烟测试（DeepSeek 连通性/工具调用/Harness） | `Program.cs` |
| `tests/ResearchAssistant.Core.Tests` | 编排与检索的单元测试（不依赖网络） | `*Tests.cs` |
| `data/corpus` | 30 篇 AI Agent 主题中文文档（PG 语料种子导入源） | `*.md` |

**设计原则：Core 里没有任何"展示"，Web 里没有任何"智能"**。所有编排逻辑都在 Core，Web 只做协议转换（工作流事件 → SSE），前端只做渲染。这样 CLI 和 Web 共用同一条代码路径，测试也只针对 Core。前后端唯一契约是 SSE 事件协议，C# record 与 TS interface 双端镜像。

## 3. 关键设计决策

### 3.1 为什么 Researcher 是 HarnessAgent，其他是普通 Agent？

`ResearchWorkflowFactory.cs` 里只有 Researcher 用 `chatClient.AsHarnessAgent(...)` 创建：

- **Researcher 的工作是开放式的**：要自己规划检索策略、决定调几次工具、用哪个通道。这正是 Harness 的价值场景——函数调用循环、todo 列表、plan/execute 模式、历史持久化全部由 Harness 托管，我们一行循环代码都不用写。
- **Analyst/Writer/Reviewer 的工作是一次性的**：收到输入、产出一轮输出就结束。给它们套 Harness 只会增加开销和不可控性，所以用 `chatClient.AsAIAgent(...)` 创建普通 Agent。

这是一个可推广的范式：**HarnessAgent 放在需要"自主探索"的节点，确定性强的节点用普通 Agent。**

### 3.2 为什么工作流是"图 + 条件路由"而不是顺序流水线？

M2 用过 `AgentWorkflowBuilder.BuildSequential`（顺序流水线），它无法表达"Reviewer 打回 → Writer 重写"。M4 改为显式建图：

```csharp
new WorkflowBuilder(researcherExecutor)
    .AddEdge(researcherExecutor, analystExecutor)
    .AddEdge(analystExecutor, writerExecutor)
    .AddEdge(writerExecutor, reviewerExecutor)
    .AddSwitch(reviewerExecutor, sw => sw
        .AddCase<ReviewDecision>(d => d?.Approved == true, finalizeExecutor)
        .AddCase<ReviewDecision>(d => d?.Approved == false, writerExecutor))
    .WithOutputFrom(finalizeExecutor)
    .Build();
```

`AddSwitch` 按消息类型+条件路由：Reviewer 产出 `ReviewDecision`，通过则去 Finalize 结束，打回则回到 Writer 形成回环。

**防死循环**：`ReviewerExecutor` 用工作流共享状态（`context.ReadStateAsync/QueueStateUpdateAsync`，作用域 `ResearchFlow`）记录修订次数，达到 `MaxRevisions = 2` 强制通过。循环必须有刹车，这是多智能体系统里最容易被忽略的工程细节。

### 3.3 Reviewer 的结构化输出：提示词 JSON + 容错解析

Reviewer 需要输出"通过/打回 + 意见"。没有采用 `ChatResponseFormat.ForJsonSchema`（不同模型厂商对 schema 约束解码支持不一），而是：

1. 提示词里明确规定"只输出一个 JSON 对象"；
2. `ReviewDecision.Parse` 截取第一个 `{` 到最后一个 `}` 之间的内容做反序列化；
3. **解析失败默认判为通过**——审校环节宁可放过瑕疵，也不能让整个流程卡死。

这对应语料库 `structured-output.md` 里的"可靠性阶梯"：提示词约定 → JSON 模式 → Schema 约束解码，按厂商支持度保守选择。

### 3.4 流式事件是怎么从 Agent 内部冒泡到浏览器的

链路分三段：

1. **Executor 内部**：`StreamingAgentExecutor.RunAndForwardAsync` 用 `agent.RunStreamingAsync` 逐增量运行，每个增量通过 `context.AddEventAsync(new AgentResponseUpdateEvent(Id, update))` 转发为工作流事件——**自定义 Executor 包装 Agent 时，框架不会自动转发流式事件，必须显式 AddEventAsync**。
2. **服务端**：`ResearchRunner.RunAsync` 用 `InProcessExecution.RunStreamingAsync(workflow, question)` 启动并把事件以 `IAsyncEnumerable` 吐出；`ResearchController` 把每个 `WorkflowEvent` 映射为一条 SSE 帧（`event:` 类型 + `data:` JSON），逐帧 `WriteAsync + FlushAsync` 并设置 `X-Accel-Buffering: no`。取消语义：浏览器 `AbortController` 中断 → `HttpRequestAborted` 触发 → 工作流取消。
3. **前端**：`sse.ts` 用原生 fetch + ReadableStream 逐行解析 SSE 帧（POST 不支持 EventSource，axios 浏览器端拿不到流式响应体，原生 fetch 是此场景的行业标准），`researchStore.ts` 把事件流构建为响应式 UI 状态（执行者分块/工具调用/报告），Vue 组件纯渲染。

### 3.8 持久化：语料与会话进 PostgreSQL

`Documents`（语料）与 `ResearchSessions`（会话/报告）两张表，EF Core Code First + Npgsql：

- **启动自举**：`DbInitializer` 先 `Database.Migrate()` 自动建表，再在语料表为空时从 `data/corpus/*.md` 种子导入——`dotnet run` 即可跑通，无需手工 SQL；
- **检索数据源切换**：`LocalCorpusSearch` 新增"内存文档列表"重载，Web 侧从 Documents 表读全文建索引，CLI 仍走文件目录版（同一套切块/BM25 逻辑）；
- **会话落库**：`ResearchRunner` 在完成/取消/失败三种终态下都写入会话记录（含开关快照与定稿报告），前端"历史会话"面板可回放任一报告；
- **演进路线**：内存 BM25 → PG 全文检索 → pgvector 混合检索（详见 [database.md](database.md)）。

### 3.5 本地检索为什么是 BM25 而不是向量？

DeepSeek 没有 embedding API，向量 RAG 需要引入第二家模型供应商，违背"单一 LLM 供应商"的简化目标。于是实现了轻量 BM25（k1=1.5，b=0.75）：

- **分词**（`LocalCorpusSearch.Tokenize`）：连续 CJK 字符切成二元组（bigram），连续拉丁字母/数字整词保留。中文无空格分词问题用 bigram 是性价比最高的近似。
- **切块**（`LocalCorpusSearch.Chunk`）：按 Markdown 标题与段落切分，保留所属标题作为上下文前缀，小于 30 字的碎片并入前块。
- **可替换**：语料库 `hybrid-search-and-rerank.md` 就是讲怎么升级成"向量+BM25 混合检索"的，接口边界（`SearchHit` 列表）已经留好。

### 3.6 搜索供应商为什么包一层接口？

`IWebSearchProvider` 只有 `SearchAsync(query, maxResults)` 一个方法，`BochaWebSearchProvider` 是其博查实现。换 Tavily/SerpAPI 只需新增一个实现类，工具层（`ResearchTools.CreateWebSearchTool`）与上层完全不感知。解析逻辑 `Parse(string json)` 是 `internal static` 纯函数，配合 `InternalsVisibleTo` 可直接单测，不需要 mock HTTP。

### 3.7 Harness 特性开关

`HarnessFeatureSwitches`（待办列表/plan-execute 模式/文件记忆/技能发现）映射到 `HarnessAgentOptions` 的 `DisableXxx` 反向开关，Web 首页有四个复选框实时切换。注意两个细节：

- `DisableWebSearch = true` 是**写死的**：Harness 自带的联网搜索是服务端托管工具，DeepSeek 不支持，我们用自研博查工具替代；
- 文件记忆默认**关**：开了会在工作目录写 `agent-file-memory/`，演示时手动开启观察。

## 4. 一次完整请求的数据流

以"Agent 工作流的检查点机制有什么用？"为例：

1. 用户回车 → `AskBar.vue` 调 `streamResearch()` → `POST /api/research` → `ResearchController` → `ResearchRunner.RunAsync(question, switches, ct)`；
2. `ResearchWorkflowFactory.Build(...)` 按当前开关构建工作流图；
3. `InProcessExecution.RunStreamingAsync` 启动，字符串输入路由到起点 `ResearcherExecutor`；
4. Researcher（HarnessAgent）规划检索 → 调 `local_docs_search` 命中 `workflow-checkpointing.md` 等 → 视情况补 `web_search` → 产出带来源标注的研究笔记；
5. Analyst 把笔记提炼成"核心结论 + 论据 + 分歧"；
6. Writer 写成完整 Markdown 报告；
7. Reviewer 输出 JSON 结论。打回则带着 `feedback` 回到 Writer（步骤 6 重来，最多 2 轮）；通过则进 Finalize；
8. Finalize 的返回值经 `WithOutputFrom` 成为 `WorkflowOutputEvent`；
9. 浏览器把报告渲染到右栏，下载按钮可导出 `.md`；会话记录（问题+开关快照+报告）已写入 PostgreSQL，历史面板可回放。

全程每个 Agent 的逐 token 输出、每次工具调用的参数与结果，都实时显示在左栏"协作过程"面板。

## 5. 配置与密钥

密钥两种配法二选一（Web 项目）：**appsettings.json**（`DeepSeek:ApiKey` / `Bocha:ApiKey`，已 gitignore，模板见 `appsettings.json.example`）或**环境变量**（优先级更高，.NET 配置系统默认行为）。CLI 仅支持环境变量。

| 配置键（环境变量） | 用途 | 缺省行为 |
|---|---|---|
| `DeepSeek:ApiKey`（`DEEPSEEK_API_KEY`） | DeepSeek API 密钥 | 必填，缺失时报友好错误 |
| `DEEPSEEK_ENDPOINT` | 覆盖端点 | `https://api.deepseek.com/v1` |
| `DEEPSEEK_MODEL` | 覆盖模型 | `deepseek-chat` |
| `Bocha:ApiKey`（`BOCHA_API_KEY`） | 博查搜索密钥 | 必填 |

密钥只放 `.env` / `appsettings.json`（均已 gitignore）。任何代码、文档、git 历史中都不得出现密钥。

## 6. 已知边界（诚实清单）

- **Reviewer 的 JSON 靠提示词约束**，模型极小概率输出非 JSON → 容错解析默认通过（见 3.3）；
- **无 checkpoint 持久化**：运行中的流程取消后不可续跑（对应语料 `workflow-checkpointing.md`，是 MAF 的进阶能力，本项目未启用）；但定稿结果已落库，历史会话可回放；
- **BM25 无语义召回**：同义改写查不到的文档就是查不到，PG 全文/pgvector 是预留演进路线（见 [database.md](database.md)）；
- **无鉴权**：API 对任何能访问端口的人开放，仅限内网/本地演示；
- **单例运行器**：`ResearchRunner` 单例复用语料索引，并发研究会话各自独立但共享底层资源，未做并发隔离设计。
