# 数据库结构（docs/database.md）

> 持久化采用 **PostgreSQL + EF Core（Npgsql）**，迁移由代码管理（Code First）。
> 本文记录表结构、字段含义与迁移工作流。连接串配置见 README（`appsettings.Local.json`，不入库）。

## 表结构

### `Documents` —— 语料文档

本地检索的内容源。`data/corpus/*.md` 是种子文件，运行时以此表为准。

| 列 | 类型 | 说明 |
|---|---|---|
| `Id` | integer (identity, PK) | 主键 |
| `Name` | varchar(200), **唯一索引** | 文件名，如 `agent-security.md` |
| `Content` | text | Markdown 全文 |
| `UpdatedAt` | timestamptz | 最后更新时间 |

### `ResearchSessions` —— 研究会话

一次"开始研究"的完整记录。

| 列 | 类型 | 说明 |
|---|---|---|
| `Id` | bigint (identity, PK) | 主键 |
| `Question` | varchar(1000) | 用户的研究问题 |
| `SwitchesJson` | text | Harness 特性开关的 JSON 快照 |
| `ReportMarkdown` | text, 可空 | 定稿报告；取消/失败时为 NULL |
| `Status` | varchar(20) | `Completed` / `Cancelled` / `Failed` |
| `CreatedAt` | timestamptz, 普通索引 | 发起时间（UTC） |

对应实体：[Data/Entities.cs](../src/ResearchAssistant.Web/Data/Entities.cs)；上下文与索引配置：[Data/ResearchDbContext.cs](../src/ResearchAssistant.Web/Data/ResearchDbContext.cs)。

## 迁移工作流

```bash
cd src/ResearchAssistant.Web

# 改了实体/索引之后：生成新迁移
dotnet ef migrations add <改动名>

# 本地手动应用（通常不需要——应用启动时会自动 Database.Migrate()）
dotnet ef database update

# 撤销上一次迁移（未应用过时）
dotnet ef migrations remove
```

- **启动自动迁移**：`Program.cs` 里 `DbInitializer.InitializeAsync` 先 `Database.Migrate()` 再种子导入，`dotnet run` 即建库建表，无需手动执行 SQL。
- **种子导入**：仅当 `Documents` 表为空时从 `data/corpus/*.md` 灌入；已有数据则跳过（幂等）。
- 当前迁移：`Migrations/20260813081443_Init.cs`（建两张表 + 两个索引）。

## 演进路线

| 阶段 | 检索方案 | 说明 |
|---|---|---|
| 现在 | 内存 BM25（`LocalCorpusSearch`，从 Documents 表读全文建索引） | 零外部依赖 |
| 下一步 | PG 全文检索（`tsvector`/`tsquery`，中文配 zhparser 或保持 bigram） | 语料量大后免内存索引 |
| 远期 | pgvector 混合检索 + Rerank | 需先接 embedding 服务（DeepSeek 无此 API），路线见语料 `hybrid-search-and-rerank.md` |

会话表可预期的扩展：用户体系（加 `UserId`）、事件留痕（`SessionEvents` 子表，回放协作过程）、报告版本（审校每轮草稿各存一行）。
