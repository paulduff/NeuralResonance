using System.Numerics;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public enum AvatarColliderShape
{
    Sphere,
    Capsule,
    Box
}

public enum AvatarKinematicChain
{
    Axial,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}

/// <summary>
/// A body-local collision volume whose outer surface approximates the avatar's
/// visible skin. Size is radius for spheres, radius/internal length for
/// capsules, and full width/height/length for boxes.
/// </summary>
public readonly record struct AvatarBodyCollider(
    string Region,
    AvatarKinematicChain Chain,
    AvatarColliderShape Shape,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 Size,
    float EffectiveMassKilograms,
    float ContactAreaSquareMillimeters);

/// <summary>
/// Converts measured joint angles into the same articulated body space used by
/// the rendered skeleton. It contains no behavioural policy; it is the physical
/// skin boundary presented to the world collision engine.
/// </summary>
public static class AvatarColliderRig
{
    public const float LocalGroundPlaneY = -0.03f;

    private const float StandingHeightMeters = 1.74f;
    private const float UpperArmLength = 0.34f;
    private const float ForearmLength = 0.32f;
    private const float ThighLength = 0.39f;
    private const float ShinLength = 0.38f;

    public static IReadOnlyList<AvatarBodyCollider> Capture(PhysicalArticulationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var groundedFrame = WithComputedSupportPlaneOffset(frame);
        return CaptureResolved(groundedFrame);
    }

    public static IReadOnlyList<AvatarBodyCollider> CaptureResolved(PhysicalArticulationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return CaptureCore(frame, frame.SupportPlaneOffsetMeters);
    }

    public static PhysicalArticulationFrame WithComputedSupportPlaneOffset(PhysicalArticulationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var uncorrected = CaptureCore(frame, 0f);
        var lowestSurface = uncorrected
            .Where(static collider =>
                collider.Chain is AvatarKinematicChain.Axial or
                    AvatarKinematicChain.LeftLeg or
                    AvatarKinematicChain.RightLeg)
            .Min(LowestSurfaceY);
        var offset = Math.Clamp(LocalGroundPlaneY - lowestSurface, -0.30f, 0.80f);
        return frame with { SupportPlaneOffsetMeters = offset };
    }

    private static IReadOnlyList<AvatarBodyCollider> CaptureCore(
        PhysicalArticulationFrame frame,
        float supportPlaneOffsetMeters)
    {
        var musculoskeletal = frame.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
        var compression = Math.Clamp((StandingHeightMeters - musculoskeletal.BodyHeightMeters) / 1.40f, 0f, 1f);
        var pelvisHeight = 0.78f - (compression * 0.48f);
        var lyingProgress = string.Equals(musculoskeletal.Posture, "lying", StringComparison.OrdinalIgnoreCase)
            ? Math.Clamp((1.48f - musculoskeletal.BodyHeightMeters) / 1.14f, 0f, 1f)
            : 0f;
        var balance = musculoskeletal.Balance ?? PhysicalBalanceStateFrame.Neutral;
        var commandedLyingRotation = Quaternion.CreateFromAxisAngle(
            Vector3.UnitX,
            -MathF.PI * 0.48f * lyingProgress);
        var physicalFallRotation = Quaternion.CreateFromYawPitchRoll(
            0f,
            -balance.FallPitchRadians,
            balance.FallRollRadians);
        var bodyPose = new RigPose(
            new Vector3(0f, (lyingProgress * 0.035f) + supportPlaneOffsetMeters, 0f),
            Quaternion.Normalize(commandedLyingRotation * physicalFallRotation));
        var pelvis = Child(bodyPose, new Vector3(0f, pelvisHeight, 0f), Quaternion.Identity);
        var axialRotation = Quaternion.CreateFromYawPitchRoll(
            frame.TrunkYawRadians * (1f - lyingProgress),
            -frame.TrunkPitchRadians * (1f - lyingProgress),
            frame.TrunkRollRadians * 0.72f);
        var lumbar = Child(pelvis, new Vector3(0f, 0.19f, 0f), axialRotation);
        var thoracic = Child(lumbar, new Vector3(0f, 0.28f, 0f), Quaternion.Identity);
        var neckRotation = Quaternion.CreateFromYawPitchRoll(
            frame.NeckYawRadians,
            -frame.NeckPitchRadians,
            0f);
        var neck = Child(thoracic, new Vector3(0f, 0.20f, 0f), neckRotation);
        var head = Child(neck, new Vector3(0f, 0.20f, 0f), Quaternion.Identity);

        var colliders = new List<AvatarBodyCollider>(18)
        {
            Box("pelvis", AvatarKinematicChain.Axial,
                Child(pelvis, new Vector3(0f, 0f, -0.06f), Quaternion.Identity),
                new Vector3(0.50f, 0.30f, 0.35f), 13f, 18_000f),
            Capsule("chest", AvatarKinematicChain.Axial, Between(lumbar, thoracic), 0.255f, 0.34f, 24f, 24_000f),
            Capsule("neck", AvatarKinematicChain.Axial, Between(thoracic, neck), 0.085f, 0.08f, 2.2f, 2_800f),
            Sphere("head", AvatarKinematicChain.Axial, head, 0.225f, 5.0f, 8_000f)
        };

        AddArm(colliders, thoracic, frame, left: true);
        AddArm(colliders, thoracic, frame, left: false);
        AddLeg(colliders, pelvis, frame, left: true);
        AddLeg(colliders, pelvis, frame, left: false);
        return colliders;
    }

    public static PhysicalArticulationFrame Interpolate(
        PhysicalArticulationFrame previous,
        PhysicalArticulationFrame proposed,
        float fraction)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(proposed);

        var t = Math.Clamp(fraction, 0f, 1f);
        var previousBody = previous.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
        var proposedBody = proposed.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
        var resolvedBody = proposedBody with
        {
            Posture = t <= 0f ? previousBody.Posture : proposedBody.Posture,
            BodyHeightMeters = Lerp(previousBody.BodyHeightMeters, proposedBody.BodyHeightMeters, t),
            UprightFraction = Lerp(previousBody.UprightFraction, proposedBody.UprightFraction, t),
            BalanceError = Lerp(previousBody.BalanceError, proposedBody.BalanceError, t),
            Balance = InterpolateBalance(previousBody.Balance, proposedBody.Balance, t)
        };

        var interpolated = new PhysicalArticulationFrame(
            Lerp(previous.LeftHipAngleRadians, proposed.LeftHipAngleRadians, t),
            Lerp(previous.RightHipAngleRadians, proposed.RightHipAngleRadians, t),
            Lerp(previous.LeftKneeAngleRadians, proposed.LeftKneeAngleRadians, t),
            Lerp(previous.RightKneeAngleRadians, proposed.RightKneeAngleRadians, t),
            Lerp(previous.LeftAnkleAngleRadians, proposed.LeftAnkleAngleRadians, t),
            Lerp(previous.RightAnkleAngleRadians, proposed.RightAnkleAngleRadians, t),
            proposed.LeftFootLoadNewtons,
            proposed.RightFootLoadNewtons,
            Lerp(previous.LeftShoulderAngleRadians, proposed.LeftShoulderAngleRadians, t),
            Lerp(previous.RightShoulderAngleRadians, proposed.RightShoulderAngleRadians, t),
            Lerp(previous.LeftElbowAngleRadians, proposed.LeftElbowAngleRadians, t),
            Lerp(previous.RightElbowAngleRadians, proposed.RightElbowAngleRadians, t),
            proposed.LeftHandLoadNewtons,
            proposed.RightHandLoadNewtons,
            Lerp(previous.ManipulatorExtensionFraction, proposed.ManipulatorExtensionFraction, t),
            Lerp(previous.TrunkPitchRadians, proposed.TrunkPitchRadians, t),
            Lerp(previous.TrunkRollRadians, proposed.TrunkRollRadians, t),
            resolvedBody,
            Lerp(previous.LeftShoulderAbductionRadians, proposed.LeftShoulderAbductionRadians, t),
            Lerp(previous.RightShoulderAbductionRadians, proposed.RightShoulderAbductionRadians, t),
            Lerp(previous.NeckYawRadians, proposed.NeckYawRadians, t),
            Lerp(previous.NeckPitchRadians, proposed.NeckPitchRadians, t),
            Lerp(previous.SupportPlaneOffsetMeters, proposed.SupportPlaneOffsetMeters, t),
            Lerp(previous.LeftHipAbductionRadians, proposed.LeftHipAbductionRadians, t),
            Lerp(previous.RightHipAbductionRadians, proposed.RightHipAbductionRadians, t),
            Lerp(previous.LeftAnkleRollRadians, proposed.LeftAnkleRollRadians, t),
            Lerp(previous.RightAnkleRollRadians, proposed.RightAnkleRollRadians, t),
            proposed.LeftFootPressure,
            proposed.RightFootPressure,
            Lerp(previous.TrunkYawRadians, proposed.TrunkYawRadians, t),
            Lerp(previous.LeftHandApertureFraction, proposed.LeftHandApertureFraction, t),
            Lerp(previous.RightHandApertureFraction, proposed.RightHandApertureFraction, t),
            Lerp(previous.LeftGripForceNewtons, proposed.LeftGripForceNewtons, t),
            Lerp(previous.RightGripForceNewtons, proposed.RightGripForceNewtons, t),
            Lerp(previous.LeftHandFatigue, proposed.LeftHandFatigue, t),
            Lerp(previous.RightHandFatigue, proposed.RightHandFatigue, t),
            Lerp(previous.LeftHandSlip, proposed.LeftHandSlip, t),
            Lerp(previous.RightHandSlip, proposed.RightHandSlip, t));
        return interpolated;
    }

    public static PhysicalArticulationFrame RetargetChain(
        PhysicalArticulationFrame current,
        PhysicalArticulationFrame proposed,
        AvatarKinematicChain chain)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);

        var retargeted = chain switch
        {
            AvatarKinematicChain.Axial => current with
            {
                TrunkPitchRadians = proposed.TrunkPitchRadians,
                TrunkRollRadians = proposed.TrunkRollRadians,
                TrunkYawRadians = proposed.TrunkYawRadians,
                NeckYawRadians = proposed.NeckYawRadians,
                NeckPitchRadians = proposed.NeckPitchRadians,
                Musculoskeletal = proposed.Musculoskeletal,
                SupportPlaneOffsetMeters = proposed.SupportPlaneOffsetMeters
            },
            AvatarKinematicChain.LeftArm => current with
            {
                LeftShoulderAngleRadians = proposed.LeftShoulderAngleRadians,
                LeftShoulderAbductionRadians = proposed.LeftShoulderAbductionRadians,
                LeftElbowAngleRadians = proposed.LeftElbowAngleRadians
            },
            AvatarKinematicChain.RightArm => current with
            {
                RightShoulderAngleRadians = proposed.RightShoulderAngleRadians,
                RightShoulderAbductionRadians = proposed.RightShoulderAbductionRadians,
                RightElbowAngleRadians = proposed.RightElbowAngleRadians
            },
            AvatarKinematicChain.LeftLeg => current with
            {
                LeftHipAngleRadians = proposed.LeftHipAngleRadians,
                LeftHipAbductionRadians = proposed.LeftHipAbductionRadians,
                LeftKneeAngleRadians = proposed.LeftKneeAngleRadians,
                LeftAnkleAngleRadians = proposed.LeftAnkleAngleRadians,
                LeftAnkleRollRadians = proposed.LeftAnkleRollRadians
            },
            AvatarKinematicChain.RightLeg => current with
            {
                RightHipAngleRadians = proposed.RightHipAngleRadians,
                RightHipAbductionRadians = proposed.RightHipAbductionRadians,
                RightKneeAngleRadians = proposed.RightKneeAngleRadians,
                RightAnkleAngleRadians = proposed.RightAnkleAngleRadians,
                RightAnkleRollRadians = proposed.RightAnkleRollRadians
            },
            _ => throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unknown avatar kinematic chain.")
        };
        return retargeted;
    }

    public static PhysicalArticulationFrame InterpolateChain(
        PhysicalArticulationFrame previous,
        PhysicalArticulationFrame proposed,
        float fraction,
        AvatarKinematicChain chain)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(proposed);

        var t = Math.Clamp(fraction, 0f, 1f);
        if (chain == AvatarKinematicChain.Axial)
        {
            var previousBody = previous.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
            var proposedBody = proposed.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
            var body = proposedBody with
            {
                Posture = t <= 0f ? previousBody.Posture : proposedBody.Posture,
                BodyHeightMeters = Lerp(previousBody.BodyHeightMeters, proposedBody.BodyHeightMeters, t),
                UprightFraction = Lerp(previousBody.UprightFraction, proposedBody.UprightFraction, t),
                BalanceError = Lerp(previousBody.BalanceError, proposedBody.BalanceError, t),
                Balance = InterpolateBalance(previousBody.Balance, proposedBody.Balance, t)
            };
            return previous with
            {
                TrunkPitchRadians = Lerp(previous.TrunkPitchRadians, proposed.TrunkPitchRadians, t),
                TrunkRollRadians = Lerp(previous.TrunkRollRadians, proposed.TrunkRollRadians, t),
                TrunkYawRadians = Lerp(previous.TrunkYawRadians, proposed.TrunkYawRadians, t),
                NeckYawRadians = Lerp(previous.NeckYawRadians, proposed.NeckYawRadians, t),
                NeckPitchRadians = Lerp(previous.NeckPitchRadians, proposed.NeckPitchRadians, t),
                Musculoskeletal = body,
                SupportPlaneOffsetMeters = Lerp(
                    previous.SupportPlaneOffsetMeters,
                    proposed.SupportPlaneOffsetMeters,
                    t)
            };
        }

        var interpolated = chain switch
        {
            AvatarKinematicChain.LeftArm => previous with
            {
                LeftShoulderAngleRadians = Lerp(
                    previous.LeftShoulderAngleRadians, proposed.LeftShoulderAngleRadians, t),
                LeftShoulderAbductionRadians = Lerp(
                    previous.LeftShoulderAbductionRadians, proposed.LeftShoulderAbductionRadians, t),
                LeftElbowAngleRadians = Lerp(previous.LeftElbowAngleRadians, proposed.LeftElbowAngleRadians, t)
            },
            AvatarKinematicChain.RightArm => previous with
            {
                RightShoulderAngleRadians = Lerp(
                    previous.RightShoulderAngleRadians, proposed.RightShoulderAngleRadians, t),
                RightShoulderAbductionRadians = Lerp(
                    previous.RightShoulderAbductionRadians, proposed.RightShoulderAbductionRadians, t),
                RightElbowAngleRadians = Lerp(previous.RightElbowAngleRadians, proposed.RightElbowAngleRadians, t)
            },
            AvatarKinematicChain.LeftLeg => previous with
            {
                LeftHipAngleRadians = Lerp(previous.LeftHipAngleRadians, proposed.LeftHipAngleRadians, t),
                LeftHipAbductionRadians = Lerp(
                    previous.LeftHipAbductionRadians, proposed.LeftHipAbductionRadians, t),
                LeftKneeAngleRadians = Lerp(previous.LeftKneeAngleRadians, proposed.LeftKneeAngleRadians, t),
                LeftAnkleAngleRadians = Lerp(previous.LeftAnkleAngleRadians, proposed.LeftAnkleAngleRadians, t),
                LeftAnkleRollRadians = Lerp(
                    previous.LeftAnkleRollRadians, proposed.LeftAnkleRollRadians, t)
            },
            AvatarKinematicChain.RightLeg => previous with
            {
                RightHipAngleRadians = Lerp(previous.RightHipAngleRadians, proposed.RightHipAngleRadians, t),
                RightHipAbductionRadians = Lerp(
                    previous.RightHipAbductionRadians, proposed.RightHipAbductionRadians, t),
                RightKneeAngleRadians = Lerp(previous.RightKneeAngleRadians, proposed.RightKneeAngleRadians, t),
                RightAnkleAngleRadians = Lerp(previous.RightAnkleAngleRadians, proposed.RightAnkleAngleRadians, t),
                RightAnkleRollRadians = Lerp(
                    previous.RightAnkleRollRadians, proposed.RightAnkleRollRadians, t)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unknown avatar kinematic chain.")
        };
        return interpolated;
    }

    private static void AddArm(
        List<AvatarBodyCollider> colliders,
        RigPose thoracic,
        PhysicalArticulationFrame frame,
        bool left)
    {
        var side = left ? -1f : 1f;
        var chain = left ? AvatarKinematicChain.LeftArm : AvatarKinematicChain.RightArm;
        var prefix = left ? "left" : "right";
        var shoulderAngle = left ? frame.LeftShoulderAngleRadians : frame.RightShoulderAngleRadians;
        var shoulderAbduction = left
            ? frame.LeftShoulderAbductionRadians
            : frame.RightShoulderAbductionRadians;
        var elbowAngle = left ? frame.LeftElbowAngleRadians : frame.RightElbowAngleRadians;
        var shoulderRotation = Quaternion.Normalize(
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, side * shoulderAbduction) *
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, -shoulderAngle));
        var shoulder = Child(
            thoracic,
            new Vector3(side * 0.292f, 0.09f, 0f),
            shoulderRotation);
        var elbow = Child(shoulder, new Vector3(0f, -UpperArmLength, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, -elbowAngle));
        var wrist = Child(elbow, new Vector3(0f, -ForearmLength, 0f), Quaternion.Identity);

        colliders.Add(Sphere($"{prefix}_shoulder", chain,
            shoulder, 0.105f, 1.0f, 1_900f));
        colliders.Add(Capsule($"{prefix}_upper_arm", chain,
            AlongNegativeY(shoulder, UpperArmLength * 0.5f), 0.070f, 0.22f, 2.1f, 2_400f));
        colliders.Add(Capsule($"{prefix}_forearm", chain,
            AlongNegativeY(elbow, ForearmLength * 0.5f), 0.058f, 0.21f, 1.4f, 1_800f));
        colliders.Add(Box($"{prefix}_hand", chain,
            AlongNegativeY(wrist, 0.055f), new Vector3(0.12f, 0.17f, 0.11f), 0.55f, 1_100f));
    }

    private static void AddLeg(
        List<AvatarBodyCollider> colliders,
        RigPose pelvis,
        PhysicalArticulationFrame frame,
        bool left)
    {
        var side = left ? -1f : 1f;
        var chain = left ? AvatarKinematicChain.LeftLeg : AvatarKinematicChain.RightLeg;
        var prefix = left ? "left" : "right";
        var hipAngle = left ? frame.LeftHipAngleRadians : frame.RightHipAngleRadians;
        var hipAbduction = left ? frame.LeftHipAbductionRadians : frame.RightHipAbductionRadians;
        var kneeAngle = left ? frame.LeftKneeAngleRadians : frame.RightKneeAngleRadians;
        var ankleAngle = left ? frame.LeftAnkleAngleRadians : frame.RightAnkleAngleRadians;
        var ankleRoll = left ? frame.LeftAnkleRollRadians : frame.RightAnkleRollRadians;
        var hip = Child(
            pelvis,
            new Vector3(side * 0.135f, -0.02f, 0f),
            Quaternion.Normalize(
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, side * hipAbduction) *
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, -hipAngle)));
        var knee = Child(hip, new Vector3(0f, -ThighLength, 0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, kneeAngle));
        var ankle = Child(knee, new Vector3(0f, -ShinLength, 0f),
            Quaternion.Normalize(
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, side * ankleRoll) *
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, ankleAngle)));
        var footCenter = Child(ankle, new Vector3(0f, -0.035f, 0.105f), Quaternion.Identity);

        colliders.Add(Capsule($"{prefix}_thigh", chain,
            AlongNegativeY(hip, ThighLength * 0.5f), 0.092f, 0.25f, 7.6f, 5_600f));
        colliders.Add(Capsule($"{prefix}_shin", chain,
            AlongNegativeY(knee, ShinLength * 0.5f), 0.078f, 0.24f, 4.0f, 4_200f));
        colliders.Add(Box($"{prefix}_foot", chain,
            footCenter, new Vector3(0.18f, 0.11f, 0.31f), 1.0f, 6_200f));
    }

    private static AvatarBodyCollider Sphere(
        string region,
        AvatarKinematicChain chain,
        RigPose pose,
        float radius,
        float mass,
        float area) =>
        new(region, chain, AvatarColliderShape.Sphere, pose.Position, pose.Orientation,
            new Vector3(radius, 0f, 0f), mass, area);

    private static AvatarBodyCollider Capsule(
        string region,
        AvatarKinematicChain chain,
        RigPose pose,
        float radius,
        float internalLength,
        float mass,
        float area) =>
        new(region, chain, AvatarColliderShape.Capsule, pose.Position, pose.Orientation,
            new Vector3(radius, internalLength, 0f), mass, area);

    private static AvatarBodyCollider Box(
        string region,
        AvatarKinematicChain chain,
        RigPose pose,
        Vector3 size,
        float mass,
        float area) =>
        new(region, chain, AvatarColliderShape.Box, pose.Position, pose.Orientation, size, mass, area);

    private static RigPose Between(RigPose first, RigPose second)
    {
        var midpoint = (first.Position + second.Position) * 0.5f;
        return new RigPose(midpoint, first.Orientation);
    }

    private static RigPose AlongNegativeY(RigPose pose, float distance) =>
        new(pose.Position + Vector3.Transform(new Vector3(0f, -distance, 0f), pose.Orientation), pose.Orientation);

    private static RigPose Child(RigPose parent, Vector3 localPosition, Quaternion localOrientation) =>
        new(
            parent.Position + Vector3.Transform(localPosition, parent.Orientation),
            Quaternion.Normalize(parent.Orientation * localOrientation));

    private static float Lerp(float from, float to, float fraction) => from + ((to - from) * fraction);

    private static PhysicalBalanceStateFrame InterpolateBalance(
        PhysicalBalanceStateFrame? previous,
        PhysicalBalanceStateFrame? proposed,
        float fraction)
    {
        var from = previous ?? PhysicalBalanceStateFrame.Neutral;
        var to = proposed ?? PhysicalBalanceStateFrame.Neutral;
        return to with
        {
            FallPitchRadians = Lerp(from.FallPitchRadians, to.FallPitchRadians, fraction),
            FallRollRadians = Lerp(from.FallRollRadians, to.FallRollRadians, fraction),
            FallPitchVelocityRadiansPerSecond = Lerp(
                from.FallPitchVelocityRadiansPerSecond,
                to.FallPitchVelocityRadiansPerSecond,
                fraction),
            FallRollVelocityRadiansPerSecond = Lerp(
                from.FallRollVelocityRadiansPerSecond,
                to.FallRollVelocityRadiansPerSecond,
                fraction),
            Phase = fraction <= 0f ? from.Phase : to.Phase
        };
    }

    public static float LowestSurfaceY(AvatarBodyCollider collider) =>
        collider.Position.Y - VerticalHalfExtent(collider);

    public static Vector3 LowestSurfacePoint(AvatarBodyCollider collider)
    {
        return collider.Shape switch
        {
            AvatarColliderShape.Sphere => collider.Position - (Vector3.UnitY * collider.Size.X),
            AvatarColliderShape.Capsule => CapsuleLowestSurfacePoint(collider),
            AvatarColliderShape.Box => BoxLowestSurfacePoint(collider),
            _ => collider.Position
        };
    }

    private static Vector3 CapsuleLowestSurfacePoint(AvatarBodyCollider collider)
    {
        var axis = Vector3.Transform(Vector3.UnitY, collider.Orientation);
        var endpointDirection = MathF.Abs(axis.Y) < 0.000001f
            ? Vector3.Zero
            : -axis * MathF.Sign(axis.Y);
        return collider.Position +
            (endpointDirection * collider.Size.Y * 0.5f) -
            (Vector3.UnitY * collider.Size.X);
    }

    private static Vector3 BoxLowestSurfacePoint(AvatarBodyCollider collider)
    {
        var localX = Vector3.Transform(Vector3.UnitX, collider.Orientation);
        var localY = Vector3.Transform(Vector3.UnitY, collider.Orientation);
        var localZ = Vector3.Transform(Vector3.UnitZ, collider.Orientation);
        var localSupport = new Vector3(
            LowestLocalCoordinate(localX.Y, collider.Size.X * 0.5f),
            LowestLocalCoordinate(localY.Y, collider.Size.Y * 0.5f),
            LowestLocalCoordinate(localZ.Y, collider.Size.Z * 0.5f));
        return collider.Position + Vector3.Transform(localSupport, collider.Orientation);
    }

    private static float LowestLocalCoordinate(float worldVerticalComponent, float halfExtent) =>
        MathF.Abs(worldVerticalComponent) < 0.000001f
            ? 0f
            : -MathF.Sign(worldVerticalComponent) * halfExtent;

    private static float VerticalHalfExtent(AvatarBodyCollider collider)
    {
        return collider.Shape switch
        {
            AvatarColliderShape.Sphere => collider.Size.X,
            AvatarColliderShape.Capsule => collider.Size.X +
                (MathF.Abs(Vector3.Transform(Vector3.UnitY, collider.Orientation).Y) * collider.Size.Y * 0.5f),
            AvatarColliderShape.Box => BoxVerticalHalfExtent(collider),
            _ => 0f
        };
    }

    private static float BoxVerticalHalfExtent(AvatarBodyCollider collider)
    {
        var localX = Vector3.Transform(Vector3.UnitX, collider.Orientation);
        var localY = Vector3.Transform(Vector3.UnitY, collider.Orientation);
        var localZ = Vector3.Transform(Vector3.UnitZ, collider.Orientation);
        return (MathF.Abs(localX.Y) * collider.Size.X * 0.5f) +
            (MathF.Abs(localY.Y) * collider.Size.Y * 0.5f) +
            (MathF.Abs(localZ.Y) * collider.Size.Z * 0.5f);
    }

    private readonly record struct RigPose(Vector3 Position, Quaternion Orientation);
}
