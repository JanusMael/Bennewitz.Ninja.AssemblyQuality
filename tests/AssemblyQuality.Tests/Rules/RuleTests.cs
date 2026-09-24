using System.Reflection;
using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;
using Xunit;

namespace AssemblyQuality.Tests.Rules;

/// <summary>
/// Every rule in both directions, against the compiled fixtures.
/// </summary>
/// <remarks>
/// ⭐ <b>Both directions, always.</b> The guards these rules came from asserted "no offenders in
/// this repository today", which passes identically whether the rule works or has quietly stopped
/// matching. Each rule here is shown finding a real violation AND clearing a clean subject.
/// </remarks>
public sealed class RuleTests
{
    private static AssemblyScanContext Subjects =>
        AssemblyScanContext.Of(typeof(Fixtures.Offender).Assembly);

    private static AssemblyScanContext Library =>
        AssemblyScanContext.Of(typeof(IAssemblyRule).Assembly);

    [Fact]
    public void ADefaultedCancellationToken_IsReported()
    {
        AssemblyRuleResult result = new CancellationTokenRule().Analyze(Subjects);

        Assert.Contains(result.Findings, f => f.Subject.Contains("Defaulted", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Findings, f => f.Subject.Contains(".Required", StringComparison.Ordinal));
        Assert.True(result.Inspected >= 2, "both tokens must be inspected, not just the offending one");
    }

    /// <summary>
    /// ⛔ The fix that satisfies the letter and defeats the point: the default moved one overload
    /// over. Without this the rule reads zero findings on exactly the shape its remarks forbid.
    /// </summary>
    [Fact]
    public void ATokenlessOverloadOfATokenedSibling_IsReported()
    {
        AssemblyRuleResult result = new CancellationTokenRule().Analyze(Subjects);

        Assert.Contains(result.Findings, f => f.Subject.EndsWith("Overloaded.Build(left, options)", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Findings, f => f.Subject.Contains("Overloaded.Parse", StringComparison.Ordinal));
    }

    [Fact]
    public void ALeakedSerializerType_IsReported()
    {
        AssemblyRuleResult result = new SurfaceLeakRule().Analyze(Subjects);

        Assert.Contains(result.Findings, f => f.Subject.EndsWith(".Leaked", StringComparison.Ordinal));
    }

    /// <summary>⛔ The shape that defeats an outer-type-only check.</summary>
    [Fact]
    public void ALeakNestedInAGenericArgument_IsReported()
    {
        AssemblyRuleResult result = new SurfaceLeakRule().Analyze(Subjects);

        Assert.Contains(result.Findings, f => f.Subject.EndsWith(".LeakedNested", StringComparison.Ordinal));
    }

    [Fact]
    public void ACleanSubject_IsNotReported()
    {
        AssemblyRuleResult result = new SurfaceLeakRule().Analyze(Subjects);

        Assert.DoesNotContain(result.Findings, f => f.Subject.Contains("Clean.Fine", StringComparison.Ordinal));
    }

    [Fact]
    public void AnAdditionalNamespace_IsHonoured()
    {
        AssemblyRuleResult ignored = new SurfaceLeakRule().Analyze(Subjects);
        AssemblyRuleResult covered = new SurfaceLeakRule(["AssemblyQuality.Tests.Fixtures"]).Analyze(Subjects);

        Assert.True(covered.Findings.Count > ignored.Findings.Count,
            "a namespace passed by the consumer must widen the rule");
    }

    [Fact]
    public void Only_ReplacesTheDefaultSet()
    {
        AssemblyRuleResult result = SurfaceLeakRule.Only(["AssemblyQuality.Tests.Fixtures"]).Analyze(Subjects);

        Assert.Contains(result.Findings, f => f.Subject.EndsWith("Clean.Fine", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Findings, f => f.Subject.EndsWith(".Leaked", StringComparison.Ordinal));
    }

    /// <summary>
    /// ⭐ A leak set nothing in reach exports cannot fire, and must say so with zero rather than a
    /// large count of slots walked past — the number a consumer asserting on Inspected would trust.
    /// </summary>
    [Fact]
    public void ALeakSetNothingInReachExports_ReportsThatItInspectedNothing()
    {
        Assert.DoesNotContain(
            typeof(IAssemblyRule).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "System.Text.Json" or "Newtonsoft.Json");

        AssemblyRuleResult unreachable = new SurfaceLeakRule().Analyze(Library);
        AssemblyRuleResult empty = SurfaceLeakRule.Only([]).Analyze(Subjects);

        Assert.Empty(unreachable.Findings);
        Assert.Equal(0, unreachable.Inspected);
        Assert.Equal(0, empty.Inspected);

        // ⚠ And where a leak IS possible the rule still counts, or this passes on a rule that has
        // stopped counting anything.
        Assert.True(new SurfaceLeakRule().Analyze(Subjects).Inspected > 0);
    }

    [Fact]
    public void AForbiddenReference_IsReported()
    {
        AssemblyRuleResult result = new ForbiddenReferenceRule(["xunit"]).Analyze(Subjects);

        Assert.NotEmpty(result.Findings);
        Assert.True(result.Inspected > 0);
    }

    /// <summary>
    /// ⭐ The distinction <see cref="AssemblyRuleResult.Inspected"/> exists for: configured with
    /// nothing, the rule must say it asked nothing rather than report a clean bill of health.
    /// </summary>
    [Fact]
    public void AForbiddenReferenceRuleWithNoConfiguration_ReportsThatItCheckedNothing()
    {
        AssemblyRuleResult result = new ForbiddenReferenceRule().Analyze(Subjects);

        Assert.Empty(result.Findings);
        Assert.Equal(0, result.Inspected);
    }

    [Fact]
    public void AShadowingNamespaceSegment_IsReported()
    {
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(Subjects);

        Assert.Contains(result.Findings, f => f.Subject.EndsWith(".Shadow.System", StringComparison.Ordinal));
    }

    /// <summary>
    /// ⚠ The advice must name both fixes. Prescribing the namespace-only rename breaks every
    /// project whose namespace follows its assembly name — pin the two options, not the sentence.
    /// </summary>
    [Fact]
    public void AShadowFinding_NamesBothFixes()
    {
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(Subjects);

        AssemblyFinding finding = Assert.Single(result.Findings, f => f.Subject.EndsWith(".Shadow.System", StringComparison.Ordinal));
        Assert.Contains("namespace alone", finding.Message, StringComparison.Ordinal);
        Assert.Contains("assembly name as well", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAssemblyWithNoShadowingSegment_IsClean()
    {
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(Library);

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "the library's own namespaces must actually be inspected");
    }

    /// <summary>
    /// ⛔ With no referenced roots to compare against, no segment could shadow anything, and the
    /// rule must report zero rather than the count of segments it walked past. Otherwise a scan
    /// whose references all failed to load reads exactly like a clean one.
    /// </summary>
    [Fact]
    public void AnAssemblyWithNoReferencedRoots_ReportsThatItComparedNothing()
    {
        // CoreLib references nothing, and has plenty of multi-segment namespaces: the same empty
        // comparison set a scan gets when every reference fails to load.
        Assembly coreLib = typeof(object).Assembly;
        Assert.Empty(coreLib.GetReferencedAssemblies());

        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(AssemblyScanContext.Of(coreLib));

        Assert.Empty(result.Findings);
        Assert.Equal(0, result.Inspected);
    }

    /// <summary>⚠ A rule handed no assemblies reports nothing AND says it inspected nothing.</summary>
    [Fact]
    public void EveryRule_GivenNothing_ReportsThatItInspectedNothing()
    {
        AssemblyScanContext empty = AssemblyScanContext.Of();

        foreach (IAssemblyRule rule in RulesCatalogTests.DiscoverRules())
        {
            AssemblyRuleResult result = rule.Analyze(empty);
            Assert.Empty(result.Findings);
            Assert.Equal(0, result.Inspected);
        }
    }

    /// <summary>
    /// ⛔ Regression: a namespace sharing its OWN root with a referenced assembly is correct, not a
    /// shadow. Measured on a real library — reporting it turned one false positive into three, and
    /// would fire on every project that shares a vendor prefix with its own packages.
    /// </summary>
    [Fact]
    public void ANamespaceSharingItsOwnRootWithAReference_IsNotAShadow()
    {
        // Fixtures.OwnRoot lives in System.AssemblyQualityProbe: its FIRST segment is `System`,
        // which really is a referenced assembly's root. Correct code, and it must stay unreported.
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(Subjects);

        Assert.DoesNotContain(result.Findings, f => f.Subject == "System.AssemblyQualityProbe");

        // ⚠ And the rule must still be looking: the same assembly's Shadow.System fixture, whose
        // `System` is NOT the first segment, is reported. Without this the test passes on a rule
        // that has stopped matching anything at all.
        Assert.Contains(result.Findings, f => f.Subject.EndsWith(".Shadow.System", StringComparison.Ordinal));
    }
}
