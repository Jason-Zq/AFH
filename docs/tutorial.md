# 分步教程：从零复现多智能体研究助手

> 跟着本教程，你将从空目录一步步构建出本项目：DeepSeek + Microsoft Agent Framework Harness + 双通道检索 + 图编排工作流 + WebAPI（SSE）+ Vue 3 实时面板 + PostgreSQL 持久化。
> 每一步都给出"做什么、为什么、怎么验证"。完整代码已在仓库中，教程引用关键片段。

**前置条件**：.NET SDK 10、DeepSeek API key、博查 API key（约 2000 次免费额度）。

## Step 0：环境准备与冒烟测试（对应里程碑 M0）

**做什么**：建解决方案与项目骨架，写 30 行冒烟测试验证 DeepSeek 能通、工具调用能用、Harness 能跑。

```bash
dotnet new sln -n ResearchAssistant
dotnet new classlib -n ResearchAssistant.Core -o src/ResearchAssistant.Core -f net10.0
dotnet add src/ResearchAssistant.Core package Microsoft.Agents.AI.Harness --version 1.17.0
```

DeepSeek 走 OpenAI 兼容端点接入：

```csharp
var client = new OpenAIClient(
        new ApiKeyCredential(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")!),
        new OpenAIClientOptions { Endpoint = new Uri("https://api.deepseek.com/v1") })
    .GetChatClient("deepseek-chat")
    .AsIChatClient();
```

**为什么**：MAF 的一切都建立在 `IChatClient`（Microsoft.Extensions.AI 的统一抽象）上。先证明模型层可用，再往上盖楼。冒烟测试要覆盖三点：普通对话、函数调用（function calling）、`AsHarnessAgent` 全流程。

**验证**：`dotnet run --project tools/SmokeTest` → 3/3 通过。

**踩坑提示**：用 C# 本地函数做工具时工具名会被编译器改写（如 `_Main_g_GetWeather_1`），正式代码用 `AIFunctionFactory.Create` 显式命名。

## Step 1：双通道检索 + 单 HarnessAgent（M1）

**做什么**：
1. `LocalCorpusSearch`：BM25 + CJK bigram 分词，索引 `data/corpus/*.md`；
2. `IWebSearchProvider` / `BochaWebSearchProvider`：博查 API 封装；
3. `ResearchTools`：把两个检索能力包成 `AIFunction` 工具；
4. CLI 里创建一个带这两个工具的 HarnessAgent。

**为什么**：Harness 的核心价值是"函数调用循环托管"——Agent 自己决定调不调工具、调几次。给 Agent 装上两个工具，观察它自主规划"先查本地库、不够再联网"，是理解 Harness 最好的第一课。

关键代码（`ResearchTools.cs`）：

```csharp
public static AIFunction CreateLocalSearchTool(LocalCorpusSearch corpus) =>
    AIFunctionFactory.Create(
        ([Description("检索关键词，用中文或英文均可")] string query,
         [Description("返回结果条数")] int maxResults = 5) => FormatHits(corpus.Search(query, maxResults)),
        name: "local_docs_search",
        description: "检索本地权威文档库（AI Agent 主题中文文档）");
```

**验证**：`dotnet run --project tools/ResearchCli -- --single "ReAct 模式是什么？"`，观察输出中 `>>> 调用工具 local_docs_search(...)` 与 `<<< 工具返回`。

**踩坑提示**：Harness 自带 `HostedWebSearchTool` 是服务端工具，DeepSeek 不支持，必须 `DisableWebSearch = true` 并用自研工具替代。

## Step 2：多 Agent 顺序工作流（M2）

**做什么**：Researcher → Analyst → Writer 三个 Agent 串成顺序流水线，CLI 里流式展示每个 Agent 的输出。

**为什么**：单 Agent 什么都能干，但职责分离（检索/分析/写作）让每个 Agent 的提示词更短更专注，输出质量更高。MAF 的 Workflow 把"谁接谁"从提示词工程变成显式代码。

**验证**：CLI 提问后看到 `════ Researcher ════` → `════ Analyst ════` → `════ Writer ════` 依次输出，最后 Writer 产出带参考来源的完整报告。

## Step 3：WebAPI + SSE + Vue 实时面板（M3/M6）

**做什么**：后端加 `ResearchController`（`POST /api/research` 以 `text/event-stream` 逐事件推送）与 `SessionsController`（历史会话）；前端 Vue 3 + Vite + TypeScript SPA 用 fetch 流式消费；EF Core + Npgsql 把语料与会话历史落 PostgreSQL。

**为什么**：LLM 流式输出的行业标准是 SSE（OpenAI/Anthropic API 都是）。POST 场景下 EventSource 不可用、axios 浏览器端拿不到流式响应体，所以用**原生 fetch + ReadableStream** 逐行解析。技术关键：

1. `InProcessExecution.RunStreamingAsync` + `WatchStreamAsync()` 拿到工作流事件流；
2. 自定义 Executor 里 `context.AddEventAsync(new AgentResponseUpdateEvent(Id, update))` 把 Agent 的流式增量转发成工作流事件（框架不会自动转发）；
3. 控制器把每个工作流事件写成一条 SSE 帧（`event:` + `data:` + 空行），逐帧 `FlushAsync`，并设 `X-Accel-Buffering: no` 防代理缓冲；
4. 前端把事件流构建为响应式 UI 状态（执行者分块、工具调用、报告），取消 = `AbortController` → 服务端 `HttpRequestAborted`；
5. 持久化：启动时 `Database.Migrate()` 自动建表 + 语料种子导入，会话在终态落库。

**验证**：`curl -N -X POST localhost:5199/api/research -H 'Content-Type: application/json' -d '{"question":"..."}'` 看到逐帧事件；浏览器三栏面板实时更新；`psql` 里 `research_sessions` 有新行。

## Step 4：Reviewer 审校回环 + Harness 开关（M4）

**做什么**：顺序流水线升级为图：Writer 后面挂 Reviewer，`AddSwitch` 按 `ReviewDecision.Approved` 路由——打回回到 Writer，通过进 Finalize。同时把 Harness 的四个特性做成 UI 开关。

**为什么**：这是"工作流"超越"流水线"的分水岭。三个工程要点：

1. **自定义 Executor 包装 Agent**：`[MessageHandler]` 标记处理方法，类必须是 `partial`，且要引用 `Microsoft.Agents.AI.Workflows.Generators` 包（源生成器单独成包，不引用会报 `CS0534: 不实现 ConfigureProtocol`）；
2. **循环必须有刹车**：`context.QueueStateUpdateAsync` 记修订计数，达到上限强制通过；
3. **结构化输出保守做**：提示词约定 JSON + 容错解析，解析失败默认通过。

**验证**：
- 单元测试：`dotnet test`——两个图编排测试（一次通过；打回一次 Writer 执行两轮）用 FakeChatClient 跑，不需要网络；
- CLI 实测：观察 `════ Reviewer ════` 输出 `{"approved": true/false, ...}`，打回时 Writer 再次出现。

## Step 5：文档与演示（M5）

补齐四件套：[架构讲解](architecture.md)、本教程、[演示脚本](demo-script.md)、[README](../README.md)。

## 常见坑速查

| 症状 | 原因 | 解法 |
|---|---|---|
| `CS0534 不实现 ConfigureProtocol` | 自定义 Executor 缺源生成器 | `dotnet add package Microsoft.Agents.AI.Workflows.Generators`，类改 `partial` |
| 最终输出事件触发两次 | 显式 `YieldOutputAsync` 与 `WithOutputFrom` 重复 | 二选一（推荐靠 `WithOutputFrom` 自动 Yield 返回值） |
| 旧教程里的 `RunStreamingAsync(workflow, List<ChatMessage>)` + `TurnToken` 报错 | 自定义图起点收 `string` | 直接传字符串输入，不需要 `TurnToken` |
| DeepSeek 调 Harness 报 400/工具错误 | `HostedWebSearchTool` 不被支持 | `DisableWebSearch = true` |
| `CS1626 无法在含 catch 的 try 中生成值` | `yield return` 与 catch 同层 | 手动迭代：内层 try/catch 定状态、外层 yield（见 `ResearchRunner.RunAsync`） |
| SSE 浏览器收不到流（一次性到齐） | 代理/中间件缓冲 | 响应头 `X-Accel-Buffering: no` + 每帧 `FlushAsync` |
| `MSB3027 DLL 被锁定` | 后台还跑着 `dotnet run` 的 Web | 停掉再 build（注意 TaskStop 可能只杀外壳，残留进程要 `taskkill`） |
| 博查返回文本词间有 `\n` | API 特性 | `BochaWebSearchProvider.Normalize` 折叠空白 |
