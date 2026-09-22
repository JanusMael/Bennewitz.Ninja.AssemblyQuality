// The segment `System` shadows the root namespace of a referenced assembly, which is exactly the
// defect AQ1004 exists for. Nothing in this file uses a qualified name through it — which is the
// point: the bug is invisible until somebody does, possibly years later.
namespace AssemblyQuality.Tests.Fixtures.Shadow.System;

/// <summary>A public type whose namespace shadows a referenced root.</summary>
public sealed class Shadowing;
