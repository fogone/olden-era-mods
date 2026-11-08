using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldenEra.Modding.Common;

// A mod built for another version of the game stays off (ModBase); this says so on screen, not only in the log
// (fogone 05.11): while the main menu is up (no map, no battle loaded), a line at the top of the screen with the version
// the mods were built for, the game's, and which mods are off. Here in the library, which is one for every version of
// the game: it works when every mod is off, the Mods tab too. The text is the game's localization (Texts), the font a
// live game text's, in the game's language.
public static class VersionNotice
{
    const string Key = "oe.common.versionOff", Name = "OE_VersionNotice";
    static readonly SortedSet<string> mods = new();
    static string builtFor, game;
    static GameObject notice;
    static TextMeshProUGUI text;
    static bool started;

    static VersionNotice() => Texts.LoadOwn();   // its text: the library's lang files

    // A mod is off for the game's version.
    public static void Add(string mod, string built, string gameVersion)
    {
        mods.Add(mod);
        builtFor = built;
        game = gameVersion;
        if (!started)
        {
            started = true;
            Ticker.Add(typeof(VersionNotice), Update);
        }
    }

    // A mod loaded on this version after all (a fixed build reloaded by the dev loader): off the list.
    public static void Remove(string mod) => mods.Remove(mod);

    static void Update()
    {
        var wanted = mods.Count > 0 && InMainMenu();
        if (wanted && notice == null)
            Make();
        if (notice == null)
            return;
        notice.SetActive(wanted);
        if (wanted)
            text.text = string.Format(Texts.Get(Key), builtFor, game, string.Join(", ", mods));
    }

    // The main menu: neither a map (its HUD scene) nor a battle loaded.
    static bool InMainMenu()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name is "sessionHUD" or "battle" or "battleUI")
                return false;
        return true;
    }

    static void Make()
    {
        // The font the game draws its menu with in its language now: of a live text of the menu with letters (not TMP's
        // default LiberationSans, which has no Cyrillic, no CJK).
        var font = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
            .FirstOrDefault(t => t.font != null && t.isActiveAndEnabled && t.gameObject.scene.IsValid() && !t.name.StartsWith("OE_") &&
                                 !t.font.name.StartsWith("LiberationSans") && t.text != null && t.text.Any(char.IsLetter))?.font;
        if (font == null)
            return;   // the menu's texts are not up yet: the next frames
        notice = new GameObject(Name, Il2CppType.Of<RectTransform>());
        UnityEngine.Object.DontDestroyOnLoad(notice);
        var canvas = notice.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var line = new GameObject(Name + "Text", Il2CppType.Of<RectTransform>());
        line.transform.SetParent(notice.transform, false);
        text = line.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 24f;
        text.color = new Color(1f, 0.55f, 0.35f, 1f);
        text.alignment = TextAlignmentOptions.Top;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.15f, 1f);
        rect.anchorMax = new Vector2(0.85f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -12f);
        rect.sizeDelta = new Vector2(0f, 90f);
    }
}
