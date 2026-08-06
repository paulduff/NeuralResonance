using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarServiceTests
{
    [Fact]
    public void ServiceProcessesNeuronalMotorSignalsOnWorker()
    {
        using var service = CreateService();

        service.PostBrainSignals(MotorDispatches());

        var signal = WaitForSignal(service, static item => item.MotorEvents == 2);
        Assert.True(signal.LeftMotorDrive > 0.0);
        Assert.True(signal.RightMotorDrive > 0.0);
        Assert.True(service.ProcessedCommands > 0);
    }

    [Fact]
    public void ServiceClockAppliesPhysicalDriveDecay()
    {
        using var service = CreateService(new AvatarServiceClockOptions(
            Enabled: true,
            TickIntervalMs: 20,
            DriveDecayOverride: 0.5));
        service.PostBrainSignals(MotorDispatches());
        var driven = WaitForSignal(service, static item => item.MotorEvents == 2);

        var decayed = WaitForSignal(service, item =>
            item.MotorEvents == driven.MotorEvents &&
            item.LeftMotorDrive < driven.LeftMotorDrive &&
            item.RightMotorDrive < driven.RightMotorDrive);

        Assert.True(decayed.LeftMotorDrive <= driven.LeftMotorDrive * 0.5);
        Assert.True(decayed.RightMotorDrive <= driven.RightMotorDrive * 0.5);
        Assert.True(service.ClockTicks > 0);
    }

    [Fact]
    public void ActionOutputIsUnmodifiedNeuronalKinematics()
    {
        using var service = CreateService();
        service.PostBrainSignals(MotorDispatches());
        var signal = WaitForSignal(service, static item => item.MotorEvents == 2);

        var output = service.PublishActionOutput(forwardGain: 1.5, turnGain: 0.75);
        var expected = AvatarKinematics.ComputeBrainMotorOutput(
            signal.LeftMotorDrive,
            signal.RightMotorDrive,
            TestKinematics,
            forwardGain: 1.5,
            turnGain: 0.75);

        Assert.Equal(expected.ForwardSpeed, output.Movement.ForwardSpeed, 12);
        Assert.Equal(expected.TurnRateDeg, output.Movement.TurnRateDeg, 12);
        Assert.Equal("avatar_action", output.OutputSource);
    }

    [Fact]
    public void BodyPacketsCannotSteerMotorOutput()
    {
        using var service = CreateService();
        service.PostBrainSignals(MotorDispatches());
        var before = WaitForSignal(service, static item => item.MotorEvents == 2);

        service.PostBodyInput(
            new AvatarBodyTelemetry(
                ForwardVelocity: 0.0,
                TurnRateDeg: 0.0,
                ContactLevel: 1.0,
                LeftMotorDrive: 0.0,
                RightMotorDrive: 0.0,
                PainLevel: 1.0),
            CreateBodyStateProfile());

        WaitUntil(() => service.ProcessedCommands >= 2);
        var after = service.LatestSignal;
        Assert.Equal(before.LeftMotorDrive, after.LeftMotorDrive);
        Assert.Equal(before.RightMotorDrive, after.RightMotorDrive);
        Assert.Equal(
            service.ComputeMotorOutput(),
            service.PublishActionOutput().Movement);
    }

    [Fact]
    public void AuditoryTransportDoesNotChooseByIntensity()
    {
        using var service = CreateService();
        var cues = new[]
        {
            new AvatarAuditoryCue("first_quiet", 0.1f, 4, Hemisphere: "L"),
            new AvatarAuditoryCue("second_loud", 1.0f, 32, Hemisphere: "R")
        };

        service.PostAuditoryInputCandidates(cues, maxCues: 1);

        var cue = WaitForAuditoryInput(service);
        Assert.Equal("first_quiet", cue.Pattern);
    }

    [Fact]
    public void ObjectTransportDoesNotChooseBySalience()
    {
        using var service = CreateService();
        var observations = new[]
        {
            new AvatarObjectObservation("first", "neutral", 0.1, 0.2, 0.1, 2, 4.0, "L"),
            new AvatarObjectObservation("second", "threat", 1.0, 1.0, 1.0, 32, 0.5, "R")
        };

        service.PostObjectCandidates(observations, maxObservations: 1);

        var observation = WaitForObjectObservation(service);
        Assert.Equal("first", observation.ObjectId);
    }

    [Fact]
    public void ServiceTransportsBodyPacketsUnchanged()
    {
        using var service = CreateService();
        var telemetry = new AvatarBodyTelemetry(
            ForwardVelocity: 1.2,
            TurnRateDeg: 0.4,
            ContactLevel: 0.2,
            LeftMotorDrive: 12.0,
            RightMotorDrive: 14.0);
        var profile = CreateBodyStateProfile();

        service.PostBodyInput(telemetry, profile);

        Assert.Equal(new AvatarBodyStateInput(telemetry, profile), WaitForBodyInput(service));
    }

    [Fact]
    public void ServiceCoalescesSightFramesAndBoundsPublishedOutput()
    {
        using var service = CreateService();
        for (var generation = 1; generation <= 160; generation++)
        {
            service.PostSightInputFrame(new AvatarSightFrame(
                Generation: generation,
                CaptureTimestampMs: generation,
                Width: 1,
                Height: 1,
                Stride: 4,
                Pixels: [1, 2, 3, 255],
                PreviewHeadingDeg: 0.0));
        }

        var latest = WaitForSightOutput(service, static frame => frame.Generation == 160);
        Assert.Equal(160, latest.Generation);
        Assert.InRange(service.PendingCommandCount, 0, 64);
        Assert.InRange(service.PublishedSightOutputCount, 0, 3);
    }

    [Fact]
    public void ServiceRejectsMalformedSightFramesBeforeWorker()
    {
        using var service = CreateService();
        var malformed = new AvatarSightFrame(
            Generation: 1,
            CaptureTimestampMs: 1,
            Width: 1,
            Height: 1,
            Stride: 3,
            Pixels: [1, 2, 3],
            PreviewHeadingDeg: 0.0);

        Assert.Throws<ArgumentException>(() => service.PostSightInputFrame(malformed));
    }

    [Fact]
    public void AvatarAssemblyContainsNoScalarCognitionOrMotorInjectionApi()
    {
        var assembly = typeof(AvatarService).Assembly;
        string[] removedTypes =
        [
            "AvatarAttentionOutput",
            "AvatarAffectiveWeather",
            "AvatarArousalOutput",
            "AvatarBodySoundOutput",
            "AvatarGestureOutput",
            "AvatarNeedsRhythmState",
            "AvatarReflexOutput",
            "AvatarSensationMemory",
            "AvatarPlaceMemory",
            "AvatarSelfDiagnostics",
            "AvatarNervousSystemBodyState",
            "AvatarToolSignal"
        ];

        Assert.All(removedTypes, name => Assert.Null(assembly.GetType($"NRE.SimAvatar.{name}")));
        Assert.Null(typeof(AvatarService).GetMethod("PostAddMotorDrive"));
        Assert.Null(typeof(AvatarService).GetMethod("PostSetMotorDrive"));
        Assert.Null(typeof(AvatarService).GetMethod("PostPlaceObservations"));
        Assert.Null(typeof(AvatarService).GetProperty("CurrentSelfDiagnostics"));
        Assert.Equal(
            ["Movement", "EmittedUnixMs", "OutputSource"],
            typeof(AvatarActionOutput).GetProperties().Select(static property => property.Name).ToArray());
    }

    private static AvatarDispatchSpike[] MotorDispatches()
        =>
        [
            new AvatarDispatchSpike("M1", "L", 100, "population:l:excitatory:1:0"),
            new AvatarDispatchSpike("M1", "R", 101, "population:r:excitatory:1:0")
        ];

    private static readonly AvatarKinematicsOptions TestKinematics = new(
        MaxMotorDrive: 240.0,
        ForwardSpeedCoefficient: 0.0125,
        TurnSpeedCoefficient: 3.2,
        MinForwardSpeed: 0.0,
        MaxForwardSpeed: 3.2,
        MaxTurnRateDeg: 220.0);

    private static AvatarService CreateService(AvatarServiceClockOptions? clockOptions = null)
        => new(
            new AvatarNervousSystemOptions(TestKinematics),
            name: "NRE.Tests.AvatarService",
            clockOptions: clockOptions);

    private static AvatarBodyStateProfile CreateBodyStateProfile()
        => new(
            MaxForwardSpeed: 3.2,
            MaxTurnRateDeg: 220.0,
            BaseIntensity: 0.2,
            MotionIntensityWeight: 0.5,
            TurnIntensityWeight: 0.1,
            ContactIntensityWeight: 0.4,
            BaseBurstCount: 6.0,
            MotionBurstWeight: 8.0,
            TurnBurstWeight: 3.0,
            ContactBurstWeight: 6.0);

    private static AvatarNervousSystemSignal WaitForSignal(
        AvatarService service,
        Func<AvatarNervousSystemSignal, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueSignal(out var queued))
            {
                if (predicate(queued))
                {
                    return queued;
                }
            }

            var latest = service.LatestSignal;
            if (predicate(latest))
            {
                return latest;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish the expected signal.");
    }

    private static AvatarAuditoryCue WaitForAuditoryInput(AvatarService service)
        => WaitForQueue<AvatarAuditoryCue>(service.TryDequeueAuditoryInput, "auditory input");

    private static AvatarBodyStateInput WaitForBodyInput(AvatarService service)
        => WaitForQueue<AvatarBodyStateInput>(service.TryDequeueBodyInput, "body input");

    private static AvatarObjectObservation WaitForObjectObservation(AvatarService service)
        => WaitForQueue<AvatarObjectObservation>(service.TryDequeueObjectObservation, "object observation");

    private static AvatarSightFrame WaitForSightOutput(
        AvatarService service,
        Func<AvatarSightFrame, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            while (service.TryDequeueSightOutput(out var frame))
            {
                if (predicate(frame))
                {
                    return frame;
                }
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not publish the expected sight frame.");
    }

    private delegate bool TryDequeue<T>(out T item);

    private static T WaitForQueue<T>(TryDequeue<T> tryDequeue, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (tryDequeue(out var item))
            {
                return item;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException($"Avatar service did not publish {description}.");
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Avatar service did not reach the expected state.");
    }
}
