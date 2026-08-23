using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ConnectomeIntegrityTests
{
    [Fact]
    public void Connectome_HasUniqueWellFormedEdgesAndCompleteParticipation()
    {
        var edges = LoadEdges();
        var structures = Enum.GetValues<StructureId>();

        Assert.Equal(edges.Count, edges.Select(edge => edge.SynapseId).Distinct().Count());
        Assert.Equal(
            edges.Count,
            edges.Select(edge => (edge.Source, edge.Target, edge.Neurotransmitter, edge.ProjectionType)).Distinct().Count());
        Assert.All(edges, edge =>
        {
            Assert.NotEqual(Guid.Empty, edge.SynapseId);
            Assert.False(string.IsNullOrWhiteSpace(edge.ProjectionType));
        });
        Assert.DoesNotContain(structures, structure => edges.All(edge => edge.Source != structure));
        Assert.DoesNotContain(structures, structure => edges.All(edge => edge.Target != structure));

        var selfLoops = edges.Where(edge => edge.Source == edge.Target).ToArray();
        var ca3Loop = Assert.Single(selfLoops);
        Assert.Equal(StructureId.CA3, ca3Loop.Source);
        Assert.Equal("ca3_recurrent_feedback", ca3Loop.ProjectionType);
    }

    [Fact]
    public void Connectome_IsStronglyConnectedIncludingCorticofugalAuditoryFeedback()
    {
        var edges = LoadEdges();
        var graph = edges
            .GroupBy(edge => edge.Source)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.Target).ToArray());

        Assert.Contains(edges, edge =>
            edge.Source == StructureId.A1 &&
            edge.Target == StructureId.InferiorColliculus &&
            edge.Neurotransmitter == NTEnum.GLUTAMATE &&
            edge.ProjectionType == "corticocollicular_feedback");
        Assert.Contains(edges, edge =>
            edge.Source == StructureId.InferiorColliculus &&
            edge.Target == StructureId.SuperiorOlive &&
            edge.Neurotransmitter == NTEnum.GLUTAMATE &&
            edge.ProjectionType == "colliculo_olivary_feedback");

        foreach (var source in Enum.GetValues<StructureId>())
        {
            var visited = new HashSet<StructureId> { source };
            var frontier = new Queue<StructureId>([source]);
            while (frontier.TryDequeue(out var current))
            {
                if (!graph.TryGetValue(current, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (visited.Add(target))
                    {
                        frontier.Enqueue(target);
                    }
                }
            }

            Assert.Equal(Enum.GetValues<StructureId>().Length, visited.Count);
        }
    }

    [Fact]
    public void VentralPallidumRetainsItsMotivationalInhibitionAndOutputGates()
    {
        var edges = LoadEdges();

        Assert.Contains(edges, edge =>
            edge.Source == StructureId.NucleusAccumbens &&
            edge.Target == StructureId.VentralPallidum &&
            edge.Neurotransmitter == NTEnum.GABA &&
            edge.ProjectionType == "ventral_striatopallidal_inhibition");
        Assert.Contains(edges, edge =>
            edge.Source == StructureId.VentralPallidum &&
            edge.Target == StructureId.Habenula &&
            edge.Neurotransmitter == NTEnum.GABA &&
            edge.ProjectionType == "ventral_pallidal_habenula_control");
        Assert.Contains(edges, edge =>
            edge.Source == StructureId.VentralPallidum &&
            edge.Target == StructureId.MediodorsalThalamus &&
            edge.Neurotransmitter == NTEnum.GABA &&
            edge.ProjectionType == "ventral_pallidal_association_gating");
        Assert.Contains(edges, edge =>
            edge.Source == StructureId.VentralPallidum &&
            edge.Target == StructureId.MotorThalamus &&
            edge.Neurotransmitter == NTEnum.GABA &&
            edge.ProjectionType == "ventral_pallidal_thalamic_gating");
    }

    private static IReadOnlyList<ConnectomeEdge> LoadEdges()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ResolveConnectivityPath()));
        var edges = new List<ConnectomeEdge>();
        foreach (var rule in document.RootElement.EnumerateArray())
        {
            Assert.True(rule.TryGetProperty("source", out var sourceElement));
            Assert.True(Enum.TryParse<StructureId>(sourceElement.GetString(), ignoreCase: true, out var source));
            Assert.True(rule.TryGetProperty("connections", out var connections));

            foreach (var connection in connections.EnumerateArray())
            {
                Assert.True(Enum.TryParse<StructureId>(connection.GetProperty("target").GetString(), ignoreCase: true, out var target));
                Assert.True(Guid.TryParse(connection.GetProperty("synapseId").GetString(), out var synapseId));
                Assert.True(Enum.TryParse<NTEnum>(connection.GetProperty("neurotransmitter").GetString(), ignoreCase: true, out var neurotransmitter));
                var projectionType = connection.GetProperty("projectionType").GetString() ?? string.Empty;
                edges.Add(new ConnectomeEdge(source, target, synapseId, neurotransmitter, projectionType));
            }
        }

        return edges;
    }

    private static string ResolveConnectivityPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "connectivity", "dnne-connectivity.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not resolve connectivity/dnne-connectivity.json from test base directory.");
    }

    private sealed record ConnectomeEdge(
        StructureId Source,
        StructureId Target,
        Guid SynapseId,
        NTEnum Neurotransmitter,
        string ProjectionType);
}
