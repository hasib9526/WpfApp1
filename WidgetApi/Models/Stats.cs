using System.ComponentModel.DataAnnotations;

namespace WidgetApi.Models;

public class Stats
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }

    public DateTime? LastLogin { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
