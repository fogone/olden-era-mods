using System.Text;
using AssetsTools.NET;

namespace Nobirds.OldenEra.Modding.Bundle;

// Type trees as Unity 6000.0 writes them into an AssetBundle, for the objects this tool adds that the model's bundle
// may have no type of: a script (MonoScript), a script's component (MonoBehaviour: its base and its serialized
// fields), the timeline player (PlayableDirector). A bundle is read by the game through these trees: field by field,
// by name.
static class TypeTrees
{
    // A node: its depth, type, name, size in bytes (-1: of variable size), Unity's flags (0x4000: aligned after it).
    public record Node(int Level, string Type, string Name, int Size, uint Meta, bool Array = false, ushort Version = 1);

    public const uint Aligned = 0x4000;

    static IEnumerable<Node> Str(int level, string name, uint meta = 0, uint childMeta = 0) => new[]
    {
        new Node(level, "string", name, -1, meta | 0x8000),
        new Node(level + 1, "Array", "Array", -1, childMeta | Aligned, Array: true),
        new Node(level + 2, "int", "size", 4, childMeta),
        new Node(level + 2, "char", "data", 1, childMeta),
    };

    public static IEnumerable<Node> PPtr(int level, string name, string type, uint meta = 0, uint childMeta = 0) => new[]
    {
        new Node(level, $"PPtr<{type}>", name, 12, meta),
        new Node(level + 1, "int", "m_FileID", 4, childMeta),
        new Node(level + 1, "SInt64", "m_PathID", 8, childMeta),
    };

    static IEnumerable<Node> Vector(int level, string name, IEnumerable<Node> element) =>
        new[] { new Node(level, "vector", name, -1, Aligned), new Node(level + 1, "Array", "Array", -1, Aligned, Array: true), new Node(level + 2, "int", "size", 4, 0) }
            .Concat(element);

    public static IEnumerable<Node> Vector3(int level, string name) => new[]
    {
        new Node(level, "Vector3f", name, 12, 0), new Node(level + 1, "float", "x", 4, 0),
        new Node(level + 1, "float", "y", 4, 0), new Node(level + 1, "float", "z", 4, 0),
    };

    public static IEnumerable<Node> PPtrArray(int level, string name, string type) => Vector(level, name, PPtr(level + 2, "data", type));

    public static IEnumerable<Node> Vector3Array(int level, string name) => Vector(level, name, Vector3(level + 2, "data"));

    // A curve (UnityEngine.AnimationCurve), as Unity writes it.
    public static IEnumerable<Node> AnimationCurve(int level, string name) =>
        new[] { new Node(level, "AnimationCurve", name, -1, 0, Version: 2) }
            .Concat(Vector(level + 1, "m_Curve", new[] { new Node(level + 3, "Keyframe", "data", 28, 0, Version: 3) }
                .Concat(new[] { "time", "value", "inSlope", "outSlope" }.Select(f => new Node(level + 4, "float", f, 4, 0)))
                .Append(new Node(level + 4, "int", "weightedMode", 4, 0))
                .Concat(new[] { "inWeight", "outWeight" }.Select(f => new Node(level + 4, "float", f, 4, 0)))))
            .Concat(new[] { "m_PreInfinity", "m_PostInfinity", "m_RotationOrder" }.Select(f => new Node(level + 1, "int", f, 4, 0)));

    // A serialized dictionary of the game's (its pairs' list), written empty: the element's type does not matter then.
    public static IEnumerable<Node> EmptyDictionary(int level, string type, string name) =>
        new[] { new Node(level, type, name, -1, 0) }.Concat(Vector(level + 1, "keyValuePairs", new[] { new Node(level + 3, "int", "data", 4, 0) }));

    public static TypeTreeType MonoScript() => Make(115, 0xFFFF, new[]
    {
        new[] { new Node(0, "MonoScript", "Base", -1, 0x8000, Version: 7) },
        Str(1, "m_Name", 0x80001, 0x80001),
        new[] { new Node(1, "int", "m_ExecutionOrder", 4, 0x10), new Node(1, "Hash128", "m_PropertiesHash", 16, 0x10) },
        Enumerable.Range(0, 16).Select(i => new Node(2, "UInt8", $"bytes[{i}]", 1, 0x10)),
        Str(1, "m_ClassName", 0x10, 0x11),
        Str(1, "m_Namespace", 0x10, 0x11),
        Str(1, "m_AssemblyName", 0x10, 0x11),
    }.SelectMany(n => n));

    // A script's component: the MonoBehaviour base, then the script's serialized fields.
    public static TypeTreeType MonoBehaviour(ushort scriptIndex, IEnumerable<Node> fields) => Make(114, scriptIndex, new[]
    {
        new[] { new Node(0, "MonoBehaviour", "Base", -1, 0x8000) },
        PPtr(1, "m_GameObject", "GameObject", 0x41, 0x41),
        new[] { new Node(1, "UInt8", "m_Enabled", 1, 0x4101) },
        PPtr(1, "m_Script", "MonoScript", 0, 0x800001),
        Str(1, "m_Name", 0x80001, 0x80001),
        fields,
    }.SelectMany(n => n));

    public static TypeTreeType PlayableDirector() => Make(320, 0xFFFF, new[]
    {
        new[] { new Node(0, "PlayableDirector", "Base", -1, 0, Version: 3) },
        PPtr(1, "m_GameObject", "GameObject"),
        new[] { new Node(1, "UInt8", "m_Enabled", 1, Aligned) },
        PPtr(1, "m_PlayableAsset", "Object"),
        new[]
        {
            new Node(1, "int", "m_InitialState", 4, 0), new Node(1, "int", "m_WrapMode", 4, 0),
            new Node(1, "int", "m_DirectorUpdateMode", 4, 0), new Node(1, "double", "m_InitialTime", 8, 0),
        },
        Vector(1, "m_SceneBindings", new[] { new Node(3, "DirectorGenericBinding", "data", 24, 0) }.Concat(PPtr(4, "key", "Object")).Concat(PPtr(4, "value", "Object"))),
        new[]
        {
            new Node(1, "ExposedReferenceTable", "m_ExposedReferences", -1, 0),
            new Node(2, "map", "m_References", -1, Aligned), new Node(3, "Array", "Array", -1, 0, Array: true),
            new Node(4, "int", "size", 4, 0), new Node(4, "pair", "data", -1, 0), new Node(5, "_string", "first", -1, 0),
        },
        Str(6, "id", Aligned),
        PPtr(5, "second", "Object"),
    }.SelectMany(n => n));

    public static TypeTreeType BoxCollider() => Make(65, 0xFFFF, new[]
    {
        new[] { new Node(0, "BoxCollider", "Base", -1, 0, Version: 3) },
        PPtr(1, "m_GameObject", "GameObject"),
        PPtr(1, "m_Material", "PhysicsMaterial"),
        new[]
        {
            new Node(1, "BitField", "m_IncludeLayers", 4, 0, Version: 2), new Node(2, "unsigned int", "m_Bits", 4, 0),
            new Node(1, "BitField", "m_ExcludeLayers", 4, 0, Version: 2), new Node(2, "unsigned int", "m_Bits", 4, 0),
            new Node(1, "int", "m_LayerOverridePriority", 4, 0), new Node(1, "bool", "m_IsTrigger", 1, 0),
            new Node(1, "bool", "m_ProvidesContacts", 1, Aligned), new Node(1, "bool", "m_Enabled", 1, Aligned),
        },
        Vector3(1, "m_Size"),
        Vector3(1, "m_Center"),
    }.SelectMany(n => n));

    static TypeTreeType Make(int typeId, ushort scriptIndex, IEnumerable<Node> nodes)
    {
        var list = nodes.ToList();
        var strings = new List<byte>();
        uint Offset(string s)
        {
            var at = (uint)strings.Count;
            strings.AddRange(Encoding.ASCII.GetBytes(s));
            strings.Add(0);
            return at;
        }
        var treeNodes = list.Select((n, i) => new TypeTreeNode
        {
            Version = n.Version, Level = (byte)n.Level, TypeFlags = n.Array ? TypeTreeNodeFlags.Array : TypeTreeNodeFlags.None,
            TypeStrOffset = Offset(n.Type), NameStrOffset = Offset(n.Name), ByteSize = n.Size, Index = (uint)i, MetaFlags = n.Meta,
        }).ToList();
        // Unity marks every node above an aligned one (0x8000) and reads alignment inside only then.
        for (var i = 0; i < treeNodes.Count; i++)
        {
            if ((treeNodes[i].MetaFlags & 0xC000) == 0) continue;
            var level = treeNodes[i].Level;
            for (var j = i - 1; j >= 0 && level > 0; j--)
                if (treeNodes[j].Level < level)
                {
                    treeNodes[j].MetaFlags |= 0x8000;
                    level = treeNodes[j].Level;
                }
        }
        var type = new TypeTreeType
        {
            TypeId = typeId, ScriptTypeIndex = scriptIndex, ScriptIdHash = Hash128.NewBlankHash(), TypeHash = Hash128.NewBlankHash(),
            TypeDependencies = Array.Empty<int>(), TypeBlob = new TypeTreeBlob(), TypeBlobIsDefinition = true,
        };
        type.StringBufferBytes = strings.ToArray();
        type.Nodes = treeNodes;
        return type;
    }
}
