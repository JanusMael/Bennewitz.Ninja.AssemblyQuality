// The FIRST segment here is `System`, which is genuinely a root namespace of a referenced
// assembly. Under the rule as intended this is correct code and must not be reported: a
// namespace's own root is the tree it already lives in. Under the rule without that exclusion it
// is reported — which is the regression this fixture exists to hold.
//
// Extending the System tree is not something to do in shipping code. It is done here because the
// only faithful subject for "own root equals a referenced root" is an assembly that really has
// one, and reflection cannot be given a hypothetical.
namespace System.AssemblyQualityProbe;

/// <summary>A public type whose namespace root is also a referenced assembly's root.</summary>
public sealed class RootSharing;
