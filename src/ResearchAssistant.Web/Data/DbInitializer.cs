using Microsoft.EntityFrameworkCore;
using ResearchAssistant.Core.Search;

namespace ResearchAssistant.Web.Data;

/// <summary>启动初始化：自动迁移建表 + 语料种子导入（documents 为空时从 data/corpus 灌入）。</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Documents.AnyAsync())
        {
            return;  // 已有语料，不重复导入
        }

        var corpusDir = CorpusLocator.Find();
        if (!Directory.Exists(corpusDir))
        {
            return;
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var file in Directory.EnumerateFiles(corpusDir, "*.md").OrderBy(f => f))
        {
            db.Documents.Add(new Document
            {
                Name = Path.GetFileName(file),
                Content = await File.ReadAllTextAsync(file),
                UpdatedAt = now,
            });
        }
        await db.SaveChangesAsync();
    }
}
