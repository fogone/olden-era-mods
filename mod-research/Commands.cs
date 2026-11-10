using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Nobirds.OldenEra.Modding.Common;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Research;

// The research commands, one per line, run on the game's main thread, from the file (research.txt) and the local
// console (127.0.0.1:8766). The tools are GameCode, Members, Probe in mod-common/shared, and Eval (live reflection).
internal sealed class Commands
{
    // The bundles loaded by the bundle command, by file name.
    static readonly System.Collections.Generic.Dictionary<string, UnityEngine.AssetBundle> bundles = new();

    public const string Help =
@"# Research of the game's code and live state. A method is Type.method (all its overloads); Type.* is all of the
# type's own methods. Obfuscated names are fine here: this is for finding what a member does.
#
# Live state (Eval: hero, $var, $ = last result, Type.static, .member, .method(args), [i], .as(Type); a type is the game's or
# Unity's, any module of it, by its name or with its namespace: UnityEngine.Resources.Load(""units/..."")):
#   eval hero.cqei.cwfa           the value of an expression
#   inspect hero                  the object's properties with their values (and its elements, for a list)
#   methods hero                  the same and its methods' signatures
#   set $h hero.cqei              keep a value in a variable
# Code:
#   members Type                  the type's properties and methods with their signatures
#   code Type.method              the method's instructions, calls named (CodeResearch.Disassemble)
#   calls Type.method             the methods it calls directly
#   callers <regex>               the game's methods calling one whose signature matches, e.g. callers ^Shader\.SetGlobal
#   shared Type.method            the methods sharing its native code (a patch would hit them all)
#   aliases [file]                the generated names (docs/game-names.md): the Lookup.Generate lookups run, written as
#                                 C# (default BepInEx\GameNames.g.cs); nothing written while one finds nothing
#   types <regex>               the game's types whose name matches (at most 100)
#   click <path end>              a mouse click on the active UI object whose path ends so (enter, down, up, click, exit),
#                                 e.g. click OE_Row_nobirds.oldenera.visited/Remove
#   objects Type [regex]          the live Unity objects of that type (at most 80; regex: by name, sprite or path): name, active, scene, path; each kept as $o0, $o1...
#   sprite <name> <file.png>      a loaded sprite of the game into a PNG file (with its size and 9-slice border)
#   bundle load <file>            an AssetBundle (a mod's models, #62): its prefabs; bundle spawn <file name> <asset path> [scale] puts one
#                                 on the ground where the camera looks, with its shaders; bundle drop removes them and unloads
#   screen make|open|hide|state|drop  a trial screen of ours in the game's screen registry (ModScreen, issue #24)
#   holders Type              the game's properties of that type (who holds a config, a service)
#   fields Type                 the class's fields with their offsets (code reading [obj+0xB8] reads the one at +0xB8)
#   slot Type 0x238             the method in the class's virtual slot (code calling [klass+0x238])
#   string qfz                    the game's hidden string constant <PrivateImplementationDetails>.a.qfz()
#   probe Type.method [seconds]   watch its calls for a while (default 10 s, at most 200 lines), not on shared code
#   probed                        the calls seen since the last time (also in the log)
#   fps [seconds]                 frame rate of the last 10 s (or the seconds given); with perf on, our code by time
#   perf on | perf off            time our mods' per-frame callbacks and patch methods (fps shows them)
#   perf add <regex>              also time our plugins' methods whose Type.method matches, e.g. perf add Plugin\.(Describe|GuardDanger); can break them: reload the plugins after
#   unprobe                     remove the probes
#   growth [dump]                 the selected hero's side: its towns' hiring (weekly growth, for hire now) and its dwellings;
#                                 dump: every value of its towns' map objects and of the side, to compare before a save and after a load
#                                 (issue #7: the growth from dwellings lost after a load)
";

    readonly Harmony harmony;
    readonly Action<string> log;
    readonly ConcurrentQueue<string> probed = new();
    float probeUntil;

    // The probes are the harmony's patches; what they see also goes to `log`.
    public Commands(Harmony harmony, Action<string> log)
    {
        this.harmony = harmony;
        this.log = log;
        perfHarmony = new Harmony(harmony.Id + ".perf");
        Perf.Log = log;
    }

    // The timing patches (Perf), apart from the probes: `unprobe` keeps them, `perf off` removes them.
    readonly Harmony perfHarmony;

    public void Unload() => Perf.Off();

    // Timing on, as the command does (the research plugin's TimeFromStart).
    public string StartTiming() => Perf.On(perfHarmony);

    // One command; an error is written as its result.
    public void Run(string line, Action<string> write)
    {
        try { Execute(line, write); }
        catch (Exception e)
        {
            var inner = e is TargetInvocationException { InnerException: { } i } ? i : e;
            write($"{line}: {inner.GetType().Name}: {inner.Message}");
        }
    }

    void Execute(string line, Action<string> write)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var argument = words.Length > 1 ? words[1] : "";
        var rest = line.Length > words[0].Length ? line[words[0].Length..].Trim() : "";
        write($"> {line}");
        switch (words[0])
        {
            case "help":
                write(Help);
                break;
            case "eval":
                write(Eval.Show(Eval.Run(rest)));
                break;
            case "inspect":
            case "methods":
                write(Eval.Inspect(Eval.Run(rest), words[0] == "methods"));
                break;
            case "set":
                var name = argument.TrimStart('$');
                var value = Eval.Run(rest[argument.Length..]);
                Eval.Variables[name] = value;
                write($"${name} = {Eval.Show(value)}");
                break;
            case "members":
                foreach (var type in Eval.TypesNamed(argument))
                {
                    write($"{type.FullName} : {type.BaseType?.Name}");
                    if (type.IsEnum)
                        write($"  values {string.Join(", ", Enum.GetNames(type).Select(n => $"{n}=0x{Convert.ToInt64(Enum.Parse(type, n)):X}"))}");
                    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                        write($"  prop {p.PropertyType.Name} {p.Name}");
                    foreach (var m in type.Methods())
                        write($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {GameCode.Signature(m)}");
                }
                break;
            case "code":
                foreach (var m in MethodsNamed(argument))
                    CodeResearch.DumpDisassembly(m, write);
                break;
            case "calls":
                foreach (var m in MethodsNamed(argument))
                    write($"{GameCode.Signature(m)}: {string.Join(", ", GameCode.Calls(m).Select(c => $"+0x{c.offset:X} {GameCode.NameAt(c.target)}"))}");
                break;
            case "callers":
                var pattern = new Regex(rest);
                foreach (var (caller, offset, target) in GameCode.CallersOf(pattern.IsMatch).Take(300))
                    write($"{GameCode.Signature(caller)} +0x{offset:X} -> {target}");
                break;
            case "shared":
                foreach (var m in MethodsNamed(argument))
                    write($"{GameCode.Signature(m)}: {string.Join(", ", GameCode.SharedWith(m).Select(GameCode.Signature))}");
                break;
            case "readers":
                // Which game methods read the static fields of a class (`readers cny`): constants kept in statics, e.g.
                // the names of the sequence effects, are found by who reads them.
                var readTypes = Eval.TypesNamed(argument).ToHashSet();
                var fieldNames = readTypes.SelectMany(t => GameCode.Fields(t).Where(f => f.isStatic).Select(f => (t, f.offset, f.name)))
                    .ToDictionary(x => (x.t, x.offset), x => x.name);
                var readerCount = 0;
                foreach (var reader in Game.Types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                             .Where(m => !m.IsGenericMethodDefinition))
                {
                    List<(Type type, int offset)> reads;
                    try { reads = GameCode.At(Game.NativeAddress(reader).ToInt64()).FirstOrDefault() == reader ? GameCode.StaticReads(reader) : null; }
                    catch (Exception) { continue; }
                    var mine = reads?.Where(r => readTypes.Contains(r.type)).Select(r => fieldNames.GetValueOrDefault((r.type, r.offset), $"+0x{r.offset:X}")).Distinct().ToList();
                    if (mine is not { Count: > 0 })
                        continue;
                    write($"{GameCode.Signature(reader)}: {string.Join(", ", mine)}");
                    if (++readerCount >= 200)
                        break;
                }
                break;
            case "aliases":
                foreach (var report in NamesGenerator.Generate(argument.Length > 0 ? string.Join(" ", words.Skip(1)) : System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "GameNames.g.cs"), Application.version))
                    write(report);
                break;
            case "types":
                var namePattern = new System.Text.RegularExpressions.Regex(argument, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (var type in Game.Types.Where(t => namePattern.IsMatch(t.FullName ?? t.Name)).Take(100))
                    write($"  {type.FullName} : {type.BaseType?.Name}");
                break;
            case "objects":
                foreach (var type in Eval.TypesNamed(argument).Where(t => typeof(UnityEngine.Object).IsAssignableFrom(t)))
                {
                    var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(type);
                    var number = 0;
                    // objects Type <regex>: only those whose name, image sprite or path matches.
                    var filter = words.Length > 2 ? new System.Text.RegularExpressions.Regex(string.Join(" ", words.Skip(2)), System.Text.RegularExpressions.RegexOptions.IgnoreCase) : null;
                    static string SpriteOf(UnityEngine.Object o) => o.TryCast<UnityEngine.UI.Image>()?.sprite?.name;
                    foreach (var o in UnityEngine.Resources.FindObjectsOfTypeAll(il2cppType)
                                 .Where(o => filter == null || filter.IsMatch(o.name) || (SpriteOf(o) is { } s && filter.IsMatch(s))
                                             || (o.TryCast<UnityEngine.Component>() is { } c && filter.IsMatch(PathOf(c.transform))))
                                 .Take(80))
                    {
                        var go = o.TryCast<UnityEngine.Component>()?.gameObject;
                        var canvas = o.TryCast<UnityEngine.Canvas>();
                        var sprite = SpriteOf(o);
                        // Each one kept as $o<n>, as the type it was asked for, for eval and inspect.
                        Eval.Variables[$"o{number}"] = ((object)o).CastTo(type);
                        write($"  $o{number++} {type.Name} '{o.name}'{(sprite != null ? $" sprite={sprite}" : "")}{(canvas != null ? $" order={canvas.sortingOrder} layer={canvas.sortingLayerName} mode={canvas.renderMode} root={canvas.isRootCanvas} scale={canvas.scaleFactor}" : "")}{(go != null ? $" active={go.activeInHierarchy} scene='{go.scene.name}' path={PathOf(go.transform)}" : "")}");
                    }
                }
                break;
            case "click":
                // click <path end>: the pointer events of a real click, sent to the object (its handlers up the hierarchy).
                {
                    var target = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.Transform>())
                        .Select(o => o.TryCast<UnityEngine.Transform>())
                        .FirstOrDefault(t => t != null && t.gameObject.activeInHierarchy && (t.name == rest || PathOf(t) == rest || PathOf(t).EndsWith("/" + rest)));
                    if (target == null) { write($"no active object {rest}"); break; }
                    var go = target.gameObject;
                    var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    {
                        button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                        position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, target.position),
                        clickCount = 1,
                    };
                    data.pointerPress = go;
                    data.rawPointerPress = go;
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(go, data, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(go, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(go, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                    var handler = UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(go, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(go, data, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                    write(handler != null ? $"clicked {PathOf(target)}: handled by {PathOf(handler.transform)}" : $"clicked {PathOf(target)}: nobody handles a click there");
                }
                break;
            case "hit":
                // hit <x> <y>: what the UI raycast finds at a screen point (pixels from the bottom left), the topmost first:
                // who takes a click there.
                {
                    if (words.Length < 3 || !float.TryParse(words[1], out var hx) || !float.TryParse(words[2], out var hy)) { write("hit <x> <y>"); break; }
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es == null) { write("no EventSystem"); break; }
                    var data = new UnityEngine.EventSystems.PointerEventData(es) { position = new UnityEngine.Vector2(hx, hy) };
                    var results = new Il2CppSystem.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(data, results);
                    write($"{results.Count} hits at {hx},{hy} (screen {UnityEngine.Screen.width}x{UnityEngine.Screen.height}):");
                    for (var i = 0; i < results.Count && i < 15; i++)
                    {
                        var r = results[i];
                        write($"  {r.sortingOrder} {r.sortingLayer} d={r.depth} {(r.gameObject != null ? PathOf(r.gameObject.transform) : "?")}");
                    }
                }
                break;
            case "shot":
                // shot <file.png>: the game's screen into a file, opaque, at full size.
                {
                    // The capture's alpha is 0 over the 3D scene (the colours are right): copied into an RGB texture, so
                    // the file is opaque wherever it is shown or cropped.
                    var shot = UnityEngine.ScreenCapture.CaptureScreenshotAsTexture();
                    var rgb = new UnityEngine.Texture2D(shot.width, shot.height, UnityEngine.TextureFormat.RGB24, false);
                    rgb.SetPixels32(shot.GetPixels32());
                    rgb.Apply();
                    var png = UnityEngine.ImageConversion.EncodeToPNG(rgb);
                    UnityEngine.Object.Destroy(shot);
                    UnityEngine.Object.Destroy(rgb);
                    System.IO.File.WriteAllBytes(argument, png);
                    write($"screenshot {png.Length} bytes into {argument}");
                }
                break;
            case "sprite":
                // sprite <name> <file.png>: a loaded sprite of the game into a file (its atlas is not readable: drawn
                // into a render texture first, then its rect read back).
                {
                    var sprite = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.Sprite>())
                        .Select(o => o.TryCast<UnityEngine.Sprite>()).FirstOrDefault(s => s != null && s.name == argument);
                    if (sprite == null || words.Length < 3) { write($"no sprite {argument} (sprite <name> <file.png>)"); break; }
                    var source = sprite.texture;
                    var rt = UnityEngine.RenderTexture.GetTemporary(source.width, source.height, 0, UnityEngine.RenderTextureFormat.ARGB32);
                    UnityEngine.Graphics.Blit(source, rt);
                    var before = UnityEngine.RenderTexture.active;
                    UnityEngine.RenderTexture.active = rt;
                    var r = sprite.textureRect;
                    var picture = new UnityEngine.Texture2D((int)r.width, (int)r.height, UnityEngine.TextureFormat.RGBA32, false);
                    picture.ReadPixels(new UnityEngine.Rect(r.x, r.y, r.width, r.height), 0, 0);
                    picture.Apply();
                    UnityEngine.RenderTexture.active = before;
                    UnityEngine.RenderTexture.ReleaseTemporary(rt);
                    var png = UnityEngine.ImageConversion.EncodeToPNG(picture);
                    UnityEngine.Object.Destroy(picture);
                    var file = string.Join(" ", words.Skip(2));
                    System.IO.File.WriteAllBytes(file, png);
                    write($"sprite {sprite.name} {r.width:0}x{r.height:0} (border {sprite.border}) into {file}");
                }
                break;
            case "bundle":
                // bundle load <file> | bundle spawn <file name> <asset path> [scale] | bundle drop: an AssetBundle of a mod's
                // (#62): its assets; a prefab of it put on the ground where the camera looks, with its renderers' shaders (whether
                // the game's build supports them).
                {
                    var verb = words.Length > 1 ? words[1] : "";
                    if (verb == "load" && words.Length > 2)
                    {
                        var path = string.Join(" ", words.Skip(2));
                        var loaded = UnityEngine.AssetBundle.LoadFromFile(path);
                        if (loaded == null) { write($"no bundle loaded from {path}"); break; }
                        bundles[System.IO.Path.GetFileName(path)] = loaded;
                        var names = loaded.GetAllAssetNames();
                        write($"bundle {loaded.name}: {names.Length} assets");
                        foreach (var asset in names.Where(n => n.EndsWith(".prefab")).Take(20))
                            write($"  {asset}");
                    }
                    else if (verb == "spawn" && words.Length > 3 && bundles.TryGetValue(words[2], out var bundle))
                    {
                        // A last word that is a number: the scale.
                        var scale = words.Length > 4 && float.TryParse(words[^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var given) ? given : 0f;
                        var prefab = bundle.LoadAsset<UnityEngine.GameObject>(string.Join(" ", words.Skip(3).Take(words.Length - 3 - (scale > 0 ? 1 : 0))));
                        if (prefab == null) { write("no such prefab"); break; }
                        var camera = UnityEngine.Camera.main;
                        // On the ground (y = 0) where the camera looks.
                        var forward = camera.transform.forward;
                        var at = forward.y < -0.01f ? camera.transform.position - forward * (camera.transform.position.y / forward.y) : camera.transform.position + forward * 25f;
                        var made = UnityEngine.Object.Instantiate(prefab, at, UnityEngine.Quaternion.identity);
                        if (scale > 0)
                            made.transform.localScale = UnityEngine.Vector3.one * scale;
                        made.name = "OE_Bundle_" + prefab.name;
                        write($"spawned {made.name} at {at}");
                        foreach (var renderer in made.GetComponentsInChildren<UnityEngine.Renderer>(true))
                            foreach (var material in renderer.sharedMaterials)
                                write($"  {renderer.GetIl2CppType().Name} {renderer.name}: {material?.name} shader {material?.shader?.name} supported {material?.shader?.isSupported}");
                        if (made.GetComponentInChildren<UnityEngine.Animator>(true) is { } animator)
                            write($"  animator {animator.runtimeAnimatorController?.name}");
                    }
                    else if (verb == "drop")
                    {
                        foreach (var o in UnityEngine.Object.FindObjectsOfType<UnityEngine.Transform>().Where(t => t.name.StartsWith("OE_Bundle_")).ToList())
                            UnityEngine.Object.Destroy(o.gameObject);
                        foreach (var b in bundles.Values)
                            b.Unload(true);
                        bundles.Clear();
                        write("dropped");
                    }
                    else
                        write("bundle load <file> | bundle spawn <file name> <asset path> [scale] | bundle drop");
                }
                break;
            case "tree":
                // tree <object name or path end> [depth]: the objects under it with their components and texts.
                {
                    var parts = argument.Split(' ');
                    var depthLimit = words.Length > 2 && int.TryParse(words[^1], out var d) ? d : 4;
                    var target = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.Transform>())
                        .Select(o => o.TryCast<UnityEngine.Transform>()).Where(t => t != null && t.gameObject.scene.IsValid())
                        .FirstOrDefault(t => t.name == parts[0] || PathOf(t).EndsWith(parts[0]));
                    if (target == null) { write($"no object {parts[0]}"); break; }
                    void Tree(UnityEngine.Transform t, int depth)
                    {
                        var components = string.Join(",", t.GetComponents<UnityEngine.Component>().Select(c => c.GetIl2CppType().Name).Where(n => n != "Transform" && n != "RectTransform"));
                        var text = t.GetComponent<TMPro.TMP_Text>() is { } tmp ? $" \"{tmp.text?.Replace("\n", " ")}\"" : "";
                        var rect = t.TryCast<UnityEngine.RectTransform>() is { } r ? $" {r.rect.width:0}x{r.rect.height:0}" : "";
                        write($"{new string(' ', depth * 2)}{(t.gameObject.activeSelf ? "" : "-")}{t.name}{rect} [{components}]{text}");
                        if (depth < depthLimit)
                            for (var i = 0; i < t.childCount; i++)
                                Tree(t.GetChild(i), depth + 1);
                    }
                    Tree(target, 0);
                }
                break;
            case "holders":
                foreach (var type in Eval.TypesNamed(argument))
                    foreach (var p in Game.Types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                                 .Where(p => p.PropertyType == type))
                        write($"  {(p.GetMethod?.IsStatic == true ? "static " : "")}{p.DeclaringType?.FullName}.{p.Name} : {type.Name}");
                break;
            case "fields":
                foreach (var type in Eval.TypesNamed(argument))
                {
                    write($"{type.FullName}:");
                    foreach (var f in GameCode.Fields(type))
                        write($"  {(f.isStatic ? "static " : "")}+0x{f.offset:X} {f.type} {f.name}");
                }
                break;
            case "slot":
                foreach (var type in Eval.TypesNamed(argument))
                    write(CodeResearch.VirtualSlot(type, Convert.ToInt32(words[2], 16)));
                break;
            case "string":
                write($"a.{argument}() = {(CodeResearch.HiddenString(argument) is { } s ? $"\"{s}\"" : "no such method")}");
                break;
            case "strings":
                var valuePattern = new Regex(rest);
                foreach (var (method, text) in CodeResearch.HiddenStrings().Where(h => valuePattern.IsMatch(h.value)).Take(100))
                    write($"a.{method}() = \"{text}\"");
                break;
            case "probe":
                var seconds = words.Length > 2 ? float.Parse(words[2], CultureInfo.InvariantCulture) : 10f;
                probeUntil = Time.unscaledTime + seconds;
                var patched = Probe.Calls(harmony, MethodsNamed(argument), () => Time.unscaledTime < probeUntil, text =>
                {
                    probed.Enqueue(text);
                    log(text);
                });
                write($"probing {seconds} s: {Members.Names(patched)}");
                break;
            case "probed":
                while (probed.TryDequeue(out var call))
                    write(call);
                break;
            case "screen":
                write(ScreenSpike.Run(argument));
                break;
            case "growth":
                foreach (var growthLine in words.Length > 1 && words[1] == "dump" ? GrowthDump() : Growth())
                    write(growthLine);
                break;
            case "fps":
                foreach (var report in Perf.Report(words.Length > 1 && float.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var perfSeconds) ? perfSeconds : 10f))
                    write(report);
                break;
            case "perf":
                write(argument == "off" ? Perf.Off() : argument == "add" && words.Length > 2 ? Perf.Add(string.Join(" ", words.Skip(2))) : Perf.On(perfHarmony));
                break;
            case "unprobe":
                harmony.UnpatchSelf();
                write("probes removed");
                break;
            default:
                write($"unknown command {words[0]} (help: the list)");
                break;
        }
    }

    // Type.method: the method's overloads; Type.*: the type's own methods.
    static string PathOf(UnityEngine.Transform t) => t.parent != null ? $"{PathOf(t.parent)}/{t.name}" : t.name;

    static IEnumerable<MethodInfo> MethodsNamed(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot < 0)
            throw new FormatException($"Type.method expected, not {name}");
        var (type, method) = (name[..dot], name[(dot + 1)..]);
        return Eval.TypesNamed(type).SelectMany(t => t.Methods()).Where(m => method == "*" || m.Name == method);
    }

    // The selected hero's side's towns (their hiring buildings: per unit, the weekly growth and how many to hire now) and
    // its unit dwellings on the map (their units and weekly growth, the bonus they give a town): to compare before a save
    // and after loading it (issue #7).
    static IEnumerable<string> Growth()
    {
        if (Game.SelectedHero?.HeroSide is not { } side || Game.MapObjects() is not { } objects)
        {
            yield return "no selected hero or no map";
            yield break;
        }
        yield return $"side {side.index}";
        foreach (var mapObject in objects)
        {
            if (mapObject?.ObjectData is not { } data || data.ownerSide != side.index)
                continue;
            if (data.TryCast<Hex.Session.Data.ObjCity>() is { } city)
            {
                yield return $"TOWN {city.cityName} ({data.sidConfig} #{data.idMapObject})";
                var hires = city.buildings?.hires;
                for (var i = 0; hires != null && i < hires.Count; i++)
                {
                    var hire = hires[i];
                    var sets = hire?.assortment?.unitSets;
                    for (var j = 0; sets != null && j < sets.Count; j++)
                        if (sets[j] is { } set)
                            yield return $"  {hire.sid} lvl {hire.level} built={hire.isConstructed}: {(set.sids?.Count > 0 ? set.sids[0] : "?")} weekly {set.weeklyIncrement}, to hire {set.currentAmount}";
                }
            }
            else if (data.TryCast<Hex.Session.Data.ObjHire>() is { } dwelling)
            {
                var sets = dwelling.assortmentData?.unitSets;
                var units = sets == null ? "" : string.Join(", ", Enumerable.Range(0, sets.Count).Select(k => sets[k]).Where(s => s != null)
                    .Select(s => $"{(s.sids?.Count > 0 ? s.sids[0] : "?")} weekly {s.weeklyIncrement}, to hire {s.currentAmount}"));
                yield return $"DWELLING {data.sidConfig} #{data.idMapObject}: {units}";
            }
        }
    }

    // Every value of the selected hero's side's towns (their map objects, the game's live logic with the data under it)
    // and of the side wrapper, two levels deep: numbers, flags, strings, the counts of lists and dictionaries. To diff a
    // dump before a save with one after the load (issue #7).
    static IEnumerable<string> GrowthDump()
    {
        if (Game.MapObjects() is not { } objects)
            yield break;
        // The selected hero's side, else the first player's (0): after a load no hero is selected yet.
        var hero = Game.SelectedHero;
        var sideIndex = hero?.HeroSide?.index ?? 0;
        var lines = new List<string>();
        if (hero != null)
            Dump("side", hero.SideOwner, 0, lines, new HashSet<IntPtr>());
        foreach (var mapObject in objects)
            if (mapObject?.ObjectData is { } data && data.ownerSide == sideIndex && data.TryCast<Hex.Session.Data.ObjCity>() != null)
                Dump($"town#{data.idMapObject}", mapObject, 0, lines, new HashSet<IntPtr>());
        foreach (var line in lines)
            yield return line;
    }

    static void Dump(string path, object value, int depth, List<string> lines, HashSet<IntPtr> seen)
    {
        if (value is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase obj || obj.Pointer == IntPtr.Zero || !seen.Add(obj.Pointer))
            return;
        foreach (var property in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.Name is "Pointer" or "ObjectClass" or "WasCollected" || property.Name.StartsWith("_"))
                continue;
            object v;
            try { v = property.GetValue(obj); }
            catch { continue; }
            var name = $"{path}.{property.Name}";
            switch (v)
            {
                case null:
                    lines.Add($"{name} = null");
                    break;
                case int or float or bool or string or Enum:
                    lines.Add($"{name} = {v}");
                    break;
                case Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase child:
                    var count = child.GetType().GetProperty("Count")?.GetValue(child);
                    if (count != null)
                        lines.Add($"{name} : {child.GetType().Name} count {count}");
                    else if (depth < 2 && child.GetType().Assembly == typeof(Hex.UI.ELang).Assembly)
                        Dump(name, child, depth + 1, lines, seen);
                    break;
            }
        }
    }
}