namespace Bennewitz.Ninja.AssemblyQuality;

/// <summary>One assembly quality rule.</summary>
/// <remarks>
/// <para>
/// ⚠ <b>A rule reports; it does not assert.</b> Returning findings rather than throwing keeps the
/// library free of any test framework, and lets the consumer choose severity: the same finding can
/// fail a build, warn in CI, or print in a report.
/// </para>
/// <para>
/// ⛔ <b>A rule that can never fire is worse than no rule.</b> It reports zero, reads as coverage,
/// and is indistinguishable from a clean assembly — which is why <see cref="AssemblyRuleResult"/>
/// carries what was inspected as well as what was found.
/// </para>
/// <para>
/// ⭐ <b>Reflection, not source.</b> These rules read the shipped shape of an assembly, which is
/// the thing a consumer actually binds against. A source analyser sees what was written; this sees
/// what was published, including everything a generator added.
/// </para>
/// </remarks>
public interface IAssemblyRule
{
    /// <summary>
    /// Stable identifier, used in findings and in any suppression a consumer builds.
    /// ⚠ Treat it as public API: changing it silently un-suppresses whatever referenced it.
    /// </summary>
    string Id { get; }

    /// <summary>One line: what the rule REQUIRES, phrased as the requirement rather than the violation.</summary>
    string Summary { get; }

    /// <summary>Examine the scanned assemblies and report every violation found.</summary>
    AssemblyRuleResult Analyze(AssemblyScanContext context);
}

/// <summary>What a rule found, and how much it looked at.</summary>
/// <param name="Findings">Every violation, in discovery order.</param>
/// <param name="Inspected">
/// How many candidate members, types or references the rule actually examined.
/// <para>
/// ⭐ <b>The number that tells "nothing is wrong" apart from "nothing was checked".</b> A rule
/// given no assemblies, or one whose configuration names nothing, returns zero findings and looks
/// like success. A consumer asserting only on <see cref="Findings"/> is trusting a number that
/// cannot rise, so it can never fail.
/// </para>
/// </param>
public sealed record AssemblyRuleResult(IReadOnlyList<AssemblyFinding> Findings, int Inspected)
{
    /// <summary>A result for a rule that examined things and found nothing wrong.</summary>
    public static AssemblyRuleResult Clean(int inspected) => new([], inspected);
}
