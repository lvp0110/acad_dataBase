using System;
using System.Windows.Input;
using AcadDwgBrowser.Plugin;
using AcadDwgBrowser.Plugin.Ui;
using Autodesk.Windows;

namespace AcadDwgBrowser.Plugin.Ribbon
{
    internal static class RibbonBuilder
    {
        private const string TabId = "AcadDwgBrowser.Tab";
        private const string PanelId = "AcadDwgBrowser.Panel";
        private static bool _subscribed;

        /// <summary>Creates ribbon tab/button when AutoCAD ribbon is ready.</summary>
        public static void Schedule()
        {
            try
            {
                if (ComponentManager.Ribbon != null)
                {
                    EnsureRibbon();
                    return;
                }

                if (_subscribed)
                    return;

                _subscribed = true;
                ComponentManager.ItemInitialized += OnItemInitialized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DWG dB ribbon schedule failed: " + ex);
            }
        }

        private static void OnItemInitialized(object? sender, RibbonItemEventArgs e)
        {
            if (ComponentManager.Ribbon == null)
                return;

            ComponentManager.ItemInitialized -= OnItemInitialized;
            _subscribed = false;
            EnsureRibbon();
        }

        public static void EnsureRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
                return;

            var tab = ribbon.FindTab(TabId);
            if (tab == null)
            {
                tab = new RibbonTab
                {
                    Title = "DWG dB",
                    Id = TabId
                };
                ribbon.Tabs.Add(tab);
            }
            else
            {
                tab.Title = "DWG dB";
            }

            if (FindPanel(tab, PanelId) != null)
            {
                tab.IsActive = false;
                return;
            }

            var panelSource = new RibbonPanelSource
            {
                Title = "Каталог",
                Id = PanelId
            };

            var iconLarge = DbIconFactory.Large;
            var iconSmall = DbIconFactory.Small;

            var button = new RibbonButton
            {
                Text = "DWG dB",
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                LargeImage = iconLarge,
                Image = iconSmall,
                ToolTip = "Открыть каталог производственных чертежей",
                CommandHandler = new RelayCommand(_ => DwgBrowserPalette.Show())
            };

            panelSource.Items.Add(button);

            var panel = new RibbonPanel { Source = panelSource };
            tab.Panels.Add(panel);
        }

        private static RibbonPanel? FindPanel(RibbonTab tab, string id)
        {
            foreach (var panel in tab.Panels)
            {
                if (panel?.Source?.Id == id)
                    return panel;
            }

            return null;
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;

            public RelayCommand(Action<object?> execute) => _execute = execute;

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => _execute(parameter);

#pragma warning disable CS0067
            public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        }
    }
}
