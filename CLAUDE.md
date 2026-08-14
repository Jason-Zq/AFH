# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目定位

学习/演示项目：用 Microsoft Agent Framework 1.17.0（.NET）+ Agent Harness 构建的多智能体研究助手。Researcher（HarnessAgent，本地 BM25 + 博查联网双通道检索）→ Analyst → Writer ⇄ Reviewer 图编排工作流（审校打回回环，最多修订 2 轮）。前后端分离：ASP.NET Core WebAPI（SSE 流式）+ Vue 3 SPA + PostgreSQL 持久化。

## 常用命令

```bash
# 后端
dotnet build                              # 构建（ResearchAssistant.slnx，含 Core/Web/Tests/Cli/SmokeTest）
dotnet test                               # 全部测试（15 个，离线，假 IChatClient 无网络依赖）
#   ⚠ 后端运行中勿在根目录 dotnet test：slnx 连 Web 一起构建会撞 DLL 锁；改测 dotnet test tests/ResearchAssistant.Core.Tests
dotnet test --filter "FullyQualifiedName~ResearchWorkflowTests"   # 跑单个测试类/方法
dotnet run --project src/ResearchAssistant.Web --urls http://localhost:5199
#   ⚠ 必须带 --urls 5199：launchSettings.json 默认 5292，会导致 Vite 代理（→5199）失效

# 前端（frontend/ 是独立 npm 项目，不在 slnx 中）
cd frontend && npm run dev                # Vite 开发服务器，/api 代理到 5199（5173 被占会自动换端口）
cd frontend && npm run build              # 产物直接输出到 src/ResearchAssistant.Web/wwwroot

# 数据库（EF Core Code First，PostgreSQL）
dotnet ef migrations add <名称> -p src/ResearchAssistant.Web    # 实体变更后生成迁移
#   ⚠ 全局工具需 10.x（dotnet tool update -g dotnet-ef），8.x 报版本不兼容
# 无需手工 update：应用启动时 Database.Migrate() 自动建库建表 + 语料种子导入（仅当 Documents 为空）

# CLI（无浏览器验证工作流，仅支持环境变量）
set -a; source .env; set +a && dotnet run --project tools/ResearchCli -- "研究问题"

# M0 冒烟（真实调 DeepSeek 验证连通/工具调用/Harness 三假设，需联网与 .env）
set -a; source .env; set +a && dotnet run --project tools/SmokeTest
```

## 分层铁律

**Core 里没有任何"展示"，Web 里没有任何"智能"，前端只做渲染。**

- `src/ResearchAssistant.Core/`：全部领域逻辑（检索、工具、Agent、工作流编排），纯类库，CLI 与 Web 共用同一代码路径，测试只针对 Core
- `src/ResearchAssistant.Web/`：协议转换（工作流事件 → SSE 帧）+ EF Core 持久化 + SPA 静态托管 + 后台管理 REST（`Admin*Controller`，无鉴权仅限本地演示）
- `frontend/`：Vue 3 + Vite + TypeScript + Element Plus + Pinia + Vue Router；`build.outDir` 指向 Web 的 wwwroot，产物单向流入

前后端唯一契约是 SSE 事件协议：`Controllers/ResearchController.cs` 的 C# record 与 `frontend/src/types/events.ts` 的 TS interface **双端镜像**——改事件结构必须两端同步。

## 前端架构要点（M7 重规划后）

- **状态全在 Pinia**：`stores/research.ts` 是 SSE 事件 → UI 的唯一状态机（`apply()` 逐事件构建 `blocks`，取消走 `notice` 而非 `error`）；组件不直接碰 SSE。另两个 store：`sessions.ts`（历史列表）、`status.ts`（语料计数）
- **三路由**（前台）：`/` 空态首页（提问入口）、`/run` 工作区（阶段条 + 过程流/报告双栏）、`/sessions/:id` 历史报告页；组件分层 = `views/` 组装 + `components/` 哑组件（props in，尽量不进 store）
- **Element Plus 按需引入**（unplugin-vue-components）：模板直接用 `el-*`，无需 import；`src/components.d.ts`、`src/auto-imports.d.ts` 是生成文件，勿手改
- **主题**：`useTheme` 切 `html.dark` 类（EP dark/css-vars + `style.css` 自定义变量双套），默认深色
- **构建即类型检查**：`npm run build` 先跑 vue-tsc 后 vite build，改动后必须过

## 后台管理要点（M8）

- **入口**：前台侧栏齿轮按钮 → `/admin`；`App.vue` 按路由前缀切壳（前台 AppSidebar / 后台 AdminLayout 互斥），admin 路由整组动态导入
- **后端三组端点**：`AdminOpsController`（status/rebuild-index/reseed-corpus/logs）、`AdminDocumentsController`（CRUD）、`AdminSessionsController`（列表/单删/批删）；列表端点一律不返回全文字段（预览 200 字 / reportLength）
- **索引一致性**：语料一切写操作（增删改、种子重导）自动调 `ResearchRunner.RebuildCorpusAsync()`（SemaphoreSlim 串行化 + Volatile 引用替换，进行中研究持旧索引）；前端写操作后连带刷新 `status` store 让前台侧栏块数同步
- **危险操作闸门**：`ResearchRunner.IsRunning`（Interlocked 计数）为真时 replace 重导返回 409；前端 Ops 页同步禁用
- **日志**：`InMemoryLogStore` 环形缓冲（1000 条，重启清空）+ 自研 ILoggerProvider，零依赖；要持久化再换 Serilog
- **record 校验坑**：位置 record 的验证特性必须挂参数本身（`record X([Required] string Name)`），挂 `[property:]` 运行期直接 InvalidOperationException

## 关键架构决策（改代码前必读）

- **HarnessAgent 只给 Researcher**：开放式探索节点（自主规划、工具调用循环）用 `AsHarnessAgent`；Analyst/Writer/Reviewer 是一次性输入输出，用普通 `AsAIAgent`。
- **`DisableWebSearch = true` 是写死的**：Harness 自带的 `HostedWebSearchTool` 是服务端托管工具，DeepSeek 不支持；联网走自研博查工具（`IWebSearchProvider` 接口，可换 Tavily）。
- **审校回环**：`WorkflowBuilder.AddSwitch` 按 `ReviewDecision.Approved` 路由；共享状态（`QueueStateUpdateAsync`，作用域 `ResearchFlow`）计修订次数，`MaxRevisions = 2` 强制通过防死循环；Reviewer 的 JSON 解析失败**默认通过**（宁可放过瑕疵不卡死流程）。
- **流式链路三段**：Executor 内 `context.AddEventAsync(new AgentResponseUpdateEvent(...))` 显式转发增量（框架不会自动转发）→ 控制器逐帧 `WriteAsync + FlushAsync`（`X-Accel-Buffering: no`）→ 前端原生 fetch + ReadableStream 解析（POST 场景 EventSource/axios 均不可用）。取消 = 浏览器 AbortController → `HttpRequestAborted`。
- **持久化**：`Documents`（语料，种子来自 `data/corpus/*.md`）+ `ResearchSessions`（问题/开关快照/定稿报告/状态）。Web 从 Documents 表建 BM25 索引，CLI 走文件目录——同一套 `LocalCorpusSearch` 切块逻辑两个 `Load` 重载。演进路线见 docs/database.md。

## MAF 踩坑备忘（都真实发生过）

| 症状 | 解法 |
|---|---|
| `CS0534 不实现 ConfigureProtocol`（自定义 Executor） | 引用 `Microsoft.Agents.AI.Workflows.Generators` 包 + 类声明 `partial`（源生成器单独成包） |
| `WorkflowOutputEvent` 触发两次 | `WithOutputFrom` 已自动 Yield 返回值，Executor 内不要再显式 `YieldOutputAsync` |
| `CS1626 无法在含 catch 的 try 中生成值` | `yield return` 与 catch 不能同层；用手动 `GetAsyncEnumerator` 循环（见 `ResearchRunner.RunAsync`） |
| 自定义图起点报 TurnToken 相关错误 | 起点收 `string`，直接传字符串输入，不用 TurnToken |
| `MSB3027 DLL 被锁定` / 端口被占 | 后台停止可能只杀外壳，残留 `ResearchAssistant.Web.exe` 要 `taskkill //F //IM ResearchAssistant.Web.exe` |
| 取消的研究落库成 `Completed` | 客户端断开时 MAF 监听流可能"安静结束"（不抛 OCE）；`RunAsync` finally 里按 `cancellationToken.IsCancellationRequested` 校正状态再落库 |
| 计数型闸门（如 `IsRunning`）卡真 | `Interlocked.Increment` 之后的所有代码必须在外层 try/finally 视野内，否则中途抛错 Decrement 永不执行 |

## 配置与密钥

密钥只存在于 `.env`（CLI）和 `src/ResearchAssistant.Web/appsettings.Local.json`（Web，模板见 `.example`），均已 gitignore。配置键：`ConnectionStrings:ResearchDb`、`DeepSeek:ApiKey`、`Bocha:ApiKey`（config 优先，环境变量兜底）。代码、文档、git 历史中不得出现真实密钥。

## 文档地图

`docs/` 下：spec.md（总规格书：功能/接口/数据/决策/踩坑/验证记录）、plan.md（实施方案与里程碑）、architecture.md（架构决策详情）、tutorial.md（分步复现 + 踩坑速查）、database.md（表结构 DDL 与迁移工作流）、demo-script.md（演示台词）。
