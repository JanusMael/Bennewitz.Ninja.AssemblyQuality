using System.Reflection;
using System.Runtime.Loader;
using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;
using Xunit;

namespace AssemblyQuality.Tests.Rules;

/// <summary>
/// BNAQ1006 against a consumer compiled against one provider version and loaded beside the next,
/// as a provider's release check would load its published consumers beside its new build.
/// </summary>
public sealed class FriendReferenceTests
{
    private const string Consumer = "AssemblyQuality.Fixtures.FriendConsumer";

    /// <summary>The consumer, loaded beside the provider in <paramref name="folder"/>, in a context of its own.</summary>
    private static Assembly LoadConsumer(string folder)
    {
        string root = Path.Combine(AppContext.BaseDirectory, folder);
        AssemblyLoadContext context = new(folder, isCollectible: true);
        context.Resolving += (_, name) =>
        {
            string candidate = Path.Combine(root, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        return context.LoadFromAssemblyPath(Path.Combine(root, Consumer + ".dll"));
    }

    /// <summary>⚠ The premise, checked: beside version two, calling the consumer really does fail.</summary>
    [Fact]
    public void BesideVersionTwo_TheConsumerReallyBreaks()
    {
        MethodInfo call = LoadConsumer("friend-v2").GetType("FriendConsumer.Use")!.GetMethod("Call")!;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => call.Invoke(null, null));
        Assert.IsType<MissingMethodException>(thrown.InnerException);
    }

    /// <summary>
    /// ⛔ Both breaks a provider's next release can cause without failing its own compile: a removed
    /// internal, and a withdrawn grant on an internal that is still there.
    /// </summary>
    [Fact]
    public void BesideVersionTwo_BothBreaksAreReported()
    {
        AssemblyRuleResult result = new FriendReferenceRule().Analyze(AssemblyScanContext.Of(LoadConsumer("friend-v2")));

        Assert.Contains(result.Findings, f => f.Subject.Contains("Secret", StringComparison.Ordinal) && f.Message.Contains("MissingMethodException", StringComparison.Ordinal));
        Assert.Contains(result.Findings, f => f.Subject.Contains("Kept", StringComparison.Ordinal) && f.Message.Contains("MethodAccessException", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Findings, f => f.Subject.Contains("Open", StringComparison.Ordinal));
        Assert.Equal(2, result.Findings.Count);
    }

    /// <summary>⭐ Beside the version it was built against, nothing is wrong, and the references were really examined.</summary>
    [Fact]
    public void BesideVersionOne_NothingIsReported_AndTheReferencesWereExamined()
    {
        AssemblyRuleResult result = new FriendReferenceRule().Analyze(AssemblyScanContext.Of(LoadConsumer("friend-v1")));

        Assert.Empty(result.Findings);
        Assert.Empty(result.Skipped);
        Assert.True(result.Inspected >= 3, $"Secret, Kept and Open must be examined; inspected {result.Inspected}.");
    }

    /// <summary>
    /// ⭐ References only into the shared framework can never skew, so an assembly with nothing else
    /// reports that it checked nothing.
    /// </summary>
    [Fact]
    public void AnAssemblyThatReferencesOnlyTheFramework_InspectsNothing()
    {
        AssemblyRuleResult result = new FriendReferenceRule().Analyze(AssemblyScanContext.Of(typeof(IAssemblyRule).Assembly));

        Assert.Empty(result.Findings);
        Assert.Equal(0, result.Inspected);
    }

    /// <summary>A provider that will not load is named in Skipped, not guessed at.</summary>
    [Fact]
    public void AProviderThatWillNotLoad_IsSkipped()
    {
        Assembly orphan = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "orphan", "AssemblyQuality.Fixtures.Orphan.dll"));

        AssemblyRuleResult result = new FriendReferenceRule().Analyze(AssemblyScanContext.Of(orphan));

        Assert.Contains(result.Skipped, s => s.Contains("AssemblyQuality.Fixtures.Absent", StringComparison.Ordinal));
    }
}
