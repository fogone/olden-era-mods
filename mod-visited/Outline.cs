using Hex.Render;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nobirds.OldenEra.Modding.Common;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Visited;

// The unvisited buildings' outline: the game's own, in a color that stands out (the Highlight option).
//
// The game hands the outline's colors to the shader every frame (Game.OutlineColorsToShader): a color index
// per tag and the palette chosen in its settings. The "Interactable" tag is what the game outlines as not visited.
// For that call only, the tag's color in the palette it is given is our animated one; after it the game's own comes
// back, so the game's palette (its settings) stays as it is.
public partial class Plugin
{
    Choice<Highlight> highlight;
    static readonly Color Bright = new(1f, 0.86f, 0.25f, 1f), Peak = new(1f, 0.98f, 0.85f, 1f);

    void PatchOutlineColors(HarmonyLib.Harmony harmony)
    {
        var patched = harmony.PatchEach(GameTargets.OutlineColorsToShader,
            prefix: new HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(OutlineColorsPrefix)),
            postfix: new HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(OutlineColorsPostfix)));
        if (patched.Count == 0)
            Log.LogWarning("The outline's colors to the shader not found: the game's own outline stays");
    }

    static void OutlineColorsPrefix(Il2CppStructArray<int> __1, Il2CppStructArray<Color> __2, out (int index, Color color)? __state) =>
        __state = instance?.PaintOutline(__1, __2);

    (int index, Color color)? PaintOutline(Il2CppStructArray<int> __1, Il2CppStructArray<Color> __2)
    {
        var mode = highlight.Value(Highlight.Game);
        if (mode == Highlight.Game || __1 == null || __2 == null || __1.Length <= (int)SelectionOutlineTag.Interactable)
            return null;
        var index = __1[(int)SelectionOutlineTag.Interactable];
        if (index < 0 || index >= __2.Length)
            return null;
        var original = __2[index];
        __2[index] = Animated(original, mode, Time.unscaledTime);
        return (index, original);
    }

    static void OutlineColorsPostfix(Il2CppStructArray<Color> __2, (int index, Color color)? __state)
    {
        if (__state is { } painted && __2 != null && painted.index < __2.Length)
            __2[painted.index] = painted.color;
    }

    static Color Animated(Color original, Highlight mode, float time) => mode switch
    {
        Highlight.Bright => Bright,
        // A smooth swing original <-> near white, 1.2 s.
        Highlight.Pulse => Color.Lerp(original, Peak, 0.5f - 0.5f * Mathf.Cos(time * 2f * Mathf.PI / 1.2f)),
        // A short flash every 2 s: sharp rise, quick fall.
        Highlight.Flash => Color.Lerp(original, Peak, Mathf.Pow(Mathf.Max(0f, Mathf.Cos(time * Mathf.PI / 2f)), 12f)),
        // A hard strobe: 0.15 s of the color six times over (above white: the outline is drawn in HDR) every 0.8 s.
        Highlight.Strobe => time % 0.8f < 0.15f ? new Color(original.r * 6f, original.g * 6f, original.b * 6f, 1f) : original,
        _ => original,
    };
}

public enum Highlight { Game, Bright, Pulse, Flash, Strobe }
