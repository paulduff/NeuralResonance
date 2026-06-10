using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

/// <summary>
/// Behavioral guards for the biological-circuit fixes in the basal ganglia,
/// neuromodulator, and reward-prediction systems.
/// </summary>
public sealed class NeuromodulationAndSelectionTests
{
    // === NeuromodulatorSystem: dopamine dip (negative RPE must drop phasic below zero) ===

    [Fact]
    public void DopaminePhasic_GoesNegative_OnNegativeRpe_AndSurvivesAStep()
    {
        var nm = new NeuromodulatorSystem();
        nm.Reset();

        nm.TriggerDopaminePhasic(-0.5f);
        Assert.True(nm.DopaminePhasic < 0f, $"expected a phasic dip below zero, got {nm.DopaminePhasic}");

        // The near-zero clamp tests magnitude, so a dip must not be erased on the next step.
        nm.Step(dt: 0.05f, arousalDrive: 0f, stressDrive: 0f, rewardSignal: 0f, attentionDemand: 0f);
        Assert.True(nm.DopaminePhasic < 0f, $"dip was wiped by the near-zero clamp: {nm.DopaminePhasic}");
    }

    // === NeuromodulatorSystem: NE gain inverted-U is continuous (no jump at ne = 0.5) ===

    [Fact]
    public void NeGain_StaysBelowPeak_AcrossTheInvertedU()
    {
        // Sweep effective NE from ~0.2 to ~1.0 by injecting phasic NE (Beta = 1 after Reset),
        // and confirm the gain never exceeds the curve's 1.1 peak. The pre-fix discontinuity
        // jumped to ~1.3 just above ne = 0.5, which this guards against.
        float maxGain = 0f;
        bool sawCriticalRegion = false;

        for (int k = 0; k <= 40; k++)
        {
            var nm = new NeuromodulatorSystem();
            nm.Reset();

            float intensity = k * 0.05f; // 0..2
            nm.TriggerNEPhasic(intensity);

            float ne = (nm.NorepinephrineTonic + nm.NorepinephrinePhasic) * nm.BetaSensitivity;
            float gain = nm.GetGainMod();

            maxGain = MathF.Max(maxGain, gain);
            if (ne > 0.5f && ne < 0.7f)
                sawCriticalRegion = true;
        }

        Assert.True(sawCriticalRegion, "sweep never crossed the ne in (0.5, 0.7) region");
        Assert.True(maxGain <= 1.12f, $"NE gain exceeded the 1.1 peak (discontinuity regression): {maxGain}");
    }

    // === RewardPredictionSystem: TD value learning converges and RPE shrinks ===

    [Fact]
    public void TdLearning_GrowsStateValue_AndShrinksRpe_TowardConvergence()
    {
        var rps = new RewardPredictionSystem(learningRate: 0.2f);

        float firstRpe = 0f, lastRpe = 0f;
        for (int i = 0; i < 200; i++)
        {
            // Terminal transition from state 0 with a fixed reward keeps re-updating V(0).
            var o = rps.ProcessTransition(newState: 0, reward: 1f, action: -1, isTerminal: true);
            if (i == 0) firstRpe = o.RPE;
            lastRpe = o.RPE;
        }

        Assert.True(firstRpe > 0.9f, $"first RPE should be ~the full reward, got {firstRpe}");
        Assert.True(lastRpe < firstRpe, "RPE should shrink as the value is learned");
        Assert.True(lastRpe < 0.1f, $"RPE should converge toward zero, got {lastRpe}");
        Assert.True(rps.Snapshot().CurrentStateValue > 0.8f, "learned state value should approach the reward");
    }

    [Fact]
    public void NegativeReward_ProducesADopaminePause()
    {
        var rps = new RewardPredictionSystem();
        var o = rps.ProcessTransition(newState: 1, reward: -1f, action: -1, isTerminal: true);

        Assert.True(o.RPE < 0f, $"omitted/negative reward should yield negative RPE, got {o.RPE}");
        Assert.True(rps.GetPhasicSignal() < 0f, $"negative RPE should pause phasic dopamine, got {rps.GetPhasicSignal()}");
    }

    // === BasalGangliaCircuit: dopamine shifts the D1/D2 balance ===

    [Theory]
    [InlineData(0.9f, true)]   // high DA -> D1 (Go) dominates
    [InlineData(0.1f, false)]  // low DA  -> D2 (NoGo) dominates
    public void Dopamine_ShiftsD1D2Balance(float dopamine, bool d1ShouldDominate)
    {
        var bg = new BasalGangliaCircuit(numChannels: 8);
        var input = new float[8];
        input[0] = 1f;

        for (int i = 0; i < 50; i++)
            bg.Step(input, dopamine01: dopamine, urgency01: 0f, dt: 0.02f);

        var s = bg.Snapshot();
        if (d1ShouldDominate)
            Assert.True(s.D1Activation > s.D2Activation, $"D1 {s.D1Activation} should exceed D2 {s.D2Activation} at high DA");
        else
            Assert.True(s.D2Activation > s.D1Activation, $"D2 {s.D2Activation} should exceed D1 {s.D1Activation} at low DA");
    }

    [Fact]
    public void BasalGanglia_SelectsTheStronglyDrivenChannel_UnderHighDopamine()
    {
        var bg = new BasalGangliaCircuit(numChannels: 8);
        var input = new float[8];
        input[3] = 1f; // concentrate cortical drive on channel 3

        BasalGangliaOutput o = default;
        for (int i = 0; i < 100; i++)
            o = bg.Step(input, dopamine01: 0.9f, urgency01: 0.2f, dt: 0.02f);

        // Channel 3 should win the thalamic gating competition.
        int argmax = 0;
        for (int c = 1; c < o.ThalamicGating.Length; c++)
            if (o.ThalamicGating[c] > o.ThalamicGating[argmax]) argmax = c;

        Assert.Equal(3, argmax);
        Assert.Equal(3, o.SelectedChannel);
    }

    [Fact]
    public void BasalGanglia_StaysFiniteAndBounded_OverManySteps()
    {
        var bg = new BasalGangliaCircuit(numChannels: 8);
        var rng = new Random(1234);
        var input = new float[8];

        for (int i = 0; i < 300; i++)
        {
            for (int c = 0; c < input.Length; c++) input[c] = (float)rng.NextDouble();
            var o = bg.Step(input, dopamine01: (float)rng.NextDouble(), urgency01: (float)rng.NextDouble(), dt: 0.02f);

            foreach (float g in o.ThalamicGating)
            {
                Assert.False(float.IsNaN(g) || float.IsInfinity(g), "thalamic gating produced a non-finite value");
                Assert.InRange(g, 0f, 1f);
            }
            Assert.InRange(o.SelectedChannel, -1, 7);
        }
    }
}
