using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SkiaSharp;

namespace Nobirds.OldenEra.Modding.Bundle;

// A town's ground as the game's towns have it: Unity's Terrain (a heightmap, a splat map mixing four textures), drawn
// with the game's terrain material. The terrain is written into the scene (a GameObject with Terrain and
// TerrainCollider, its TerrainData and layers in the scene's shared assets); the game's material and textures are not:
// the terrain's material is named after the game's, each layer after the game's texture it stands for, and the mod
// library puts the game's in when the scene is loaded (ModScenes.Terrains).
sealed class TerrainSpec
{
    // The heights: Unity's RAW (16-bit little-endian, rows from z = 0, the resolution's square: 2^n + 1).
    public string Heights { get; set; }
    // The splat map (RGBA: the weights of the material's four textures), (resolution - 1) square.
    public string Splat { get; set; }
    public float[] Size { get; set; }
    public float[] Position { get; set; } = { 0, 0, 0 };
    public string Material { get; set; }
    public LayerSpec[] Layers { get; set; }

    // A layer: the game's texture by name, or an image of the mod's (written into the bundle, repeating), and how
    // far apart it repeats.
    public sealed class LayerSpec
    {
        public string Texture { get; set; }
        public string Image { get; set; }
        public float Tile { get; set; } = 2;
    }
}

sealed partial class CityScene
{
    const int TerrainDataId = 156, TerrainId = 218, TerrainColliderId = 154, TerrainLayerId = 1953259897;
    const int PatchSize = 17;

    void AddTerrain(TerrainSpec spec, uint layer)
    {
        foreach (var (file, id) in new[] { (scene, TerrainId), (scene, TerrainColliderId), (shared, TerrainDataId), (shared, TerrainLayerId) })
            if (file.file.Metadata.FindTypeTreeTypeByID(id) == null)
                file.file.Metadata.TypeTreeTypes.Add(TypeTrees.Unity(id));

        var raw = File.ReadAllBytes(spec.Heights);
        var resolution = (int)Math.Round(Math.Sqrt(raw.Length / 2));
        if (resolution * resolution * 2 != raw.Length || ((resolution - 1) & (resolution - 2)) != 0)
            throw new Exception($"terrain.heights: {raw.Length} bytes are no square of 2^n + 1 16-bit heights");
        var levels = (int)Math.Round(Math.Log2((resolution - 1) / (PatchSize - 1)));
        var patches = Enumerable.Range(0, levels + 1).Sum(l => 1 << (2 * l));

        // The splat map: a texture of the shared assets, made from one of them (the models' bundles bring one).
        long splat = nextShared++;
        CloneShared(AssetClassID.Texture2D, splat, v => WriteTexture(v, spec.Splat, "SplatAlpha 0", resolution - 1, linear: true, repeat: false));
        var images = new List<long>();

        var layerIds = new List<long>();
        foreach (var l in spec.Layers ?? throw new Exception("terrain.layers: one per texture of the material (four)"))
        {
            long id = nextShared++, image = 0;
            layerIds.Add(id);
            if (l.Image != null)
            {
                image = nextShared++;
                images.Add(image);
                CloneShared(AssetClassID.Texture2D, image, v => WriteTexture(v, l.Image, Path.GetFileNameWithoutExtension(l.Image), 0, linear: false, repeat: true));
            }
            New(shared, id, TerrainLayerId, 0xFFFF, v =>
            {
                v["m_Name"].AsString = l.Texture ?? Path.GetFileNameWithoutExtension(l.Image);
                v["m_DiffuseTexture.m_PathID"].AsLong = image;
                v["m_TileSize.x"].AsFloat = v["m_TileSize.y"].AsFloat = l.Tile;
                foreach (var c in "xyzw")
                    v["m_DiffuseRemapMax"][c.ToString()].AsFloat = v["m_MaskMapRemapMax"][c.ToString()].AsFloat = 1;
                v["m_NormalScale"].AsFloat = 1;
            });
        }

        long data = nextShared++;
        New(shared, data, TerrainDataId, 0xFFFF, v =>
        {
            v["m_Name"].AsString = "Terrain";
            var s = v["m_SplatDatabase"];
            foreach (var id in layerIds)
                Add(s["m_TerrainLayers.Array"], e => (e["m_FileID"].AsInt, e["m_PathID"].AsLong) = (0, id));
            Add(s["m_AlphaTextures.Array"], e => (e["m_FileID"].AsInt, e["m_PathID"].AsLong) = (0, splat));
            s["m_AlphamapResolution"].AsInt = resolution - 1;
            s["m_BaseMapResolution"].AsInt = 1024;
            var d = v["m_DetailDatabase"];
            d["m_PatchSamples"].AsInt = 16;
            foreach (var c in "rgba")
                d["WavingGrassTint"][c.ToString()].AsFloat = 0.7f;
            d["m_WavingGrassStrength"].AsFloat = d["m_WavingGrassAmount"].AsFloat = d["m_WavingGrassSpeed"].AsFloat = 0.5f;
            var h = v["m_Heightmap"];
            var heights = h["m_Heights.Array"];
            for (var i = 0; i < resolution * resolution; i++)
            {
                var value = BitConverter.ToUInt16(raw, i * 2);
                Add(heights, e => e.AsShort = (short)Math.Round(value / 65535.0 * 32766));
            }
            // What Unity works out of the heights to draw them by: an error per patch of every level and its heights'
            // range, written so that every patch is drawn at full detail and none is culled.
            for (var i = 0; i < patches; i++)
            {
                Add(h["m_PrecomputedError.Array"], e => e.AsFloat = 1);
                Add(h["m_MinMaxPatchHeights.Array"], e => e.AsFloat = 0);
                Add(h["m_MinMaxPatchHeights.Array"], e => e.AsFloat = 1);
            }
            h["m_EnableHolesTextureCompression"].AsBool = true;
            h["m_Resolution"].AsInt = resolution;
            h["m_Levels"].AsInt = levels;
            h["m_Scale.x"].AsFloat = spec.Size[0] / (resolution - 1);
            h["m_Scale.y"].AsFloat = spec.Size[1];
            h["m_Scale.z"].AsFloat = spec.Size[2] / (resolution - 1);
        });

        // The material: named after the game's (one of the shared materials made a placeholder of no shader).
        long material = nextShared++;
        CloneShared(AssetClassID.Material, material, v =>
        {
            v["m_Name"].AsString = spec.Material;
            v["m_Shader.m_FileID"].AsInt = 0;
            v["m_Shader.m_PathID"].AsLong = 0;
            foreach (var list in new[] { "m_TexEnvs", "m_Ints", "m_Floats", "m_Colors" })
                if (!v["m_SavedProperties"][list].IsDummy)
                    v["m_SavedProperties"][list]["Array"].Children.Clear();
        });

        long go = nextScene++, transform = nextScene++, terrain = nextScene++, collider = nextScene++;
        GameObject(go, "Terrain", layer, transform, terrain, collider);
        Transform(transform, go, 0, spec.Position);
        New(scene, terrain, TerrainId, 0xFFFF, v =>
        {
            v["m_GameObject.m_PathID"].AsLong = go;
            v["m_Enabled"].AsByte = 1;
            (v["m_TerrainData.m_FileID"].AsInt, v["m_TerrainData.m_PathID"].AsLong) = (sharedFileId, data);
            v["m_TreeDistance"].AsFloat = 5000;
            v["m_TreeBillboardDistance"].AsFloat = 50;
            v["m_TreeCrossFadeLength"].AsFloat = 5;
            v["m_TreeMaximumFullLODCount"].AsInt = 50;
            v["m_DetailObjectDistance"].AsFloat = 80;
            v["m_DetailObjectDensity"].AsFloat = 1;
            v["m_HeightmapPixelError"].AsFloat = 1;
            v["m_SplatMapDistance"].AsFloat = 1000;
            v["m_ShadowCastingMode"].AsInt = 1;
            v["m_DrawHeightmap"].AsBool = true;
            v["m_DrawTreesAndFoliage"].AsBool = true;
            v["m_ReflectionProbeUsage"].AsInt = 1;
            (v["m_MaterialTemplate.m_FileID"].AsInt, v["m_MaterialTemplate.m_PathID"].AsLong) = (sharedFileId, material);
            v["m_LightmapIndex"].AsUShort = v["m_LightmapIndexDynamic"].AsUShort = 0xFFFF;
            foreach (var st in new[] { "m_LightmapTilingOffset", "m_LightmapTilingOffsetDynamic", "m_DynamicUVST", "m_ChunkDynamicUVST" })
                v[st]["x"].AsFloat = v[st]["y"].AsFloat = 1;
            v["m_RenderingLayerMask"].AsUInt = 1;
            v["m_AllowAutoConnect"].AsBool = true;
        });
        New(scene, collider, TerrainColliderId, 0xFFFF, v =>
        {
            v["m_GameObject.m_PathID"].AsLong = go;
            v["m_Enabled"].AsBool = true;
            (v["m_TerrainData.m_FileID"].AsInt, v["m_TerrainData.m_PathID"].AsLong) = (sharedFileId, data);
        });
        Preload(new[] { splat, data, material }.Concat(layerIds).Concat(images).ToArray());
        Console.WriteLine($"  terrain: {resolution}x{resolution} heights, {string.Join(" x ", spec.Size)}, {spec.Material}: {string.Join(", ", spec.Layers.Select(l => l.Texture ?? l.Image))}");
    }

    static void Add(AssetTypeValueField array, Action<AssetTypeValueField> fill)
    {
        var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
        fill(e);
        array.Children.Add(e);
    }

    // A new shared asset made of one of the shared assets of the type, changed.
    void CloneShared(AssetClassID type, long id, Action<AssetTypeValueField> change)
    {
        var source = shared.file.GetAssetsOfType(type).FirstOrDefault()
            ?? throw new Exception($"terrain: the scene's shared assets have no {type} to make one of (put a model into the scene)");
        var v = am.GetBaseField(shared, source);
        change(v);
        var info = AssetFileInfo.Create(shared.file, id, (int)type, 0xFFFF, null, false);
        info.SetNewData(v);
        shared.file.Metadata.AddAssetInfo(info);
    }

    // A texture of an image: RGBA32 with its mipmaps, the rows as the image has them (its first row is v = 0: for the
    // splat map z = 0); a square of the size given, or of any size (0). The splat map is linear and clamped, a layer's
    // image a colour (sRGB) repeating.
    static void WriteTexture(AssetTypeValueField v, string path, string name, int size, bool linear, bool repeat)
    {
        // Read as it is (unpremultiplied): a splat map's alpha is a weight, the colour must not be multiplied by it.
        using var codec = SKCodec.Create(path) ?? throw new Exception($"terrain: {path} could not be read");
        if (size > 0 && (codec.Info.Width != size || codec.Info.Height != size))
            throw new Exception($"terrain: {path} is {codec.Info.Width}x{codec.Info.Height}, {size}x{size} is needed");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var image = new SKBitmap(info);
        if (codec.GetPixels(info, image.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new Exception($"terrain: {path} could not be read");
        // The mipmaps: each the average of four of the level above, channel by channel (alpha a channel as the others).
        var levels = new List<byte[]> { image.Bytes };
        for (int w = image.Width, h = image.Height; w > 1 || h > 1;)
        {
            int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
            var above = levels[^1];
            var level = new byte[nw * nh * 4];
            for (var y = 0; y < nh; y++)
                for (var x = 0; x < nw; x++)
                    for (var c = 0; c < 4; c++)
                    {
                        int x0 = Math.Min(2 * x, w - 1), x1 = Math.Min(2 * x + 1, w - 1), y0 = Math.Min(2 * y, h - 1), y1 = Math.Min(2 * y + 1, h - 1);
                        var sum = above[(y0 * w + x0) * 4 + c] + above[(y0 * w + x1) * 4 + c] + above[(y1 * w + x0) * 4 + c] + above[(y1 * w + x1) * 4 + c];
                        level[(y * nw + x) * 4 + c] = (byte)((sum + 2) / 4);
                    }
            levels.Add(level);
            (w, h) = (nw, nh);
        }
        var all = levels.SelectMany(l => l).ToArray();
        v["m_Name"].AsString = name;
        v["m_Width"].AsInt = image.Width;
        v["m_Height"].AsInt = image.Height;
        var settings = v["m_TextureSettings"];
        settings["m_FilterMode"].AsInt = 2;
        settings["m_Aniso"].AsInt = repeat ? 4 : 1;
        settings["m_WrapU"].AsInt = settings["m_WrapV"].AsInt = settings["m_WrapW"].AsInt = repeat ? 0 : 1;
        v["m_TextureFormat"].AsInt = 4;
        v["m_MipCount"].AsInt = levels.Count;
        v["m_CompleteImageSize"].AsUInt = (uint)all.Length;
        v["image data"].AsByteArray = all;
        v["m_ColorSpace"].AsInt = linear ? 0 : 1;
        v["m_StreamData.offset"].AsULong = 0;
        v["m_StreamData.size"].AsUInt = 0;
        v["m_StreamData.path"].AsString = "";
    }
}
