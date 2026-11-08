using System.Collections.Generic;
using System.Linq;
using Hex.UI.GeneralKit;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.Modding.Common;

// A game screen copied as a window of our own: the copy's scripts that run the screen taken off, its parts filled with
// ours. The copy keeps the screen's look (canvas, scaler, frames, fonts) and is drawn like the game's screens.
//
//   window = ScreenCopy.Make(Scene.Find<ScLoad>().gameObject, "OE_MyWindow", new HashSet<string> { "ScLoad", ... });
//   ScreenCopy.Place(window.GetComponent<Canvas>(), loadCanvas, 1600, 29000);
//   ...
//   Object.DestroyImmediate(window);   // on unload: at once, a reloaded build looks for it in the same frame
internal static class ScreenCopy
{
    // A copy of `screen` with the scripts named in `scripts` (by their Il2Cpp type name) taken off, not under any
    // parent, kept across scenes, shown, its controls on (EnableControls).
    public static GameObject Make(GameObject screen, string name, ISet<string> scripts)
    {
        // Made under an inactive holder first: the copy's scripts do not wake up before they are taken off.
        var holder = new GameObject(name + "Holder");
        holder.SetActive(false);
        var copy = Object.Instantiate(screen, holder.transform, false);
        copy.name = name;
        StripScripts(copy, scripts);
        copy.transform.SetParent(null, false);
        Object.Destroy(holder);
        Object.DontDestroyOnLoad(copy);
        copy.SetActive(true);
        if (copy.GetComponent<Canvas>() is { } canvas)
            canvas.enabled = true;
        EnableControls(copy);
        if (copy.GetComponent<CanvasGroup>() is { } group)
        {
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = true;
        }
        return copy;
    }

    // The scripts named in `scripts` (by their Il2Cpp type name) off a copy and its children.
    public static void StripScripts(GameObject copy, ISet<string> scripts)
    {
        foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true).ToList())
            if (script != null && scripts.Contains(script.GetIl2CppType().Name))
                Object.DestroyImmediate(script);
    }

    // A screen turns its parts on when it shows (by its scripts, taken off a copy): on the copy they are turned on here.
    // The raycasters (with the screen's own off only the parts under a nested canvas take clicks), the scrolling, the
    // controls, the dropdowns, the layouts. Not a dropdown's own button: the game keeps it off (the dropdown's frame
    // opens the list on the press); on, it closes the list again on the release.
    public static void EnableControls(GameObject copy)
    {
        foreach (var raycaster in copy.GetComponentsInChildren<GraphicRaycaster>(true))
            raycaster.enabled = true;
        foreach (var scroll in copy.GetComponentsInChildren<ScrollRect>(true))
            scroll.enabled = true;
        foreach (var control in copy.GetComponentsInChildren<Selectable>(true))
            if (control.TryCast<Button>() == null || control.GetComponentInParent<BhDropdown>(true) == null)
                control.enabled = true;
        foreach (var dropdown in copy.GetComponentsInChildren<BhDropdown>(true))
            dropdown.enabled = true;
        foreach (var layout in copy.GetComponentsInChildren<LayoutGroup>(true))
            layout.enabled = true;
    }

    // The copy's canvas drawn as the game's screens are: with the UI camera of `screen` (the original screen's canvas;
    // else the message box's, else any camera canvas), on its layer, above the screens the game keeps switched on and
    // just under the game's message box, so the game's message and input boxes come over it (`screenOrder`: the order
    // above which it goes when there is no message box). Its nested canvases with their own order move with it. Without
    // a camera: drawn over everything at `overlayOrder`. Returns the camera, or null.
    public static Camera Place(Canvas canvas, Canvas screen, int screenOrder, int overlayOrder)
    {
        var box = Scene.Find<ScMessageBox>();
        var messageBox = box?.GetComponent<Canvas>();
        var camera = screen?.worldCamera ?? box?.canvas?.worldCamera
                     ?? Object.FindObjectsOfType<Canvas>().FirstOrDefault(c => c != null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null)?.worldCamera;
        if (camera == null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = overlayOrder;
            return null;
        }
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        if (screen != null)
        {
            canvas.planeDistance = screen.planeDistance;
            canvas.sortingLayerID = screen.sortingLayerID;
        }
        canvas.overrideSorting = true;
        var order = messageBox != null ? messageBox.sortingOrder - 2 : (screen != null ? screen.sortingOrder : screenOrder) + 50;
        var old = canvas.sortingOrder;
        canvas.sortingOrder = order;
        foreach (var nested in canvas.GetComponentsInChildren<Canvas>(true))
            if (nested != canvas && nested.overrideSorting)
                nested.sortingOrder = order + (nested.sortingOrder - old);
        return camera;
    }

    // A part of the copy drawn over all of it: a canvas of its own sorted above the copy's other canvases (a part under
    // a nested canvas of the game's would draw over a plain child), with its raycaster.
    public static void DrawOnTop(Canvas root, Transform part)
    {
        var canvas = part.GetComponent<Canvas>() ?? part.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = root.sortingLayerID;
        canvas.sortingOrder = root.GetComponentsInChildren<Canvas>(true).Where(c => c != canvas).Max(c => c.sortingOrder) + 1;
        if (part.GetComponent<GraphicRaycaster>() == null)
            part.gameObject.AddComponent<GraphicRaycaster>();
    }
}
