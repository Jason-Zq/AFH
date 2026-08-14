using Microsoft.EntityFrameworkCore;
using ResearchAssistant.Core.Search;

namespace ResearchAssistant.Web.Data;

/// <summary>种子导入模式：Append 跳过重名；Replace 先清空 documents 表再导入。</summary>
public enum SeedMode
{
    Append,
    Replace,
}

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

        try
        {
            await SeedCorpusAsync(db);
        }
        catch (DirectoryNotFoundException)
        {
            // 无语料目录则空库启动，与改造前行为一致
        }
    }

    /// <summary>
    /// 从 data/corpus 导入种子语料，返回 (导入数, 跳过重名数, 删除数)。
    /// Replace 模式用事务包"删 + 插"，导入失败可回滚。
    /// 调用方负责在导入完成后重建索引（ResearchRunner.RebuildCorpusAsync）。
    /// </summary>
    public static async Task<(int Imported, int Skipped, int Deleted)> SeedCorpusAsync(ResearchDbContext db, SeedMode mode = SeedMode.Append)
    {
        var corpusDir = CorpusLocator.Find();
        var files = Directory.EnumerateFiles(corpusDir, "*.md").OrderBy(f => f).ToList();
        var now = DateTimeOffset.UtcNow;

        if (mode == SeedMode.Replace)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var deleted = await db.Documents.ExecuteDeleteAsync();
            foreach (var file in files)
            {
                db.Documents.Add(new Document
                {
                    Name = Path.GetFileName(file),
                    Content = await File.ReadAllTextAsync(file),
                    UpdatedAt = now,
                });
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (files.Count, 0, deleted);
        }

        var existing = (await db.Documents.Select(d => d.Name).ToListAsync()).ToHashSet();
        var imported = 0;
        var skipped = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (existing.Contains(name))
            {
                skipped++;
                continue;
            }
            db.Documents.Add(new Document
            {
                Name = name,
                Content = await File.ReadAllTextAsync(file),
                UpdatedAt = now,
            });
            imported++;
        }
        await db.SaveChangesAsync();
        return (imported, skipped, 0);
    }
}
