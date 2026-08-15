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
    BroadSupport
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
    AvatarBalancePhase Phase)
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
    private const double FallCommitSeconds = 0.10;
    private const double RightingCommitSeconds = 0.14;
    private const double RightingCompletionAngleRadians = 0.16;
    private const double MinimumRightingDrive = 0.16;
    private const double MinimumRightingForceFraction = 0.10;
    private const double FallenAngleRadians = 1.30;
    private const double MaximumFallAngleRadians = 1.50;
    private const double PassiveAngularDampingPerSecond = 0.42;
    private const double StableAngularDampingPerSecond = 5.5;
    private const double MaximumComSpeedMetersPerSecond = 5.0;
    private const double MinimumSupportLoadNewtons = 0.5;

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
        double rightingForceFraction = 0.0)
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
        var extrapolatedCenter = center2 + (pendularVelocity / (float)naturalFrequency);
        var staticMargin = SignedMargin(supportHull, center2);
        var dynamicMargin = SignedMargin(supportHull, extrapolatedCenter);

        var broadSupport = groundContacts.Any(static contact =>
            contact.Region is "pelvis" or "chest" or "head" or
                "left_knee" or "right_knee" or
                "left_shin" or "right_shin" or
                "left_thigh" or "right_thigh");
        var hasSupport = grounded && supportHull.Count >= 3 && supportArea > 0.0001;
        var instabilitySeconds = current.InstabilitySeconds;
        if (!hasSupport || broadSupport || dynamicMargin >= InstabilityMarginMeters)
        {
            instabilitySeconds = Math.Max(0.0, instabilitySeconds - (dt * 2.0));
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
        var controlledDescent = commandedPosture is "sitting" or "lying" &&
                                current.Phase is not AvatarBalancePhase.Falling and
                                    not AvatarBalancePhase.Fallen and
                                    not AvatarBalancePhase.Righting &&
                                hasSupport &&
                                externalContacts.Count == 0;

        var phase = current.Phase;
        if (!grounded)
        {
            phase = AvatarBalancePhase.Airborne;
            rightingSeconds = 0.0;
            pitch += pitchVelocity * dt;
            roll += rollVelocity * dt;
        }
        else if (phase is (AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen or AvatarBalancePhase.Righting) &&
                 (requestsRighting || phase is AvatarBalancePhase.Righting && continuesRighting))
        {
            rightingSeconds += dt;
            pitchVelocity = Damp(pitchVelocity, 8.0, dt);
            rollVelocity = Damp(rollVelocity, 8.0, dt);
            centerVelocity *= (float)Math.Exp(-8.0 * dt);

            if (rightingSeconds >= RightingCommitSeconds)
            {
                phase = AvatarBalancePhase.Righting;
                var rightingRate = 0.22 + (1.35 * recoveryDrive * recoveryForce);
                pitch = MoveTowards(pitch, 0.0, rightingRate * dt);
                roll = MoveTowards(roll, 0.0, rightingRate * dt);

                if (Math.Max(Math.Abs(pitch), Math.Abs(roll)) <= RightingCompletionAngleRadians &&
                    hasSupport &&
                    dynamicMargin >= InstabilityMarginMeters)
                {
                    pitch = 0.0;
                    roll = 0.0;
                    pitchVelocity = 0.0;
                    rollVelocity = 0.0;
                    centerVelocity = Vector2.Zero;
                    instabilitySeconds = 0.0;
                    rightingSeconds = 0.0;
                    phase = hasSupport ? AvatarBalancePhase.Marginal : AvatarBalancePhase.Airborne;
                }
            }
        }
        else if (phase is AvatarBalancePhase.Righting)
        {
            phase = AvatarBalancePhase.Falling;
            rightingSeconds = 0.0;
        }
        else if (controlledDescent && !broadSupport)
        {
            phase = AvatarBalancePhase.Marginal;
            rightingSeconds = 0.0;
            pitchVelocity = Damp(pitchVelocity, StableAngularDampingPerSecond, dt);
            rollVelocity = Damp(rollVelocity, StableAngularDampingPerSecond, dt);
            pitch = MoveTowards(pitch, 0.0, Math.Abs(pitchVelocity) * dt + (dt * 0.7));
            roll = MoveTowards(roll, 0.0, Math.Abs(rollVelocity) * dt + (dt * 0.7));
        }
        else if (broadSupport && phase is not AvatarBalancePhase.Falling and not AvatarBalancePhase.Fallen)
        {
            phase = AvatarBalancePhase.BroadSupport;
            rightingSeconds = 0.0;
            pitchVelocity = Damp(pitchVelocity, StableAngularDampingPerSecond, dt);
            rollVelocity = Damp(rollVelocity, StableAngularDampingPerSecond, dt);
            pitch = MoveTowards(pitch, 0.0, Math.Abs(pitchVelocity) * dt + (dt * 0.7));
            roll = MoveTowards(roll, 0.0, Math.Abs(rollVelocity) * dt + (dt * 0.7));
        }
        else
        {
            var committed = phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen ||
                            instabilitySeconds >= FallCommitSeconds;
            if (committed)
            {
                phase = AvatarBalancePhase.Falling;
                rightingSeconds = 0.0;
                var lever = center2 - centerOfPressure;
                if (lever.LengthSquared() < 0.000001f)
                {
                    lever = extrapolatedCenter - centerOfPressure;
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
                    ? AvatarBalancePhase.Unstable
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
        var physicalHeight = phase is AvatarBalancePhase.Falling or AvatarBalancePhase.Fallen or AvatarBalancePhase.Righting
            ? Math.Clamp(
                (commandedBodyHeightMeters * uprightFraction) + (0.28 * (1.0 - uprightFraction)),
                0.28,
                commandedBodyHeightMeters)
            : commandedBodyHeightMeters;
        var balanceError = ResolveBalanceError(dynamicMargin, instabilitySeconds, tilt, phase);
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
            phase);
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
            (float)recoveryForce);
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
        double instabilitySeconds,
        double tilt,
        AvatarBalancePhase phase)
    {
        var marginError = dynamicMargin >= 0.0
            ? Math.Clamp((MarginalMarginMeters - dynamicMargin) / MarginalMarginMeters, 0.0, 0.45)
            : Math.Clamp((-dynamicMargin) / 0.22, 0.0, 1.0);
        var timeError = Math.Clamp(instabilitySeconds / FallCommitSeconds, 0.0, 1.0);
        var tiltError = Math.Clamp(tilt / FallenAngleRadians, 0.0, 1.0);
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

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        var delta = target - current;
        return Math.Abs(delta) <= maximumDelta ? target : current + (Math.Sign(delta) * maximumDelta);
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
