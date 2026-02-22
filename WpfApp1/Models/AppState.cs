using System;
using System.Collections.Generic;

namespace WpfApp1.Models
{
    public static class AppState
    {
        public static string Token        { get; set; } = string.Empty;
        public static string UserName     { get; set; } = string.Empty;
        public static string UserCode     { get; set; } = string.Empty;
        public static string EmployeeName { get; set; } = string.Empty;
        public static string Department   { get; set; } = string.Empty;
        public static string Designation  { get; set; } = string.Empty;
        public static string Email        { get; set; } = string.Empty;
        public static string MailPassword { get; set; } = string.Empty;

        private static int _nextId      = 1;
        private static int _nextNotifId = 1;

        public static List<TodoItem>         Todos         { get; set; } = new();
        public static List<NotificationItem> Notifications { get; set; } = new();

        public static int GetNextId()      => _nextId++;
        public static int GetNextNotifId() => _nextNotifId++;

        public static void Clear()
        {
            Token        = string.Empty;
            UserName     = string.Empty;
            UserCode     = string.Empty;
            EmployeeName = string.Empty;
            Department   = string.Empty;
            Designation  = string.Empty;
            Email        = string.Empty;
            MailPassword = string.Empty;
            Todos.Clear();
            Notifications.Clear();
            _nextId      = 1;
            _nextNotifId = 1;
        }
    }

    public class NotificationItem
    {
        public int      Id         { get; set; }
        public string   Title      { get; set; } = string.Empty;
        public string   Message    { get; set; } = string.Empty;
        public string   Type       { get; set; } = "info";
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
        public bool     IsRead     { get; set; }
    }
}
