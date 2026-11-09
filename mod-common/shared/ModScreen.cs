using System;
using Hex.InputSys;
using Hex.UI;
using Hex.UI.GeneralKit;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.Modding.Common;

// A screen of ours in the game's screen registry (issue #24), opened and hidden as the game's own screens are: the
// registry puts it on its stack of open screens, gives it the UI camera and the input (Escape included), takes the
// clicks from the screen below and gives them back on hide, hides the tooltips, plays the game's appear and disappear.
//
//   screen = ModScreen.Create("OE_ModsScreen", sortingOrder: 1998);
//   Build(screen.Content);                    // our content under its canvas (layer UI)
//   screen.Closed = () => ...;                // after each hide: Escape, screen.Hide(), the game's
//   screen.Open();
//   ...
//   screen.Destroy();                         // on the plugin's unload
//
// The registry is emptied on a mode change (cqp.Clear, refilled by cqp.Init from the scenes); the screen registers
// again when it is opened next. Its object outlives the scenes (DontDestroyOnLoad).
internal sealed class ModScreen
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

    // Makes the screen (hidden) and registers it; one registered under the same name before (a reloaded plugin's) is
    // taken out and destroyed. `sortingOrder` is the canvas's place among the game's screens: the game keeps each
    // screen's own (its message box is at 5002, its input box at 2000).
    public static ModScreen Create(string name, int sortingOrder)
    {
        var registry = GameNames.ScreenRegistry();
        if (registry.Screens.TryGetValue(name, out var old))
        {
            if (old != null)
            {
                if (old.IsOpen)
                    registry.Hide(name);
                UnityEngine.Object.Destroy(old.gameObject);
            }
            registry.Screens.Remove(name);
        }

        var root = new GameObject(name);
        root.SetActive(false);   // BhScreen's Awake waits until its fields are set
        root.layer = 5;          // UI: the only layer the game's UI camera draws
        UnityEngine.Object.DontDestroyOnLoad(root);
        root.AddComponent<RectTransform>();
        var canvas = root.AddComponent<Canvas>();
        canvas.sortingOrder = sortingOrder;
        ScaleAsGame(root.AddComponent<CanvasScaler>());
        var raycaster = root.AddComponent<GraphicRaycaster>();
        var group = root.AddComponent<CanvasGroup>();

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

    // Out of the registry and gone (the plugin unloads).
    public void Destroy()
    {
        if (screen == null)
            return;
        Hide();
        var registry = GameNames.ScreenRegistry();
        if (registry.Screens.TryGetValue(Name, out var registered) && registered == screen)
            registry.Screens.Remove(Name);
        UnityEngine.Object.Destroy(screen.gameObject);
    }
}
