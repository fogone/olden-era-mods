using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Common;

// Pictures from files (PNG, JPG) in the UI: loaded once into textures, shown in a RawImage filling its frame ("cover",
// cut) or whole in an area ("contain"), the picture's aspect kept either way.
//
//   var pictures = new PictureCache(error => Logs.Once(error));
//   Pictures.Cover(rawImage, pictures.Get(path));
//   Pictures.FitInside(rawImage.rectTransform, texture, new Vector2(400, 300));
//   pictures.Clear();   // on unload: the textures destroyed
public sealed class PictureCache
{
    readonly Dictionary<string, Texture2D> textures = new();
    readonly Action<string> error;

    public PictureCache(Action<string> error = null) => this.error = error;

    // The file's texture, loaded at the first ask; null for no path or a file that is not a picture (asked once).
    public Texture2D Get(string path)
    {
        if (path == null)
            return null;
        if (textures.TryGetValue(path, out var known))
            return known;
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
        }
        catch (Exception e)
        {
            error?.Invoke($"picture {path}: {e.Message}");
            texture = null;
        }
        textures[path] = texture;
        return texture;
    }

    public void Clear()
    {
        foreach (var texture in textures.Values)
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        textures.Clear();
    }
}

public static class Pictures
{
    // A picture filling the image's frame, its aspect kept: cut at the sides or at the top and bottom, centred. No
    // picture (not loaded): a dim frame.
    public static void Cover(RawImage image, Texture2D texture)
    {
        image.texture = texture;
        image.color = texture != null ? Color.white : new Color(0f, 0f, 0f, 0.35f);
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        var frame = image.rectTransform.rect;
        if (texture == null || texture.height == 0 || frame.width <= 0f || frame.height <= 0f)
            return;
        var frameAspect = frame.width / frame.height;
        var aspect = (float)texture.width / texture.height;
        image.uvRect = aspect > frameAspect
            ? new Rect(0.5f - frameAspect / aspect / 2f, 0f, frameAspect / aspect, 1f)
            : new Rect(0f, 0.5f - aspect / frameAspect / 2f, 1f, aspect / frameAspect);
    }

    // A rect sized to show a picture whole in an area, its aspect kept, centred on its parent (`fallback`: the aspect
    // with no picture).
    public static void FitInside(RectTransform rect, Texture2D texture, Vector2 area, float fallback = 1f)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        var aspect = texture != null && texture.height > 0 ? (float)texture.width / texture.height : fallback;
        var width = Math.Min(area.x, area.y * aspect);
        rect.sizeDelta = new Vector2(width, width / aspect);
    }
}
