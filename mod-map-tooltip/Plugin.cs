using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Hex.Session.Hud;
using Hex.UI.Tooltips;
using OldenEra.Modding.Common;
using UnityEngine;

namespace OldenEra.Modding.MapTooltip;

// The tooltip of a map object (a squad, a building, a pickup) stands over the object and often covers it. The game
// shows it with BhTooltip.Show(Camera, GameObject, float), and every frame its positioner places it (wgf(Vector3) on
// 0.82.7): the object's point goes to the canvas (Camera.WorldToViewportPoint, times the canvas size, from its centre),
// then the positioner's own function gives the tooltip's place for that point (Game.TooltipPlaceForPoint: wdp on
// 0.82.7, the point lifted by the tooltip's height), the tooltip is set there and kept inside the screen.
//
// The plugin replaces that function with its version: the game's place, moved, when it covers the object or a HUD panel
// (the resources bar, the day panel, the heroes and towns lists), to the first free side: above, right, left, below.
// The move is chosen once per showing, on the first frame the tooltip is laid out, from where the game put it; after
// that the game places it as always, with the move, and keeps it on screen.
[BepInPlugin("nobirds.oldenera.maptooltip", "Olden Era Map Tooltip", "0.9.3")]
public partial class Plugin : ModBase
{
    Switch placeFree;

    const float Margin = 12f;  // screen pixels between the tooltip and what it must not cover, and to the screen's edges

    sealed class Over
    {
        public GameObject Target;
        public Camera Camera;
        public Renderer[] Meshes;    // the object's meshes, looked up once
        public bool Decided;         // the side was chosen (or the game's place kept)
        public Side? Side;           // the side moved to, or null: the game's place
        public Vector2 Move;         // the move, in canvas units
        public Vector2 FromPoint;    // the wanted place's corner minus the object's point, on screen
        public int Checks;           // frames the move was checked against where the tooltip came out
        public Vector2? LastFromPoint; // the tooltip's corner minus the object's point last frame
        public Vector2 LastSize;
    }

    readonly Dictionary<IntPtr, Over> shown = new();                    // by tooltip
    readonly Dictionary<IntPtr, IntPtr> tooltipOf = new();        // positioner -> tooltip
    const int MoveChecks = 5;

    protected override void OnLoad()
    {
        Game.Log = Logs.Info;
        placeFree = Menu.Toggle("PlaceFree", false);
        // The game's events (mod-core): the tooltip shown over its object, hidden, every frame, its place.
        On(ModEvents.TooltipShown, OnShown);
        On(ModEvents.TooltipHidden, OnHidden);
        On(ModEvents.TooltipLateUpdate, OnLateUpdate);
        On(ModEvents.TooltipPlace, OnPlace);
    }

    void OnShown(TooltipShown shownOver)
    {
        shown[shownOver.Tooltip.Pointer] = new Over { Target = shownOver.Target, Camera = shownOver.Camera };
        var positioner = shownOver.Tooltip.Positioner?.Pointer ?? IntPtr.Zero;
        if (positioner != IntPtr.Zero)
            tooltipOf[positioner] = shownOver.Tooltip.Pointer;
    }

    void OnHidden(BhTooltip tooltip)
    {
        shown.Remove(tooltip.Pointer);
        // The game makes a new positioner for each showing.
        foreach (var positioner in tooltipOf.Where(p => p.Value == tooltip.Pointer).Select(p => p.Key).ToList())
            tooltipOf.Remove(positioner);
    }

    // Our version of the game's place for the point: the game's own, moved to the chosen side.
    void OnPlace(TooltipPlace place)
    {
        if (tooltipOf.TryGetValue(place.Positioner.Pointer, out var tooltip) && shown.TryGetValue(tooltip, out var over) && over.Side != null && placeFree.On)
            place.Place += over.Move;
    }

    // On the first frame the tooltip is laid out: is the game's place free; if not, the move to the first free side.
    void OnLateUpdate(BhTooltip tooltip)
    {
        if (tooltip == null || !shown.TryGetValue(tooltip.Pointer, out var over) || over.Target == null || !placeFree.On || (over.Decided && (over.Side == null || over.Checks >= MoveChecks)))
            return;
        try
        {
            var camera = over.Camera != null ? over.Camera : Camera.main;
            // The visible box is tooltipRect_; thisRect_ is a holder of no size in the middle of the screen.
            var box = tooltip.tooltipRect_;
            var root = tooltip.transform.TryCast<RectTransform>();   // `as` is always null for Il2Cpp objects
            var canvas = box != null ? box.GetComponentInParent<Canvas>()?.rootCanvas : null;
            var canvasRect = canvas != null ? canvas.transform.TryCast<RectTransform>() : null;
            if (camera == null || box == null || root == null || canvasRect == null || box.rect.width <= 1f || ObjectOnScreen(over, camera) is not { } obj)
                return;   // not laid out yet
            var tip = TooltipOnScreen(root, box, Scene.UiCamera(canvas));
            // Screen pixels to canvas units (the canvas covers the screen).
            var units = new Vector2(canvasRect.rect.width / Screen.width, canvasRect.rect.height / Screen.height);
            if (camera.WorldToScreenPoint(over.Target.transform.position) is not { z: > 0 } point)
                return;
            // Measured only at rest: while it appears the game animates it.
            var fromPoint = tip.min - (Vector2)point;
            var resting = over.LastFromPoint is { } last && (fromPoint - last).sqrMagnitude < 0.25f && (tip.size - over.LastSize).sqrMagnitude < 0.25f;
            over.LastFromPoint = fromPoint;
            over.LastSize = tip.size;
            if (!resting)
                return;
            if (over.Decided)
            {
                // Where the game kept it on screen (pushed in from an edge), the move came out short: made up for. The
                // wanted place is inside the screen, so after that the game does not push it any more.
                var error = (Vector2)point + over.FromPoint - tip.min;
                over.Checks = error.sqrMagnitude < 1f ? MoveChecks : over.Checks + 1;
                over.Move += Vector2.Scale(error, units);
                return;
            }
            over.Decided = true;
            var blocked = Hud().Append(obj).ToList();
            bool Free(Rect r) => !blocked.Any(b => Grown(b).Overlaps(r)) && r.xMin >= 0 && r.yMin >= 0 && r.xMax <= Screen.width && r.yMax <= Screen.height;
            if (Free(tip))
                return;   // where the game puts it, nothing is covered
            var screen = new Rect(Margin, Margin, Screen.width - 2 * Margin, Screen.height - 2 * Margin);
            foreach (var side in Sides)
            {
                var place = Fit(Candidate(side, obj, tip.size), screen);
                if (!Free(place))
                    continue;
                over.Side = side;
                over.FromPoint = place.min - (Vector2)point;
                over.Move = Vector2.Scale(place.min - tip.min, units);
                Logs.Info($"{over.Target.name}: {side.ToString().ToLowerInvariant()}, moved by {over.Move.x:0}, {over.Move.y:0}");
                return;
            }
        }
        catch (Exception e)
        {
            Logs.Report("place", e);
        }
    }

    // The sides of the object a tooltip may go to, in the order they are tried (fogone): above, as the game puts it,
    // then right, left, below.
    enum Side { Above, Right, Left, Below }
    static readonly Side[] Sides = Enum.GetValues<Side>();

    // Where the tooltip goes on a side of the object, on screen.
    static Rect Candidate(Side side, Rect obj, Vector2 size)
    {
        float w = size.x, h = size.y;
        return side switch
        {
            Side.Right => new Rect(obj.xMax + Margin, obj.center.y - h / 2, w, h),
            Side.Left => new Rect(obj.xMin - Margin - w, obj.center.y - h / 2, w, h),
            Side.Below => new Rect(obj.center.x - w / 2, obj.yMin - Margin - h, w, h),
            _ => new Rect(obj.center.x - w / 2, obj.yMax + Margin, w, h),   // above
        };
    }

    static Rect Grown(Rect r) => new(r.xMin - Margin, r.yMin - Margin, r.width + 2 * Margin, r.height + 2 * Margin);

    // Kept on screen along the side, as the game keeps the tooltip; may cover something then, which the caller checks.
    static Rect Fit(Rect r, Rect screen)
    {
        var x = Mathf.Clamp(r.x, screen.xMin, Mathf.Max(screen.xMin, screen.xMax - r.width));
        var y = Mathf.Clamp(r.y, screen.yMin, Mathf.Max(screen.yMin, screen.yMax - r.height));
        return new Rect(x, y, r.width, r.height);
    }



    // The box with what our mods hang on the tooltip beside it (children of its root named OE_..., the visit badges).
    static Rect TooltipOnScreen(RectTransform root, RectTransform box, Camera uiCamera)
    {
        var r = Scene.OnScreen(box, uiCamera);
        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.gameObject.activeSelf && child.name.StartsWith("OE_") && child.TryCast<RectTransform>() is { } extra && extra.rect.width > 1f)
            {
                var e = Scene.OnScreen(extra, uiCamera);
                r = Rect.MinMaxRect(Mathf.Min(r.xMin, e.xMin), Mathf.Min(r.yMin, e.yMin), Mathf.Max(r.xMax, e.xMax), Mathf.Max(r.yMax, e.yMax));
            }
        }
        return r;
    }

    // The HUD panels a tooltip must not cover: the resources bar's elements, the day panel, the heroes and towns lists.
    // Panels as large as half the screen are screens, not panels, and are left out. The panels are found
    // once (Hud), their places at most twice a second.
    List<Rect> hud = new();
    List<(RectTransform Rect, Camera UiCamera)> hudPanels = new();
    int nextHud;

    // The HUD's own scene (its panels' canvases): its objects are found there, not among every loaded one (200 ms).
    const string HudScene = "sessionHUD";

    List<Rect> Hud()
    {
        if (Time.frameCount < nextHud)
            return hud;
        nextHud = Time.frameCount + 30;
        // Found once, in the HUD scene only (Shown; through every loaded object it took 200 ms); again only when the
        // game has destroyed one of them (it remakes the resource elements) or a new map's HUD came.
        if (hudPanels.Count == 0 || hudPanels.Any(p => p.Rect == null))
        {
            var panels = new List<(RectTransform, Camera)>();
            void Add(IEnumerable<Component> found)
            {
                foreach (var panel in found)
                    if (panel != null && panel.gameObject.scene.IsValid() && panel.transform.TryCast<RectTransform>() is { } rect)
                    {
                        var canvas = rect.GetComponentInParent<Canvas>(true)?.rootCanvas;
                        panels.Add((rect, canvas != null ? Scene.UiCamera(canvas) : null));
                    }
            }
            Add(Scene.Shown<BhResElement>(HudScene));
            Add(Scene.Shown<BhDayPanel>(HudScene));
            Add(Scene.Shown<BhDayPanelV2>(HudScene));
            Add(Scene.Shown<BhSideHeroesPanel>(HudScene));
            Add(Scene.Shown<BhSideCitiesPanel>(HudScene));
            hudPanels = panels;
        }
        var places = new List<Rect>();
        foreach (var (rect, uiCamera) in hudPanels)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
                continue;
            var r = Scene.OnScreen(rect, uiCamera);
            if (r.width > 1f && r.height > 1f && r.width * r.height < Screen.width * Screen.height / 2f)
                places.Add(r);
        }
        return hud = places;
    }


    // The object's meshes on screen (effects reach far above models and are left out); null when behind the camera.
    static Rect? ObjectOnScreen(Over over, Camera camera)
    {
        over.Meshes ??= over.Target.GetComponentsInChildren<Renderer>(false)
            .Where(r => r.enabled && (r.TryCast<MeshRenderer>() != null || r.TryCast<SkinnedMeshRenderer>() != null) && r.bounds.size.sqrMagnitude > 0.0001f)
            .ToArray();
        var renderers = over.Meshes.Where(r => r != null).ToArray();
        if (renderers.Length == 0)
            return null;
        var bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            var p = camera.WorldToScreenPoint(corner);
            if (p.z <= 0)
                return null;
            xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
            yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

}
