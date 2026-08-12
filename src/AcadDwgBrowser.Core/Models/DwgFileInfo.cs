using System;

namespace AcadDwgBrowser.Core.Models
{
    public sealed class DwgFileInfo
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Direct download URL if known; otherwise resolved via /content/{id} → /api/v2/files/{fileID}.</summary>
        public string DownloadUrl { get; set; } = string.Empty;

        public long? SizeBytes { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? Project { get; set; }

        public string? Status { get; set; }

        /// <summary>Rejection reason from approvals (when status is rejected).</summary>
        public string? RejectionComment { get; set; }

        public string? ContentType { get; set; }

        public string? GroupId { get; set; }

        /// <summary>Local path after download / last open from catalog.</summary>
        public string? LocalPath { get; set; }

        /// <summary>Form field code for the DWG (e.g. file_dwg), used when uploading.</summary>
        public string? DwgFieldCode { get; set; }

        /// <summary>Label field codes from content payload (brand, model, …).</summary>
        public ProductionDrawingLabels? Labels { get; set; }

        public override string ToString() => Name;
    }

    public sealed class ContentMetaInfo
    {
        public ProductionDrawingLabels? Labels { get; set; }

        public string? RejectionComment { get; set; }
    }
}
