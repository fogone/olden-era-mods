using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SkiaSharp;

namespace Nobirds.OldenEra.Modding.Bundle;

// A model of a glTF binary file (.glb: an image-to-3D tool's, a 3D editor's) made a prefab of a bundle of the game's
// Unity version, without Unity: a bundle of that version (one holding a prefab of a textured mesh: a GameObject with a
// MeshFilter and a MeshRenderer, its material's texture) is the shape of what is written, its prefab given the model's
// mesh and texture, everything else in it taken out:
//
//   { "glb": "town.glb", "name": "myfaction_town", "template": "<the bundle's prefab of a textured mesh>", "turn": 0 }
//
// The mesh: the glTF's first one, its triangles, positions, normals and first UV set (into Unity's left-handed space:
// x mirrored, the triangles' order turned), turned by "turn" degrees about the vertical axis. The texture: the
// material's base colour (PNG, JPEG or WebP), uncompressed with its mipmaps. The material keeps the template's shader;
// the library draws a model on a shader the game does not have with the game's material (ModAssets).
sealed class GlbSpec
{
    public string Glb { get; set; }
    public string Name { get; set; }
    public string Template { get; set; }
    public float Turn { get; set; }

    public static GlbSpec Read(string file)
    {
        var spec = JsonSerializer.Deserialize<GlbSpec>(File.ReadAllText(file),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        spec.Glb = Path.GetFullPath(spec.Glb, Path.GetDirectoryName(Path.GetFullPath(file)));
        return spec;
    }
}

sealed class GlbModel
{
    public float[] Positions, Normals, Uvs;
    public uint[] Indices;
    public byte[] Texture;

    public static GlbModel Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (BitConverter.ToUInt32(bytes, 0) != 0x46546C67)
            throw new Exception($"{path}: not a glTF binary file");
        var jsonLength = BitConverter.ToInt32(bytes, 12);
        var json = JsonNode.Parse(bytes.AsSpan(20, jsonLength))!;
        var bin = 20 + jsonLength + 8;
        var accessors = json["accessors"]!.AsArray();
        var views = json["bufferViews"]!.AsArray();

        (int offset, int stride, int count, int components, int type) Accessor(int index)
        {
            var a = accessors[index]!;
            var view = views[(int)a["bufferView"]!]!;
            var components = (string)a["type"] switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, var t => throw new Exception($"accessor type {t}") };
            var type = (int)a["componentType"]!;
            var size = type switch { 5126 or 5125 => 4, 5123 => 2, 5121 => 1, _ => throw new Exception($"component type {type}") };
            return (bin + ((int?)view["byteOffset"] ?? 0) + ((int?)a["byteOffset"] ?? 0), (int?)view["byteStride"] ?? size * components,
                (int)a["count"]!, components, type);
        }

        float[] Floats(int index)
        {
            var (offset, stride, count, components, type) = Accessor(index);
            if (type != 5126)
                throw new Exception("a vertex attribute is not of floats");
            var values = new float[count * components];
            for (var i = 0; i < count; i++)
                for (var c = 0; c < components; c++)
                    values[i * components + c] = BitConverter.ToSingle(bytes, offset + i * stride + c * 4);
            return values;
        }

        var meshes = json["meshes"]!.AsArray();
        if (meshes.Count != 1 || meshes[0]!["primitives"]!.AsArray().Count != 1)
            Console.WriteLine($"  {Path.GetFileName(path)}: {meshes.Count} meshes, the first one's first part taken");
        var primitive = meshes[0]!["primitives"]![0]!;
        var attributes = primitive["attributes"]!;
        var model = new GlbModel
        {
            Positions = Floats((int)attributes["POSITION"]!),
            Normals = attributes["NORMAL"] is { } n ? Floats((int)n) : null,
            Uvs = attributes["TEXCOORD_0"] is { } uv ? Floats((int)uv) : null,
        };
        var (indexOffset, indexStride, indexCount, _, indexType) = Accessor((int)primitive["indices"]!);
        model.Indices = new uint[indexCount];
        for (var i = 0; i < indexCount; i++)
            model.Indices[i] = indexType switch
            {
                5125 => BitConverter.ToUInt32(bytes, indexOffset + i * indexStride),
                5123 => BitConverter.ToUInt16(bytes, indexOffset + i * indexStride),
                _ => bytes[indexOffset + i * indexStride],
            };
        if (json["nodes"]?.AsArray().Any(node => node?["matrix"] != null || node?["rotation"] != null || node?["scale"] != null || node?["translation"] != null) == true)
            Console.WriteLine($"  {Path.GetFileName(path)}: its nodes' transforms are not applied");

        // The base colour's image, as stored in the file.
        if (primitive["material"] is { } m && json["materials"]![(int)m]!["pbrMetallicRoughness"]?["baseColorTexture"]?["index"] is { } t)
        {
            // The image: the texture's source, or its WebP one (EXT_texture_webp).
            var texture = json["textures"]![(int)t]!;
            var source = texture["source"] ?? texture["extensions"]?["EXT_texture_webp"]?["source"] ?? throw new Exception("the base colour texture has no image");
            var image = json["images"]![(int)source]!;
            var view = views[(int)image["bufferView"]!]!;
            model.Texture = bytes.AsSpan(bin + ((int?)view["byteOffset"] ?? 0), (int)view["byteLength"]!).ToArray();
        }
        return model;
    }
}

sealed class GlbPrefab
{
    readonly AssetsManager am;
    readonly AssetsFileInstance file;

    public GlbPrefab(AssetsManager am, AssetsFileInstance file)
    {
        this.am = am;
        this.file = file;
    }

    AssetTypeValueField Field(long id) => am.GetBaseField(file, file.file.GetAssetInfo(id));

    void Edit(long id, Action<AssetTypeValueField> change)
    {
        var info = file.file.GetAssetInfo(id);
        var v = am.GetBaseField(file, info);
        change(v);
        info.SetNewData(v);
    }

    IEnumerable<long> Components(long go) => Field(go)["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong);

    long Component(long go, AssetClassID type) => Components(go).FirstOrDefault(c => file.file.GetAssetInfo(c).TypeId == (int)type);

    public void Make(GlbSpec spec, GlbModel model)
    {
        // The template prefab: its root, the GameObject with the textured mesh, its mesh, material, texture, shader.
        var roots = file.file.GetAssetsOfType(AssetClassID.GameObject).Select(i => i.PathId)
            .Where(go => Field(go)["m_Name"].AsString == spec.Template).ToList();
        if (roots.Count == 0)
            throw new Exception($"the bundle has no prefab {spec.Template}");
        var root = roots[0];
        var objects = new Hierarchy(am, file).Objects(root);
        var meshGo = objects.FirstOrDefault(o => file.file.GetAssetInfo(o).TypeId == (int)AssetClassID.GameObject
            && Component(o, AssetClassID.MeshFilter) != 0 && Component(o, AssetClassID.MeshRenderer) != 0);
        if (meshGo == 0)
            throw new Exception($"{spec.Template} has no textured mesh");
        long meshFilter = Component(meshGo, AssetClassID.MeshFilter), renderer = Component(meshGo, AssetClassID.MeshRenderer);
        var mesh = Field(meshFilter)["m_Mesh.m_PathID"].AsLong;
        var material = Field(renderer)["m_Materials.Array"].Children[0]["m_PathID"].AsLong;
        var materialValue = Field(material);
        var texture = materialValue["m_SavedProperties.m_TexEnvs.Array"].Children
            .First(e => e["first"].AsString == "_MainTex")["second.m_Texture.m_PathID"].AsLong;
        var shader = materialValue["m_Shader.m_PathID"].AsLong;

        // The GameObjects from the root down to the mesh's, the mesh's under the root alone.
        var chain = new List<long>();
        for (var t = Component(meshGo, AssetClassID.Transform); t != 0; t = Field(t)["m_Father.m_PathID"].AsLong)
            chain.Add(t);
        var kept = new HashSet<long>(chain) { mesh, material, texture, shader, meshFilter, renderer };
        foreach (var t in chain)
            kept.Add(Field(t)["m_GameObject.m_PathID"].AsLong);
        foreach (var info in file.file.GetAssetsOfType(AssetClassID.AssetBundle))
            kept.Add(info.PathId);
        foreach (var info in file.file.Metadata.AssetInfos.ToList())
            if (!kept.Contains(info.PathId))
                file.file.Metadata.RemoveAssetInfo(info);
        foreach (var t in chain)
            Edit(t, v =>
            {
                v["m_Children.Array"].Children.RemoveAll(c => !kept.Contains(c["m_PathID"].AsLong));
                (v["m_LocalPosition.x"].AsFloat, v["m_LocalPosition.y"].AsFloat, v["m_LocalPosition.z"].AsFloat) = (0, 0, 0);
                (v["m_LocalRotation.x"].AsFloat, v["m_LocalRotation.y"].AsFloat, v["m_LocalRotation.z"].AsFloat, v["m_LocalRotation.w"].AsFloat) = (0, 0, 0, 1);
                (v["m_LocalScale.x"].AsFloat, v["m_LocalScale.y"].AsFloat, v["m_LocalScale.z"].AsFloat) = (1, 1, 1);
            });
        foreach (var t in chain)
            Edit(Field(t)["m_GameObject.m_PathID"].AsLong, v => v["m_Component.Array"].Children.RemoveAll(c => !kept.Contains(c["component.m_PathID"].AsLong)));
        Edit(root, v => v["m_Name"].AsString = spec.Name);
        Edit(meshGo, v => v["m_Name"].AsString = spec.Name + "_mesh");

        WriteMesh(mesh, spec, model);
        WriteTexture(texture, spec, model);
        Edit(material, v =>
        {
            v["m_Name"].AsString = spec.Name;
            // Only the main texture: the others were the template's.
            v["m_SavedProperties.m_TexEnvs.Array"].Children.RemoveAll(e => e["first"].AsString != "_MainTex");
        });

        // The bundle's one asset: the prefab, with what it is made of to preload.
        foreach (var info in file.file.GetAssetsOfType(AssetClassID.AssetBundle))
            Edit(info.PathId, v =>
            {
                v["m_Name"].AsString = spec.Name;
                v["m_AssetBundleName"].AsString = spec.Name;
                var preload = v["m_PreloadTable.Array"];
                preload.Children.Clear();
                foreach (var id in kept.Where(id => id != info.PathId && id != root).OrderBy(id => id))
                {
                    var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(preload);
                    e["m_PathID"].AsLong = id;
                    preload.Children.Add(e);
                }
                var container = v["m_Container.Array"];
                var entry = container.Children[0];
                container.Children.Clear();
                entry["first"].AsString = $"assets/{spec.Name}.prefab";
                entry["second.preloadIndex"].AsInt = 0;
                entry["second.preloadSize"].AsInt = preload.Children.Count;
                entry["second.asset.m_PathID"].AsLong = root;
                container.Children.Add(entry);
            });
        Console.WriteLine($"  {spec.Name}: {model.Positions.Length / 3} vertices, {model.Indices.Length / 3} triangles, texture {(model.Texture == null ? "none" : "from the model")}");
    }

    void WriteMesh(long id, GlbSpec spec, GlbModel model)
    {
        var count = model.Positions.Length / 3;
        var turn = Matrix4x4.CreateRotationY(spec.Turn * MathF.PI / 180);
        // Unity's vertex layout of the template: position, normal and the first UV set, floats, one stream.
        var data = new byte[count * 32];
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        for (var i = 0; i < count; i++)
        {
            var p = Vector3.Transform(new Vector3(-model.Positions[i * 3], model.Positions[i * 3 + 1], model.Positions[i * 3 + 2]), turn);
            var n = model.Normals == null ? Vector3.UnitY
                : Vector3.TransformNormal(new Vector3(-model.Normals[i * 3], model.Normals[i * 3 + 1], model.Normals[i * 3 + 2]), turn);
            float u = model.Uvs?[i * 2] ?? 0, w = model.Uvs?[i * 2 + 1] ?? 0;
            var at = i * 32;
            foreach (var f in new[] { p.X, p.Y, p.Z, n.X, n.Y, n.Z, u, w })
            {
                BitConverter.TryWriteBytes(data.AsSpan(at), f);
                at += 4;
            }
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        var indices = new byte[model.Indices.Length * 4];
        for (var i = 0; i < model.Indices.Length; i += 3)
        {
            // Mirrored: each triangle's order turned.
            BitConverter.TryWriteBytes(indices.AsSpan(i * 4), model.Indices[i]);
            BitConverter.TryWriteBytes(indices.AsSpan(i * 4 + 4), model.Indices[i + 2]);
            BitConverter.TryWriteBytes(indices.AsSpan(i * 4 + 8), model.Indices[i + 1]);
        }
        var center = (min + max) / 2;
        var extent = (max - min) / 2;
        void Box(AssetTypeValueField box)
        {
            (box["m_Center.x"].AsFloat, box["m_Center.y"].AsFloat, box["m_Center.z"].AsFloat) = (center.X, center.Y, center.Z);
            (box["m_Extent.x"].AsFloat, box["m_Extent.y"].AsFloat, box["m_Extent.z"].AsFloat) = (extent.X, extent.Y, extent.Z);
        }
        Edit(id, v =>
        {
            var channels = v["m_VertexData.m_Channels.Array"].Children;
            (int offset, int dimension)[] wanted = { (0, 3), (12, 3), (0, 0), (0, 0), (24, 2) };
            for (var c = 0; c < channels.Count; c++)
            {
                var (offset, dimension) = c < wanted.Length ? wanted[c] : (0, 0);
                channels[c]["stream"].AsByte = 0;
                channels[c]["offset"].AsByte = (byte)offset;
                channels[c]["format"].AsByte = 0;
                channels[c]["dimension"].AsByte = (byte)dimension;
            }
            v["m_Name"].AsString = spec.Name;
            v["m_VertexData.m_VertexCount"].AsUInt = (uint)count;
            v["m_VertexData.m_DataSize"].AsByteArray = data;
            v["m_IndexFormat"].AsInt = 1;
            v["m_IndexBuffer.Array"].AsByteArray = indices;
            var sub = v["m_SubMeshes.Array"].Children[0];
            sub["firstByte"].AsUInt = 0;
            sub["indexCount"].AsUInt = (uint)model.Indices.Length;
            sub["baseVertex"].AsUInt = 0;
            sub["firstVertex"].AsUInt = 0;
            sub["vertexCount"].AsUInt = (uint)count;
            Box(sub["localAABB"]);
            Box(v["m_LocalAABB"]);
            v["m_StreamData.offset"].AsULong = 0;
            v["m_StreamData.size"].AsUInt = 0;
            v["m_StreamData.path"].AsString = "";
        });
    }

    void WriteTexture(long id, GlbSpec spec, GlbModel model)
    {
        var info = new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var decoded = model.Texture != null ? SKBitmap.Decode(model.Texture) ?? throw new Exception("the base colour image could not be read") : null;
        using var image = new SKBitmap(info.WithSize(decoded?.Width ?? 4, decoded?.Height ?? 4));
        if (decoded == null)
            image.Erase(new SKColor(200, 200, 200));
        else
            decoded.CopyTo(image, SKColorType.Rgba8888);
        // Uncompressed (RGBA32) with its mipmaps, the rows as the image has them: Unity's first row is a UV's v = 0, the
        // glTF's top one.
        var levels = new List<byte[]>();
        for (int w = image.Width, h = image.Height; ; w = Math.Max(1, w / 2), h = Math.Max(1, h / 2))
        {
            using var level = w == image.Width ? image.Copy() : image.Resize(info.WithSize(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            levels.Add(level.Bytes);
            if (w == 1 && h == 1)
                break;
        }
        var all = levels.SelectMany(l => l).ToArray();
        Edit(id, v =>
        {
            v["m_Name"].AsString = spec.Name;
            v["m_Width"].AsInt = image.Width;
            v["m_Height"].AsInt = image.Height;
            v["m_TextureFormat"].AsInt = 4;
            v["m_MipCount"].AsInt = levels.Count;
            v["m_CompleteImageSize"].AsUInt = (uint)all.Length;
            v["image data"].AsByteArray = all;
            v["m_StreamData.offset"].AsULong = 0;
            v["m_StreamData.size"].AsUInt = 0;
            v["m_StreamData.path"].AsString = "";
        });
    }
}
