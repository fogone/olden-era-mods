using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OldenEra.Modding.Common;

// Small helpers for building UI from code next to the game's widgets: a new rect, stretching, a layout size, a row
// layout, clearing and hiding children, a click on any object.
//
//   using static OldenEra.Modding.Common.UiLayout;
//   var strip = NewRect("OE_Strip", parent);  Size(strip.gameObject, 440f, 40f);  HRow(strip.gameObject, 8f, TextAnchor.MiddleLeft);
//   Clickable(strip.gameObject, () => ...);
internal static class UiLayout
{
    // On its parent's layer: the game's UI camera draws only the UI layer, and a new object starts on Default (it would
    // take clicks but not be drawn once it has a canvas of its own).
    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform.Cast<RectTransform>();
    }

    // Over the whole parent.
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Wider, its left edge where it was (a group laying its children out from the left).
    public static void Widen(RectTransform rect, float width)
    {
        var grow = width - rect.rect.width;
        if (grow <= 0f)
            return;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x + grow, rect.sizeDelta.y);
        rect.anchoredPosition += new Vector2(grow * rect.pivot.x, 0f);
    }

    // The size a layout group gives it (a negative one: left as it is).
    public static LayoutElement Size(GameObject go, float width, float height)
    {
        var size = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (width >= 0f)
            size.preferredWidth = size.minWidth = width;
        if (height >= 0f)
            size.preferredHeight = size.minHeight = height;
        return size;
    }

    // Its children in a row, sized by their layout sizes, not stretched.
    public static HorizontalLayoutGroup HRow(GameObject go, float spacing, TextAnchor alignment)
    {
        var line = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
        line.spacing = spacing;
        line.childAlignment = alignment;
        line.childControlWidth = true;
        line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;
        return line;
    }

    // Every child destroyed at once.
    public static void Clear(Transform parent)
    {
        if (parent == null)
            return;
        for (var i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    // The children of these names hidden.
    public static void Hide(Transform parent, params string[] names)
    {
        if (parent == null)
            return;
        foreach (var name in names)
            if (parent.Find(name) is { } part)
                part.gameObject.SetActive(false);
    }

    // A click on an object (a transparent picture added to catch it when it has none); a new click replaces the old.
    public static void Clickable(GameObject go, Action click)
    {
        if (go.GetComponent<Graphic>() == null)
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener((UnityAction)click);
    }
}
