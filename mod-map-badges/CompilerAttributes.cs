// We compile against the .NET 6 runtime assemblies BepInEx ships (not reference assemblies). They contain these
// attributes only as internal types, so the compiler neither sees them nor embeds its own copy. Declaring them
// here lets lambdas, records and similar constructs compile.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public NullableAttribute(byte flag) { }
        public NullableAttribute(byte[] flags) { }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public NullableContextAttribute(byte flag) { }
    }
}
