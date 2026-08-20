using System;
using System.Collections.Generic;

namespace AcadDwgBrowser.Core.Configuration
{
    public sealed class PluginSettings
    {
        public string ApiBaseUrl { get; set; } = "https://dev3.constrtodo.ru:3005";

        /// <summary>Optional static Bearer token (bypass UI login). Prefer POST /login session.</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>POST — swagger /login</summary>
        public string LoginEndpoint { get; set; } = "/login";

        /// <summary>GET — swagger /auth/session</summary>
        public string SessionEndpoint { get; set; } = "/auth/session";

        /// <summary>POST — swagger /auth/logout</summary>
        public string LogoutEndpoint { get; set; } = "/auth/logout";

        /// <summary>
        /// Fixed content type: production drawings only (models.DocumentsType.production_drawings).
        /// </summary>
        public string ContentType { get; set; } = "production_drawings";

        /// <summary>Optional query category for /content/list/{type}?category=</summary>
        public string ContentCategory { get; set; } = string.Empty;

        /// <summary>Template with {type} — swagger /content/list/{type}</summary>
        public string ContentListPath { get; set; } = "/content/list/{type}";

        /// <summary>Template with {id} — swagger /content/{id}</summary>
        public string ContentDetailPath { get; set; } = "/content/{id}";

        /// <summary>Template with {code} — swagger /content/types/form/{code}</summary>
        public string ContentFormPath { get; set; } = "/content/types/form/{code}";

        /// <summary>Template with {fileID} — swagger /api/v2/files/{fileID}</summary>
        public string FileDownloadPath { get; set; } = "/api/v2/files/{fileID}";

        /// <summary>POST — swagger /content/{code}</summary>
        public string ContentCreatePath { get; set; } = "/content/{code}";

        /// <summary>Legacy alias kept for older configs.</summary>
        public string ListEndpoint { get; set; } = "/content/list/production_drawings";

        /// <summary>GET — swagger /content/types (optional).</summary>
        public string ContentTypesEndpoint { get; set; } = "/content/types";

        public string DownloadDirectory { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>Defaults for POST /content/production_drawings form fields.</summary>
        public string DefaultBrandCode { get; set; } = "bon";

        public string DefaultModelCode { get; set; } = "albero";

        public string DefaultGlobalCategoryCode { get; set; } = "acoustic";

        public string DefaultProdDrawingEdgeCode { get; set; } = "2p_2pk";

        public string DefaultProdDrawingPanelSizeCode { get; set; } = "1200_600_30";

        public string DefaultProdDrawingPerforationCode { get; set; } = "block_2";

        /// <summary>
        /// Auto-update feed: HTTP(S) or UNC/local path to manifest.json.
        /// Empty = updates disabled. Example:
        /// \\server\cad\AcadDwgBrowser\manifest.json
        /// </summary>
        public string UpdateManifestUrl { get; set; } = string.Empty;

        public string ResolveDownloadDirectory()
        {
            if (!string.IsNullOrWhiteSpace(DownloadDirectory))
                return DownloadDirectory;

            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadDwgBrowser",
                "Downloads");
        }

        public string ResolveContentType() =>
            string.IsNullOrWhiteSpace(ContentType) ? "production_drawings" : ContentType.Trim();

        public string BuildContentListUrl(
            IReadOnlyDictionary<string, string>? filters = null)
        {
            var type = ResolveContentType();
            var path = (ContentListPath ?? "/content/list/{type}").Replace("{type}", Uri.EscapeDataString(type));
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(ContentCategory))
                query.Add("category=" + Uri.EscapeDataString(ContentCategory.Trim()));

            // Same contract as web / swagger: filter field codes as query params
            // (multi-values comma-separated, e.g. status=approved,rejected).
            if (filters != null)
            {
                foreach (var pair in filters)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                        continue;
                    query.Add(
                        Uri.EscapeDataString(pair.Key.Trim())
                        + "="
                        + Uri.EscapeDataString(pair.Value.Trim()));
                }
            }

            if (query.Count > 0)
                path += (path.Contains("?") ? "&" : "?") + string.Join("&", query);

            return path.TrimStart('/');
        }

        public string BuildContentDetailUrl(string id) =>
            (ContentDetailPath ?? "/content/{id}")
                .Replace("{id}", Uri.EscapeDataString(id ?? string.Empty))
                .TrimStart('/');

        public string BuildContentFormUrl(string? code = null) =>
            (ContentFormPath ?? "/content/types/form/{code}")
                .Replace("{code}", Uri.EscapeDataString(code ?? ResolveContentType()))
                .TrimStart('/');

        public string BuildFileDownloadUrl(string fileId) =>
            (FileDownloadPath ?? "/api/v2/files/{fileID}")
                .Replace("{fileID}", Uri.EscapeDataString(fileId ?? string.Empty))
                .TrimStart('/');

        /// <summary>GET — swagger /content/filters/{code}</summary>
        public string ContentFiltersPath { get; set; } = "/content/filters/{code}";

        /// <summary>GET — swagger /content/references/{code} (full select catalogs).</summary>
        public string ContentReferencesPath { get; set; } = "/content/references/{code}";

        /// <summary>GET — swagger /api/v2/entities/{type}</summary>
        public string EntitiesPath { get; set; } = "/api/v2/entities/{type}";

        /// <summary>GET — swagger /api/v2/globalMenuCategories</summary>
        public string GlobalMenuCategoriesPath { get; set; } = "/api/v2/globalMenuCategories";

        /// <summary>GET — swagger /content/toApproval/preview/{id}</summary>
        public string ApprovalPreviewPath { get; set; } = "/content/toApproval/preview/{id}";

        /// <summary>POST — swagger /content/toApproval/{id}</summary>
        public string ApprovalStartPath { get; set; } = "/content/toApproval/{id}";

        /// <summary>GET — swagger /content/toApproval/active-preview/{id}</summary>
        public string ApprovalActivePreviewPath { get; set; } = "/content/toApproval/active-preview/{id}";

        /// <summary>PUT — swagger /content/toApproval/assignees/{id}</summary>
        public string ApprovalAssigneesPath { get; set; } = "/content/toApproval/assignees/{id}";

        /// <summary>PUT — swagger /content/withdraw/{id}</summary>
        public string ContentWithdrawPath { get; set; } = "/content/withdraw/{id}";

        public string BuildContentFiltersUrl(string? code = null) =>
            (ContentFiltersPath ?? "/content/filters/{code}")
                .Replace("{code}", Uri.EscapeDataString(code ?? ResolveContentType()))
                .TrimStart('/');

        /// <summary>
        /// Reference options — matches web contentReferences(source, filter, limit, offset, query).
        /// Default API page size is small; ComboBox needs a high limit when filter is empty.
        /// Optional <paramref name="query"/> is the cascade fragment from form field.query
        /// (e.g. brand_code=bon), appended like the web client does.
        /// </summary>
        public string BuildContentReferencesUrl(
            string referenceCode,
            int limit = 500,
            int offset = 0,
            string? filter = null,
            string? query = null)
        {
            var path = (ContentReferencesPath ?? "/content/references/{code}")
                .Replace("{code}", Uri.EscapeDataString(referenceCode ?? string.Empty))
                .TrimStart('/');

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(query))
                parts.Add(query.Trim().TrimStart('?'));

            parts.Add("filter=" + Uri.EscapeDataString(filter ?? string.Empty));
            parts.Add("limit=" + Math.Max(1, limit));
            parts.Add("offset=" + Math.Max(0, offset));
            return path + "?" + string.Join("&", parts);
        }

        public string BuildEntitiesUrl(string entityType) =>
            (EntitiesPath ?? "/api/v2/entities/{type}")
                .Replace("{type}", Uri.EscapeDataString(entityType ?? string.Empty))
                .TrimStart('/');

        public string BuildGlobalMenuCategoriesUrl() =>
            (GlobalMenuCategoriesPath ?? "/api/v2/globalMenuCategories").TrimStart('/');

        public string BuildApprovalPreviewUrl(string contentId) =>
            (ApprovalPreviewPath ?? "/content/toApproval/preview/{id}")
                .Replace("{id}", Uri.EscapeDataString(contentId ?? string.Empty))
                .TrimStart('/');

        public string BuildApprovalStartUrl(string contentId) =>
            (ApprovalStartPath ?? "/content/toApproval/{id}")
                .Replace("{id}", Uri.EscapeDataString(contentId ?? string.Empty))
                .TrimStart('/');

        public string BuildApprovalActivePreviewUrl(string contentId) =>
            (ApprovalActivePreviewPath ?? "/content/toApproval/active-preview/{id}")
                .Replace("{id}", Uri.EscapeDataString(contentId ?? string.Empty))
                .TrimStart('/');

        public string BuildApprovalAssigneesUrl(string contentId) =>
            (ApprovalAssigneesPath ?? "/content/toApproval/assignees/{id}")
                .Replace("{id}", Uri.EscapeDataString(contentId ?? string.Empty))
                .TrimStart('/');

        public string BuildContentWithdrawUrl(string contentId) =>
            (ContentWithdrawPath ?? "/content/withdraw/{id}")
                .Replace("{id}", Uri.EscapeDataString(contentId ?? string.Empty))
                .TrimStart('/');

        /// <summary>GET — swagger /content/users/all</summary>
        public string UsersAllPath { get; set; } = "/content/users/all";

        /// <summary>POST — web createPanelSizeApi</summary>
        public string PanelSizeCreatePath { get; set; } = "/content/production-drawings/sizes";

        /// <summary>POST — web createPerforationApi</summary>
        public string PerforationCreatePath { get; set; } = "/content/production-drawings/perforations";

        /// <summary>POST — web createEdgeApi</summary>
        public string EdgeCreatePath { get; set; } = "/content/production-drawings/edges";

        public string BuildUsersAllUrl() =>
            (UsersAllPath ?? "/content/users/all").TrimStart('/');

        public string BuildPanelSizeCreateUrl() =>
            (PanelSizeCreatePath ?? "/content/production-drawings/sizes").TrimStart('/');

        public string BuildPerforationCreateUrl() =>
            (PerforationCreatePath ?? "/content/production-drawings/perforations").TrimStart('/');

        public string BuildEdgeCreateUrl() =>
            (EdgeCreatePath ?? "/content/production-drawings/edges").TrimStart('/');

        public string BuildContentCreateUrl(string? code = null) =>
            (ContentCreatePath ?? "/content/{code}")
                .Replace("{code}", Uri.EscapeDataString(code ?? ResolveContentType()))
                .TrimStart('/');
    }
}
