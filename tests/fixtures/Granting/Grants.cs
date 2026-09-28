using System.Runtime.CompilerServices;

// One grant to an assembly the solution builds: allowed.
[assembly: InternalsVisibleTo("AssemblyQuality.Tests")]

// A grant to another repository's assembly: allowed only when declared in the External file.
[assembly: InternalsVisibleTo("Friend.Elsewhere")]

// The namespace form of this solution's test assembly. It compiles, ships, and grants nothing,
// because a grant is matched against an assembly NAME and no assembly is called this.
[assembly: InternalsVisibleTo("Bennewitz.Ninja.Granting.Tests")]

// A name that exists nowhere.
[assembly: InternalsVisibleTo("Nobody.Anywhere")]

namespace Granting;

/// <summary>A public type, so the test project can reach this assembly by <c>typeof</c>.</summary>
public sealed class Marker;
