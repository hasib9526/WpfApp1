using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;
using WidgetApi.Data;

// Required for Microsoft.VisualBasic (EncryptionService) on .NET Core
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Register this server to auto-start with Windows
try
{
    var exePath = Environment.ProcessPath;
    if (!string.IsNullOrEmpty(exePath))
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        key?.SetValue("DesktopWidgetApi", $"\"{exePath}\"");
    }
}
catch { }

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("Jwt:Key missing in appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = "WidgetApi",
            ValidAudience            = "WidgetApp",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// SQLite — stores notifications locally on the server machine (no SQL Server table needed)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=widget_notifications.db"));

// CORS — allow widget from any PC on the network
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Listen on all interfaces — any PC on same network can connect
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

// Auto-create the SQLite notifications table on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("============================================");
Console.WriteLine("  Widget API — Running on port 5000");
Console.WriteLine("  DB: SystemManager @ 192.168.10.6");
Console.WriteLine("  Auth: sp_UserAuthenticationService");
Console.WriteLine("============================================");
Console.WriteLine("  Find your IP: run 'ipconfig' in CMD");
Console.WriteLine("  Other PCs use: http://<YOUR-IP>:5000");
Console.WriteLine("============================================");

app.Run();
