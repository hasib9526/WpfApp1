using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class App : Application
    {
        public static TaskbarIcon?      TrayIcon  { get; private set; }
        public static DashboardWindow?  Dashboard { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            RegisterAutoStart();
            CreateTrayIcon();

            // Auto-start WidgetApi.exe if server URL is localhost and not already running
            await TryStartLocalServer();

            // Try to restore previous session — only show login if that fails
            if (!await TryAutoLogin())
                ShowLogin();
        }

        private static async Task<bool> TryAutoLogin()
        {
            var saved = SessionService.LoadFull();
            if (saved == null) return false;

            var (data, password, mailPassword) = saved.Value;
            try
            {
                var api    = new ApiService();
                var result = await api.LoginAsync(data.UserName, password);
                if (result == null) return false;

                AppState.UserCode     = result.UserCode     ?? data.UserCode;
                AppState.UserName     = result.UserName     ?? data.UserName;
                AppState.EmployeeName = result.EmployeeName ?? data.EmployeeName;
                AppState.Department   = result.Department   ?? data.Department;
                AppState.Designation  = result.Designation  ?? data.Designation;
                AppState.Email        = result.Email        ?? data.Email;
                AppState.MailPassword = mailPassword; // restored — dashboard auto-connects

                ShowDashboard();
                return true;
            }
            catch { return false; }
        }

        private static async Task TryStartLocalServer()
        {
            var url = ApiService.ServerUrl;
            if (!url.Contains("localhost") && !url.Contains("127.0.0.1"))
                return; // Remote server — not our job to start it

            // Check if already running
            if (await IsServerUp(url)) return;

            // Find WidgetApi.exe
            var serverExe = FindServerExe();
            if (serverExe == null) return;

            try
            {
                // WorkingDirectory MUST be the server's folder so it finds appsettings.json
                var serverDir = Path.GetDirectoryName(serverExe)!;
                Process.Start(new ProcessStartInfo(serverExe)
                {
                    UseShellExecute  = true,
                    WindowStyle      = ProcessWindowStyle.Minimized,
                    WorkingDirectory = serverDir
                });
            }
            catch { return; }

            // Poll every second until server responds (max 20 seconds)
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(1000);
                if (await IsServerUp(url)) return;
            }
        }

        private static async Task<bool> IsServerUp(string baseUrl)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
                await client.GetAsync(baseUrl + "/api/auth/login");
                return true; // Any response (even 405) means port is open
            }
            catch { return false; }
        }

        private static string? FindServerExe()
        {
            // 1. Same directory as this exe (published / Output\Widget mode)
            var sameDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WidgetApi.exe");
            if (File.Exists(sameDir)) return sameDir;

            // 2. Walk up directory tree — finds Output\Server\WidgetApi.exe from bin\Debug\
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 6; i++)
            {
                dir = dir?.Parent!;
                if (dir == null) break;
                var candidate = Path.Combine(dir.FullName, "Output", "Server", "WidgetApi.exe");
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        public static void ShowLogin()
        {
            var login = new MainWindow();
            login.Left = SystemParameters.WorkArea.Width  - login.Width  - 20;
            login.Top  = SystemParameters.WorkArea.Height - login.Height - 20;
            login.Show();
        }

        public static void ShowDashboard()
        {
            if (Dashboard == null || !Dashboard.IsLoaded)
            {
                Dashboard = new DashboardWindow();
                Dashboard.Left = SystemParameters.WorkArea.Width  - Dashboard.Width  - 20;
                Dashboard.Top  = SystemParameters.WorkArea.Height - Dashboard.Height - 20;
            }
            Dashboard.Show();
        }

        private static void CreateTrayIcon()
        {
            TrayIcon = new TaskbarIcon
            {
                ToolTipText = "Desktop Widget",
                Icon        = SystemIcons.Application
            };

            var menu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem { Header = "Show Widget" };
            showItem.Click += (_, _) =>
            {
                if (string.IsNullOrEmpty(AppState.UserCode)) ShowLogin();
                else ShowDashboard();
            };

            var testNotif = new System.Windows.Controls.MenuItem { Header = "Test Notification" };
            testNotif.Click += (_, _) =>
                Dashboard?.NotificationService.PushLocal("Test", "Notification on top of everything!", "info");

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (_, _) => { TrayIcon?.Dispose(); Current.Shutdown(); };

            menu.Items.Add(showItem);
            menu.Items.Add(testNotif);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(exitItem);

            TrayIcon.ContextMenu = menu;
            TrayIcon.TrayMouseDoubleClick += (_, _) =>
            {
                if (string.IsNullOrEmpty(AppState.UserCode)) ShowLogin();
                else ShowDashboard();
            };
        }

        private static void RegisterAutoStart()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue("DesktopWidget", $"\"{exePath}\"");
            }
            catch { }
        }

        public static void RemoveAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("DesktopWidget", false);
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TrayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
