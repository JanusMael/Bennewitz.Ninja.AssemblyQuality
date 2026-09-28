namespace FriendConsumer;

/// <summary>Calls both of version one's internals, and its public member.</summary>
public static class Use
{
    /// <summary>Three references into the provider: two internal, one public.</summary>
    public static int Call() => Friend.Provider.Secret(1) + Friend.Provider.Kept(1) + Friend.Provider.Open();
}
