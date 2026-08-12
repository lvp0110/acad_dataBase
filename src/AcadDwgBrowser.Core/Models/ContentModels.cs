using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>swagger.ContentListResponse — live API wraps items in data.content (+ filters).</summary>
    public sealed class ContentListResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public ContentListData? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        public IReadOnlyList<ContentInfo> Items =>
            Data?.Content ?? (IReadOnlyList<ContentInfo>)Array.Empty<ContentInfo>();
    }

    public sealed class ContentListData
    {
        [JsonPropertyName("content")]
        public List<ContentInfo> Content { get; set; } = new List<ContentInfo>();
    }

    /// <summary>models.ContentInfo</summary>
    public sealed class ContentInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Document code / display title used by production_drawings.</summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("group_id")]
        public string? GroupId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("updated_by")]
        public string? UpdatedBy { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("verified_at")]
        public string? VerifiedAt { get; set; }

        [JsonPropertyName("labels")]
        public List<ContentLabel>? Labels { get; set; }
    }

    public sealed class ContentLabel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    /// <summary>swagger.ContentFullInfo</summary>
    public sealed class ContentFullResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public ContentFullInfo? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>models.ContentFullResp — payload is document-specific</summary>
    public sealed class ContentFullInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("payload")]
        public System.Text.Json.JsonElement Payload { get; set; }

        [JsonPropertyName("approvals")]
        public List<ContentApprovalStep>? Approvals { get; set; }
    }

    /// <summary>swagger.ContentTypesResponse</summary>
    public sealed class ContentTypesResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<ContentTypeInfo> Data { get; set; } = new List<ContentTypeInfo>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public sealed class ContentTypeInfo
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name) ? Code : Name;
    }

    /// <summary>swagger.DocFieldsResponse</summary>
    public sealed class DocFieldsResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<DocTypeStruct> Data { get; set; } = new List<DocTypeStruct>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>models.DocTypeStruct</summary>
    public sealed class DocTypeStruct
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("fields")]
        public List<DocField> Fields { get; set; } = new List<DocField>();
    }

    /// <summary>models.Field</summary>
    public sealed class DocField
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("accept")]
        public string? Accept { get; set; }

        public bool IsDwgFileField =>
            string.Equals(Type, "file_dwg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Type, "file_dxf", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(Accept) && Accept.IndexOf("dwg", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
