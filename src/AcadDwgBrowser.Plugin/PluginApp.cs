using System;
using System.IO;
using System.Reflection;
using AcadDwgBrowser.Core.Configuration;
using AcadDwgBrowser.Core.Services;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(AcadDwgBrowser.Plugin.PluginApp))]
[assembly: CommandClass(typeof(AcadDwgBrowser.Plugin.Commands))]

namespace AcadDwgBrowser.Plugin
{
    public sealed class PluginApp : IExtensionApplication
    {
        internal static PluginSettings Settings { get; set; } = new PluginSettings();

        internal static AuthSession? Session { get; set; }

        internal static string PluginDirectory { get; private set; } =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;

        static PluginApp()
        {
            // AutoCAD does not reliably probe the plugin folder for NuGet dependencies
            // (System.Text.Json and friends). Resolve them from the DLL directory.
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public void Initialize()
        {
            try
            {
                PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                  ?? AppDomain.CurrentDomain.BaseDirectory;
                Settings = SettingsLoader.Load(PluginDirectory);
                Session = AuthSessionStore.Load();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AcadDwgBrowser init failed: " + ex);
            }

            // Ribbon is often not ready during Initialize — create tab/button on first Idle.
            try
            {
                Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnApplicationIdle;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AcadDwgBrowser ribbon idle hook failed: " + ex);
            }
        }

        private static void OnApplicationIdle(object? sender, System.EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnApplicationIdle;
            try
            {
                Ribbon.RibbonBuilder.Schedule();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AcadDwgBrowser ribbon failed: " + ex);
            }
        }

        public void Terminate()
        {
            Ui.DwgBrowserPalette.Close();
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                var requested = new AssemblyName(args.Name);
                var simpleName = requested.Name;
                if (string.IsNullOrEmpty(simpleName))
                    return null;

                var dir = PluginDirectory;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                          ?? AppDomain.CurrentDomain.BaseDirectory;
                }

                var candidate = Path.Combine(dir, simpleName + ".dll");
                if (!File.Exists(candidate))
                    return null;

                return Assembly.LoadFrom(candidate);
            }
            catch
            {
                return null;
            }
        }
    }
}
