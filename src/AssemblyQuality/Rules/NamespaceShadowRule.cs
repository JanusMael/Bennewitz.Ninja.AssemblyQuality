using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Bennewitz.Ninja.AssemblyQuality.Rules;

/// <summary>
/// No declared namespace carries a segment that shadows the root namespace of a referenced assembly.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>C# resolves the first identifier of a qualified name by walking outward.</b> So inside
/// <c>Acme.Widgets.Avalonia</c>, the name <c>Avalonia</c> finds YOUR namespace first, and
/// <c>Avalonia.Media.Color</c> fails to compile looking for
/// <c>Acme.Widgets.Avalonia.Media.Color</c> — CS0234, reported at the use site, about a type that
/// obviously exists. The fix is <c>global::</c> at every use, which is a scar rather than a
/// solution.
/// </para>
/// <para>
/// ⛔ <b>Only segments after the first.</b> A namespace's own root is the tree it already lives in,
/// so <c>Acme.Widgets</c> beside a referenced <c>Acme.Other</c> resolves perfectly — flagging it
/// would fire on every library that shares a vendor prefix with its own dependencies.
/// </para>
/// <para>
/// ⚠ <b>The defect is the shape, not the word.</b> This checks every segment against every
/// referenced assembly's root, so a future <c>.Media</c> or <c>.Controls</c> segment is the same
/// bug caught by the same rule — not a second special case to remember.
/// </para>
/// <para>
/// ⛔ <b>This one is invisible until somebody writes the unlucky line.</b> A shadowing segment
/// compiles perfectly until the first file needs a qualified name through it, which can be years.
/// </para>
/// <para>
/// ⚠ <b>The message offers two fixes, not one.</b> Where the namespace follows the assembly name
/// (<c>RootNamespace</c> derived from it), renaming only the namespace breaks that convention — the
/// fix there renames assembly and namespace together, <c>.Avalonia</c> → <c>.AvaloniaUI</c>. The
/// package id takes no part in name resolution, so it may keep <c>.Avalonia</c>.
/// </para>
/// <para>
/// ⭐ <b><see cref="AssemblyRuleResult.Inspected"/> counts comparisons that could have fired.</b>
/// A segment is counted only when the assembly has referenced roots to compare it against, so an
/// assembly whose references all failed to load contributes zero. A reference that would not load
/// is named in <see cref="AssemblyRuleResult.Skipped"/>, which is how a PARTIAL load shows: the
/// count covers the references that loaded, and Skipped lists the ones that did not.
/// </para>
/// <para>
/// ⚠ <b>Public types by default; <see cref="IncludingInternalTypes"/> for all of them.</b> The shadow
/// is a compile-time failure inside the declaring assembly, so it bites internal code exactly as
/// hard as public code. The default follows the rest of this library in reading only what a
/// consumer can see; a namespace holding only internal types is examined only by the opt-in.
/// </para>
/// </remarks>
public sealed class NamespaceShadowRule : IAssemblyRule
{
    private readonly bool _includeInternal;

    /// <summary>Examines the namespaces of public types.</summary>
    public NamespaceShadowRule()
        : this(includeInternal: false)
    {
    }

    private NamespaceShadowRule(bool includeInternal)
    {
        _includeInternal = includeInternal;
    }

    /// <inheritdoc />
    public string Id => "BNAQ1004";

    /// <inheritdoc />
    public string Summary => "No declared namespace segment shadows the root namespace of a referenced assembly.";

    /// <summary>Examines the namespaces of every type, public and internal alike.</summary>
    public static NamespaceShadowRule IncludingInternalTypes() => new(includeInternal: true);

    /// <inheritdoc />
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AssemblyFinding> findings = [];
        List<string> skipped = [];
        int inspected = 0;

        foreach (Assembly assembly in context.Assemblies)
        {
            HashSet<string> roots = RootNamespacesOf(assembly, skipped);

            // ⛔ Nothing to compare against means nothing was checked. Counting these segments
            // anyway reports the same number as a clean scan — the inertness Inspected exists to
            // expose.
            if (roots.Count == 0)
            {
                continue;
            }

            HashSet<string> reported = new(StringComparer.Ordinal);
            AssemblyScanContext single = AssemblyScanContext.Of(assembly);

            foreach ((Assembly owner, Type type) in _includeInternal ? single.AllTypes(skipped) : single.ExportedTypes(skipped))
            {
                if (type.Namespace is not { } ns)
                {
                    continue;
                }

                // ⛔ The FIRST segment can never shadow anything, and skipping it is not an
                // optimisation. Inside Acme.Widgets the name `Acme` binds to your own root, which
                // is the same tree a referenced Acme.Other lives in, so it resolves correctly.
                // Counting it reports every library that shares a vendor prefix with its own
                // dependencies — measured, and it turned one false positive into three.
                foreach (string segment in ns.Split('.').Skip(1))
                {
                    inspected++;

                    if (!roots.Contains(segment) || !reported.Add(ns + "/" + segment))
                    {
                        continue;
                    }

                    findings.Add(new AssemblyFinding(
                        Id,
                        owner.GetName().Name ?? "?",
                        ns,
                        $"The segment '{segment}' shadows the root namespace of a referenced "
                        + $"assembly, so inside this namespace the name '{segment}' resolves here "
                        + "first and a qualified name through it fails with CS0234 — at the use "
                        + "site, about a type that plainly exists. Rename the segment: in the "
                        + "namespace alone, or, where namespaces follow the assembly name, in the "
                        + $"assembly name as well (e.g. '{segment}UI'). A package id is not a "
                        + "namespace and may keep the word."));
                }
            }
        }

        return new AssemblyRuleResult(findings, inspected) { Skipped = [.. skipped.Distinct(StringComparer.Ordinal)] };
    }

    /// <summary>
    /// The first segment of every namespace a referenced assembly actually exports.
    /// </summary>
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    private static HashSet<string> RootNamespacesOf(Assembly assembly, ICollection<string> skipped)
    {
        HashSet<string> roots = new(StringComparer.Ordinal);

        foreach (string ns in AssemblyScanContext.ReferencedNamespaces(assembly, skipped))
        {
            roots.Add(ns.Split('.')[0]);
        }

        return roots;
    }
}
