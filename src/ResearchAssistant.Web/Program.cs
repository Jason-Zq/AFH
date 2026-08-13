using Microsoft.EntityFrameworkCore;
using ResearchAssistant.Web.Data;
using ResearchAssistant.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 机密配置（API key、数据库连接串）放 appsettings.Local.json，已在 .gitignore 中排除，绝不入库；
// 环境变量优先级更高（.NET 配置系统默认行为），两种配法二选一即可。
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();
// OpenAPI 文档（.NET 内置），开发期便于查看接口契约
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ResearchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ResearchDb")
        ?? throw new InvalidOperationException("未找到数据库连接串：请在 appsettings.Local.json 配置 ConnectionStrings:ResearchDb。")));

// 研究工作流运行器（单例：语料索引与 HTTP 客户端进程内复用；DB 访问走作用域）
builder.Services.AddSingleton<ResearchRunner>();

var app = builder.Build();

// 启动即迁移建表 + 语料种子导入（幂等：表已存在/已有数据则跳过）
await DbInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();

// SPA 静态托管：Vue 构建产物直接输出到 wwwroot（开发期也可用 Vite dev server + /api 代理）
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
// 非 API 的 GET 路由回落到 SPA 入口（Vue Router 前端路由用）
app.MapFallbackToFile("index.html");

app.Run();
