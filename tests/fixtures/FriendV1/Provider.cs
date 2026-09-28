[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AssemblyQuality.Fixtures.FriendConsumer")]

namespace Friend;

/// <summary>Version one: both internals exist, and the consumer is granted them.</summary>
public static class Provider
{
    internal static int Secret(int value) => value + 1;

    internal static int Kept(int value) => value + 2;

    /// <summary>Public, so a reference to it needs no grant.</summary>
    public static int Open() => 3;
}
