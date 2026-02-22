using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using WidgetApi.Models;
using WidgetApi.Services;

namespace WidgetApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly string _connStr;
    private readonly EncryptionService _encryption = new();

    public AuthController(IConfiguration config)
    {
        _config = config;
        _connStr = config.GetConnectionString("SysManagerConnection")
                   ?? throw new Exception("SysManagerConnection not found in appsettings.json");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Username and password are required" });

        try
        {
            // Encrypt credentials exactly like PPC-Web does
            var encUser = _encryption.EncryptWord(req.Username);
            var encPass = _encryption.EncryptWord(req.Password);

            // Call sp_UserAuthenticationService (QryOption=1)
            var user = await AuthenticateAsync(encUser, encPass);

            if (user == null)
                return Unauthorized(new { message = "Invalid username or password" });

            if (!user.IsActive)
                return Unauthorized(new { message = "Account is inactive" });

            // Update login status (QryOption=3)
            await UpdateLoginStatusAsync(user.UserCode, DateTime.Now, true);

            var token = GenerateJwt(user);

            return Ok(new LoginResponse
            {
                Token  = token,
                Name   = user.Name ?? user.UserName,
                UserId = user.UserCode
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Server error: " + ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req)
    {
        await UpdateLoginStatusAsync(req.UserCode, DateTime.Now, false);
        return Ok();
    }

    private async Task<UserAuthResult?> AuthenticateAsync(string encUser, string encPass)
    {
        await using var conn = new SqlConnection(_connStr);
        await using var cmd  = new SqlCommand("sp_UserAuthenticationService", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.Add(new SqlParameter("@UserName",  SqlDbType.NVarChar, 250) { Value = encUser });
        cmd.Parameters.Add(new SqlParameter("@Password",  SqlDbType.NVarChar, 250) { Value = encPass });
        cmd.Parameters.Add(new SqlParameter("@QryOption", SqlDbType.Int)           { Value = 1 });

        await conn.OpenAsync();
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return null;

        return new UserAuthResult
        {
            UserCode     = reader["UserCode"]?.ToString()     ?? "",
            UserName     = reader["UserName"]?.ToString()     ?? "",
            Name         = reader["Name"]?.ToString()         ?? "",
            Email        = reader["Email"]?.ToString()        ?? "",
            EmployeeCode = reader["EmployeeCode"]?.ToString() ?? "",
            IsActive     = Convert.ToBoolean(reader["IsActive"]),
            IsAdmin      = Convert.ToBoolean(reader["IsAdmin"]),
            ComId        = reader["ComID"]?.ToString()        ?? "",
            UnitId       = reader["UnitID"]?.ToString()       ?? ""
        };
    }

    private async Task UpdateLoginStatusAsync(string userCode, DateTime time, bool isLoggedIn)
    {
        try
        {
            await using var conn = new SqlConnection(_connStr);
            await using var cmd  = new SqlCommand("sp_UserAuthenticationService", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@UserCode",   SqlDbType.NVarChar, 250) { Value = userCode });
            cmd.Parameters.Add(new SqlParameter("@LogonTime",  SqlDbType.DateTime)      { Value = time });
            cmd.Parameters.Add(new SqlParameter("@IsLoggedIn", SqlDbType.Bit)           { Value = isLoggedIn });
            cmd.Parameters.Add(new SqlParameter("@QryOption",  SqlDbType.Int)           { Value = 3 });
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* Non-critical */ }
    }

    private string GenerateJwt(UserAuthResult user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserCode),
            new Claim(ClaimTypes.Name,           user.UserName),
            new Claim("FullName",                user.Name ?? ""),
            new Claim("UserCode",                user.UserCode),
            new Claim("ComId",                   user.ComId  ?? ""),
            new Claim(ClaimTypes.Role,           user.IsAdmin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            issuer:             "WidgetApi",
            audience:           "WidgetApp",
            claims:             claims,
            expires:            DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
