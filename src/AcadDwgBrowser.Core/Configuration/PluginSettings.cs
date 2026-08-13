using System;

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

        public string BuildContentListUrl()
        {
            var type = ResolveContentType();
            var path = (ContentListPath ?? "/content/list/{type}").Replace("{type}", Uri.EscapeDataString(type));
            if (!string.IsNullOrWhiteSpace(ContentCategory))
            {
                var sep = path.Contains("?") ? "&" : "?";
                path += sep + "category=" + Uri.EscapeDataString(ContentCategory.Trim());
            }

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

        public string BuildContentFiltersUrl(string? code = null) =>
            (ContentFiltersPath ?? "/content/filters/{code}")
                .Replace("{code}", Uri.EscapeDataString(code ?? ResolveContentType()))
                .TrimStart('/');

        /// <summary>
        /// Full reference list. Default API limit is 20 — always request a high limit.
        /// </summary>
        public string BuildContentReferencesUrl(string referenceCode, int limit = 500) =>
            (ContentReferencesPath ?? "/content/references/{code}")
                .Replace("{code}", Uri.EscapeDataString(referenceCode ?? string.Empty))
                .TrimStart('/')
            + "?limit=" + Math.Max(1, limit) + "&offset=0";

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

        /// <summary>GET — swagger /content/users/all</summary>
        public string UsersAllPath { get; set; } = "/content/users/all";

        public string BuildUsersAllUrl() =>
            (UsersAllPath ?? "/content/users/all").TrimStart('/');

        public string BuildContentCreateUrl(string? code = null) =>
            (ContentCreatePath ?? "/content/{code}")
                .Replace("{code}", Uri.EscapeDataString(code ?? ResolveContentType()))
                .TrimStart('/');
    }
}
