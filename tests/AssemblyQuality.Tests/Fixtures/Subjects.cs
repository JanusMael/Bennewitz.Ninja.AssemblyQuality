using System.Text.Json.Nodes;

namespace AssemblyQuality.Tests.Fixtures;

/// <summary>
/// Real types with real violations, because every rule here reads compiled metadata.
/// </summary>
/// <remarks>
/// ⭐ A mock cannot be reflected over. The only faithful fixture for a reflection rule is a type
/// that actually has the shape in question, compiled by the same compiler as the code it guards.
/// </remarks>
public sealed class Offender
{
    /// <summary>BNAQ1001: the defaulted token.</summary>
    public void Defaulted(CancellationToken token = default) { }

    /// <summary>BNAQ1001: the same parameter, required — this one is correct.</summary>
    public void Required(CancellationToken token) { }

    /// <summary>BNAQ1002: a leaked serializer type.</summary>
    public JsonNode? Leaked() => null;

    /// <summary>BNAQ1002: the same leak, wrapped — a shape that fools an outer-type-only check.</summary>
    public Task<JsonNode?> LeakedNested() => Task.FromResult<JsonNode?>(null);
}

/// <summary>
/// BNAQ1001: the overload pair that answers a defaulted-token finding without fixing it.
/// </summary>
public sealed class Overloaded
{
    /// <summary>Exists only to omit the token — reported.</summary>
    public void Build(int left, string? options = null) => Build(left, options, CancellationToken.None);

    /// <summary>The token is required here, so this one is correct on its own.</summary>
    public void Build(int left, string? options, CancellationToken token) { }

    /// <summary>Same name, a different operation: not a shorter spelling of the sibling below.</summary>
    public void Parse(Stream source) { }

    /// <summary>Takes a token; the overload above is not its abbreviation.</summary>
    public void Parse(string text, CancellationToken token) { }
}

/// <summary>A type with nothing wrong with it.</summary>
public sealed class Clean
{
    /// <summary>Required token, own return type.</summary>
    public Clean Fine(CancellationToken token) => this;
}
