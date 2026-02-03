using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using XOutputRedux.Core.Plugins;

namespace XOutputRedux.App;

/// <summary>
/// Discovers and loads plugins from the plugins directory.
/// Each plugin lives in its own subdirectory under plugins/.
/// </summary>
public static class PluginLoader
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddDllDirectory(string newDirectory);

    public static List<IXOutputPlugin> LoadPlugins(string pluginsDirectory)
    {
        var plugins = new List<IXOutputPlugin>();

        if (!Directory.Exists(pluginsDirectory))
            return plugins;

        foreach (var dir in Directory.GetDirectories(pluginsDirectory))
        {
            try
            {
                LoadPluginsFromDirectory(dir, plugins);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load plugin from {dir}", ex);
            }
        }

        return plugins;
    }

    private static void LoadPluginsFromDirectory(string dir, List<IXOutputPlugin> plugins)
    {
        var pluginDlls = Directory.GetFiles(dir, "*.Plugin.dll");
        if (pluginDlls.Length == 0)
            return;

        // Add directory to native DLL search path so SDK DLLs can be found
        AddDllDirectory(Path.GetFullPath(dir));

        var context = new AssemblyLoadContext(
            Path.GetFileName(dir), isCollectible: false);

        // Resolve managed dependencies from the plugin directory
        context.Resolving += (ctx, name) =>
        {
            var candidate = Path.Combine(dir, name.Name + ".dll");
            return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
        };

        foreach (var dll in pluginDlls)
        {
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));

                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(IXOutputPlugin).IsAssignableFrom(type) || type.IsAbstract)
                        continue;

                    var plugin = (IXOutputPlugin)Activator.CreateInstance(type)!;
                    if (plugin.Initialize())
                    {
                        plugins.Add(plugin);
                        AppLogger.Info($"Loaded plugin: {plugin.DisplayName} ({plugin.Id})");
                    }
                    else
                    {
                        AppLogger.Warning($"Plugin failed to initialize: {type.FullName}");
                        plugin.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load plugin assembly {dll}", ex);
            }
        }
    }
}
