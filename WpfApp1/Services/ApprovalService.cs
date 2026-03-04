using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    /// <summary>
    /// Polls /api/approval every 1 minute on a background thread.
    /// Fires DataUpdated (on UI thread) when fresh data arrives and
    /// shows a toast when new pending approvals are detected.
    /// </summary>
    public class ApprovalService
    {
        private readonly ApiService           _api;
        private readonly NotificationService  _notifService;
        private readonly Dictionary<int, int> _prevCounts = new();
        private bool _isFirstLoad = true;
        private CancellationTokenSource? _cts;

        /// <summary>Raised on the UI thread whenever fresh approval data arrives.</summary>
        public event Action<ApprovalSummaryResponse>? DataUpdated;

        public ApprovalService(NotificationService notifService)
        {
            _api          = new ApiService();
            _notifService = notifService;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = RunLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        public Task RefreshAsync() => FetchAsync();

        private async Task RunLoopAsync(CancellationToken ct)
        {
            await FetchAsync();                          // immediate first fetch
            while (!ct.IsCancellationRequested)
            {
                try   { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
                catch (TaskCanceledException) { break; }

                if (!ct.IsCancellationRequested)
                    await FetchAsync();
            }
        }

        private async Task FetchAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Approval] Fetching... UserCode={AppState.UserCode}");

                var data = await _api.GetApprovalsAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"[Approval] Got {data?.Approvals?.Count ?? -1} items, total={data?.TotalPending}");

                if (data == null) return;

                if (!_isFirstLoad)
                {
                    var newNames = new List<string>();
                    int newCount = 0;

                    foreach (var item in data.Approvals)
                    {
                        var prev = _prevCounts.TryGetValue(item.Approval, out var p) ? p : 0;
                        if (item.Request > prev)
                        {
                            newNames.Add(item.ApprovalName);
                            newCount += item.Request - prev;
                        }
                    }

                    if (newNames.Count > 0)
                        _notifService.PushLocal(
                            $"New Pending Approval{(newCount > 1 ? "s" : "")} ({newCount})",
                            string.Join(", ", newNames),
                            "approval");
                }

                foreach (var item in data.Approvals)
                    _prevCounts[item.Approval] = item.Request;

                _isFirstLoad = false;

                // Fire event on UI thread
                Application.Current?.Dispatcher.Invoke(() => DataUpdated?.Invoke(data));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Approval] ERROR: {ex.Message}");
            }
        }
    }
}
