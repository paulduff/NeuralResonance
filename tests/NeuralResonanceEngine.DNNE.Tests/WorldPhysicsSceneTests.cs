using System.Numerics;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using NRE.WorldSim;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class WorldPhysicsSceneTests
{
    [Fact]
    public void AvatarRigCoversEveryMajorBodySegmentIncludingBothHands()
    {
        var colliders = AvatarColliderRig.Capture(PhysicalArticulationFrame.Neutral);

        Assert.Equal(18, colliders.Count);
        Assert.Contains(colliders, collider => collider.Region == "head");
        Assert.Contains(colliders, collider => collider.Region == "chest");
        Assert.Contains(colliders, collider => collider.Region == "pelvis");
        Assert.Contains(colliders, collider => collider.Region == "left_hand");
        Assert.Contains(colliders, collider => collider.Region == "right_hand");
        Assert.Contains(colliders, collider => collider.Region == "left_shoulder");
        Assert.Contains(colliders, collider => collider.Region == "right_shoulder");
        Assert.Contains(colliders, collider => collider.Region == "left_foot");
        Assert.Contains(colliders, collider => collider.Region == "right_foot");
        Assert.All(colliders, collider => Assert.True(collider.ContactAreaSquareMillimeters > 0f));
    }

    [Fact]
    public void SeatedArticulationKeepsEverySkinColliderAboveTheGroundPlane()
    {
        var body = new AvatarArticulatedBody();
        for (var step = 0; step < 120; step++)
        {
            body.Advance(0.025, 0.0, 0.0, 0.0, 0.0, 0.0, true, false, sitDrive: 1.0);
        }

        var frame = body.CaptureFrame();
        var lowestSurface = AvatarColliderRig.Capture(frame)
            .Min(collider => collider.Position.Y - VerticalHalfExtent(collider));

        Assert.True(frame.SupportPlaneOffsetMeters > 0.10f);
        Assert.InRange(lowestSurface, -0.0305f, -0.0295f);
    }

    [Fact]
    public void ArmPoseCannotRedefineTheBodySupportPlane()
    {
        var accepted = AvatarColliderRig.WithComputedSupportPlaneOffset(PhysicalArticulationFrame.Neutral);
        var armsLowered = AvatarColliderRig.WithComputedSupportPlaneOffset(
            PhysicalArticulationFrame.Neutral with
            {
                LeftShoulderAngleRadians = -0.70f,
                RightShoulderAngleRadians = -0.70f,
                LeftElbowAngleRadians = 2.62f,
                RightElbowAngleRadians = 2.62f
            });

        Assert.Equal(accepted.SupportPlaneOffsetMeters, armsLowered.SupportPlaneOffsetMeters, 5);
    }

    [Fact]
    public void RetargetingAnArmPreservesTheAcceptedSupportPlane()
    {
        var accepted = PhysicalArticulationFrame.Neutral with { SupportPlaneOffsetMeters = 0.17f };
        var proposed = PhysicalArticulationFrame.Neutral with
        {
            LeftShoulderAngleRadians = 1.40f,
            SupportPlaneOffsetMeters = 0.62f
        };

        var retargeted = AvatarColliderRig.RetargetChain(
            accepted,
            proposed,
            AvatarKinematicChain.LeftArm);

        Assert.Equal(proposed.LeftShoulderAngleRadians, retargeted.LeftShoulderAngleRadians);
        Assert.Equal(accepted.SupportPlaneOffsetMeters, retargeted.SupportPlaneOffsetMeters);
    }

    [Fact]
    public void ContinuousSweepStopsWholeBodyBeforeCrossingAThinWall()
    {
        using var scene = CreateWallScene(new Vector3(0f, 1f, 1f), new Vector3(4f, 4f, 0.04f));

        var result = scene.ResolveAvatar(
            Vector3.Zero,
            0f,
            PhysicalArticulationFrame.Neutral,
            new Vector3(0f, 0f, 2f),
            0f,
            PhysicalArticulationFrame.Neutral,
            0.025f);

        Assert.True(result.Blocked);
        Assert.InRange(result.RootPosition.Z, 0f, 0.80f);
        Assert.NotEmpty(result.Contacts);
        Assert.All(result.Contacts, contact => Assert.True(contact.BodyNormal.Z < -0.8f));
    }

    [Fact]
    public void ObliqueRootMotionSlidesAlongAWallInsteadOfFreezingTheWholePose()
    {
        using var scene = CreateWallScene(new Vector3(0f, 1f, 1f), new Vector3(4f, 4f, 0.04f));

        var result = scene.ResolveAvatar(
            new Vector3(-0.8f, 0f, 0f),
            0f,
            PhysicalArticulationFrame.Neutral,
            new Vector3(0.8f, 0f, 2f),
            0f,
            PhysicalArticulationFrame.Neutral,
            0.05f);

        Assert.True(result.RootMotionConstrained);
        Assert.True(result.RootPosition.X > 0.65f);
        Assert.InRange(result.RootPosition.Z, 0f, 0.80f);
        Assert.NotEmpty(result.Contacts);
        Assert.Equal(
            result.Contacts.Count,
            result.Contacts.Select(contact => contact.InputSource).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ContactOnOneArmDoesNotFreezeTheOppositeArm()
    {
        var proposed = PhysicalArticulationFrame.Neutral with
        {
            LeftShoulderAngleRadians = 1.40f,
            RightShoulderAngleRadians = 0.55f,
            ManipulatorExtensionFraction = 1f
        };
        var proposedHand = AvatarColliderRig.Capture(proposed)
            .Single(collider => collider.Region == "left_hand");
        using var scene = CreateWallScene(proposedHand.Position, new Vector3(0.16f, 0.16f, 0.05f));

        var result = scene.ResolveAvatar(
            Vector3.Zero,
            0f,
            PhysicalArticulationFrame.Neutral,
            Vector3.Zero,
            0f,
            proposed,
            0.05f);

        Assert.True(result.Articulation.LeftShoulderAngleRadians < proposed.LeftShoulderAngleRadians);
        Assert.Equal(proposed.RightShoulderAngleRadians, result.Articulation.RightShoulderAngleRadians, 4);
        Assert.Contains(AvatarKinematicChain.LeftArm, result.ConstrainedChains);
        Assert.DoesNotContain(AvatarKinematicChain.RightArm, result.ConstrainedChains);
    }

    [Fact]
    public void LimbCanWithdrawFromAnAcceptedContactPose()
    {
        var extended = PhysicalArticulationFrame.Neutral with
        {
            LeftShoulderAngleRadians = 1.40f,
            ManipulatorExtensionFraction = 1f
        };
        var proposedHand = AvatarColliderRig.Capture(extended)
            .Single(collider => collider.Region == "left_hand");
        using var scene = CreateWallScene(proposedHand.Position, new Vector3(0.16f, 0.16f, 0.05f));
        var contact = scene.ResolveAvatar(
            Vector3.Zero, 0f, PhysicalArticulationFrame.Neutral,
            Vector3.Zero, 0f, extended, 0.05f);

        var withdrawal = scene.ResolveAvatar(
            contact.RootPosition, contact.HeadingDegrees, contact.Articulation,
            contact.RootPosition, contact.HeadingDegrees, PhysicalArticulationFrame.Neutral, 0.05f);

        Assert.True(withdrawal.Articulation.LeftShoulderAngleRadians <
                    contact.Articulation.LeftShoulderAngleRadians);
        Assert.Equal(0f, withdrawal.Articulation.LeftShoulderAngleRadians, 4);
        Assert.DoesNotContain(AvatarKinematicChain.LeftArm, withdrawal.ConstrainedChains);
    }

    [Fact]
    public void RootCanRetreatAfterContinuousSweepContact()
    {
        using var scene = CreateWallScene(new Vector3(0f, 1f, 1f), new Vector3(4f, 4f, 0.04f));
        var contact = scene.ResolveAvatar(
            Vector3.Zero, 0f, PhysicalArticulationFrame.Neutral,
            new Vector3(0f, 0f, 2f), 0f, PhysicalArticulationFrame.Neutral, 0.05f);

        var retreat = scene.ResolveAvatar(
            contact.RootPosition, contact.HeadingDegrees, contact.Articulation,
            new Vector3(0f, 0f, -0.5f), 0f, PhysicalArticulationFrame.Neutral, 0.05f);

        Assert.False(retreat.RootMotionConstrained);
        Assert.Equal(-0.5f, retreat.RootPosition.Z, 3);
    }

    [Fact]
    public void PerpendicularWallsResolveAsACornerWithoutPenetration()
    {
        using var scene = new WorldPhysicsScene(
        [
            new WorldPhysicsBox("wall_z", new Vector3(0f, 1f, 1f),
                new Vector3(4f, 4f, 0.04f), Quaternion.Identity),
            new WorldPhysicsBox("wall_x", new Vector3(1f, 1f, 0f),
                new Vector3(0.04f, 4f, 4f), Quaternion.Identity)
        ]);

        var result = scene.ResolveAvatar(
            Vector3.Zero, 0f, PhysicalArticulationFrame.Neutral,
            new Vector3(2f, 0f, 2f), 0f, PhysicalArticulationFrame.Neutral, 0.05f);

        Assert.True(result.RootMotionConstrained);
        Assert.InRange(result.RootPosition.X, 0f, 0.80f);
        Assert.InRange(result.RootPosition.Z, 0f, 0.80f);
        Assert.True(float.IsFinite(result.RootPosition.X));
        Assert.True(float.IsFinite(result.RootPosition.Z));
    }

    [Fact]
    public void ArticulatedArmCannotEnterAWallWhileTheRootIsStationary()
    {
        var proposed = PhysicalArticulationFrame.Neutral with
        {
            LeftShoulderAngleRadians = 1.40f,
            ManipulatorExtensionFraction = 1f
        };
        var proposedHand = AvatarColliderRig.Capture(proposed)
            .Single(collider => collider.Region == "left_hand");
        using var scene = CreateWallScene(proposedHand.Position, new Vector3(0.16f, 0.16f, 0.05f));

        var result = scene.ResolveAvatar(
            Vector3.Zero,
            0f,
            PhysicalArticulationFrame.Neutral,
            Vector3.Zero,
            0f,
            proposed,
            0.05f);

        Assert.True(result.Blocked);
        Assert.False(result.RootMotionConstrained);
        Assert.Equal(Vector3.Zero, result.RootPosition);
        Assert.True(result.Articulation.LeftShoulderAngleRadians < proposed.LeftShoulderAngleRadians);
        Assert.Contains(result.Contacts, contact => contact.Chain == AvatarKinematicChain.LeftArm);
        Assert.All(result.Contacts, contact => Assert.StartsWith("avatar_world_left_", contact.InputSource));
    }

    [Fact]
    public void GreaterMuscleEffortProducesGreaterLocalContactForce()
    {
        var unforced = PhysicalArticulationFrame.Neutral with
        {
            LeftShoulderAngleRadians = 1.40f,
            ManipulatorExtensionFraction = 1f
        };
        var proposedHand = AvatarColliderRig.Capture(unforced)
            .Single(collider => collider.Region == "left_hand");
        using var scene = CreateWallScene(proposedHand.Position, new Vector3(0.16f, 0.16f, 0.05f));
        var forceful = unforced with
        {
            Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            {
                Muscles =
                [
                    new PhysicalMuscleMeasurement(
                        "AnteriorDeltoid", "L", 1f, 1_000f, 1f, 0f, 0f)
                ]
            }
        };

        var low = scene.ResolveAvatar(
            Vector3.Zero, 0f, PhysicalArticulationFrame.Neutral,
            Vector3.Zero, 0f, unforced, 0.05f);
        var high = scene.ResolveAvatar(
            Vector3.Zero, 0f, PhysicalArticulationFrame.Neutral,
            Vector3.Zero, 0f, forceful, 0.05f);

        Assert.NotEmpty(low.Contacts);
        Assert.NotEmpty(high.Contacts);
        Assert.True(high.Contacts.Max(contact => contact.ForceNewtons) >
                    low.Contacts.Max(contact => contact.ForceNewtons) + 300f);
    }

    private static WorldPhysicsScene CreateWallScene(Vector3 center, Vector3 size) =>
        new(
        [
            new WorldPhysicsBox("test_wall", center, size, Quaternion.Identity)
        ]);

    private static float VerticalHalfExtent(AvatarBodyCollider collider)
    {
        if (collider.Shape == AvatarColliderShape.Sphere)
        {
            return collider.Size.X;
        }

        var localX = Vector3.Transform(Vector3.UnitX, collider.Orientation);
        var localY = Vector3.Transform(Vector3.UnitY, collider.Orientation);
        var localZ = Vector3.Transform(Vector3.UnitZ, collider.Orientation);
        return collider.Shape == AvatarColliderShape.Capsule
            ? collider.Size.X + (MathF.Abs(localY.Y) * collider.Size.Y * 0.5f)
            : (MathF.Abs(localX.Y) * collider.Size.X * 0.5f) +
              (MathF.Abs(localY.Y) * collider.Size.Y * 0.5f) +
              (MathF.Abs(localZ.Y) * collider.Size.Z * 0.5f);
    }
}
