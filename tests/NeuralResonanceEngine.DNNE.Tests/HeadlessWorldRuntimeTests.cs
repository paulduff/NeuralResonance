using NRE.WorldSim;
using NRE.SimAvatar;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HeadlessWorldRuntimeTests
{
    [Fact]
    public void FallbackContactImpulseUsesOnlyTheCurrentBodyFrame()
    {
        var impulse = HeadlessWorldRuntime.CalculateFrameImpulseNewtonSeconds(
            forceNewtons: 450f,
            frameInterval: TimeSpan.FromMilliseconds(25));

        Assert.Equal(11.25f, impulse, 3);
        Assert.Equal(0f, HeadlessWorldRuntime.CalculateFrameImpulseNewtonSeconds(
            float.NaN,
            TimeSpan.FromMilliseconds(25)));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(24.0, 0.5)]
    [InlineData(-48.0, -1.0)]
    [InlineData(240.0, 1.0)]
    public void MotorRecruitmentUsesThePhysiologicalPopulationEnvelope(
        double accumulatedDrive,
        double expectedRecruitment)
    {
        Assert.Equal(
            expectedRecruitment,
            HeadlessWorldRuntime.NormalizeMotorRecruitment(accumulatedDrive),
            precision: 6);
    }

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
            where terrain.HeightAtCell(x, z) < WorldTerrain.SeaLevelHeightUnits
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
        Assert.Equal(TimeSpan.FromSeconds(30), options.EffectiveRollingReportInterval);
        Assert.Equal(TimeSpan.FromSeconds(4), options.EffectiveBrainFrameOverloadThreshold);
        Assert.Equal(3, options.ConsecutiveBrainFrameOverloadLimit);
        Assert.False(options.MotorTrainingMode);
        options.Validate();
    }

    [Fact]
    public void PascalCasedBrainAuthorityHistoryIsReadFromControlFrameState()
    {
        var expected = new ActionAuthorityCumulativeTelemetry(
            Samples: 120,
            CircuitObservedTicks: 118,
            AuthorityGrantedTicks: 9,
            AuthorityGrantEpisodes: 2,
            FirstAuthorityGrantTick: 31,
            LastAuthorityGrantTick: 104,
            Channels: []);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            ActionAuthorityHistory = expected
        });
        using var document = System.Text.Json.JsonDocument.Parse(json);

        var actual = HeadlessWorldRuntime.ReadActionAuthorityHistory(document.RootElement);

        Assert.NotNull(actual);
        Assert.Equal(expected.Samples, actual.Samples);
        Assert.Equal(expected.AuthorityGrantedTicks, actual.AuthorityGrantedTicks);
        Assert.Equal(expected.AuthorityGrantEpisodes, actual.AuthorityGrantEpisodes);
    }

    [Fact]
    public async Task RunningWorldLeavesAnAtomicRollingReportBeforeGracefulShutdown()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dnne-world-rolling-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
                new Uri("http://127.0.0.1:1"),
                SimulationInterval: TimeSpan.FromMilliseconds(10),
                FramePollInterval: TimeSpan.FromSeconds(1),
                BodyFrameInterval: TimeSpan.FromSeconds(1),
                VisionFrameInterval: TimeSpan.FromSeconds(1),
                AudioFrameInterval: TimeSpan.FromSeconds(1),
                ReportDirectory: directory,
                RollingReportInterval: TimeSpan.FromMilliseconds(100)));
            runtime.Start();

            var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
            while (runtime.LastRollingReportPath is null && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.NotNull(runtime.LastRollingReportPath);
            Assert.Null(runtime.LastRollingReportError);
            Assert.True(File.Exists(runtime.LastRollingReportPath));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            var report = System.Text.Json.JsonSerializer.Deserialize<WorldRunReport>(
                await File.ReadAllTextAsync(runtime.LastRollingReportPath!));
            Assert.NotNull(report);
            Assert.Equal("rolling-heartbeat", report.Reason);
            Assert.True(report.Snapshot.WorldTick > 0);
            Assert.EndsWith("-rolling.json", runtime.LastRollingReportPath, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OnlySustainedBrainFrameLatencySafetyPausesPhysicalTime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dnne-world-overload-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
                new Uri("http://127.0.0.1:1"),
                ReportDirectory: directory,
                BrainFrameOverloadThreshold: TimeSpan.FromMilliseconds(100),
                ConsecutiveBrainFrameOverloadLimit: 3));

            Assert.False(runtime.ObserveBrainFrameLatency(TimeSpan.FromMilliseconds(150), frameSucceeded: true));
            Assert.True(runtime.GetSnapshot().Running);
            Assert.Equal(1, runtime.GetSnapshot().ConsecutiveSlowBrainFrames);

            Assert.False(runtime.ObserveBrainFrameLatency(TimeSpan.FromMilliseconds(20), frameSucceeded: false));
            Assert.Equal(0, runtime.GetSnapshot().ConsecutiveSlowBrainFrames);

            Assert.False(runtime.ObserveBrainFrameLatency(TimeSpan.FromMilliseconds(110), frameSucceeded: false));
            Assert.False(runtime.ObserveBrainFrameLatency(TimeSpan.FromMilliseconds(120), frameSucceeded: true));
            Assert.True(runtime.ObserveBrainFrameLatency(TimeSpan.FromMilliseconds(130), frameSucceeded: true));

            var paused = runtime.GetSnapshot();
            Assert.False(paused.Running);
            Assert.Equal(3, paused.ConsecutiveSlowBrainFrames);
            Assert.Equal(1, paused.BrainFrameOverloadSafetyPauses);
            Assert.Contains("3 consecutive", paused.LastBrainFrameOverloadReason, StringComparison.Ordinal);
            Assert.Contains("safety pause", paused.LastInteractionOutcome, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(runtime.LastRunReportPath);
            Assert.True(File.Exists(runtime.LastRunReportPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(WorldDevelopmentStage.HandSpace, 1, 0)]
    [InlineData(WorldDevelopmentStage.NearFlat, 1, 0)]
    [InlineData(WorldDevelopmentStage.NormalDistance, 4, 0)]
    [InlineData(WorldDevelopmentStage.Terrain, 12, 5)]
    [InlineData(WorldDevelopmentStage.Ecology, 12, 5)]
    public async Task DevelopmentStageControlsForagingDistanceAndComplexity(
        WorldDevelopmentStage stage,
        int expectedFood,
        int expectedDevices)
    {
        await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
            new Uri("http://127.0.0.1:1"),
            DevelopmentStage: stage));

        var snapshot = runtime.GetSnapshot();

        Assert.Equal(stage.ToString(), snapshot.DevelopmentStage);
        Assert.Equal(expectedFood, snapshot.FoodPickups.Count);
        Assert.Equal(expectedDevices, snapshot.WeaponPickups.Count);
        Assert.Empty(snapshot.Predators);
        if (stage == WorldDevelopmentStage.HandSpace)
        {
            var target = Assert.Single(snapshot.FoodPickups);
            var distance = Math.Sqrt(
                Math.Pow(target.X - snapshot.AvatarX, 2) +
                Math.Pow(target.Z - snapshot.AvatarZ, 2));
            Assert.InRange(distance, 0.70, 0.78);
            Assert.True(target.Y > snapshot.AvatarY + 0.75);
        }
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
    public async Task PredatorsRequireAnExplicitEcologyStageOverride()
    {
        await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
            new Uri("http://127.0.0.1:1"),
            PredatorsEnabled: true,
            DevelopmentStage: WorldDevelopmentStage.Ecology));

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
    public void RightingPostureCanUseBoundedNeuronalPropulsionToRepositionSupport()
    {
        var state = AvatarPlanarDynamics.Advance(
            default,
            requestedForwardSpeed: 1.8,
            requestedTurnRate: 120.0,
            posture: "righting",
            grounded: true,
            deltaSeconds: 0.25);

        Assert.InRange(state.ForwardVelocityMetersPerSecond, 0.001, 0.612);
        Assert.InRange(state.TurnVelocityDegreesPerSecond, 0.001, 40.81);
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
