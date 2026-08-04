using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using AcadDwgBrowser.Core.Configuration;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>Creates HttpClient with cookie session (access_token / csrf_token) from ConstrTodo auth.</summary>
    public static class ApiHttpFactory
    {
        public const string AccessTokenCookie = "access_token";
        public const string CsrfTokenCookie = "csrf_token";
        public const string CsrfHeaderName = "X-CSRF-Token";

        public static HttpClient Create(PluginSettings settings, AuthSession? session = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
                throw new InvalidOperationException("В config.json не задан ApiBaseUrl.");

            var baseUri = new Uri(TrimSlash(settings.ApiBaseUrl) + "/");
            var cookies = new CookieContainer();

            if (session != null)
                ApplySessionCookies(cookies, baseUri, session);

            var handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                UseCookies = true,
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

        public static void ApplySessionCookies(CookieContainer cookies, Uri baseUri, AuthSession session)
        {
            if (IsRealToken(session.AccessToken))
            {
                try
                {
                    cookies.Add(baseUri, new Cookie(AccessTokenCookie, session.AccessToken, "/", baseUri.Host));
                }
                catch
                {
                    cookies.Add(baseUri, new Cookie(AccessTokenCookie, session.AccessToken) { Path = "/" });
                }
            }

            if (!string.IsNullOrWhiteSpace(session.CsrfToken))
            {
                try
                {
                    cookies.Add(baseUri, new Cookie(CsrfTokenCookie, session.CsrfToken, "/", baseUri.Host));
                }
                catch
                {
                    cookies.Add(baseUri, new Cookie(CsrfTokenCookie, session.CsrfToken) { Path = "/" });
                }
            }
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

            if (session != null && !string.IsNullOrWhiteSpace(session.CsrfToken))
                http.DefaultRequestHeaders.TryAddWithoutValidation(CsrfHeaderName, session.CsrfToken);

            // Explicit Cookie header — more reliable than CookieContainer on non-standard ports (:3005).
            if (session != null)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (IsRealToken(session.AccessToken))
                    parts.Add(AccessTokenCookie + "=" + session.AccessToken);
                if (!string.IsNullOrWhiteSpace(session.CsrfToken))
                    parts.Add(CsrfTokenCookie + "=" + session.CsrfToken);
                if (parts.Count > 0)
                    http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", parts));
            }
        }

        public static bool IsRealToken(string? token) =>
            !string.IsNullOrWhiteSpace(token)
            && !string.Equals(token, "cookie-session", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(token, "session", StringComparison.OrdinalIgnoreCase);

        public static string TrimSlash(string value) => (value ?? string.Empty).TrimEnd('/');
    }
}
