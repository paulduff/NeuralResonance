using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UmbrellaRetirementTests
{
    private static readonly string[] RetiredStructureNames =
    [
        "Thalamus",
        "Amygdala",
        "Hypothalamus",
        "GlobusPallidus",
        "DeepCerebellarNuclei",
        "Medulla",
        "Pons",
        "BasalForebrain"
    ];

    [Fact]
    public void ProtocolAndAtlas_ExposeOnlyConcreteRuntimeStructures()
    {
        var protocolNames = Enum.GetNames<StructureId>();

        Assert.Equal(119, protocolNames.Length);
        Assert.Equal(protocolNames.Length, StructureAtlas.All.Count);
        Assert.All(RetiredStructureNames, name =>
            Assert.False(Enum.TryParse<StructureId>(name, false, out _), $"Retired structure {name} is still a protocol identifier."));
        Assert.True(Enum.TryParse<StructureId>("PontineNuclei", false, out _));
        Assert.True(Enum.TryParse<StructureId>("NucleusBasalis", false, out _));
    }

    [Fact]
    public void RegistryDeploymentAndConnectome_CoverTheSameConcreteSet()
    {
        var expected = Enum.GetNames<StructureId>().ToHashSet(StringComparer.Ordinal);
        using var settings = ReadJson("ControlProgram", "appsettings.json");
        using var manifest = ReadJson("deploy", "distributed", "dnne-deploy.manifest.json");
        using var connectome = ReadJson("connectivity", "dnne-connectivity.json");

        var registered = settings.RootElement.GetProperty("ServiceRegistry")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        var assigned = manifest.RootElement.GetProperty("deployables")
            .EnumerateArray()
            .SelectMany(deployable => deployable.GetProperty("structures").EnumerateArray())
            .Select(structure => structure.GetString()!)
            .ToArray();
        var sources = connectome.RootElement.EnumerateArray()
            .Select(rule => rule.GetProperty("source").GetString()!)
            .ToArray();
        var targets = connectome.RootElement.EnumerateArray()
            .SelectMany(rule => rule.GetProperty("connections").EnumerateArray())
            .Select(connection => connection.GetProperty("target").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected, registered.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(expected.Count, registered.Length);
        Assert.Equal(expected, assigned.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(expected.Count, assigned.Length);
        Assert.Equal(expected, sources.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(expected.Count, sources.Length);
        Assert.Equal(expected, targets);
    }

    [Fact]
    public void RuntimeProjectsAndBlazorAtlas_DoNotContainUmbrellaServices()
    {
        var root = ResolveRepositoryRoot();
        foreach (var retiredDirectory in new[]
        {
            "Thalamus",
            "Amygdala",
            "Hypothalamus",
            "GlobusPallidus",
            "Cerebellum.DCN",
            "Medulla",
            "Pons",
            "BasalForebrainCholinergic"
        })
        {
            Assert.Empty(Directory.Exists(Path.Combine(root, "Structures", retiredDirectory))
                ? Directory.EnumerateFiles(Path.Combine(root, "Structures", retiredDirectory), "*.csproj")
                : []);
        }

        Assert.True(File.Exists(Path.Combine(root, "Structures", "PontineNuclei", "NeuralResonanceEngine.Structures.PontineNuclei.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "Structures", "NucleusBasalis", "NeuralResonanceEngine.Structures.NucleusBasalis.csproj")));

        using var atlas = ReadJson("src", "NRE.BlazorEditor", "wwwroot", "data", "brain-atlas.json");
        var rootElement = atlas.RootElement;
        var renderedIds = rootElement.GetProperty("structures")
            .EnumerateArray()
            .Select(instance => instance.GetProperty("structureId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(119, rootElement.GetProperty("definitionCount").GetInt32());
        Assert.Equal(228, rootElement.GetProperty("instanceCount").GetInt32());
        Assert.Equal(Enum.GetNames<StructureId>().ToHashSet(StringComparer.Ordinal), renderedIds);
        Assert.DoesNotContain(renderedIds, RetiredStructureNames.Contains);
    }

    private static JsonDocument ReadJson(params string[] path) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine([ResolveRepositoryRoot(), .. path])));

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
