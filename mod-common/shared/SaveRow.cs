using System;
using System.Collections.Generic;
using Hex.UI.SaveLoad;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Common;

// A row of the game's Load screen list showing data of ours (issue #24): an instance of the row prefab the list makes
// its rows of (BhVirtualizedList.sessionPrefab), its own script kept. The game lays it out, its presenter plays the hover
// and pressed looks; it is filled through the row's own setters. What needs a save behind it is switched off: the
// rename button and the full name's tooltip.
//
//   var row = SaveRow.Make(listContent, "OE_Row_" + id);
//   row.Name("Map badges");  row.Icon(texture);  row.Tags(new[] { ("UI", color) });
//   row.Info("Built-in catalog", "0.1.2", "");  row.Selected(picked);  row.OnClick(() => pick(id));
//   row.Check(isOn, value => ...);   // or row.NoCheck()
public sealed class SaveRow
{
    public readonly BhSessionInfoElement View;

    SaveRow(BhSessionInfoElement view) => View = view;

    public GameObject Root => View.gameObject;

    // The row prefab (an asset, not a scene's object), from the Load screen in the game's screen registry; null while
    // there is none.
    public static BhSessionInfoElement Prefab => List?.sessionPrefab;

    // The space the game's list leaves between its rows.
    public static float Spacing => List?.spacing ?? 0f;

    static BhVirtualizedList List =>
        GameNames.ScreenRegistry()?.Screen("ScLoad") is { } load && load != null &&
        load.GetComponentInChildren<BhVirtualizedList>(true) is { } list && list != null ? list : null;

    // A new row under the parent, empty: no tags, no check box, no cloud, campaign or map icons, no information.
    public static SaveRow Make(Transform parent, string name)
    {
        if (Prefab is not { } prefab || prefab == null)
            return null;
        var view = UnityEngine.Object.Instantiate(prefab.gameObject, parent, false).GetComponent<BhSessionInfoElement>();
        view.name = name;
        // Its presenter (made in Awake) reads the save for these: never wired to ours.
        if (view.sessionNameEvents != null)
            view.sessionNameEvents.enabled = false;
        view.renameButton?.gameObject.SetActive(false);
        view.cloudOn?.SetActive(false);
        view.cloudOff?.SetActive(false);
        view.ShowCampaign(false);
        view.ShowMapIcons(false);
        var row = new SaveRow(view);
        row.Tags(null);
        row.NoCheck();
        row.Info("", "", "");
        return row;
    }

    // The name; a second part after the game's separator when given.
    public void Name(string name, string second = null) => View.Presenter().ShowName(name, second ?? "");

    // The picture, whole in the row's icon frame; null hides it.
    public void Icon(Texture2D texture)
    {
        View.ShowIcon(Sprite(texture));
        if (View.sessionIcon is { } image && image != null)
        {
            image.enabled = texture != null;
            image.preserveAspect = true;
        }
    }

    // The tag chips: a text and a colour each, as many chips as tags (the prefab's own chip copied for more).
    public void Tags(IReadOnlyList<(string text, Color color)> tags)
    {
        var chips = View.sessionTags;
        var count = tags?.Count ?? 0;
        while (chips != null && chips.Count > 0 && chips.Count < count)
        {
            var first = chips[0];
            var copy = UnityEngine.Object.Instantiate(first.gameObject, first.transform.parent, false);
            copy.transform.SetSiblingIndex(chips[chips.Count - 1].transform.GetSiblingIndex() + 1);
            chips.Add(copy.GetComponent<Hex.UI.GeneralKit.BhTag>());
        }
        for (var i = 0; chips != null && i < chips.Count; i++)
        {
            if (i < count)
                View.ShowTag(i, tags[i].text, tags[i].color);
            View.ShowTagShown(i, i < count);
        }
    }

    // The tag chips on a line of their own under the name (the game puts them after it): a copy of the name's group
    // laid out as it is, one name line lower, the chips moved into it.
    public void TagsUnderName()
    {
        var chips = View.sessionTags;
        var name = View.sessionName;
        if (chips == null || chips.Count == 0 || name == null || name.transform.parent is not { } header ||
            header.GetComponent<HorizontalLayoutGroup>() is not { } layout || header.parent.Find("OE_Tags") != null)
            return;
        var headerRect = header.Cast<RectTransform>();
        var line = new GameObject("OE_Tags", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>()).transform.Cast<RectTransform>();
        line.SetParent(header.parent, false);
        line.anchorMin = headerRect.anchorMin;
        line.anchorMax = headerRect.anchorMax;
        line.pivot = headerRect.pivot;
        line.sizeDelta = headerRect.sizeDelta;
        line.anchoredPosition = headerRect.anchoredPosition - new Vector2(0f, name.GetPreferredValues(name.text).y + layout.spacing);
        var row = line.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = layout.spacing;
        row.childAlignment = layout.childAlignment;
        row.childControlWidth = layout.childControlWidth;
        row.childControlHeight = layout.childControlHeight;
        row.childForceExpandWidth = layout.childForceExpandWidth;
        row.childForceExpandHeight = layout.childForceExpandHeight;
        foreach (var chip in chips)
            chip.transform.SetParent(line, false);
    }

    // The three lines on the right (the save's header, time and game time); an empty one is hidden.
    public void Info(string header, string value, string note)
    {
        Line(View.lastSaveHeader, header);
        Line(View.lastSaveTime, value);
        Line(View.lastSaveGameTime, note);
    }

    static void Line(TMP_Text text, string value)
    {
        if (text == null)
            return;
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        text.text = value ?? "";
    }

    // The selected look.
    public void Selected(bool selected) => View.ShowSelected(selected);

    // What a click on the row does.
    public void OnClick(Action click) => View.Presenter().OnClick((Il2CppSystem.Action)click);

    // The check box shown with a value (no call back for it) and what changing it does.
    public void Check(bool value, Action<bool> changed)
    {
        View.toggle.gameObject.SetActive(true);
        GameUi.Check(View.toggle, value, changed);
    }

    public void NoCheck() => View.toggle?.gameObject.SetActive(false);

    // A sprite of a whole texture, made once per texture.
    static readonly Dictionary<IntPtr, Sprite> sprites = new();

    static Sprite Sprite(Texture2D texture)
    {
        if (texture == null)
            return null;
        if (sprites.TryGetValue(texture.Pointer, out var sprite) && sprite != null && sprite.texture == texture)
            return sprite;
        sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprites[texture.Pointer] = sprite;
    }
}
