# SPEC：多智能体研究助手 · 开发与规格全记录

> 版本：v1.0（2026-08-13）
> 本文是项目的**总规格书 + 开发档案**：系统做什么、用什么技术、接口与数据契约、里程碑历程、关键决策与踩坑实录。
> 细节深挖请分流到：[架构讲解](architecture.md)｜[分步教程](tutorial.md)｜[数据库](database.md)｜[演示脚本](demo-script.md)｜[实施方案](plan.md)

## 1. 项目概述

学习/演示项目，两个目标并重：

1. **学透 Microsoft Agent Framework（.NET 版）**——重点是 Agent Harness 运行时与多智能体图编排；
2. **演示一个完整的多智能体应用**——研究助手：用户提一个研究问题，四个 Agent 分工协作（检索 → 分析 → 写作 → 审校），产出带来源标注的 Markdown 研究报告，全程实时可视化。

形态：前后端分离 Web 应用（Vue 3 SPA + ASP.NET Core WebAPI + SSE 流式 + PostgreSQL），另附 CLI 运行器。定位从"学习试验品"演进为"往产品形态养"（M6 起：正式 API 契约、历史会话、持久化）。

## 2. 功能规格

| # | 功能 | 说明 | 状态 |
|---|---|---|---|
| F1 | 发起研究 | 输入问题 → 四 Agent 图编排工作流执行 | ✅ |
| F2 | 实时过程面板 | 阶段条显示流水线进度与修订轮次；按执行者分块逐 token 流式渲染；工具调用折叠卡片（结果截断 600 字符）；自动滚动跟随 | ✅ |
| F3 | 双通道检索 | Researcher 自主决定调用本地 BM25 语料检索和/或博查联网搜索 | ✅ |
| F4 | 审校回环 | Reviewer 结构化评审（JSON），打回则 Writer 修订，最多 2 轮强制通过 | ✅ |
| F5 | Harness 特性开关 | 待办列表 / plan-execute 模式 / 文件记忆 / 技能发现，四项运行时切换 | ✅ |
| F6 | 报告渲染与下载 | marked 渲染 + DOMPurify 消毒 + 目录导航（h1–h3 锚点）+ Blob 下载 .md | ✅ |
| F7 | 历史会话 | 每次研究（完成/取消/失败）落库；侧栏列表展示近 50 条，点击进入 `/sessions/:id` 独立报告页回放 | ✅ |
| F8 | 语料管理 | 30 篇 AI Agent 主题中文文档，启动时自动种子导入 PG | ✅ |
| F9 | CLI 运行器 | 无浏览器跑同一工作流，脚本化演示/验证 | ✅ |
| F10 | 后台管理 | `/admin` 独立布局：系统状态仪表盘（10s 轮询）、语料 CRUD（写后自动重建索引）、会话浏览/删除（单个+批量）、运维操作（索引重建 / 种子重导 append/replace）、内存日志查看（级别过滤 + 自动刷新） | ✅ |

明确的**非目标**（边界，见 architecture.md §6）：无鉴权（仅限本地/内网演示）、无 checkpoint 断点续跑、无语义向量检索（演进路线预留）、并发未做隔离设计。

## 3. 技术栈规格

| 层 | 技术 | 版本 |
|---|---|---|
| 运行时 | .NET | 10（SDK 10.0.301） |
| Agent 框架 | Microsoft.Agents.AI.Harness / .OpenAI / .Workflows / .Workflows.Generators | 1.17.0 |
| LLM | DeepSeek `deepseek-chat`（OpenAI 兼容端点 `https://api.deepseek.com/v1`） | — |
| 联网搜索 | 博查 Web Search API（`IWebSearchProvider` 抽象，留 Tavily 切换位） | — |
| 本地检索 | 自研 BM25（k1=1.5, b=0.75）+ CJK bigram 分词 | — |
| Web 宿主 | ASP.NET Core WebAPI + Microsoft.AspNetCore.OpenApi | 10.0.11 |
| 持久化 | EF Core + Npgsql.EntityFrameworkCore.PostgreSQL（Code First，启动自动迁移） | 10.0.11 / 10.0.3 |
| 数据库 | PostgreSQL（本地实例，库名 `research_assistant`） | — |
| 前端 | Vue / Vite / TypeScript / Element Plus / Pinia / Vue Router / marked / dompurify | 3.5 / 8.2 / 6.0 / 2.14 / 4.0 / 5.2 / 18 / 3.4 |
| 测试 | xUnit + 假 IChatClient（离线） | 15 个测试 |
| 工具链 | Node v24 / npm 11 / dotnet-ef 10.0.11 | — |

## 4. 系统架构

```
Vue 3 SPA（frontend/，产物单向输出到 Web/wwwroot）
   │ POST /api/research（SSE）｜GET /api/status｜/api/sessions[/id]
   ▼
ASP.NET Core WebAPI（src/ResearchAssistant.Web）
   ResearchController / SessionsController / Admin*Controller → ResearchRunner（单例）
   │
   ▼ WorkflowEvent 流
研究工作流图（src/ResearchAssistant.Core/Workflow）
   Researcher ─▶ Analyst ─▶ Writer ─▶ Reviewer ─┐
   (HarnessAgent)            ▲          │ AddSwitch
                            └── 打回(≤2轮) ┤
                                通过 ▼
                              Finalize ─▶ WorkflowOutputEvent
   │
   ▼ 工具调用（Researcher 自主决策）
LocalCorpusSearch（PG Documents 表建索引）｜BochaWebSearchProvider
```

**分层铁律**：Core 无任何"展示"，Web 无任何"智能"（只做工作流事件→SSE 的协议转换），前端只渲染。CLI 与 Web 共用 Core 同一代码路径；测试只针对 Core。

## 5. 接口规格

### 5.1 REST 端点

| 端点 | 方法 | 请求体 / 参数 | 响应 |
|---|---|---|---|
| `/api/research` | POST | `ResearchRequest`（见 5.2） | `text/event-stream` SSE 事件流 |
| `/api/status` | GET | — | `{ corpusChunkCount: number }` |
| `/api/sessions` | GET | — | `SessionSummary[]`（近 50 条倒序：id/question/status/createdAt） |
| `/api/sessions/{id}` | GET | long id | `SessionDetail`（+ reportMarkdown/switches） |

后台管理（`/api/admin/*`，无鉴权——仅限本地演示；列表端点一律不返回 Content/ReportMarkdown 全文）：

| 端点 | 方法 | 说明 |
|---|---|---|
| `/api/admin/ops/status` | GET | 系统状态：dbConnected/文档数/会话数/已应用迁移/索引块数/运行时长/是否研究中/语料目录（DB 挂了也照常返回） |
| `/api/admin/ops/rebuild-index` | POST | 重建 BM25 索引（原子替换）→ `{ chunkCount, elapsedMs }` |
| `/api/admin/ops/reseed-corpus` | POST | `{ mode: "append" \| "replace" }` 种子重导；replace 在研究中返回 409 → `{ imported, skipped, deleted, chunkCount }` |
| `/api/admin/ops/logs` | GET | `?minLevel&take(≤1000)` 内存环形日志（容量 1000，重启清空），最新在前 |
| `/api/admin/documents` | GET/POST | 分页列表（`?page&pageSize&search`，ILIKE 搜 Name+Content，200 字预览）/ 新增（重名 409） |
| `/api/admin/documents/{id}` | GET/PUT/DELETE | 详情（全文）/ 更新 / 删除；写操作自动重建索引并回传 `corpusChunkCount` |
| `/api/admin/sessions` | GET | 分页列表（`?page&pageSize&status`，reportLength 代替全文） |
| `/api/admin/sessions/{id}` | DELETE | 删除单条（204/404） |
| `/api/admin/sessions/delete-batch` | POST | `{ ids: long[] }` 批量删除（≤500 条）→ `{ deleted }` |

开发期 OpenAPI 文档：`/openapi/v1.json`。

### 5.2 SSE 事件协议（前后端唯一契约，双端镜像）

`POST /api/research` 请求体：

```json
{ "question": "string", "todoList": true, "planExecuteMode": true, "fileMemory": false, "skillDiscovery": true }
```

响应帧格式 `event: <类型>\ndata: <JSON>\n\n`，逐帧 Flush，响应头 `X-Accel-Buffering: no`：

| event | data 载荷 | 含义 |
|---|---|---|
| `executor-start` | `{ id }` | 某执行者开始产出（executorId 变化时发一次） |
| `text` | `{ executorId, delta }` | 逐 token 文本增量 |
| `tool-call` | `{ executorId, name, args }` | 工具调用（args 为序列化 JSON 字符串） |
| `tool-result` | `{ executorId, text }` | 工具返回（截断 600 字符） |
| `report` | `{ markdown }` | 定稿报告（来自 WorkflowOutputEvent） |
| `error` | `{ message }` | 执行者失败 / 工作流异常 |
| `done` | `{}` | 正常收尾 |

**契约维护规则**：C# 端在 `ResearchController.WriteSseAsync`，TS 端在 `frontend/src/types/events.ts`——改协议必须两端同步。取消语义：浏览器 `AbortController` → 服务端 `HttpRequestAborted` → 工作流取消，连接安静结束。

## 6. 数据规格

两张表（EF Core Code First，DDL 与迁移工作流详见 [database.md](database.md)）：

- **Documents**（语料）：`Id` int PK｜`Name` varchar(200) 唯一｜`Content` text｜`UpdatedAt` timestamptz
- **ResearchSessions**（会话）：`Id` bigint PK｜`Question` varchar(1000)｜`SwitchesJson` text（开关快照）｜`ReportMarkdown` text 可空｜`Status` varchar(20)（Completed/Cancelled/Failed）｜`CreatedAt` timestamptz（索引）

启动自举：`Database.Migrate()` 自动建库建表 → Documents 为空时从 `data/corpus/*.md` 种子导入。演进路线：内存 BM25 → PG 全文检索 → pgvector 混合检索。

## 7. 开发历程

| 里程碑 | 内容 | 验证 | 状态 |
|---|---|---|---|
| **M0 技术验证** | DeepSeek 连通 + 工具调用 + Harness 冒烟（tools/SmokeTest） | 控制台 3/3 通过 | ✅ |
| **M1 核心能力** | BM25 本地检索 + 博查搜索 + 单 HarnessAgent（CLI） | Agent 自主双通道检索作答 | ✅ |
| **M2 编排** | Researcher→Analyst→Writer 顺序工作流 + 流式事件 | CLI 可见三 Agent 依次产出 | ✅ |
| **M3 Web UI** | Blazor Server 实时面板 + 报告渲染 | 浏览器完成一次完整研究 | ✅（后被 M6 替换） |
| **M4 深化** | Reviewer 审校回环（图编排）、Harness 开关、语料补全至 30 篇 | 实测 Reviewer `{"approved": true}` 产出 2116 字带来源报告 | ✅ |
| **M5 文档** | architecture / tutorial / demo-script + 测试补齐 | 四件套齐、测试全绿 | ✅ |
| **M6 前后端分离** | Blazor → Vue3+TS SPA + WebAPI/SSE + PostgreSQL 持久化 | 见 §9 联调记录 | ✅ 2026-08-13 |
| **M7 前端重规划** | Element Plus + Pinia + Vue Router 重构；三路由布局；过程可视化与报告阅读器升级 | 构建通过；SPA 路由与 SSE 冒烟实测（见 §9） | ✅ 2026-08-13 |
| **M8 后台管理** | `/admin` 独立布局（仪表盘 + 语料 CRUD + 会话管理 + 运维操作 + 日志查看）+ 12 个 `/api/admin/*` 端点 | 端点 curl 全量实测；构建通过；深链无 404（见 §9） | ✅ 2026-08-14 |

### M8 后台管理方案（2026-08-14 实施完成）

- **决策过程**：用户逐项拍板——数据操作范围=完整 CRUD（Documents 增删改 + Sessions 可删，均带确认）；运维菜单=状态总览/索引重建/种子重导/日志查看全要；入口=`/admin` 独立布局（vue-element-admin 式，同一 SPA 内按路由前缀切换壳，非独立工程）；鉴权不做（项目明确的非目标）；
- **后端**：`AdminOpsController`（status/rebuild-index/reseed-corpus/logs）+ `AdminDocumentsController` + `AdminSessionsController`；`InMemoryLogStore`（环形缓冲 1000 条 + 自研 `ILoggerProvider`，零依赖替代 Serilog）；`ResearchRunner` 增加 `RebuildCorpusAsync`（`SemaphoreSlim` 串行化 + `Volatile.Write` 引用原子替换，进行中研究持旧索引天然安全）与 `IsRunning` 闸门（`Interlocked` 计数，replace 重导时拒绝 409）；`DbInitializer.SeedCorpusAsync` 提取为可复用静态方法（Append 幂等跳重名 / Replace 事务包裹删+插）；
- **约定**：语料一切写操作自动重建索引并回传块数（演示规模毫秒级）；列表端点永不返回全文字段（200 字预览 / reportLength）；分页参数钳制（pageSize≤100）；重名双保险（AnyAsync 预检友好提示 + PG 23505 唯一冲突捕获防竞态）；
- **前端**：`AdminLayout`（el-menu 侧栏 + 抽屉响应式）+ 五视图（Dashboard 10s 轮询 / Documents 表格+编辑对话框 / Sessions 多选批删+报告抽屉复用 ReportView / Ops 双卡片 / Logs 级别过滤+5s 自动刷新）；admin 三 store 与前台 store 联动（语料/会话变更后自动刷新前台侧栏）；`/admin` 整组路由动态导入，前台首屏零成本；
- **阶段**：P1 日志基建+status/logs → P2 重建/重导+IsRunning 闸门 → P3 CRUD 端点 → P4 前端骨架 → P5 四管理页+走查。

### M7 前端重规划方案（2026-08-13 实施完成）

- **目标**：学习现代前端架构（Pinia / Vue Router / 组件库 / 组件分层），顺带按行业最佳实践升级界面；
- **调研结论**：deep research 类产品收敛形态 = 对话式入口 + 左活动流/右报告双栏工作区 + 报告一等公民（TOC / 独立阅读路由 / 导出）；工具调用折叠、状态点 + 耗时、自动滚动跟随；
- **技术选型**（D12）：Element Plus + Pinia + Vue Router + @element-plus/icons-vue，按需自动引入（unplugin-vue-components / unplugin-auto-import）；暗色 = 官方 `dark/css-vars` + `html.dark` 类切换；
- **布局**：三路由——`/` 空态首页（居中提问框，开关收进 Popover）、`/run` 工作区（阶段条 + 双栏）、`/sessions/:id` 报告阅读器（TOC + 元信息）；<1100px 双栏变 Tab、侧栏变抽屉；
- **约束**：SSE 协议与后端零改动；演示脚本叙事线不断（过程流、开关对比、历史回放均可演示）；
- **阶段**：P1 工程地基（依赖 + Pinia 迁移 + 主题 + 路由骨架，功能零回归）→ P2 布局重构（三视图 + 侧栏）→ P3 过程可视化（StageStepper / 折叠工具调用 / 自动滚动）→ P4 报告阅读器（TOC / 下载 / 骨架屏）→ P5 打磨（空/错/取消态、响应式、演示走查）。

### M6 决策过程实录（分析先行，用户拍板）

1. **起因**：Blazor 静态资源清单损坏事故（见 §8 坑 6）→ 用户质疑 blazor.web.js 方案，要求先调研行业实践；
2. **调研结论**：SSE 是 LLM 流式输出的行业标准（OpenAI/Anthropic API 同模式）；POST 场景 EventSource 不可用、axios 浏览器端拿不到流式响应体 → **原生 fetch + ReadableStream**，不引入 axios；
3. **关键分歧点**（用户逐项决策）：前后端分离开发成本变高，但"往产品形态养"的定位下值得 → **正式分离**；接受 Node 工具链；**用 TypeScript**；数据库不做 SQLite 过渡，**直接 PostgreSQL**（本地已有实例）；DB 结构必须入文档（→ database.md）；
4. **目录约定固化**：`frontend/` 独立一级目录（npm 项目，不入 slnx），`build.outDir` 指向 Web/wwwroot，产物单向流入；`src/` 只放 .NET 项目。

## 8. 关键决策记录（ADR 简表）

| # | 决策 | 理由 / 取舍 |
|---|---|---|
| D1 | LLM 用 DeepSeek（OpenAI 兼容端点） | 国内可用、成本低；`IChatClient` 抽象 + 环境变量可换任意 OpenAI 兼容服务 |
| D2 | 本地检索自研 BM25 而非向量 RAG | DeepSeek 无 embedding API，不想引入第二家供应商；CJK bigram 分词是零依赖近似；混合检索留演进位 |
| D3 | `DisableWebSearch = true` 写死 + 自研博查工具 | Harness 自带 `HostedWebSearchTool` 是服务端托管工具，DeepSeek 不支持 |
| D4 | HarnessAgent 只给 Researcher | 开放式探索节点才需要 Harness 托管（工具循环/todo/plan-execute）；确定性节点用普通 Agent 减少开销与不可控性 |
| D5 | 顺序流水线 → `WorkflowBuilder` 图 + `AddSwitch` 条件路由 | BuildSequential 无法表达"打回重写"回环；共享状态计修订次数（MaxRevisions=2）防死循环 |
| D6 | Reviewer 结构化输出用提示词 JSON + 容错解析 | 各厂商 schema 约束解码支持不一；解析失败默认通过——宁可放过瑕疵不卡死流程 |
| D7 | M6 弃 Blazor Server，改 Vue3+TS+WebAPI/SSE | blazor.web.js 事故暴露脆弱性；产品化定位需要正式 API 契约与独立前端工程 |
| D8 | SSE 客户端用原生 fetch，不引 axios/EventSource | EventSource 仅 GET；axios 浏览器端基于 XHR 拿不到流式响应体 |
| D9 | PostgreSQL 直入，EF Core Code First + 启动自迁移 + 种子导入 | 用户本地已有 PG 实例；`dotnet run` 即可跑通，无需手工 SQL |
| D10 | 密钥只放 `.env` / `appsettings.Local.json`（均 gitignore） | 代码/文档/git 历史零密钥；config 优先、环境变量兜底 |
| D11 | 自定义 Executor 必须 `partial` + 引用 Workflows.Generators 包 | `[MessageHandler]` 路由配置由独立成包的源生成器生成 |
| D12 | 前端重构选型 Element Plus（用户拍板，非 Naive UI / shadcn-vue）+ Pinia + Vue Router | EP 官方暗色方案成熟（css-vars + `html.dark` 切换）、中文资料全；Pinia / Vue Router 是 Vue 3 工程标准，替代模块级 reactive 单例与单页无路由形态 |
| D13 | 后台管理：无鉴权 + 内存日志（不引 Serilog）+ 语料写后自动重建索引 | 项目非目标明确无鉴权（本地演示）；环形缓冲 1000 条自研 `ILoggerProvider` 零依赖够用，要持久化再换 Serilog；演示规模重建毫秒级，"写后手动重建"只会被遗忘 |

## 9. 验证记录（2026-08-13 联调实测）

| 验证项 | 方法 | 结果 |
|---|---|---|
| 单元测试 | `dotnet test` | ✅ 15/15 通过（624ms，离线） |
| 自动迁移 | 启动观察 EF 日志 | ✅ 自动建库 `research_assistant` + 2 表 + 2 索引 |
| 种子导入 | `GET /api/status` | ✅ 30 篇入库，重建索引 `corpusChunkCount: 175`；重复启动不重复导入 |
| SSE 流式 | `curl -N POST /api/research` 实测完整研究 | ✅ 6055 帧逐条到达（executor-start/text/tool-call/tool-result/report/done 全类型），连接正常收尾 |
| 会话落库 | `GET /api/sessions` + `/{id}` | ✅ id=1 status=Completed，完整 Markdown 报告可取回 |
| 生产模式 | wwwroot SPA | ✅ index/JS 资源/SPA fallback 均 200 |
| 开发模式 | Vite 代理 | ✅ `/api` 转发正常（5173 被本机其他服务占用，Vite 自动让至 5174） |
| M7 前端构建 | `npm run build`（vue-tsc + vite） | ✅ 通过；Element Plus 按需引入生效 |
| M7 SPA 路由 | curl `/`、`/run`、`/sessions/1` | ✅ 均 200（MapFallbackToFile 覆盖 history 模式） |
| M7 SSE 冒烟 | `curl -N POST /api/research` | ✅ executor-start/text 帧正常到达，协议零改动 |
| M8 状态/日志端点 | `GET /api/admin/ops/status` `/logs` | ✅ 8 字段齐全（含修复后的真实进程运行时长）；日志级别过滤生效 |
| M8 索引重建 | `POST rebuild-index` | ✅ 175 块 / 72ms；研究进行中重建互不影响 |
| M8 种子重导 | `POST reseed-corpus` | ✅ append 幂等（两次 imported:0/skipped:30）；replace 空闲时 30 删 30 导；非法 mode 400 |
| M8 危险操作闸门 | 研究进行中 POST replace 重导 | ✅ 409 友好文案；SSE 流不受任何影响 |
| M8 语料 CRUD | curl 全流程 | ✅ 新增（索引 175→176）/重名 409/空名 400/更新/删除 204，清理后恢复 30 篇 175 块 |
| M8 会话管理 | curl 列表+删除 | ✅ 分页+reportLength 投影；删不存在 404；批删空 ids 400 |
| M8 前端构建 | `npm run build`（vue-tsc + vite） | ✅ 通过；admin 整组独立懒加载 chunk |
| M8 深链 | curl `/admin/*` 五页 + 前台三页 | ✅ 全 200，无回归 |
| M8 评审修复回归 | code-reviewer 六条意见修复后实测：研究中 kill 客户端 | ✅ `isResearchRunning` 正确归位（HIGH 计数泄漏修复生效）；会话正确记 `Cancelled`（顺带修了流安静结束误记 Completed 的存量 bug）；构建 + 15 测试全绿 |

## 10. 踩坑实录

| # | 症状 | 根因 | 解法 |
|---|---|---|---|
| 1 | `CS0534 不实现 ConfigureProtocol` | 自定义 Executor 的 `[MessageHandler]` 路由由**独立成包**的源生成器生成 | 引用 `Microsoft.Agents.AI.Workflows.Generators` + 类改 `partial` |
| 2 | 最终输出事件触发两次 | 显式 `YieldOutputAsync` 与 `WithOutputFrom` 重复 | 只靠 `WithOutputFrom` 自动 Yield 返回值 |
| 3 | `CS1626 无法在含 catch 的 try 中生成值` | `yield return` 与 catch 同层（C# 语言限制） | 手动 `GetAsyncEnumerator` 循环：内层 try/catch 定状态、外层 yield |
| 4 | 自定义图起点报 TurnToken 错误 | 旧教程模式不适用自定义图 | 起点收 `string`，直接传字符串，不用 TurnToken |
| 5 | DeepSeek 调 Harness 报 400/工具错误 | `HostedWebSearchTool` 为服务端托管工具 | `DisableWebSearch = true` + 自研博查工具 |
| 6 | Blazor 页面按钮灰色不可点 + 完全无样式 | DLL 锁中断的构建损坏 MapStaticAssets 开发清单 → blazor.web.js 与 scoped CSS 500 | 清 bin/obj 重建；此事故直接促成 M6 弃 Blazor |
| 7 | `MSB3027 DLL 被锁定` / 端口占用 | 停后台任务只杀外壳，`ResearchAssistant.Web.exe` 残留占端口 | `taskkill //F //IM ResearchAssistant.Web.exe` |
| 8 | `dotnet ef` 报版本不兼容 | 全局工具 8.0.11 < EF Core 10 | `dotnet tool update -g dotnet-ef` 至 10.0.11 |
| 9 | Vite 代理 404/连不上 | launchSettings 默认端口 5292 ≠ 代理目标 5199 | 启动固定 `--urls http://localhost:5199` |
| 10 | C# 本地函数做工具，工具名被编译器改写 | 编译器生成 `_Main_g_GetWeather_1` 类名字 | `AIFunctionFactory.Create` 显式命名 |
| 11 | 博查返回文本词间夹 `\n` | API 特性 | `BochaWebSearchProvider.Normalize` 折叠空白 |
| 12 | record 校验 500：`validation metadata ... will be ignored` | 位置参数的验证特性挂 `[property: Required]` 会被运行时拒绝 | 特性挂参数本身（默认目标）：`record DocumentInput([Required] string Name, ...)` |
| 13 | `uptimeSeconds` 显示 2.7E-06 | `static readonly Stopwatch.StartNew()` 首次访问控制器才初始化 | 改存 `Process.GetCurrentProcess().StartTime`，与进程启动对齐 |
| 14 | replace 重导突然永久 409 | `Interlocked.Increment` 在 try/finally 视野外：Build/启动流抛错（如客户端恰好此时断开）则 Decrement 永不执行，计数泄漏 → IsRunning 卡真 | 自增之后的所有代码放进外层 try/finally；评审抓到，取消实测回归验证 |
| 15 | 取消的研究落库成 `Completed` | 客户端断开时 MAF 监听流可能"安静结束"（`MoveNextAsync` 返回 false）而不抛 OCE | finally 里按 `cancellationToken.IsCancellationRequested` 校正状态再落库 |

## 11. 后续路线（已识别，未排期）

- **检索升级**：PG 全文检索 → pgvector 向量混合检索（接口边界已留好，见 database.md）
- **场景扩展**：代码审查流水线、客服工单流转、知识库问答（复用同一编排骨架）
- **搜索供应商**：Tavily 实现 `IWebSearchProvider` 即可切换
- **运行可靠性**：MAF checkpoint 断点续跑（对应语料 workflow-checkpointing.md）
- **产品化**：鉴权、并发隔离、会话管理增强
