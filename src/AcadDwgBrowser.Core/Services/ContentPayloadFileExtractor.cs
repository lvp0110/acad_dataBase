using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>
    /// Extracts file refs from content payload.
    /// Live API uses objects like { download_url, file_name, field_code } under file_dwg / file_pdf.
    /// </summary>
    public static class ContentPayloadFileExtractor
    {
        private static readonly Regex UuidRegex = new Regex(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        public sealed class ExtractedFile
        {
            public string FileId { get; set; } = string.Empty;
            public string? DownloadUrl { get; set; }
            public string? FileName { get; set; }
            public bool PreferDwg { get; set; }
            public string? FieldCode { get; set; }
        }

        public static IReadOnlyList<ExtractedFile> Extract(
            JsonElement payload,
            IReadOnlyCollection<string>? preferredDwgFieldCodes = null)
        {
            var preferred = preferredDwgFieldCodes != null
                ? new HashSet<string>(preferredDwgFieldCodes, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var found = new List<ExtractedFile>();
            Walk(payload, parentKey: null, preferred, found);

            var result = new List<ExtractedFile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddUnique(ExtractedFile item)
            {
                var key = !string.IsNullOrWhiteSpace(item.DownloadUrl)
                    ? item.DownloadUrl!
                    : item.FileId;
                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                    return;
                result.Add(item);
            }

            // 1) Preferred form field codes (file_dwg from schema)
            foreach (var item in found)
            {
                if (item.FieldCode != null && preferred.Contains(item.FieldCode))
                    AddUnique(item);
            }

            // 2) Heuristic DWG
            foreach (var item in found)
            {
                if (item.PreferDwg)
                    AddUnique(item);
            }

            // 3) Anything else file-like
            foreach (var item in found)
                AddUnique(item);

            return result;
        }

        private static void Walk(
            JsonElement el,
            string? parentKey,
            HashSet<string> preferred,
            List<ExtractedFile> found)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    string? id = null;
                    string? name = null;
                    string? downloadUrl = null;
                    string? fieldCode = parentKey;

                    foreach (var prop in el.EnumerateObject())
                    {
                        if (prop.NameEquals("id") || prop.NameEquals("file_id") || prop.NameEquals("fileId")
                            || prop.NameEquals("uuid") || prop.NameEquals("fileID"))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                var s = prop.Value.GetString();
                                if (IsUuid(s))
                                    id = s;
                            }
                        }

                        if (prop.NameEquals("name") || prop.NameEquals("filename") || prop.NameEquals("file_name")
                            || prop.NameEquals("original_name") || prop.NameEquals("originalName"))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                name = prop.Value.GetString();
                        }

                        if (prop.NameEquals("download_url") || prop.NameEquals("downloadUrl")
                            || prop.NameEquals("url"))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                downloadUrl = prop.Value.GetString();
                        }

                        if (prop.NameEquals("field_code") || prop.NameEquals("fieldCode"))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                var fc = prop.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(fc))
                                    fieldCode = fc;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(id) && !string.IsNullOrWhiteSpace(downloadUrl))
                        id = TryExtractUuid(downloadUrl);

                    if (!string.IsNullOrEmpty(id) || !string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        found.Add(new ExtractedFile
                        {
                            FileId = id ?? string.Empty,
                            DownloadUrl = downloadUrl,
                            FileName = name,
                            FieldCode = fieldCode,
                            PreferDwg = LooksLikeDwg(fieldCode, name)
                                        || (fieldCode != null && preferred.Contains(fieldCode))
                        });
                    }

                    foreach (var prop in el.EnumerateObject())
                        Walk(prop.Value, prop.Name, preferred, found);
                    break;

                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray())
                        Walk(item, parentKey, preferred, found);
                    break;

                case JsonValueKind.String:
                    var value = el.GetString();
                    if (IsUuid(value) && (LooksLikeFileKey(parentKey) || (parentKey != null && preferred.Contains(parentKey))))
                    {
                        found.Add(new ExtractedFile
                        {
                            FileId = value!,
                            FieldCode = parentKey,
                            PreferDwg = LooksLikeDwg(parentKey, null) || (parentKey != null && preferred.Contains(parentKey))
                        });
                    }
                    break;
            }
        }

        private static bool IsUuid(string? value) =>
            !string.IsNullOrWhiteSpace(value) && UuidRegex.IsMatch(value!) && value!.Length == 36;

        private static string? TryExtractUuid(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = UuidRegex.Match(text!);
            return m.Success ? m.Value : null;
        }

        private static bool LooksLikeFileKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var k = key!.ToLowerInvariant();
            return k.Contains("file") || k.Contains("dwg") || k.Contains("dxf")
                   || k.Contains("attachment") || k.Contains("drawing") || k.Contains("document");
        }

        private static bool LooksLikeDwg(string? key, string? fileName)
        {
            if (!string.IsNullOrEmpty(fileName)
                && fileName!.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(key))
            {
                var k = key!.ToLowerInvariant();
                if (k.Contains("dwg") || k == "file_dwg")
                    return true;
            }

            return false;
        }
    }
}
