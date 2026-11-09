using BepInEx;
using BepInEx.Preloader.Core.Patching;

namespace Nobirds.OldenEra.Modding.Manager;

// The mod manager's preloader patcher (BepInEx\patchers, issue #10): BepInEx runs it at the start, before it loads any
// plugin, so it can replace the files a running game holds: the manager and the library in BepInEx\plugins (Updater:
// a staged update applied, an update that did not start rolled back). It patches no assembly.
[PatcherPluginInfo("nobirds.oldenera.manager.patcher", "Olden Era Mod Manager Patcher", "0.1.0")]
public class Patcher : BasePatcher
{
    public override void Initialize() => new Updater(Paths.BepInExRootPath, Log.LogInfo).ApplyAtStart();
}
