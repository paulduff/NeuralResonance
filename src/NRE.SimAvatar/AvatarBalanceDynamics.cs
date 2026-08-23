using System.Numerics;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public enum AvatarBalancePhase
{
    Stable,
    Marginal,
    Unstable,
    Falling,
    Fallen,
    Righting,
    Airborne,
    BroadSupport,
    Dynamic
}

public readonly record struct AvatarExternalBodyContact(
    string Region,
    Vector3 BodyPosition,
    Vector3 BodyNormal,
    double ForceNewtons,
    double ImpulseNewtonSeconds,
    double ContactAreaSquareMillimeters);

public readonly record struct AvatarBalanceState(
    bool Initialized,
    Vector2 PreviousCenterOfMass,
    Vector2 CenterOfMassVelocity,
    double FallPitchRadians,
    double FallRollRadians,
    double FallPitchVelocityRadiansPerSecond,
    double FallRollVelocityRadiansPerSecond,
    double InstabilitySeconds,
    double RightingSeconds,
    AvatarBalancePhase Phase,
    double RecoveryStableSeconds = 0.0)
{
    public static AvatarBalanceState Neutral { get; } = new(
        false,
        Vector2.Zero,
        Vector2.Zero,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        AvatarBalancePhase.Stable);
}

public readonly record struct AvatarBalanceResult(
    AvatarBalanceState State,
    PhysicalBalanceStateFrame Frame,
    double BalanceError,
    double UprightFraction,
    double BodyHeightMeters,
    string PhysicalPosture);

/// <summary>
/// Computes whole-body balance from segment masses and measured support. The
/// model supplies mechanics and receptor measurements only; it never selects a
/// recovery action or moves a limb on the brain's behalf.
/// </summary>
public static class AvatarBalanceDynamics
{
    private const double GravityMetersPerSecondSquared = 9.80665;
    private const double MinimumComHeightMeters = 0.20;
    private const double InstabilityMarginMeters = -0.012;
    private const double MarginalMarginMeters = 0.035;
    private const double AcceptablePosturalTiltRadians = 0.075;
    private const double FallCommitSeconds = 0.20;
    private const double SevereDynamicFallMarginMeters = -0.09;
    private const double FallTiltEvidenceRadians = 0.18;
    private const double RightingCommitSeconds = 0.14;
    private const double RightingEvidenceHoldSeconds = 0.45;
    private const double RightingCompletionAngleRadians = 0.16;
    private const double RightingStableCommitSeconds = 0.18;
    private const double PassiveRecoveryCompletionAngleRadians = 0.10;
    private const double PassiveRecoveryMaximumAngularSpeedRadiansPerSecond = 0.12;
    private const double PassiveRecoveryMaximumComSpeedMetersPerSecond = 0.10;
    private const double PassiveRecoveryStableCommitSeconds = 0.35;
    private const double MinimumRightingDrive = 0.16;
    private const double MinimumRightingForceFraction = 0.10;
    private const double FallenAngleRadians = 1.30;
    private const double MaximumFallAngleRadians = 1.50;
    private const double PassiveAngularDampingPerSecond = 0.42;
    private const double StableAngularDampingPerSecond = 5.5;
    private const double MaximumComSpeedMetersPerSecond = 5.0;
    private const double MinimumSupportLoadNewtons = 0.5;
    private const double MaximumDynamicStabilityAllowanceMeters = 0.075;
    private const double MinimumUnobstructedFootClearanceMeters = 0.16;
    private const double RecumbentMaximumUprightFraction = 0.22;

    public static AvatarBalanceResult Advance(
        AvatarBalanceState current,
        PhysicalArticulationFrame articulation,
        IReadOnlyList<AvatarGroundContactProbe> groundContacts,
        IReadOnlyList<AvatarExternalBodyContact> externalContacts,
        bool grounded,
        string commandedPosture,
        double commandedBodyHeightMeters,
        double deltaSeconds,
        double rightingDrive = 0.0,
        double rightingForceFraction = 0.0,
        double locomotorEffort = 0.0,
        double commandedForwardSpeedMetersPerSecond = 0.0)
    {
        ArgumentNullException.ThrowIfNull(articulation);
        ArgumentNullException.ThrowIfNull(groundContacts);
        ArgumentNullException.ThrowIfNull(externalContacts);

        var dt = double.IsFinite(deltaSeconds)
            ? Math.Clamp(deltaSeconds, 0.001, 0.25)
            : 0.001;
        var colliders = AvatarColliderRig.CaptureResolved(articulation);
        var (centerOfMass, totalMass, pitchInertia, rollInertia) = ResolveMassProperties(colliders);
        var center2 = new Vector2(centerOfMass.X, centerOfMass.Z);
        var rawVelocity = current.Initialized
            ? (center2 - current.PreviousCenterOfMass) / (float)dt
            : Vector2.Zero;
        rawVelocity = ClampMagnitude(rawVelocity, MaximumComSpeedMetersPerSecond);
        var centerVelocity = current.Initialized
            ? Vector2.Lerp(current.CenterOfMassVelocity, rawVelocity, (float)Math.Clamp(dt * 12.0, 0.0, 1.0))
            : Vector2.Zero;

        var supportSamples = BuildSupportSamples(groundContacts, externalContacts);
        var supportPoints = supportSamples.SelectMany(CreatePatch).ToArray();
        var supportHull = ConvexHull(supportPoints);
        var supportArea = PolygonArea(supportHull);
        var centerOfPressure = ResolveCenterOfPressure(supportSamples);
        var comHeight = Math.Max(MinimumComHeightMeters, centerOfMass.Y);
        var naturalFrequency = Math.Sqrt(GravityMetersPerSecondSquared / comHeight);

        var pitchVelocity = Sanitize(current.FallPitchVelocityRadiansPerSecond);
        var rollVelocity = Sanitize(current.FallRollVelocityRadiansPerSecond);
        var pitch = Math.Clamp(Sanitize(current.FallPitchRadians), -MaximumFallAngleRadians, MaximumFallAngleRadians);
        var roll = Math.Clamp(Sanitize(current.FallRollRadians), -MaximumFallAngleRadians, MaximumFallAngleRadians);
        var pendularVelocity = new Vector2(
            centerVelocity.X - (float)(rollVelocity * comHeight),
            centerVelocity.Y - (float)(pitchVelocity * comHeight));
        var crossedLegConstraint = ResolveCrossedLegConstraint(colliders, supportSamples);
        // Root translation is not present in the body-local collider positions.
        // During an ordinary gait the swing leg can establish the next support
        // point, but crossed legs remove that capture step. In that configuration
        // the achieved root velocity must therefore remain in the extrapolated
        // centre of mass instead of being hidden by the local coordinate frame.
        pendularVelocity.Y += (float)(
            Sanitize(commandedForwardSpeedMetersPerSecond) *
            Math.Clamp(Sanitize(locomotorEffort), 0.0, 1.0) *
            crossedLegConstraint);
        var extrapolatedCenter = center2 + (pendularVelocity / (float)naturalFrequency);
        var staticMargin = SignedMargin(supportHull, center2);
        var dynamicMargin = SignedMargin(supportHull, extrapolatedCenter);

        var broadSupport = HasBroadBodySupport(groundContacts);
        var recumbentSupport = HasRecumbentSupport(externalContacts);
        var hasSupport = grounded && supportHull.Count >= 3 && supportArea > 0.0001;
        var dynamicStabilityAllowance = ResolveDynamicStabilityAllowance(
            locomotorEffort,
            commandedForwardSpeedMetersPerSecond,
            supportSamples,
            broadSupport,
            pitch,
            roll,
            crossedLegConstraint);
        var effectiveInstabilityMargin = InstabilityMarginMeters - dynamicStabilityAllowance;
        var tiltMagnitude = Math.Max(Math.Abs(pitch), Math.Abs(roll));
        var physicalFallEvidence = recumbentSupport ||
            !hasSupport ||
            staticMargin < SevereDynamicFallMarginMeters ||
            dynamicMargin < SevereDynamicFallMarginMeters ||
            (dynamicStabilityAllowance <= 0.0 &&
             staticMargin < InstabilityMarginMeters) ||
            (tiltMagnitude >= FallTiltEvidenceRadians &&
             dynamicMargin < effectiveInstabilityMargin);
        var instabilitySeconds = current.InstabilitySeconds;
        if (!physicalFallEvidence || (broadSupport && !recumbentSupport))
        {
            instabilitySeconds = Math.Max(0.0, instabilitySeconds - (dt * 4.0));
        }
        else
        {
            instabilitySeconds += dt;
        }

        var externalTorque = ResolveExternalTorque(
            externalContacts,
            centerOfMass,
            totalMass,
            pitchInertia,
            rollInertia);
        pitchVelocity += externalTorque.PitchAcceleration * dt;
        rollVelocity += externalTorque.RollAcceleration * dt;

        var recoveryDrive = Math.Clamp(Sanitize(rightingDrive), 0.0, 1.0);
        var recoveryForce = Math.Clamp(Sanitize(rightingForceFraction), 0.0, 1.0);
        var requestsRighting = commandedPosture is "standing" or "crouching" &&
                               recoveryDrive >= MinimumRightingDrive &&
                               recoveryForce >= MinimumRightingForceFraction;
        var continuesRighting = commandedPosture is "standing" or "crouching" &&
                                recoveryDrive >= MinimumRightingDrive * 0.5 &&
                                recoveryForce >= MinimumRightingForceFraction * 0.6;
        var rightingSeconds = current.RightingSeconds;
        var recoveryStableSeconds = Math.Max(0.0, current.RecoveryStableSeconds);
        var phase = current.Phase;
        // Neural righting can arrest momentum while the body's actual mass is
        // still over its base. It cannot hold the body upright after the mass
        // itself has passed beyond the measured support polygon.
        var rightingSupportAvailable = hasSupport &&
                                       staticMargin >= InstabilityMarginMeters;
        var rightingEvidencePresent = requestsRighting || continuesRighting;
        var retainedRightingEvidence = phase is AvatarBalancePhase.Righting &&
                                       rightingSeconds > 0.0;
        var controlledDescent = commandedPosture is "sitting" or "lying" &&
                                current.Phase is not AvatarBalancePhase.Falling and
                                    not AvatarBalancePhase.Fallen and
                                    not AvatarBalancePhase.Righting &&
                                hasSupport &&
                                externalContacts.Count == 0;
        var passiveRecoveryMeasured =
            (phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen or AvatarBalancePhase.Righting) &&
            !recumbentSupport &&
            hasSupport &&
            dynamicMargin >= MarginalMarginMeters &&
            Math.Max(Math.Abs(pitch), Math.Abs(roll)) <= PassiveRecoveryCompletionAngleRadians &&
            Math.Max(Math.Abs(pitchVelocity), Math.Abs(rollVelocity)) <=
                PassiveRecoveryMaximumAngularSpeedRadiansPerSecond &&
            centerVelocity.Length() <= PassiveRecoveryMaximumComSpeedMetersPerSecond;
        if (!grounded)
        {
            phase = AvatarBalancePhase.Airborne;
            rightingSeconds = 0.0;
            recoveryStableSeconds = 0.0;
            pitch += pitchVelocity * dt;
            roll += rollVelocity * dt;
        }
        else if (rightingSupportAvailable &&
                 phase is (AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen or AvatarBalancePhase.Righting) &&
                 (rightingEvidencePresent || retainedRightingEvidence))
        {
            rightingSeconds = rightingEvidencePresent
                ? Math.Min(RightingCommitSeconds + RightingEvidenceHoldSeconds, rightingSeconds + dt)
                : Math.Max(0.0, rightingSeconds - dt);
            // Righting populations recruit real axial force. Convert that
            // measured force into torque around the body's measured inertias;
            // never author an angle or snap the pose toward upright.
            var rightingTorqueNewtonMeters =
                totalMass * GravityMetersPerSecondSquared * 0.22 * recoveryDrive * recoveryForce;
            var pitchRestoringDirection = RestoringDirection(pitch, pitchVelocity);
            var rollRestoringDirection = RestoringDirection(roll, rollVelocity);
            pitchVelocity += pitchRestoringDirection *
                (rightingTorqueNewtonMeters / pitchInertia) * dt;
            rollVelocity += rollRestoringDirection *
                (rightingTorqueNewtonMeters / rollInertia) * dt;
            var muscularDamping = 1.2 + (3.8 * recoveryDrive * recoveryForce);
            pitchVelocity = Damp(pitchVelocity, muscularDamping, dt);
            rollVelocity = Damp(rollVelocity, muscularDamping, dt);
            pitch += pitchVelocity * dt;
            roll += rollVelocity * dt;

            if (phase is AvatarBalancePhase.Righting || rightingSeconds >= RightingCommitSeconds)
            {
                phase = AvatarBalancePhase.Righting;
                var physicallyStable = Math.Max(Math.Abs(pitch), Math.Abs(roll)) <= RightingCompletionAngleRadians &&
                    !recumbentSupport && hasSupport && dynamicMargin >= MarginalMarginMeters;
                recoveryStableSeconds = physicallyStable
                    ? recoveryStableSeconds + dt
                    : Math.Max(0.0, recoveryStableSeconds - (dt * 2.0));
                if (recoveryStableSeconds >= RightingStableCommitSeconds)
                {
                    instabilitySeconds = 0.0;
                    rightingSeconds = 0.0;
                    recoveryStableSeconds = 0.0;
                    phase = dynamicMargin < MarginalMarginMeters
                        ? AvatarBalancePhase.Marginal
                        : AvatarBalancePhase.Stable;
                }
            }
        }
        else if (passiveRecoveryMeasured)
        {
            // This is state reconciliation, not a recovery controller: the body
            // has already become upright, slow, and well supported through its
            // own measured mechanics. No joint, pose, or root motion is authored.
            recoveryStableSeconds += dt;
            pitchVelocity = Damp(pitchVelocity, StableAngularDampingPerSecond, dt);
            rollVelocity = Damp(rollVelocity, StableAngularDampingPerSecond, dt);
            centerVelocity *= (float)Math.Exp(-StableAngularDampingPerSecond * dt);
            if (recoveryStableSeconds >= PassiveRecoveryStableCommitSeconds)
            {
                phase = dynamicMargin < MarginalMarginMeters
                    ? AvatarBalancePhase.Marginal
                    : AvatarBalancePhase.Stable;
                instabilitySeconds = 0.0;
                rightingSeconds = 0.0;
                recoveryStableSeconds = 0.0;
            }
        }
        else if (phase is AvatarBalancePhase.Righting)
        {
            phase = AvatarBalancePhase.Falling;
            rightingSeconds = 0.0;
            recoveryStableSeconds = 0.0;
        }
        else if (controlledDescent && !broadSupport)
        {
            phase = AvatarBalancePhase.Marginal;
            rightingSeconds = 0.0;
            recoveryStableSeconds = 0.0;
            pitchVelocity = Damp(pitchVelocity, StableAngularDampingPerSecond, dt);
            rollVelocity = Damp(rollVelocity, StableAngularDampingPerSecond, dt);
            pitch += pitchVelocity * dt;
            roll += rollVelocity * dt;
        }
        else if (recumbentSupport && commandedPosture != "lying")
        {
            // Multiple load-bearing axial contacts are direct physical evidence
            // that the body is recumbent. They may arrest a fall, but they do
            // not turn it into upright broad support or author a recovery.
            phase = AvatarBalancePhase.Fallen;
            instabilitySeconds = Math.Max(instabilitySeconds, FallCommitSeconds);
            rightingSeconds = 0.0;
            recoveryStableSeconds = 0.0;
            pitchVelocity = Damp(pitchVelocity, 7.5, dt);
            rollVelocity = Damp(rollVelocity, 7.5, dt);
        }
        else if (broadSupport && phase is not AvatarBalancePhase.Falling and not AvatarBalancePhase.Fallen)
        {
            phase = AvatarBalancePhase.BroadSupport;
            rightingSeconds = 0.0;
            recoveryStableSeconds = 0.0;
            pitchVelocity = Damp(pitchVelocity, StableAngularDampingPerSecond, dt);
            rollVelocity = Damp(rollVelocity, StableAngularDampingPerSecond, dt);
            pitch += pitchVelocity * dt;
            roll += rollVelocity * dt;
        }
        else
        {
            var committed = phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen ||
                            instabilitySeconds >= FallCommitSeconds;
            if (committed)
            {
                phase = AvatarBalancePhase.Falling;
                rightingSeconds = 0.0;
                recoveryStableSeconds = 0.0;
                var staticLever = center2 - centerOfPressure;
                var dynamicLever = extrapolatedCenter - centerOfPressure;
                // Once momentum has carried the capture point farther outside
                // support than the static mass, fall direction follows that
                // dynamic lever. Using only the static COM can make a body that
                // is travelling forward rotate backward around its feet.
                var lever = dynamicMargin < staticMargin
                    ? dynamicLever
                    : staticLever;
                if (lever.LengthSquared() < 0.000001f)
                {
                    lever = dynamicLever;
                }
                if (lever.LengthSquared() < 0.000001f)
                {
                    lever = new Vector2(0f, -1f);
                }

                var gravitationalPitchAcceleration =
                    (-(GravityMetersPerSecondSquared / comHeight) * lever.Y) +
                    ((GravityMetersPerSecondSquared / comHeight) * Math.Sin(pitch));
                var gravitationalRollAcceleration =
                    (-(GravityMetersPerSecondSquared / comHeight) * lever.X) +
                    ((GravityMetersPerSecondSquared / comHeight) * Math.Sin(roll));
                pitchVelocity += gravitationalPitchAcceleration * dt;
                rollVelocity += gravitationalRollAcceleration * dt;
                pitchVelocity = Damp(pitchVelocity, PassiveAngularDampingPerSecond, dt);
                rollVelocity = Damp(rollVelocity, PassiveAngularDampingPerSecond, dt);
                pitch += pitchVelocity * dt;
                roll += rollVelocity * dt;

                var maximumTilt = Math.Max(Math.Abs(pitch), Math.Abs(roll));
                if (maximumTilt >= FallenAngleRadians)
                {
                    phase = AvatarBalancePhase.Fallen;
                    pitchVelocity = Damp(pitchVelocity, 7.5, dt);
                    rollVelocity = Damp(rollVelocity, 7.5, dt);
                }
            }
            else
            {
                phase = dynamicMargin < InstabilityMarginMeters
                    ? dynamicMargin >= effectiveInstabilityMargin && dynamicStabilityAllowance > 0.0
                        ? AvatarBalancePhase.Dynamic
                        : AvatarBalancePhase.Unstable
                    : dynamicMargin < MarginalMarginMeters
                        ? AvatarBalancePhase.Marginal
                        : AvatarBalancePhase.Stable;
                pitchVelocity = Damp(pitchVelocity, StableAngularDampingPerSecond, dt);
                rollVelocity = Damp(rollVelocity, StableAngularDampingPerSecond, dt);
            }
        }

        pitch = Math.Clamp(pitch, -MaximumFallAngleRadians, MaximumFallAngleRadians);
        roll = Math.Clamp(roll, -MaximumFallAngleRadians, MaximumFallAngleRadians);
        var tilt = Math.Max(Math.Abs(pitch), Math.Abs(roll));
        var uprightFraction = Math.Clamp(Math.Cos(Math.Min(Math.PI * 0.5, tilt)), 0.0, 1.0);
        if (recumbentSupport && phase is AvatarBalancePhase.Fallen)
        {
            uprightFraction = Math.Min(uprightFraction, RecumbentMaximumUprightFraction);
        }
        var physicalHeight = phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen or AvatarBalancePhase.Righting
            ? Math.Clamp(
                (commandedBodyHeightMeters * uprightFraction) + (0.28 * (1.0 - uprightFraction)),
                0.28,
                commandedBodyHeightMeters)
            : commandedBodyHeightMeters;
        // Standing remains a controlled instability. The raw centre-of-mass,
        // capture-point, and support-margin measurements continue upstream for
        // neuronal learning, but sway inside the mechanically recoverable
        // envelope is not itself an aversive balance error.
        var acceptableControlMargin = effectiveInstabilityMargin;
        var balanceError = ResolveBalanceError(
            dynamicMargin,
            acceptableControlMargin,
            instabilitySeconds,
            tilt,
            phase);
        var physicalPosture = phase switch
        {
            AvatarBalancePhase.Falling => "falling",
            AvatarBalancePhase.Fallen => "fallen",
            AvatarBalancePhase.Righting => "righting",
            _ => commandedPosture
        };
        var next = new AvatarBalanceState(
            true,
            center2,
            centerVelocity,
            pitch,
            roll,
            pitchVelocity,
            rollVelocity,
            instabilitySeconds,
            rightingSeconds,
            phase,
            recoveryStableSeconds);
        var frame = new PhysicalBalanceStateFrame(
            (float)centerOfMass.X,
            (float)centerOfMass.Y,
            (float)centerOfMass.Z,
            centerVelocity.X,
            centerVelocity.Y,
            extrapolatedCenter.X,
            extrapolatedCenter.Y,
            centerOfPressure.X,
            centerOfPressure.Y,
            (float)supportArea,
            (float)(hasSupport ? Math.Min(staticMargin, dynamicMargin) : -1.0),
            (float)pitch,
            (float)roll,
            (float)pitchVelocity,
            (float)rollVelocity,
            PhaseName(phase),
            (float)recoveryForce,
            (float)dynamicStabilityAllowance);
        return new AvatarBalanceResult(
            next,
            frame,
            balanceError,
            uprightFraction,
            physicalHeight,
            physicalPosture);
    }

    private static (Vector3 Center, double Mass, double PitchInertia, double RollInertia) ResolveMassProperties(
        IReadOnlyList<AvatarBodyCollider> colliders)
    {
        var totalMass = colliders.Sum(static collider => Math.Max(0.001f, collider.EffectiveMassKilograms));
        if (totalMass <= 0.0)
        {
            return (new Vector3(0f, 0.94f, 0f), 72.0, 18.0, 18.0);
        }

        var weighted = Vector3.Zero;
        foreach (var collider in colliders)
        {
            weighted += collider.Position * Math.Max(0.001f, collider.EffectiveMassKilograms);
        }
        var center = weighted / (float)totalMass;
        var pitchInertia = 0.0;
        var rollInertia = 0.0;
        foreach (var collider in colliders)
        {
            var mass = Math.Max(0.001f, collider.EffectiveMassKilograms);
            var offset = collider.Position - center;
            pitchInertia += mass * ((offset.Y * offset.Y) + (offset.Z * offset.Z));
            rollInertia += mass * ((offset.X * offset.X) + (offset.Y * offset.Y));
        }
        return (center, totalMass, Math.Max(1.0, pitchInertia), Math.Max(1.0, rollInertia));
    }

    private static IReadOnlyList<SupportSample> BuildSupportSamples(
        IReadOnlyList<AvatarGroundContactProbe> groundContacts,
        IReadOnlyList<AvatarExternalBodyContact> externalContacts)
    {
        var samples = new List<SupportSample>(groundContacts.Count + externalContacts.Count);
        foreach (var contact in groundContacts)
        {
            if (contact.LoadNewtons >= MinimumSupportLoadNewtons)
            {
                samples.Add(new SupportSample(
                    contact.Region,
                    new Vector2((float)contact.BodyX, (float)contact.BodyZ),
                    contact.LoadNewtons,
                    PatchHalfExtents(contact.Region, contact.AreaSquareMillimeters)));
            }
        }
        foreach (var contact in externalContacts)
        {
            if (contact.ForceNewtons < MinimumSupportLoadNewtons || contact.BodyNormal.Y < 0.55f)
            {
                continue;
            }
            var radius = Math.Clamp(
                Math.Sqrt(Math.Max(1.0, contact.ContactAreaSquareMillimeters) / Math.PI) / 1000.0,
                0.025,
                0.14);
            samples.Add(new SupportSample(
                contact.Region,
                new Vector2(contact.BodyPosition.X, contact.BodyPosition.Z),
                contact.ForceNewtons,
                new Vector2((float)radius, (float)radius)));
        }
        return samples;
    }

    private static bool HasBroadBodySupport(IReadOnlyList<AvatarGroundContactProbe> contacts)
        => contacts.Any(static contact =>
            contact.LoadNewtons >= MinimumSupportLoadNewtons &&
            IsBroadSupportRegion(contact.Region));

    private static bool HasRecumbentSupport(IReadOnlyList<AvatarExternalBodyContact> contacts)
    {
        var regions = contacts
            .Where(static contact =>
                contact.ForceNewtons >= MinimumSupportLoadNewtons &&
                contact.BodyNormal.Y >= 0.55f)
            .Select(static contact => contact.Region)
            .ToHashSet(StringComparer.Ordinal);
        var chest = regions.Contains("chest");
        var head = regions.Contains("head");
        var pelvis = regions.Contains("pelvis");
        var leftLeg = regions.Overlaps(["left_knee", "left_shin", "left_thigh"]);
        var rightLeg = regions.Overlaps(["right_knee", "right_shin", "right_thigh"]);

        return (chest && (head || pelvis || leftLeg || rightLeg)) ||
               (head && pelvis && (leftLeg || rightLeg)) ||
               (pelvis && leftLeg && rightLeg && (head || chest));
    }

    private static bool IsBroadSupportRegion(string region)
        => region is "pelvis" or "chest" or "head" or
            "left_knee" or "right_knee" or
            "left_shin" or "right_shin" or
            "left_thigh" or "right_thigh";

    private static double ResolveDynamicStabilityAllowance(
        double locomotorEffort,
        double commandedForwardSpeedMetersPerSecond,
        IReadOnlyList<SupportSample> supportSamples,
        bool broadSupport,
        double pitch,
        double roll,
        double crossedLegConstraint)
    {
        if (broadSupport || supportSamples.Count == 0)
        {
            return 0.0;
        }

        var hasFootSupport = supportSamples.Any(static sample =>
            sample.Region.StartsWith("left_foot", StringComparison.Ordinal) ||
            sample.Region.StartsWith("right_foot", StringComparison.Ordinal));
        if (!hasFootSupport)
        {
            return 0.0;
        }

        var effort = Math.Clamp(Sanitize(locomotorEffort), 0.0, 1.0);
        var speed = Math.Clamp(Math.Abs(Sanitize(commandedForwardSpeedMetersPerSecond)) / 1.8, 0.0, 1.0);
        var upright = Math.Clamp(Math.Cos(Math.Min(Math.PI * 0.5, Math.Max(Math.Abs(pitch), Math.Abs(roll)))), 0.0, 1.0);
        var availableStepReserve = 1.0 - Math.Clamp(crossedLegConstraint, 0.0, 1.0);
        return MaximumDynamicStabilityAllowanceMeters * effort * (0.35 + (speed * 0.65)) * upright *
            availableStepReserve;
    }

    private static double ResolveCrossedLegConstraint(
        IReadOnlyList<AvatarBodyCollider> colliders,
        IReadOnlyList<SupportSample> supportSamples)
    {
        var hasLeftSupport = supportSamples.Any(static sample =>
            sample.Region.StartsWith("left_foot", StringComparison.Ordinal));
        var hasRightSupport = supportSamples.Any(static sample =>
            sample.Region.StartsWith("right_foot", StringComparison.Ordinal));
        if (!hasLeftSupport || !hasRightSupport)
        {
            return 0.0;
        }

        var leftFoot = colliders.FirstOrDefault(static collider => collider.Region == "left_foot");
        var rightFoot = colliders.FirstOrDefault(static collider => collider.Region == "right_foot");
        if (leftFoot.Region is null || rightFoot.Region is null)
        {
            return 0.0;
        }

        // Left is negative X and right is positive X in body space. As that
        // ordering closes and reverses, the legs obstruct one another and a
        // normal forward capture step ceases to be mechanically available.
        var lateralClearance = rightFoot.Position.X - leftFoot.Position.X;
        return Math.Clamp(
            (MinimumUnobstructedFootClearanceMeters - lateralClearance) /
            MinimumUnobstructedFootClearanceMeters,
            0.0,
            1.0);
    }

    private static Vector2 PatchHalfExtents(string region, double areaSquareMillimeters) => region switch
    {
        "left_foot" or "right_foot" => new Vector2(0.09f, 0.155f),
        "left_hand" or "right_hand" => new Vector2(0.055f, 0.07f),
        "left_knee" or "right_knee" or "left_shin" or "right_shin" or
            "left_thigh" or "right_thigh" => new Vector2(0.075f, 0.07f),
        "pelvis" => new Vector2(0.22f, 0.15f),
        "chest" => new Vector2(0.23f, 0.18f),
        "head" => new Vector2(0.12f, 0.12f),
        _ => CircularPatchHalfExtents(areaSquareMillimeters)
    };

    private static Vector2 CircularPatchHalfExtents(double areaSquareMillimeters)
    {
        var radius = Math.Clamp(
            Math.Sqrt(Math.Max(1.0, areaSquareMillimeters) / Math.PI) / 1000.0,
            0.025,
            0.14);
        return new Vector2((float)radius, (float)radius);
    }

    private static IEnumerable<Vector2> CreatePatch(SupportSample sample)
    {
        var center = sample.Position;
        var half = sample.HalfExtents;
        yield return center + new Vector2(-half.X, -half.Y);
        yield return center + new Vector2(-half.X, half.Y);
        yield return center + new Vector2(half.X, -half.Y);
        yield return center + new Vector2(half.X, half.Y);
    }

    private static Vector2 ResolveCenterOfPressure(IReadOnlyList<SupportSample> samples)
    {
        var totalLoad = samples.Sum(static sample => Math.Max(0.0, sample.LoadNewtons));
        if (totalLoad <= 0.0)
        {
            return Vector2.Zero;
        }
        var weighted = Vector2.Zero;
        foreach (var sample in samples)
        {
            weighted += sample.Position * (float)(Math.Max(0.0, sample.LoadNewtons) / totalLoad);
        }
        return weighted;
    }

    private static (double PitchAcceleration, double RollAcceleration) ResolveExternalTorque(
        IReadOnlyList<AvatarExternalBodyContact> contacts,
        Vector3 centerOfMass,
        double totalMass,
        double pitchInertia,
        double rollInertia)
    {
        var torque = Vector3.Zero;
        foreach (var contact in contacts)
        {
            var force = contact.BodyNormal * (float)Math.Max(0.0, contact.ForceNewtons);
            torque += Vector3.Cross(contact.BodyPosition - centerOfMass, force);
        }
        var massScale = Math.Clamp(totalMass / 72.0, 0.5, 2.0);
        return (
            Math.Clamp(torque.X / pitchInertia / massScale, -24.0, 24.0),
            Math.Clamp(torque.Z / rollInertia / massScale, -24.0, 24.0));
    }

    private static IReadOnlyList<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
    {
        if (points.Count <= 1)
        {
            return points.ToArray();
        }
        var sorted = points
            .Distinct()
            .OrderBy(static point => point.X)
            .ThenBy(static point => point.Y)
            .ToArray();
        if (sorted.Length <= 2)
        {
            return sorted;
        }
        var lower = new List<Vector2>();
        foreach (var point in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^1] - lower[^2], point - lower[^1]) <= 0f)
            {
                lower.RemoveAt(lower.Count - 1);
            }
            lower.Add(point);
        }
        var upper = new List<Vector2>();
        for (var index = sorted.Length - 1; index >= 0; index--)
        {
            var point = sorted[index];
            while (upper.Count >= 2 && Cross(upper[^1] - upper[^2], point - upper[^1]) <= 0f)
            {
                upper.RemoveAt(upper.Count - 1);
            }
            upper.Add(point);
        }
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static double SignedMargin(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        if (polygon.Count < 3)
        {
            return -1.0;
        }
        var inside = true;
        var minimum = double.PositiveInfinity;
        for (var index = 0; index < polygon.Count; index++)
        {
            var a = polygon[index];
            var b = polygon[(index + 1) % polygon.Count];
            if (Cross(b - a, point - a) < -0.000001f)
            {
                inside = false;
            }
            minimum = Math.Min(minimum, DistanceToSegment(point, a, b));
        }
        return inside ? minimum : -minimum;
    }

    private static double PolygonArea(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3)
        {
            return 0.0;
        }
        var twiceArea = 0.0;
        for (var index = 0; index < polygon.Count; index++)
        {
            var a = polygon[index];
            var b = polygon[(index + 1) % polygon.Count];
            twiceArea += (a.X * b.Y) - (a.Y * b.X);
        }
        return Math.Abs(twiceArea) * 0.5;
    }

    private static double DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0000001f)
        {
            return Vector2.Distance(point, a);
        }
        var projection = Math.Clamp(Vector2.Dot(point - a, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, a + (segment * projection));
    }

    private static double ResolveBalanceError(
        double dynamicMargin,
        double acceptableControlMargin,
        double instabilitySeconds,
        double tilt,
        AvatarBalancePhase phase)
    {
        var marginError = dynamicMargin >= acceptableControlMargin
            ? 0.0
            : Math.Clamp((acceptableControlMargin - dynamicMargin) / 0.22, 0.0, 1.0);
        var timeError = Math.Clamp(instabilitySeconds / FallCommitSeconds, 0.0, 1.0);
        var tiltError = tilt <= AcceptablePosturalTiltRadians
            ? 0.0
            : Math.Clamp(
                (tilt - AcceptablePosturalTiltRadians) /
                (FallenAngleRadians - AcceptablePosturalTiltRadians),
                0.0,
                1.0);
        var phaseFloor = phase switch
        {
            AvatarBalancePhase.Falling => 0.68,
            AvatarBalancePhase.Fallen => 1.0,
            AvatarBalancePhase.Righting => Math.Max(0.40, Math.Sin(Math.Min(Math.PI * 0.5, tilt))),
            AvatarBalancePhase.Unstable => 0.45,
            AvatarBalancePhase.Airborne => 0.25,
            _ => 0.0
        };
        return Math.Clamp(Math.Max(phaseFloor, Math.Max(marginError, Math.Max(timeError * 0.72, tiltError))), 0.0, 1.0);
    }

    private static Vector2 ClampMagnitude(Vector2 value, double maximum)
    {
        var length = value.Length();
        return length > maximum && length > 0f ? value * (float)(maximum / length) : value;
    }

    private static double Damp(double value, double dampingPerSecond, double dt)
        => value * Math.Exp(-Math.Max(0.0, dampingPerSecond) * dt);

    private static double RestoringDirection(double angle, double angularVelocity)
    {
        if (Math.Abs(angle) > 0.002)
        {
            return -Math.Sign(angle);
        }

        return Math.Abs(angularVelocity) > 0.002
            ? -Math.Sign(angularVelocity)
            : 0.0;
    }

    private static float Cross(Vector2 first, Vector2 second) =>
        (first.X * second.Y) - (first.Y * second.X);

    private static double Sanitize(double value) => double.IsFinite(value) ? value : 0.0;

    private static string PhaseName(AvatarBalancePhase phase) => phase switch
    {
        AvatarBalancePhase.BroadSupport => "broad_support",
        AvatarBalancePhase.Righting => "righting",
        _ => phase.ToString().ToLowerInvariant()
    };

    private readonly record struct SupportSample(
        string Region,
        Vector2 Position,
        double LoadNewtons,
        Vector2 HalfExtents);
}
