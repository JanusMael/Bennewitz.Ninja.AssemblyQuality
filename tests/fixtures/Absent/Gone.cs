namespace Absent;

/// <summary>A type Orphan names and nobody can load at run time.</summary>
public sealed class Gone;

/// <summary>A base class Orphan derives a public type from, so that type cannot load at all.</summary>
public class Base;

/// <summary>An interface Orphan implements on a public type, the other way a type depends on its supertypes.</summary>
public interface IContract;
