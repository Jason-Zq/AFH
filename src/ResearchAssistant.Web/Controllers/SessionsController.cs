using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResearchAssistant.Web.Data;

namespace ResearchAssistant.Web.Controllers;

/// <summary>历史会话接口：列表 + 单次研究详情（含定稿报告）。</summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(ResearchDbContext db) : ControllerBase
{
    /// <summary>最近 50 次会话（倒序）。</summary>
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.ResearchSessions
        .OrderByDescending(s => s.CreatedAt)
        .Take(50)
        .Select(s => new { s.Id, s.Question, s.Status, s.CreatedAt })
        .ToListAsync());

    /// <summary>单次会话详情（含报告全文与开关快照）。</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var session = await db.ResearchSessions.FindAsync(id);
        if (session is null)
        {
            return NotFound();
        }
        return Ok(new
        {
            session.Id,
            session.Question,
            session.Status,
            session.CreatedAt,
            session.ReportMarkdown,
            switches = session.SwitchesJson,
        });
    }
}
