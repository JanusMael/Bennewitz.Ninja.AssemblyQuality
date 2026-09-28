// The `Two` segment here shadows a root namespace that only VERSION TWO of its dependency exports.
// So BNAQ1004 reports it exactly when the scan resolves the reference to the version that actually
// loads beside this assembly, and misses it when the scan resolves somewhere else.
namespace Dependent.Two;

/// <summary>Compiled against version one; the reference in its body is what ties the two together.</summary>
public sealed class Consumer
{
    /// <summary>Names a version-one type, so the compiled assembly references the dependency.</summary>
    internal static object Make() => new One.Things.Thing();
}
