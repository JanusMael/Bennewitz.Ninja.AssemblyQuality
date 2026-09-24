// Orphan's only route to the `System` root is System.Runtime, which at run time is a facade: it
// exports nothing and forwards every type to CoreLib. A rule reading exported types alone sees no
// `System` root behind it and passes this shadow; one that reads forwarded types reports it.
namespace Orphan.System;

/// <summary>A public type whose namespace shadows a root reachable only through a facade.</summary>
public sealed class BehindAFacade;
