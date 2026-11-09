using System;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// Badge icons drawn at runtime from distance functions (64x64, y up), so they don't depend on the game's sprites:
// dark plate, rounded frame, glyph strokes.
//
//   var sprite = Badges.Draw(Badges.Amber, (x, y) => Badges.Segment(x, y, 16, 32, 48, 32));
public static class Badges
{
    public static readonly Color Plate = new(0.11f, 0.09f, 0.07f, 0.92f), Gold = new(0.88f, 0.78f, 0.56f), Frame = new(0.79f, 0.69f, 0.48f),
                                 Green = new(0.45f, 0.80f, 0.30f), Amber = new(0.94f, 0.62f, 0.15f), Red = new(0.90f, 0.33f, 0.24f);

    // `glyph` is the distance from a pixel to the glyph's strokes; the frame in `frame` (the glyph's colour by default).
    public static Sprite Draw(Color color, Func<float, float, float> glyph, float frameWidth = 2.5f, float strokeWidth = 4f, Color? frame = null)
    {
        const int n = 64;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color[n * n];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            float px = x + 0.5f, py = y + 0.5f;
            var box = RoundBox(px, py, 32, 32, 28, 28, 10);
            var c = Paint(Plate, Plate.a * Fill(box));
            if (frameWidth > 0)
                c = Over(c, Paint(frame ?? color, Stroke(Math.Abs(box), frameWidth)));
            c = Over(c, Paint(color, Stroke(glyph(px, py), strokeWidth)));
            pixels[y * n + x] = c;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    // A round badge, 128x128: a white disc in a dark rim. Tinted (Image.color), the disc takes the colour, the rim
    // stays dark.
    static Sprite disc;

    public static Sprite Disc()
    {
        if (disc != null)
            return disc;
        const int n = 128;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color[n * n];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var d = Circle(x + 0.5f, y + 0.5f, 64, 64, 60);
            var c = Paint(Plate, Fill(d));
            pixels[y * n + x] = Over(c, Paint(Color.white, Fill(d + 7f)));
        }
        texture.SetPixels(pixels);
        texture.Apply();
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        disc = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        disc.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return disc;
    }

    // An empty badge, the plate alone without a frame, to put a picture on (a game icon).
    static readonly System.Collections.Generic.Dictionary<Color, Sprite> plates = new();

    public static Sprite Empty(Color frame)
    {
        if (!plates.TryGetValue(frame, out var sprite) || sprite == null)
            plates[frame] = sprite = Draw(frame, (_, _) => float.MaxValue, 0f);
        return sprite;
    }

    // A game sprite by name: Game.SpriteNamed (shared/Game.cs).

    // How much of its own square a sprite's picture takes (the larger side of its opaque part, 0..1), so pictures with
    // different margins can be drawn the same size: picture scale = wanted / Extent. Read back once per sprite through a
    // render texture (the game's textures are not readable); 1 when that fails.
    static readonly System.Collections.Generic.Dictionary<IntPtr, float> extents = new();

    public static float Extent(Sprite sprite)
    {
        if (sprite == null)
            return 1f;
        if (extents.TryGetValue(sprite.Pointer, out var known))
            return known;
        var extent = 1f;
        var texture = sprite.texture;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            int w = (int)sprite.rect.width, h = (int)sprite.rect.height;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(sprite.rect.x, sprite.rect.y, w, h), 0, 0);
            int left = w, right = -1, bottom = h, top = -1;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (copy.GetPixel(x, y).a < 0.5f)
                    continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                bottom = Math.Min(bottom, y); top = Math.Max(top, y);
            }
            UnityEngine.Object.Destroy(copy);
            if (right >= left)
                extent = Math.Max((right - left + 1f) / w, (top - bottom + 1f) / h);
        }
        catch (Exception) { }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
        return extents[sprite.Pointer] = extent;
    }

    public static float Min(float a, float b, float c) => Math.Min(a, Math.Min(b, c));

    // Signed distance to a circle and to a rounded box (half sizes hx, hy, corner radius r); distance to a segment.
    public static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

    public static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Math.Abs(x - cx) - (hx - r), qy = Math.Abs(y - cy) - (hy - r);
        var outside = Mathf.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0));
        return outside + Math.Min(Math.Max(qx, qy), 0) - r;
    }

    public static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        var t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
        float px = ax + t * dx - x, py = ay + t * dy - y;
        return Mathf.Sqrt(px * px + py * py);
    }

    // Coverage of a stroke of half-width w at distance d, and of a filled shape at signed distance d.
    static float Stroke(float d, float w) => Mathf.Clamp01(w + 0.5f - d);
    static float Fill(float d) => Mathf.Clamp01(0.5f - d);

    static Color Paint(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    // `top` drawn over `bottom`.
    static Color Over(Color bottom, Color top)
    {
        var a = top.a + bottom.a * (1 - top.a);
        if (a <= 0)
            return new Color(0, 0, 0, 0);
        var w = top.a / a;
        return new Color(top.r * w + bottom.r * (1 - w), top.g * w + bottom.g * (1 - w), top.b * w + bottom.b * (1 - w), a);
    }
}
