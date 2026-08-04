using System;
using System.IO;
using System.Text;

namespace AcadDwgBrowser.Core.Services
{
    internal static class AuthDebugLog
    {
        public static string Path =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AcadDwgBrowser",
                "login-debug.log");

        public static void Write(string message)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine;
                File.AppendAllText(Path, line, Encoding.UTF8);
            }
            catch
            {
                // never break login because of logging
            }
        }
    }
}
