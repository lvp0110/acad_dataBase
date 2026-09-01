using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AcadDwgBrowser.Core.Configuration;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>
    /// ConstrTodo auth per swagger: POST /auth/login, POST /auth/refresh, GET /auth/session, POST /auth/logout.
    /// Login returns UserCredentials (access_token + refresh_token) and sets access_token / csrf_token cookies.
    /// </summary>
    public sealed class AuthApiClient : IAuthApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly PluginSettings _settings;

        public AuthApiClient(PluginSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.NormalizeAuthEndpoints();
        }

        public async Task<AuthSession> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Укажите email.", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Укажите пароль.", nameof(password));
            if (string.IsNullOrWhiteSpace(_settings.ApiBaseUrl)
                || _settings.ApiBaseUrl.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    "Укажите реальный адрес API (ApiBaseUrl), а не api.example.com.");
            }

            try
            {
                return await LoginCoreAsync(email.Trim(), password, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AuthDebugLog.Write("Login exception: " + ex);
                throw new InvalidOperationException(DescribeNetworkError(ex, _settings.ApiBaseUrl), ex);
            }
        }

        private async Task<AuthSession> LoginCoreAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            var cookies = new CookieContainer();
            var baseUri = new Uri(ApiHttpFactory.TrimSlash(_settings.ApiBaseUrl) + "/");

            using (var handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                UseCookies = true,
                UseDefaultCredentials = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            using (var http = new HttpClient(handler)
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(Math.Max(5, _settings.TimeoutSeconds))
            })
            {
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                AuthDebugLog.Write("POST " + baseUri + _settings.LoginEndpoint.TrimStart('/') +
                                   " email=" + email);

                var payload = new LoginRequest { Email = email, Password = password };
                var json = JsonSerializer.Serialize(payload, JsonOptions);

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    // Some Go servers are picky about charset in Content-Type.
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    using (var response = await http.PostAsync(
                               _settings.LoginEndpoint.TrimStart('/'),
                               content,
                               cancellationToken).ConfigureAwait(false))
                    {
                        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var requestUri = response.RequestMessage?.RequestUri ?? baseUri;

                        ExtractTokens(response, cookies, baseUri, requestUri, responseText,
                            out var access, out var refresh, out var csrf, out var cookieDump);

                        AuthDebugLog.Write(
                            "Login HTTP " + (int)response.StatusCode +
                            " cookies=[" + cookieDump + "]" +
                            " accessLen=" + (access?.Length ?? 0) +
                            " refreshLen=" + (refresh?.Length ?? 0) +
                            " csrfLen=" + (csrf?.Length ?? 0) +
                            " body=" + Truncate(responseText, 400));

                        if (!response.IsSuccessStatusCode)
                            throw new InvalidOperationException(FormatApiError(response.StatusCode, responseText));

                        var session = new AuthSession { Email = email };
                        OverlayCookieTokens(session, access, refresh, csrf);
                        ApplyAuthPayload(session, responseText);

                        if (!ApiHttpFactory.IsRealToken(session.AccessToken)
                            && session.User == null
                            && !session.HasRefreshToken)
                        {
                            throw new InvalidOperationException(
                                "Вход не подтверждён: нет access_token / refresh_token и нет данных пользователя. " +
                                "Подробности: %LocalAppData%\\AcadDwgBrowser\\login-debug.log");
                        }

                        if (!ApiHttpFactory.IsRealToken(session.AccessToken) && session.User != null)
                            session.AccessToken = "cookie-session";

                        AuthSessionStore.Save(session);
                        AuthDebugLog.Write("Login OK user=" + (session.User?.Email ?? email) +
                                           " csrfLen=" + session.CsrfToken.Length +
                                           " refreshLen=" + (session.RefreshToken?.Length ?? 0));
                        return session;
                    }
                }
            }
        }

        public async Task<AuthSession> GetSessionAsync(
            AuthSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            using (var http = ApiHttpFactory.Create(_settings, session))
            using (var response = await http.GetAsync(
                       _settings.SessionEndpoint.TrimStart('/'),
                       cancellationToken).ConfigureAwait(false))
            {
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AuthDebugLog.Write("Session HTTP " + (int)response.StatusCode + " body=" + Truncate(responseText, 300));

                if (!response.IsSuccessStatusCode)
                {
                    if (IsAuthFailure(response.StatusCode) && session.HasRefreshToken)
                    {
                        AuthDebugLog.Write("Session unauthorized — trying refresh token");
                        session = await RefreshSessionAsync(session, cancellationToken).ConfigureAwait(false);
                        try
                        {
                            return await GetSessionAfterRefreshAsync(session, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            AuthDebugLog.Write("Session after refresh: " + ex.Message);
                            return session;
                        }
                    }

                    throw new InvalidOperationException(FormatApiError(response.StatusCode, responseText));
                }

                ApplyHttpTokens(session, http, response, responseText);
                AuthSessionStore.Save(session);
                return session;
            }
        }

        public async Task<AuthSession> RefreshSessionAsync(
            AuthSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.HasRefreshToken)
                throw new InvalidOperationException("Нет refresh-токена. Войдите снова.");

            using (var http = ApiHttpFactory.Create(_settings, session))
            {
                var refreshed = await PostRefreshAsync(http, session, cancellationToken).ConfigureAwait(false);
                if (refreshed)
                    return session;

                // Some servers reject an expired Bearer; retry with refresh cookie/body only.
                http.DefaultRequestHeaders.Remove("Authorization");
                AuthDebugLog.Write("Refresh retry without Bearer");
                if (await PostRefreshAsync(http, session, cancellationToken).ConfigureAwait(false))
                    return session;

                throw new InvalidOperationException("Не удалось обновить сессию по refresh-токену. Войдите снова.");
            }
        }

        private async Task<bool> PostRefreshAsync(
            HttpClient http,
            AuthSession session,
            CancellationToken cancellationToken)
        {
            var endpoint = string.IsNullOrWhiteSpace(_settings.RefreshEndpoint)
                ? "auth/refresh"
                : _settings.RefreshEndpoint.TrimStart('/');
            var payload = new RefreshRequest { RefreshToken = session.RefreshToken.Trim() };
            var json = JsonSerializer.Serialize(payload, JsonOptions);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var response = await http.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false))
                {
                    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AuthDebugLog.Write(
                        "Refresh HTTP " + (int)response.StatusCode +
                        " body=" + Truncate(responseText, 300));

                    if (!response.IsSuccessStatusCode)
                        return false;

                    ApplyHttpTokens(session, http, response, responseText);
                    if (!ApiHttpFactory.IsRealToken(session.AccessToken) && !session.HasRefreshToken)
                        return false;

                    AuthSessionStore.Save(session);
                    AuthDebugLog.Write(
                        "Refresh OK accessLen=" + (session.AccessToken?.Length ?? 0) +
                        " refreshLen=" + (session.RefreshToken?.Length ?? 0) +
                        " csrfLen=" + (session.CsrfToken?.Length ?? 0));
                    return true;
                }
            }
        }

        private async Task<AuthSession> GetSessionAfterRefreshAsync(
            AuthSession session,
            CancellationToken cancellationToken)
        {
            using (var http = ApiHttpFactory.Create(_settings, session))
            using (var response = await http.GetAsync(
                       _settings.SessionEndpoint.TrimStart('/'),
                       cancellationToken).ConfigureAwait(false))
            {
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AuthDebugLog.Write(
                    "Session after refresh HTTP " + (int)response.StatusCode +
                    " body=" + Truncate(responseText, 300));

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(FormatApiError(response.StatusCode, responseText));

                ApplyHttpTokens(session, http, response, responseText);
                AuthSessionStore.Save(session);
                return session;
            }
        }

        /// <summary>
        /// Ensures CSRF is present and access token is not expired before POST/PUT.
        /// </summary>
        public async Task<AuthSession> EnsureFreshCsrfAsync(
            AuthSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            session.CsrfToken = ApiHttpFactory.NormalizeToken(session.CsrfToken) ?? session.CsrfToken;

            if (session.HasRefreshToken && session.IsAccessExpiring(TimeSpan.FromMinutes(1)))
            {
                try
                {
                    return await RefreshSessionAsync(session, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AuthDebugLog.Write("Proactive refresh failed: " + ex.Message);
                }
            }

            try
            {
                return await GetSessionAsync(session, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AuthDebugLog.Write("EnsureFreshCsrf fallback: " + ex.Message);
                if (session.HasRefreshToken)
                {
                    try
                    {
                        return await RefreshSessionAsync(session, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception refreshEx)
                    {
                        AuthDebugLog.Write("EnsureFreshCsrf refresh fallback: " + refreshEx.Message);
                    }
                }

                AuthSessionStore.Save(session);
                return session;
            }
        }

        public async Task LogoutAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            try
            {
                if (session != null && !string.IsNullOrWhiteSpace(session.AccessToken)
                    && !string.Equals(session.AccessToken, "cookie-session", StringComparison.Ordinal))
                {
                    using (var http = ApiHttpFactory.Create(_settings, session))
                    using (var response = await http.PostAsync(
                               _settings.LogoutEndpoint.TrimStart('/'),
                               content: null,
                               cancellationToken).ConfigureAwait(false))
                    {
                        AuthDebugLog.Write("Logout HTTP " + (int)response.StatusCode);
                    }
                }
            }
            finally
            {
                AuthSessionStore.Clear();
            }
        }

        private static void ExtractTokens(
            HttpResponseMessage response,
            CookieContainer cookies,
            Uri baseUri,
            Uri requestUri,
            string responseText,
            out string? accessToken,
            out string? refreshToken,
            out string? csrfToken,
            out string cookieDump)
        {
            accessToken = null;
            refreshToken = null;
            csrfToken = null;
            var names = new List<string>();

            CollectFromContainer(cookies, baseUri, names, ref accessToken, ref refreshToken, ref csrfToken);
            CollectFromContainer(cookies, requestUri, names, ref accessToken, ref refreshToken, ref csrfToken);

            // Host without explicit path variants
            try
            {
                var root = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + "/");
                CollectFromContainer(cookies, root, names, ref accessToken, ref refreshToken, ref csrfToken);
            }
            catch
            {
                // ignore
            }

            foreach (var raw in GetSetCookieHeaders(response))
            {
                ParseCookiePair(raw, ApiHttpFactory.AccessTokenCookie, ref accessToken);
                ParseCookiePair(raw, ApiHttpFactory.RefreshTokenCookie, ref refreshToken);
                ParseCookiePair(raw, ApiHttpFactory.CsrfTokenCookie, ref csrfToken);
                var name = raw.Split('=')[0].Trim();
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                    names.Add(name + "(hdr)");
            }

            TryExtractTokensFromBody(responseText, ref accessToken, ref refreshToken, ref csrfToken);
            cookieDump = names.Count == 0 ? "none" : string.Join(", ", names);
        }

        private static void CollectFromContainer(
            CookieContainer cookies,
            Uri uri,
            List<string> names,
            ref string? accessToken,
            ref string? refreshToken,
            ref string? csrfToken)
        {
            try
            {
                foreach (Cookie c in cookies.GetCookies(uri))
                {
                    var label = c.Name + "@" + uri.Host + ":" + uri.Port;
                    if (!names.Contains(label))
                        names.Add(label);

                    if (string.Equals(c.Name, ApiHttpFactory.AccessTokenCookie, StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(accessToken))
                        accessToken = c.Value;

                    if (string.Equals(c.Name, ApiHttpFactory.RefreshTokenCookie, StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(refreshToken))
                        refreshToken = c.Value;

                    if (string.Equals(c.Name, ApiHttpFactory.CsrfTokenCookie, StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(csrfToken))
                        csrfToken = c.Value;
                }
            }
            catch
            {
                // ignore
            }
        }

        private static IEnumerable<string> GetSetCookieHeaders(HttpResponseMessage response)
        {
            var list = new List<string>();
            try
            {
                if (response.Headers.TryGetValues("Set-Cookie", out var values))
                    list.AddRange(values);
            }
            catch
            {
                // ignore
            }

            // .NET may hide Set-Cookie from TryGetValues; iterate raw headers.
            try
            {
                foreach (var header in response.Headers)
                {
                    if (!string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                        continue;
                    list.AddRange(header.Value);
                }
            }
            catch
            {
                // ignore
            }

            return list.Distinct();
        }

        private static void ParseCookiePair(string setCookie, string name, ref string? value)
        {
            if (string.IsNullOrEmpty(setCookie) || !string.IsNullOrWhiteSpace(value))
                return;

            var parts = setCookie.Split(';');
            if (parts.Length == 0)
                return;

            var kv = parts[0].Split(new[] { '=' }, 2);
            if (kv.Length != 2)
                return;

            if (!string.Equals(kv[0].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return;

            value = kv[1].Trim();
            try
            {
                value = Uri.UnescapeDataString(value);
            }
            catch
            {
                // keep raw
            }
        }

        private static void TryExtractTokensFromBody(
            string body,
            ref string? access,
            ref string? refresh,
            ref string? csrf)
        {
            if (string.IsNullOrWhiteSpace(access))
                access = TryReadJsonString(body, "access_token") ?? TryReadJsonString(body, "token");
            if (string.IsNullOrWhiteSpace(refresh))
                refresh = TryReadJsonString(body, "refresh_token");
            if (string.IsNullOrWhiteSpace(csrf))
                csrf = TryReadJsonString(body, "csrf_token");
        }

        private static void ApplyHttpTokens(
            AuthSession session,
            HttpClient http,
            HttpResponseMessage response,
            string responseText)
        {
            var baseUri = http.BaseAddress ?? new Uri("http://localhost/");
            var requestUri = response.RequestMessage?.RequestUri ?? baseUri;
            ExtractTokens(response, new CookieContainer(), baseUri, requestUri, responseText,
                out var access, out var refresh, out var csrf, out _);
            OverlayCookieTokens(session, access, refresh, csrf);
            ApplyAuthPayload(session, responseText);
        }

        private static void ApplyAuthPayload(AuthSession session, string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return;

            try
            {
                var credentials = JsonSerializer.Deserialize<ApiEnvelope<UserCredentials>>(responseText, JsonOptions);
                if (credentials != null && credentials.Code >= 400)
                    throw new InvalidOperationException(FormatBodyError(credentials.Error, (HttpStatusCode)credentials.Code));

                var data = credentials?.Data;
                if (data != null
                    && (ApiHttpFactory.IsRealToken(data.AccessToken)
                        || ApiHttpFactory.IsRealToken(data.RefreshToken)
                        || data.User != null))
                {
                    if (ApiHttpFactory.IsRealToken(data.AccessToken))
                        session.AccessToken = data.AccessToken!.Trim();
                    if (ApiHttpFactory.IsRealToken(data.RefreshToken))
                        session.RefreshToken = data.RefreshToken!.Trim();
                    if (data.User != null)
                        session.User = data.User;

                    var accessExp = ParseTimestamp(data.ExpiresAt);
                    if (accessExp.HasValue)
                        session.AccessExpiresAt = accessExp;
                    var refreshExp = ParseTimestamp(data.RefreshExpiresAt);
                    if (refreshExp.HasValue)
                        session.RefreshExpiresAt = refreshExp;
                }
                else
                {
                    var legacy = JsonSerializer.Deserialize<ApiEnvelope<UserFullInfo>>(responseText, JsonOptions);
                    if (legacy?.Data != null)
                        session.User = legacy.Data;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // keep tokens already collected from cookies
            }

            if (!string.IsNullOrWhiteSpace(session.User?.Email))
                session.Email = session.User!.Email!;
        }

        private static void OverlayCookieTokens(
            AuthSession session,
            string? access,
            string? refresh,
            string? csrf)
        {
            // Cookies rotate tokens; body UserCredentials overwrites access/refresh afterwards.
            if (ApiHttpFactory.IsRealToken(access))
                session.AccessToken = access!.Trim();
            if (ApiHttpFactory.IsRealToken(refresh))
                session.RefreshToken = refresh!.Trim();

            var normalizedCsrf = ApiHttpFactory.NormalizeToken(csrf);
            if (!string.IsNullOrWhiteSpace(normalizedCsrf))
                session.CsrfToken = normalizedCsrf!;
            else
                session.CsrfToken = ApiHttpFactory.NormalizeToken(session.CsrfToken) ?? session.CsrfToken ?? string.Empty;
        }

        private static DateTimeOffset? ParseTimestamp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                    out var parsed))
                return parsed;
            return null;
        }

        private static bool IsAuthFailure(HttpStatusCode status)
        {
            var code = (int)status;
            return code == 401 || code == 403;
        }

        private static string? TryReadJsonString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        return null;

                    if (TryGetStringProp(doc.RootElement, propertyName, out var direct))
                        return direct;

                    if (doc.RootElement.TryGetProperty("data", out var data)
                        && data.ValueKind == JsonValueKind.Object
                        && TryGetStringProp(data, propertyName, out var nested))
                        return nested;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static bool TryGetStringProp(JsonElement obj, string name, out string? value)
        {
            value = null;
            if (!obj.TryGetProperty(name, out var el))
                return false;
            if (el.ValueKind != JsonValueKind.String)
                return false;
            value = el.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string FormatApiError(HttpStatusCode status, string body)
        {
            string? apiError = null;
            try
            {
                var envelope = JsonSerializer.Deserialize<ApiEnvelope<object>>(body, JsonOptions);
                apiError = envelope?.Error;
            }
            catch
            {
                // fall through
            }

            return FormatBodyError(apiError, status);
        }

        private static string FormatBodyError(string? apiError, HttpStatusCode status)
        {
            var err = apiError ?? string.Empty;
            if (err.IndexOf("no rows", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("GetUserByEmail", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Пользователь с таким email не найден.";

            if (err.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("парол", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("credentials", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Неверный email или пароль.";

            if (!string.IsNullOrWhiteSpace(err)
                && err.IndexOf("row.Scan", StringComparison.OrdinalIgnoreCase) < 0
                && err.IndexOf("s.store.", StringComparison.OrdinalIgnoreCase) < 0)
                return err;

            if ((int)status == 401)
                return "Неверный email или пароль.";
            if ((int)status == 403)
                return "Сессия недействительна или доступ запрещён.";
            if ((int)status == 404)
                return "Пользователь не найден или неверный email.";

            if (!string.IsNullOrWhiteSpace(err))
                return "Ошибка сервера: " + Truncate(err, 180);

            return "HTTP " + (int)status;
        }

        public static string DescribeNetworkError(Exception ex, string apiBaseUrl)
        {
            var msg = ex.GetBaseException().Message ?? ex.Message;
            if (string.IsNullOrWhiteSpace(apiBaseUrl)
                || apiBaseUrl.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Не задан рабочий ApiBaseUrl (сейчас «" + apiBaseUrl + "»). " +
                       "Укажите адрес сервера ConstrTodo на экране входа.";
            }

            if (msg.IndexOf("неизвестен", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("unknown host", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("No such host", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Name or service not known", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Не удаётся найти хост «" + apiBaseUrl + "». Проверьте ApiBaseUrl.";
            }

            if (msg.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("TLS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Ошибка TLS/сертификата при обращении к «" + apiBaseUrl + "»: " + msg;
            }

            return "Сеть: " + msg + " (ApiBaseUrl: " + apiBaseUrl + ")";
        }

        private static string Truncate(string text, int max)
        {
            text = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
