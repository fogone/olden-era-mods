using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Nobirds.OldenEra.Modding.Common;

// The game's start: the intro videos (issue #9). The comments name each member as it is on 0.82.7.
public static partial class Game
{
    // The game's video player's static Play(ClipInfo[] clips, bool, Action whenDone) (lr.Play): plays the clips one after
    // another (each with its own skip rules), then calls back.
    static readonly Lookup<MethodInfo> playVideos = Lookup<MethodInfo>.Among("play videos (ClipInfo[], bool, Action)",
        () => Types.Methods().Static().Named("Play").Returns(typeof(void))
            .Takes(typeof(Il2CppReferenceArray<Hex.Videoplayer.ClipInfo>), typeof(bool), typeof(Il2CppSystem.Action)),
        Members.Name, onlyOne: true);

    // The start's step that shows the intro videos (ccj.qrx): a step of the game's start (its class says how it went by
    // Hex.InitScene.EResualt) whose run, void M(), clears its done flag, makes the clips (the studios' logos, the
    // trailer) and hands them to Play with its own callback, which sets the flag; the start waits for the flag.
    static readonly Lookup<MethodInfo> introVideos = Lookup<MethodInfo>.Among("intro videos step",
        () => playVideos.Value is { } play
            ? Types.Where(t => t.Methods().Instance().Returns(typeof(Hex.InitScene.EResualt)).Takes().Any())
                .Methods().Instance().Returns(typeof(void)).Takes()
                .Where(m => GameCode.Calls(m).Any(c => GameCode.At(c.target).Contains(play)))
            : null,
        Members.Name, onlyOne: true).GenerateTarget("IntroVideos");

    // The step's class (ccj).
    static readonly Lookup<Type> introStep = Lookup<Type>.Among("intro videos step class",
        () => introVideos.Value is { } run ? new[] { run.DeclaringType } : null, t => t.Name).Generate("GameIntroStep");

    // Its done flag (ccgc), the one bool of its own: the start goes on once it is set.
    static readonly Lookup<PropertyInfo> introDone = Lookup<PropertyInfo>.Among("intro videos step done",
        () => introStep.Value is { } step ? Properties(step).Where(p => p.DeclaringType == step && p.PropertyType == typeof(bool)) : null,
        Name, onlyOne: true).Generate("Done", writable: true);
}
