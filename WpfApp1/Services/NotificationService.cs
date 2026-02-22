using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    /// <summary>
    /// Polls the API for new notifications and shows toast popups
    /// on top of ALL windows (Chrome, games, everything).
    /// </summary>
    public class NotificationService
    {
        private readonly DispatcherTimer _pollTimer;
        private readonly HttpClient _client;
        private readonly List<NotificationWindow> _activeToasts = new();
        private const string BaseUrl = "http://localhost:5000/api";

        // How often to check for new notifications (seconds)
        private const int PollIntervalSeconds = 30;

        public NotificationService()
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(PollIntervalSeconds)
            };
            _pollTimer.Tick += async (_, _) => await CheckForNotifications();
        }

        public void Start()
        {
            _pollTimer.Start();

            // Also check immediately on start
            _ = CheckForNotifications();
        }

        public void Stop()
        {
            _pollTimer.Stop();
        }

        private async System.Threading.Tasks.Task CheckForNotifications()
        {
            try
            {
                if (string.IsNullOrEmpty(AppState.Token)) return;

                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AppState.Token);

                var response = await _client.GetAsync("/api/notifications/unread");
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var notifications = JsonConvert.DeserializeObject<List<NotificationItem>>(json);

                if (notifications == null || notifications.Count == 0) return;

                // Show toast for each new notification
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var notif in notifications)
                    {
                        // Skip if already shown
                        if (AppState.Notifications.Any(n => n.Id == notif.Id)) continue;

                        AppState.Notifications.Add(notif);
                        ShowToast(notif.Title, notif.Message, notif.Type);
                    }
                });
            }
            catch
            {
                // Server not available — that's okay, keep trying
            }
        }

        /// <summary>
        /// Shows a toast popup ON TOP of everything.
        /// Call this from anywhere to notify the user.
        /// </summary>
        public void ShowToast(string title, string message, string type = "info")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Clean up closed toasts
                _activeToasts.RemoveAll(t => !t.IsLoaded);

                var toast = new NotificationWindow(title, message, type);
                toast.StackBelow(_activeToasts.Count);
                toast.Closed += (_, _) => _activeToasts.Remove(toast);
                _activeToasts.Add(toast);
                toast.Show();
            });
        }

        /// <summary>
        /// Manually push a local notification + show toast.
        /// Use this when you don't need the API.
        /// </summary>
        public void PushLocal(string title, string message, string type = "info")
        {
            var notif = new NotificationItem
            {
                Id = AppState.GetNextNotifId(),
                Title = title,
                Message = message,
                Type = type,
                ReceivedAt = DateTime.Now,
                IsRead = false
            };

            AppState.Notifications.Add(notif);
            ShowToast(title, message, type);
        }

        /// <summary>
        /// Returns count of unread notifications.
        /// </summary>
        public int UnreadCount => AppState.Notifications.Count(n => !n.IsRead);
    }
}
