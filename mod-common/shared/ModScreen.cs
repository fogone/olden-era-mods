using System;
using System.Linq;
using Hex.InputSys;
using Hex.UI;
using Hex.UI.GeneralKit;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Common;

// A screen of ours in the game's screen registry (issue #24), opened and hidden as the game's own screens are: the
// registry puts it on its stack of open screens, gives it the UI camera and the input (Escape included), takes the
// clicks from the screen below and gives them back on hide, hides the tooltips, plays the game's appear and disappear.
//
//   screen = ModScreen.Create("OE_MyScreen", ModScreen.UnderGameBoxes());
//   Build(screen.Content);                    // our content under its canvas (layer UI)
//   screen.Closed = () => ...;                // after each hide: Escape, screen.Hide(), the game's
//   screen.Open();
//   ...
//   screen.Destroy();                         // on the plugin's unload
//
// A copy of a game screen (ScreenCopy.Make), built, becomes one with ModScreen.Adopt(copy, order).
// The registry is emptied on a mode change (cqp.Clear, refilled by cqp.Init from the scenes); the screen registers
// again when it is opened next. Its object outlives the scenes (DontDestroyOnLoad).
public sealed class ModScreen
{
    public readonly string Name;
    // The canvas the content goes under.
    public readonly RectTransform Content;
    // What Escape does while the screen is on top: hide it, unless set otherwise.
    public Action Escape;
    // Called after the screen hid, however it was hidden.
    public Action Closed;

    readonly BhScreen screen;

    ModScreen(string name, BhScreen screen)
    {
        Name = name;
        this.screen = screen;
        Content = screen.GetComponent<RectTransform>();
        Escape = Hide;
    }

    // Makes an empty screen (hidden) and registers it; see Adopt.
    public static ModScreen Create(string name, int sortingOrder)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        root.AddComponent<RectTransform>();
        root.AddComponent<Canvas>();
        ScaleAsGame(root.AddComponent<CanvasScaler>());
        return Adopt(root, sortingOrder);
    }

    // Makes an object with a Canvas (a built copy of a game screen, its own scripts off) a screen of ours under its
    // object's name, and registers it (hidden); one registered under the same name before (a reloaded plugin's) is taken
    // out and destroyed. `sortingOrder` is the canvas's place among the game's screens (the game keeps each screen's
    // own: UnderGameBoxes); its nested canvases with their own order move with it.
    public static ModScreen Adopt(GameObject root, int sortingOrder)
    {
        var name = root.name;
        var registry = GameNames.ScreenRegistry();
        if (registry.Screens.TryGetValue(name, out var old))
        {
            registry.Screens.Remove(name);
            if (old != null && old.gameObject != root)
                Gone(old);
        }

        root.SetActive(false);   // BhScreen's Awake waits until its fields are set
        root.layer = 5;          // UI: the only layer the game's UI camera draws
        if (root.transform.parent == null)
            UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        SetOrder(canvas, sortingOrder);
        var raycaster = root.GetComponent<GraphicRaycaster>() ?? root.AddComponent<GraphicRaycaster>();
        var group = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();

        var screen = root.AddComponent<BhScreen>();
        screen.canvas = canvas;
        screen.cg = group;
        screen.raycaster = raycaster;
        screen.deactivateOnHide = true;     // hidden, the object is off
        screen.renderToMainCamera = true;   // the game gives it the UI camera and the UI sorting layer while it is on
        screen.SetFocused = true;
        root.SetActive(true);   // Awake: registers it under its name and hides it

        var made = new ModScreen(name, screen);
        screen.SetUpOnce();     // its input receiver (the registry's Init did it for the game's screens)
        screen.Input.Bind(EInputAction.Escape, EInputState.Down, (Il2CppSystem.Action)(() => made.Escape?.Invoke()));
        return made;
    }

    // The order just under the game's message and input boxes (the input box is drawn lower), so they come over the
    // screen; above the screens the game keeps on (its settings ~2000 are hidden while ours is open).
    public static int UnderGameBoxes(int fallback = 1998)
    {
        var orders = new[] { Scene.Find<ScMessageBox>()?.GetComponent<Canvas>(), Scene.Find<ScInputBox>()?.GetComponent<Canvas>() }
            .Where(c => c != null).Select(c => c.sortingOrder).ToList();
        return orders.Count > 0 ? orders.Min() - 2 : fallback;
    }

    static void SetOrder(Canvas canvas, int order)
    {
        var old = canvas.sortingOrder;
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        foreach (var nested in canvas.GetComponentsInChildren<Canvas>(true))
            if (nested != canvas && nested.overrideSorting)
                nested.sortingOrder = order + (nested.sortingOrder - old);
    }

    // A screen out of the game's stacks at once and destroyed at once (a reloaded build makes its own in the same frame).
    static void Gone(BhScreen screen)
    {
        var registry = GameNames.ScreenRegistry();
        registry.OpenScreens.Remove(screen);
        screen.HideAtOnce();   // off the input stack, focus popped (as on registering)
        UnityEngine.Object.DestroyImmediate(screen.gameObject);
    }

    // The same scaling as the game's screens (its message box's canvas), so sizes in our content are the game's.
    static void ScaleAsGame(CanvasScaler scaler)
    {
        var game = Scene.Find<ScMessageBox>()?.GetComponent<CanvasScaler>();
        if (game == null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return;
        }
        scaler.uiScaleMode = game.uiScaleMode;
        scaler.referenceResolution = game.referenceResolution;
        scaler.screenMatchMode = game.screenMatchMode;
        scaler.matchWidthOrHeight = game.matchWidthOrHeight;
        scaler.referencePixelsPerUnit = game.referencePixelsPerUnit;
    }

    public bool IsOpen => screen != null && screen.IsOpen;

    public void Open()
    {
        if (screen == null)
            return;
        var registry = GameNames.ScreenRegistry();
        if (!registry.Screens.ContainsKey(Name))
            registry.Register(screen);   // the registry was emptied since (a mode change)
        screen.WhenClosed = (Il2CppSystem.Action)(() => Closed?.Invoke());
        registry.Open(Name);
    }

    public void Hide()
    {
        if (!IsOpen)
            return;
        var registry = GameNames.ScreenRegistry();
        if (registry.Screens.ContainsKey(Name))
            registry.Hide(Name);
        else
        {
            // Left open when the registry was emptied: registering puts it in the hidden state at once.
            registry.Register(screen);
            Closed?.Invoke();
        }
    }

    // Out of the registry and gone at once (the plugin unloads; its reloaded build makes the screen again in the same
    // frame). Open, it is hidden by the registry first: the screen below gets the clicks back.
    public void Destroy()
    {
        if (screen == null)
            return;
        var registry = GameNames.ScreenRegistry();
        var registered = registry.Screens.TryGetValue(Name, out var found) && found == screen;
        if (registered && IsOpen)
            registry.Hide(Name);
        if (registered)
            registry.Screens.Remove(Name);
        Gone(screen);
    }
}
