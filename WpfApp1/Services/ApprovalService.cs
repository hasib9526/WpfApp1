using System;
using System.Collections.Generic;
using System.Windows.Threading;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    /// <summary>
    /// Polls /api/approval every 5 minutes. Fires DataUpdated when fresh data
    /// arrives and shows a toast when new pending approvals are detected.
    /// </summary>
    public class ApprovalService
    {
        private readonly DispatcherTimer     _timer;
        private readonly ApiService          _api;
        private readonly NotificationService _notifService;
        private readonly Dictionary<int, int> _prevCounts = new();
        private bool _isFirstLoad = true;

        /// <summary>Raised on the UI thread whenever fresh approval data arrives.</summary>
        public event Action<ApprovalSummaryResponse>? DataUpdated;

        public ApprovalService(NotificationService notifService)
        {
            _api          = new ApiService();
            _notifService = notifService;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _timer.Tick += async (_, _) => await FetchAsync();
        }

        public void Start()
        {
            _timer.Start();
            _ = FetchAsync();   // immediate first fetch
        }

        public void Stop() => _timer.Stop();

        private async System.Threading.Tasks.Task FetchAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Approval] Fetching... UserCode={WpfApp1.Models.AppState.UserCode}");

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
                        var prev = _prevCounts.TryGetValue(item.Approval, out var p)
                                   ? p : item.Request;

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

                // Update baseline counts
                foreach (var item in data.Approvals)
                    _prevCounts[item.Approval] = item.Request;

                _isFirstLoad = false;
                DataUpdated?.Invoke(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Approval] ERROR: {ex.Message}");
            }
        }
    }
}
