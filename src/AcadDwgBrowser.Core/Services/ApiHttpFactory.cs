using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using AcadDwgBrowser.Core.Configuration;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>Creates HttpClient with cookie session (access_token / refresh_token / csrf_token) from ConstrTodo auth.</summary>
    public static class ApiHttpFactory
    {
        public const string AccessTokenCookie = "access_token";
        public const string RefreshTokenCookie = "refresh_token";
        public const string CsrfTokenCookie = "csrf_token";
        public const string CsrfHeaderName = "X-CSRF-Token";

        public static HttpClient Create(PluginSettings settings, AuthSession? session = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
                throw new InvalidOperationException("В config.json не задан ApiBaseUrl.");

            var baseUri = new Uri(TrimSlash(settings.ApiBaseUrl) + "/");

            // Do not send cookies via CookieContainer — it can URL-encode csrf_token
            // differently from our explicit Cookie / X-CSRF-Token headers and break CSRF checks.
            var handler = new HttpClientHandler
            {
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var http = new HttpClient(handler)
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds))
            };

            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            ApplyAuthHeaders(http, settings, session);
            return http;
        }

        public static void ApplyAuthHeaders(HttpClient http, PluginSettings settings, AuthSession? session)
        {
            http.DefaultRequestHeaders.Remove("Authorization");
            http.DefaultRequestHeaders.Remove(CsrfHeaderName);
            http.DefaultRequestHeaders.Remove("Cookie");

            var bearer = session != null && IsRealToken(session.AccessToken)
                ? session.AccessToken
                : settings.ApiKey;

            if (!string.IsNullOrWhiteSpace(bearer))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            var csrf = session != null ? NormalizeToken(session.CsrfToken) : null;
            if (!string.IsNullOrWhiteSpace(csrf))
                http.DefaultRequestHeaders.TryAddWithoutValidation(CsrfHeaderName, csrf);

            // Explicit Cookie header — same decoded values as X-CSRF-Token (port :3005 safe).
            if (session != null)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (IsRealToken(session.AccessToken))
                    parts.Add(AccessTokenCookie + "=" + session.AccessToken.Trim());
                if (IsRealToken(session.RefreshToken))
                    parts.Add(RefreshTokenCookie + "=" + session.RefreshToken.Trim());
                if (!string.IsNullOrWhiteSpace(csrf))
                    parts.Add(CsrfTokenCookie + "=" + csrf);
                if (parts.Count > 0)
                    http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", parts));
            }
        }

        /// <summary>Decode percent-encoding once so cookie and header stay identical.</summary>
        public static string? NormalizeToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var value = token.Trim();
            try
            {
                // Only unescape when it looks percent-encoded; avoid corrupting '+' etc.
                if (value.IndexOf('%') >= 0)
                    value = Uri.UnescapeDataString(value);
            }
            catch
            {
                // keep trimmed raw
            }

            return value;
        }

        public static bool IsRealToken(string? token) =>
            !string.IsNullOrWhiteSpace(token)
            && !string.Equals(token, "cookie-session", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(token, "session", StringComparison.OrdinalIgnoreCase);

        public static string TrimSlash(string value) => (value ?? string.Empty).TrimEnd('/');
    }
}
