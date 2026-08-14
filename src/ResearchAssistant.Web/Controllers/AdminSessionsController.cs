using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResearchAssistant.Web.Data;

namespace ResearchAssistant.Web.Controllers;

/// <summary>后台会话管理：分页浏览（不含报告全文）与删除。无鉴权——仅限本地演示。</summary>
[ApiController]
[Route("api/admin/sessions")]
public class AdminSessionsController(ResearchDbContext db, ILogger<AdminSessionsController> logger) : ControllerBase
{
    /// <summary>分页列表：用 reportLength 代替 ReportMarkdown 全文（报告可能几十 KB）。</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ResearchSessions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Question,
                s.Status,
                s.CreatedAt,
                reportLength = s.ReportMarkdown != null ? s.ReportMarkdown.Length : 0,
            })
            .ToListAsync();
        return Ok(new { total, items });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await db.ResearchSessions.Where(s => s.Id == id).ExecuteDeleteAsync();
        if (deleted == 0)
        {
            return NotFound();
        }
        logger.LogInformation("删除会话 #{Id}", id);
        return NoContent();
    }

    /// <summary>批量删除：一条 SQL 完成，ids 去重后上限 500 条。</summary>
    [HttpPost("delete-batch")]
    public async Task<IActionResult> DeleteBatch([FromBody] IdsInput input)
    {
        var ids = input.Ids.Distinct().Take(500).ToArray();
        if (ids.Length == 0)
        {
            return BadRequest(new { message = "ids 不能为空。" });
        }
        var deleted = await db.ResearchSessions.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync();
        logger.LogInformation("批量删除会话 {Count} 条", deleted);
        return Ok(new { deleted });
    }
}

/// <summary>批量删除请求体。</summary>
public sealed record IdsInput(long[] Ids);
