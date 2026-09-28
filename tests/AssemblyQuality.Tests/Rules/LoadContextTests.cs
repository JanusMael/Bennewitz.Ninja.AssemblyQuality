using System.Reflection;
using System.Runtime.Loader;
using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;
using Xunit;

namespace AssemblyQuality.Tests.Rules;

/// <summary>
/// A scanned assembly's references resolve where the assembly itself was loaded, not in the default
/// load context.
/// </summary>
/// <remarks>
/// ⛔ <b>The question a rule asks is "what does this assembly see?"</b> An assembly loaded into a
/// context of its own, beside a different version of a dependency than the test process has, sees
/// that version. Resolving its references in the default context answers for some other version, or
/// for none, and the rule reports on an assembly that does not exist.
/// </remarks>
public sealed class LoadContextTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "versioned");

    /// <summary>Dependent, loaded beside version two of its dependency, in a context of its own.</summary>
    private static Assembly LoadDependent()
    {
        AssemblyLoadContext context = new("versioned", isCollectible: true);
        context.Resolving += (_, name) =>
        {
            string candidate = Path.Combine(Folder, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        return context.LoadFromAssemblyPath(Path.Combine(Folder, "AssemblyQuality.Fixtures.Dependent.dll"));
    }

    /// <summary>⚠ The premise, checked: the dependency Dependent sees really is version two.</summary>
    [Fact]
    public void TheDependentSeesVersionTwo_InItsOwnContext()
    {
        Assembly dependent = LoadDependent();
        AssemblyName reference = Assert.Single(dependent.GetReferencedAssemblies(), r => r.Name == "AssemblyQuality.Fixtures.Versioned");

        Assembly versioned = AssemblyLoadContext.GetLoadContext(dependent)!.LoadFromAssemblyName(reference);

        Assert.Contains(versioned.GetExportedTypes(), t => t.Namespace == "Two.Things");
        Assert.Throws<FileNotFoundException>(() => Assembly.Load(reference));
    }

    /// <summary>
    /// ⛔ Regression: <c>Dependent.Two</c> shadows the root <c>Two</c>, which only version two
    /// exports. Resolved in the default context, the reference does not load at all and the shadow is
    /// missed; resolved where Dependent lives, it is found.
    /// </summary>
    [Fact]
    public void AShadowOfARootOnlyTheLoadedVersionExports_IsReported()
    {
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(AssemblyScanContext.Of(LoadDependent()));

        Assert.Contains(result.Findings, f => f.Subject == "Dependent.Two");
        Assert.DoesNotContain(result.Skipped, s => s.Contains("AssemblyQuality.Fixtures.Versioned", StringComparison.Ordinal));
    }
}
