using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalAffectValuationTests
{
    [Fact]
    public void RewardAndHomeostaticPopulationsProduceAppetitiveValuation()
    {
        var decision = NeuronalAffectValuationDecoder.Decode(AppetitiveCircuit());

        Assert.True(decision.Available);
        Assert.True(decision.Active);
        Assert.Equal(0, decision.DominantChannel);
        Assert.True(decision.AppetitiveDrive > decision.DefensiveDrive);
        Assert.Equal(1.0, decision.CircuitCoverage, 6);
    }

    [Fact]
    public void ThreatAndDefensePopulationsProduceDefensiveValuation()
    {
        var decision = NeuronalAffectValuationDecoder.Decode(DefensiveCircuit());

        Assert.True(decision.Active);
        Assert.Equal(3, decision.DominantChannel);
        Assert.True(decision.DefensiveDrive > decision.AppetitiveDrive);
        Assert.True(decision.NegativeValence > decision.PositiveValence);
    }

    [Fact]
    public void IncompleteAffectCircuitCannotClaimAuthority()
    {
        var partial = DefensiveCircuit()
            .Where(snapshot => snapshot.SalienceAffectDiagnostics is not null)
            .ToArray();

        var decision = NeuronalAffectValuationDecoder.Decode(partial);

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.Equal(-1, decision.DominantChannel);
        Assert.Equal(0.25, decision.CircuitCoverage, 6);
    }

    [Fact]
    public void DiagnosticModeLabelsCannotChangeValuation()
    {
        var baseline = NeuronalAffectValuationDecoder.Decode(DefensiveCircuit("first"));
        var renamed = NeuronalAffectValuationDecoder.Decode(DefensiveCircuit("contradictory-label"));

        Assert.Equal(baseline.DominantChannel, renamed.DominantChannel);
        Assert.Equal(baseline.ChannelScores, renamed.ChannelScores);
        Assert.Equal(baseline.Confidence, renamed.Confidence, 10);
    }

    [Fact]
    public void MissingPopulationDiagnosticsProduceNoValuation()
    {
        var decision = NeuronalAffectValuationDecoder.Decode([]);

        Assert.False(decision.Available);
        Assert.False(decision.Active);
        Assert.Equal(-1, decision.DominantChannel);
    }

    private static IReadOnlyList<InstanceStructureSnapshot> AppetitiveCircuit(string mode = "appetitive")
        =>
        [
            Snapshot(
                StructureId.Insula,
                affect: new SalienceAffectDiagnostics(mode, 0f, 20f, 0f, 8f, 10f, 0f, 5f, 20f)),
            Snapshot(
                StructureId.Hypothalamus,
                homeostasis: new HypothalamicHomeostasisDiagnostics(mode, 12f, 20f, 16f, 15f, 8f, 6f, 2f, 0f)),
            Snapshot(
                StructureId.PeriaqueductalGray,
                defense: new DescendingDefenseDiagnostics(mode, 0f, 0f, 0f, 3f, 2f, 2f, 0f, 0f)),
            Snapshot(
                StructureId.Vta,
                reward: new DopamineRewardDiagnostics(mode, 25f, 12f, 25f, 20f, 0f, 20f, 8f, 0f, 15f))
        ];

    private static IReadOnlyList<InstanceStructureSnapshot> DefensiveCircuit(string mode = "defensive")
        =>
        [
            Snapshot(
                StructureId.Amygdala,
                affect: new SalienceAffectDiagnostics(mode, 25f, 2f, 5f, 20f, 2f, 25f, 0f, 25f)),
            Snapshot(
                StructureId.Hypothalamus,
                homeostasis: new HypothalamicHomeostasisDiagnostics(mode, 5f, 8f, 5f, 20f, 18f, 20f, 12f, 25f)),
            Snapshot(
                StructureId.PeriaqueductalGray,
                defense: new DescendingDefenseDiagnostics(mode, 25f, 20f, 25f, 2f, 12f, 20f, 20f, 25f)),
            Snapshot(
                StructureId.Habenula,
                reward: new DopamineRewardDiagnostics(mode, 0f, 2f, 0f, 2f, 25f, 3f, 2f, 0f, 5f))
        ];

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        SalienceAffectDiagnostics? affect = null,
        HypothalamicHomeostasisDiagnostics? homeostasis = null,
        DescendingDefenseDiagnostics? defense = null,
        DopamineRewardDiagnostics? reward = null)
        => new(
            new ServiceInstance(structure, $"{structure}-M", "M", new Uri("http://localhost")),
            structure,
            ActiveNeuronCount: 32,
            MeanFiringRateHz: 8f,
            DominantRhythm: BrainRhythm.BETA,
            TopActiveNeurons: [],
            NeuromodLocal: new NeuromodState(),
            SpikeInCount: 0,
            SpikeOutCount: 0,
            FeedbackQueueDepth: 0,
            SalienceAffectDiagnostics: affect,
            HypothalamicHomeostasisDiagnostics: homeostasis,
            DescendingDefenseDiagnostics: defense,
            DopamineRewardDiagnostics: reward);
}
