using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Bennewitz.Ninja.AssemblyQuality;

/// <summary>The assemblies a scan covers.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>A value handed to rules, never ambient state.</b> Two scans over different assemblies must
/// be able to run in one process without seeing each other, and a rule must never reach for
/// <c>Assembly.GetExecutingAssembly()</c> — which would silently inspect the test project instead
/// of the library under test.
/// </para>
/// <para>
/// ⚠ <b>Exported types only, and that is the whole point.</b> Every rule here asks a question about
/// what a CONSUMER can see. Internals are free to be whatever they need to be; a rule that policed
/// them would be enforcing style rather than contract.
/// </para>
/// </remarks>
public sealed class AssemblyScanContext
{
    /// <summary>
    /// The <see cref="RequiresUnreferencedCodeAttribute"/> message on every reflection entry point.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Trimming does not break a scan; it makes one lie.</b> The trimmer removes exactly the
    /// types and references these rules look for, so a trimmed scan completes and under-reports.
    /// </remarks>
    internal const string TrimMessage =
        "Reads exported types and assembly references by reflection. Trimming removes what the "
        + "rules look for, so a scan in a trimmed application completes and silently under-reports.";

    private AssemblyScanContext(IReadOnlyList<Assembly> assemblies)
    {
        Assemblies = assemblies;
    }

    /// <summary>Every assembly in the scan.</summary>
    public IReadOnlyList<Assembly> Assemblies { get; }

    /// <summary>Scan <paramref name="assemblies"/>.</summary>
    public static AssemblyScanContext Of(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return new AssemblyScanContext([.. assemblies]);
    }

    /// <summary>
    /// Every public type in the scan, paired with the assembly it came from.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A partially-loadable assembly yields what it can rather than throwing.</b> One missing
    /// optional dependency would otherwise take the whole scan down, and a rule that cannot run is
    /// a rule nobody keeps.
    /// </remarks>
    [RequiresUnreferencedCode(TrimMessage)]
    public IEnumerable<(Assembly Assembly, Type Type)> ExportedTypes()
    {
        foreach (Assembly assembly in Assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.OfType<Type>().Where(t => t.IsPublic || t.IsNestedPublic)];
            }

            foreach (Type type in types)
            {
                yield return (assembly, type);
            }
        }
    }

    /// <summary>
    /// Every namespace exported by an assembly that <paramref name="assembly"/> directly references.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>NOT the referenced assemblies' names.</b> That shortcut looks sound — and is, for
    /// framework assemblies where <c>Avalonia.dll</c> holds <c>Avalonia.*</c> — but it is wrong
    /// wherever a project names assemblies and namespaces differently on purpose. A repository that
    /// ships assembly <c>Widgets.Core</c> under namespace <c>Acme.Widgets.Core</c> exports nothing
    /// called <c>Widgets</c>. Measured: the shortcut fired on a real library whose convention is
    /// exactly that.
    /// </para>
    /// <para>
    /// ⚠ <b>A reference that will not load is skipped, not guessed at</b>, so the set can be partial
    /// or empty. A rule comparing against it must count nothing it compared against an empty set:
    /// that comparison could never have produced a finding, and counting it is how a rule with
    /// nothing to look at reports the same healthy number as a clean one.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimMessage)]
    internal static HashSet<string> ReferencedNamespaces(Assembly assembly)
    {
        HashSet<string> namespaces = new(StringComparer.Ordinal);

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

            foreach ((Assembly _, Type type) in Of(referenced).ExportedTypes())
            {
                if (type.Namespace is { Length: > 0 } ns)
                {
                    namespaces.Add(ns);
                }
            }
        }

        return namespaces;
    }
}
