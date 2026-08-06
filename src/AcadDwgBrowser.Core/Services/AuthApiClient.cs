using System;
using System.Collections.Generic;
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
    /// ConstrTodo auth per swagger: POST /login, GET /auth/session, POST /auth/logout.
    /// Login sets access_token and csrf_token cookies.
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
                            out var access, out var csrf, out var cookieDump);

                        AuthDebugLog.Write(
                            "Login HTTP " + (int)response.StatusCode +
                            " cookies=[" + cookieDump + "]" +
                            " accessLen=" + (access?.Length ?? 0) +
                            " csrfLen=" + (csrf?.Length ?? 0) +
                            " body=" + Truncate(responseText, 400));

                        if (!response.IsSuccessStatusCode)
                            throw new InvalidOperationException(FormatApiError(response.StatusCode, responseText));

                        var envelope = JsonSerializer.Deserialize<ApiEnvelope<UserFullInfo>>(responseText, JsonOptions);
                        if (envelope != null && envelope.Code >= 400)
                            throw new InvalidOperationException(FormatBodyError(envelope.Error, response.StatusCode));

                        var user = envelope?.Data;

                        if (string.IsNullOrWhiteSpace(access) && user == null)
                        {
                            throw new InvalidOperationException(
                                "Вход не подтверждён: нет cookie access_token и нет данных пользователя. " +
                                "Подробности: %LocalAppData%\\AcadDwgBrowser\\login-debug.log");
                        }

                        if (string.IsNullOrWhiteSpace(access))
                            access = "cookie-session";

                        var session = new AuthSession
                        {
                            AccessToken = access!,
                            CsrfToken = ApiHttpFactory.NormalizeToken(csrf) ?? string.Empty,
                            Email = email,
                            User = user
                        };
                        AuthSessionStore.Save(session);
                        AuthDebugLog.Write("Login OK user=" + (user?.Email ?? email) +
                                           " csrfLen=" + session.CsrfToken.Length);
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
                    throw new InvalidOperationException(FormatApiError(response.StatusCode, responseText));

                // Refresh tokens if server rotated cookies on /auth/session.
                var baseUri = http.BaseAddress ?? new Uri(ApiHttpFactory.TrimSlash(_settings.ApiBaseUrl) + "/");
                var requestUri = response.RequestMessage?.RequestUri ?? baseUri;
                ExtractTokens(response, new CookieContainer(), baseUri, requestUri, responseText,
                    out var access, out var csrf, out _);

                if (ApiHttpFactory.IsRealToken(access))
                    session.AccessToken = access!;

                var normalizedCsrf = ApiHttpFactory.NormalizeToken(csrf);
                if (!string.IsNullOrWhiteSpace(normalizedCsrf))
                    session.CsrfToken = normalizedCsrf!;
                else
                    session.CsrfToken = ApiHttpFactory.NormalizeToken(session.CsrfToken) ?? session.CsrfToken;

                var envelope = JsonSerializer.Deserialize<ApiEnvelope<UserFullInfo>>(responseText, JsonOptions);
                if (envelope?.Data != null)
                    session.User = envelope.Data;

                if (!string.IsNullOrWhiteSpace(session.User?.Email))
                    session.Email = session.User!.Email!;

                AuthSessionStore.Save(session);
                return session;
            }
        }

        /// <summary>
        /// Ensures CSRF is present and normalized before POST/PUT. Client-side only.
        /// </summary>
        public async Task<AuthSession> EnsureFreshCsrfAsync(
            AuthSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            session.CsrfToken = ApiHttpFactory.NormalizeToken(session.CsrfToken) ?? session.CsrfToken;

            try
            {
                return await GetSessionAsync(session, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AuthDebugLog.Write("EnsureFreshCsrf fallback: " + ex.Message);
                // Keep existing session if session probe fails — still use normalized CSRF.
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
            out string? csrfToken,
            out string cookieDump)
        {
            accessToken = null;
            csrfToken = null;
            var names = new List<string>();

            CollectFromContainer(cookies, baseUri, names, ref accessToken, ref csrfToken);
            CollectFromContainer(cookies, requestUri, names, ref accessToken, ref csrfToken);

            // Host without explicit path variants
            try
            {
                var root = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + "/");
                CollectFromContainer(cookies, root, names, ref accessToken, ref csrfToken);
            }
            catch
            {
                // ignore
            }

            foreach (var raw in GetSetCookieHeaders(response))
            {
                ParseCookiePair(raw, ApiHttpFactory.AccessTokenCookie, ref accessToken);
                ParseCookiePair(raw, ApiHttpFactory.CsrfTokenCookie, ref csrfToken);
                var name = raw.Split('=')[0].Trim();
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                    names.Add(name + "(hdr)");
            }

            TryExtractTokensFromBody(responseText, ref accessToken, ref csrfToken);
            cookieDump = names.Count == 0 ? "none" : string.Join(", ", names);
        }

        private static void CollectFromContainer(
            CookieContainer cookies,
            Uri uri,
            List<string> names,
            ref string? accessToken,
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

        private static void TryExtractTokensFromBody(string body, ref string? access, ref string? csrf)
        {
            if (string.IsNullOrWhiteSpace(access))
                access = TryReadJsonString(body, "access_token") ?? TryReadJsonString(body, "token");
            if (string.IsNullOrWhiteSpace(csrf))
                csrf = TryReadJsonString(body, "csrf_token");
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
