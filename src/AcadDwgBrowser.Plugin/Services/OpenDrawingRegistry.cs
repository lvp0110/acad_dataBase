using System;
using System.Collections.Concurrent;
using System.IO;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Plugin.Services
{
    /// <summary>
    /// Maps local DWG paths opened from the catalog to their ConstrTodo content records.
    /// </summary>
    internal static class OpenDrawingRegistry
    {
        private static readonly ConcurrentDictionary<string, DwgFileInfo> ByPath =
            new ConcurrentDictionary<string, DwgFileInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Last drawing opened from the catalog (fallback when AutoCAD path differs).</summary>
        public static DwgFileInfo? LastOpened { get; private set; }

        /// <summary>Suggested name for a new (not yet catalog-linked) AutoCAD drawing.</summary>
        public static string? PendingNewName { get; set; }

        public static void Register(string localPath, DwgFileInfo file)
        {
            if (string.IsNullOrWhiteSpace(localPath) || file == null)
                return;

            var copy = Clone(file);
            copy.LocalPath = TryNormalize(localPath) ?? localPath.Trim();
            ByPath[copy.LocalPath] = copy;

            var fileName = Path.GetFileName(copy.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
                ByPath["name:" + fileName] = copy;

            LastOpened = copy;
        }

        public static void Update(DwgFileInfo file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.LocalPath))
                return;

            Register(file.LocalPath!, file);
        }

        public static bool TryGet(string localPath, out DwgFileInfo file)
        {
            file = null!;
            if (string.IsNullOrWhiteSpace(localPath))
                return false;

            var key = TryNormalize(localPath) ?? localPath.Trim();
            if (ByPath.TryGetValue(key, out file!))
                return true;

            var fileName = Path.GetFileName(key);
            if (!string.IsNullOrWhiteSpace(fileName)
                && ByPath.TryGetValue("name:" + fileName, out file!))
                return true;

            return false;
        }

        public static bool TryGetActive(out DwgFileInfo file, out string localPath)
        {
            file = null!;
            localPath = AcadDocumentService.TryGetActiveDocumentPath() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(localPath) && TryGet(localPath, out file))
            {
                localPath = file.LocalPath ?? localPath;
                return true;
            }

            // Fallback: last catalog open whose file name matches the active document.
            var last = LastOpened;
            if (last?.LocalPath == null)
                return false;

            if (string.IsNullOrWhiteSpace(localPath))
            {
                // No reliable active path — still allow actions on the last catalog drawing
                // if that file exists on disk (typical right after Open from palette).
                if (File.Exists(last.LocalPath))
                {
                    file = last;
                    localPath = last.LocalPath;
                    return true;
                }

                return false;
            }

            var activeName = Path.GetFileName(localPath);
            var lastName = Path.GetFileName(last.LocalPath);
            if (!string.IsNullOrWhiteSpace(activeName)
                && string.Equals(activeName, lastName, StringComparison.OrdinalIgnoreCase))
            {
                var activePath = localPath;
                file = last;
                localPath = last.LocalPath;
                ByPath[TryNormalize(activePath) ?? activePath] = last;
                return true;
            }

            // Active drawing is unrelated — do not use last opened.
            return false;
        }

        /// <summary>
        /// Drawing targeted by Rename/Save: active catalog match, else last opened from catalog.
        /// </summary>
        public static bool TryGetCurrent(out DwgFileInfo file, out string localPath)
        {
            if (TryGetActive(out file, out localPath))
                return true;

            var last = LastOpened;
            if (last?.LocalPath == null || !File.Exists(last.LocalPath))
            {
                file = null!;
                localPath = string.Empty;
                return false;
            }

            file = last;
            localPath = last.LocalPath;
            return true;
        }

        private static string? TryNormalize(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private static DwgFileInfo Clone(DwgFileInfo src) =>
            new DwgFileInfo
            {
                Id = src.Id,
                Name = src.Name,
                DownloadUrl = src.DownloadUrl,
                SizeBytes = src.SizeBytes,
                UpdatedAt = src.UpdatedAt,
                Project = src.Project,
                Status = src.Status,
                ContentType = src.ContentType,
                GroupId = src.GroupId,
                LocalPath = src.LocalPath,
                DwgFieldCode = src.DwgFieldCode,
                Labels = src.Labels?.Clone()
            };
    }
}
