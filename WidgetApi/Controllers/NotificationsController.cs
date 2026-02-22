using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WidgetApi.Data;
using WidgetApi.Models;

namespace WidgetApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get unread notifications for the logged-in user.
    /// The widget polls this endpoint every 30 seconds.
    /// </summary>
    [HttpGet("unread")]
    [Authorize]
    public async Task<IActionResult> GetUnread()
    {
        var userId = GetUserId();

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.ReceivedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                ReceivedAt = n.ReceivedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    /// <summary>
    /// Get all notifications for the logged-in user.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.ReceivedAt)
            .Take(50)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                ReceivedAt = n.ReceivedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    [HttpPut("{id}/read")]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetUserId();
        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notif == null) return NotFound();

        notif.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Send a notification to a user.
    /// Call this from your backend/admin panel when user has new email,
    /// message, or any event. The widget will pick it up automatically.
    ///
    /// No auth required — this is meant to be called by your server.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "Title is required" });

        var notification = new Notification
        {
            UserId = req.UserId,
            Title = req.Title,
            Message = req.Message,
            Type = req.Type,
            IsRead = false,
            ReceivedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        return Ok(new NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            IsRead = notification.IsRead,
            ReceivedAt = notification.ReceivedAt
        });
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}
