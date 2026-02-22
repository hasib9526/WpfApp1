namespace WidgetApi.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token  { get; set; } = string.Empty;
    public string Name   { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty; // UserCode (string) from tblUser
}

public class LogoutRequest
{
    public string UserCode { get; set; } = string.Empty;
}

// Result from sp_UserAuthenticationService (QryOption=1)
public class UserAuthResult
{
    public string UserCode     { get; set; } = string.Empty;
    public string UserName     { get; set; } = string.Empty;
    public string? Name        { get; set; }
    public string? Email       { get; set; }
    public string? EmployeeCode{ get; set; }
    public bool   IsActive     { get; set; }
    public bool   IsAdmin      { get; set; }
    public string? ComId       { get; set; }
    public string? UnitId      { get; set; }
}

public class TodoDto
{
    public int    Id          { get; set; }
    public string Title       { get; set; } = string.Empty;
    public bool   IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StatsDto
{
    public int       TotalTasks     { get; set; }
    public int       CompletedTasks { get; set; }
    public DateTime? LastLogin      { get; set; }
}

public class DashboardResponse
{
    public List<TodoDto> Todos { get; set; } = new();
    public StatsDto      Stats { get; set; } = new();
}

public class AddTodoRequest
{
    public string Title { get; set; } = string.Empty;
}
