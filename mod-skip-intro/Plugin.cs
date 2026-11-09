using BepInEx;
using HarmonyLib;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.SkipIntro;

// The videos on the game's start skipped (issue #9, fogone 08.11): the studios' logos and the trailer. The game's start
// is a row of steps; the intro step (Game.IntroVideos: ccj.qrx on 0.82.7) hands the clips to the video player and the
// start waits for its done flag, which the player's callback sets when the last clip ends. Here the step does not start
// the player and only sets its flag, as that callback does: the start goes on at once.
[BepInPlugin("nobirds.oldenera.skipintro", "Olden Era Skip Intro", "0.1.1")]
public class Plugin : ModBase
{
    static Switch skipVideos;
    static Plugin instance;

    protected override void OnLoad()
    {
        instance = this;
        skipVideos = Menu.Toggle("SkipVideos", true);
        if (Harmony.PatchEach(new[] { GameTargets.IntroVideos }, prefix: new HarmonyMethod(typeof(Plugin), nameof(IntroPrefix))).Count == 0)
            Logs.Warn("the game's intro step not found: the videos play");
    }

    static bool IntroPrefix(GameIntroStep __instance)
    {
        if (skipVideos?.On != true)
            return true;
        __instance.Done = true;
        instance?.Logs.Info("the intro videos skipped");
        return false;
    }
}
