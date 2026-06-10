using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

/// <summary>
/// Behavioral guards for the hippocampal CA3 recurrent-completion wiring and the
/// amygdala ITC gating path.
/// </summary>
public sealed class HippocampusAmygdalaTests
{
    // === Hippocampus: CA3 recurrent associations drive pattern completion ===

    [Fact]
    public void Ca3_LearnedAssociations_CompletePatternFromPartialCue()
    {
        // A trained hippocampus, cued with part of a learned pattern, should show
        // higher CA3 coherence (recurrent activation of associated units) than a
        // fresh one given the same cue. Pre-fix, the associations were never read,
        // so completion would not occur and coherence would stay ~1.0.
        float learnedCoherence = RunPartialCue(trainSteps: 300);
        float freshCoherence = RunPartialCue(trainSteps: 0);

        Assert.True(freshCoherence < 1.1f, $"untrained coherence should be ~1.0, got {freshCoherence}");
        Assert.True(learnedCoherence > 1.3f, $"trained recurrent completion should lift coherence, got {learnedCoherence}");
        Assert.True(learnedCoherence > freshCoherence * 1.2f, "training should measurably raise CA3 coherence");
    }

    private static float RunPartialCue(int trainSteps)
    {
        var hippo = new Hippocampus();

        // Full pattern: 8 "core" indices with distinct (descending) activity so the
        // DG winner-take-all is deterministic, plus filler so the DG sparse target
        // resolves to the 8 core units.
        var full = BuildFullPattern();
        for (int i = 0; i < trainSteps; i++)
            hippo.Step(stepIndex: i, full, dt: 0.02f);

        // Partial cue: only the top 3 core indices (plus filler), so DG emits those 3.
        var partial = BuildPartialCue();
        var o = hippo.Step(stepIndex: trainSteps + 1, partial, dt: 0.02f);
        return o.CA3Coherence;
    }

    private static List<(byte hemi, int idx)> BuildFullPattern()
    {
        var spikes = new List<(byte, int)>();
        // core 1000..1007 with counts 9..2 (distinct activities -> deterministic DG order)
        for (int k = 0; k < 8; k++)
            for (int rep = 0; rep < 9 - k; rep++)
                spikes.Add(((byte)0, 1000 + k));
        // 80 filler singletons -> 88 distinct -> DG target ~8 (the core units win)
        for (int f = 0; f < 80; f++)
            spikes.Add(((byte)0, 2000 + f));
        return spikes;
    }

    private static List<(byte hemi, int idx)> BuildPartialCue()
    {
        var spikes = new List<(byte, int)>();
        // top 3 core indices with counts 5,4,3
        spikes.Add(((byte)0, 1000)); spikes.Add(((byte)0, 1000)); spikes.Add(((byte)0, 1000)); spikes.Add(((byte)0, 1000)); spikes.Add(((byte)0, 1000));
        spikes.Add(((byte)0, 1001)); spikes.Add(((byte)0, 1001)); spikes.Add(((byte)0, 1001)); spikes.Add(((byte)0, 1001));
        spikes.Add(((byte)0, 1002)); spikes.Add(((byte)0, 1002)); spikes.Add(((byte)0, 1002));
        // 27 filler singletons -> 30 distinct -> DG target 3 (the core units win)
        for (int f = 0; f < 27; f++)
            spikes.Add(((byte)0, 3000 + f));
        return spikes;
    }

    // === Amygdala: PFC inhibition gates CeA output (extinction) ===

    [Fact]
    public void PfcInhibition_SuppressesCeaOutput()
    {
        var spikes = new List<(byte hemi, int idx, byte region)>();
        for (int i = 0; i < 50; i++)
            spikes.Add(((byte)0, i, (byte)5));

        float ceaNoPfc = DriveAmygdala(spikes, pfcInhibition: 0f, out bool detected);
        float ceaWithPfc = DriveAmygdala(spikes, pfcInhibition: 1f, out _);

        Assert.True(detected, "a salient region should be detected");
        Assert.True(ceaNoPfc > 0f, "salient input should drive CeA output");
        Assert.True(ceaWithPfc < ceaNoPfc, $"PFC extinction should suppress CeA: noPfc={ceaNoPfc}, withPfc={ceaWithPfc}");
    }

    private static float DriveAmygdala(List<(byte hemi, int idx, byte region)> spikes, float pfcInhibition, out bool detected)
    {
        var amy = new Amygdala();
        amy.SetRegionSalience(regionId: 5, salience: 0.9f);

        AmygdalaOutput o = default;
        for (int i = 0; i < 150; i++)
            o = amy.Step(dt: 0.02f, stepIndex: i, spikes, hippocampalContext: 0f, pfcInhibition: pfcInhibition);

        detected = o.SalientEventDetected;
        return o.CeAOutput;
    }
}
