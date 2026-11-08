using System;
using System.Linq;
using BepInEx;
using OldenEra.Modding.Common;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace OldenEra.Modding.Settings;

// Next to the game's version in the main menu (the game's text that shows Application.version), the Mods' own:
// "0.82.07 · моды 0.11.0" (fogone 06.11). The game's text gets our part after its own and back without it on unload.
public partial class Plugin
{
    const string VersionKey = "oe.settings.version", VersionOffKey = "oe.settings.version.off";
    TMP_Text versionText;
    string versionBase;
    int nextVersionSearch;

    void ShowVersion()
    {
        if (versionText == null)
        {
            // Only in the main menu (no map, no battle): looked for every two seconds there, not while playing.
            if (Time.frameCount < nextVersionSearch)
                return;
            nextVersionSearch = Time.frameCount + 120;
            if (!InMainMenu())
                return;
            var game = Application.version;
            versionText = Resources.FindObjectsOfTypeAll<TMP_Text>()
                .FirstOrDefault(t => t != null && t.gameObject.scene.IsValid() && t.isActiveAndEnabled && !t.name.StartsWith("OE_") && t.text != null &&
                                     t.text.Trim().StartsWith(game) && (t.text.Trim().Length <= game.Length + 12 || t.text.Contains(" · ")));   // the game's "0.82.07" (its text "Version"), with our part left on it by an unloaded build or not
            if (versionText == null)
                return;
            versionBase = versionText.text.Split(" · ")[0];
            Logs.Info($"Version: next to the game's '{versionBase}' ({versionText.name})");
        }
        HoverModsList();
        // "mods … off" while the mods' mode is "never".
        var ours = " · " + string.Format(Texts.Get(ModMenu.Mode.Value != ModMenu.ModsMode.Off ? VersionKey : VersionOffKey), ModsVersion);
        // The game may write its text again (another language): then that is its own one.
        if (!versionText.text.EndsWith(ours))
        {
            if (!versionText.text.Contains(" · "))
                versionBase = versionText.text;
            versionText.text = versionBase + ours;
        }
    }

    // The mods' version: the mod manager's (fogone 07.11; the mods come and update one by one through it). Without the
    // manager, the old set's version (version.txt, written into every assembly at build time: AssemblyMetadata "ModsVersion").
    string ModsVersion => ModCatalog.ManagerVersion ?? SetVersion;

    string SetVersion => GetType().Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .OfType<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "ModsVersion")?.Value ?? "?";

    // On hovering the version: a table of every mod of ours, its version and whether it is on, off or built for another
    // version of the game (fogone 06.11). A panel of our own, laid out by Unity's layouts (a vertical group of rows, each a
    // horizontal group of cells with fixed widths): the game's text tooltip centres its lines, and columns made in its
    // text slipped. The panel has the game's tooltip frame (its basic tooltip's own sprite), the menu's font, our drawn
    // state badges; a mod that is off in grey, one for another version in amber with that version.
    const string ModsKey = "oe.settings.mods", OtherKey = "oe.settings.mods.other";
    const string TableName = "OE_ModsTable";
    static readonly Color Grey = new(0.54f, 0.54f, 0.54f, 1f), Amber = new(0.95f, 0.71f, 0.27f, 1f), Light = new(0.92f, 0.88f, 0.80f, 1f);
    const float FontSize = 22f, IconSize = 24f, Gap = 18f;
    GameObject table;
    Sprite offIcon;

    void HoverModsList()
    {
        var hovered = Scene.Hovered(versionText.rectTransform);
        if (hovered && (table == null || !table.activeSelf))
            ShowTable();
        else if (!hovered && table != null && table.activeSelf)
            table.SetActive(false);
    }

    void ShowTable()
    {
        if (table != null)
            UnityEngine.Object.Destroy(table);
        var canvas = versionText.canvas != null ? versionText.canvas.rootCanvas : null;
        if (canvas == null)
            return;
        // On a canvas of its own over everything: the version stands on the menu's background canvas, drawn behind the
        // menu (the table was hidden under it, 06.11). Its place in screen pixels.
        table = new GameObject(TableName, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        var overlay = table.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 32000;
        var panel = new GameObject("Panel", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        panel.transform.SetParent(table.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        // The game's tooltip frame behind it.
        var background = panel.AddComponent<Image>();
        if (GameNames.Tooltips?.basic?.GetComponent<Image>() is { } frame)
        {
            background.sprite = frame.sprite;
            background.type = frame.type;
            background.color = frame.color;
        }
        else
            background.color = new Color(0.08f, 0.07f, 0.06f, 0.92f);
        background.raycastTarget = false;
        var column = panel.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset { left = 22, right = 22, top = 16, bottom = 18 };
        column.spacing = 6f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = Cell(panel.transform, string.Format(Texts.Get(ModsKey), ModsVersion, GameNames.GameVersion), FontSize * 1.1f, Light, -1f);
        title.fontStyle = FontStyles.Bold;
        var menus = ModMenu.All.ToList();
        // The name column as wide as the widest name; the version column as the widest version.
        var names = menus.Select(m => Texts.Get($"oe.{m.Id}.title")).ToList();
        // No mods yet (only the manager installed): just the title.
        var nameWidth = names.Select(n => Measure(n)).DefaultIfEmpty(0f).Max() + Gap;
        var versionWidth = menus.Select(m => Measure(m.Version)).DefaultIfEmpty(0f).Max() + Gap;
        for (var i = 0; i < menus.Count; i++)
        {
            var menu = menus[i];
            var color = menu.IsOff ? Amber : menu.IsEnabled ? Light : Grey;
            var row = new GameObject("Row", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            row.transform.SetParent(panel.transform, false);
            var line = row.AddComponent<HorizontalLayoutGroup>();
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;
            Cell(row.transform, names[i], FontSize, color, nameWidth);
            Cell(row.transform, menu.Version, FontSize, color, versionWidth);
            Icon(row.transform, menu.IsOff ? BadgeIcons.Icon(Badge.Available) : menu.IsEnabled ? BadgeIcons.Icon(Badge.Visited) : OffIcon(),
                 menu.IsOff || menu.IsEnabled ? Color.white : Grey);
            if (menu.IsOff)
                Cell(row.transform, "  " + string.Format(Texts.Get(OtherKey), menu.BuiltFor), FontSize, Amber, -1f);
        }
        // Above the version, on the side of the screen the version is on (screen pixels: the overlay canvas's own).
        var label = Scene.OnScreen(versionText.rectTransform, Scene.UiCamera(canvas));
        var right = label.center.x > Screen.width / 2f;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(right ? 1f : 0f, 0f);
        rect.anchoredPosition = new Vector2(right ? label.xMax : label.xMin, label.yMax + 8f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    // A text cell in the menu's font; `width` fixed when not negative.
    TextMeshProUGUI Cell(Transform parent, string text, float size, Color color, float width)
    {
        var go = new GameObject("Cell", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.transform.SetParent(parent, false);
        var cell = go.AddComponent<TextMeshProUGUI>();
        cell.font = versionText.font;
        cell.fontSize = size;
        cell.color = color;
        cell.text = text;
        cell.enableWordWrapping = false;
        cell.raycastTarget = false;
        if (width >= 0f)
        {
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
        }
        return cell;
    }

    void Icon(Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject("Icon", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var element = go.AddComponent<LayoutElement>();
        element.preferredWidth = element.preferredHeight = element.minWidth = element.minHeight = IconSize;
    }

    // A text's width in the menu's font at the table's size.
    float Measure(string text)
    {
        var size = versionText.fontSize;
        versionText.fontSize = FontSize;
        var width = versionText.GetPreferredValues(text).x;
        versionText.fontSize = size;
        return width;
    }

    // Off: a cross in grey on our badge.
    Sprite OffIcon() => offIcon != null ? offIcon : offIcon = Badges.Draw(Badges.Gold, (x, y) =>
        Math.Min(Badges.Segment(x, y, 20, 20, 44, 44), Badges.Segment(x, y, 44, 20, 20, 44)), frameWidth: 1.5f, strokeWidth: 4f, frame: Badges.Frame);

    void HideVersion()
    {
        if (table != null)
            UnityEngine.Object.Destroy(table);
        table = null;
        if (versionText != null && versionBase != null)
            versionText.text = versionBase;
        versionText = null;
    }

    static bool InMainMenu()
    {
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name is "sessionHUD" or "battle" or "battleUI")
                return false;
        return true;
    }
}
