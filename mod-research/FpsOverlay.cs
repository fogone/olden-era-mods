using System.Collections.Generic;
using System.Linq;
using Hex.Session.Hud;
using Hex.Session.World;
using Nobirds.OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Research;

// The frame rate under the players' list of the map (else in the top left corner of the screen), with a switch in the "Моды" tab (ShowFps): the frames per
// second of the last second in a disc, green while smooth, amber with a frame over 33 ms, red with one over 100 ms
// (Perf's frame times), twice a second. The number is written like the move points along a hero's path: with the
// font, material and style of the game's prefab for them (BhPathDrawerMovePoints.prefab).
internal static class FpsOverlay
{
    public const string ObjectName = "OE_Fps";
    const float Size = 64f;
    static readonly Color Smooth = new(0.35f, 0.72f, 0.30f), Hitches = new(0.92f, 0.60f, 0.15f), Stutters = new(0.85f, 0.25f, 0.20f);
    static GameObject root;
    static Image disc;
    static TextMeshProUGUI number;
    static float nextUpdate;

    public static void Update(bool on)
    {
        if (!on)
        {
            if (root != null && root.activeSelf)
                root.SetActive(false);
            return;
        }
        if (root == null)
            Make();
        if (!root.activeSelf)
            root.SetActive(true);
        var hovered = RectTransformUtility.RectangleContainsScreenPoint(disc.rectTransform, Scene.MousePosition(), null);
        if (hovered != details.gameObject.activeSelf)
        {
            details.gameObject.SetActive(hovered);
            nextUpdate = 0f;
        }
        if (Time.unscaledTime < nextUpdate)
            return;
        nextUpdate = Time.unscaledTime + 0.5f;
        var (fps, worst) = Perf.Recent(1f);
        number.text = $"{fps:0}";
        disc.color = worst > 100f ? Stutters : worst > 33f ? Hitches : Smooth;
        PlaceUnderPlayers();
        if (hovered)
            FillDetails();
    }

    // ---------- the details, while the mouse is over the disc ----------

    static Image details;
    static TextMeshProUGUI detailsText;

    static void FillDetails()
    {
        var lines = new List<string>();
        if (Perf.Measure(10f, 8) is { } s)
        {
            lines.Add(string.Format(Texts.Get("oe.research.text.Frames"), s.Span, s.Fps, s.Median, s.Worst));
            lines.Add(string.Format(Texts.Get("oe.research.text.Slow"), s.Over33, s.Over100));
            if (s.Timed)
            {
                lines.Add(string.Format(Texts.Get("oe.research.text.Ours"), s.OursPerSecond, s.OursShare, s.OursWorst));
                lines.AddRange(s.Top.Select(t => $"<size=80%>{t.msPerSecond:0.00} · {t.max:0.0} · {t.name}</size>"));
            }
            else
                lines.Add(Texts.Get("oe.research.text.NotTimed"));
        }
        detailsText.text = string.Join("\n", lines);
        // Right of the disc, its top at the disc's top.
        var rect = details.rectTransform;
        var at = disc.rectTransform;
        rect.anchorMin = rect.anchorMax = at.anchorMin;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = at.anchoredPosition + new Vector2(at.pivot.x == 0.5f ? Size / 2f + 8f : Size + 8f, 0f);
        rect.sizeDelta = new Vector2(460f, detailsText.preferredHeight + 16f);
    }

    static void MakeDetails()
    {
        var panel = new GameObject($"{ObjectName}_details");
        panel.transform.SetParent(root.transform, false);
        details = panel.AddComponent<Image>();
        details.color = new Color(0.08f, 0.07f, 0.06f, 0.92f);
        details.raycastTarget = false;
        var text = new GameObject($"{ObjectName}_details_text");
        text.transform.SetParent(panel.transform, false);
        detailsText = text.AddComponent<TextMeshProUGUI>();
        // The font of a game's text that shows a capital К or В now: the move points' Roboto says it has them
        // (HasCharacter) but draws boxes for them.
        detailsText.font = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
            .FirstOrDefault(t => t.font != null && t.isActiveAndEnabled && !t.font.name.StartsWith("LiberationSans") && t.text != null && (t.text.Contains('К') || t.text.Contains('В')))?.font
            ?? number.font;
        Perf.Log?.Invoke($"FPS overlay: the details in '{detailsText.font?.name}'");
        detailsText.fontSize = 16f;
        detailsText.color = Color.white;
        detailsText.raycastTarget = false;
        detailsText.enableWordWrapping = false;
        var rect = detailsText.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 8f);
        rect.offsetMax = new Vector2(-10f, -8f);
        panel.SetActive(false);
    }

    // Under the players' list of the map's HUD (BhSideTimers, CanvasSideTimers: a timer per player), centred under its
    // last shown player; in the corner when the list is not on screen.
    static BhSideTimers players;

    static void PlaceUnderPlayers()
    {
        var rect = disc.rectTransform;
        if (players == null)
            players = Scene.Find<BhSideTimers>();
        var timers = players != null && players.gameObject.activeInHierarchy && players.bhTimer != null
            ? Enumerable.Range(0, players.bhTimer.Count).Select(i => players.bhTimer[i]).Where(t => t != null && t.gameObject.activeInHierarchy).ToList()
            : null;
        var canvas = players != null ? players.GetComponentInParent<Canvas>()?.rootCanvas : null;
        if (timers is not { Count: > 0 } || canvas == null)
        {
            Place(rect, new Vector2(12f, -12f), new Vector2(Size, Size));
            return;
        }
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        float bottom = float.MaxValue, left = 0f, right = 0f;
        foreach (var timer in timers)
        {
            var box = timer.transform.TryCast<RectTransform>();
            if (box == null)
                continue;
            box.GetWorldCorners(corners);
            var low = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var high = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            if (low.y < bottom)
                (bottom, left, right) = (low.y, low.x, high.x);
        }
        if (bottom == float.MaxValue)
            return;
        // Screen pixels to our canvas (top left anchored).
        var ours = root.transform.TryCast<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(ours, new Vector2((left + right) / 2f, bottom), null, out var local);
        var size = ours.rect.size;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(Size, Size);
        rect.anchoredPosition = new Vector2(local.x + size.x / 2f, local.y - size.y / 2f - 6f);
    }

    static void Make()
    {
        root = new GameObject(ObjectName);
        Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var circle = new GameObject($"{ObjectName}_disc");
        circle.transform.SetParent(root.transform, false);
        disc = circle.AddComponent<Image>();
        disc.sprite = Badges.Disc();
        disc.raycastTarget = false;
        Place(disc.rectTransform, new Vector2(12f, -12f), new Vector2(Size, Size));

        var text = new GameObject($"{ObjectName}_number");
        text.transform.SetParent(circle.transform, false);
        number = text.AddComponent<TextMeshProUGUI>();
        var game = MovePointsText();
        if (game != null)
        {
            number.font = game.font;
            number.fontSharedMaterial = game.fontSharedMaterial;
            number.fontStyle = game.fontStyle;
        }
        Perf.Log?.Invoke($"FPS overlay: the move points' text {(game != null ? $"'{game.font?.name}', material '{game.fontSharedMaterial?.name}'" : "not found, the default font")}");
        number.fontSize = Size * 0.5f;
        number.alignment = TextAlignmentOptions.Center;
        number.enableWordWrapping = false;
        number.raycastTarget = false;
        number.color = Color.white;
        var rect = number.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        MakeDetails();
    }

    static void Place(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = at;
        // (the pivot back in the corner after PlaceUnderPlayers)
        rect.sizeDelta = size;
    }

    // The text of the game's prefab for the move points along a hero's path.
    static TextMeshProUGUI MovePointsText() =>
        Resources.FindObjectsOfTypeAll<BhPathDrawerMovePoints>().Select(d => d.prefab).Where(p => p != null)
            .Select(p => p.GetComponentInChildren<TextMeshProUGUI>(true)).FirstOrDefault(t => t != null && t.font != null);
}
