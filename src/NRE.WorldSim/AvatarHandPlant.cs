namespace NRE.WorldSim;

public enum AvatarHandPhase
{
    Open,
    Closing,
    Contact,
    Grasping,
    Holding,
    Opening,
    Slipping
}

public readonly record struct AvatarHandPlantInput(
    double SignedGraspDrive,
    bool TargetContact,
    bool HoldingObject,
    double RequiredGripForceNewtons);

public readonly record struct AvatarHandPlantOutput(
    AvatarHandPhase Phase,
    double PhaseDurationSeconds,
    double ApertureFraction,
    double GripForceNewtons,
    double FatigueFraction,
    double SlipFraction,
    bool GraspAcquired,
    bool Released,
    bool FatigueRelease);

/// <summary>
/// Antagonistic physical hand plant. Neural close/open drive changes aperture;
/// contact geometry and available grip determine whether an object can be held.
/// The plant contains no target selection or behavioural policy.
/// </summary>
public sealed class AvatarHandPlant
{
    private const double CloseDeadband = 0.08;
    private const double OpenDeadband = 0.08;
    private const double CloseRatePerSecond = 2.4;
    private const double OpenRatePerSecond = 3.2;
    private const double RelaxationRatePerSecond = 0.22;
    private const double MaximumGripForceNewtons = 92.0;
    private const double GraspApertureThreshold = 0.42;
    private const double ReleaseApertureThreshold = 0.68;
    private const double FatigueReleaseThreshold = 0.94;
    private const double BaseFatigueRatePerSecond = 0.055;
    private const double LoadedFatigueRatePerSecond = 0.22;
    private const double RecoveryRatePerSecond = 0.085;

    private AvatarHandPhase phase = AvatarHandPhase.Open;
    private double phaseDurationSeconds;
    private double apertureFraction = 1.0;
    private double fatigueFraction;

    public AvatarHandPlantOutput State { get; private set; } = new(
        AvatarHandPhase.Open,
        0.0,
        1.0,
        0.0,
        0.0,
        0.0,
        false,
        false,
        false);

    public AvatarHandPlantOutput Advance(double deltaSeconds, AvatarHandPlantInput input)
    {
        var dt = double.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0.001, 0.25) : 0.001;
        var signedDrive = double.IsFinite(input.SignedGraspDrive)
            ? Math.Clamp(input.SignedGraspDrive, -1.0, 1.0)
            : 0.0;
        var closeDrive = Math.Max(0.0, signedDrive);
        var openDrive = Math.Max(0.0, -signedDrive);
        var requiredGrip = double.IsFinite(input.RequiredGripForceNewtons)
            ? Math.Clamp(input.RequiredGripForceNewtons, 0.0, MaximumGripForceNewtons)
            : 0.0;

        var effectiveCloseDrive = closeDrive * (1.0 - fatigueFraction);
        if (closeDrive >= CloseDeadband)
        {
            apertureFraction -= CloseRatePerSecond * effectiveCloseDrive * dt;
        }
        else if (openDrive >= OpenDeadband)
        {
            apertureFraction += OpenRatePerSecond * openDrive * dt;
        }
        else
        {
            // Zero is the relaxed state: without continued neuronal recruitment,
            // the antagonistic extensor plant gradually opens the hand.
            apertureFraction += RelaxationRatePerSecond * dt;
        }
        apertureFraction = Math.Clamp(apertureFraction, 0.0, 1.0);

        var loadFraction = input.HoldingObject
            ? Math.Clamp(requiredGrip / MaximumGripForceNewtons, 0.0, 1.0)
            : 0.0;
        if (closeDrive >= CloseDeadband)
        {
            fatigueFraction += closeDrive *
                (BaseFatigueRatePerSecond + (loadFraction * LoadedFatigueRatePerSecond)) * dt;
        }
        else
        {
            fatigueFraction -= RecoveryRatePerSecond * (0.35 + openDrive) * dt;
        }
        fatigueFraction = Math.Clamp(fatigueFraction, 0.0, 1.0);

        var closureFraction = 1.0 - apertureFraction;
        var gripForce = closeDrive >= CloseDeadband
            ? MaximumGripForceNewtons * effectiveCloseDrive * closureFraction
            : 0.0;
        var gripDeficit = input.HoldingObject && requiredGrip > 0.001
            ? Math.Clamp((requiredGrip - gripForce) / requiredGrip, 0.0, 1.0)
            : 0.0;
        var fatigueRelease = input.HoldingObject && fatigueFraction >= FatigueReleaseThreshold;
        var released = input.HoldingObject &&
            (fatigueRelease || openDrive >= OpenDeadband ||
             apertureFraction >= ReleaseApertureThreshold || gripDeficit >= 0.98);
        var acquired = !input.HoldingObject && input.TargetContact &&
            apertureFraction <= GraspApertureThreshold &&
            gripForce >= Math.Max(4.0, requiredGrip);

        var nextPhase = released || gripDeficit > 0.10
            ? AvatarHandPhase.Slipping
            : input.HoldingObject
                ? AvatarHandPhase.Holding
                : acquired
                    ? AvatarHandPhase.Grasping
                    : input.TargetContact && closeDrive >= CloseDeadband
                        ? AvatarHandPhase.Contact
                        : closeDrive >= CloseDeadband
                            ? AvatarHandPhase.Closing
                            : openDrive >= OpenDeadband || apertureFraction < 0.98
                                ? AvatarHandPhase.Opening
                                : AvatarHandPhase.Open;

        phaseDurationSeconds = nextPhase == phase ? phaseDurationSeconds + dt : 0.0;
        phase = nextPhase;
        State = new AvatarHandPlantOutput(
            phase,
            phaseDurationSeconds,
            apertureFraction,
            gripForce,
            fatigueFraction,
            gripDeficit,
            acquired,
            released,
            fatigueRelease);
        return State;
    }

    public void Reset()
    {
        phase = AvatarHandPhase.Open;
        phaseDurationSeconds = 0.0;
        apertureFraction = 1.0;
        fatigueFraction = 0.0;
        State = new AvatarHandPlantOutput(
            AvatarHandPhase.Open,
            0.0,
            1.0,
            0.0,
            0.0,
            0.0,
            false,
            false,
            false);
    }
}
