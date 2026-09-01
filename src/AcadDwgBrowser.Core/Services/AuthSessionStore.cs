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
                if (dto == null)
                    return null;
                if (string.IsNullOrWhiteSpace(dto.AccessToken)
                    && string.IsNullOrWhiteSpace(dto.RefreshToken))
                    return null;

                return new AuthSession
                {
                    AccessToken = dto.AccessToken ?? string.Empty,
                    RefreshToken = dto.RefreshToken ?? string.Empty,
                    CsrfToken = ApiHttpFactory.NormalizeToken(dto.CsrfToken) ?? dto.CsrfToken ?? string.Empty,
                    Email = dto.Email ?? string.Empty,
                    AccessExpiresAt = ParseTimestamp(dto.AccessExpiresAt),
                    RefreshExpiresAt = ParseTimestamp(dto.RefreshExpiresAt),
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
                RefreshToken = session.RefreshToken,
                CsrfToken = session.CsrfToken,
                Email = session.Email,
                AccessExpiresAt = FormatTimestamp(session.AccessExpiresAt),
                RefreshExpiresAt = FormatTimestamp(session.RefreshExpiresAt),
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

        private static DateTimeOffset? ParseTimestamp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (DateTimeOffset.TryParse(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
                return parsed;
            return null;
        }

        private static string? FormatTimestamp(DateTimeOffset? value) =>
            value?.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        private sealed class StoredSession
        {
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public string? CsrfToken { get; set; }
            public string? Email { get; set; }
            public string? AccessExpiresAt { get; set; }
            public string? RefreshExpiresAt { get; set; }
            public UserFullInfo? User { get; set; }
        }
    }
}
