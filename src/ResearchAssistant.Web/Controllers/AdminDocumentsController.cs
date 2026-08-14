using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ResearchAssistant.Web.Data;
using ResearchAssistant.Web.Services;

namespace ResearchAssistant.Web.Controllers;

/// <summary>后台语料管理：Documents 分页浏览与增删改。写操作成功后自动重建索引（响应附带新块数）。无鉴权——仅限本地演示。</summary>
[ApiController]
[Route("api/admin/documents")]
public class AdminDocumentsController(ResearchDbContext db, ResearchRunner runner, ILogger<AdminDocumentsController> logger) : ControllerBase
{
    /// <summary>分页列表：只返回 200 字预览与全文长度，绝不传 Content 全文。</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Documents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // PG ILIKE 对中文即子串匹配；演示规模顺序扫描无感，语料上量后再考虑 pg_trgm / tsvector
            query = query.Where(d => EF.Functions.ILike(d.Name, $"%{search}%") || EF.Functions.ILike(d.Content, $"%{search}%"));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.Name)  // 与索引构建顺序一致，心智模型统一
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.Name,
                contentPreview = d.Content.Substring(0, Math.Min(200, d.Content.Length)),
                contentLength = d.Content.Length,
                d.UpdatedAt,
            })
            .ToListAsync();
        return Ok(new { total, items });
    }

    /// <summary>文档详情（全文，供编辑）。</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null)
        {
            return NotFound();
        }
        return Ok(new { doc.Id, doc.Name, doc.Content, doc.UpdatedAt });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentInput input)
    {
        // 重名双保险：预检为了友好，DbUpdateException 捕获为了正确（防竞态）
        if (await db.Documents.AnyAsync(d => d.Name == input.Name))
        {
            return Conflict(new { message = $"已存在同名文档：{input.Name}" });
        }
        var doc = new Document { Name = input.Name, Content = input.Content, UpdatedAt = DateTimeOffset.UtcNow };
        db.Documents.Add(doc);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict(new { message = $"已存在同名文档：{input.Name}" });
        }
        var chunkCount = await TryRebuildAsync();
        logger.LogInformation("新增语料文档 {Name}，索引重建为 {ChunkCount} 块", doc.Name, chunkCount);
        return CreatedAtAction(nameof(Get), new { id = doc.Id }, new { doc.Id, doc.Name, doc.UpdatedAt, corpusChunkCount = chunkCount });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DocumentInput input)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null)
        {
            return NotFound();
        }
        if (await db.Documents.AnyAsync(d => d.Name == input.Name && d.Id != id))
        {
            return Conflict(new { message = $"已存在同名文档：{input.Name}" });
        }
        doc.Name = input.Name;
        doc.Content = input.Content;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict(new { message = $"已存在同名文档：{input.Name}" });
        }
        var chunkCount = await TryRebuildAsync();
        logger.LogInformation("更新语料文档 {Name}，索引重建为 {ChunkCount} 块", doc.Name, chunkCount);
        return Ok(new { doc.Id, doc.Name, doc.UpdatedAt, corpusChunkCount = chunkCount });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null)
        {
            return NotFound();
        }
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        var chunkCount = await TryRebuildAsync();
        logger.LogInformation("删除语料文档 {Name}，索引重建为 {ChunkCount} 块", doc.Name, chunkCount);
        return NoContent();
    }

    /// <summary>写操作已落库，重建失败不应拖垮请求报 500——记日志并回传 null，前端提示去运维页手动重建。</summary>
    private async Task<int?> TryRebuildAsync()
    {
        try
        {
            return await runner.RebuildCorpusAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "语料写操作后自动重建索引失败");
            return null;
        }
    }
}

/// <summary>语料文档新增/编辑请求体（[ApiController] 自动 400 校验）。</summary>
/// <remarks>record 位置参数的验证特性必须挂在参数上（默认目标），挂 [property:] 会抛 InvalidOperationException。</remarks>
public sealed record DocumentInput(
    [Required(ErrorMessage = "文档名不能为空")]
    [StringLength(200, ErrorMessage = "文档名不能超过 200 字符")]
    string Name,
    [Required(ErrorMessage = "内容不能为空")]
    string Content);
