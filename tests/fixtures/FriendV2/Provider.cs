namespace Friend;

/// <summary>Version two: <c>Secret</c> is gone, and no grant names the consumer any more.</summary>
public static class Provider
{
    internal static int Kept(int value) => value + 2;

    /// <summary>Public, so a reference to it needs no grant.</summary>
    public static int Open() => 3;
}
