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
}
