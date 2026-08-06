using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using Xunit;

namespace NeuralResonanceEngine.DNNE.Tests;

/// <summary>
/// Regression guard for axonal fan-out routing (Finding 5 fix).
///
/// A structure's spike must propagate along EVERY connectome edge of the source, not just
/// its DefaultTarget. Before the fix, <c>ResolveRoute</c> selected a single edge (the one
/// matching the spike's own target/NT), so divergent projections — e.g. the basal-ganglia
/// indirect pathway Striatum-&gt;GPe and the hyperdirect SMA-&gt;STN — never carried forward
/// spikes. <see cref="TickCoordinator.ResolveRoutes"/> now returns all candidate edges.
/// </summary>
public sealed class RoutingFanOutTests
{
    [Theory]
    // source,            defaultTarget,        divergent edge that must also be routed
    [InlineData(StructureId.Striatum, StructureId.GPi, StructureId.GPe, NTEnum.GABA)]       // BG indirect pathway
    [InlineData(StructureId.Sma, StructureId.M1, StructureId.Stn, NTEnum.GLUTAMATE)]        // BG hyperdirect pathway
    public void ResolveRoutes_FansOutToDivergentEdges(
        StructureId source, StructureId defaultTarget, StructureId divergentTarget, NTEnum emittedNt)
    {
        var candidates = LoadConnections(source);

        // A firing neuron emits to its structure's single DefaultTarget with a fresh synapse id.
        var spike = new SpikeMessage
        {
            MessageId = Guid.NewGuid(),
            SourceStructure = source,
            TargetStructure = defaultTarget,
            Neurotransmitter = emittedNt,
            SynapseId = Guid.NewGuid(),
            VesicleQuanta = 1f,
        };

        var routes = TickCoordinator.ResolveRoutes(candidates, spike);
        var routedTargets = routes.Select(r => r.Target).ToHashSet();

        // Fan-out reaches BOTH the default target and the previously-dead divergent edge.
        Assert.Contains(defaultTarget, routedTargets);
        Assert.Contains(divergentTarget, routedTargets);
    }

    [Fact]
    public void ResolveRoutes_ReturnsEveryConnectomeEdge_ForEveryStructure()
    {
        // For every structure, the routed target set equals the full set of its connectome
        // edges — no divergent collateral is dropped.
        foreach (var (source, connections) in LoadAllRules())
        {
            if (connections.Count == 0)
            {
                continue;
            }

            var primary = connections[0];
            var spike = new SpikeMessage
            {
                MessageId = Guid.NewGuid(),
                SourceStructure = source,
                TargetStructure = primary.Target,
                Neurotransmitter = primary.Neurotransmitter,
                SynapseId = Guid.NewGuid(),
                VesicleQuanta = 1f,
            };

            var routedTargets = TickCoordinator.ResolveRoutes(connections, spike).Select(r => r.Target).ToHashSet();
            var expectedTargets = connections.Select(c => c.Target).ToHashSet();
            Assert.Equal(expectedTargets, routedTargets);
        }
    }

    [Fact]
    public void StriatalD1AndD2NeuronsUseAnatomicallyDistinctProjectionSets()
    {
        var candidates = LoadConnections(StructureId.Striatum);
        var d1 = TickCoordinator.ResolveRoutes(candidates, new SpikeMessage
        {
            SourceStructure = StructureId.Striatum,
            SourceNeuronId = "n-000"
        });
        var d2 = TickCoordinator.ResolveRoutes(candidates, new SpikeMessage
        {
            SourceStructure = StructureId.Striatum,
            SourceNeuronId = "n-001"
        });

        Assert.Contains(d1, route => route.Target == StructureId.GPi);
        Assert.Contains(d1, route => route.Target == StructureId.Snr);
        Assert.DoesNotContain(d1, route => route.Target == StructureId.GPe);

        Assert.Contains(d2, route => route.Target == StructureId.GPe);
        Assert.DoesNotContain(d2, route => route.Target is StructureId.GPi or StructureId.Snr or StructureId.Snc);
    }

    // ---- connectivity loading (mirrors MajorPathwayIntegrationTests) ----

    private static List<SynapticConnection> LoadConnections(StructureId source)
    {
        var match = LoadAllRules().FirstOrDefault(r => r.Source == source);
        Assert.True(match.Connections is { Count: > 0 }, $"No connections found for {source}.");
        return match.Connections;
    }

    private static List<(StructureId Source, List<SynapticConnection> Connections)> LoadAllRules()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ResolveConnectivityPath()));
        var result = new List<(StructureId, List<SynapticConnection>)>();
        foreach (var rule in doc.RootElement.EnumerateArray())
        {
            if (!rule.TryGetProperty("source", out var src) ||
                !Enum.TryParse<StructureId>(src.GetString(), ignoreCase: true, out var source))
            {
                continue;
            }

            var connections = new List<SynapticConnection>();
            if (rule.TryGetProperty("connections", out var conns))
            {
                foreach (var conn in conns.EnumerateArray())
                {
                    if (!Enum.TryParse<StructureId>(conn.GetProperty("target").GetString(), ignoreCase: true, out var target) ||
                        !Enum.TryParse<NTEnum>(conn.GetProperty("neurotransmitter").GetString(), ignoreCase: true, out var nt))
                    {
                        continue;
                    }

                    var synapseId = conn.TryGetProperty("synapseId", out var sid) && Guid.TryParse(sid.GetString(), out var g)
                        ? g
                        : Guid.NewGuid();
                    var projection = conn.TryGetProperty("projectionType", out var p) ? p.GetString() ?? string.Empty : string.Empty;
                    connections.Add(new SynapticConnection(target, synapseId, nt, projection));
                }
            }

            result.Add((source, connections));
        }

        return result;
    }

    private static string ResolveConnectivityPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "connectivity", "dnne-connectivity.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve connectivity/dnne-connectivity.json from test base directory.");
    }
}
