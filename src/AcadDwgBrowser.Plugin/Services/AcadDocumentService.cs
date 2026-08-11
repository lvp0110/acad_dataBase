using System;
using System.IO;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadDwgBrowser.Plugin.Services
{
    /// <summary>
    /// Opens / saves DWG on the AutoCAD application thread (safe from async/UI callbacks).
    /// </summary>
    public static class AcadDocumentService
    {
        public static void OpenDwg(string path, bool readOnly = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу пуст.", nameof(path));

            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new FileNotFoundException("DWG не найден.", path);

            if (!readOnly)
                ClearReadOnlyAttribute(path);

            var docs = AcApp.DocumentManager;
            Exception? error = null;

            void Open()
            {
                try
                {
                    docs.Open(path, readOnly);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }

            // Modeless palette usually runs in application context — open synchronously
            // so the active document is ready before Save / edit.
            if (docs.IsApplicationContext)
            {
                Open();
            }
            else
            {
                // Queue to application thread and wait briefly. Palette/UI callers are
                // typically already in app context; this path is for command context.
                using (var done = new ManualResetEventSlim(false))
                {
                    docs.ExecuteInApplicationContext(
                        _ =>
                        {
                            try
                            {
                                Open();
                            }
                            finally
                            {
                                done.Set();
                            }
                        },
                        null);

                    if (!done.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("Таймаут открытия чертежа в AutoCAD.");
                }
            }

            if (error != null)
                throw error;

            // Verify read/write mode when edit was requested.
            if (!readOnly)
            {
                var doc = docs.MdiActiveDocument;
                if (doc != null && doc.IsReadOnly)
                {
                    throw new InvalidOperationException(
                        "AutoCAD открыл чертёж только для чтения. Закройте файл в других окнах и повторите.");
                }
            }
        }

        public static string? TryGetActiveDocumentPath()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return null;

                var name = doc.Name;
                if (string.IsNullOrWhiteSpace(name))
                    return null;

                try
                {
                    if (File.Exists(name))
                        return Path.GetFullPath(name);
                }
                catch
                {
                    // Drawing1.dwg etc.
                }

                return name.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Saves the active drawing to disk. Returns the file path.</summary>
        public static string SaveActiveDocument(string? preferredPath = null)
        {
            var docs = AcApp.DocumentManager;
            string? result = null;
            Exception? error = null;

            void Save()
            {
                try
                {
                    var doc = docs.MdiActiveDocument;
                    if (doc == null)
                        throw new InvalidOperationException("Нет активного чертежа в AutoCAD.");

                    if (doc.IsReadOnly)
                        throw new InvalidOperationException(
                            "Чертёж открыт только для чтения — сохранение невозможно.");

                    var path = preferredPath;
                    if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                        path = doc.Name;

                    if (string.IsNullOrWhiteSpace(path))
                        throw new InvalidOperationException(
                            "Активный чертёж ещё не сохранён на диск.");

                    // Unsaved DrawingN.dwg has no real path yet.
                    if (!File.Exists(path) && !Path.IsPathRooted(path))
                        throw new InvalidOperationException(
                            "Активный чертёж ещё не сохранён на диск.");

                    path = Path.GetFullPath(path);
                    ClearReadOnlyAttribute(path);

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    using (doc.LockDocument())
                    {
                        doc.Database.SaveAs(path, true, DwgVersion.Current, doc.Database.SecurityParameters);
                    }

                    result = path;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }

            // Always save on the calling AutoCAD/UI thread with LockDocument.
            // Do NOT fire-and-forget ExecuteInApplicationContext — that returns
            // before Save finishes and breaks the Save button.
            Save();

            if (error != null)
                throw error;

            return result ?? throw new InvalidOperationException("Не удалось сохранить чертёж.");
        }

        /// <summary>
        /// Saves the active drawing to a target path (works for unsaved DrawingN.dwg).
        /// </summary>
        public static string SaveActiveDocumentAs(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Путь сохранения пуст.", nameof(targetPath));

            targetPath = Path.GetFullPath(targetPath);
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var docs = AcApp.DocumentManager;
            string? result = null;
            Exception? error = null;

            void Save()
            {
                try
                {
                    var doc = docs.MdiActiveDocument;
                    if (doc == null)
                        throw new InvalidOperationException("Нет активного чертежа в AutoCAD.");

                    if (doc.IsReadOnly)
                        throw new InvalidOperationException(
                            "Чертёж открыт только для чтения — сохранение невозможно.");

                    ClearReadOnlyAttribute(targetPath);

                    using (doc.LockDocument())
                    {
                        doc.Database.SaveAs(
                            targetPath, true, DwgVersion.Current, doc.Database.SecurityParameters);
                    }

                    result = targetPath;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }

            Save();

            if (error != null)
                throw error;

            return result ?? throw new InvalidOperationException("Не удалось сохранить чертёж.");
        }

        public static bool IsActiveDocumentReadOnly()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                return doc == null || doc.IsReadOnly;
            }
            catch
            {
                return true;
            }
        }

        public static string? TryGetActiveDocumentTitle()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return null;

                var name = doc.Name;
                if (string.IsNullOrWhiteSpace(name))
                    return null;

                return Path.GetFileNameWithoutExtension(name);
            }
            catch
            {
                return null;
            }
        }

        public static bool HasActiveDocument()
        {
            try
            {
                return AcApp.DocumentManager.MdiActiveDocument != null;
            }
            catch
            {
                return false;
            }
        }

        public static void WriteMessage(string message)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n[DWG dB] " + message + "\n");
        }

        public static void SubscribeDocumentActivated(DocumentCollectionEventHandler handler)
        {
            AcApp.DocumentManager.DocumentActivated -= handler;
            AcApp.DocumentManager.DocumentActivated += handler;
        }

        public static void UnsubscribeDocumentActivated(DocumentCollectionEventHandler handler)
        {
            AcApp.DocumentManager.DocumentActivated -= handler;
        }

        private static void ClearReadOnlyAttribute(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
            catch
            {
                // best-effort — Save/Open will report a clearer error if needed
            }
        }
    }
}
