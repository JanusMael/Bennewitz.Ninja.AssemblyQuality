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
    public IEnumerable<(Assembly Assembly, Type Type)> ExportedTypes() => ExportedTypes(null);

    /// <summary>
    /// <see cref="ExportedTypes()"/>, recording in <paramref name="skipped"/> any assembly whose
    /// types only partly loaded.
    /// </summary>
    [RequiresUnreferencedCode(TrimMessage)]
    internal IEnumerable<(Assembly Assembly, Type Type)> ExportedTypes(ICollection<string>? skipped)
    {
        foreach (Assembly assembly in Assemblies)
        {
            foreach (Type type in Load(assembly, publicOnly: true, skipped))
            {
                yield return (assembly, type);
            }
        }
    }

    /// <summary>
    /// Every type in the scan, public and internal alike, recording partial loads in
    /// <paramref name="skipped"/>.
    /// </summary>
    [RequiresUnreferencedCode(TrimMessage)]
    internal IEnumerable<(Assembly Assembly, Type Type)> AllTypes(ICollection<string>? skipped)
    {
        foreach (Assembly assembly in Assemblies)
        {
            foreach (Type type in Load(assembly, publicOnly: false, skipped))
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
    /// ⚠ <b>A reference that will not load is skipped, not guessed at</b>, and recorded in
    /// <paramref name="skipped"/> so the rule's result can say what its answer is missing. A rule
    /// comparing against this set must also count nothing it compared against an empty one: that
    /// comparison could never have produced a finding.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimMessage)]
    internal static HashSet<string> ReferencedNamespaces(Assembly assembly, ICollection<string> skipped)
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
                skipped.Add($"{assembly.GetName().Name}: reference {reference.Name} {reference.Version} "
                    + $"would not load ({ex.GetType().Name}), so nothing it exports was compared against.");
                continue;
            }

            foreach ((Assembly _, Type type) in Of(referenced).ExportedTypes(skipped))
            {
                if (type.Namespace is { Length: > 0 } ns)
                {
                    namespaces.Add(ns);
                }
            }

            foreach (Type type in Forwarded(referenced, skipped))
            {
                if (type.Namespace is { Length: > 0 } ns)
                {
                    namespaces.Add(ns);
                }
            }
        }

        return namespaces;
    }

    /// <summary>
    /// The public types <paramref name="assembly"/> forwards elsewhere.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A facade exports nothing and forwards everything.</b> At run time <c>System.Runtime</c>
    /// holds no types of its own — each is forwarded to CoreLib — so reading exported types alone
    /// finds no <c>System</c> root behind the one reference nearly every assembly has. Measured: an
    /// assembly referencing only <c>System.Runtime</c> had nothing to compare against at all.
    /// </remarks>
    [RequiresUnreferencedCode(TrimMessage)]
    private static Type[] Forwarded(Assembly assembly, ICollection<string> skipped)
    {
        try
        {
            return [.. assembly.GetForwardedTypes().Where(t => t.IsPublic || t.IsNestedPublic)];
        }
        catch (ReflectionTypeLoadException ex)
        {
            skipped.Add($"{assembly.GetName().Name}: {ex.Types.Count(t => t is null)} forwarded type(s) would not "
                + $"load ({ex.LoaderExceptions.FirstOrDefault()?.Message ?? "no loader message"}), so they were not "
                + "compared against.");
            return [.. ex.Types.OfType<Type>().Where(t => t.IsPublic || t.IsNestedPublic)];
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is a load failure a rule should record and step past
    /// rather than let take the scan down.
    /// </summary>
    internal static bool IsLoadFailure(Exception exception) =>
        exception is FileNotFoundException or FileLoadException or BadImageFormatException or TypeLoadException;

    /// <summary>
    /// A record of one type a rule could not read, because something its members name will not load.
    /// </summary>
    internal static string Unreadable(Type type, Exception exception) =>
        $"{type.Assembly.GetName().Name}: {type.FullName} could not be read ({exception.GetType().Name}: "
        + $"{exception.Message}), so its members were not examined.";

    /// <summary>
    /// The types of <paramref name="assembly"/>, public only or all, with any that will not load
    /// recorded in <paramref name="skipped"/> rather than thrown.
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>GetExportedTypes()</c> does not fail the way <c>GetTypes()</c> does.</b> When a public
    /// type's BASE class lives in an assembly that will not load, it throws
    /// <see cref="FileNotFoundException"/> and returns nothing, where <c>GetTypes()</c> throws a
    /// <see cref="ReflectionTypeLoadException"/> carrying every type that did load. So a load failure
    /// from the first is answered by asking the second and keeping its public types. Measured: view
    /// models deriving from a missing <c>CommunityToolkit.Mvvm</c> took every rule's scan down.
    /// </remarks>
    [RequiresUnreferencedCode(TrimMessage)]
    private static Type[] Load(Assembly assembly, bool publicOnly, ICollection<string>? skipped)
    {
        try
        {
            return publicOnly ? assembly.GetExportedTypes() : assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return Partial(assembly, ex, publicOnly, skipped);
        }
        catch (Exception ex) when (publicOnly && IsLoadFailure(ex))
        {
            try
            {
                return [.. assembly.GetTypes().Where(t => t.IsPublic || t.IsNestedPublic)];
            }
            catch (ReflectionTypeLoadException partial)
            {
                return Partial(assembly, partial, publicOnly, skipped);
            }
        }
    }

    private static Type[] Partial(Assembly assembly, ReflectionTypeLoadException ex, bool publicOnly, ICollection<string>? skipped)
    {
        skipped?.Add($"{assembly.GetName().Name}: {ex.Types.Count(t => t is null)} type(s) would not load "
            + $"({ex.LoaderExceptions.FirstOrDefault()?.Message ?? "no loader message"}), so they were not examined.");
        return [.. ex.Types.OfType<Type>().Where(t => !publicOnly || t.IsPublic || t.IsNestedPublic)];
    }
}
