using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WidgetApi.Data;
using WidgetApi.Models;

namespace WidgetApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApprovalController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppDbContext       _db;

    // In-memory: last known pending count per user, per approval type
    // Key: "userCode|approvalId"
    private static readonly Dictionary<string, int> _prevCounts = new();
    private static readonly object _lock = new();

    public ApprovalController(IHttpClientFactory httpFactory, AppDbContext db)
    {
        _httpFactory = httpFactory;
        _db          = db;
    }

    /// <summary>
    /// GET /api/Approval
    /// Returns approval list with names and pending counts for the logged-in user.
    /// Also saves a notification if new approvals arrived since last call.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetApprovals()
    {
        var userCode = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserCode")?.Value
                    ?? string.Empty;

        if (string.IsNullOrEmpty(userCode))
            return Unauthorized();

        try
        {
            // Call BIMOB API
            var client   = _httpFactory.CreateClient("BimobApi");
            var bimobUrl = $"/api/Approval/GetApprovals?UserID={userCode}&QryOption=1";
            var data     = await client.GetFromJsonAsync<List<ApprovalItem>>(bimobUrl);

            if (data == null)
                return StatusCode(502, new { message = "Could not reach BIMOB API" });

            // Filter only items with pending requests
            var pending = data.Where(a => a.Request > 0).ToList();
            var total   = pending.Sum(a => a.Request);

            // ── Detect new approvals & push notification ──────────────────
            await CheckAndNotifyAsync(userCode, pending);

            return Ok(new ApprovalSummaryDto
            {
                TotalPending = total,
                Approvals    = pending
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { message = "BIMOB API error: " + ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private async Task CheckAndNotifyAsync(string userCode, List<ApprovalItem> current)
    {
        var newItems = new List<string>();
        int newCount = 0;

        lock (_lock)
        {
            foreach (var item in current)
            {
                var key  = $"{userCode}|{item.Approval}";
                var prev = _prevCounts.TryGetValue(key, out var p) ? p : item.Request;

                if (item.Request > prev)
                {
                    newItems.Add(item.ApprovalName);
                    newCount += item.Request - prev;
                }

                _prevCounts[key] = item.Request;
            }
        }

        if (newItems.Count == 0) return;

        // Push into existing Notifications table so the widget picks it up
        var notif = new Notification
        {
            UserId     = userCode,
            Title      = $"New Pending Approval{(newCount > 1 ? "s" : "")} ({newCount})",
            Message    = string.Join(", ", newItems),
            Type       = "approval",
            IsRead     = false,
            ReceivedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
    }
}
