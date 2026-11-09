using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// Finding the game's live objects and where they are on screen, the same way in every mod.
//
//   Scene.Find<ScSettings>()                        // the live settings screen (not a prefab), or null
//   Scene.Shown<BhDayPanel>("sessionHUD")           // shown objects of a type in the HUD's scene
//   Scene.OnScreen(rect, Scene.UiCamera(canvas))    // a UI rect's place on screen, in pixels
//   Scene.Tree(transform, log)                      // a debug dump of an object tree
public static class Scene
{
    // The live object of a type, one in a loaded scene (not a prefab nor an asset); null when none. Goes through every
    // loaded object: for what is looked for once, not every frame.
    public static T Find<T>() where T : Component =>
        Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());

    // The shown objects of a type in a loaded scene of that name (cheap: that scene only), else in the whole game.
    public static List<T> Shown<T>(string sceneName) where T : Component
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(false)).ToList();
        return Resources.FindObjectsOfTypeAll<T>().Where(x => x != null && x.gameObject.activeInHierarchy).ToList();
    }

    // The camera a canvas draws with: none for one drawn over the screen.
    public static Camera UiCamera(Canvas canvas) => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    // A UI rect's corners on screen, in pixels, from its own rect (GetWorldCorners fills a copy of the array under Il2Cpp).
    public static Rect OnScreen(RectTransform rect, Camera uiCamera)
    {
        var r = rect.rect;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        for (var i = 0; i < 4; i++)
        {
            var p = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(new Vector3(i % 2 == 0 ? r.xMin : r.xMax, i < 2 ? r.yMin : r.yMax)));
            xMin = Math.Min(xMin, p.x);
            yMin = Math.Min(yMin, p.y);
            xMax = Math.Max(xMax, p.x);
            yMax = Math.Max(yMax, p.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // The mouse on screen, in pixels: the game's input system, else the old input; (-1, -1) when neither answers.
    public static Vector2 MousePosition()
    {
        try
        {
            if (UnityEngine.InputSystem.Mouse.current is { } mouse)
                return mouse.position.ReadValue();
        }
        catch (Exception) { }
        try { return Input.mousePosition; }
        catch (Exception) { return new Vector2(-1f, -1f); }
    }

    // Whether the mouse is over the UI rect (its canvas's camera).
    public static bool Hovered(RectTransform rect) =>
        rect != null && rect.gameObject.activeInHierarchy && rect.GetComponentInParent<Canvas>() is { } canvas &&
        RectTransformUtility.RectangleContainsScreenPoint(rect, MousePosition(), UiCamera(canvas.rootCanvas));

    // Debug: an object tree, one line each: name, components, active, size and place, alpha, text.
    public static void Tree(Transform t, Action<string> log, int maxDepth = 6, int depth = 0)
    {
        if (depth > maxDepth)
            return;
        var components = string.Join(",", t.GetComponents<Component>().Select(c => c.GetIl2CppType().Name).Where(n => n is not ("Transform" or "RectTransform")));
        var text = t.GetComponent<TMPro.TMP_Text>()?.text;
        var rect = t.GetComponent<RectTransform>();
        var group = t.GetComponent<CanvasGroup>();
        log($"TREE {new string(' ', depth * 2)}{t.name} [{components}] active={t.gameObject.activeSelf} size={rect?.rect.size} pos={rect?.anchoredPosition}" +
            $"{(group != null ? $" alpha={group.alpha}" : "")}{(text != null ? $" text='{text}'" : "")}");
        for (var i = 0; i < t.childCount; i++)
            Tree(t.GetChild(i), log, maxDepth, depth + 1);
    }

    // An Il2Cpp list's items.
    public static IEnumerable<T> Items<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        for (var i = 0; list != null && i < list.Count; i++)
            yield return list[i];
    }
}
