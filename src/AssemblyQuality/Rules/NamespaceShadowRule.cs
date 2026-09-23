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
/// fix there renames package, assembly and namespace together, <c>.Avalonia</c> → <c>.AvaloniaUI</c>.
/// </para>
/// </remarks>
public sealed class NamespaceShadowRule : IAssemblyRule
{
    /// <inheritdoc />
    public string Id => "AQ1004";

    /// <inheritdoc />
    public string Summary => "No declared namespace segment shadows the root namespace of a referenced assembly.";

    /// <inheritdoc />
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AssemblyFinding> findings = [];
        int inspected = 0;

        foreach (Assembly assembly in context.Assemblies)
        {
            HashSet<string> roots = RootNamespacesOf(assembly);

            HashSet<string> reported = new(StringComparer.Ordinal);

            foreach ((Assembly owner, Type type) in AssemblyScanContext.Of(assembly).ExportedTypes())
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
                        + $"assembly and package name as well (e.g. '{segment}UI')."));
                }
            }
        }

        return new AssemblyRuleResult(findings, inspected);
    }

    /// <summary>
    /// The first segment of every namespace a referenced assembly actually exports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>NOT the referenced assembly's name.</b> That shortcut looks sound — and is, for
    /// framework assemblies where <c>Avalonia.dll</c> holds <c>Avalonia.*</c> — but it is wrong
    /// wherever a project names assemblies and namespaces differently on purpose. A repository that
    /// ships assembly <c>Widgets.Core</c> under namespace <c>Acme.Widgets.Core</c> has no root
    /// called <c>Widgets</c>, and the shortcut reports a shadow of something that does not exist.
    /// Measured: it fired on a real library whose convention is exactly that.
    /// </para>
    /// <para>
    /// ⚠ <b>A reference that will not load is skipped, not guessed at.</b> That means this rule can
    /// under-report, which is the failure mode worth being loudest about — so skipped references do
    /// not count toward <see cref="AssemblyRuleResult.Inspected"/> either.
    /// </para>
    /// </remarks>
    private static HashSet<string> RootNamespacesOf(Assembly assembly)
    {
        HashSet<string> roots = new(StringComparer.Ordinal);

        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            Assembly referenced;
            try
            {
                referenced = Assembly.Load(reference);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                continue;
            }

            foreach ((Assembly _, Type type) in AssemblyScanContext.Of(referenced).ExportedTypes())
            {
                if (type.Namespace?.Split('.') is [string root, ..] && root.Length > 0)
                {
                    roots.Add(root);
                }
            }
        }

        return roots;
    }
}
