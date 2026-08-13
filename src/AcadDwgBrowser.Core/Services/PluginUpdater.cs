using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcadDwgBrowser.Core.Configuration;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>
    /// Checks a remote/share manifest, stages an update, and schedules apply after AutoCAD exits.
    /// User only needs to restart AutoCAD.
    /// </summary>
    public static class PluginUpdater
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static int _started;

        public static void StartBackgroundCheck(PluginSettings settings, string pluginDirectory)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.UpdateManifestUrl))
                return;
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CheckAndStageAsync(settings, pluginDirectory, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    AuthDebugLog.Write("Auto-update: " + ex.Message);
                }
            });
        }

        public static async Task<string?> CheckAndStageAsync(
            PluginSettings settings,
            string pluginDirectory,
            CancellationToken cancellationToken)
        {
            var manifestUrl = (settings.UpdateManifestUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(manifestUrl))
                return null;

            var bundleRoot = FindBundleRoot(pluginDirectory);
            if (string.IsNullOrWhiteSpace(bundleRoot))
            {
                AuthDebugLog.Write("Auto-update: bundle root not found from " + pluginDirectory);
                return null;
            }

            var localVersion = ReadLocalVersion(bundleRoot) ?? "0.0.0";
            var manifest = await LoadManifestAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
                return null;

            if (!IsNewer(manifest.Version, localVersion))
            {
                AuthDebugLog.Write(
                    "Auto-update: up to date local=" + localVersion + " remote=" + manifest.Version);
                return null;
            }

            AuthDebugLog.Write(
                "Auto-update: staging " + manifest.Version + " (local " + localVersion + ")");

            var stagingRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadDwgBrowser",
                "update-staging");
            var stagingBundle = Path.Combine(stagingRoot, "AcadDwgBrowser.bundle");

            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
            Directory.CreateDirectory(stagingRoot);

            if (!string.IsNullOrWhiteSpace(manifest.BundlePath)
                && Directory.Exists(manifest.BundlePath)
                && File.Exists(Path.Combine(manifest.BundlePath!, "PackageContents.xml")))
            {
                CopyDirectory(manifest.BundlePath!, stagingBundle);
            }
            else if (!string.IsNullOrWhiteSpace(manifest.PackageUrl))
            {
                var zipPath = Path.Combine(stagingRoot, "update.zip");
                await DownloadToFileAsync(manifest.PackageUrl!, zipPath, cancellationToken)
                    .ConfigureAwait(false);
                ExtractBundleFromZip(zipPath, stagingRoot, stagingBundle);
            }
            else
            {
                AuthDebugLog.Write("Auto-update: manifest has no packageUrl/bundlePath");
                return null;
            }

            if (!File.Exists(Path.Combine(stagingBundle, "PackageContents.xml")))
            {
                AuthDebugLog.Write("Auto-update: staged package incomplete");
                return null;
            }

            // Preserve machine-specific API URL from current install.
            TryPreserveConfig(bundleRoot!, stagingBundle);

            ScheduleApplyAfterExit(stagingBundle, bundleRoot!);
            var msg = "Доступно обновление DWG dB "
                      + manifest.Version
                      + ". Оно установится после закрытия AutoCAD — просто перезапустите AutoCAD.";
            try
            {
                var flag = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AcadDwgBrowser",
                    "update-pending.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(flag)!);
                File.WriteAllText(flag, msg, new UTF8Encoding(false));
            }
            catch
            {
                // ignore
            }

            AuthDebugLog.Write("Auto-update: scheduled apply → " + bundleRoot);
            return msg;
        }

        private static void TryPreserveConfig(string currentBundle, string stagingBundle)
        {
            try
            {
                var currentCfg = Path.Combine(currentBundle, "Contents", "config.json");
                var stagingCfg = Path.Combine(stagingBundle, "Contents", "config.json");
                if (!File.Exists(currentCfg) || !File.Exists(stagingCfg))
                    return;

                var current = JsonSerializer.Deserialize<PluginSettings>(
                                  File.ReadAllText(currentCfg).TrimStart('\uFEFF'), JsonOptions)
                              ?? new PluginSettings();
                var staged = JsonSerializer.Deserialize<PluginSettings>(
                                 File.ReadAllText(stagingCfg).TrimStart('\uFEFF'), JsonOptions)
                             ?? new PluginSettings();

                // Keep local API URL and update feed URL.
                if (!string.IsNullOrWhiteSpace(current.ApiBaseUrl))
                    staged.ApiBaseUrl = current.ApiBaseUrl;
                if (!string.IsNullOrWhiteSpace(current.UpdateManifestUrl))
                    staged.UpdateManifestUrl = current.UpdateManifestUrl;
                if (!string.IsNullOrWhiteSpace(current.ApiKey))
                    staged.ApiKey = current.ApiKey;

                File.WriteAllText(
                    stagingCfg,
                    JsonSerializer.Serialize(staged, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                AuthDebugLog.Write("Auto-update: preserve config failed: " + ex.Message);
            }
        }

        private static void ScheduleApplyAfterExit(string stagingBundle, string targetBundle)
        {
            var pid = Process.GetCurrentProcess().Id;
            var script = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadDwgBrowser",
                "apply-update.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);

            var ps = new StringBuilder();
            ps.AppendLine("$ErrorActionPreference = 'Stop'");
            ps.AppendLine("$pidToWait = " + pid);
            ps.AppendLine("$src = " + PsQuote(stagingBundle));
            ps.AppendLine("$dst = " + PsQuote(targetBundle));
            ps.AppendLine("$log = " + PsQuote(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadDwgBrowser",
                "update-apply.log")));
            ps.AppendLine("function Log($m){ Add-Content -LiteralPath $log -Value ((Get-Date -Format o) + ' ' + $m) }");
            ps.AppendLine("Log 'Waiting for AutoCAD pid=' + $pidToWait");
            ps.AppendLine("try { Wait-Process -Id $pidToWait -ErrorAction SilentlyContinue } catch {}");
            ps.AppendLine("Start-Sleep -Seconds 2");
            ps.AppendLine("Log 'Applying update src=' + $src + ' dst=' + $dst");
            ps.AppendLine("if (-not (Test-Path -LiteralPath $src)) { Log 'staging missing'; exit 1 }");
            ps.AppendLine("$parent = Split-Path -Parent $dst");
            ps.AppendLine("New-Item -ItemType Directory -Force -Path $parent | Out-Null");
            ps.AppendLine("if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }");
            ps.AppendLine("Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force");
            ps.AppendLine("Log 'OK'");
            File.WriteAllText(script, ps.ToString(), new UTF8Encoding(true));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "
                            + PsQuote(script),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }

        private static string PsQuote(string path) =>
            "'" + (path ?? string.Empty).Replace("'", "''") + "'";

        private static async Task<UpdateManifest?> LoadManifestAsync(
            string manifestUrl,
            CancellationToken cancellationToken)
        {
            string json;
            if (IsHttp(manifestUrl))
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                using (var response = await http.GetAsync(manifestUrl, cancellationToken)
                           .ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            else
            {
                var path = manifestUrl;
                if (manifestUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    path = new Uri(manifestUrl).LocalPath;
                if (!File.Exists(path))
                    throw new FileNotFoundException("Update manifest not found: " + path);
                json = File.ReadAllText(path);
            }

            return JsonSerializer.Deserialize<UpdateManifest>(json.TrimStart('\uFEFF'), JsonOptions);
        }

        private static async Task DownloadToFileAsync(
            string urlOrPath,
            string destination,
            CancellationToken cancellationToken)
        {
            if (IsHttp(urlOrPath))
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                using (var response = await http.GetAsync(urlOrPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                           .ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = File.Create(destination))
                        await input.CopyToAsync(output).ConfigureAwait(false);
                }
                return;
            }

            var path = urlOrPath;
            if (urlOrPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                path = new Uri(urlOrPath).LocalPath;
            File.Copy(path, destination, overwrite: true);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static void ExtractBundleFromZip(string zipPath, string stagingRoot, string stagingBundle)
        {
            var extractDir = Path.Combine(stagingRoot, "extract");
            Directory.CreateDirectory(extractDir);

            // Use PowerShell Expand-Archive for netstandard2.0 without extra packages.
            var zipQ = zipPath.Replace("'", "''");
            var dirQ = extractDir.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath '"
                            + zipQ + "' -DestinationPath '" + dirQ + "' -Force\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                if (p == null)
                    throw new InvalidOperationException("Не удалось распаковать обновление.");
                p.WaitForExit(120000);
                if (p.ExitCode != 0)
                    throw new InvalidOperationException("Ошибка распаковки обновления, code=" + p.ExitCode);
            }

            var found = Directory.GetDirectories(extractDir, "AcadDwgBrowser.bundle", SearchOption.AllDirectories);
            if (found.Length == 0)
                throw new InvalidOperationException("В ZIP нет папки AcadDwgBrowser.bundle");
            CopyDirectory(found[0], stagingBundle);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var rel = dir.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, rel));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dest = Path.Combine(destination, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
        }

        public static string? FindBundleRoot(string pluginDirectory)
        {
            try
            {
                var dir = new DirectoryInfo(pluginDirectory);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "PackageContents.xml")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        public static string? ReadLocalVersion(string bundleRoot)
        {
            try
            {
                var xmlPath = Path.Combine(bundleRoot, "PackageContents.xml");
                if (!File.Exists(xmlPath))
                    return null;
                var doc = XDocument.Load(xmlPath);
                var ver = doc.Root?.Attribute("AppVersion")?.Value
                          ?? doc.Root?.Attribute("FriendlyVersion")?.Value;
                return string.IsNullOrWhiteSpace(ver) ? null : ver.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsNewer(string remote, string local)
        {
            try
            {
                var r = new Version(PadVersion(remote));
                var l = new Version(PadVersion(local));
                return r > l;
            }
            catch
            {
                return false;
            }
        }

        private static string PadVersion(string value)
        {
            var parts = (value ?? "0").Trim().Split('.');
            var list = new System.Collections.Generic.List<string>();
            foreach (var p in parts)
                list.Add(string.IsNullOrWhiteSpace(p) ? "0" : p.Trim());
            while (list.Count < 4)
                list.Add("0");
            while (list.Count > 4)
                list.RemoveAt(list.Count - 1);
            return string.Join(".", list);
        }

        private static bool IsHttp(string value) =>
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
