// The `Absent` segment here shadows the root of a referenced assembly — the BNAQ1004 defect — and
// the rule cannot see it, because that assembly never loads and its root never reaches the
// comparison. What the rule CAN do is say so. `global::` below is the scar the shadow forces.
namespace Orphan.Absent;

/// <summary>Readable: nothing in its signatures is missing.</summary>
public sealed class Readable
{
    /// <summary>A required token, so BNAQ1001 has something to count here.</summary>
    public int Value(CancellationToken token) => 0;
}

/// <summary>Unreadable: a signature names the assembly that will not load.</summary>
public sealed class Unreadable
{
    /// <summary>Reading this member's return type throws.</summary>
    public global::Absent.Gone? Lost() => null;
}
