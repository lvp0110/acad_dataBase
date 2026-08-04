using System;
using System.IO;
using System.Text.Json;
using AcadDwgBrowser.Core.Configuration;

namespace AcadDwgBrowser.Core.Services
{
    public static class SettingsLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        public static PluginSettings Load(string pluginDirectory)
        {
            var path = ResolveConfigPath(pluginDirectory);
            if (!File.Exists(path))
            {
                var defaults = new PluginSettings();
                Save(path, defaults);
                return defaults;
            }

            var json = File.ReadAllText(path).TrimStart('\uFEFF');
            var settings = JsonSerializer.Deserialize<PluginSettings>(json, JsonOptions) ?? new PluginSettings();
            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                || settings.ApiBaseUrl.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                settings.ApiBaseUrl = new PluginSettings().ApiBaseUrl;
            }

            return settings;
        }

        /// <summary>
        /// Prefer Contents/config.json (bundle layout), then config.json next to the DLL.
        /// </summary>
        public static string ResolveConfigPath(string pluginDirectory)
        {
            var parentDir = Directory.GetParent(pluginDirectory)?.FullName ?? pluginDirectory;
            var bundleConfig = Path.Combine(parentDir, "config.json");
            if (File.Exists(bundleConfig))
                return bundleConfig;

            var local = Path.Combine(pluginDirectory, "config.json");
            if (File.Exists(local))
                return local;

            return bundleConfig;
        }

        public static void Save(string path, PluginSettings settings)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            // UTF-8 without BOM — BOM breaks some readers and confuses hand-edited configs.
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public static void SaveForPlugin(string pluginDirectory, PluginSettings settings)
        {
            Save(ResolveConfigPath(pluginDirectory), settings);
        }
    }
}
