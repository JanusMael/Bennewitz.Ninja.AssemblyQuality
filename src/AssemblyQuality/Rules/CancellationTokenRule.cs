using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Bennewitz.Ninja.AssemblyQuality.Rules;

/// <summary>
/// No public method takes a <see cref="CancellationToken"/> with a default value.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A defaulted token is a cancellation contract that silently opts out.</b> Write
/// <c>CancellationToken token = default</c> and every caller who forgets it gets
/// <see cref="CancellationToken.None"/> — an operation that cannot be cancelled, chosen by nobody,
/// visible nowhere. The call compiles, runs, and hangs on shutdown. Requiring the parameter makes
/// the caller write <c>CancellationToken.None</c> when they mean it, which is a decision a reviewer
/// can see.
/// </para>
/// <para>
/// ⚠ <b>This is a POLICY rule, not a defect rule — adopt it deliberately.</b> The BCL defaults its
/// own tokens everywhere, so this fires on code written in the most conventional style there is,
/// and a finding here is not evidence of a bug. It is the rule an SDK adopts when its cancellation
/// contract is load-bearing and it would rather be verbose than let a caller opt out by accident.
/// The other rules in this library report hazards; this one reports a house style, and conflating
/// the two is how a catalogue loses a reader's trust.
/// </para>
/// <para>
/// ⚠ <b>Compiler-generated members are skipped.</b> Record equality members and property accessors
/// carry no token; including them would only inflate <see cref="AssemblyRuleResult.Inspected"/> and
/// make a weak scan look thorough.
/// </para>
/// </remarks>
public sealed class CancellationTokenRule : IAssemblyRule
{
    /// <inheritdoc />
    public string Id => "AQ1001";

    /// <inheritdoc />
    public string Summary => "No public method takes a CancellationToken with a default value.";

    /// <inheritdoc />
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AssemblyFinding> findings = [];
        int inspected = 0;

        foreach ((Assembly assembly, Type type) in context.ExportedTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (parameter.ParameterType != typeof(CancellationToken))
                    {
                        continue;
                    }

                    inspected++;

                    if (!parameter.HasDefaultValue)
                    {
                        continue;
                    }

                    findings.Add(new AssemblyFinding(
                        Id,
                        assembly.GetName().Name ?? "?",
                        $"{type.FullName}.{method.Name}({parameter.Name})",
                        "This CancellationToken has a default value, so a caller who omits it gets "
                        + "CancellationToken.None and an operation that cannot be cancelled — chosen "
                        + "by nobody and visible nowhere. Drop the default and let the caller write "
                        + "CancellationToken.None when that is what they mean."));
                }
            }
        }

        return new AssemblyRuleResult(findings, inspected);
    }
}
