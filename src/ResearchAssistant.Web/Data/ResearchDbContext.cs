using Microsoft.EntityFrameworkCore;

namespace ResearchAssistant.Web.Data;

/// <summary>研究助手的 EF Core 上下文（PostgreSQL）。表结构详见 docs/database.md。</summary>
public sealed class ResearchDbContext(DbContextOptions<ResearchDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ResearchSession> ResearchSessions => Set<ResearchSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(e =>
        {
            e.HasIndex(d => d.Name).IsUnique();
            e.Property(d => d.Name).HasMaxLength(200);
        });
        modelBuilder.Entity<ResearchSession>(e =>
        {
            e.HasIndex(s => s.CreatedAt);
            e.Property(s => s.Question).HasMaxLength(1000);
            e.Property(s => s.Status).HasMaxLength(20);
        });
    }
}
