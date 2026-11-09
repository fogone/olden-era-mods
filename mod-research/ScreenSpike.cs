using System.Linq;
using Hex.UI;
using OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static OldenEra.Modding.Common.UiLayout;

namespace OldenEra.Modding.Research;

// A trial screen of ours in the game's screen registry (ModScreen, issue #24), to check it with the game running:
//   screen make | open | hide | state | drop
internal static class ScreenSpike
{
    const string Name = "OE_SpikeScreen";
    static ModScreen screen;
    static int closed;

    public static string Run(string what)
    {
        switch (what)
        {
            case "make":
                screen?.Destroy();
                screen = ModScreen.Create(Name, 1999);
                Build(screen.Content);
                screen.Closed = () => Plugin.Note($"spike: closed ({++closed})");
                return "made\n" + State();
            case "open":
                screen?.Open();
                return State();
            case "hide":
                screen?.Hide();
                return State();
            case "drop":
                screen?.Destroy();
                screen = null;
                return "dropped\n" + State();
            default:
                return State();
        }
    }

    static void Build(RectTransform content)
    {
        var dim = NewRect("Dim", content);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
        var panel = NewRect("Panel", content);
        panel.sizeDelta = new Vector2(700, 300);
        panel.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.12f, 0.08f, 1f);
        var text = NewRect("Text", panel);
        Stretch(text);
        var tmp = text.gameObject.AddComponent<TextMeshProUGUI>();
        var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
        if (font != null) tmp.font = font;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = "OE spike screen (ModScreen)\nEscape closes";
    }

    static string State()
    {
        var registry = GameNames.ScreenRegistry();
        var stack = string.Join(" > ", Enumerable.Range(0, registry.OpenScreens.Count).Select(i => registry.OpenScreens[i]?.name));
        var line = $"registered {registry.Screens.Count}, ours {registry.Screens.ContainsKey(Name)}; stack: {stack}; closed {closed}";
        if (screen == null)
            return line + "; no screen";
        var c = screen.Content.GetComponent<Canvas>();
        return line + $"\nours: open {screen.IsOpen}, active {screen.Content.gameObject.activeSelf}, canvas {c.renderMode} cam {(c.worldCamera != null ? c.worldCamera.name : "-")} order {c.sortingOrder}";
    }
}
