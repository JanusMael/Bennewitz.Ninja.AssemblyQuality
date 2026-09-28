// A public type that only IMPLEMENTS an interface from the assembly that will not load, with no base
// class there. The runtime resolves a type's interfaces when it loads the type, so this fails the way
// Descendant does. Kept as its own fixture because a sink implementing a logging framework's interface
// is the common shape, and a fix covering base classes alone would pass every other test here.
//
// The lambda below makes the compiler emit a nested closure class, Implementer+<>c. It DOES load when
// Implementer does not, and reading its Namespace resolves Implementer and throws. Measured:
// AppServices' BucketedRollingFileSink took BNAQ1004's internal-types scan down this way.
namespace Orphan.Contracts;

/// <summary>Implements an interface from Absent.</summary>
public sealed class Implementer : global::Absent.IContract
{
    /// <summary>A lambda, so the compiler emits a nested closure class under this type.</summary>
    internal static Func<int, int> Increment() => value => value + 1;
}
