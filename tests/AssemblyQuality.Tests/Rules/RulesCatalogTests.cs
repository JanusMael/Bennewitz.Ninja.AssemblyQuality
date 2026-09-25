using System.Reflection;
using Bennewitz.Ninja.AssemblyQuality;
using Xunit;

namespace AssemblyQuality.Tests.Rules;

/// <summary>
/// The catalogue itself: every rule is discoverable, uniquely identified, and listed in the README.
/// </summary>
/// <remarks>
/// ⛔ <b>A rule nobody can find is a rule nobody runs.</b> A new rule that compiles but is never
/// listed ships as dead weight, and the table drifting from the types is how a reader comes to
/// distrust the documentation and then stop reading it.
/// </remarks>
public sealed class RulesCatalogTests
{
    private const string Begin = "<!-- BEGIN GENERATED RULES -->";
    private const string End = "<!-- END GENERATED RULES -->";

    /// <summary>Every concrete rule in the library, instantiated through its parameterless constructor.</summary>
    internal static IReadOnlyList<IAssemblyRule> DiscoverRules()
    {
        return [.. typeof(IAssemblyRule).Assembly
            .GetExportedTypes()
            .Where(t => typeof(IAssemblyRule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => (IAssemblyRule)Activator.CreateInstance(t)!)];
    }

    [Fact]
    public void EveryRuleId_IsUnique()
    {
        string[] ids = [.. DiscoverRules().Select(rule => rule.Id)];

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// ⛔ Every id is <c>BNAQ</c> and four digits: the family scheme, <c>BN</c> plus the product's
    /// initials. The prefix is frozen once an id has shipped, because a consumer's suppression is
    /// keyed on it, so a rule added under any other prefix fails here rather than in a release.
    /// </summary>
    [Fact]
    public void EveryRuleId_CarriesTheFamilyPrefix()
    {
        foreach (IAssemblyRule rule in DiscoverRules())
        {
            Assert.Matches(@"^BNAQ\d{4}$", rule.Id);
        }
    }

    [Fact]
    public void EveryRule_SummarisesWhatItRequires()
    {
        foreach (IAssemblyRule rule in DiscoverRules())
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Summary), $"{rule.Id} has no summary");
            Assert.EndsWith(".", rule.Summary, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheCatalogue_IsNotSilentlyEmpty()
    {
        // ⚠ Every other test here passes over an empty catalogue.
        IReadOnlyList<IAssemblyRule> rules = DiscoverRules();

        Assert.Contains(rules, rule => rule.Id == "BNAQ1001");
        Assert.Contains(rules, rule => rule.Id == "BNAQ1002");
        Assert.Contains(rules, rule => rule.Id == "BNAQ1003");
        Assert.Contains(rules, rule => rule.Id == "BNAQ1004");
    }

    [Fact]
    public void TheReadmeTable_MatchesTheRuleTypes()
    {
        string path = ReadmePath();
        string readme = File.ReadAllText(path);
        string rendered = Render();

        if (Environment.GetEnvironmentVariable("AQ_UPDATE_DOCS") == "1")
        {
            int from = readme.IndexOf(Begin, StringComparison.Ordinal) + Begin.Length;
            int to = readme.IndexOf(End, StringComparison.Ordinal);
            File.WriteAllText(path, readme[..from] + "\n" + rendered + "\n" + readme[to..]);
            return;
        }

        Assert.Equal(rendered, ExtractRegion(readme), ignoreLineEndingDifferences: true);
    }

    private static string Render()
    {
        IEnumerable<string> rows = DiscoverRules()
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(rule => "| `" + rule.Id + "` | " + rule.Summary.Replace("|", "\\|", StringComparison.Ordinal) + " |");

        return string.Join("\n", ["| Id | Requires |", "|---|---|", .. rows]);
    }

    private static string ExtractRegion(string readme)
    {
        int from = readme.IndexOf(Begin, StringComparison.Ordinal);
        int to = readme.IndexOf(End, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from, "the README has no generated rules region");
        return readme[(from + Begin.Length)..to].Trim('\n', '\r');
    }

    private static string ReadmePath()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir, "README.md");
            if (File.Exists(candidate) && File.Exists(Path.Combine(dir, "AssemblyQuality.slnx")))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find the repository README from " + AppContext.BaseDirectory);
    }
}
