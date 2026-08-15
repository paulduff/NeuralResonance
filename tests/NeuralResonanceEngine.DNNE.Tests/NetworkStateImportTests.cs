using NeuralResonanceEngine.ControlProgram;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NetworkStateImportTests
{
    [Fact]
    public void ImportPreservesActiveDeploymentAndAnatomicalTopology()
    {
        var configuredSynapse = Guid.NewGuid();
        var state = new SimulationState();
        state.Configure(
            1.0,
            new Dictionary<StructureId, string>
            {
                [StructureId.BasolateralAmygdala] = "http://localhost:52303",
                [StructureId.CentralAmygdala] = "http://localhost:52304"
            },
            new Dictionary<StructureId, List<SynapticConnection>>
            {
                [StructureId.BasolateralAmygdala] =
                [
                    new SynapticConnection(
                        StructureId.CentralAmygdala,
                        configuredSynapse,
                        NTEnum.GLUTAMATE,
                        "amygdala-threat-appraisal")
                ]
            });

        var checkpoint = new NetworkStateDocument
        {
            SchemaVersion = NetworkStateDocument.CurrentSchemaVersion,
            Tick = 42,
            SimulationClockMs = 42,
            ServiceRegistry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [StructureId.BasolateralAmygdala.ToString()] = "http://stale-host:5000"
            },
            ConnectivityMap = new Dictionary<string, List<SynapticConnection>>(StringComparer.OrdinalIgnoreCase)
            {
                [StructureId.A1.ToString()] =
                [
                    new SynapticConnection(
                        StructureId.IntralaminarThalamus,
                        Guid.NewGuid(),
                        NTEnum.GLUTAMATE,
                        "legacy-route")
                ]
            }
        };

        Assert.True(state.TryImportNetworkState(checkpoint, out var report, out var error), error);

        Assert.Equal("http://localhost:52303", state.ServiceRegistry[StructureId.BasolateralAmygdala]);
        Assert.Equal("http://localhost:52304", state.ServiceRegistry[StructureId.CentralAmygdala]);
        var connection = Assert.Single(state.ConnectivityMap[StructureId.BasolateralAmygdala]);
        Assert.Equal(configuredSynapse, connection.SynapseId);
        Assert.False(state.ConnectivityMap.ContainsKey(StructureId.A1));
        Assert.Contains(report.Warnings, warning => warning.Contains("deployment configuration", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("anatomical connectome", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportCanPopulateAnUnconfiguredState()
    {
        var importedSynapse = Guid.NewGuid();
        var state = new SimulationState();
        var checkpoint = new NetworkStateDocument
        {
            SchemaVersion = NetworkStateDocument.CurrentSchemaVersion,
            ServiceRegistry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [StructureId.BasolateralAmygdala.ToString()] = "http://localhost:52303"
            },
            ConnectivityMap = new Dictionary<string, List<SynapticConnection>>(StringComparer.OrdinalIgnoreCase)
            {
                [StructureId.BasolateralAmygdala.ToString()] =
                [
                    new SynapticConnection(
                        StructureId.CentralAmygdala,
                        importedSynapse,
                        NTEnum.GLUTAMATE,
                        "amygdala-threat-appraisal")
                ]
            }
        };

        Assert.True(state.TryImportNetworkState(checkpoint, out var report, out var error), error);

        Assert.Equal("http://localhost:52303", state.ServiceRegistry[StructureId.BasolateralAmygdala]);
        Assert.Equal(importedSynapse, Assert.Single(state.ConnectivityMap[StructureId.BasolateralAmygdala]).SynapseId);
        Assert.Empty(report.Warnings);
    }
}
