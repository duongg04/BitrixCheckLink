using BitrixChecker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitrixChecker.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize(Policy = "AdminOrUser")]
public sealed class NotificationController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var notifications = await context.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new { n.Id, n.Message, n.Type, n.IsRead, n.CreatedAt })
            .ToListAsync();
        var unreadCount = await context.Notifications.CountAsync(n => !n.IsRead);
        return Ok(new { notifications, unreadCount });
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkAllRead()
    {
        await context.Notifications.Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok(new { message = "Đã đánh dấu tất cả là đã đọc." });
    }
}
