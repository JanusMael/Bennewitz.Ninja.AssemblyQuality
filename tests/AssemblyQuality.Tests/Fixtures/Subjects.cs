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
    /// <summary>AQ1001: the defaulted token.</summary>
    public void Defaulted(CancellationToken token = default) { }

    /// <summary>AQ1001: the same parameter, required — this one is correct.</summary>
    public void Required(CancellationToken token) { }

    /// <summary>AQ1002: a leaked serializer type.</summary>
    public JsonNode? Leaked() => null;

    /// <summary>AQ1002: the same leak, wrapped — a shape that fools an outer-type-only check.</summary>
    public Task<JsonNode?> LeakedNested() => Task.FromResult<JsonNode?>(null);
}

/// <summary>A type with nothing wrong with it.</summary>
public sealed class Clean
{
    /// <summary>Required token, own return type.</summary>
    public Clean Fine(CancellationToken token) => this;
}
