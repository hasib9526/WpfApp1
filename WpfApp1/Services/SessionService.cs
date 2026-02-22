using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class SessionData
    {
        public string UserName               { get; set; } = string.Empty;
        public string UserCode               { get; set; } = string.Empty;
        public string EmployeeName           { get; set; } = string.Empty;
        public string Department             { get; set; } = string.Empty;
        public string Designation            { get; set; } = string.Empty;
        public string Email                  { get; set; } = string.Empty;
        public string EncryptedPassword      { get; set; } = string.Empty;
        public string EncryptedMailPassword  { get; set; } = string.Empty;
    }

    public static class SessionService
    {
        private static string SessionPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WpfApp1",
                "session.dat");

        /// <summary>Saves login credentials and profile after a successful login.</summary>
        public static void SaveLogin(string plainPassword, LoginResponse resp)
        {
            try
            {
                var dir = Path.GetDirectoryName(SessionPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // Preserve any existing mail password
                var existing = LoadRaw();

                var data = new SessionData
                {
                    UserName              = resp.UserName     ?? string.Empty,
                    UserCode              = resp.UserCode     ?? string.Empty,
                    EmployeeName          = resp.EmployeeName ?? string.Empty,
                    Department            = resp.Department   ?? string.Empty,
                    Designation           = resp.Designation  ?? string.Empty,
                    Email                 = resp.Email        ?? string.Empty,
                    EncryptedPassword     = Encrypt(plainPassword),
                    EncryptedMailPassword = existing?.EncryptedMailPassword ?? string.Empty
                };

                File.WriteAllText(SessionPath, JsonConvert.SerializeObject(data));
            }
            catch { }
        }

        /// <summary>Saves the mail password separately (called after successful EWS connect).</summary>
        public static void SaveMailPassword(string mailPassword)
        {
            try
            {
                var existing = LoadRaw();
                if (existing == null) return;

                existing.EncryptedMailPassword = Encrypt(mailPassword);
                File.WriteAllText(SessionPath, JsonConvert.SerializeObject(existing));
            }
            catch { }
        }

        /// <summary>
        /// Loads the saved session and decrypts both passwords.
        /// Returns null if no session exists or decryption fails.
        /// </summary>
        public static (SessionData Data, string Password, string MailPassword)? LoadFull()
        {
            try
            {
                var data = LoadRaw();
                if (data == null || string.IsNullOrEmpty(data.EncryptedPassword))
                    return null;

                var password = Decrypt(data.EncryptedPassword);
                var mail     = string.IsNullOrEmpty(data.EncryptedMailPassword)
                                   ? string.Empty
                                   : Decrypt(data.EncryptedMailPassword);

                return (data, password, mail);
            }
            catch { return null; }
        }

        /// <summary>Deletes the saved session (call on logout).</summary>
        public static void Clear()
        {
            try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch { }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static SessionData? LoadRaw()
        {
            try
            {
                if (!File.Exists(SessionPath)) return null;
                return JsonConvert.DeserializeObject<SessionData>(File.ReadAllText(SessionPath));
            }
            catch { return null; }
        }

        private static string Encrypt(string plainText)
        {
            var bytes     = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string Decrypt(string base64)
        {
            var bytes     = Convert.FromBase64String(base64);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
