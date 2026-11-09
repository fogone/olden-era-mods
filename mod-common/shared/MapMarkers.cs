using System;
using System.Collections.Generic;
using System.Linq;
using Hex.Session.Data;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// Badges over map objects, made the way the game shows its campaign quest marks: an object in the world next to
// the object's model, at the model's quest anchor (a child named quest_point, quest_marker_point on squads). So a
// badge stays on its object, goes behind what is in front of it and is switched off with the map section the game
// switches off. A plugin says every so often which models get which icons (Set) and turns them to the camera every
// frame (Face); the badges are named after the plugin's object prefix, so ModBase removes them on unload.
//
//   var markers = new MapMarkers("OE_Visit");
//   markers.Set(new() { [model.Pointer] = new Marker(model, new[] { new MarkerIcon("available", sprite) }, 0.5f, 0.7f) });
//   OnTick(markers.Face);
public sealed class MapMarkers
{
    readonly string name;
    readonly Dictionary<IntPtr, (Transform root, string look)> shown = new();

    public MapMarkers(string name) => this.name = name;

    bool changed;

    public void Set(Dictionary<IntPtr, Marker> wanted)
    {
        changed = true;
        foreach (var key in shown.Keys.ToList())
        {
            var (root, look) = shown[key];
            if (root != null && wanted.TryGetValue(key, out var want) && want.Look == look)
                continue;
            if (root != null)
            {
                anchors.Remove(root.Pointer);
                UnityEngine.Object.Destroy(root.gameObject);
            }
            shown.Remove(key);
        }
        foreach (var (key, marker) in wanted)
        {
            if (!shown.TryGetValue(key, out var entry))
                shown[key] = entry = (Make(marker), marker.Look);
            if (!entry.root.gameObject.activeSelf)
                entry.root.gameObject.SetActive(true);
        }
    }

    // Every frame: the badges face the camera and stand a little in front of their object, towards the camera along the
    // line of sight, so they stay where they are on screen but a long model does not cover them.
    const float Pull = 2.5f;   // world units
    readonly Dictionary<IntPtr, (Vector3 at, Vector3 scale)> anchors = new();

    // Only when the camera has moved, or the badges changed (Set): they keep their place otherwise.
    Vector3 facedFrom;
    Quaternion facedTo;
    int facedCount = -1;

    public void Face()
    {
        if (Camera.main == null)
            return;
        var camera = Camera.main.transform;
        var rotation = camera.rotation;
        // Also when a map section with badges is switched on or off (the badges seen change).
        var seen = 0;
        foreach (var (root, _) in shown.Values)
            seen += root != null && root.gameObject.activeInHierarchy ? 1 : 0;
        if (camera.position == facedFrom && rotation == facedTo && seen == facedCount && !changed)
            return;
        facedFrom = camera.position;
        facedTo = rotation;
        facedCount = seen;
        changed = false;
        foreach (var (root, _) in shown.Values)
        {
            if (root == null || !root.gameObject.activeInHierarchy)
                continue;
            root.rotation = rotation;
            if (anchors.TryGetValue(root.Pointer, out var anchor))
            {
                // Closer to the camera looks bigger: scaled down by as much, the badge keeps its size on screen.
                var toCamera = camera.position - anchor.at;
                var distance = toCamera.magnitude;
                var pull = Mathf.Min(Pull, distance * 0.5f);
                root.position = anchor.at + toCamera.normalized * pull;
                root.localScale = anchor.scale * (distance > 0.01f ? (distance - pull) / distance : 1f);
            }
        }
    }

    // ---------- the badges' life on the map, the same for every mod ----------

    // Made again every ScanEvery frames while shown; when shown again (the key pressed) first as they were, the scan a
    // few frames later (a scan costs tens of ms, not in the frame of the key).
    const int ScanEvery = 30, ScanAfterShown = 10;
    int nextScan;
    bool hidden = true;

    // Every frame: `on` whether the mod's badges are switched on at all, `when` always or with the game's alt info,
    // `scan` makes them (Set); its error is reported once.
    public void Update(bool on, MarkersShown when, Action scan, ModLog log)
    {
        if (!on || !Wanted(when))
        {
            Hide();
            hidden = true;
            return;
        }
        if (hidden)
        {
            hidden = false;
            nextScan = Math.Max(nextScan, Time.frameCount + ScanAfterShown);
        }
        Show();
        if (Time.frameCount >= nextScan)
        {
            nextScan = Time.frameCount + ScanEvery;
            log.Guard("map badges", scan);
        }
        Face();
    }

    // Ready before they are first shown (a hero selected on the map, ModEvents.HeroSelected): one scan, the badges drawn
    // invisibly for a few frames (Prewarm), so their first showing only turns them on. Making them all in the frame the
    // key was pressed stalled the game.
    public void Prepare(bool on, MarkersShown when, Action scan, ModLog log)
    {
        if (!on || when == MarkersShown.Never)
            return;
        log.Guard("map badges", scan);
        if (!Wanted(when))
            Prewarm();
        nextScan = Time.frameCount + ScanEvery;
    }

    // Shown again after Hide, without waiting for the next Set.
    public void Show()
    {
        if (faded.Count > 0)
        {
            Unfade();
            prewarmFrames = 0;
        }
        foreach (var (root, _) in shown.Values)
            if (root != null && !root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
                changed = true;
            }
    }

    // Whether the badges are to be seen now: always, or while the game's "alt info" action is held (Alt by default,
    // whatever key it is bound to), when the game shows its own extra info. The game's own flag of that mode:
    // BhPathDrawer.ShowMovePointsTooltip, on exactly while the key is held. Never: not even then.
    public static bool Wanted(MarkersShown when) => when switch
    {
        MarkersShown.Always => true,
        MarkersShown.Alt => Hex.Session.World.BhPathDrawer.ShowMovePointsTooltip,
        _ => false,
    };

    // The battle danger of a level as a badge: the game's skulls widget's lit skull in the level's colour (BhSkulls.umq).
    public static MarkerIcon Danger(Hex.UI.BhSkulls skulls, int level, bool corner = false) =>
        new($"danger:{level}", skulls.skullActive, skulls.DangerColor(level), corner: corner);

    public void Hide()
    {
        // Drawn invisibly for a few frames first (Prewarm): the game's first drawing of them is done then.
        if (prewarmFrames > 0 && --prewarmFrames > 0)
            return;
        if (faded.Count > 0)
            Unfade();
        foreach (var (root, _) in shown.Values)
            if (root != null && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
    }

    // The badges drawn once while nobody sees them (fully transparent, for a few frames), then hidden: the first time
    // the game draws new pictures it uploads them and readies their drawing, which stalled the first showing for most of
    // a second (05.11: 765 ms of an 818 ms frame were the game's). For a mod's preparation (on the hero selected).
    const int PrewarmFrames = 3;
    int prewarmFrames;
    readonly List<Action> faded = new();   // each puts a faded picture's or caption's colour back

    public void Prewarm()
    {
        Unfade();
        foreach (var (root, _) in shown.Values)
        {
            if (root == null)
                continue;
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var color = renderer.color;
                faded.Add(() => { if (renderer != null) renderer.color = color; });
                renderer.color = new Color(color.r, color.g, color.b, 0f);
            }
            foreach (var caption in root.GetComponentsInChildren<TextMeshPro>(true))
            {
                var alpha = caption.alpha;
                faded.Add(() => { if (caption != null) caption.alpha = alpha; });
                caption.alpha = 0f;
            }
            if (!root.gameObject.activeSelf)
                root.gameObject.SetActive(true);
        }
        prewarmFrames = PrewarmFrames;
        changed = true;
    }

    void Unfade()
    {
        foreach (var restore in faded)
            restore();
        faded.Clear();
    }

    // A row of icons of one size, centred above the anchor.
    Transform Make(Marker marker)
    {
        var model = marker.Model;
        var go = new GameObject(name);
        go.layer = model.layer;
        // Next to the model, not in it: models are scaled and some are mirrored. The holder is in the map section.
        var holder = model.transform.parent != null ? model.transform.parent : model.transform;
        go.transform.SetParent(holder, false);
        go.transform.position = AnchorOf(model, marker.OverMesh) + Vector3.up * marker.Lift;
        var parent = holder.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Abs(parent.x), 1f / Mathf.Abs(parent.y), 1f / Mathf.Abs(parent.z));
        anchors[go.transform.Pointer] = (go.transform.position, go.transform.localScale);
        // The row, and the corner icons over the bottom right corner of its first icon.
        var row = marker.Icons.Where(i => !i.Corner).ToList();
        var corner = marker.Icons.Where(i => i.Corner).ToList();
        if (row.Count == 0)
        {
            row = corner;
            corner = new List<MarkerIcon>();
        }
        var first = (row.Count - 1) / -2f * marker.Size * 1.05f;
        for (var i = 0; i < corner.Count; i++)
        {
            var small = marker.CornerSize;
            var at = new Vector3(first + marker.Size * 0.42f - i * small, -marker.Size * 0.42f, -0.01f);
            Icon(go.transform, corner[i], small, at, 3, model.layer);
        }
        for (var i = 0; i < row.Count; i++)
            Icon(go.transform, row[i], marker.Size, new Vector3(first + i * marker.Size * 1.05f, 0f, 0f), 0, model.layer);
        return go.transform;
    }

    void Icon(Transform parent, MarkerIcon icon, float size, Vector3 at, int order, int layer)
    {
        var holder = new GameObject($"{name}_{icon.Key}");
        holder.layer = layer;
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = at;
        // A card (a unit portrait the way the game draws it): solid back, the picture on it, the frame on top.
        var picture = icon.Background != null ? size * icon.PictureScale : size * icon.Scale;
        if (icon.Background != null)
            Sprite(holder.transform, $"{name}_back", icon.Background, icon.Color, size, order, layer);
        Sprite(holder.transform, $"{name}_picture", icon.Sprite, icon.Color, picture, order + 1, layer);
        if (icon.Overlay != null)
            Sprite(holder.transform, $"{name}_frame", icon.Overlay, Color.white, size, order + 2, layer);
        if (!string.IsNullOrEmpty(icon.Caption))
            Caption(holder.transform, icon.Caption, size, order + 3, layer);
    }

    // A caption in the bottom right corner of an icon (a count, the way the game writes it on its unit cards), in the
    // font the game writes the move points along a hero's path with.
    void Caption(Transform parent, string text, float size, int order, int layer)
    {
        var go = new GameObject($"{name}_caption");
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(size * 0.2f, -size * 0.36f, -0.02f);
        var caption = go.AddComponent<TextMeshPro>();
        var (font, material) = Font();
        if (font != null)
            caption.font = font;
        if (material != null)
            caption.fontSharedMaterial = material;
        caption.text = text;
        caption.alignment = TextAlignmentOptions.Right;
        // A fixed size: auto-sizing in a box this small gives glyphs a tenth of the icon high.
        caption.enableAutoSizing = false;
        caption.fontSize = size * 4f;
        caption.enableWordWrapping = false;
        caption.overflowMode = TextOverflowModes.Overflow;
        caption.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.3f);
        caption.color = Color.white;
        caption.outlineWidth = 0.3f;
        caption.outlineColor = new Color32(0, 0, 0, 255);
        caption.renderer.sortingOrder = order;
    }

    // The font and material of the move points the game writes along a hero's path (LT-Regular), else any text's font.
    static TMP_FontAsset font;
    static Material fontMaterial;

    static (TMP_FontAsset, Material) Font()
    {
        if (font == null || !font.name.StartsWith("LT-Regular"))
        {
            var texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            var path = texts.FirstOrDefault(t => t.font != null && t.font.name.StartsWith("LT-Regular"));
            font = path?.font ?? texts.Select(t => t.font).FirstOrDefault(f => f != null);
            fontMaterial = path?.fontSharedMaterial;
        }
        return (font, fontMaterial);
    }

    static void Sprite(Transform parent, string name, Sprite image, Color color, float size, int order, int layer)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = image;
        sprite.color = color;
        sprite.sortingOrder = order;
        var bounds = image != null ? image.bounds.size : Vector3.one;
        var scale = size / Mathf.Max(Mathf.Max(bounds.x, bounds.y), 0.01f);
        go.transform.localScale = new Vector3(scale, scale, scale);
    }

    // Where the game puts its quest mark: the model's quest_point (quest_marker_point on squads); else the top of
    // the model's meshes (effects reach high above it, switched-off ones have empty bounds at the world's origin).
    public static Vector3 AnchorOf(GameObject model, bool overMesh = false)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
            if (!overMesh && t.name.StartsWith("quest_", StringComparison.OrdinalIgnoreCase) && t.name.EndsWith("point", StringComparison.OrdinalIgnoreCase))
                return t.position;
        var renderers = model.GetComponentsInChildren<Renderer>(true)
                             .Where(r => r.enabled && (r.TryCast<MeshRenderer>() != null || r.TryCast<SkinnedMeshRenderer>() != null) && r.bounds.size.sqrMagnitude > 0.0001f)
                             .ToArray();
        if (renderers.Length == 0)
            return model.transform.position;
        var bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    // Whether the side has scouted a map cell: the game's own check of its fog (DataFogOfWar bool M(int cell), bkin on
    // 0.82.7: true while the cell is in the fog). A squad's cell is its DataSquad.node.
    public static bool Scouted(DataFogOfWar fog, int cell) =>
        cell >= 0 && cell < fog.sizeX * fog.sizeZ && !fog.InFog(cell);

    // The cell under a world point, as the game numbers them: rows along x, the world's origin in the middle of the map
    // (x + sizeX/2 + (z + sizeZ/2) * sizeX; checked against DataSquad.node on 0.82.7). For what has no node of its own.
    public static bool Scouted(DataFogOfWar fog, Vector3 world)
    {
        int x = Mathf.FloorToInt(world.x + fog.sizeX / 2f), z = Mathf.FloorToInt(world.z + fog.sizeZ / 2f);
        return x >= 0 && z >= 0 && x < fog.sizeX && z < fog.sizeZ && Scouted(fog, x + z * fog.sizeX);
    }
}

// When a mod's map badges are seen; the menu's options (lang "<mod>.MarkersShown.0 .. .2") in this order.
public enum MarkersShown { Always, Alt, Never }

// What a badge shows: one or more icons over a model, `size` (one icon) and `lift` in world units.
public sealed class Marker
{
    public readonly GameObject Model;
    public readonly IReadOnlyList<MarkerIcon> Icons;
    public readonly float Size, Lift, CornerSize;
    public readonly bool OverMesh;   // above the model's meshes, not at its quest point (an artifact's is high above it)
    internal readonly string Look;

    public Marker(GameObject model, IReadOnlyList<MarkerIcon> icons, float size, float lift, float cornerSize = 0f, bool overMesh = false)
    {
        Model = model;
        OverMesh = overMesh;
        Icons = icons;
        Size = size;
        Lift = lift;
        CornerSize = cornerSize > 0f ? cornerSize : size * 0.45f;
        Look = $"{size}:{lift}:{CornerSize}:{string.Join(",", icons.Select(i => $"{i.Key}/{i.Color}/{i.Background != null}/{i.Corner}/{i.Caption}"))}";
    }
}

public readonly struct MarkerIcon
{
    public readonly string Key;
    public readonly Sprite Sprite;
    public readonly Color Color;
    public readonly Sprite Background, Overlay;   // a card's back under the picture and frame over it, or null
    public readonly float PictureScale;           // the picture's size on a card, to the card's
    public readonly bool Corner;                  // small, over the bottom right corner of the row's first icon
    public readonly string Caption;               // text in the icon's bottom right corner, or null
    public readonly float Scale;                  // a picture without a card: its size to the icon's (to even out margins)

    public MarkerIcon(string key, Sprite sprite) : this(key, sprite, Color.white) { }

    public MarkerIcon(MarkerIcon icon, float scale) : this(icon.Key, icon.Sprite, icon.Color, icon.Background, icon.Overlay, icon.PictureScale, icon.Corner, icon.Caption) =>
        Scale = scale;

    public MarkerIcon(string key, Sprite sprite, Color color, Sprite background = null, Sprite overlay = null, float pictureScale = 0.9f, bool corner = false, string caption = null)
    {
        Key = key;
        Sprite = sprite;
        Color = color;
        Background = background;
        Overlay = overlay;
        PictureScale = pictureScale;
        Corner = corner;
        Caption = caption;
        Scale = 1f;
    }
}
