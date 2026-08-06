using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalAttentionWorkspaceTests
{
    [Fact]
    public void StableTopologyPreservesLaneAcrossAttentionCircuit()
    {
        for (var channel = 0; channel < AttentionWorkspaceTopology.ChannelCount; channel++)
        {
            var cortical = 70 + channel;
            var pulvinar = AttentionWorkspaceTopology.Project(
                cortical,
                StructureId.Pfc,
                350,
                StructureId.Pulvinar,
                179);
            var thalamus = AttentionWorkspaceTopology.Project(
                pulvinar,
                StructureId.Pulvinar,
                350,
                StructureId.Thalamus,
                181);
            var returned = AttentionWorkspaceTopology.Project(
                thalamus,
                StructureId.Thalamus,
                350,
                StructureId.Pfc,
                191);

            Assert.Equal(channel, AttentionWorkspaceTopology.ChannelForNeuron(pulvinar, StructureId.Pulvinar));
            Assert.Equal(channel, AttentionWorkspaceTopology.ChannelForNeuron(thalamus, StructureId.Thalamus));
            Assert.Equal(channel, AttentionWorkspaceTopology.ChannelForNeuron(returned, StructureId.Pfc));
        }
    }

    [Fact]
    public void DistributedCompetitionSelectsOneLaneAndBoundsMaintenanceCapacity()
    {
        var decision = NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(2));

        Assert.True(decision.Available);
        Assert.True(decision.Active);
        Assert.Equal(2, decision.SelectedChannel);
        Assert.True(decision.SelectionMargin > 0.01);
        Assert.InRange(decision.CapacityUsed, 1, 4);
        Assert.Contains(2, decision.MaintainedChannels);
        Assert.True(decision.BroadcastActive);
        Assert.Equal(2, decision.BroadcastChannel);
    }

    [Fact]
    public void DistractorCompetitionNarrowsSelectionMargin()
    {
        var clean = NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(0, competitor: 0.08f));
        var distracted = NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(0, competitor: 0.78f));

        Assert.True(clean.Active);
        Assert.True(distracted.Active);
        Assert.Equal(0, distracted.SelectedChannel);
        Assert.True(distracted.SelectionMargin < clean.SelectionMargin);
    }

    [Fact]
    public void TrnStimulationSuppressesItsTargetLane()
    {
        var baseline = NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(0, competitor: 0.62f));
        var suppressed = NeuronalAttentionWorkspaceDecoder.Decode(
            CreateCircuit(0, competitor: 0.62f, selectedTrnSuppression: 1f));

        Assert.Equal(0, baseline.SelectedChannel);
        Assert.NotEqual(0, suppressed.SelectedChannel);
        Assert.True(suppressed.Channels[0].CompetitionScore < baseline.Channels[0].CompetitionScore);
    }

    [Fact]
    public void PulvinarAblationLowersPriorityAndPfcAblationRemovesMaintenance()
    {
        var intact = NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(4));
        var noPulvinar = NeuronalAttentionWorkspaceDecoder.Decode(
            CreateCircuit(4).Where(static snapshot => snapshot.StructureId != StructureId.Pulvinar).ToArray());
        var noPfc = NeuronalAttentionWorkspaceDecoder.Decode(
            CreateCircuit(4).Where(static snapshot => snapshot.StructureId != StructureId.Pfc).ToArray());

        Assert.True(noPulvinar.Channels[4].CompetitionScore < intact.Channels[4].CompetitionScore);
        Assert.Empty(noPfc.MaintainedChannels);
        Assert.Equal(0, noPfc.CapacityUsed);
        Assert.True(noPfc.Active);
    }

    [Fact]
    public void IntralaminarAblationRemovesBroadcastButNotLocalSelection()
    {
        var ablated = NeuronalAttentionWorkspaceDecoder.Decode(
            CreateCircuit(5).Where(static snapshot => snapshot.StructureId != StructureId.IntralaminarThalamus).ToArray());

        Assert.True(ablated.Active);
        Assert.Equal(5, ablated.SelectedChannel);
        Assert.False(ablated.BroadcastActive);
        Assert.Equal(-1, ablated.BroadcastChannel);
    }

    [Fact]
    public void CoreThalamicAblationPreventsSelectionAuthority()
    {
        var ablated = NeuronalAttentionWorkspaceDecoder.Decode(
            CreateCircuit(1).Where(static snapshot => snapshot.StructureId is not (
                StructureId.Pulvinar or StructureId.Thalamus)).ToArray());

        Assert.True(ablated.Available);
        Assert.False(ablated.Active);
        Assert.Equal(-1, ablated.SelectedChannel);
        Assert.True(ablated.CircuitCoverage < 0.75);
    }

    [Fact]
    public void NeuronalPayloadContainsNoSemanticSelectionLabels()
    {
        var payload = JsonSerializer.Serialize(NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(3)));

        Assert.DoesNotContain("visual", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auditory", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("language", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("memory", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NeuronalWinnerOverridesLegacyFocusAndBiasesPerception()
    {
        var decision = NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(1));
        var legacy = BiologicalAttentionRuntime.Default with
        {
            DominantChannel = "visual",
            Visual = 0.90f,
            Auditory = 0.01f
        };

        var authoritative = NeuronalAttentionWorkspaceDecoder.ApplyAuthority(
            42,
            legacy,
            legacy,
            decision);

        Assert.Equal("auditory", authoritative.DominantChannel);
        Assert.True(authoritative.Auditory > authoritative.Visual);
        Assert.True(authoritative.SensoryBias.Auditory > authoritative.SensoryBias.Visual);
        Assert.Equal(42, authoritative.LastSwitchTick);
    }

    [Fact]
    public void IncompleteNeuronalCircuitCannotSilentlyFallBackToLegacyWinner()
    {
        var incomplete = NeuronalAttentionWorkspaceDecoder.Decode(
            CreateCircuit(0).Where(static snapshot => snapshot.StructureId is not (
                StructureId.Pulvinar or StructureId.Thalamus)).ToArray());
        var legacy = BiologicalAttentionRuntime.Default with { DominantChannel = "visual" };

        var authoritative = NeuronalAttentionWorkspaceDecoder.ApplyAuthority(
            43,
            legacy,
            legacy,
            incomplete);

        Assert.True(incomplete.Available);
        Assert.False(incomplete.Active);
        Assert.Equal("none", authoritative.DominantChannel);
        Assert.Equal(0f, authoritative.FocusConfidence);
    }

    [Fact]
    public void MissingNeuronalCircuitCannotRestoreLegacyWinner()
    {
        var legacy = BiologicalAttentionRuntime.Default with
        {
            DominantChannel = "visual",
            Visual = 0.95f,
            FocusConfidence = 0.90f
        };

        var authoritative = NeuronalAttentionWorkspaceDecoder.ApplyAuthority(
            44,
            legacy,
            legacy,
            NeuronalAttentionWorkspaceDecision.Unavailable);

        Assert.Equal("none", authoritative.DominantChannel);
        Assert.Equal(0f, authoritative.Visual);
        Assert.Equal(0f, authoritative.FocusConfidence);
    }

    [Fact]
    public void SpontaneousSensoryBiasComesFromNeuronalAttentionScores()
    {
        var visual = NeuronalAttentionWorkspaceDecoder.ToSensoryBias(
            NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(0)));
        var auditory = NeuronalAttentionWorkspaceDecoder.ToSensoryBias(
            NeuronalAttentionWorkspaceDecoder.Decode(CreateCircuit(1)));

        Assert.True(visual.Visual > visual.Auditory);
        Assert.True(auditory.Auditory > auditory.Visual);
        Assert.Equal(1f, visual.Visual + visual.Auditory + visual.Somatosensory + visual.Interoceptive, 5);
        Assert.Equal(1f, auditory.Visual + auditory.Auditory + auditory.Somatosensory + auditory.Interoceptive, 5);
    }

    [Fact]
    public void MissingNeuronalAttentionProducesNeutralSensoryBias()
    {
        var bias = NeuronalAttentionWorkspaceDecoder.ToSensoryBias(
            NeuronalAttentionWorkspaceDecision.Unavailable);

        Assert.Equal(0.25f, bias.Visual);
        Assert.Equal(0.25f, bias.Auditory);
        Assert.Equal(0.25f, bias.Somatosensory);
        Assert.Equal(0.25f, bias.Interoceptive);
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CreateCircuit(
        int selected,
        float competitor = 0.10f,
        float selectedTrnSuppression = 0.04f)
        =>
        [
            Snapshot(StructureId.V1, selected, competitor, sensory: 0.96f),
            Snapshot(StructureId.Pulvinar, selected, competitor, pulvinar: 0.90f),
            Snapshot(StructureId.Thalamus, selected, competitor, relay: 0.86f),
            Snapshot(StructureId.Trn, selected, competitor, trn: selectedTrnSuppression, competitorTrn: 0.22f),
            Snapshot(StructureId.MediodorsalThalamus, selected, competitor, mediodorsal: 0.72f),
            Snapshot(StructureId.Pfc, selected, competitor, pfc: 0.78f),
            Snapshot(StructureId.IntralaminarThalamus, selected, competitor, broadcast: 0.68f)
        ];

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        int selected,
        float competitor,
        float sensory = 0f,
        float pulvinar = 0f,
        float trn = 0f,
        float competitorTrn = 0f,
        float relay = 0f,
        float mediodorsal = 0f,
        float pfc = 0f,
        float broadcast = 0f)
    {
        var channels = Enumerable.Range(0, 7)
            .Select(channel =>
            {
                var selectedScale = channel == selected ? 1f : competitor;
                var trnScale = channel == selected ? trn : competitorTrn;
                return new AttentionWorkspaceChannelActivity(
                    channel,
                    sensory * selectedScale,
                    pulvinar * selectedScale,
                    trnScale,
                    relay * selectedScale,
                    mediodorsal * selectedScale,
                    pfc * selectedScale,
                    broadcast * selectedScale,
                    0f);
            })
            .ToArray();
        var diagnostic = new NeuronalAttentionWorkspaceDiagnostics(
            structure,
            channels,
            selected,
            0.5f,
            structure == StructureId.Pfc ? [selected] : [],
            structure == StructureId.Trn ? competitorTrn : 0f);
        return new InstanceStructureSnapshot(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            32,
            4f,
            BrainRhythm.GAMMA,
            [],
            new NeuromodState(),
            0,
            0,
            0,
            NeuronalAttentionWorkspaceDiagnostics: diagnostic);
    }
}
