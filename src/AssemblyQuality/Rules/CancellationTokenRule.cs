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
/// ⛔ <b>A token-less overload is the same default, moved.</b> Answering a finding with
/// <c>Build(a, b, options = null) =&gt; Build(a, b, options, CancellationToken.None)</c> removes the
/// defaulted parameter and keeps the opt-out: a caller who omits the token still gets one they never
/// chose. So a public method is reported when a same-named sibling takes a token and this one's
/// parameters are that sibling's, token removed, or a leading run of them.
/// </para>
/// <para>
/// ⚠ <b>The fix that holds: put the token ahead of any optional parameter.</b> Dropping the token's
/// default alone is CS1737 when an optional parameter follows it, and dropping that one's default
/// too leaves every caller writing a literal <c>null</c> for an argument nobody thought about.
/// Measured on a real adoption: moving the token ahead of the optional parameter touched 71 call
/// sites and left one literal <c>null</c>; dropping both defaults would have left 44.
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
    public string Id => "BNAQ1001";

    /// <inheritdoc />
    public string Summary => "No public method takes a CancellationToken with a default value.";

    /// <inheritdoc />
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AssemblyFinding> findings = [];
        List<string> skipped = [];
        int inspected = 0;

        foreach ((Assembly assembly, Type type) in context.ExportedTypes(skipped))
        {
            // ⚠ Read every signature before counting any of it. A parameter naming an assembly
            // that will not load throws part-way through, and a type half-counted is a number that
            // matches neither what was examined nor what was not.
            Signature[] methods;
            try
            {
                methods =
                [
                    .. type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Where(method => !method.IsSpecialName)
                        .Select(Signature.Of),
                ];
            }
            catch (Exception ex) when (AssemblyScanContext.IsLoadFailure(ex))
            {
                skipped.Add(AssemblyScanContext.Unreadable(type, ex));
                continue;
            }

            foreach (Signature method in methods)
            {
                foreach (ParameterInfo parameter in method.Parameters)
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
                        $"{type.FullName}.{method.Method.Name}({parameter.Name})",
                        "This CancellationToken has a default value, so a caller who omits it gets "
                        + "CancellationToken.None and an operation that cannot be cancelled — chosen "
                        + "by nobody and visible nowhere. Drop the default and let the caller write "
                        + "CancellationToken.None when that is what they mean."));
                }
            }

            foreach (Signature method in methods.Where(m => !m.TakesToken))
            {
                Signature[] siblings = [.. methods.Where(s => s.Method.Name == method.Method.Name && s.TakesToken)];
                if (siblings.Length == 0)
                {
                    continue;
                }

                inspected++;

                if (!siblings.Any(method.Abbreviates))
                {
                    continue;
                }

                findings.Add(new AssemblyFinding(
                    Id,
                    assembly.GetName().Name ?? "?",
                    $"{type.FullName}.{method.Method.Name}({string.Join(", ", method.Parameters.Select(p => p.Name))})",
                    "This overload is a sibling that takes a CancellationToken with the token left "
                    + "out, so a caller who uses it gets CancellationToken.None without choosing it — "
                    + "the defaulted token again, one overload over. Remove it, and put the sibling's "
                    + "token ahead of any optional parameter so the caller must write it."));
            }
        }

        return new AssemblyRuleResult(findings, inspected) { Skipped = [.. skipped.Distinct(StringComparer.Ordinal)] };
    }

    /// <summary>A method with its parameter types already resolved.</summary>
    /// <param name="Method">The method.</param>
    /// <param name="Parameters">Its parameters.</param>
    /// <param name="TokenlessTypes">
    /// Every parameter type other than <see cref="CancellationToken"/>, by NAME rather than
    /// identity: two generic methods each declare their own <c>T</c>, and identity would call them
    /// different types.
    /// </param>
    /// <param name="TakesToken">Whether any parameter is a <see cref="CancellationToken"/>.</param>
    private sealed record Signature(MethodInfo Method, ParameterInfo[] Parameters, string[] TokenlessTypes, bool TakesToken)
    {
        public static Signature Of(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string[] tokenless =
            [
                .. parameters
                    .Where(p => p.ParameterType != typeof(CancellationToken))
                    .Select(p => p.ParameterType.ToString()),
            ];

            return new Signature(method, parameters, tokenless, tokenless.Length != parameters.Length);
        }

        /// <summary>
        /// Whether this method's parameters are <paramref name="sibling"/>'s with every token
        /// removed, or a leading run of them — the overload that exists only to omit the token.
        /// </summary>
        public bool Abbreviates(Signature sibling) =>
            TokenlessTypes.Length <= sibling.TokenlessTypes.Length
            && TokenlessTypes.SequenceEqual(sibling.TokenlessTypes.Take(TokenlessTypes.Length));
    }
}
