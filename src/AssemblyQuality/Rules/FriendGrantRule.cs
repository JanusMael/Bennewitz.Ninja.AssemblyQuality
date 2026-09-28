using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bennewitz.Ninja.AssemblyQuality.Rules;

/// <summary>
/// Every <c>InternalsVisibleTo</c> grant names an assembly the caller allows.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A wrong grant fails silently.</b> A grant is matched against an assembly's simple name, so
/// one that names a namespace (<c>Bennewitz.Ninja.X</c> where the assembly is <c>X</c>) or a
/// misspelled assembly compiles, ships, and grants nothing. And a grant to an assembly nobody
/// declared exposes internals nobody reviewed. Neither fails anything until something needs the
/// grant, or until someone notices what it let in.
/// </para>
/// <para>
/// ⭐ <b>Read from the compiled assembly, so every spelling is covered</b>: the attribute in source,
/// the SDK's <c>&lt;InternalsVisibleTo&gt;</c> item, and an <c>&lt;AssemblyAttribute&gt;</c> item all
/// end up as the same attribute here.
/// </para>
/// <para>
/// ⚠ <b>No default allow-list, deliberately.</b> A consumer scans its shipped assemblies and not its
/// tests, so "the assemblies in the scan" would report every grant to a test assembly. The caller
/// passes what is allowed: in the Bennewitz.Ninja family, the solution's assemblies plus the names in
/// <c>AssemblyInfo.InternalsVisibleTo.External.cs</c>. Configured with nothing, this reports
/// <see cref="AssemblyRuleResult.Inspected"/> of zero, as <see cref="ForbiddenReferenceRule"/> does.
/// </para>
/// <para>
/// ⚠ <b>Names compare case-insensitively, and a <c>PublicKey=</c> part is ignored</b>, as the runtime
/// compares simple names. A keyed grant is judged by its name like any other.
/// </para>
/// </remarks>
public sealed class FriendGrantRule : IAssemblyRule
{
    private readonly HashSet<string> _allowed;

    /// <summary>Allows nothing; reports that it inspected nothing.</summary>
    public FriendGrantRule()
        : this([])
    {
    }

    /// <summary>Allows grants to <paramref name="allowedAssemblies"/> and nothing else.</summary>
    /// <param name="allowedAssemblies">
    /// Simple assembly names a grant may name: the solution's own assemblies, and any declared
    /// cross-repository grantees.
    /// </param>
    public FriendGrantRule(IEnumerable<string> allowedAssemblies)
    {
        ArgumentNullException.ThrowIfNull(allowedAssemblies);
        _allowed = new HashSet<string>(allowedAssemblies, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string Id => "BNAQ1005";

    /// <inheritdoc />
    public string Summary => "Every InternalsVisibleTo grant names an assembly the caller allows.";

    /// <inheritdoc />
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_allowed.Count == 0)
        {
            return AssemblyRuleResult.Clean(0);
        }

        List<AssemblyFinding> findings = [];
        int inspected = 0;

        foreach (Assembly assembly in context.Assemblies)
        {
            foreach (CustomAttributeData attribute in assembly.GetCustomAttributesData())
            {
                if (attribute.AttributeType.FullName != typeof(InternalsVisibleToAttribute).FullName
                    || attribute.ConstructorArguments is not [{ Value: string grant }])
                {
                    continue;
                }

                inspected++;

                if (_allowed.Contains(SimpleName(grant)))
                {
                    continue;
                }

                findings.Add(new AssemblyFinding(
                    Id,
                    assembly.GetName().Name ?? "?",
                    grant,
                    $"This assembly grants its internals to '{SimpleName(grant)}', which is not an allowed "
                    + "assembly. A grant matches an assembly's simple name only, so one that names a "
                    + "namespace or a misspelled assembly compiles, ships and grants nothing, and one to an "
                    + "undeclared assembly exposes internals nobody reviewed. Correct it to the assembly's "
                    + "name, declare it as a deliberate cross-repository grant, or remove it."));
            }
        }

        return new AssemblyRuleResult(findings, inspected);
    }

    /// <summary>
    /// The simple name a grant is matched by: everything before the first comma, which is where a
    /// <c>PublicKey=</c> part begins.
    /// </summary>
    private static string SimpleName(string grant) => grant.Split(',')[0].Trim();
}
