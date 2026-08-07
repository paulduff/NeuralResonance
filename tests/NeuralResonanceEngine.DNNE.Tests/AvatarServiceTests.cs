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
    public void AudioTransportPreservesRawPcmFrame()
    {
        using var service = CreateService();
        var pcm = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var expected = new AvatarAudioFrame(7, 1234, 16000, 1, 2, pcm);

        service.PostAudioInputFrame(expected);

        var actual = WaitForAudioInput(service);
        Assert.Same(expected, actual);
        Assert.Equal(pcm, actual.Pcm16Le);
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

    private static AvatarAudioFrame WaitForAudioInput(AvatarService service)
        => WaitForQueue<AvatarAudioFrame>(service.TryDequeueAudioInput, "audio input");

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
