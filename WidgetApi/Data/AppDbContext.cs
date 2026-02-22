using Microsoft.EntityFrameworkCore;
using WidgetApi.Models;

namespace WidgetApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();
}
