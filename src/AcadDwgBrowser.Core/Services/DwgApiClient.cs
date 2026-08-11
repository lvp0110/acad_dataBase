using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        private readonly AuthSession? _session;
        private readonly bool _ownsHttp;

        public DwgApiClient(PluginSettings settings, AuthSession? session = null, HttpClient? httpClient = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _session = session;

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
                if (!string.IsNullOrWhiteSpace(resolved.FieldCode))
                    file.DwgFieldCode = resolved.FieldCode;
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
            file.LocalPath = targetPath;
            return targetPath;
        }

        public async Task UpdateContentAsync(
            string contentId,
            string? newName = null,
            string? localDwgPath = null,
            string? dwgFieldCode = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                throw new ArgumentException("Id контента пуст.", nameof(contentId));

            var hasRename = !string.IsNullOrWhiteSpace(newName);
            var hasFile = !string.IsNullOrWhiteSpace(localDwgPath);
            if (!hasRename && !hasFile)
                throw new ArgumentException("Укажите новое имя и/или путь к DWG.");

            if (hasFile && !File.Exists(localDwgPath!))
                throw new FileNotFoundException("Локальный DWG не найден.", localDwgPath);

            var detail = await GetContentAsync(contentId, cancellationToken).ConfigureAwait(false);
            var fieldCode = await ResolveDwgFieldCodeAsync(detail, dwgFieldCode, cancellationToken)
                .ConfigureAwait(false);
            var payloadJson = BuildUpdatePayloadJson(detail, newName, stripFileFields: hasFile, fieldCode);

            var endpoint = _settings.BuildContentDetailUrl(contentId);
            progress?.Report(0.05);

            using (var form = new MultipartFormDataContent())
            {
                // Some servers expect payload as plain text JSON string (not application/json part).
                form.Add(new StringContent(payloadJson, Encoding.UTF8), "payload");

                if (hasFile)
                {
                    var fileName = Path.GetFileName(localDwgPath!);
                    if (string.IsNullOrWhiteSpace(fileName))
                        fileName = (newName ?? detail.Name ?? "drawing") + ".dwg";
                    if (!fileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                        fileName += ".dwg";

                    // Read with ReadWrite share — AutoCAD may still hold the file.
                    var bytes = await ReadFileSharedAsync(localDwgPath!, cancellationToken)
                        .ConfigureAwait(false);
                    progress?.Report(0.35);

                    var fileContent = new ByteArrayContent(bytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    var partName = string.IsNullOrWhiteSpace(fieldCode) ? "file" : fieldCode!;
                    form.Add(fileContent, partName, fileName);
                }

                progress?.Report(0.45);

                using (var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = form })
                using (var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    await EnsureOkAsync(response).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    EnsureApiSuccess(body);
                }
            }

            progress?.Report(1.0);
        }

        public async Task<IReadOnlyList<FilterEntity>> GetFiltersAsync(
            CancellationToken cancellationToken = default)
        {
            var endpoint = _settings.BuildContentFiltersUrl();
            using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<FiltersResponse>(json, JsonOptions)
                              ?? new FiltersResponse();
                if (payload.Code >= 400)
                    throw new InvalidOperationException(payload.Error ?? "Ошибка фильтров/меток.");
                return payload.Data ?? new List<FilterEntity>();
            }
        }

        public async Task<DwgFileInfo> CreateContentAsync(
            string name,
            string localDwgPath,
            ProductionDrawingLabels labels,
            string? dwgFieldCode = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя чертежа пусто.", nameof(name));
            if (string.IsNullOrWhiteSpace(localDwgPath) || !File.Exists(localDwgPath))
                throw new FileNotFoundException("Локальный DWG не найден.", localDwgPath);
            if (labels == null || !labels.IsComplete)
                throw new InvalidOperationException(
                    "Заполните все обязательные метки: " + (labels?.MissingFieldName() ?? "метки"));

            name = name.Trim();
            var fieldCode = string.IsNullOrWhiteSpace(dwgFieldCode) ? "file_dwg" : dwgFieldCode!.Trim();
            try
            {
                var codes = await GetDwgFieldCodesAsync(cancellationToken).ConfigureAwait(false);
                if (codes.Count > 0 && string.IsNullOrWhiteSpace(dwgFieldCode))
                    fieldCode = codes[0];
            }
            catch
            {
                // form schema may 404; file_dwg is the live field code
            }

            var endpoint = _settings.BuildContentCreateUrl();
            progress?.Report(0.05);

            var fileName = Path.GetFileName(localDwgPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = name + ".dwg";
            if (!fileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                fileName += ".dwg";

            var bytes = await ReadFileSharedAsync(localDwgPath, cancellationToken).ConfigureAwait(false);
            progress?.Report(0.35);

            string body;
            using (var form = new MultipartFormDataContent())
            {
                void AddField(string key, string value) =>
                    form.Add(new StringContent(value ?? string.Empty, Encoding.UTF8), key);

                AddField("code", name);
                AddField("user_uuid", labels.UserUuid);
                AddField("brand_code", labels.BrandCode);
                AddField("model_code", labels.ModelCode);
                AddField("global_category_code", labels.GlobalCategoryCode);
                AddField("prod_drawing_edge_code", labels.EdgeCode);
                AddField("prod_drawing_panel_size_code", labels.PanelSizeCode);
                AddField("prod_drawing_perforation_code", labels.PerforationCode);

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, fieldCode, fileName);

                progress?.Report(0.45);

                AuthDebugLog.Write("POST " + endpoint + " code=" + name + " field=" + fieldCode);

                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form })
                using (var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var status = (int)response.StatusCode;
                    body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AuthDebugLog.Write("Create content HTTP " + status + " body=" + Truncate(body));
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(ExtractErrorMessage(body, status));

                    EnsureApiSuccess(body);
                }
            }

            progress?.Report(0.85);

            var createdId = TryExtractCreatedId(body);
            if (string.IsNullOrWhiteSpace(createdId))
            {
                var list = await ListFilesAsync(cancellationToken).ConfigureAwait(false);
                var match = list.FirstOrDefault(f =>
                    string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                createdId = match?.Id;
            }

            progress?.Report(1.0);

            return new DwgFileInfo
            {
                Id = createdId ?? string.Empty,
                Name = name,
                LocalPath = localDwgPath,
                DwgFieldCode = fieldCode,
                ContentType = _settings.ResolveContentType(),
                Status = "draft",
                UpdatedAt = DateTimeOffset.Now
            };
        }

        private static HttpResponseMessage CreateFailedResponse(System.Net.HttpStatusCode status, string body)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
            };
            return response;
        }

        private static string ExtractErrorMessage(string body, int statusCode)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body ?? string.Empty))
                {
                    if (doc.RootElement.TryGetProperty("error", out var err)
                        && err.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(err.GetString()))
                        return err.GetString()!;
                }
            }
            catch
            {
                // ignore
            }

            if (statusCode == 401 || statusCode == 403)
                return "Требуется авторизация. Войдите снова.";

            return "HTTP " + statusCode + ": " + Truncate(body ?? string.Empty);
        }

        private static string? TryExtractCreatedId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("data", out var data))
                        return null;

                    if (data.ValueKind == JsonValueKind.String)
                    {
                        var s = data.GetString();
                        return IsUuid(s) ? s : null;
                    }

                    if (data.ValueKind == JsonValueKind.Object)
                    {
                        if (data.TryGetProperty("content_id", out var contentId)
                            && contentId.ValueKind == JsonValueKind.String)
                            return contentId.GetString();

                        if (data.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                        {
                            var s = id.GetString();
                            return IsUuid(s) ? s : s;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static bool IsUuid(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && value!.Length == 36
            && value.IndexOf('-') > 0;

        public void Dispose()
        {
            if (_ownsHttp)
                _http.Dispose();
        }

        private async Task<ContentFullInfo> GetContentAsync(
            string contentId,
            CancellationToken cancellationToken)
        {
            var detailPath = _settings.BuildContentDetailUrl(contentId);
            using (var response = await _http.GetAsync(detailPath, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var envelope = JsonSerializer.Deserialize<ContentFullResponse>(json, JsonOptions);
                if (envelope == null || envelope.Code >= 400 || envelope.Data == null)
                    throw new InvalidOperationException(envelope?.Error ?? "Не удалось получить /content/{id}.");
                return envelope.Data;
            }
        }

        private async Task<(string Url, string? FileName, string? FieldCode)> ResolveDownloadFromContentAsync(
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

            var detail = await GetContentAsync(contentId, cancellationToken).ConfigureAwait(false);
            var files = ContentPayloadFileExtractor.Extract(detail.Payload, dwgFieldCodes);
            if (files.Count == 0)
                throw new InvalidOperationException(
                    "В payload контента «" + (detail.Name ?? contentId) +
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
                name = detail.Name;
            if (!string.IsNullOrWhiteSpace(name) && pick.PreferDwg
                && !name!.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)
                && !Path.HasExtension(name))
                name += ".dwg";

            return (url, name, pick.FieldCode);
        }

        private async Task<string> ResolveDwgFieldCodeAsync(
            ContentFullInfo detail,
            string? preferred,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred!.Trim();

            try
            {
                var codes = await GetDwgFieldCodesAsync(cancellationToken).ConfigureAwait(false);
                if (codes.Count > 0)
                    return codes[0];
            }
            catch
            {
                // ignore
            }

            var extracted = ContentPayloadFileExtractor.Extract(detail.Payload);
            var pick = extracted.FirstOrDefault(f => f.PreferDwg) ?? extracted.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(pick?.FieldCode))
                return pick!.FieldCode!;

            return "file_dwg";
        }

        private static string BuildUpdatePayloadJson(
            ContentFullInfo detail,
            string? newName,
            bool stripFileFields,
            string? dwgFieldCode)
        {
            JsonObject root;
            if (detail.Payload.ValueKind == JsonValueKind.Object)
            {
                root = JsonNode.Parse(detail.Payload.GetRawText()) as JsonObject ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var name = !string.IsNullOrWhiteSpace(newName) ? newName!.Trim() : detail.Name;
            if (!string.IsNullOrWhiteSpace(name))
                root["name"] = name;

            if (stripFileFields)
            {
                var keysToClear = new List<string>();
                foreach (var prop in root)
                {
                    var key = prop.Key;
                    if (IsFileFieldKey(key, dwgFieldCode) || LooksLikeFileNode(prop.Value))
                        keysToClear.Add(key);
                }

                foreach (var key in keysToClear)
                    root.Remove(key);
            }

            return root.ToJsonString();
        }

        private static bool IsFileFieldKey(string key, string? dwgFieldCode)
        {
            if (!string.IsNullOrWhiteSpace(dwgFieldCode)
                && string.Equals(key, dwgFieldCode, StringComparison.OrdinalIgnoreCase))
                return true;

            var k = key.ToLowerInvariant();
            return k == "file" || k == "file_dwg" || k == "file_dxf" || k == "file_pdf"
                   || k == "attachments" || k.EndsWith("_dwg") || k.EndsWith("_dxf");
        }

        private static bool LooksLikeFileNode(JsonNode? node)
        {
            if (node is not JsonObject obj)
                return false;

            return obj.ContainsKey("download_url")
                   || obj.ContainsKey("downloadUrl")
                   || obj.ContainsKey("file_name")
                   || obj.ContainsKey("file_id")
                   || obj.ContainsKey("fileId");
        }

        private static async Task<byte[]> ReadFileSharedAsync(string path, CancellationToken cancellationToken)
        {
            using (var input = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, true))
            using (var ms = new MemoryStream())
            {
                await input.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        private static void EnsureApiSuccess(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;

            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.TryGetProperty("code", out var code)
                        && code.ValueKind == JsonValueKind.Number
                        && code.GetInt32() >= 400)
                    {
                        string? err = null;
                        if (doc.RootElement.TryGetProperty("error", out var e)
                            && e.ValueKind == JsonValueKind.String)
                            err = e.GetString();
                        throw new InvalidOperationException(err ?? "Ошибка обновления контента.");
                    }
                }
            }
            catch (JsonException)
            {
                // Non-JSON success body is fine.
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
