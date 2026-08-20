using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.Windows;

namespace AcadDwgBrowser.Plugin.Ui
{
    internal static class DwgBrowserPalette
    {
        public const string DisplayName = "DWG dB";

        // New Guid so AutoCAD does not restore the old cached "DWG Browser" title.
        private static readonly Guid PaletteGuid = new Guid("A1B2C3D4-E5F6-4789-A012-3456789ABCDE");

        private static PaletteSet? _palette;
        private static DwgBrowserControl? _control;

        public static void Show()
        {
            if (_palette == null)
            {
                _control = new DwgBrowserControl();
                _palette = new PaletteSet(DisplayName, PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowAutoHideButton
                            | PaletteSetStyles.ShowCloseButton
                            | PaletteSetStyles.ShowPropertiesMenu
                            | PaletteSetStyles.Snappable,
                    MinimumSize = new Size(320, 420),
                    DockEnabled = DockSides.Left | DockSides.Right | DockSides.None,
                    Size = new Size(400, 720)
                };
                _palette.Add(DisplayName, _control);
            }

            // AutoCAD may restore a saved title for a PaletteSet Guid — force current name.
            _palette.Name = DisplayName;
            _palette.Visible = true;
            _control?.RefreshOnShow();
        }

        public static void Close()
        {
            if (_palette != null)
            {
                _palette.Visible = false;
                _palette.Dispose();
                _palette = null;
            }

            if (_control != null)
            {
                _control.Dispose();
                _control = null;
            }
        }
    }
}
