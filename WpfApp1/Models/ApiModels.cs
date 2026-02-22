using System;
using System.Collections.Generic;

namespace WpfApp1.Models
{
    public class LoginRequest
    {
        public string UserName    { get; set; } = string.Empty;
        public string Password    { get; set; } = string.Empty;
        public string DeviceID    { get; set; } = "init_id";
        public string DeviceToken { get; set; } = "464be0aa-cfc8-46a7-a217-d4e2fe4eb85c";
        public string DeviceName  { get; set; } = "Windows PC";
        public string Platform    { get; set; } = "windows";
        public int    QryOption   { get; set; } = 1;
        public int    VersionCode { get; set; } = 31;
        public int    UserCode    { get; set; } = 1;
        public string OSName      { get; set; } = "W";
        public string OSVersion   { get; set; } = "11";
    }

    public class LoginResponse
    {
        public string? UserCode      { get; set; }
        public string? UserName      { get; set; }
        public string? EmployeeName  { get; set; }
        public string? Email         { get; set; }
        public string? Unit          { get; set; }
        public string? Department    { get; set; }
        public string? Designation   { get; set; }
        public string? EmpImage      { get; set; }
        public string? LeaveApproval { get; set; }
        public string? LeaveRecommend{ get; set; }
        public int?    VersionCode   { get; set; }
        public bool?   IsOtpSend     { get; set; }
        public bool?   IsAutoUser    { get; set; }
        public string? ComID         { get; set; }
        public string? CompanyID     { get; set; }
    }

    public class TodoItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StatsData
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class DashboardData
    {
        public List<TodoItem> Todos { get; set; } = new();
        public StatsData Stats { get; set; } = new();
    }

    public class AddTodoRequest
    {
        public string Title { get; set; } = string.Empty;
    }

    public class EmailMessage
    {
        public string   Uid     { get; set; } = string.Empty;
        public string   From    { get; set; } = string.Empty;
        public string   Subject { get; set; } = string.Empty;
        public DateTime Date    { get; set; }
    }
}
