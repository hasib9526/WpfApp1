using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class ApiService
    {
        private readonly HttpClient _client;

        // Reads server URL from config.json next to the exe, falls back to PPC API server
        public static string ServerUrl
        {
            get
            {
                try
                {
                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                    if (File.Exists(configPath))
                    {
                        var json = File.ReadAllText(configPath);
                        var cfg  = JsonConvert.DeserializeObject<dynamic>(json);
                        string? url = cfg?.ServerUrl;
                        if (!string.IsNullOrWhiteSpace(url)) return url!.TrimEnd('/');
                    }
                }
                catch { }
                return "http://172.16.13.6:13806/api";
            }
        }

        public ApiService()
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri(ServerUrl.TrimEnd('/') + "/"),
                Timeout     = TimeSpan.FromSeconds(10)
            };
        }

        private void SetAuthHeader()
        {
            if (!string.IsNullOrEmpty(AppState.Token))
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AppState.Token);
        }

        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            var request = new LoginRequest { UserName = username, Password = password };

            var query = $"userName={Uri.EscapeDataString(request.UserName)}" +
                        $"&Password={Uri.EscapeDataString(request.Password)}" +
                        $"&DeviceID={Uri.EscapeDataString(request.DeviceID)}" +
                        $"&DeviceToken={Uri.EscapeDataString(request.DeviceToken)}" +
                        $"&DeviceName={Uri.EscapeDataString(request.DeviceName)}" +
                        $"&Platform={Uri.EscapeDataString(request.Platform)}" +
                        $"&QryOption={request.QryOption}" +
                        $"&VersionCode={request.VersionCode}" +
                        $"&UserCode={request.UserCode}" +
                        $"&OSName={Uri.EscapeDataString(request.OSName)}" +
                        $"&OSVersion={Uri.EscapeDataString(request.OSVersion)}";

            var resp = await _client.GetAsync($"Account/GetUserInfo?{query}");

            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(body);

            // Validate: UserCode and UserName must be present (empty = invalid credentials)
            if (loginResponse == null ||
                string.IsNullOrWhiteSpace(loginResponse.UserCode) ||
                string.IsNullOrWhiteSpace(loginResponse.UserName))
                return null;

            return loginResponse;
        }

        public async Task LogoutAsync()
        {
            SetAuthHeader();
            var json    = JsonConvert.SerializeObject(new { UserCode = AppState.UserCode });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _client.PostAsync("/api/auth/logout", content);
        }
    }
}
