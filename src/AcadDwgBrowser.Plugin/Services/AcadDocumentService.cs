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

            var docs = AcApp.DocumentManager;
            if (docs.IsApplicationContext)
            {
                docs.Open(path, readOnly);
                return;
            }

            // Queue open on the application thread. Do not Wait() — that can deadlock AutoCAD.
            docs.ExecuteInApplicationContext(
                _ => { docs.Open(path, readOnly); },
                null);

            // Give AutoCAD a short moment to activate the document when possible.
            Thread.Sleep(50);
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

                    var path = preferredPath;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        path = doc.Name;

                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        throw new InvalidOperationException(
                            "Активный чертёж ещё не сохранён на диск.");

                    path = Path.GetFullPath(path);
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

            if (docs.IsApplicationContext)
                Save();
            else
                docs.ExecuteInApplicationContext(_ => Save(), null);

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

            if (docs.IsApplicationContext)
                Save();
            else
                docs.ExecuteInApplicationContext(_ => Save(), null);

            if (error != null)
                throw error;

            return result ?? throw new InvalidOperationException("Не удалось сохранить чертёж.");
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
    }
}
