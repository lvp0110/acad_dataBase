using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AcadDwgBrowser.Core.Configuration;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    public sealed class DwgApiClient : IDwgApiClient, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _http;
        private readonly PluginSettings _settings;
        private readonly bool _ownsHttp;

        public DwgApiClient(PluginSettings settings, AuthSession? session = null, HttpClient? httpClient = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (httpClient != null)
            {
                _http = httpClient;
                _ownsHttp = false;
            }
            else
            {
                _http = ApiHttpFactory.Create(settings, session);
                _ownsHttp = true;
            }
        }

        public async Task<IReadOnlyList<DwgFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            // Always production drawings (or configured ContentType, default production_drawings).
            _settings.ContentType = _settings.ResolveContentType();

            var endpoint = _settings.BuildContentListUrl();
            using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<ContentListResponse>(json, JsonOptions)
                              ?? new ContentListResponse();

                if (payload.Code >= 400)
                    throw new InvalidOperationException(payload.Error ?? "Ошибка списка контента.");

                return payload.Items
                    .Select(MapContent)
                    .ToList();
            }
        }

        public async Task<IReadOnlyList<string>> GetDwgFieldCodesAsync(CancellationToken cancellationToken = default)
        {
            var endpoint = _settings.BuildContentFormUrl();
            using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var envelope = JsonSerializer.Deserialize<DocFieldsResponse>(json, JsonOptions)
                               ?? new DocFieldsResponse();

                if (envelope.Code >= 400)
                    throw new InvalidOperationException(envelope.Error ?? "Ошибка схемы формы.");

                var codes = new List<string>();
                foreach (var doc in envelope.Data ?? new List<DocTypeStruct>())
                {
                    foreach (var field in doc.Fields ?? new List<DocField>())
                    {
                        if (field.IsDwgFileField && !string.IsNullOrWhiteSpace(field.Code))
                            codes.Add(field.Code);
                    }
                }

                return codes;
            }
        }

        public async Task<IReadOnlyList<ContentTypeInfo>> ListContentTypesAsync(
            CancellationToken cancellationToken = default)
        {
            var endpoint = (_settings.ContentTypesEndpoint ?? "/content/types").TrimStart('/');
            using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<ContentTypesResponse>(json, JsonOptions)
                              ?? new ContentTypesResponse();
                return payload.Data ?? new List<ContentTypeInfo>();
            }
        }

        public async Task<string> DownloadFileAsync(
            DwgFileInfo file,
            string destinationDirectory,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (string.IsNullOrWhiteSpace(file.Id) && string.IsNullOrWhiteSpace(file.DownloadUrl))
                throw new InvalidOperationException("У элемента нет Id контента.");

            Directory.CreateDirectory(destinationDirectory);

            string downloadUrl;
            string preferredName;

            if (!string.IsNullOrWhiteSpace(file.DownloadUrl)
                && file.DownloadUrl.IndexOf("/api/v2/files/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                downloadUrl = file.DownloadUrl;
                preferredName = file.Name;
            }
            else
            {
                var resolved = await ResolveDownloadFromContentAsync(file.Id, cancellationToken)
                    .ConfigureAwait(false);
                downloadUrl = resolved.Url;
                preferredName = resolved.FileName ?? file.Name;
            }

            var safeName = MakeSafeFileName(string.IsNullOrWhiteSpace(preferredName) ? file.Id + ".dwg" : preferredName);
            if (!Path.HasExtension(safeName))
                safeName += ".dwg";

            var targetPath = Path.Combine(destinationDirectory, safeName);
            var tempPath = targetPath + ".partial";

            using (var response = await _http.GetAsync(
                       ResolveUrl(downloadUrl),
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);

                // Prefer server filename from Content-Disposition when present.
                var serverName = TryGetFileNameFromDisposition(response.Content.Headers.ContentDisposition);
                if (!string.IsNullOrWhiteSpace(serverName))
                {
                    safeName = MakeSafeFileName(serverName!);
                    targetPath = Path.Combine(destinationDirectory, safeName);
                    tempPath = targetPath + ".partial";
                }

                var total = response.Content.Headers.ContentLength;
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long readTotal = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        readTotal += read;
                        if (total.HasValue && total.Value > 0 && progress != null)
                            progress.Report((double)readTotal / total.Value);
                    }
                }
            }

            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(tempPath, targetPath);

            progress?.Report(1.0);
            return targetPath;
        }

        public void Dispose()
        {
            if (_ownsHttp)
                _http.Dispose();
        }

        private async Task<(string Url, string? FileName)> ResolveDownloadFromContentAsync(
            string contentId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> dwgFieldCodes = Array.Empty<string>();
            try
            {
                dwgFieldCodes = await GetDwgFieldCodesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Form schema is optional; fall back to heuristic extraction.
            }

            var detailPath = _settings.BuildContentDetailUrl(contentId);
            using (var response = await _http.GetAsync(detailPath, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var envelope = JsonSerializer.Deserialize<ContentFullResponse>(json, JsonOptions);
                if (envelope == null || envelope.Code >= 400 || envelope.Data == null)
                    throw new InvalidOperationException(envelope?.Error ?? "Не удалось получить /content/{id}.");

                var files = ContentPayloadFileExtractor.Extract(envelope.Data.Payload, dwgFieldCodes);
                if (files.Count == 0)
                    throw new InvalidOperationException(
                        "В payload контента «" + (envelope.Data.Name ?? contentId) +
                        "» не найден файл (ожидается file_dwg с download_url).");

                // Prefer real DWG when available; otherwise first file from file_dwg field.
                var pick = files.FirstOrDefault(f =>
                               !string.IsNullOrEmpty(f.FileName)
                               && f.FileName!.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                           ?? files.FirstOrDefault(f => f.PreferDwg)
                           ?? files[0];

                string url;
                if (!string.IsNullOrWhiteSpace(pick.DownloadUrl))
                    url = pick.DownloadUrl!;
                else if (!string.IsNullOrWhiteSpace(pick.FileId))
                    url = _settings.BuildFileDownloadUrl(pick.FileId);
                else
                    throw new InvalidOperationException("У файла нет download_url и file id.");

                var name = pick.FileName;
                if (string.IsNullOrWhiteSpace(name))
                    name = envelope.Data.Name;
                if (!string.IsNullOrWhiteSpace(name) && pick.PreferDwg
                    && !name!.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)
                    && !Path.HasExtension(name))
                    name += ".dwg";

                return (url, name);
            }
        }

        private static DwgFileInfo MapContent(ContentInfo c)
        {
            DateTimeOffset? updated = null;
            if (!string.IsNullOrWhiteSpace(c.UpdatedAt)
                && DateTimeOffset.TryParse(c.UpdatedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var dt))
                updated = dt;

            var project = c.Labels != null && c.Labels.Count > 0
                ? string.Join(", ", c.Labels.Where(l => !string.IsNullOrWhiteSpace(l.Name)).Select(l => l.Name))
                : c.ContentType;

            return new DwgFileInfo
            {
                Id = c.Id ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(c.Name) ? (c.Id ?? "content") : c.Name,
                Status = c.Status,
                ContentType = c.ContentType,
                GroupId = c.GroupId,
                UpdatedAt = updated,
                Project = project,
                DownloadUrl = string.Empty
            };
        }

        private Uri ResolveUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
                return absolute;

            return new Uri(
                _http.BaseAddress ?? new Uri(ApiHttpFactory.TrimSlash(_settings.ApiBaseUrl) + "/"),
                url.TrimStart('/'));
        }

        private static string? TryGetFileNameFromDisposition(ContentDispositionHeaderValue? disposition)
        {
            if (disposition == null) return null;
            if (!string.IsNullOrWhiteSpace(disposition.FileNameStar))
                return disposition.FileNameStar.Trim('"');
            if (!string.IsNullOrWhiteSpace(disposition.FileName))
                return disposition.FileName.Trim('"');
            return null;
        }

        private static async Task EnsureOkAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                throw new InvalidOperationException("Требуется авторизация. Войдите снова.");

            string? apiError = null;
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.TryGetProperty("error", out var err)
                        && err.ValueKind == JsonValueKind.String)
                        apiError = err.GetString();
                }
            }
            catch
            {
                // ignore
            }

            if (!string.IsNullOrWhiteSpace(apiError))
                throw new InvalidOperationException(apiError);

            throw new HttpRequestException("HTTP " + (int)response.StatusCode + ": " + Truncate(body));
        }

        private static string Truncate(string text)
        {
            text = (text ?? string.Empty).Trim();
            return text.Length <= 200 ? text : text.Substring(0, 200) + "…";
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
