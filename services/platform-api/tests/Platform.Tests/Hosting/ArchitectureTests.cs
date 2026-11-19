using System.Reflection;
using System.Xml.Linq;

namespace Platform.Tests.Hosting;

/// <summary>
/// Module boundaries (ARCHITECTURE §2, PLAN D101): Reclutamiento and Nómina are separate businesses, so the Recruiting module
/// never depends on Payroll or Bank; Core depends on nothing of ours; Bank builds on Payroll, never the other way round; only the
/// host (Platform.Api) sees everything. Checks both the compiled references and the csproj ProjectReferences: a compiled reference only exists when the code uses a type of the other assembly (constants are inlined and leave none), so the csproj is what catches a dependency added before it is used.
/// </summary>
public sealed class ArchitectureTests
{
    // Each assembly may reference only these of our assemblies (Platform.*). Infrastructure wires the modules together.
    public static TheoryData<string, string[]> Allowed => new()
    {
        { "Platform.Core", [] },
        { "Platform.Payroll.Engine", [] },
        { "Platform.Recruiting", ["Platform.Core"] },
        // Payroll still uses a few Recruiting types (the report-export DTO and candidate types; conversion itself was removed in D101); the reverse never.
        { "Platform.Payroll", ["Platform.Core", "Platform.Payroll.Engine", "Platform.Recruiting"] },
        { "Platform.Bank", ["Platform.Core", "Platform.Payroll", "Platform.Payroll.Engine", "Platform.Recruiting"] },
        {
            "Platform.Infrastructure",
            ["Platform.Core", "Platform.Payroll.Engine", "Platform.Recruiting", "Platform.Payroll", "Platform.Bank"]
        },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void An_assembly_references_only_the_modules_it_may_build_on(string assemblyName, string[] allowed)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => n.StartsWith("Platform.", StringComparison.Ordinal))
            .Order()
            .ToList();

        var forbidden = references.Except(allowed).ToList();
        Assert.True(forbidden.Count == 0, $"{assemblyName} must not reference {string.Join(", ", forbidden)}.");
    }

    private static string SourceDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Platform.slnx")))
                return Path.Combine(dir.FullName, "src");
        throw new InvalidOperationException("Platform.slnx not found above the test output.");
    }

    [Fact]
    public void Project_references_follow_the_same_rules_and_every_source_project_is_covered()
    {
        var allowed = Allowed.ToDictionary(row => (string)row[0], row => (string[])row[1]);
        allowed["Platform.Api"] = [.. allowed["Platform.Infrastructure"], "Platform.Infrastructure"]; // the host sees everything

        foreach (var csproj in Directory.GetFiles(SourceDirectory(), "Platform.*.csproj", SearchOption.AllDirectories))
        {
            var project = Path.GetFileNameWithoutExtension(csproj);
            Assert.True(allowed.TryGetValue(project, out var may), $"{project} has no row in the architecture rules; add it to Allowed.");
            var references = XDocument.Load(csproj).Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension(((string)r.Attribute("Include")!).Replace('\\', '/')))
                .ToList();
            var forbidden = references.Except(may!).ToList();
            Assert.True(forbidden.Count == 0, $"{project}.csproj must not reference {string.Join(", ", forbidden)}.");
        }
    }

    [Fact]
    public void Recruiting_never_depends_on_payroll()
    {
        var names = Assembly.Load("Platform.Recruiting").GetReferencedAssemblies().Select(r => r.Name);
        Assert.DoesNotContain("Platform.Payroll", names);
        Assert.DoesNotContain("Platform.Bank", names);
    }
}
