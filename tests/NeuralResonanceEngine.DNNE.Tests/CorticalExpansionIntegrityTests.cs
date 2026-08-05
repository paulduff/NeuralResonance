using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CorticalExpansionIntegrityTests
{
    private static readonly (StructureId Id, string Folder)[] ExpandedCircuits =
    [
        (StructureId.V3, "V3"),
        (StructureId.AuditoryAssociationCortex, "AuditoryAssociationCortex"),
        (StructureId.SecondarySomatosensoryCortex, "SecondarySomatosensoryCortex"),
        (StructureId.InferotemporalCortex, "InferotemporalCortex"),
        (StructureId.FusiformGyrus, "FusiformGyrus"),
        (StructureId.TemporalPole, "TemporalPole"),
        (StructureId.TemporoparietalJunction, "TemporoparietalJunction"),
        (StructureId.Precuneus, "Precuneus"),
        (StructureId.MidcingulateCortex, "MidcingulateCortex"),
        (StructureId.DorsomedialPrefrontalCortex, "DorsomedialPrefrontalCortex"),
        (StructureId.VentromedialPrefrontalCortex, "VentromedialPrefrontalCortex"),
        (StructureId.FrontalEyeFields, "FrontalEyeFields")
    ];

    [Fact]
    public void ExpandedCorticalCircuits_HaveHostsRegistryRoutesAndEditorTerritories()
    {
        var root = ResolveRepositoryRoot();
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "ControlProgram", "appsettings.json")));
        using var connectivity = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "connectivity", "dnne-connectivity.json")));
        var registry = settings.RootElement.GetProperty("ServiceRegistry");
        var layout = File.ReadAllText(Path.Combine(root, "src", "NRE.WpfEditor", "MainWindow.Brain3D.Layout.cs"));
        var territories = File.ReadAllText(Path.Combine(root, "src", "NRE.WpfEditor", "MainWindow.Brain3D.CorticalTerritories.cs"));

        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in connectivity.RootElement.EnumerateArray())
        {
            sources.Add(rule.GetProperty("source").GetString() ?? string.Empty);
            foreach (var connection in rule.GetProperty("connections").EnumerateArray())
            {
                targets.Add(connection.GetProperty("target").GetString() ?? string.Empty);
            }
        }

        foreach (var (id, folder) in ExpandedCircuits)
        {
            var name = id.ToString();
            var projectDirectory = Path.Combine(root, "Structures", folder);
            Assert.True(Directory.Exists(projectDirectory), $"Missing service directory for {name}.");
            Assert.Single(Directory.GetFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly));
            Assert.True(File.Exists(Path.Combine(projectDirectory, "Program.cs")), $"Missing host entry point for {name}.");
            Assert.True(registry.TryGetProperty(name, out _), $"Missing service registry endpoint for {name}.");
            Assert.Contains(name, sources);
            Assert.Contains(name, targets);
            Assert.Contains($"\"{name}\" => true", layout, StringComparison.Ordinal);
            Assert.Contains($"\"{name}\" => new", territories, StringComparison.Ordinal);
        }
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
