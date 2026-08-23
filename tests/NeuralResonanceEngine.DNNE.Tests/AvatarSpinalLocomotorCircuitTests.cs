using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarSpinalLocomotorCircuitTests
{
    [Fact]
    public void TonicDescendingRecruitmentProducesReciprocalPhaseHandoffsWithoutAClockWave()
    {
        var circuit = new AvatarSpinalLocomotorCircuit();
        var priorSide = 0;
        var observedLeft = false;
        var observedRight = false;
        var maximumLeftLead = 0.0;
        var maximumRightLead = 0.0;

        for (var tick = 0; tick < 1_200; tick++)
        {
            var state = circuit.Advance(
                1.0 / 60.0,
                descendingRecruitment: 0.72,
                leftPlantarLoadFraction: priorSide < 0 ? 0.0 : 0.55,
                rightPlantarLoadFraction: priorSide > 0 ? 0.0 : 0.55);
            priorSide = state.LeftFlexorRecruitment > state.RightFlexorRecruitment ? -1 : 1;
            observedLeft |= state.LeftFlexorRecruitment > state.RightFlexorRecruitment + 0.10;
            observedRight |= state.RightFlexorRecruitment > state.LeftFlexorRecruitment + 0.10;
            maximumLeftLead = Math.Max(
                maximumLeftLead,
                state.LeftFlexorRecruitment - state.RightFlexorRecruitment);
            maximumRightLead = Math.Max(
                maximumRightLead,
                state.RightFlexorRecruitment - state.LeftFlexorRecruitment);
        }

        Assert.True(observedLeft, $"Maximum left flexor lead was {maximumLeftLead:0.000}.");
        Assert.True(observedRight, $"Maximum right flexor lead was {maximumRightLead:0.000}.");
        Assert.True(
            circuit.State.PhaseHandoffs >= 4,
            $"Only {circuit.State.PhaseHandoffs} reciprocal phase handoffs occurred.");
        Assert.InRange(circuit.State.LeftSwingSeconds, 0.0, 1.9);
        Assert.InRange(circuit.State.RightSwingSeconds, 0.0, 1.9);
    }

    [Fact]
    public void RemovingDescendingRecruitmentSilencesTheFlexorPools()
    {
        var circuit = new AvatarSpinalLocomotorCircuit();
        for (var tick = 0; tick < 180; tick++)
        {
            circuit.Advance(1.0 / 60.0, 0.8, 0.5, 0.5);
        }
        for (var tick = 0; tick < 180; tick++)
        {
            circuit.Advance(1.0 / 60.0, 0.0, 0.5, 0.5);
        }

        Assert.InRange(circuit.State.LeftFlexorRecruitment, 0.0, 0.02);
        Assert.InRange(circuit.State.RightFlexorRecruitment, 0.0, 0.02);
    }

    [Fact]
    public void ContinuousSwingIsBoundedByNeuralAdaptationAndRefractoriness()
    {
        var circuit = new AvatarSpinalLocomotorCircuit();
        var maximumSwing = 0.0;

        for (var tick = 0; tick < 2_400; tick++)
        {
            var state = circuit.Advance(1.0 / 60.0, 1.0, 0.0, 0.0);
            maximumSwing = Math.Max(maximumSwing, Math.Max(state.LeftSwingSeconds, state.RightSwingSeconds));
        }

        Assert.InRange(maximumSwing, 0.05, 1.90);
        Assert.True(circuit.State.PhaseHandoffs >= 4);
    }
}
