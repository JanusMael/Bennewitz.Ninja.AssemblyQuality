using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;
using Xunit;

namespace AssemblyQuality.Tests.Rules;

/// <summary>
/// BNAQ1005 in both directions, against the <c>Granting</c> fixture's four grants.
/// </summary>
public sealed class FriendGrantTests
{
    private static AssemblyScanContext Granting => AssemblyScanContext.Of(typeof(Granting.Marker).Assembly);

    private const string Solution = "AssemblyQuality.Tests";
    private const string External = "Friend.Elsewhere";

    /// <summary>
    /// ⛔ The three grants that are not allowed are each reported, the namespace-form one included:
    /// it compiles, ships and grants nothing, which is exactly why it has to be caught here.
    /// </summary>
    [Fact]
    public void EveryGrantNotAllowed_IsReported()
    {
        AssemblyRuleResult result = new FriendGrantRule([Solution]).Analyze(Granting);

        Assert.Equal(["Bennewitz.Ninja.Granting.Tests", External, "Nobody.Anywhere"], result.Findings.Select(f => f.Subject).Order(StringComparer.Ordinal));
        Assert.Equal(4, result.Inspected);
    }

    [Fact]
    public void AnExternalNameAllowed_IsNotReported()
    {
        AssemblyRuleResult result = new FriendGrantRule([Solution, External]).Analyze(Granting);

        Assert.DoesNotContain(result.Findings, f => f.Subject == External);
        Assert.Equal(2, result.Findings.Count);
    }

    /// <summary>The runtime compares simple names case-insensitively, and so does the rule.</summary>
    [Fact]
    public void NamesCompare_CaseInsensitively()
    {
        AssemblyRuleResult result = new FriendGrantRule([Solution.ToUpperInvariant()]).Analyze(Granting);

        Assert.DoesNotContain(result.Findings, f => f.Subject == Solution);
    }

    /// <summary>⭐ Configured with nothing, it says it checked nothing, as BNAQ1003 does.</summary>
    [Fact]
    public void Unconfigured_ReportsThatItInspectedNothing()
    {
        AssemblyRuleResult result = new FriendGrantRule().Analyze(Granting);

        Assert.Empty(result.Findings);
        Assert.Equal(0, result.Inspected);
    }

    /// <summary>An assembly that grants nothing has nothing to check.</summary>
    [Fact]
    public void AnAssemblyWithNoGrants_InspectsNothing()
    {
        AssemblyRuleResult result = new FriendGrantRule([Solution]).Analyze(AssemblyScanContext.Of(typeof(IAssemblyRule).Assembly));

        Assert.Empty(result.Findings);
        Assert.Equal(0, result.Inspected);
    }
}
