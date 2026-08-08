using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DistributedDeploymentTests
{
    [Fact]
    public void Manifest_Assigns_Every_Registered_Structure_Exactly_Once()
    {
        using var manifest = ReadJson("deploy", "distributed", "dnne-deploy.manifest.json");
        using var settings = ReadJson("ControlProgram", "appsettings.json");

        var registered = settings.RootElement.GetProperty("ServiceRegistry")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var assigned = manifest.RootElement.GetProperty("deployables")
            .EnumerateArray()
            .SelectMany(deployable => deployable.GetProperty("structures").EnumerateArray())
            .Select(structure => structure.GetString()!)
            .ToArray();

        Assert.Equal(90, registered.Length);
        Assert.Equal(registered.Length, assigned.Length);
        Assert.Equal(assigned.Length, assigned.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(registered, assigned.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Wpf_Apps_Are_Confined_To_Windows_Deployables()
    {
        using var manifest = ReadJson("deploy", "distributed", "dnne-deploy.manifest.json");
        var apps = manifest.RootElement.GetProperty("apps");

        foreach (var appProperty in apps.EnumerateObject())
        {
            var app = appProperty.Value;
            if (app.GetProperty("role").GetString() != "wpf")
            {
                continue;
            }

            var platforms = app.GetProperty("platforms").EnumerateArray().Select(value => value.GetString()!).ToArray();
            Assert.Equal(new[] { "windows" }, platforms);
        }

        foreach (var deployable in manifest.RootElement.GetProperty("deployables").EnumerateArray())
        {
            var platforms = deployable.GetProperty("platforms").EnumerateArray().Select(value => value.GetString()!).ToArray();
            foreach (var appId in deployable.GetProperty("apps").EnumerateArray().Select(value => value.GetString()!))
            {
                if (apps.GetProperty(appId).GetProperty("role").GetString() == "wpf")
                {
                    Assert.Equal(new[] { "windows" }, platforms);
                }
            }
        }
    }

    [Fact]
    public void Tartarus_Example_Assigns_All_Required_Deployables_To_Compatible_Nodes()
    {
        using var manifest = ReadJson("deploy", "distributed", "dnne-deploy.manifest.json");
        using var inventory = ReadJson("deploy", "distributed", "tartarus.inventory.example.json");

        var assignments = inventory.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .SelectMany(node => node.GetProperty("deployables").EnumerateArray()
                .Select(deployable => (Name: deployable.GetString()!, Platform: node.GetProperty("platform").GetString()!)))
            .ToDictionary(entry => entry.Name, entry => entry.Platform, StringComparer.Ordinal);

        foreach (var deployable in manifest.RootElement.GetProperty("deployables").EnumerateArray())
        {
            if (!deployable.GetProperty("required").GetBoolean())
            {
                continue;
            }

            var name = deployable.GetProperty("name").GetString()!;
            Assert.True(assignments.TryGetValue(name, out var platform), $"Required deployable {name} is not assigned.");
            Assert.Contains(platform, deployable.GetProperty("platforms").EnumerateArray().Select(value => value.GetString()));
        }
    }

    [Fact]
    public void Packaging_Requires_Validation_And_Ships_Node_Preflight()
    {
        var packageSource = ReadText("tools", "package-dnne-distributed.ps1");
        var validatorSource = ReadText("tools", "test-dnne-distributed-deployment.ps1");
        var preflightSource = ReadText("tools", "test-dnne-node.ps1");

        Assert.Contains("test-dnne-distributed-deployment.ps1", packageSource, StringComparison.Ordinal);
        Assert.Contains("test-dnne-node.ps1", packageSource, StringComparison.Ordinal);
        Assert.Contains("TargetPlatform", packageSource, StringComparison.Ordinal);
        Assert.Contains("Registered structure", validatorSource, StringComparison.Ordinal);
        Assert.Contains("shared-secret", preflightSource, StringComparison.Ordinal);
        Assert.Contains("control-clock", preflightSource, StringComparison.Ordinal);
        Assert.Contains("Test-PortAvailable", preflightSource, StringComparison.Ordinal);
    }

    private static JsonDocument ReadJson(params string[] path) => JsonDocument.Parse(ReadText(path));

    private static string ReadText(params string[] path)
    {
        var parts = new[] { ResolveRepositoryRoot() }.Concat(path).ToArray();
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string ResolveRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not resolve the DNNE repository root.");
    }
}
