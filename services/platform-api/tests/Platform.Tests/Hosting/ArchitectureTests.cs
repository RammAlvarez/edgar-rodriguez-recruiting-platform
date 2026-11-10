using System.Reflection;

namespace Platform.Tests.Hosting;

/// <summary>
/// Module boundaries (ARCHITECTURE §2, PLAN D101): Reclutamiento and Nómina are separate businesses, so the Recruiting module
/// never depends on Payroll or Bank; Core depends on nothing of ours; Bank builds on Payroll, never the other way round; only the
/// host (Platform.Api) sees everything. Reads the assemblies' real references, so a new dependency fails here, not in review.
/// </summary>
public sealed class ArchitectureTests
{
    // Each assembly may reference only these of our assemblies (Platform.*). Infrastructure wires the modules together.
    public static TheoryData<string, string[]> Allowed => new()
    {
        { "Platform.Core", [] },
        { "Platform.Payroll.Engine", [] },
        { "Platform.Recruiting", ["Platform.Core"] },
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

    [Fact]
    public void Recruiting_never_depends_on_payroll()
    {
        var names = Assembly.Load("Platform.Recruiting").GetReferencedAssemblies().Select(r => r.Name);
        Assert.DoesNotContain("Platform.Payroll", names);
        Assert.DoesNotContain("Platform.Bank", names);
    }
}
