using System.Reflection;

namespace AssemblyQuality.Tests.Packaging;

/// <summary>
/// Guards that every shipped assembly carries the IsTrimmable mark, read off the compiled output.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The mark travels inside the package</b>, as <c>[AssemblyMetadata("IsTrimmable", "True")]</c>,
/// and an app publishing with <c>TrimMode=partial</c> trims ONLY assemblies that carry it. Without it
/// a consumer's trimmed publish keeps the assembly whole and outside its trim analysis. Nothing fails,
/// so nothing would notice.
/// </para>
/// <para>
/// ⚠ Read from the DLL, never the project file: here the mark comes from <c>IsAotCompatible</c>, which
/// implies <c>IsTrimmable</c>, so no project file names it, and the DLL is what a consumer's publish
/// obeys. Marked trimmable is not the same as trim-safe: the reflection entry points carry
/// <c>[RequiresUnreferencedCode]</c>, which is what a trimmed consumer is warned by.
/// </para>
/// </remarks>
public sealed class TrimmableTests
{
    [Fact]
    public void Every_shipped_assembly_is_marked_trimmable()
    {
        string output = Path.GetDirectoryName(typeof(TrimmableTests).Assembly.Location)!;

        string[] projects =
        [
            .. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(name => name!),
        ];

        // ⛔ Without this, an empty src/ would pass every assertion below.
        Assert.NotEmpty(projects);

        List<string> unmarked = [];

        foreach (string project in projects)
        {
            string path = Path.Combine(output, project + ".dll");

            // A project missing from the output would otherwise be skipped rather than checked.
            Assert.True(File.Exists(path), $"{project}.dll is not in {output}, so its mark cannot be checked.");

            bool marked = Assembly.LoadFrom(path)
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(a => a.Key == "IsTrimmable"
                          && string.Equals(a.Value, "True", StringComparison.OrdinalIgnoreCase));

            if (!marked)
            {
                unmarked.Add(project);
            }
        }

        Assert.True(
            unmarked.Count == 0,
            "These shipped assemblies are not marked trimmable:\n  " + string.Join("\n  ", unmarked)
            + "\n\nAn app publishing with TrimMode=partial trims ONLY marked assemblies, so these would "
            + "ship whole and outside its trim analysis. Check IsAotCompatible in the project file.");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
