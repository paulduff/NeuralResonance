using System.Numerics;

namespace NRE.WorldSim;

public enum AvatarTerrainAscentMode
{
    None,
    Step,
    Mantle
}

public readonly record struct AvatarTerrainAscentReadiness(
    double ForwardEffort,
    double LegEffort,
    double ArmEffort,
    double ManipulatorEffort,
    double UprightFraction,
    double SupportFraction,
    bool LeftHandSupported,
    bool RightHandSupported)
{
    public bool HasHandSupport => LeftHandSupported || RightHandSupported;
}

public readonly record struct AvatarTerrainAscentProposal(
    bool Active,
    Vector3 RootPosition,
    double Progress,
    AvatarTerrainAscentMode Mode);

/// <summary>
/// Mechanical terrain negotiation for the articulated body. This is not a
/// navigation controller: it cannot choose a destination or initiate motion.
/// It only converts ongoing neuronal muscle effort and measured contact into a
/// bounded candidate pose which the collision scene must still accept.
/// </summary>
public sealed class AvatarTerrainAscentController
{
    public const double MaximumStepHeightMeters = 0.30;
    public const double MaximumMantleHeightMeters = 1.05;
    private const double FootClearanceMeters = 0.03;
    private const double MaximumUnsupportedSeconds = 0.40;
    private const double MaximumStalledSeconds = 0.32;
    private const double CandidateAcceptanceMeters = 0.035;

    private Vector3 startRoot;
    private Vector3 targetRoot;
    private double durationSeconds;
    private double progress;
    private double unsupportedSeconds;
    private double stalledSeconds;

    public AvatarTerrainAscentMode Mode { get; private set; }
    public bool IsActive => Mode != AvatarTerrainAscentMode.None;
    public double Progress => progress;
    public string ModeName => Mode.ToString().ToLowerInvariant();
    public long EncounterCount { get; private set; }
    public long StartedCount { get; private set; }
    public long StepStartedCount { get; private set; }
    public long MantleStartedCount { get; private set; }
    public long CompletedCount { get; private set; }
    public long StepCompletedCount { get; private set; }
    public long MantleCompletedCount { get; private set; }
    public long AbortedCount { get; private set; }
    public long RejectedCount { get; private set; }
    public string LastOutcome { get; private set; } = "none";

    public bool TryBegin(
        Vector3 currentRoot,
        WorldTerrainRise rise,
        AvatarTerrainAscentReadiness readiness)
    {
        if (IsActive)
        {
            return false;
        }

        EncounterCount++;
        if (!IsFinite(currentRoot) ||
            !double.IsFinite(rise.RiseMeters) || rise.RiseMeters <= 0.015 ||
            rise.RiseMeters > MaximumMantleHeightMeters)
        {
            Reject($"rejected invalid rise {rise.RiseMeters:0.000} m");
            return false;
        }

        var mode = rise.RiseMeters <= MaximumStepHeightMeters
            ? AvatarTerrainAscentMode.Step
            : AvatarTerrainAscentMode.Mantle;
        if (!CanInitiate(mode, readiness))
        {
            Reject($"{ModeText(mode)} rejected: insufficient measured support or neuronal effort");
            return false;
        }

        var target = new Vector3(
            (float)rise.TargetX,
            (float)(rise.TargetSurfaceY + FootClearanceMeters),
            (float)rise.TargetZ);
        if (!IsFinite(target) || target.Y <= currentRoot.Y)
        {
            Reject($"{ModeText(mode)} rejected: target was not above the current support plane");
            return false;
        }

        Mode = mode;
        startRoot = currentRoot;
        targetRoot = target;
        progress = 0.0;
        unsupportedSeconds = 0.0;
        stalledSeconds = 0.0;
        var power = mode == AvatarTerrainAscentMode.Step
            ? Math.Clamp(readiness.LegEffort, 0.25, 1.0)
            : Math.Clamp(
                (readiness.ArmEffort * 0.52) +
                (readiness.LegEffort * 0.28) +
                (readiness.ManipulatorEffort * 0.20),
                0.28,
                1.0);
        durationSeconds = (mode == AvatarTerrainAscentMode.Step ? 0.58 : 1.85) / power;
        StartedCount++;
        if (mode == AvatarTerrainAscentMode.Step)
        {
            StepStartedCount++;
        }
        else
        {
            MantleStartedCount++;
        }
        LastOutcome = $"{ModeText(mode)} started for {rise.RiseMeters:0.000} m rise";
        return true;
    }

    public AvatarTerrainAscentProposal Propose(
        double deltaSeconds,
        AvatarTerrainAscentReadiness readiness)
    {
        if (!IsActive || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            return new AvatarTerrainAscentProposal(false, startRoot, progress, Mode);
        }

        var dt = Math.Clamp(deltaSeconds, 0.001, 0.10);
        if (!CanSustain(Mode, readiness, progress))
        {
            unsupportedSeconds += dt;
            if (unsupportedSeconds >= MaximumUnsupportedSeconds)
            {
                Abort("aborted: measured neuronal effort or support was withdrawn");
                return new AvatarTerrainAscentProposal(false, startRoot, 0.0, AvatarTerrainAscentMode.None);
            }

            return new AvatarTerrainAscentProposal(true, PositionAt(progress), progress, Mode);
        }

        unsupportedSeconds = Math.Max(0.0, unsupportedSeconds - (dt * 2.0));
        var proposedProgress = Math.Min(1.0, progress + (dt / durationSeconds));
        return new AvatarTerrainAscentProposal(
            true,
            PositionAt(proposedProgress),
            proposedProgress,
            Mode);
    }

    public bool Commit(AvatarTerrainAscentProposal proposal, Vector3 resolvedRoot, double deltaSeconds)
    {
        if (!IsActive || !proposal.Active || proposal.Mode != Mode || !IsFinite(resolvedRoot))
        {
            return false;
        }

        var intendedMotion = proposal.RootPosition - PositionAt(progress);
        var resolvedError = Vector3.Distance(resolvedRoot, proposal.RootPosition);
        var acceptedFraction = resolvedError <= CandidateAcceptanceMeters ? 1f : 0f;
        if (acceptedFraction <= 0f && intendedMotion.LengthSquared() > 0.000001f)
        {
            var achieved = resolvedRoot - PositionAt(progress);
            acceptedFraction = Math.Clamp(
                Vector3.Dot(achieved, intendedMotion) / intendedMotion.LengthSquared(),
                0f,
                1f);
        }

        if (acceptedFraction < 0.10f)
        {
            stalledSeconds += Math.Clamp(deltaSeconds, 0.001, 0.10);
            if (stalledSeconds >= MaximumStalledSeconds)
            {
                Abort("aborted: collision scene rejected the ascent trajectory");
            }
            return false;
        }

        stalledSeconds = 0.0;
        progress += (proposal.Progress - progress) * acceptedFraction;
        if (progress < 0.9999 || Vector3.Distance(resolvedRoot, targetRoot) > 0.06f)
        {
            return true;
        }

        var completedMode = Mode;
        CompletedCount++;
        if (completedMode == AvatarTerrainAscentMode.Step)
        {
            StepCompletedCount++;
        }
        else
        {
            MantleCompletedCount++;
        }
        LastOutcome = $"{ModeText(completedMode)} completed";
        ResetActive();
        return true;
    }

    public void Reset()
    {
        ResetActive();
        EncounterCount = 0;
        StartedCount = 0;
        StepStartedCount = 0;
        MantleStartedCount = 0;
        CompletedCount = 0;
        StepCompletedCount = 0;
        MantleCompletedCount = 0;
        AbortedCount = 0;
        RejectedCount = 0;
        LastOutcome = "none";
    }

    public void CancelActive(string reason)
    {
        if (!IsActive)
        {
            return;
        }

        Abort(string.IsNullOrWhiteSpace(reason) ? "aborted" : $"aborted: {reason.Trim()}");
    }

    private void Abort(string outcome)
    {
        AbortedCount++;
        LastOutcome = $"{ModeText(Mode)} {outcome}";
        ResetActive();
    }

    private void Reject(string outcome)
    {
        RejectedCount++;
        LastOutcome = outcome;
    }

    private void ResetActive()
    {
        Mode = AvatarTerrainAscentMode.None;
        startRoot = default;
        targetRoot = default;
        durationSeconds = 0.0;
        progress = 0.0;
        unsupportedSeconds = 0.0;
        stalledSeconds = 0.0;
    }

    private static string ModeText(AvatarTerrainAscentMode mode)
        => mode.ToString().ToLowerInvariant();

    private Vector3 PositionAt(double value)
    {
        var t = Math.Clamp(value, 0.0, 1.0);
        if (Mode == AvatarTerrainAscentMode.Step)
        {
            var vertical = SmoothStep(Math.Clamp(t / 0.45, 0.0, 1.0));
            var horizontal = SmoothStep(Math.Clamp((t - 0.30) / 0.70, 0.0, 1.0));
            var result = Vector3.Lerp(startRoot, targetRoot, (float)horizontal);
            result.Y = (float)Lerp(startRoot.Y, targetRoot.Y, vertical);
            result.Y += (float)(Math.Sin(Math.PI * t) * 0.035);
            return result;
        }

        var lift = SmoothStep(Math.Clamp(t / 0.62, 0.0, 1.0));
        var traverse = SmoothStep(Math.Clamp((t - 0.42) / 0.58, 0.0, 1.0));
        var mantle = Vector3.Lerp(startRoot, targetRoot, (float)traverse);
        mantle.Y = (float)Lerp(startRoot.Y, targetRoot.Y, lift);
        mantle.Y += (float)(Math.Sin(Math.PI * t) * 0.055);
        return mantle;
    }

    private static bool CanInitiate(
        AvatarTerrainAscentMode mode,
        AvatarTerrainAscentReadiness readiness)
        => mode switch
        {
            AvatarTerrainAscentMode.Step =>
                readiness.ForwardEffort >= 0.10 &&
                readiness.LegEffort >= 0.16 &&
                readiness.UprightFraction >= 0.58 &&
                readiness.SupportFraction >= 0.35,
            AvatarTerrainAscentMode.Mantle =>
                readiness.ForwardEffort >= 0.08 &&
                readiness.LegEffort >= 0.10 &&
                readiness.ArmEffort >= 0.10 &&
                readiness.ManipulatorEffort >= 0.24 &&
                readiness.UprightFraction >= 0.40 &&
                readiness.HasHandSupport,
            _ => false
        };

    private static bool CanSustain(
        AvatarTerrainAscentMode mode,
        AvatarTerrainAscentReadiness readiness,
        double progress)
        => mode switch
        {
            AvatarTerrainAscentMode.Step =>
                readiness.ForwardEffort >= 0.045 && readiness.LegEffort >= 0.09,
            AvatarTerrainAscentMode.Mantle when progress < 0.62 =>
                readiness.ForwardEffort >= 0.035 &&
                readiness.ArmEffort >= 0.06 &&
                readiness.ManipulatorEffort >= 0.12,
            AvatarTerrainAscentMode.Mantle =>
                readiness.ForwardEffort >= 0.035 && readiness.LegEffort >= 0.07,
            _ => false
        };

    private static double SmoothStep(double value) => value * value * (3.0 - (2.0 * value));
    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);
    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
