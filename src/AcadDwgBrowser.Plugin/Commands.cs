using AcadDwgBrowser.Plugin.Ui;
using Autodesk.AutoCAD.Runtime;

namespace AcadDwgBrowser.Plugin
{
    public sealed class Commands
    {
        public const string OpenBrowserCommand = "DWGDB";

        /// <summary>Previous command name — kept as alias.</summary>
        public const string OpenBrowserCommandAlias = "DWGBROWSER";

        [CommandMethod(OpenBrowserCommand, CommandFlags.Session)]
        public void OpenBrowser()
        {
            DwgBrowserPalette.Show();
        }

        [CommandMethod(OpenBrowserCommandAlias, CommandFlags.Session)]
        public void OpenBrowserAlias()
        {
            DwgBrowserPalette.Show();
        }
    }
}
