using NRE.WorldSim;
using NRE.SimAvatar;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HeadlessWorldRuntimeTests
{
    [Fact]
    public void TerrainGenerationIsDeterministicAndMatchesWorldBounds()
    {
        var first = new WorldTerrain(317);
        var second = new WorldTerrain(317);

        Assert.Equal(WorldTerrain.Size, 132);
        Assert.Equal(first.ExplorableCellCount, second.ExplorableCellCount);
        Assert.Equal(first.StaticObstacleCount, second.StaticObstacleCount);
        Assert.True(first.StaticObstacleCount > 100);
        Assert.Equal(12, first.ShelterSites.Count);
        Assert.Equal(first.SurfaceAt(0, 6), second.SurfaceAt(0, 6));
        Assert.Equal(first.SurfaceAt(-42.5, 31.25), second.SurfaceAt(-42.5, 31.25));
        Assert.True(first.IsInside(65.5, -65.5));
        Assert.False(first.IsInside(66.0, 0.0));
    }

    [Fact]
    public void WaterCellsAreNotWalkableTerrain()
    {
        var terrain = new WorldTerrain(317);
        var half = (WorldTerrain.Size - 1) * 0.5;
        var waterCell = (
            from x in Enumerable.Range(0, WorldTerrain.Size)
            from z in Enumerable.Range(0, WorldTerrain.Size)
            where terrain.HeightAtCell(x, z) < WorldTerrain.SeaLevel
            select (X: x - half, Z: z - half)).First();

        Assert.True(terrain.IsInside(waterCell.X, waterCell.Z));
        Assert.True(terrain.IsWater(waterCell.X, waterCell.Z));
        Assert.False(terrain.IsWalkable(waterCell.X, waterCell.Z));
        Assert.True(terrain.IsWalkable(0.0, 6.0));
    }

    [Fact]
    public void EveryShelterHasAFlatClearFoundationAndEntranceApproach()
    {
        var terrain = new WorldTerrain(317);

        foreach (var site in terrain.ShelterSites)
        {
            var surface = terrain.SurfaceAt(site.X, site.Z);
            var foundationOffset = 3.25 * site.Scale;
            var entranceZ = site.Z + (6.0 * site.Scale);

            Assert.Equal(surface, terrain.SurfaceAt(site.X + foundationOffset, site.Z));
            Assert.Equal(surface, terrain.SurfaceAt(site.X - foundationOffset, site.Z));
            Assert.Equal(surface, terrain.SurfaceAt(site.X, site.Z + foundationOffset));
            Assert.Equal(surface, terrain.SurfaceAt(site.X, site.Z - foundationOffset));
            Assert.Equal(surface, terrain.SurfaceAt(site.X, entranceZ));
            Assert.False(terrain.IsWater(site.X, site.Z));
            Assert.True(terrain.IsInsideShelterClearance(site.X, site.Z));
            Assert.True(terrain.IsInsideShelterClearance(site.X, entranceZ));
            Assert.False(terrain.CollidesWithStaticObstacle(site.X, site.Z, radius: 0.34));
            Assert.False(terrain.CollidesWithStaticObstacle(site.X, entranceZ, radius: 0.34));
        }
    }

    [Fact]
    public void BinocularEyePoseUsesAdultInterpupillarySpacingAndParallelRestingAxes()
    {
        var pose = AvatarBinocularVision.ComputeEyePose(4.0, -2.0, 37.0);
        var separation = Math.Sqrt(
            Math.Pow(pose.Right.X - pose.Left.X, 2.0) +
            Math.Pow(pose.Right.Z - pose.Left.Z, 2.0));

        Assert.Equal(0.064, separation, precision: 10);
        Assert.Equal(pose.Left.HeadingDegrees, pose.Right.HeadingDegrees);
        Assert.Equal(37.0, pose.Left.HeadingDegrees);
    }

    [Fact]
    public void FastSomaticAndBinocularSamplingDefaultsRemainLaptopPractical()
    {
        var options = new HeadlessWorldOptions(new Uri("http://127.0.0.1:5080"));

        Assert.Equal(TimeSpan.FromMilliseconds(50), options.EffectiveBodyFrameInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(125), options.EffectiveVisionFrameInterval);
        options.Validate();
    }

    [Fact]
    public async Task RuntimeOwnsWorldTimeAndSupportsPauseResumeReset()
    {
        await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
            new Uri("http://127.0.0.1:1"),
            SimulationInterval: TimeSpan.FromMilliseconds(15),
            FramePollInterval: TimeSpan.FromMilliseconds(100),
            BodyFrameInterval: TimeSpan.FromSeconds(1),
            VisionFrameInterval: TimeSpan.FromSeconds(1),
            AudioFrameInterval: TimeSpan.FromSeconds(1)));
        runtime.Start();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        var live = runtime.GetSnapshot();
        while (live.WorldTick == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
            live = runtime.GetSnapshot();
        }

        Assert.True(live.Running);
        Assert.True(live.WorldTick > 0);
        Assert.Equal(12, live.Shelters.Count);
        Assert.Equal(12, live.FoodPickups.Count);
        Assert.Equal(5, live.WeaponPickups.Count);
        Assert.Empty(live.Predators);
        Assert.True(live.PredatorsSuspended);
        Assert.True(live.AvatarGrounded);
        Assert.Equal(0, live.SomaticFramesRejected);
        Assert.Equal(0, live.PhysicalBodyFramesRejected);
        Assert.Equal(0, live.BodyInputFailures);
        Assert.Equal("none", live.LastBodyInputError);
        Assert.InRange(
            live.Articulation.LeftFootLoadNewtons + live.Articulation.RightFootLoadNewtons,
            719f,
            721f);
        Assert.Equal(0.0, live.AvatarVerticalVelocity);
        Assert.InRange(Math.Abs(live.AvatarForwardSpeed), 0.0, 1.8);
        var terrain = new WorldTerrain(live.Seed);
        Assert.All(live.FoodPickups, item => Assert.False(terrain.IsInsideShelterClearance(item.X, item.Z)));
        Assert.All(live.WeaponPickups, item => Assert.False(terrain.IsInsideShelterClearance(item.X, item.Z)));

        runtime.Pause();
        var pausedTick = runtime.GetSnapshot().WorldTick;
        await Task.Delay(70);
        var paused = runtime.GetSnapshot();
        Assert.False(paused.Running);
        Assert.Equal(pausedTick, paused.WorldTick);

        runtime.Resume();
        await Task.Delay(70);
        Assert.True(runtime.GetSnapshot().WorldTick > pausedTick);

        var reset = runtime.Reset(411);
        Assert.Equal(411, reset.Seed);
        Assert.Equal(0, reset.WorldTick);
        Assert.Equal("world reset", reset.LastInteractionOutcome);
        Assert.Equal(0f, reset.Articulation.LeftHipAngleRadians);
        Assert.Equal("standing", reset.Articulation.Musculoskeletal?.Posture);
        Assert.NotEmpty(reset.Articulation.Musculoskeletal?.Muscles ?? []);
    }

    [Fact]
    public async Task PredatorsRequireAnExplicitTrainingOverride()
    {
        await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
            new Uri("http://127.0.0.1:1"),
            PredatorsEnabled: true));

        var snapshot = runtime.GetSnapshot();

        Assert.False(snapshot.PredatorsSuspended);
        Assert.Equal(3, snapshot.PredatorsActive);
        Assert.Equal(3, snapshot.Predators.Count);
    }

    [Fact]
    public void LyingDownPreservesMomentumWhichThenDecaysThroughGroundFriction()
    {
        var moving = new AvatarPlanarMotionState(1.4, 48.0);

        var firstLyingStep = AvatarPlanarDynamics.Advance(
            moving,
            requestedForwardSpeed: 1.8,
            requestedTurnRate: 120.0,
            posture: "lying",
            grounded: true,
            deltaSeconds: 0.05);

        Assert.InRange(firstLyingStep.ForwardVelocityMetersPerSecond, 1.0, 1.399);
        Assert.InRange(firstLyingStep.TurnVelocityDegreesPerSecond, 0.1, 47.9);

        var state = firstLyingStep;
        for (var step = 0; step < 30; step++)
        {
            var next = AvatarPlanarDynamics.Advance(state, 1.8, 120.0, "lying", true, 0.05);
            Assert.InRange(next.ForwardVelocityMetersPerSecond, 0.0, state.ForwardVelocityMetersPerSecond);
            state = next;
        }

        Assert.Equal(0.0, state.ForwardVelocityMetersPerSecond);
        Assert.Equal(0.0, state.TurnVelocityDegreesPerSecond);
    }

    [Fact]
    public void NonPropulsivePosturesCannotCreateMotionFromRest()
    {
        foreach (var posture in new[] { "sitting", "lying" })
        {
            var state = AvatarPlanarDynamics.Advance(
                default,
                requestedForwardSpeed: 1.8,
                requestedTurnRate: 120.0,
                posture,
                grounded: true,
                deltaSeconds: 0.25);

            Assert.Equal(default, state);
        }
    }

    [Fact]
    public void AirborneBodyRetainsHorizontalMomentumWithoutAirPropulsion()
    {
        var state = AvatarPlanarDynamics.Advance(
            new AvatarPlanarMotionState(1.2, 30.0),
            requestedForwardSpeed: -0.65,
            requestedTurnRate: -120.0,
            posture: "standing",
            grounded: false,
            deltaSeconds: 0.10);

        Assert.InRange(state.ForwardVelocityMetersPerSecond, 1.18, 1.2);
        Assert.InRange(state.TurnVelocityDegreesPerSecond, 29.8, 30.0);
    }
}
