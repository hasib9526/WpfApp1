using System.ComponentModel.DataAnnotations;

namespace WidgetApi.Models;

public class Notification
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string UserId { get; set; } = string.Empty; // UserCode from tblUser

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Type { get; set; } = "info"; // info, email, warning, reminder

    public bool IsRead { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
    public bool IsRead { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class SendNotificationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
}
