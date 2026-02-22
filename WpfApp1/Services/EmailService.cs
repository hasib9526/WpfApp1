using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Exchange.WebServices.Data;
using Newtonsoft.Json;
using WpfApp1.Models;
using SysTask = System.Threading.Tasks.Task;
using AppEmailMessage = WpfApp1.Models.EmailMessage;
using EwsEmailMessage = Microsoft.Exchange.WebServices.Data.EmailMessage;

namespace WpfApp1.Services
{
    public class EmailResult
    {
        public bool                  IsSuccess { get; init; }
        public string                Error     { get; init; } = string.Empty;
        public List<AppEmailMessage> Emails    { get; init; } = new();
    }

    public class EmailService
    {
        private readonly string _ewsUrl;

        public EmailService()
        {
            _ewsUrl = ResolveEwsUrl();

            // Trust all SSL certificates (for internal Exchange with self-signed cert)
            ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, errors) => true;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        private static string ResolveEwsUrl()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var cfg  = JsonConvert.DeserializeObject<dynamic>(json);
                    string? url = cfg?.EwsUrl;
                    if (!string.IsNullOrWhiteSpace(url)) return url!;
                }
            }
            catch { }

            var email = AppState.Email;
            if (!string.IsNullOrEmpty(email) && email.Contains('@'))
            {
                var domain = email.Split('@')[1];
                return $"https://mail.{domain}/EWS/Exchange.asmx";
            }

            return "https://mail.bitopibd.com/EWS/Exchange.asmx";
        }

        private ExchangeService CreateService() =>
            new ExchangeService(ExchangeVersion.Exchange2013_SP1)
            {
                Credentials = new WebCredentials(AppState.Email, AppState.MailPassword),
                Url         = new Uri(_ewsUrl),
                Timeout     = 15000
            };

        /// <summary>
        /// Fetches unread emails from Exchange inbox via EWS.
        /// Returns IsSuccess=true (even if inbox is empty) on successful auth.
        /// Returns IsSuccess=false with Error message on auth/connection failure.
        /// </summary>
        public async System.Threading.Tasks.Task<EmailResult> GetUnreadEmailsAsync(int maxCount = 30)
        {
            if (string.IsNullOrEmpty(AppState.Email) || string.IsNullOrEmpty(AppState.MailPassword))
                return new EmailResult { IsSuccess = false, Error = "Email or password not set." };

            try
            {
                var list = await SysTask.Run(() =>
                {
                    var service = CreateService();

                    var filter = new SearchFilter.IsEqualTo(
                        EmailMessageSchema.IsRead, false);

                    var view = new ItemView(maxCount)
                    {
                        PropertySet = new PropertySet(
                            BasePropertySet.IdOnly,
                            EmailMessageSchema.Subject,
                            EmailMessageSchema.From,
                            EmailMessageSchema.DateTimeReceived)
                    };
                    view.OrderBy.Add(
                        EmailMessageSchema.DateTimeReceived, SortDirection.Descending);

                    var found = service.FindItems(WellKnownFolderName.Inbox, filter, view);

                    var result = new List<AppEmailMessage>();
                    foreach (Item item in found)
                    {
                        if (item is EwsEmailMessage msg)
                        {
                            result.Add(new AppEmailMessage
                            {
                                Uid     = msg.Id.UniqueId,
                                From    = msg.From?.Name ?? msg.From?.Address ?? "Unknown",
                                Subject = msg.Subject ?? "(no subject)",
                                Date    = msg.DateTimeReceived.ToLocalTime()
                            });
                        }
                    }
                    return result;
                });

                return new EmailResult { IsSuccess = true, Emails = list };
            }
            catch (ServiceRequestException ex)
            {
                return new EmailResult
                {
                    IsSuccess = false,
                    Error = $"Authentication failed: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new EmailResult
                {
                    IsSuccess = false,
                    Error = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        /// <summary>Fetches full email body (plain text) by item UID.</summary>
        public async System.Threading.Tasks.Task<EmailBodyResult> GetEmailBodyAsync(string uid)
        {
            try
            {
                return await SysTask.Run(() =>
                {
                    var service = CreateService();

                    var propSet = new PropertySet(
                        BasePropertySet.FirstClassProperties,
                        ItemSchema.Body,
                        EmailMessageSchema.From,
                        EmailMessageSchema.ToRecipients,
                        EmailMessageSchema.Subject,
                        EmailMessageSchema.DateTimeReceived);

                    var msg = EwsEmailMessage.Bind(service, new ItemId(uid), propSet);

                    var body = msg.Body?.Text ?? string.Empty;

                    // Strip HTML tags if body is HTML
                    if (msg.Body?.BodyType == BodyType.HTML)
                        body = StripHtml(body);

                    return new EmailBodyResult
                    {
                        Subject = msg.Subject ?? "(no subject)",
                        From    = msg.From?.Name ?? msg.From?.Address ?? "Unknown",
                        Date    = msg.DateTimeReceived.ToLocalTime(),
                        Body    = body.Trim()
                    };
                });
            }
            catch (Exception ex)
            {
                return new EmailBodyResult
                {
                    Subject = "Error",
                    Body    = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            // Remove style/script blocks
            html = System.Text.RegularExpressions.Regex.Replace(
                html, @"<(style|script)[^>]*>.*?</(style|script)>",
                string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Replace block tags with newlines
            html = System.Text.RegularExpressions.Regex.Replace(
                html, @"<(br|p|div|tr|li)[^>]*>", "\n",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Remove remaining tags
            html = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
            // Decode HTML entities
            html = System.Net.WebUtility.HtmlDecode(html);
            // Collapse excess blank lines
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\n{3,}", "\n\n");
            return html.Trim();
        }

        // ── EWS Pull Subscription — fires instantly when mail arrives ────────────

        private volatile bool _syncRunning;
        private Action?       _onUpdate;

        /// <summary>
        /// Starts a background loop using EWS PullSubscription.
        /// GetEvents() blocks (up to 30 min) waiting for inbox events — no timer needed.
        /// Fires <paramref name="onUpdate"/> the moment a NewMail / Deleted / Modified event arrives.
        /// </summary>
        public void StartRealtimeSync(Action onUpdate)
        {
            _onUpdate    = onUpdate;
            _syncRunning = true;
            System.Threading.Tasks.Task.Run(() => PullLoop());
        }

        private void PullLoop()
        {
            while (_syncRunning)
            {
                try
                {
                    var service = CreateService();

                    // Subscribe — 30-minute window, auto-renews in loop
                    var sub = service.SubscribeToPullNotifications(
                        new FolderId[] { WellKnownFolderName.Inbox },
                        30, // minutes before subscription expires
                        null,
                        EventType.NewMail,
                        EventType.Deleted,
                        EventType.Modified);

                    while (_syncRunning)
                    {
                        // Blocks here until an event fires OR 30-min timeout
                        var events = sub.GetEvents();

                        if (!_syncRunning) break;

                        if (events.ItemEvents.Any() || events.FolderEvents.Any())
                            _onUpdate?.Invoke();
                    }
                }
                catch
                {
                    if (_syncRunning)
                        Thread.Sleep(10_000); // wait 10s then reconnect
                }
            }
        }

        public void StopRealtimeSync()
        {
            _syncRunning = false;
            _onUpdate    = null;
        }

        // ── Open email in mail app via .eml temp file ─────────────────────────

        /// <summary>
        /// Downloads raw MIME bytes for the given item UID.
        /// Save as .eml and Process.Start to open in Outlook / default mail app.
        /// </summary>
        public async System.Threading.Tasks.Task<byte[]?> GetEmailMimeAsync(string uid)
        {
            try
            {
                return await SysTask.Run(() =>
                {
                    var service = CreateService();
                    var propSet = new PropertySet(ItemSchema.MimeContent);
                    var item    = Item.Bind(service, new ItemId(uid), propSet);
                    return item.MimeContent?.Content;
                });
            }
            catch { return null; }
        }

        public static void ResetConfig() { /* kept for API compatibility */ }
    }

    public class EmailBodyResult
    {
        public string   Subject { get; init; } = string.Empty;
        public string   From    { get; init; } = string.Empty;
        public DateTime Date    { get; init; }
        public string   Body    { get; init; } = string.Empty;
    }
}
