using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Bundle;

// A town's light, fog and post-processing as the game's towns have them: global volumes of Post Processing v2 on layer
// 12 (the game's cameras render the volumes of that layer), one with the game's own effect Hex.Render.LightingSettings
// (the sun's colour, strength, height and direction, its shadows, the ambient colour, SSAO, the fog: the game's
// pipeline sets its light and fog from it on every render), one with bloom, depth of field and vignette. The camera's
// post-processing layer, which needs the game's own resources, is added by the mod library when the scene loads.
sealed class LightingSpec
{
    public SunSpec Sun { get; set; } = new();
    public float[] Ambient { get; set; } = { 0.41f, 0.47f, 0.55f };
    public float EmissionStrength { get; set; } = 1;
    public SsaoSpec Ssao { get; set; } = new();
    public FogSpec Fog { get; set; }
    public BloomSpec Bloom { get; set; }
    public VignetteSpec Vignette { get; set; }
    public DepthOfFieldSpec DepthOfField { get; set; }

    public sealed class SunSpec
    {
        public float[] Color { get; set; } = { 1, 0.96f, 0.85f };
        public float Intensity { get; set; } = 1.3f;
        // Degrees: the sun's height over the horizon and its direction about the vertical axis.
        public float Height { get; set; } = 31;
        public float Azimuth { get; set; } = 132;
        public float Shadows { get; set; } = 1;
    }

    public sealed class SsaoSpec
    {
        public float Intensity { get; set; } = 0.72f;
        public float Radius { get; set; } = 0.039f;
        public float DirectLightOcclusion { get; set; } = 1;
    }

    public sealed class FogSpec
    {
        public float[] Color { get; set; } = { 0.7f, 0.79f, 0.75f, 0.41f };
        public float Start { get; set; } = 20;
        public float End { get; set; } = 80;
    }

    public sealed class BloomSpec
    {
        public float Intensity { get; set; } = 4.39f;
        public float Threshold { get; set; } = 0.97f;
        public float SoftKnee { get; set; } = 0.042f;
        public float[] Color { get; set; } = { 1, 0.853f, 0.42f };
    }

    public sealed class VignetteSpec
    {
        public float Intensity { get; set; } = 0.34f;
        public float Smoothness { get; set; } = 0.26f;
        public float[] Color { get; set; } = { 0, 0, 0 };
    }

    public sealed class DepthOfFieldSpec
    {
        public float FocusDistance { get; set; } = 31;
        public float Aperture { get; set; } = 3.1f;
        public float FocalLength { get; set; } = 72;
    }
}

sealed partial class CityScene
{
    const string PostProcessing = "UnityEngine.Rendering.PostProcessing", PostProcessingAssembly = "Unity.Postprocessing.Runtime.dll";
    const uint VolumeLayer = 12;
    // The town camera's rig (CityCamera), where the volumes go, after the camera, as in the game's towns: they turn on
    // after the camera's post-processing layer (on before it, the effects' volume left the picture black).
    long cameraRig;

    void AddLighting(LightingSpec spec)
    {
        var light = Settings("Hex.Render", "LightingSettings", "LightingSettings", Assembly, new (string, string)[]
        {
            ("mainLightColor", "Color"), ("mainLightIntensity", "Float"), ("lightZenithAngle", "Float"),
            ("lightAzimuthAngle", "Float"), ("shadowsStrength", "Float"), ("cloudShadowsOpacity", "Float"),
            ("emissionStrength", "Float"), ("skybox", "Material"), ("ambientColor", "Color"), ("ssaoEnabled", "Bool"),
            ("ssaoIntensity", "Float"), ("ssaoRadius", "Float"), ("directLightOcclusion", "Float"), ("fogEnabled", "Bool"),
            ("fogColor", "Color"), ("fogMode", "FogMode"), ("fogDensity", "Float"), ("fogStart", "Float"), ("fogEnd", "Float"),
        }, v =>
        {
            Color(v, "mainLightColor", spec.Sun.Color);
            Float(v, "mainLightIntensity", spec.Sun.Intensity);
            // The game's zenith angle is the sun's height over the horizon (the towns' 22-62 degrees).
            Float(v, "lightZenithAngle", spec.Sun.Height);
            Float(v, "lightAzimuthAngle", spec.Sun.Azimuth);
            Float(v, "shadowsStrength", spec.Sun.Shadows);
            Float(v, "emissionStrength", spec.EmissionStrength);
            Color(v, "ambientColor", spec.Ambient);
            Bool(v, "ssaoEnabled", true);
            Float(v, "ssaoIntensity", spec.Ssao.Intensity);
            Float(v, "ssaoRadius", spec.Ssao.Radius);
            Float(v, "directLightOcclusion", spec.Ssao.DirectLightOcclusion);
            Bool(v, "fogEnabled", spec.Fog != null);
            if (spec.Fog is { } fog)
            {
                Color(v, "fogColor", fog.Color);
                Int(v, "fogMode", 1);                       // linear, as all the game's towns
                Float(v, "fogStart", fog.Start);
                Float(v, "fogEnd", fog.End);
            }
        });
        Volume("LightVolume", new[] { light });
        MainLight();

        var effects = new List<long>();
        if (spec.Bloom is { } bloom)
            effects.Add(Settings(PostProcessing, "Bloom", "Bloom", PostProcessingAssembly, new (string, string)[]
            {
                ("intensity", "Float"), ("threshold", "Float"), ("softKnee", "Float"), ("clamp", "Float"), ("diffusion", "Float"),
                ("anamorphicRatio", "Float"), ("color", "Color"), ("fastMode", "Bool"),
            }, v =>
            {
                Float(v, "intensity", bloom.Intensity);
                Float(v, "threshold", bloom.Threshold);
                Float(v, "softKnee", bloom.SoftKnee);
                Float(v, "clamp", 65472, false);
                Float(v, "diffusion", 7, false);
                Color(v, "color", bloom.Color);
            }));
        if (spec.DepthOfField is { } dof)
            effects.Add(Settings(PostProcessing, "DepthOfField", "DepthOfField", PostProcessingAssembly, new (string, string)[]
            {
                ("focusDistance", "Float"), ("aperture", "Float"), ("focalLength", "Float"), ("kernelSize", "KernelSize"),
            }, v =>
            {
                Float(v, "focusDistance", dof.FocusDistance);
                Float(v, "aperture", dof.Aperture);
                Float(v, "focalLength", dof.FocalLength);
                Int(v, "kernelSize", 1, false);
            }));
        if (spec.Vignette is { } vignette)
            effects.Add(Settings(PostProcessing, "Vignette", "Vignette", PostProcessingAssembly, new (string, string)[]
            {
                ("mode", "VignetteMode"), ("color", "Color"), ("center", "Vector2"), ("intensity", "Float"), ("smoothness", "Float"),
                ("roundness", "Float"), ("rounded", "Bool"),
            }, v =>
            {
                Color(v, "color", vignette.Color);
                v["center.value.x"].AsFloat = v["center.value.y"].AsFloat = 0.5f;
                Float(v, "intensity", vignette.Intensity);
                Float(v, "smoothness", vignette.Smoothness);
                Float(v, "roundness", 1, false);
            }));
        if (effects.Count > 0)
            Volume("PostProcesEffects", effects.ToArray());
        Console.WriteLine($"  lighting: sun {spec.Sun.Intensity} at {spec.Sun.Height}/{spec.Sun.Azimuth}, fog {(spec.Fog is { } f ? $"{f.Start}-{f.End}" : "off")}, {effects.Count} effects");
    }

    // The scene's directional light made its sun, as the game's towns' "Sun": the game's main light marker on it (a
    // readable name, the library puts the game's in), which the game's pipeline takes the sun by, turns and lights
    // from the LightingSettings; its shadows on (hard, as the game's).
    void MainLight()
    {
        var suns = scene.file.GetAssetsOfType(AssetClassID.Light).Select(i => i.PathId)
            .Where(id => Field(scene, id)["m_Type"].AsInt == 1).ToList();
        if (suns.Count == 0)
            throw new Exception("lighting: the scene has no directional light to make its sun");
        var sun = suns[0];
        Edit(sun, v => v["m_Shadows.m_Type"].AsInt = 1);
        var go = Field(scene, sun)["m_GameObject.m_PathID"].AsLong;
        long marker = nextScene++;
        Component(marker, go, "OldenEra", "MainLight", Enumerable.Empty<TypeTrees.Node>(), _ => { });
        AddComponents(go, marker);
        Console.WriteLine($"  sun: {Field(scene, go)["m_Name"].AsString}");
    }

    // A global volume: an object on the volumes' layer under the camera's rig with the volume and its profile.
    void Volume(string name, long[] settings)
    {
        var profile = SharedAsset(PostProcessing, "PostProcessProfile", TypeTrees.PPtrArray(1, "settings", "PostProcessEffectSettings")
            .Append(new TypeTrees.Node(1, "bool", "isDirty", 1, TypeTrees.Aligned)), v =>
        {
            v["m_Name"].AsString = name;
            var array = v["settings.Array"];
            foreach (var s in settings)
            {
                var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
                e["m_FileID"].AsInt = 0;
                e["m_PathID"].AsLong = s;
                array.Children.Add(e);
            }
        }, PostProcessingAssembly);
        long go = nextScene++, transform = nextScene++, volume = nextScene++;
        GameObject(go, name, VolumeLayer, transform, volume);
        Transform(transform, go, cameraRig, new[] { 0f, 0f, 0f });
        if (cameraRig != 0)
            Edit(cameraRig, v =>
            {
                var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(v["m_Children.Array"]);
                e["m_PathID"].AsLong = transform;
                v["m_Children.Array"].Children.Add(e);
            });
        Component(volume, go, PostProcessing, "PostProcessVolume", TypeTrees.PPtr(1, "sharedProfile", "PostProcessProfile").Concat(new[]
        {
            new TypeTrees.Node(1, "bool", "isGlobal", 1, TypeTrees.Aligned), new TypeTrees.Node(1, "float", "blendDistance", 4, 0),
            new TypeTrees.Node(1, "float", "weight", 4, 0), new TypeTrees.Node(1, "float", "priority", 4, 0),
        }), v =>
        {
            v["sharedProfile.m_FileID"].AsInt = sharedFileId;
            v["sharedProfile.m_PathID"].AsLong = profile;
            v["isGlobal"].AsBool = true;
            v["weight"].AsFloat = 1;
            v["priority"].AsFloat = 1;
        }, PostProcessingAssembly);
    }

    // An effect's settings (a ScriptableObject): the base's active and enabled, then its parameters, each an override
    // flag and a value.
    long Settings(string ns, string cls, string name, string assembly, (string field, string type)[] parameters, Action<AssetTypeValueField> fill) =>
        SharedAsset(ns, cls, new[] { new TypeTrees.Node(1, "bool", "active", 1, TypeTrees.Aligned) }
            .Concat(Parameter("enabled", "Bool")).Concat(parameters.SelectMany(p => Parameter(p.field, p.type))), v =>
        {
            v["m_Name"].AsString = name;
            v["active"].AsBool = true;
            Bool(v, "enabled", true);
            fill(v);
        }, assembly);

    static IEnumerable<TypeTrees.Node> Parameter(string field, string type)
    {
        yield return new TypeTrees.Node(1, type + "Parameter", field, -1, 0);
        yield return new TypeTrees.Node(2, "bool", "overrideState", 1, TypeTrees.Aligned);
        switch (type)
        {
            case "Float": yield return new TypeTrees.Node(2, "float", "value", 4, 0); break;
            case "Bool": yield return new TypeTrees.Node(2, "bool", "value", 1, TypeTrees.Aligned); break;
            case "Color":
                yield return new TypeTrees.Node(2, "ColorRGBA", "value", 16, 0);
                foreach (var c in new[] { "r", "g", "b", "a" })
                    yield return new TypeTrees.Node(3, "float", c, 4, 0);
                break;
            case "Vector2":
                yield return new TypeTrees.Node(2, "Vector2f", "value", 8, 0);
                yield return new TypeTrees.Node(3, "float", "x", 4, 0);
                yield return new TypeTrees.Node(3, "float", "y", 4, 0);
                break;
            case "Material":
                foreach (var n in TypeTrees.PPtr(2, "value", "Material"))
                    yield return n;
                break;
            default: yield return new TypeTrees.Node(2, "int", "value", 4, 0); break;   // an enum
        }
    }

    static void Float(AssetTypeValueField v, string field, float value, bool over = true) =>
        (v[field + ".overrideState"].AsBool, v[field + ".value"].AsFloat) = (over, value);

    static void Int(AssetTypeValueField v, string field, int value, bool over = true) =>
        (v[field + ".overrideState"].AsBool, v[field + ".value"].AsInt) = (over, value);

    static void Bool(AssetTypeValueField v, string field, bool value) =>
        (v[field + ".overrideState"].AsBool, v[field + ".value"].AsBool) = (true, value);

    static void Color(AssetTypeValueField v, string field, float[] c)
    {
        v[field + ".overrideState"].AsBool = true;
        var value = v[field + ".value"];
        (value["r"].AsFloat, value["g"].AsFloat, value["b"].AsFloat) = (c[0], c[1], c[2]);
        value["a"].AsFloat = c.Length > 3 ? c[3] : 1;
    }
}
