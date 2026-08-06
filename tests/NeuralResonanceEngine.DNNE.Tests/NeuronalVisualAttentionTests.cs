using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalVisualAttentionTests
{
    [Fact]
    public void Decoder_Selects_Contralateral_Field_From_Complete_Bilateral_Circuit()
    {
        var decision = NeuronalVisualAttentionDecoder.Decode(CompleteCircuit(
            leftExcitatoryHz: 8f,
            rightExcitatoryHz: 38f,
            leftTrnHz: 12f,
            rightTrnHz: 2f));

        Assert.True(decision.Available);
        Assert.True(decision.Active);
        Assert.Equal("left", decision.FocusedField);
        Assert.Equal("R", decision.FocusedHemisphere);
        Assert.Equal(1.0, decision.CircuitCoverage, 6);
        Assert.True(decision.LeftFieldDrive > decision.RightFieldDrive);
        Assert.True(decision.FocusConfidence > 0.0);
    }

    [Fact]
    public void Decoder_Fails_Closed_When_A_Required_Hemisphere_Is_Missing()
    {
        var snapshots = CompleteCircuit(8f, 38f, 12f, 2f)
            .Where(snapshot =>
                snapshot.StructureId != StructureId.Pfc ||
                snapshot.Instance.HemisphereNormalized != "L")
            .ToArray();

        var decision = NeuronalVisualAttentionDecoder.Decode(snapshots);

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.Equal("neutral", decision.FocusedField);
        Assert.Equal("M", decision.FocusedHemisphere);
        Assert.True(decision.CircuitCoverage < 1.0);
        Assert.Equal(0.0, decision.FocusConfidence);
    }

    [Fact]
    public void Decoder_Fails_Closed_On_A_Bilateral_Tie()
    {
        var decision = NeuronalVisualAttentionDecoder.Decode(CompleteCircuit(
            leftExcitatoryHz: 24f,
            rightExcitatoryHz: 24f,
            leftTrnHz: 5f,
            rightTrnHz: 5f));

        Assert.True(decision.Available);
        Assert.False(decision.Active);
        Assert.Equal("neutral", decision.FocusedField);
        Assert.Equal(0.0, decision.FocusConfidence);
    }

    [Fact]
    public void Runtime_Observes_Persistence_But_Does_Not_Hold_A_Previous_Winner()
    {
        var runtime = new NeuronalVisualAttentionRuntime();
        var leftField = CompleteCircuit(8f, 38f, 12f, 2f);
        var rightField = CompleteCircuit(38f, 8f, 2f, 12f);

        var first = runtime.Update(10, leftField);
        var repeated = runtime.Update(11, leftField);
        var switched = runtime.Update(12, rightField);

        Assert.Equal(1, first.SustainedSelectionTicks);
        Assert.Equal(2, repeated.SustainedSelectionTicks);
        Assert.Equal(10, repeated.LastSelectionTick);
        Assert.Equal("right", switched.FocusedField);
        Assert.Equal("L", switched.FocusedHemisphere);
        Assert.Equal(1, switched.SustainedSelectionTicks);
        Assert.Equal(12, switched.LastSelectionTick);
    }

    [Fact]
    public void Sensory_Gain_Encodes_Visual_Fields_Contralaterally()
    {
        var leftHemisphere = NeuronalVisualAttentionDecoder.GetContralateralSensoryGain(
            "L",
            leftFieldSaliency: 0.9f,
            rightFieldSaliency: 0.2f);
        var rightHemisphere = NeuronalVisualAttentionDecoder.GetContralateralSensoryGain(
            "R",
            leftFieldSaliency: 0.9f,
            rightFieldSaliency: 0.2f);

        Assert.True(rightHemisphere > leftHemisphere);
        Assert.Equal(1.0, NeuronalVisualAttentionDecoder.GetContralateralSensoryGain("L", null, null), 6);
    }

    [Fact]
    public void Legacy_Visual_Winner_And_Checkpoint_Authority_Are_Absent()
    {
        Assert.Null(typeof(SimulationState).GetMethod("RegisterVisualAttentionObservation"));
        Assert.Null(typeof(SimulationState).GetMethod("AdvanceVisualAttentionWta"));
        Assert.Null(typeof(NetworkStateDocument).GetProperty("VisualAttention"));

        var snapshot = new NeuronalVisualAttentionRuntime().GetSnapshot();
        Assert.True(snapshot.ReadOnlyMonitor);
        Assert.False(snapshot.CanAcceptAttentionOverrides);
        Assert.False(snapshot.LegacyWinnerEnabled);
    }

    private static IReadOnlyList<InstanceStructureSnapshot> CompleteCircuit(
        float leftExcitatoryHz,
        float rightExcitatoryHz,
        float leftTrnHz,
        float rightTrnHz)
    {
        var snapshots = new List<InstanceStructureSnapshot>(8);
        foreach (var structure in new[] { StructureId.Pfc, StructureId.Ppc, StructureId.Pulvinar })
        {
            snapshots.Add(Snapshot(structure, "L", leftExcitatoryHz));
            snapshots.Add(Snapshot(structure, "R", rightExcitatoryHz));
        }

        snapshots.Add(Snapshot(StructureId.Trn, "L", leftTrnHz));
        snapshots.Add(Snapshot(StructureId.Trn, "R", rightTrnHz));
        return snapshots;
    }

    private static InstanceStructureSnapshot Snapshot(
        StructureId structure,
        string hemisphere,
        float firingRateHz)
        => new(
            new ServiceInstance(
                structure,
                $"{structure}-{hemisphere}",
                hemisphere,
                new Uri("http://localhost")),
            structure,
            ActiveNeuronCount: 32,
            MeanFiringRateHz: firingRateHz,
            DominantRhythm: BrainRhythm.GAMMA,
            TopActiveNeurons: [],
            NeuromodLocal: new NeuromodState(),
            SpikeInCount: 0,
            SpikeOutCount: 0,
            FeedbackQueueDepth: 0);
}
