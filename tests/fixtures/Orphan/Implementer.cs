// A public type that only IMPLEMENTS an interface from the assembly that will not load, with no base
// class there. The runtime resolves a type's interfaces when it loads the type, so this fails the way
// Descendant does. Kept as its own fixture because a sink implementing a logging framework's interface
// is the common shape, and a fix covering base classes alone would pass every other test here.
namespace Orphan.Contracts;

/// <summary>Implements an interface from Absent.</summary>
public sealed class Implementer : global::Absent.IContract;
