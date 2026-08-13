# 多智能体研究助手（Microsoft Agent Framework Harness 演示）

用 **Microsoft Agent Framework（.NET）+ Agent Harness** 构建的多智能体研究助手：
Researcher（HarnessAgent，双通道检索）→ Analyst → Writer ⇄ Reviewer 图编排工作流。
前后端分离：**ASP.NET Core WebAPI（SSE 流式）+ Vue 3 SPA + PostgreSQL 持久化**。

## 快速开始

**前置**：.NET SDK 10、Node.js 20+、本地 PostgreSQL、DeepSeek API key、博查（BoCha）API key。

```bash
# 1. 配置机密（复制模板并填入，appsettings.Local.json 已在 .gitignore 中）
cp src/ResearchAssistant.Web/appsettings.Local.json.example src/ResearchAssistant.Web/appsettings.Local.json
#    填入：PG 连接串密码、DeepSeek key、博查 key

# 2. 构建后端 + 测试
dotnet build
dotnet test          # 15 个单元测试，不依赖网络

# 3. 构建前端（产物输出到后端 wwwroot）
cd frontend && npm install && npm run build && cd ..

# 4. 启动（首次启动自动建库建表 + 导入语料种子）
dotnet run --project src/ResearchAssistant.Web --urls http://localhost:5199
# 浏览器打开 http://localhost:5199
```

**开发模式**（前端热更新）：

```bash
dotnet run --project src/ResearchAssistant.Web --urls http://localhost:5199   # 终端 1：API
cd frontend && npm run dev                                                   # 终端 2：Vite（/api 已代理到 5199）
```

**CLI（仅环境变量方式）**：`set -a; source .env; set +a` 后 `dotnet run --project tools/ResearchCli -- "你的问题"`。

可选环境变量：`DEEPSEEK_ENDPOINT`（默认 `https://api.deepseek.com/v1`）、`DEEPSEEK_MODEL`（默认 `deepseek-chat`）。

## 仓库结构

```
src/ResearchAssistant.Core/    领域逻辑：BM25 本地检索、博查搜索、Agent/工作流编排
src/ResearchAssistant.Web/     API 宿主：SSE 控制器、EF Core/PostgreSQL、SPA 静态托管
frontend/                      Vue 3 + Vite + TypeScript 前端（产物输出到 Web/wwwroot）
tools/ResearchCli/             命令行运行器
tools/SmokeTest/               DeepSeek 连通性冒烟测试
tests/ResearchAssistant.Core.Tests/   15 个离线单元测试（假 IChatClient）
data/corpus/                   30 篇 AI Agent 主题中文语料（PG 种子导入源）
docs/plan.md                   实施方案
docs/spec.md                   SPEC：开发与规格全记录（功能/接口/数据/决策/踩坑/验证）
docs/architecture.md           架构讲解
docs/database.md               数据库结构与迁移工作流
docs/tutorial.md               分步复现教程（含踩坑速查）
docs/demo-script.md            10 分钟演示脚本
```

## 接口一览

| 端点 | 说明 |
|---|---|
| `POST /api/research` | 发起研究，SSE 逐事件推送（executor-start/text/tool-call/tool-result/report/error/done） |
| `GET /api/status` | 语料块数等状态 |
| `GET /api/sessions` | 最近 50 次研究会话 |
| `GET /api/sessions/{id}` | 单次会话详情（含定稿报告） |

开发期 OpenAPI 文档：`/openapi/v1.json`。

## 技术要点速览

- **HarnessAgent 只给 Researcher**：自主探索型节点用 Harness（工具循环/todo/plan-execute 托管），确定性节点用普通 Agent
- **图编排 + 条件路由**：`WorkflowBuilder` + `AddSwitch` 实现 Writer⇄Reviewer 审校回环，共享状态计数防死循环（最多修订 2 轮）
- **SSE 流式**：fetch + ReadableStream（POST 场景 EventSource/axios 均不可用），取消 = AbortController → `HttpRequestAborted`
- **持久化**：语料与会话历史进 PostgreSQL，启动自动迁移 + 种子导入；检索留 PG 全文/pgvector 演进路线
- **密钥安全**：全部走 `appsettings.Local.json` 或环境变量，均已 gitignore
