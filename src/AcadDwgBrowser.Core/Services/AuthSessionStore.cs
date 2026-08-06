using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    public static class AuthSessionStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string DefaultPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadDwgBrowser",
                "auth-session.json");

        public static AuthSession? Load(string? path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<StoredSession>(json, JsonOptions);
                if (dto == null || string.IsNullOrWhiteSpace(dto.AccessToken))
                    return null;

                return new AuthSession
                {
                    AccessToken = dto.AccessToken ?? string.Empty,
                    CsrfToken = ApiHttpFactory.NormalizeToken(dto.CsrfToken) ?? dto.CsrfToken ?? string.Empty,
                    Email = dto.Email ?? string.Empty,
                    User = dto.User
                };
            }
            catch
            {
                return null;
            }
        }

        public static void Save(AuthSession session, string? path = null)
        {
            path = path ?? DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var dto = new StoredSession
            {
                AccessToken = session.AccessToken,
                CsrfToken = session.CsrfToken,
                Email = session.Email,
                User = session.User
            };
            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
        }

        public static void Clear(string? path = null)
        {
            path = path ?? DefaultPath;
            if (File.Exists(path))
                File.Delete(path);
        }

        private sealed class StoredSession
        {
            public string? AccessToken { get; set; }
            public string? CsrfToken { get; set; }
            public string? Email { get; set; }
            public UserFullInfo? User { get; set; }
        }
    }
}
