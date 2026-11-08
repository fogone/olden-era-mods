using System;
using System.Collections.Generic;
using System.Linq;
using Hex.UI;
using Hex.UI.GeneralKit;
using OldenEra.Modding.Common;
using static OldenEra.Modding.Common.UiLayout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The Mods window's right panel (the Load screen's information panel): a mod's details, its screenshots in the panel's
// picture squares, the big screenshot over the window.
public partial class Plugin
{
    // The picture squares at the top of the panel, left to right (PictureSquares).
    readonly List<RectTransform> shotSlots = new();
    Transform squaresBack;
    // The screenshots (their files) of the mod in the details; the first one in the squares, the one shown big (-1: none).
    readonly List<string> panelShots = new();
    int shotIndex, bigShotIndex = -1;
    PictureCache pictures;

    // The panel's big square pictures (its preview and its minimap's), left to right: the biggest roughly square graphic
    // at each place across. Found by their shape, not by name, and logged; the template preview alone when none is.
    void FindPictureSquares(Transform back)
    {
        shotSlots.Clear();
        squaresBack = back;
        if (back == null)
            return;
        var squares = back.GetComponentsInChildren<Graphic>(true)
            .Select(g => g.rectTransform).Where(r => r != back)
            .Where(r => r.rect.width >= 100f && r.rect.height >= 100f && Math.Abs(r.rect.width - r.rect.height) <= 0.25f * Math.Max(r.rect.width, r.rect.height))
            .Select(r => (rect: r, x: back.InverseTransformPoint(r.TransformPoint(r.rect.center)).x))
            .GroupBy(s => Mathf.Round(s.x / 40f))
            // At one place: the picture itself (a RawImage) rather than its frame, else the inner one.
            .Select(g => g.OrderByDescending(s => s.rect.GetComponent<RawImage>() != null).ThenBy(s => s.rect.rect.width * s.rect.rect.height).First())
            .OrderBy(s => s.x).Take(2).Select(s => s.rect).ToList();
        if (squares.Count == 0 && back.Find("TemplatePreview") is { } preview)
            squares.Add(preview.Cast<RectTransform>());
        shotSlots.AddRange(squares);
        Logs.Info("Mods window: picture squares " + string.Join(", ", shotSlots.Select(s => $"{s.name} {s.rect.width:0}x{s.rect.height:0}")));
    }

    void FillDetails()
    {
        Clear(infoContent);
        panelShots.Clear();
        if (tab == WindowTab.Sources)
        {
            var source = ModCatalog.Sources.FirstOrDefault(s => s.Url == sourcePick);
            ShowPanel(source != null, W + "pickSource");
            if (source != null)
                SourceDetails(source);
            return;
        }
        var id = tab == WindowTab.Installed ? installedPick : catalogPick;
        var mod = id == null ? null : ModCatalog.Manager?.Id == id ? ModCatalog.Manager : ModCatalog.Mods.FirstOrDefault(m => m.Id == id);
        ShowPanel(mod != null, W + "pickMod");
        if (mod == null)
            return;
        if (screenshotsAsked.Add(mod.Id) && ModCatalog.LoadScreenshots is { } load)
            Guard("load screenshots " + mod.Id, () => load(mod.Id));

        if (panelTitle != null)
            SetText(panelTitle, Texts.Get(mod.NameKey));
        panelShots.AddRange((mod.Screenshots ?? Array.Empty<ModScreenshot>()).Where(s => s?.Path != null).Select(s => s.Path));
        ShowSquares();
        SetPair(versionLabel, versionValue, Texts.Get(W + "versionLabel"),
            mod.Installed != null && mod.HasUpdate && mod.Available != null ? $"{mod.Installed} / {mod.Available}" : mod.Installed ?? mod.Available ?? "");
        // The catalog's box: its icon at the left, its name over the mod's author.
        SetPair(originLabel, originValue, Coloured(mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : Texts.Get(W + "byHand"), versionValue),
            string.IsNullOrEmpty(mod.Author) ? "" : PairText(W + "authorLabel", mod.Author));
        BoxIcon(Picture(CatalogIcon(mod.SourceNameKey)));

        if (mod.NoteKey != null)
            InfoLine($"<color=#A5A5A5>{Texts.Get(mod.NoteKey)}</color>");
        if (mod.TagKeys is { Count: > 0 } tagKeys)
            Chips(tagKeys);
        if (mod.DescriptionKey != null)
            InfoText(Texts.Get(mod.DescriptionKey));
        if (mod.RequiresKeys is { Count: > 0 } requires)
            InfoText(PairText(W + "requiresLabel", string.Join(", ", requires.Select(Texts.Get))));

        // The buttons by tab: the big one Update / Install, the bin Remove. No Update and no Remove for a mod BepInEx
        // loaded itself (ByHand).
        if (tab == WindowTab.Installed)
        {
            PanelButton(primaryButton, W + "update", mod.HasUpdate && !mod.ByHand && ModCatalog.Update != null, () => ModCatalog.Update?.Invoke(new[] { mod.Id }));
            PanelButton(deleteButton, null, mod != ModCatalog.Manager && mod.Installed != null && !mod.RequiredByOthers && !mod.ByHand && ModCatalog.Remove != null,
                () => ModCatalog.Remove?.Invoke(new[] { mod.Id }));
        }
        else
        {
            PanelButton(primaryButton, W + "install", mod.Installed == null && mod.Available != null && ModCatalog.Install != null, () => ModCatalog.Install?.Invoke(mod.Id));
            PanelButton(deleteButton, null, false, null);
        }
        PanelButton(secondaryButton, null, false, null);
    }

    // ---------- the picture squares ----------

    // The screenshots from shotIndex in the squares, cut to fill each (a click shows it big), with arrows to the next
    // ones when there are more than the squares; a square without one is off (the panel's ornaments show there).
    void ShowSquares()
    {
        foreach (var slot in shotSlots)
        {
            ClearOwn(slot);
            ShowSlot(slot, false);
        }
        if (shotSlots.Count == 0 || panelShots.Count == 0)
            return;
        if (shotIndex >= panelShots.Count)
            shotIndex = 0;
        for (var i = 0; i < shotSlots.Count && shotIndex + i < panelShots.Count; i++)
        {
            var index = shotIndex + i;
            var image = SlotImage(shotSlots[i]);
            Pictures.Cover(image, Picture(panelShots[index]));
            Clickable(image.gameObject, () => Redraw(() => bigShotIndex = index));
        }
        if (panelShots.Count <= shotSlots.Count)
            return;
        var pages = shotSlots.Count;
        var lastPage = (panelShots.Count - 1) / pages * pages;
        ArrowButton(shotSlots[0], W + "prev", new Vector2(0f, 0.5f), 6f,
            () => Redraw(() => shotIndex = shotIndex == 0 ? lastPage : shotIndex - pages));
        // The last page may fill the first square only: the next arrow on the first square then.
        var right = shotSlots.Last(s => s.gameObject.activeSelf);
        ArrowButton(right, W + "next", new Vector2(1f, 0.5f), -6f,
            () => Redraw(() => shotIndex = shotIndex >= lastPage ? 0 : shotIndex + pages));
    }

    // A square on or off with what holds it in the panel: the game keeps a square's holder off while it shows nothing
    // (the minimap's picture is inside MinimapScreenShot).
    void ShowSlot(RectTransform slot, bool shown)
    {
        for (var t = (Transform)slot; t != null && t != squaresBack; t = t.parent)
            t.gameObject.SetActive(shown);
    }

    // The square shown, and the picture drawn in it: the square's own RawImage, or one of ours over it.
    RawImage SlotImage(RectTransform slot)
    {
        ShowSlot(slot, true);
        if (slot.GetComponent<RawImage>() is { } own)
            return own;
        var picture = NewRect("OE_Shot", slot).gameObject.AddComponent<RawImage>();
        Stretch(picture.rectTransform);
        return picture;
    }

    // Our children of a game object (named OE_...) gone.
    static void ClearOwn(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name.StartsWith("OE_", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    // ---------- the panel's parts ----------

    // The panel, or the empty one with a hint.
    void ShowPanel(bool show, string hintKey)
    {
        panel?.gameObject.SetActive(show);
        if (emptyPanel == null)
            return;
        emptyPanel.gameObject.SetActive(!show);
        if (emptyPanel.Find("OE_Hint")?.GetComponent<TMP_Text>() is { } hint)
            SetText(hint, Texts.Get(hintKey));
    }

    static void SetPair(TMP_Text label, TMP_Text value, string labelText, string valueText)
    {
        if (label != null)
            SetText(label, labelText);
        if (value != null)
            SetText(value, valueText);
    }

    // A picture at the left of the panel's second box (its two lines moved right to make room for it); null: none.
    void BoxIcon(Texture2D icon)
    {
        if (originLabel == null || originValue == null)
            return;
        var parent = originLabel.transform.parent;
        var image = parent.Find("OE_BoxIcon")?.GetComponent<RawImage>();
        var room = 0f;
        if (icon == null)
            image?.gameObject.SetActive(false);
        else
        {
            if (image == null)
            {
                image = NewRect("OE_BoxIcon", parent).gameObject.AddComponent<RawImage>();
                image.raycastTarget = false;
            }
            image.gameObject.SetActive(true);
            image.texture = icon;
            // The box's two lines, from the top of the first to the bottom of the second, in the parent's space.
            var top = Corners(originLabel.rectTransform, parent);
            var bottom = Corners(originValue.rectTransform, parent);
            var side = Math.Min(56f, top.yMax - bottom.yMin);
            Pictures.FitInside(image.rectTransform, icon, new Vector2(side, side));
            var left = Math.Min(top.xMin, bottom.xMin) + 12f;
            image.rectTransform.localPosition = new Vector3(left + side / 2f, (top.yMax + bottom.yMin) / 2f, 0f);
            room = side + 20f;
        }
        originLabel.margin = originValue.margin = new Vector4(room, 0f, 0f, 0f);
    }

    // A rect's corners in another transform's space (GetWorldCorners does not fill a managed array under Il2Cpp).
    static Rect Corners(RectTransform rect, Transform space)
    {
        var r = rect.rect;
        var a = space.InverseTransformPoint(rect.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
        var b = space.InverseTransformPoint(rect.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    // A line of the panel's information, in its own style.
    void InfoLine(string text)
    {
        if (lineTemplate == null || string.IsNullOrEmpty(text))
            return;
        var line = Copy(lineTemplate, infoContent);
        line.name = "OE_Line";
        line.SetActive(true);
        var tmp = line.GetComponent<TMP_Text>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        SetText(tmp, text);
    }

    // "Label: value", the label muted (as the panel's own labels), with a picture (the catalog's icon) before the value.
    void InfoPair(string labelKey, string value, string iconPath = null)
    {
        if (lineTemplate == null || string.IsNullOrEmpty(value))
            return;
        if (Picture(iconPath) is not { } icon)
        {
            InfoLine(PairText(labelKey, value));
            return;
        }
        var text = PairText(labelKey, "");
        var row = InfoRow("OE_Pair", 40f);
        HRow(row.gameObject, 8f, TextAnchor.MiddleLeft);
        AddLine(row, text);
        var picture = NewRect("OE_Icon", row).gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        picture.texture = icon;
        Pictures.FitInside(picture.rectTransform, icon, new Vector2(36f, 36f));
        Size(picture.gameObject, picture.rectTransform.sizeDelta.x, picture.rectTransform.sizeDelta.y);
        AddLine(row, Coloured(value, versionValue));
    }

    // "Label: value" by the language's own punctuation, in the colours of the panel's own label and value (its version
    // pair at the bottom: the label grey, the value light).
    string PairText(string labelKey, string value) =>
        string.Format(Texts.Get(W + "pair"), Coloured(Texts.Get(labelKey), versionLabel), Coloured(value, versionValue));

    static string Coloured(string text, TMP_Text like) =>
        like == null || string.IsNullOrEmpty(text) ? text : $"<color=#{ColorUtility.ToHtmlStringRGB(like.color)}>{text}</color>";

    // A row of the information as wide as its text lines (the list does not size its children, it centres them).
    RectTransform InfoRow(string name, float height)
    {
        var row = NewRect(name, infoContent);
        var width = lineTemplate != null ? lineTemplate.transform.Cast<RectTransform>().rect.width : 440f;
        row.sizeDelta = new Vector2(width, height);
        Size(row.gameObject, width, height);
        return row;
    }

    // A line of the panel's style that keeps its own width (in a row); that width.
    float AddLine(Transform row, string text)
    {
        var line = Copy(lineTemplate, row);
        line.name = "OE_Line";
        line.SetActive(true);
        var tmp = line.GetComponent<TMP_Text>();
        tmp.enableWordWrapping = false;
        SetText(tmp, text);
        // Measured from the text itself: its mesh is not built yet.
        var element = line.GetComponent<LayoutElement>() ?? line.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = tmp.GetPreferredValues(text).x;
        element.flexibleWidth = 0f;
        // Its own width too, for a row that does not size its children.
        var rect = line.transform.Cast<RectTransform>();
        rect.sizeDelta = new Vector2(element.preferredWidth, rect.sizeDelta.y);
        return element.preferredWidth;
    }

    // A text that wraps (the description), as the panel's own description.
    void InfoText(string text)
    {
        var template = wrapTemplate ?? lineTemplate;
        if (template == null || string.IsNullOrEmpty(text))
            return;
        var block = Copy(template, infoContent);
        block.name = "OE_Text";
        block.SetActive(true);
        var tmp = block.GetComponent<TMP_Text>();
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        SetText(tmp, text);
        if (block.GetComponent<LayoutElement>() is { } element)
            element.preferredHeight = element.minHeight = -1f;
        if (block.GetComponent<ContentSizeFitter>() == null)
            block.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // The tags as the game's chips, in rows as wide as the panel.
    void Chips(IReadOnlyList<string> keys)
    {
        if (chipTemplate == null)
            return;
        Transform row = null;
        var width = 0f;
        foreach (var key in keys)
        {
            if (row == null || width > 360f)
            {
                var first = row == null;
                row = InfoRow("OE_Chips", 26f);
                HRow(row.gameObject, 6f, TextAnchor.MiddleLeft);
                width = 0f;
                // "Tags:" before the first row, as the other lines' labels.
                if (first && lineTemplate != null)
                    width += AddLine(row, PairText(W + "tagsLabel", "")) + 6f;
            }
            var chip = Copy(chipTemplate, row).GetComponent<BhTag>();
            chip.gameObject.SetActive(true);
            ShowChip(chip, key);
            width += (chip.layoutElement != null ? chip.layoutElement.preferredWidth : 80f) + 6f;
        }
    }

    // A button of the panel: our label (null: keep its picture) and action, hidden while not available.
    void PanelButton(BhGenericButton button, string key, bool available, Action click)
    {
        if (button == null)
            return;
        button.gameObject.SetActive(available);
        if (available)
            Bind(button, key != null ? Texts.Get(key) : null, click, true);
        if (available && ModCatalog.Busy)
            GreyOut(button, true);
    }

    // ---------- the big screenshot ----------

    // The screenshot bigShotIndex whole over the window on a dark backdrop, with arrows to the others and "2 / 3" under
    // it; a click (but on the arrows) or Escape closes it, the arrow keys page.
    void FillBigShot()
    {
        Clear(bigShotView);
        if (bigShotIndex >= panelShots.Count)
            bigShotIndex = -1;
        bigShotView.gameObject.SetActive(bigShotIndex >= 0);
        if (bigShotIndex < 0)
            return;
        bigShotView.SetAsLastSibling();
        ScreenCopy.DrawOnTop(window.GetComponent<Canvas>(), bigShotView);
        var shade = bigShotView.gameObject.GetComponent<Image>() ?? bigShotView.gameObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.85f);
        Clickable(bigShotView.gameObject, () => Redraw(() => bigShotIndex = -1));
        var picture = NewRect("Shot", bigShotView).gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        var texture = Picture(panelShots[bigShotIndex]);
        picture.texture = texture;
        var area = window.GetComponent<RectTransform>().rect;
        Pictures.FitInside(picture.rectTransform, texture, new Vector2(area.width - 160f, area.height - 120f), 16f / 9f);
        if (panelShots.Count < 2)
            return;
        ArrowButton(bigShotView, W + "prev", new Vector2(0f, 0.5f), 20f, () => Redraw(() => StepBigShot(-1)));
        ArrowButton(bigShotView, W + "next", new Vector2(1f, 0.5f), -20f, () => Redraw(() => StepBigShot(1)));
        if (lineTemplate == null)
            return;
        var counter = Copy(lineTemplate, bigShotView);
        counter.name = "OE_Counter";
        var rect = counter.transform.Cast<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(300f, 40f);
        var text = counter.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        SetText(text, string.Format(Texts.Get(W + "shotOf"), bigShotIndex + 1, panelShots.Count));
        counter.SetActive(true);
    }

    // The next (+1) or the previous (-1) screenshot shown big, round the ends.
    void StepBigShot(int step)
    {
        if (bigShotIndex >= 0 && panelShots.Count > 0)
            bigShotIndex = (bigShotIndex + step + panelShots.Count) % panelShots.Count;
    }

    // ---------- pictures ----------

    // A picture file as a texture, loaded once (destroyed with the window).
    Texture2D Picture(string path) => (pictures ??= new PictureCache(error => Logs.Once("Mods window: " + error))).Get(path);
}
