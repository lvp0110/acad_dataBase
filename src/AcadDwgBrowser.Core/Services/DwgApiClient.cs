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
                    .Where(f => !string.IsNullOrWhiteSpace(f.Id))
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
                if (resolved.Labels != null)
                    file.Labels = resolved.Labels;
            }

            // Local file / AutoCAD tab title = catalog display name.
            var display = !string.IsNullOrWhiteSpace(file.Name)
                ? file.Name
                : preferredName;
            var safeName = MakeSafeDwgFileName(display, file.Id);
            var targetPath = Path.Combine(destinationDirectory, safeName);
            var tempPath = targetPath + ".partial";

            using (var response = await _http.GetAsync(
                       ResolveUrl(downloadUrl),
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);

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

            try
            {
                var attrs = File.GetAttributes(targetPath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(targetPath, attrs & ~FileAttributes.ReadOnly);
            }
            catch
            {
                // ignore
            }

            progress?.Report(1.0);
            file.LocalPath = targetPath;
            return targetPath;
        }

        public async Task<DwgFileInfo> UpdateContentAsync(
            string contentId,
            string? newName = null,
            string? localDwgPath = null,
            string? dwgFieldCode = null,
            ProductionDrawingLabels? labels = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                throw new ArgumentException("Id контента пуст.", nameof(contentId));

            var hasRename = !string.IsNullOrWhiteSpace(newName);
            var hasFile = !string.IsNullOrWhiteSpace(localDwgPath);
            if (!hasRename && !hasFile && (labels == null || !labels.HasAnyValue))
                throw new ArgumentException("Укажите новое имя, метки и/или путь к DWG.");

            if (hasFile && !File.Exists(localDwgPath!))
                throw new FileNotFoundException("Локальный DWG не найден.", localDwgPath);

            var detail = await GetContentAsync(contentId, cancellationToken).ConfigureAwait(false);
            var resolvedLabels = ProductionDrawingLabels.TryFromPayload(detail.Payload)
                                 ?? new ProductionDrawingLabels();
            if (labels != null)
            {
                if (!string.IsNullOrWhiteSpace(labels.UserUuid)) resolvedLabels.UserUuid = labels.UserUuid;
                if (!string.IsNullOrWhiteSpace(labels.BrandCode)) resolvedLabels.BrandCode = labels.BrandCode;
                if (!string.IsNullOrWhiteSpace(labels.ModelCode)) resolvedLabels.ModelCode = labels.ModelCode;
                if (!string.IsNullOrWhiteSpace(labels.GlobalCategoryCode))
                    resolvedLabels.GlobalCategoryCode = labels.GlobalCategoryCode;
                if (!string.IsNullOrWhiteSpace(labels.EdgeCode)) resolvedLabels.EdgeCode = labels.EdgeCode;
                if (!string.IsNullOrWhiteSpace(labels.PanelSizeCode))
                    resolvedLabels.PanelSizeCode = labels.PanelSizeCode;
                if (!string.IsNullOrWhiteSpace(labels.PerforationCode))
                    resolvedLabels.PerforationCode = labels.PerforationCode;
            }

            // Explicit rename: always use the exact name entered by the user.
            // Otherwise keep a stable title (never temporary replace codes).
            var name = hasRename
                ? newName!.Trim()
                : ResolveStableContentName(detail, null, contentId);

            // --- DWG replace: create new + delete old (PUT+file is broken on server) ---
            if (hasFile)
            {
                if (resolvedLabels == null || !resolvedLabels.IsComplete)
                    throw new InvalidOperationException(
                        "Для сохранения DWG нужны все метки: "
                        + (resolvedLabels?.MissingFieldName() ?? "метки"));

                // Temporary unique code must NOT become the visible title.
                var tempCode = BuildUniqueTempCode();
                AuthDebugLog.Write(
                    "Replace content via create+delete oldId=" + contentId
                    + " name=" + name + " tempCode=" + tempCode);

                var created = await CreateContentAsync(
                        tempCode,
                        localDwgPath!,
                        resolvedLabels,
                        dwgFieldCode,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(created.Id))
                {
                    created.Id = await ResolveNewestContentIdByNameAsync(tempCode, cancellationToken)
                        .ConfigureAwait(false) ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(created.Id))
                {
                    throw new InvalidOperationException(
                        "Новая версия чертежа создана, но сервер не вернул Id. "
                        + "Старый черновик не удалён — обновите список и проверьте дубликаты.");
                }

                try
                {
                    await DeleteContentAsync(contentId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AuthDebugLog.Write("Delete old content after replace failed: " + ex.Message);
                }

                // Always restore the original display name (PUT without file).
                try
                {
                    await PutContentMetaAsync(
                            created.Id,
                            name,
                            resolvedLabels,
                            created.DwgFieldCode ?? dwgFieldCode,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AuthDebugLog.Write("Rename after replace failed: " + ex.Message);
                }

                created.Name = name;
                created.LocalPath = localDwgPath;
                created.Status = "draft";
                created.Labels = resolvedLabels.Clone();
                progress?.Report(1.0);
                return created;
            }

            // --- Labels / rename only: PUT without file (works) ---
            var fieldCode = await ResolveDwgFieldCodeAsync(detail, dwgFieldCode, cancellationToken)
                .ConfigureAwait(false);
            await PutContentMetaAsync(
                    contentId,
                    name,
                    labels ?? resolvedLabels,
                    fieldCode,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            return new DwgFileInfo
            {
                Id = contentId,
                Name = name,
                Status = detail.Status ?? "draft",
                ContentType = detail.ContentType,
                DwgFieldCode = fieldCode,
                Labels = (labels ?? resolvedLabels)?.Clone()
            };
        }

        private async Task PutContentMetaAsync(
            string contentId,
            string name,
            ProductionDrawingLabels? labels,
            string? dwgFieldCode,
            CancellationToken cancellationToken,
            IProgress<double>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя чертежа пусто.", nameof(name));

            name = name.Trim();
            var detail = await GetContentAsync(contentId, cancellationToken).ConfigureAwait(false);
            var fieldCode = await ResolveDwgFieldCodeAsync(detail, dwgFieldCode, cancellationToken)
                .ConfigureAwait(false);
            var payloadJson = BuildUpdatePayloadJson(
                detail, name, stripFileFields: false, fieldCode, labels);

            var endpoint = _settings.BuildContentDetailUrl(contentId);
            progress?.Report(0.2);

            using (var form = new MultipartFormDataContent())
            {
                void AddField(string key, string value) =>
                    form.Add(new StringContent(value ?? string.Empty, Encoding.UTF8), key);

                // Live create API expects flat fields; send the same on rename/update.
                AddField("code", name);
                AddField("name", name);

                var resolved = labels;
                if (resolved == null || !resolved.HasAnyValue)
                    resolved = ProductionDrawingLabels.TryFromPayload(detail.Payload);

                if (resolved != null && resolved.HasAnyValue)
                {
                    if (!string.IsNullOrWhiteSpace(resolved.UserUuid))
                        AddField("user_uuid", resolved.UserUuid);
                    if (!string.IsNullOrWhiteSpace(resolved.BrandCode))
                        AddField("brand_code", resolved.BrandCode);
                    if (!string.IsNullOrWhiteSpace(resolved.ModelCode))
                        AddField("model_code", resolved.ModelCode);
                    if (!string.IsNullOrWhiteSpace(resolved.GlobalCategoryCode))
                        AddField("global_category_code", resolved.GlobalCategoryCode);
                    if (!string.IsNullOrWhiteSpace(resolved.EdgeCode))
                        AddField("prod_drawing_edge_code", resolved.EdgeCode);
                    if (!string.IsNullOrWhiteSpace(resolved.PanelSizeCode))
                        AddField("prod_drawing_panel_size_code", resolved.PanelSizeCode);
                    if (!string.IsNullOrWhiteSpace(resolved.PerforationCode))
                        AddField("prod_drawing_perforation_code", resolved.PerforationCode);
                }

                form.Add(new StringContent(payloadJson, Encoding.UTF8), "payload");
                AuthDebugLog.Write(
                    "PUT " + endpoint + " rename code=" + name + " payload=" + Truncate(payloadJson));

                using (var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = form })
                using (var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var status = (int)response.StatusCode;
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AuthDebugLog.Write("Update content HTTP " + status + " body=" + Truncate(body));
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(ExtractErrorMessage(body, status));
                    EnsureApiSuccess(body);
                }
            }

            progress?.Report(1.0);
        }

        private static string BuildUniqueTempCode() =>
            "tmp-" + Guid.NewGuid().ToString("N");

        /// <summary>
        /// Picks a stable user-facing title. Temporary replace codes (tmp-… / "name #abc123")
        /// must never become the displayed catalog name.
        /// </summary>
        private static string ResolveStableContentName(
            ContentFullInfo detail,
            string? preferredName,
            string contentId)
        {
            if (IsStableDisplayName(preferredName))
                return preferredName!.Trim();

            string? payloadCode = null;
            if (detail.Payload.ValueKind == JsonValueKind.Object
                && detail.Payload.TryGetProperty("code", out var codeEl)
                && codeEl.ValueKind == JsonValueKind.String)
            {
                payloadCode = codeEl.GetString();
            }

            foreach (var candidate in new[] { detail.Name, payloadCode })
            {
                var cleaned = SanitizeDisplayName(candidate);
                if (IsStableDisplayName(cleaned))
                    return cleaned!;
            }

            return !string.IsNullOrWhiteSpace(contentId) ? contentId : "drawing";
        }

        private static string? SanitizeDisplayName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var s = value.Trim();

            // Strip stacked temporary suffixes from older builds: "name #a1b2c3"
            while (true)
            {
                var idx = s.LastIndexOf(" #", StringComparison.Ordinal);
                if (idx <= 0 || idx + 2 >= s.Length)
                    break;

                var suffix = s.Substring(idx + 2);
                if (suffix.Length == 6 && IsHex(suffix))
                {
                    s = s.Substring(0, idx).TrimEnd();
                    continue;
                }

                break;
            }

            if (s.StartsWith("tmp-", StringComparison.OrdinalIgnoreCase)
                && s.Length == 4 + 32
                && IsHex(s.Substring(4)))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static bool IsStableDisplayName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (LooksLikeUuid(value!))
                return false;
            if (value!.StartsWith("tmp-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (value.StartsWith("Без имени", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        private static bool IsHex(string value)
        {
            foreach (var ch in value)
            {
                var isHex = (ch >= '0' && ch <= '9')
                            || (ch >= 'a' && ch <= 'f')
                            || (ch >= 'A' && ch <= 'F');
                if (!isHex)
                    return false;
            }

            return value.Length > 0;
        }

        public async Task DeleteContentAsync(
            string contentId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                throw new ArgumentException("Id контента пуст.", nameof(contentId));

            var endpoint = _settings.BuildContentDetailUrl(contentId);
            AuthDebugLog.Write("DELETE " + endpoint);

            using (var request = new HttpRequestMessage(HttpMethod.Delete, endpoint))
            using (var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var status = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AuthDebugLog.Write("Delete content HTTP " + status + " body=" + Truncate(body));
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(ExtractErrorMessage(body, status));
                EnsureApiSuccess(body);
            }
        }

        public async Task<IReadOnlyList<FilterEntity>> GetFiltersAsync(
            CancellationToken cancellationToken = default)
        {
            var endpoint = _settings.BuildContentFiltersUrl();
            List<FilterEntity> filters;
            using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<FiltersResponse>(json, JsonOptions)
                              ?? new FiltersResponse();
                if (payload.Code >= 400)
                    throw new InvalidOperationException(payload.Error ?? "Ошибка фильтров/меток.");
                filters = payload.Data ?? new List<FilterEntity>();
            }

            // /content/filters returns only values already used in content.
            // Full catalogs live in /api/v2/entities and global menu categories.
            await EnrichFilterOptionsAsync(filters, "brand_code", "brand", cancellationToken)
                .ConfigureAwait(false);
            await EnrichFilterOptionsAsync(filters, "model_code", "model", cancellationToken)
                .ConfigureAwait(false);
            await EnrichGlobalCategoryOptionsAsync(filters, cancellationToken).ConfigureAwait(false);

            AuthDebugLog.Write(
                "Filters enriched: " +
                string.Join(", ", filters.Select(f => f.Code + "=" + (f.Options?.Count ?? 0))));

            return filters;
        }

        public async Task<ProductionDrawingLabels?> GetContentLabelsAsync(
            string contentId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                return null;

            var detail = await GetContentAsync(contentId, cancellationToken).ConfigureAwait(false);
            return ProductionDrawingLabels.TryFromPayload(detail.Payload);
        }

        private async Task EnrichFilterOptionsAsync(
            List<FilterEntity> filters,
            string filterCode,
            string entityType,
            CancellationToken cancellationToken)
        {
            var entity = filters.FirstOrDefault(f =>
                string.Equals(f.Code, filterCode, StringComparison.OrdinalIgnoreCase));
            if (entity == null)
                return;

            try
            {
                var options = await GetEntityOptionsAsync(entityType, cancellationToken)
                    .ConfigureAwait(false);
                if (options.Count > 0)
                    entity.Options = MergeOptions(entity.Options, options);
            }
            catch (Exception ex)
            {
                AuthDebugLog.Write("Enrich " + filterCode + " from entities/" + entityType + ": " + ex.Message);
            }
        }

        private async Task EnrichGlobalCategoryOptionsAsync(
            List<FilterEntity> filters,
            CancellationToken cancellationToken)
        {
            var entity = filters.FirstOrDefault(f =>
                string.Equals(f.Code, "global_cat_code", StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Code, "global_category_code", StringComparison.OrdinalIgnoreCase));
            if (entity == null)
                return;

            try
            {
                var endpoint = _settings.BuildGlobalMenuCategoriesUrl();
                using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return;

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var payload = JsonSerializer.Deserialize<GlobalMenuCategoriesResponse>(json, JsonOptions)
                                  ?? new GlobalMenuCategoriesResponse();
                    if (payload.Code >= 400 || payload.Data == null || payload.Data.Count == 0)
                        return;

                    var options = payload.Data
                        .Where(c => !string.IsNullOrWhiteSpace(c.Type))
                        .Select(c => new FilterOption
                        {
                            Code = c.Type.Trim(),
                            Name = string.IsNullOrWhiteSpace(c.Name) ? c.Type.Trim() : c.Name.Trim()
                        })
                        .ToList();
                    entity.Options = MergeOptions(entity.Options, options);
                }
            }
            catch (Exception ex)
            {
                AuthDebugLog.Write("Enrich global_cat_code: " + ex.Message);
            }
        }

        private async Task<List<FilterOption>> GetEntityOptionsAsync(
            string entityType,
            CancellationToken cancellationToken)
        {
            var endpoint = _settings.BuildEntitiesUrl(entityType);
            using (var response = await _http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                await EnsureOkAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var payload = JsonSerializer.Deserialize<EntitiesListResponse>(json, JsonOptions)
                              ?? new EntitiesListResponse();
                if (payload.Code >= 400)
                    throw new InvalidOperationException(payload.Error ?? "Ошибка справочника " + entityType);

                return (payload.Data ?? new List<EntityItem>())
                    .Where(e => !string.IsNullOrWhiteSpace(e.Code))
                    .Select(e => new FilterOption
                    {
                        Code = e.Code.Trim(),
                        Name = string.IsNullOrWhiteSpace(e.Name) ? e.Code.Trim() : e.Name.Trim()
                    })
                    .ToList();
            }
        }

        private static List<FilterOption> MergeOptions(
            List<FilterOption>? existing,
            List<FilterOption> extra)
        {
            var map = new Dictionary<string, FilterOption>(StringComparer.OrdinalIgnoreCase);
            foreach (var opt in existing ?? new List<FilterOption>())
            {
                if (opt == null || string.IsNullOrWhiteSpace(opt.Code))
                    continue;
                map[opt.Code.Trim()] = opt;
            }

            foreach (var opt in extra)
            {
                if (opt == null || string.IsNullOrWhiteSpace(opt.Code))
                    continue;
                var code = opt.Code.Trim();
                if (!map.TryGetValue(code, out var prev)
                    || string.IsNullOrWhiteSpace(prev.Name)
                    || string.Equals(prev.Name, prev.Code, StringComparison.Ordinal))
                {
                    map[code] = opt;
                }
            }

            return map.Values
                .OrderBy(o => o.Name ?? o.Code, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
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
                UpdatedAt = DateTimeOffset.Now,
                Labels = labels.Clone()
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

        private async Task<string?> ResolveNewestContentIdByNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var list = await ListFilesAsync(cancellationToken).ConfigureAwait(false);
            var match = list
                .Where(f =>
                    !string.IsNullOrWhiteSpace(f.Id)
                    && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.UpdatedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();
            return match?.Id;
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

        private async Task<(string Url, string? FileName, string? FieldCode, ProductionDrawingLabels? Labels)> ResolveDownloadFromContentAsync(
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
            var labels = ProductionDrawingLabels.TryFromPayload(detail.Payload);
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

            return (url, name, pick.FieldCode, labels);
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
            string? dwgFieldCode,
            ProductionDrawingLabels? labels = null)
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

            // production_drawings uses "code" as the document name field.
            // On rename, write exactly the name provided by the caller.
            if (!string.IsNullOrWhiteSpace(newName))
            {
                var exact = newName!.Trim();
                root["code"] = exact;
                root["name"] = exact;
            }

            if (labels != null && labels.HasAnyValue)
            {
                if (!string.IsNullOrWhiteSpace(labels.UserUuid))
                    root["user_uuid"] = labels.UserUuid;
                if (!string.IsNullOrWhiteSpace(labels.BrandCode))
                    root["brand_code"] = labels.BrandCode;
                if (!string.IsNullOrWhiteSpace(labels.ModelCode))
                    root["model_code"] = labels.ModelCode;
                if (!string.IsNullOrWhiteSpace(labels.GlobalCategoryCode))
                    root["global_category_code"] = labels.GlobalCategoryCode;
                if (!string.IsNullOrWhiteSpace(labels.EdgeCode))
                    root["prod_drawing_edge_code"] = labels.EdgeCode;
                if (!string.IsNullOrWhiteSpace(labels.PanelSizeCode))
                    root["prod_drawing_panel_size_code"] = labels.PanelSizeCode;
                if (!string.IsNullOrWhiteSpace(labels.PerforationCode))
                    root["prod_drawing_perforation_code"] = labels.PerforationCode;
            }

            if (stripFileFields)
            {
                // Server deletes previous files by ID when a new multipart file arrives.
                // Live payload often has download_url only (no file_id) → DeleteFilesByIDs(NULL).
                // Replace file nodes with explicit {file_id} entries extracted from URLs.
                var field = string.IsNullOrWhiteSpace(dwgFieldCode) ? "file_dwg" : dwgFieldCode!.Trim();
                var ids = ExtractFileIdsFromPayload(detail.Payload, field);

                var keysToClear = new List<string>();
                foreach (var prop in root)
                {
                    var key = prop.Key;
                    if (IsFileFieldKey(key, field) || LooksLikeFileNode(prop.Value))
                        keysToClear.Add(key);
                }

                foreach (var key in keysToClear)
                    root.Remove(key);

                if (ids.Count > 0)
                {
                    var arr = new JsonArray();
                    foreach (var id in ids)
                        arr.Add(new JsonObject { ["file_id"] = id });
                    root[field] = arr;
                }
            }

            return root.ToJsonString();
        }

        private static List<string> ExtractFileIdsFromPayload(JsonElement payload, string fieldCode)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string? id)
            {
                if (string.IsNullOrWhiteSpace(id))
                    return;
                id = id.Trim();
                if (seen.Add(id))
                    ids.Add(id);
            }

            void FromObject(JsonElement obj)
            {
                if (obj.TryGetProperty("file_id", out var fid) && fid.ValueKind == JsonValueKind.String)
                    Add(fid.GetString());
                else if (obj.TryGetProperty("fileId", out var fid2) && fid2.ValueKind == JsonValueKind.String)
                    Add(fid2.GetString());
                else if (obj.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    var s = id.GetString();
                    if (s != null && s.Length == 36 && s.IndexOf('-') > 0)
                        Add(s);
                }

                if (obj.TryGetProperty("download_url", out var url) && url.ValueKind == JsonValueKind.String)
                    Add(TryExtractUuid(url.GetString()));
                else if (obj.TryGetProperty("downloadUrl", out var url2) && url2.ValueKind == JsonValueKind.String)
                    Add(TryExtractUuid(url2.GetString()));
            }

            if (payload.ValueKind != JsonValueKind.Object)
                return ids;

            JsonElement fieldEl = default;
            var hasField = payload.TryGetProperty(fieldCode, out fieldEl)
                           || payload.TryGetProperty("file_dwg", out fieldEl);

            if (!hasField)
            {
                // Fallback: any file-like nodes via extractor.
                foreach (var f in ContentPayloadFileExtractor.Extract(payload))
                {
                    if (!string.IsNullOrWhiteSpace(f.FileId))
                        Add(f.FileId);
                    else
                        Add(TryExtractUuid(f.DownloadUrl));
                }

                return ids;
            }

            if (fieldEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in fieldEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                        FromObject(item);
                    else if (item.ValueKind == JsonValueKind.String)
                        Add(item.GetString());
                }
            }
            else if (fieldEl.ValueKind == JsonValueKind.Object)
            {
                FromObject(fieldEl);
            }
            else if (fieldEl.ValueKind == JsonValueKind.String)
            {
                Add(fieldEl.GetString());
            }

            return ids;
        }

        private static string? TryExtractUuid(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var m = System.Text.RegularExpressions.Regex.Match(
                text,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value : null;
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
                Name = ResolveDisplayName(c),
                Status = string.IsNullOrWhiteSpace(c.Status) ? "draft" : c.Status,
                ContentType = c.ContentType,
                GroupId = c.GroupId,
                UpdatedAt = updated,
                Project = project,
                DownloadUrl = string.Empty
            };
        }

        private static string ResolveDisplayName(ContentInfo c)
        {
            // production_drawings title is payload/list "code"; list "name" is often stale.
            var fromCode = SanitizeDisplayName(c.Code);
            var fromName = SanitizeDisplayName(c.Name);

            if (IsStableDisplayName(fromCode))
                return fromCode!;
            if (IsStableDisplayName(fromName))
                return fromName!;

            if (!string.IsNullOrWhiteSpace(c.Id))
            {
                var shortId = c.Id.Length > 8 ? c.Id.Substring(0, 8) : c.Id;
                return "Без имени (" + shortId + ")";
            }

            return "Без имени";
        }

        private static bool LooksLikeUuid(string value) =>
            value.Length == 36 && value.IndexOf('-') == 8;

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

        /// <summary>
        /// Catalog display name → local .dwg file name (also used as AutoCAD tab title).
        /// </summary>
        private static string MakeSafeDwgFileName(string? displayName, string? fallbackId)
        {
            var safe = MakeSafeFileName(displayName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = !string.IsNullOrWhiteSpace(fallbackId)
                    ? fallbackId!.Replace("-", string.Empty)
                    : Guid.NewGuid().ToString("N");
            }

            if (!safe.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                safe += ".dwg";
            return safe;
        }
    }
}
