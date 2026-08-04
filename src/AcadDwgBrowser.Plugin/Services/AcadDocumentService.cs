using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadDwgBrowser.Plugin.Services
{
    /// <summary>
    /// Opens DWG on the AutoCAD application thread (safe from async/UI callbacks).
    /// </summary>
    public static class AcadDocumentService
    {
        public static void OpenDwg(string path, bool readOnly = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь к файлу пуст.", nameof(path));

            if (!System.IO.File.Exists(path))
                throw new System.IO.FileNotFoundException("DWG не найден.", path);

            var docs = AcApp.DocumentManager;
            if (docs.IsApplicationContext)
            {
                docs.Open(path, readOnly);
                return;
            }

            docs.ExecuteInApplicationContext(
                _ => { docs.Open(path, readOnly); },
                null);
        }

        public static void WriteMessage(string message)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n[DWG dB] " + message + "\n");
        }
    }
}
