using System;
using System.Collections.Generic;
using Hex.Settings.UI;
using Hex.UI;
using Hex.UI.GeneralKit;
using OldenEra.Modding.Common;
using static OldenEra.Modding.Common.UiLayout;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The Mods window's widgets: copies of the game's (a text field, a dropdown, the switch, the cross, a text button) with
// our texts and actions. The general layout helpers are mod-common/shared/UiLayout.cs.
public partial class Plugin
{
    // ---------- copies of the game's widgets ----------

    // A text field: a copy of the game's input box's, in the filter row's size (its font halved: that box is drawn on
    // a canvas twice as big), its placeholder ours, each change reported.
    TMP_InputField NewInput(Transform parent, string placeholderKey, Action<string> changed)
    {
        if (inputTemplate == null)
            return null;
        var copy = Copy(inputTemplate.gameObject, parent);
        copy.name = "OE_Input";
        Size(copy, 380f, 31f);
        copy.transform.Cast<RectTransform>().sizeDelta = new Vector2(380f, 31f);
        copy.SetActive(true);
        var input = copy.GetComponent<TMP_InputField>();
        foreach (var text in copy.GetComponentsInChildren<TMP_Text>(true))
            text.fontSize = 18f;
        input.text = "";
        input.characterLimit = 0;
        if (input.placeholder != null && input.placeholder.GetComponent<TMP_Text>() is { } placeholder)
            SetKey(placeholder, placeholderKey);
        input.onValueChanged.AddListener((UnityAction<string>)(value => Guard("input", () => changed(value))));
        return input;
    }

    // What a dropdown does with the option chosen (not while the window fills it).
    void BindDropdown(BhDropdown dropdown, Action<int> changed) =>
        dropdown.onValueChanged.AddListener((UnityAction<int>)(i => Guard("dropdown", () =>
        {
            if (!filling)
                changed(i);
        })));

    // The switch: a copy of the settings tab's switch, only its knob shown, centred (half size: the settings' canvas is
    // twice as big).
    void NewSwitch(Transform line, bool on, Action<bool> set)
    {
        var holder = NewRect("OE_Switch", line);
        Size(holder.gameObject, 70f, 50f);
        var copy = UnityEngine.Object.Instantiate(switchSource.gameObject, holder, false);
        copy.name = "Switch";
        copy.SetActive(true);
        var copyRect = copy.transform.Cast<RectTransform>();
        copyRect.anchorMin = copyRect.anchorMax = copyRect.pivot = new Vector2(0.5f, 0.5f);
        copyRect.sizeDelta = new Vector2(140f, 100f);
        copyRect.anchoredPosition = Vector2.zero;
        copyRect.localScale = new Vector3(0.5f, 0.5f, 1f);
        if (copy.GetComponent<Image>() is { } rowImage)
            rowImage.color = new Color(0f, 0f, 0f, 0f);
        for (var i = 0; i < copy.transform.childCount; i++)
        {
            var part = copy.transform.GetChild(i);
            if (part.name == "CheckBox")
            {
                var knob = part.Cast<RectTransform>();
                knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
                knob.anchoredPosition = Vector2.zero;
            }
            else
                part.gameObject.SetActive(false);
        }
        copy.GetComponent<BhSettingsCheckbox>().Init((Action<bool>)set, on);
    }

    // The remove cross: the information panel's own.
    void NewCross(Transform line, Action click)
    {
        var cross = Copy(crossSource, line).GetComponent<BhGenericButton>();
        cross.name = "OE_Remove";
        Size(cross.gameObject, 37f, 37f);
        cross.gameObject.SetActive(true);
        Bind(cross, null, click, true);
    }

    // A text button: a copy of the panel's "Set up in the lobby" button, waiting (greyed out) while the manager works.
    BhGenericButton TextButton(Transform parent, string label, float width, Action click)
    {
        var button = CopyTextButton(parent, label, new Vector2(width, 40f), click, true);
        if (ModCatalog.Busy)
            GreyOut(button, true);
        return button;
    }

    // An arrow (never waiting) at the left or right edge of its parent, vertically centred: the game's, else a small text
    // button.
    void ArrowButton(Transform parent, string key, Vector2 edge, float x, Action click)
    {
        var rect = GameArrow(parent, edge.x < 0.5f, click) ?? TextArrow(parent, key, click);
        if (rect == null)
            return;
        rect.anchorMin = rect.anchorMax = rect.pivot = edge;
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    // The main menu's arrow (its sprite and its lit one under the pointer), turned round for "back"; null while the
    // game has not loaded it.
    static RectTransform GameArrow(Transform parent, bool back, Action click)
    {
        if (Game.SpriteNamed("Arrow_MainMeny") is not { } normal)
            return null;
        var rect = NewRect("OE_Arrow", parent);
        rect.sizeDelta = new Vector2(64f, 64f);
        // The picture turned round its own centre, the button's place at the edge kept.
        var picture = NewRect("Icon", rect);
        Stretch(picture);
        if (back)
            picture.localEulerAngles = new Vector3(0f, 0f, 180f);
        var image = picture.gameObject.AddComponent<Image>();
        image.sprite = normal;
        image.preserveAspect = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (Game.SpriteNamed("Arrow_MainMeny_MouseOver") is { } lit)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            var states = button.spriteState;
            states.highlightedSprite = states.pressedSprite = states.selectedSprite = lit;
            button.spriteState = states;
        }
        button.onClick.AddListener(click);
        return rect;
    }

    RectTransform TextArrow(Transform parent, string key, Action click)
    {
        if (textButtonTemplate == null)
            return null;
        var button = CopyTextButton(parent, Texts.Get(key), new Vector2(48f, 48f), click, false);
        button.name = "OE_Arrow";
        return button.transform.Cast<RectTransform>();
    }

    BhGenericButton CopyTextButton(Transform parent, string label, Vector2 size, Action click, bool waits)
    {
        var button = Copy(textButtonTemplate, parent).GetComponent<BhGenericButton>();
        button.name = "OE_Button";
        Size(button.gameObject, size.x, size.y);
        button.transform.Cast<RectTransform>().sizeDelta = size;
        button.gameObject.SetActive(true);
        Bind(button, label, click, waits);
        if (button.label != null)
        {
            button.label.enableAutoSizing = true;
            button.label.fontSizeMin = 10f;
            button.label.fontSizeMax = Math.Max(button.label.fontSize, 12f);
        }
        return button;
    }

    // Our label and action on a game button (the copy keeps the original's persistent click: a new event with ours).
    // `waits`: does nothing while the manager works.
    void Bind(BhGenericButton button, string label, Action click, bool waits)
    {
        if (label != null)
            SetText(button.label, label);
        button.UnityClickEvent = new UnityEvent();
        button.UnityClickEvent.AddListener((UnityAction)(() =>
        {
            if (waits && ModCatalog.Busy)
                return;
            Guard(label ?? button.name, click);
            windowDirty = true;
        }));
    }

    // A new button's Start (on its first shown frame) makes it available again: greyed out then, and on the frame after.
    readonly List<(BhGenericButton button, int until)> greyed = new();

    void GreyOut(BhGenericButton button, bool grey)
    {
        button.SetAvailable(!grey);
        // Its label dimmed too (the grey picture keeps a bright label).
        if (button.label != null)
            button.label.alpha = grey ? 0.45f : 1f;
        if (grey)
            greyed.Add((button, Time.frameCount + 2));
    }

    void GreyLater()
    {
        for (var i = greyed.Count - 1; i >= 0; i--)
        {
            var (button, until) = greyed[i];
            if (button == null || Time.frameCount > until)
            {
                greyed.RemoveAt(i);
                continue;
            }
            button.SetAvailable(false);
        }
    }

    // ---------- copies ----------

    // A copy of a widget, made inactive (its scripts wake up when it is shown), then put under the parent.
    GameObject Copy(GameObject template, Transform parent)
    {
        var copy = UnityEngine.Object.Instantiate(template, staging.transform, false);
        copy.SetActive(false);
        copy.transform.SetParent(parent, false);
        return copy;
    }

    // A click on an object, its errors to the log.
    void Clickable(GameObject go, Action click) => UiLayout.Clickable(go, () => Guard("click", click));
}
