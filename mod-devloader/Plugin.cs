using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.DevLoader;

// Development only: loads our plugins from BepInEx\devplugins instead of BepInEx\plugins and reloads one when its
// DLL changes (build.ps1 -Dev copies it there while the game runs). The old build is unloaded first (its Unload:
// patches, menu, callbacks and objects of a ModBase plugin), then the new one is loaded from the file's bytes, so
// the file stays free for the next build. Old builds stay in memory: .NET cannot unload assemblies Harmony patched
// from.
[BepInPlugin("nobirds.oldenera.devloader", "Olden Era Dev Loader", "0.1.0")]
public class Plugin : BasePlugin
{
    static ManualLogSource logger;
    static string folder;
    // Loaded builds by file: the file's write time and the plugin instance.
    static readonly Dictionary<string, (DateTime written, BasePlugin plugin)> loaded = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, DateTime> failed = new(StringComparer.OrdinalIgnoreCase);
    static float nextCheck;

    public override void Load()
    {
        logger = Log;
        folder = Path.Combine(Paths.BepInExRootPath, "devplugins");
        Directory.CreateDirectory(folder);
        // First check on the first frame, after all normal plugins are loaded.
        Ticker.Add(this, Check);
        logger.LogInfo($"Watching {folder}");
    }

    static void Check()
    {
        if (UnityEngine.Time.realtimeSinceStartup < nextCheck)
            return;
        nextCheck = UnityEngine.Time.realtimeSinceStartup + 1f;
        foreach (var path in Directory.GetFiles(folder, "*.dll"))
        {
            var written = File.GetLastWriteTimeUtc(path);
            // Wait until the copy is done: the file has not changed for half a second.
            if ((DateTime.UtcNow - written).TotalSeconds < 0.5)
                continue;
            if (loaded.TryGetValue(path, out var current) && current.written == written)
                continue;
            if (failed.TryGetValue(path, out var failedAt) && failedAt == written)
                continue;
            Reload(path, written);
        }
    }

    static void Reload(string path, DateTime written)
    {
        var name = Path.GetFileName(path);
        try
        {
            if (loaded.TryGetValue(path, out var old))
            {
                // A plugin that cannot unload itself (not a ModBase) keeps its patches and callbacks: the new build is
                // not loaded over it (its patches would be there twice), until the game is restarted.
                if (!old.plugin.Unload())
                {
                    logger.LogWarning($"DEV {name} cannot unload itself: the new build is not loaded; restart the game");
                    failed[path] = written;
                    return;
                }
                loaded.Remove(path);
                logger.LogMessage($"DEV unloaded {name}");
            }
            var assembly = Assembly.Load(File.ReadAllBytes(path));
            var type = SafeTypes(assembly).FirstOrDefault(t => typeof(BasePlugin).IsAssignableFrom(t) && !t.IsAbstract && t.GetCustomAttribute<BepInPlugin>() != null);
            if (type == null)
            {
                logger.LogWarning($"DEV {name}: no plugin class");
                failed[path] = written;
                return;
            }
            var metadata = type.GetCustomAttribute<BepInPlugin>();
            if (IL2CPPChainloader.Instance.Plugins.ContainsKey(metadata.GUID))
            {
                logger.LogWarning($"DEV {name}: {metadata.GUID} is also in BepInEx\\plugins; remove it from there (with the game closed)");
                failed[path] = written;
                return;
            }
            var plugin = (BasePlugin)Activator.CreateInstance(type);
            plugin.Load();
            loaded[path] = (written, plugin);
            failed.Remove(path);
            logger.LogMessage($"DEV loaded {metadata.Name} {metadata.Version} from {name}");
        }
        catch (Exception e)
        {
            failed[path] = written;
            logger.LogError($"DEV {name}: {e}");
        }
    }

    static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }
}
