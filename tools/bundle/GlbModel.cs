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
    // The name the template's shader is given: a shader of the game (e.g. Hex/Lit), which the mod library draws the
    // model with in place of the template's (ModAssets, ModScenes); the template's own when left out.
    public string Shader { get; set; }
    // The texture's largest side at most (halved until it fits); the model's own size when left out.
    public int TextureSize { get; set; }

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
    // A skinned mesh's (the glTF's mesh with JOINTS_0 and WEIGHTS_0 on a node with a skin): four bones and weights a
    // vertex, the skin's inverse bind matrices (glTF's order: by columns), its bones' paths from the root bone down
    // ("Root/Hips/Spine", as Unity names a bone in a mesh) and the root bone's name.
    public int[] Joints;
    public float[] Weights, BindPoses;
    public string[] BonePaths;
    public string RootBone;
    public bool Skinned => Joints != null;

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
            var components = (string)a["type"] switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, var t => throw new Exception($"accessor type {t}") };
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
        var nodes = json["nodes"]?.AsArray() ?? new JsonArray();
        var skinned = nodes.Select((node, i) => (node, i)).FirstOrDefault(n => (int?)n.node?["mesh"] == 0 && n.node?["skin"] != null);
        if (skinned.node != null && attributes["JOINTS_0"] is { } joints && attributes["WEIGHTS_0"] is { } weights)
        {
            var (offset, stride, count, components, type) = Accessor((int)joints);
            model.Joints = new int[count * 4];
            for (var i = 0; i < count; i++)
                for (var c = 0; c < 4; c++)
                    model.Joints[i * 4 + c] = type == 5123 ? BitConverter.ToUInt16(bytes, offset + i * stride + c * 2) : bytes[offset + i * stride + c];
            model.Weights = Floats((int)weights);
            var skin = json["skins"]![(int)skinned.node["skin"]!]!;
            var jointNodes = skin["joints"]!.AsArray().Select(j => (int)j!).ToArray();
            model.BindPoses = Floats((int)skin["inverseBindMatrices"]!);
            // A bone's path: the names from the topmost bone of the skin down to it.
            var parent = new Dictionary<int, int>();
            for (var i = 0; i < nodes.Count; i++)
                foreach (var child in nodes[i]?["children"]?.AsArray() ?? new JsonArray())
                    parent[(int)child!] = i;
            var isJoint = jointNodes.ToHashSet();
            string Name(int n) => (string)nodes[n]!["name"] ?? $"node{n}";
            model.BonePaths = jointNodes.Select(n =>
            {
                var names = new List<string> { Name(n) };
                for (var p = n; parent.TryGetValue(p, out var up) && isJoint.Contains(up); p = up)
                    names.Insert(0, Name(up));
                return string.Join("/", names);
            }).ToArray();
            model.RootBone = model.BonePaths.OrderBy(p => p.Count(c => c == '/')).First().Split('/')[0];
        }
        else if (nodes.Any(node => node?["matrix"] != null || node?["rotation"] != null || node?["scale"] != null || node?["translation"] != null))
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
        if (spec.Shader != null)
            Edit(shader, v => v["m_ParsedForm.m_Name"].AsString = spec.Shader);
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
        // A skinned mesh stays in its bind poses' space: not turned.
        if (model.Skinned && spec.Turn != 0)
            Console.WriteLine($"  {spec.Name}: a skinned mesh is not turned (its bind poses would not fit)");
        var turn = Matrix4x4.CreateRotationY((model.Skinned ? 0 : spec.Turn) * MathF.PI / 180);
        // Unity's vertex layout of the template: position, normal and the first UV set, floats, one stream; a skinned
        // mesh's bone weights (four floats) and bones (four 32-bit integers) in a second stream, as the game's units have.
        var data = new byte[count * (model.Skinned ? 64 : 32)];
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
            if (model.Skinned)
            {
                var skin = count * 32 + i * 32;
                for (var c = 0; c < 4; c++)
                {
                    BitConverter.TryWriteBytes(data.AsSpan(skin + c * 4), model.Weights[i * 4 + c]);
                    BitConverter.TryWriteBytes(data.AsSpan(skin + 16 + c * 4), (uint)model.Joints[i * 4 + c]);
                }
            }
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
            // Channels: position, normal, tangent, colour, UV0..7, blend weights, blend indices (stream, offset, format
            // (0 float, 10 32-bit unsigned integer), dimension).
            var wanted = new (int stream, int offset, int format, int dimension)[14];
            (wanted[0], wanted[1], wanted[4]) = ((0, 0, 0, 3), (0, 12, 0, 3), (0, 24, 0, 2));
            if (model.Skinned)
                (wanted[12], wanted[13]) = ((1, 0, 0, 4), (1, 16, 10, 4));
            for (var c = 0; c < channels.Count; c++)
            {
                var (stream, offset, format, dimension) = c < wanted.Length ? wanted[c] : (0, 0, 0, 0);
                channels[c]["stream"].AsByte = (byte)stream;
                channels[c]["offset"].AsByte = (byte)offset;
                channels[c]["format"].AsByte = (byte)format;
                channels[c]["dimension"].AsByte = (byte)dimension;
            }
            if (model.Skinned)
                WriteSkin(v, model, data, count);
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

    // A skinned mesh's bind poses (glTF's inverse bind matrices, mirrored as the vertices are), its bones by the CRC-32
    // of their paths (as Unity hashes a bone's name in a mesh) and each bone's box of the vertices it moves, in its own
    // space (the renderer's bounds are made of them).
    static void WriteSkin(AssetTypeValueField v, GlbModel model, byte[] data, int count)
    {
        var bones = model.BonePaths.Length;
        var poses = new Matrix4x4[bones];
        float[] sign = { -1, 1, 1, 1 };
        for (var b = 0; b < bones; b++)
        {
            float M(int r, int c) => sign[r] * sign[c] * model.BindPoses[b * 16 + c * 4 + r];
            poses[b] = new Matrix4x4(M(0, 0), M(0, 1), M(0, 2), M(0, 3), M(1, 0), M(1, 1), M(1, 2), M(1, 3),
                M(2, 0), M(2, 1), M(2, 2), M(2, 3), M(3, 0), M(3, 1), M(3, 2), M(3, 3));
        }
        var low = Enumerable.Repeat(new Vector3(float.PositiveInfinity), bones).ToArray();
        var high = Enumerable.Repeat(new Vector3(float.NegativeInfinity), bones).ToArray();
        for (var i = 0; i < count; i++)
        {
            var p = new Vector3(BitConverter.ToSingle(data, i * 32), BitConverter.ToSingle(data, i * 32 + 4), BitConverter.ToSingle(data, i * 32 + 8));
            for (var c = 0; c < 4; c++)
            {
                if (model.Weights[i * 4 + c] <= 0)
                    continue;
                var b = model.Joints[i * 4 + c];
                var m = poses[b];
                // Unity's matrices act on columns: q = M p.
                var q = new Vector3(m.M11 * p.X + m.M12 * p.Y + m.M13 * p.Z + m.M14, m.M21 * p.X + m.M22 * p.Y + m.M23 * p.Z + m.M24,
                    m.M31 * p.X + m.M32 * p.Y + m.M33 * p.Z + m.M34);
                low[b] = Vector3.Min(low[b], q);
                high[b] = Vector3.Max(high[b], q);
            }
        }
        var poseArray = v["m_BindPose.Array"];
        poseArray.Children.Clear();
        foreach (var m in poses)
        {
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(poseArray);
            float[,] values = { { m.M11, m.M12, m.M13, m.M14 }, { m.M21, m.M22, m.M23, m.M24 }, { m.M31, m.M32, m.M33, m.M34 }, { m.M41, m.M42, m.M43, m.M44 } };
            for (var r = 0; r < 4; r++)
                for (var c = 0; c < 4; c++)
                    e[$"e{r}{c}"].AsFloat = values[r, c];
            poseArray.Children.Add(e);
        }
        var hashes = v["m_BoneNameHashes.Array"];
        hashes.Children.Clear();
        foreach (var path in model.BonePaths)
        {
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(hashes);
            e.AsUInt = Crc32(path);
            hashes.Children.Add(e);
        }
        v["m_RootBoneNameHash"].AsUInt = Crc32(model.RootBone);
        var boxes = v["m_BonesAABB.Array"];
        boxes.Children.Clear();
        for (var b = 0; b < bones; b++)
        {
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(boxes);
            (e["m_Min.x"].AsFloat, e["m_Min.y"].AsFloat, e["m_Min.z"].AsFloat) = (low[b].X, low[b].Y, low[b].Z);
            (e["m_Max.x"].AsFloat, e["m_Max.y"].AsFloat, e["m_Max.z"].AsFloat) = (high[b].X, high[b].Y, high[b].Z);
            boxes.Children.Add(e);
        }
        v["m_VariableBoneCountWeights.m_Data.Array"].Children.Clear();
        Console.WriteLine($"  skinned: {bones} bones from {model.RootBone}");
    }

    static uint Crc32(string text)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(text))
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    void WriteTexture(long id, GlbSpec spec, GlbModel model)
    {
        var info = new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var decoded = model.Texture != null ? SKBitmap.Decode(model.Texture) ?? throw new Exception("the base colour image could not be read") : null;
        int width = decoded?.Width ?? 4, height = decoded?.Height ?? 4;
        while (spec.TextureSize > 0 && Math.Max(width, height) > spec.TextureSize)
            (width, height) = (Math.Max(1, width / 2), Math.Max(1, height / 2));
        using var image = new SKBitmap(info.WithSize(width, height));
        if (decoded == null)
            image.Erase(new SKColor(200, 200, 200));
        else if (decoded.Width == width && decoded.Height == height)
            decoded.CopyTo(image, SKColorType.Rgba8888);
        else
            decoded.ScalePixels(image, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
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
