namespace NRE.SimAvatar;

public readonly record struct AvatarSpinalLocomotorState(
    double LeftFlexorRecruitment,
    double LeftExtensorRecruitment,
    double RightFlexorRecruitment,
    double RightExtensorRecruitment,
    double LeftSwingSeconds,
    double RightSwingSeconds,
    long PhaseHandoffs)
{
    public double LeftCycle => LeftFlexorRecruitment - LeftExtensorRecruitment;
    public double RightCycle => RightFlexorRecruitment - RightExtensorRecruitment;
}

/// <summary>
/// Four-population spinal half-centre. Tonic descending excitation, reciprocal
/// GABA-like inhibition, activity adaptation, and plantar reafference produce
/// alternating flexor/extensor recruitment without a clock or scripted sine.
/// </summary>
public sealed class AvatarSpinalLocomotorCircuit
{
    private const double NeuralTimeConstantSeconds = 0.085;
    private const double AdaptationTimeConstantSeconds = 0.72;
    private const double MaximumUninterruptedSwingSeconds = 1.20;

    private double leftFlexor = 0.46;
    private double leftExtensor = 0.18;
    private double rightFlexor = 0.16;
    private double rightExtensor = 0.48;
    private double leftFlexorMembrane = 0.34;
    private double rightFlexorMembrane = -0.04;
    private double leftFlexorAdaptation;
    private double rightFlexorAdaptation;
    private double leftExtensorAdaptation;
    private double rightExtensorAdaptation;
    private double leftSwingSeconds;
    private double rightSwingSeconds;
    private double leftDominanceSeconds;
    private double rightDominanceSeconds;
    private int priorDominantSide = 1;
    private long phaseHandoffs;

    public AvatarSpinalLocomotorState State { get; private set; }

    public AvatarSpinalLocomotorState Advance(
        double deltaSeconds,
        double descendingRecruitment,
        double leftPlantarLoadFraction,
        double rightPlantarLoadFraction)
    {
        var dt = double.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0.001, 0.10) : 0.001;
        var descending = double.IsFinite(descendingRecruitment)
            ? Math.Clamp(descendingRecruitment, 0.0, 1.0)
            : 0.0;
        var leftContact = Math.Clamp(leftPlantarLoadFraction, 0.0, 1.0);
        var rightContact = Math.Clamp(rightPlantarLoadFraction, 0.0, 1.0);

        if (descending < 0.015)
        {
            leftFlexorMembrane = Approach(leftFlexorMembrane, 0.0, dt * 6.0);
            rightFlexorMembrane = Approach(rightFlexorMembrane, 0.0, dt * 6.0);
            leftFlexor = Approach(leftFlexor, 0.0, dt * 4.5);
            rightFlexor = Approach(rightFlexor, 0.0, dt * 4.5);
            leftExtensor = Approach(leftExtensor, leftContact * 0.12, dt * 4.5);
            rightExtensor = Approach(rightExtensor, rightContact * 0.12, dt * 4.5);
            DecayAdaptation(dt);
            leftSwingSeconds = 0.0;
            rightSwingSeconds = 0.0;
            leftDominanceSeconds = 0.0;
            rightDominanceSeconds = 0.0;
            return Capture();
        }

        var leftSwingDominant = leftFlexor > leftExtensor + 0.04;
        var rightSwingDominant = rightFlexor > rightExtensor + 0.04;
        leftSwingSeconds = leftSwingDominant ? leftSwingSeconds + dt : 0.0;
        rightSwingSeconds = rightSwingDominant ? rightSwingSeconds + dt : 0.0;
        leftDominanceSeconds = leftFlexor > rightFlexor + 0.02
            ? leftDominanceSeconds + dt
            : 0.0;
        rightDominanceSeconds = rightFlexor > leftFlexor + 0.02
            ? rightDominanceSeconds + dt
            : 0.0;
        var leftRefractory = Math.Clamp(
            (Math.Max(leftSwingSeconds, leftDominanceSeconds) - MaximumUninterruptedSwingSeconds) / 0.35,
            0.0,
            1.0);
        var rightRefractory = Math.Clamp(
            (Math.Max(rightSwingSeconds, rightDominanceSeconds) - MaximumUninterruptedSwingSeconds) / 0.35,
            0.0,
            1.0);

        var tonicFlexor = 0.42 + (descending * 1.12);
        var leftFlexorOutput = Math.Max(0.0, leftFlexorMembrane);
        var rightFlexorOutput = Math.Max(0.0, rightFlexorMembrane);
        var leftMembraneDerivative =
            (-leftFlexorMembrane - (2.15 * rightFlexorOutput) -
             (2.55 * leftFlexorAdaptation) + tonicFlexor -
             (leftContact * 0.48) - (leftRefractory * 2.8) +
             (rightRefractory * 0.85)) / 0.20;
        var rightMembraneDerivative =
            (-rightFlexorMembrane - (2.15 * leftFlexorOutput) -
             (2.55 * rightFlexorAdaptation) + tonicFlexor -
             (rightContact * 0.48) - (rightRefractory * 2.8) +
             (leftRefractory * 0.85)) / 0.20;
        leftFlexorMembrane = Math.Clamp(
            leftFlexorMembrane + (leftMembraneDerivative * dt),
            -3.0,
            3.0);
        rightFlexorMembrane = Math.Clamp(
            rightFlexorMembrane + (rightMembraneDerivative * dt),
            -3.0,
            3.0);
        leftFlexor = Math.Clamp(Math.Max(0.0, leftFlexorMembrane), 0.0, 1.0);
        rightFlexor = Math.Clamp(Math.Max(0.0, rightFlexorMembrane), 0.0, 1.0);

        var tonicExtensor = 0.18 + (descending * 0.62);
        var leftExtensorInput = tonicExtensor +
            (leftExtensor * 0.82) - (leftFlexor * 1.32) -
            (rightExtensor * 0.42) - (leftExtensorAdaptation * 1.30) +
            (leftContact * 1.28) + (rightFlexor * 0.20);
        var rightExtensorInput = tonicExtensor +
            (rightExtensor * 0.82) - (rightFlexor * 1.32) -
            (leftExtensor * 0.42) - (rightExtensorAdaptation * 1.30) +
            (rightContact * 1.28) + (leftFlexor * 0.20);

        leftExtensor = IntegratePopulation(leftExtensor, leftExtensorInput, dt);
        rightExtensor = IntegratePopulation(rightExtensor, rightExtensorInput, dt);

        leftFlexorAdaptation = IntegrateAdaptation(leftFlexorAdaptation, leftFlexor, dt);
        rightFlexorAdaptation = IntegrateAdaptation(rightFlexorAdaptation, rightFlexor, dt);
        leftExtensorAdaptation = IntegrateAdaptation(leftExtensorAdaptation, leftExtensor, dt);
        rightExtensorAdaptation = IntegrateAdaptation(rightExtensorAdaptation, rightExtensor, dt);

        var dominantSide = leftFlexor > rightFlexor + 0.05
            ? -1
            : rightFlexor > leftFlexor + 0.05
                ? 1
                : priorDominantSide;
        if (dominantSide != priorDominantSide)
        {
            phaseHandoffs++;
            priorDominantSide = dominantSide;
        }

        return Capture();
    }

    public void Reset()
    {
        leftFlexor = 0.46;
        leftExtensor = 0.18;
        rightFlexor = 0.16;
        rightExtensor = 0.48;
        leftFlexorMembrane = 0.34;
        rightFlexorMembrane = -0.04;
        leftFlexorAdaptation = 0.0;
        rightFlexorAdaptation = 0.0;
        leftExtensorAdaptation = 0.0;
        rightExtensorAdaptation = 0.0;
        leftSwingSeconds = 0.0;
        rightSwingSeconds = 0.0;
        leftDominanceSeconds = 0.0;
        rightDominanceSeconds = 0.0;
        priorDominantSide = 1;
        phaseHandoffs = 0;
        State = default;
    }

    private AvatarSpinalLocomotorState Capture()
    {
        State = new AvatarSpinalLocomotorState(
            NormalizeRecruitment(leftFlexor),
            NormalizeRecruitment(leftExtensor),
            NormalizeRecruitment(rightFlexor),
            NormalizeRecruitment(rightExtensor),
            leftSwingSeconds,
            rightSwingSeconds,
            phaseHandoffs);
        return State;
    }

    private void DecayAdaptation(double dt)
    {
        leftFlexorAdaptation = Approach(leftFlexorAdaptation, 0.0, dt * 1.6);
        rightFlexorAdaptation = Approach(rightFlexorAdaptation, 0.0, dt * 1.6);
        leftExtensorAdaptation = Approach(leftExtensorAdaptation, 0.0, dt * 1.6);
        rightExtensorAdaptation = Approach(rightExtensorAdaptation, 0.0, dt * 1.6);
    }

    private static double IntegratePopulation(double current, double input, double dt)
    {
        var target = Activation(input);
        var alpha = 1.0 - Math.Exp(-dt / NeuralTimeConstantSeconds);
        return Math.Clamp(current + ((target - current) * alpha), 0.0, 1.0);
    }

    private static double IntegrateAdaptation(double current, double activity, double dt)
    {
        var alpha = 1.0 - Math.Exp(-dt / AdaptationTimeConstantSeconds);
        return Math.Clamp(current + ((activity - current) * alpha), 0.0, 1.0);
    }

    private static double Activation(double input) =>
        1.0 / (1.0 + Math.Exp(-4.6 * (input - 0.52)));

    private static double NormalizeRecruitment(double activity) =>
        Math.Clamp((activity - 0.08) / 0.82, 0.0, 1.0);

    private static double Approach(double current, double target, double maximumDelta)
    {
        var delta = target - current;
        return Math.Abs(delta) <= maximumDelta
            ? target
            : current + (Math.Sign(delta) * maximumDelta);
    }
}
