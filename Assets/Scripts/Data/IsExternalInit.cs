namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler marker that enables <c>init</c> accessors. Unity 6's netstandard profile does not
    /// ship it, and definitions must be immutable after load (ARCHITECTURE §Never), so this
    /// assembly declares its own.
    /// <para>
    /// ponytail: public, not internal, because setting an <c>init</c> property needs this type
    /// accessible at the call site — an internal copy would stop EditMode tests from building
    /// def fixtures inline (T-072 and every formula test after it). Ceiling: a referenced NuGet
    /// DLL declaring its own public copy would be a CS0433 ambiguity; if that happens, make this
    /// internal again and add <c>InternalsVisibleTo</c> for the test assemblies.
    /// </para>
    /// </summary>
    public static class IsExternalInit
    {
    }
}
