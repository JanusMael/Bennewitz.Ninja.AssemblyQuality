using System.Reflection;

namespace Bennewitz.Ninja.AssemblyQuality.Rules;

/// <summary>
/// An assembly references none of the assemblies its layer forbids.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Layering is a claim that nothing enforces until something does.</b> "The SDK has no UI
/// dependency" holds until one <c>using</c> in one file quietly adds the reference, and nothing
/// fails: it builds, it ships, and the first symptom is a console consumer dragging a windowing
/// stack behind it. The reference list is the only place that claim is checkable.
/// </para>
/// <para>
/// ⚠ <b>No default forbidden set, deliberately.</b> Which assembly is out of bounds is entirely a
/// property of the consumer's architecture, and a guessed default would either fire on everyone or
/// on no one. Configured with nothing, this reports zero findings and zero
/// <see cref="AssemblyRuleResult.Inspected"/> — the honest signal that it was never asked a
/// question, rather than a clean bill of health.
/// </para>
/// <para>
/// ⛔ <b>Direct references only.</b> A transitive one is a property of what you depend on, not of
/// what you wrote, and reporting it would blame the wrong assembly.
/// </para>
/// </remarks>
public sealed class ForbiddenReferenceRule : IAssemblyRule
{
    private readonly string[] _forbidden;

    /// <summary>Forbids nothing; reports that it inspected nothing.</summary>
    public ForbiddenReferenceRule()
        : this([])
    {
    }

    /// <summary>Forbids a direct reference to any assembly whose simple name starts with one of these.</summary>
    /// <param name="forbiddenPrefixes">
    /// Simple-name prefixes — <c>Avalonia</c>, <c>Microsoft.AspNetCore</c>. Prefixes rather than
    /// exact names, because a framework ships as a family and forbidding one of its assemblies
    /// while admitting the other twelve is not the rule anybody meant.
    /// </param>
    public ForbiddenReferenceRule(IEnumerable<string> forbiddenPrefixes)
    {
        ArgumentNullException.ThrowIfNull(forbiddenPrefixes);
        _forbidden = [.. forbiddenPrefixes.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public string Id => "AQ1003";

    /// <inheritdoc />
    public string Summary => "An assembly references none of the assemblies its layer forbids.";

    /// <inheritdoc />
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_forbidden.Length == 0)
        {
            return AssemblyRuleResult.Clean(0);
        }

        List<AssemblyFinding> findings = [];
        int inspected = 0;

        foreach (Assembly assembly in context.Assemblies)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                inspected++;
                string name = reference.Name ?? string.Empty;

                if (!_forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                findings.Add(new AssemblyFinding(
                    Id,
                    assembly.GetName().Name ?? "?",
                    name,
                    $"This assembly directly references {name}, which its layer forbids. Nothing "
                    + "about the build says so, and the first symptom is a consumer dragging that "
                    + "dependency behind it. Move whatever needs it above this layer."));
            }
        }

        return new AssemblyRuleResult(findings, inspected);
    }
}
