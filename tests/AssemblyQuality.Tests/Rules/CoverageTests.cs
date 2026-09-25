using System.Reflection;
using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;
using Xunit;

namespace AssemblyQuality.Tests.Rules;

/// <summary>
/// What a rule did NOT see: references that would not load, types it could not read, and internal
/// types outside its default reach.
/// </summary>
/// <remarks>
/// ⭐ <b>A partial answer must say it is partial.</b> <see cref="AssemblyRuleResult.Inspected"/>
/// tells "nothing checked" from "checked and clean", but a scan missing one dependency still
/// counts everything else. <see cref="AssemblyRuleResult.Skipped"/> is what names the gap, and
/// these tests hold it against a real assembly whose dependency is really missing.
/// </remarks>
public sealed class CoverageTests
{
    private const string AbsentName = "AssemblyQuality.Fixtures.Absent";

    /// <summary>
    /// Orphan, loaded from a folder that does not hold Absent. The test project builds Orphan but
    /// never references it, so Absent is not in this process's dependency graph either.
    /// </summary>
    private static Assembly Orphan { get; } =
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "orphan", "AssemblyQuality.Fixtures.Orphan.dll"));

    private static AssemblyScanContext Orphaned => AssemblyScanContext.Of(Orphan);

    /// <summary>⚠ The premise, checked: without it every test below passes on a dependency that loaded.</summary>
    [Fact]
    public void TheOrphansReference_ReallyWillNotLoad()
    {
        AssemblyName absent = Assert.Single(Orphan.GetReferencedAssemblies(), r => r.Name == AbsentName);

        Assert.Throws<FileNotFoundException>(() => Assembly.Load(absent));
    }

    [Fact]
    public void AReferenceThatWillNotLoad_IsNamedInSkipped()
    {
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(Orphaned);

        Assert.Contains(result.Skipped, s => s.Contains(AbsentName, StringComparison.Ordinal));

        // The Orphan.Absent shadow is real and invisible: its root never loaded. Skipped is the
        // only place the result admits that.
        Assert.DoesNotContain(result.Findings, f => f.Subject == "Orphan.Absent");
        Assert.True(result.Inspected > 0, "the references that DID load must still be compared against");
    }

    /// <summary>
    /// ⛔ Regression: a root reachable only through a facade is still a root. System.Runtime
    /// forwards every type it names, so reading exported types alone left Orphan with nothing to
    /// compare against and passed its <c>.System</c> shadow.
    /// </summary>
    [Fact]
    public void ARootReachableOnlyThroughAFacade_IsComparedAgainst()
    {
        Assert.DoesNotContain(Orphan.GetReferencedAssemblies(), r => r.Name is "System.Private.CoreLib");

        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(Orphaned);

        Assert.Contains(result.Findings, f => f.Subject == "Orphan.System");
    }

    /// <summary>
    /// ⛔ A leak set whose only home would not load reads zero inspected — and without Skipped
    /// that zero would say "nothing in reach" when the truth is "could not look".
    /// </summary>
    [Fact]
    public void AnUnreachableLeakSet_SaysWhyItIsUnreachable()
    {
        AssemblyRuleResult result = SurfaceLeakRule.Only(["Absent"]).Analyze(Orphaned);

        Assert.Equal(0, result.Inspected);
        Assert.Contains(result.Skipped, s => s.Contains(AbsentName, StringComparison.Ordinal));
    }

    /// <summary>
    /// ⛔ A signature naming a missing assembly used to throw out of the whole scan. It is now
    /// skipped and named, and the rest of the assembly is still examined.
    /// </summary>
    [Fact]
    public void ATypeWhoseSignatureCannotBeRead_IsSkippedNotFatal()
    {
        AssemblyRuleResult tokens = new CancellationTokenRule().Analyze(Orphaned);
        AssemblyRuleResult leaks = SurfaceLeakRule.Only(["Orphan"]).Analyze(Orphaned);

        foreach (AssemblyRuleResult result in (AssemblyRuleResult[])[tokens, leaks])
        {
            Assert.Contains(result.Skipped, s => s.Contains("Orphan.Absent.Unreadable", StringComparison.Ordinal));
            Assert.True(result.Inspected > 0, "Readable must still be examined beside the type that could not be");
        }
    }

    /// <summary>
    /// ⛔ Regression: a public type whose BASE class is in a missing assembly makes
    /// <c>GetExportedTypes()</c> throw <see cref="FileNotFoundException"/>, not a
    /// <see cref="ReflectionTypeLoadException"/>. Every rule must still run, name the gap in
    /// <c>Skipped</c>, and examine the types that did load.
    /// </summary>
    [Fact]
    public void AnAssemblyWithATypeThatCannotLoad_IsScannedNotFatal()
    {
        Assert.Throws<FileNotFoundException>(() => Orphan.GetExportedTypes());

        foreach (IAssemblyRule rule in RulesCatalogTests.DiscoverRules().Where(r => r is not ForbiddenReferenceRule))
        {
            AssemblyRuleResult result = rule.Analyze(Orphaned);
            Assert.Contains(result.Skipped, s => s.Contains("AssemblyQuality.Fixtures.Orphan", StringComparison.Ordinal)
                && s.Contains("would not load", StringComparison.Ordinal));
        }

        // The loadable types were still examined: Readable's token is counted.
        Assert.True(new CancellationTokenRule().Analyze(Orphaned).Inspected > 0);
    }

    /// <summary>
    /// ⚠ A missing INTERFACE fails a type's load just as a missing base class does. Both of Orphan's
    /// unloadable shapes are named here, so a fix that handled base classes alone would fail.
    /// </summary>
    [Fact]
    public void ATypeImplementingAMissingInterface_CannotLoad_AndIsCounted()
    {
        ReflectionTypeLoadException partial = Assert.Throws<ReflectionTypeLoadException>(() => Orphan.GetTypes());
        Assert.Equal(2, partial.Types.Count(t => t is null));

        AssemblyRuleResult result = new CancellationTokenRule().Analyze(Orphaned);
        Assert.Contains(result.Skipped, s => s.Contains("AssemblyQuality.Fixtures.Orphan: 2 type(s) would not load", StringComparison.Ordinal));
    }

    /// <summary>⭐ A scan with every dependency present says its answer is whole.</summary>
    [Fact]
    public void AScanWithEverythingPresent_SkipsNothing()
    {
        AssemblyScanContext subjects = AssemblyScanContext.Of(typeof(Fixtures.Offender).Assembly, typeof(IAssemblyRule).Assembly);

        foreach (IAssemblyRule rule in RulesCatalogTests.DiscoverRules())
        {
            Assert.Empty(rule.Analyze(subjects).Skipped);
        }
    }

    [Fact]
    public void AnInternalShadow_IsReportedOnlyWhenOptedIn()
    {
        AssemblyScanContext subjects = AssemblyScanContext.Of(typeof(Fixtures.Offender).Assembly);

        AssemblyRuleResult published = new NamespaceShadowRule().Analyze(subjects);
        AssemblyRuleResult everything = NamespaceShadowRule.IncludingInternalTypes().Analyze(subjects);

        Assert.DoesNotContain(published.Findings, f => f.Subject.EndsWith(".Hidden.System", StringComparison.Ordinal));
        Assert.Contains(everything.Findings, f => f.Subject.EndsWith(".Hidden.System", StringComparison.Ordinal));
        Assert.True(everything.Inspected > published.Inspected, "the opt-in must examine more, not the same set");
    }

    /// <summary>
    /// ⚠ Internal types include what the compiler emits — embedded attributes, nullable metadata —
    /// in namespaces nobody wrote. The opt-in must stay clean over a library that has no shadow.
    /// </summary>
    [Fact]
    public void TheInternalOptIn_IsCleanOverALibraryWithNoShadow()
    {
        AssemblyRuleResult result = NamespaceShadowRule.IncludingInternalTypes()
            .Analyze(AssemblyScanContext.Of(typeof(IAssemblyRule).Assembly));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0);
    }
}
