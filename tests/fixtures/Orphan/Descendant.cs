// A public type whose BASE class lives in the assembly that will not load. Unlike Unreadable, whose
// missing type appears only in a member signature, this one cannot be loaded at all, so asking
// Orphan for its exported types throws FileNotFoundException rather than returning them. Measured:
// ScopedEditors' view-models deriving from CommunityToolkit.Mvvm took every rule's scan down this way.
namespace Orphan.Lineage;

/// <summary>Derives from a type in Absent.</summary>
public class Descendant : global::Absent.Base;
