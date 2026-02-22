using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WpfApp1.Helpers;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class DashboardWindow : Window
    {
        private readonly NotificationService _notifService;
        private readonly EmailService       _emailService;
        private readonly DispatcherTimer    _badgeTimer;
        private readonly HashSet<string>    _knownEmailUids = new();
        private bool                        _emailFirstLoad = true;

        public DashboardWindow()
        {
            InitializeComponent();
            lblWelcome.Text  = $"Hi, {AppState.UserName}!";
            txtMailEmail.Text = AppState.Email;

            _notifService = new NotificationService();
            _emailService = new EmailService();

            LoadDashboard();

            // Pin to desktop layer
            Loaded += async (_, _) =>
            {
                DesktopHelper.PinToDesktop(this);

                // Start notification polling (checks API every 30s)
                _notifService.Start();

                // Auto-connect email if mail password was restored from session
                if (!string.IsNullOrEmpty(AppState.MailPassword))
                    await AutoConnectEmailAsync();
            };

            // Update notification badge every 5 seconds
            _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _badgeTimer.Tick += (_, _) => UpdateBadge();
            _badgeTimer.Start();

        }

        /// <summary>
        /// Access notification service from outside (e.g., App.xaml.cs)
        /// </summary>
        public NotificationService NotificationService => _notifService;

        private void LoadDashboard()
        {
            var todos = AppState.Todos.OrderByDescending(t => t.CreatedAt).ToList();
            lstTodos.ItemsSource = null;
            lstTodos.ItemsSource = todos;

            var total = todos.Count;
            var done = todos.Count(t => t.IsCompleted);

            lblTotal.Text = total.ToString();
            lblDone.Text = done.ToString();
            lblPending.Text = (total - done).ToString();

            lblLastLogin.Text = $"{DateTime.Now:MMM dd, hh:mm tt}";

            UpdateBadge();
            UpdateNotificationList();
        }

        private void UpdateBadge()
        {
            var unread = AppState.Notifications.Count(n => !n.IsRead);
            if (unread > 0)
            {
                badgeUnread.Visibility = Visibility.Visible;
                lblUnreadCount.Text = unread.ToString();
            }
            else
            {
                badgeUnread.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateNotificationList()
        {
            var recent = AppState.Notifications
                .OrderByDescending(n => n.ReceivedAt)
                .Take(10)
                .ToList();
            lstNotifications.ItemsSource = null;
            lstNotifications.ItemsSource = recent;
        }

        private async Task LoadEmailsAsync()
        {
            lblEmailStatus.Text = "Inbox (loading...)";

            var result = await _emailService.GetUnreadEmailsAsync();

            lblEmailStatus.Text = "Inbox";

            if (!result.IsSuccess)
            {
                ShowConnectPanel(result.Error);
                return;
            }

            // Detect new emails and show toast — skip toasts on first load
            foreach (var mail in result.Emails)
            {
                bool isNew = _knownEmailUids.Add(mail.Uid);
                if (isNew && !_emailFirstLoad)
                    _notifService.PushLocal($"New Email from {mail.From}", mail.Subject, "email");
            }
            _emailFirstLoad = false;

            ApplyEmailList(result.Emails);
        }

        /// <summary>Called on startup when a mail password was restored from the saved session.</summary>
        private async Task AutoConnectEmailAsync()
        {
            lblEmailStatus.Text = "Inbox (connecting...)";
            var result = await _emailService.GetUnreadEmailsAsync();

            if (result.IsSuccess)
            {
                mailConnectPanel.Visibility = Visibility.Collapsed;
                mailListPanel.Visibility    = Visibility.Visible;
                ApplyEmailList(result.Emails);

                _emailService.StartRealtimeSync(() =>
                    Dispatcher.InvokeAsync(async () => await LoadEmailsAsync()));
            }
            else
            {
                // Saved password is stale — clear it and show connect panel
                AppState.MailPassword = string.Empty;
                SessionService.SaveMailPassword(string.Empty);
                lblEmailStatus.Text = "Inbox";
            }
        }

        private void ApplyEmailList(List<EmailMessage> emails)
        {
            lstEmails.ItemsSource = null;
            lstEmails.ItemsSource = emails;

            lblNoMail.Visibility = emails.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (emails.Count > 0)
            {
                lblEmailUnreadCount.Text    = emails.Count.ToString();
                badgeEmailUnread.Visibility = Visibility.Visible;
            }
            else
            {
                badgeEmailUnread.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowConnectPanel(string error = "")
        {
            AppState.MailPassword           = string.Empty;
            mailConnectPanel.Visibility     = Visibility.Visible;
            mailListPanel.Visibility        = Visibility.Collapsed;
            badgeEmailUnread.Visibility     = Visibility.Collapsed;
            lblEmailStatus.Text             = "Inbox";

            if (!string.IsNullOrEmpty(error))
            {
                lblMailError.Text       = error;
                lblMailError.Visibility = Visibility.Visible;
            }
            else
            {
                lblMailError.Visibility = Visibility.Collapsed;
            }
        }

        private void EmailBadge_Click(object sender, MouseButtonEventArgs e)
        {
            emailPanel.Visibility = emailPanel.Visibility == Visibility.Collapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void BtnMailConnect_Click(object sender, RoutedEventArgs e)
        {
            var pass = txtMailPassword.Password;
            if (string.IsNullOrEmpty(pass)) return;

            // Use custom email if entered, otherwise keep existing AppState.Email
            var customEmail = txtMailEmail.Text.Trim();
            if (!string.IsNullOrEmpty(customEmail))
                AppState.Email = customEmail;

            btnMailConnect.IsEnabled = false;
            lblMailError.Visibility  = Visibility.Collapsed;
            lblEmailStatus.Text      = "Inbox (connecting...)";

            AppState.MailPassword = pass;
            var result = await _emailService.GetUnreadEmailsAsync();

            if (result.IsSuccess)
            {
                // Persist mail password so next launch auto-connects
                SessionService.SaveMailPassword(pass);

                mailConnectPanel.Visibility = Visibility.Collapsed;
                mailListPanel.Visibility    = Visibility.Visible;
                ApplyEmailList(result.Emails);

                // Start real-time streaming sync — badge updates instantly on new mail
                _emailService.StartRealtimeSync(() =>
                    Dispatcher.InvokeAsync(async () => await LoadEmailsAsync()));
            }
            else
            {
                ShowConnectPanel(result.Error);
            }

            btnMailConnect.IsEnabled = true;
        }

        private void BtnMailDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _emailService.StopRealtimeSync();
            EmailService.ResetConfig();
            txtMailPassword.Password = string.Empty;
            ShowConnectPanel();
        }

        private void TxtMailPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnMailConnect_Click(sender, e);
        }

        private async void EmailItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not WpfApp1.Models.EmailMessage mail) return;

            // Download raw MIME and open as .eml in Outlook / default mail app
            var bytes = await _emailService.GetEmailMimeAsync(mail.Uid);
            if (bytes == null || bytes.Length == 0) return;

            var tempFile = Path.Combine(
                Path.GetTempPath(), $"mail_{Guid.NewGuid():N}.eml");
            File.WriteAllBytes(tempFile, bytes);
            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }

        private void NotifBadge_Click(object sender, MouseButtonEventArgs e)
        {
            // Toggle notification panel
            if (notifPanel.Visibility == Visibility.Collapsed)
            {
                // Mark all as read
                foreach (var n in AppState.Notifications)
                    n.IsRead = true;

                UpdateBadge();
                UpdateNotificationList();
                notifPanel.Visibility = Visibility.Visible;
            }
            else
            {
                notifPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnAddTodo_Click(object sender, RoutedEventArgs e)
        {
            var title = txtNewTodo.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;

            AppState.Todos.Add(new TodoItem
            {
                Id = AppState.GetNextId(),
                Title = title,
                IsCompleted = false,
                CreatedAt = DateTime.Now
            });

            txtNewTodo.Text = "";
            LoadDashboard();
        }

        private void TxtNewTodo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnAddTodo_Click(sender, e);
        }

        private void TodoCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is TodoItem todo)
            {
                var item = AppState.Todos.FirstOrDefault(t => t.Id == todo.Id);
                if (item != null)
                    item.IsCompleted = !item.IsCompleted;
                LoadDashboard();
            }
        }

        private void BtnDeleteTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TodoItem todo)
            {
                AppState.Todos.RemoveAll(t => t.Id == todo.Id);
                LoadDashboard();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            menuPopup.IsOpen = !menuPopup.IsOpen;
        }

        private async void BtnLogoutMenu_Click(object sender, RoutedEventArgs e)
        {
            menuPopup.IsOpen = false;

            try { await new ApiService().LogoutAsync(); } catch { }

            _emailService.StopRealtimeSync();
            _notifService.Stop();
            _badgeTimer.Stop();
            AppState.Clear();
            SessionService.Clear();

            this.Hide();
            App.ShowLogin();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                e.Handled = true; // stop bubbling to Window_MouseLeftButtonDown
                var hwnd = new WindowInteropHelper(this).Handle;
                SendMessage(hwnd, 0x112, (IntPtr)0xF008, IntPtr.Zero);
            }
        }
    }
}
