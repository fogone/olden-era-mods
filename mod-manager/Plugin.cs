using BepInEx;
using BepInEx.Unity.IL2CPP;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.Manager;

// The mod manager (issue #10): it stays in BepInEx\plugins with the library, and loads the mods it installed from
// BepInEx\oldenera-mods (ManagerState, ModLoader) on the first frame, after BepInEx has loaded its own plugins.
// Its package (one release) holds the manager, the library, the Mods tab (mod-settings, where the catalog is shown) and
// the preloader patcher that updates them (Updater); the mods come from the catalog (ManagerService, CatalogRows).
[BepInPlugin("nobirds.oldenera.manager", "Olden Era Mod Manager", "0.4.1")]
public class Plugin : BasePlugin
{
    bool started;

    public override void Load()
    {
        ModCatalog.ManagerVersion = MetadataHelper.GetMetadata(this).Version.ToString();
        var state = ManagerState.Load(Paths.BepInExRootPath, Log.LogWarning);
        ModLoader.Init(state, Log);
        ManagerService.Init(Config, Log, UnityEngine.Application.version, MetadataHelper.GetMetadata(this).Version.ToString());
        Ticker.Add(this, ManagerService.Pump);
        Ticker.Add(this, Start);
        Log.LogInfo($"{state.Mods.Count} mods installed in {ManagerState.Folder}");
    }

    // Once, on the first frame: by then BepInEx has loaded the plugins in BepInEx\plugins (the core among them, for now).
    void Start()
    {
        if (started)
            return;
        started = true;
        // This manager started: an update to it that the patcher is trying (Updater) is kept.
        new Updater(Paths.BepInExRootPath, Log.LogInfo).Confirm();
        ModLoader.LoadAll();
        // What the catalog has (read in the background; nothing installed by itself).
        ManagerService.Refresh();
    }
}
