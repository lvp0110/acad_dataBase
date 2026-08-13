using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>
    /// Published next to the distribution ZIP / share folder.
    /// Example:
    /// {
    ///   "version": "1.0.1",
    ///   "packageUrl": "\\\\server\\cad\\AcadDwgBrowser\\AcadDwgBrowser-1.0.1.zip"
    /// }
    /// </summary>
    public sealed class UpdateManifest
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>HTTP(S) or UNC/local path to AcadDwgBrowser-*.zip</summary>
        [JsonPropertyName("packageUrl")]
        public string? PackageUrl { get; set; }

        /// <summary>Optional UNC/local path to a ready AcadDwgBrowser.bundle folder.</summary>
        [JsonPropertyName("bundlePath")]
        public string? BundlePath { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
