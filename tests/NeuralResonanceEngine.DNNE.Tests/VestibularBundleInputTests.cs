using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// All gains below are deliberately arbitrary dimensional controls. None is a
// proposed physiological parameter, active hair cell, afferent or body loop.
public sealed class VestibularBundleInputTests
{
    private static PhysicalSampleClock Clock(long initial = 0) => new(
        Guid.Parse("e49918f1-0597-45fd-bec0-a242f8a3f430"), "body", "skull", initial, .125, 500, 1000);
    private static AvatarVestibularBundleProjection Projection(AvatarVestibularBundleReceptor receptor = AvatarVestibularBundleReceptor.LeftHorizontal) =>
        new(Guid.Parse("b14e5b1d-edf1-456f-a179-482b48c6d437"), "diagnostic-only dimensional fixture; no physiological calibration",
            receptor, receptor == AvatarVestibularBundleReceptor.Otolith ? 0 : 100, new(0, 0, 0), 0);
    private static AvatarVestibularKinematics Input(PhysicalSampleClock clock, long step, double yaw = 1,
        double pitch = 2, double roll = .5, double right = 3, double down = 4, double forward = -2) =>
        new(step, PhysicalSampleProtocol.Identity(clock, step), new string('A', 64), pitch, yaw, roll, right, down, forward);

    [Fact]
    public void CellSourceUnitsAndFiniteCoefficientsAreRequired()
    {
        var p = Projection();
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), p with { HairCellId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), p with { ParameterSource = " " }));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), p with { Receptor = (AvatarVestibularBundleReceptor)99 }));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), p with { CanalNanometersPerRadianPerSecond = double.NaN }));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), p with { OffsetNanometers = double.PositiveInfinity }));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), p with { OtolithNanometersPerMeterPerSecondSquared = new(0, 0, double.NaN) }));
    }

    [Fact]
    public void CanalAndOtolithUnitsCannotBeMixedInOneReceptor()
    {
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), Projection() with
            { OtolithNanometersPerMeterPerSecondSquared = new(1, 0, 0) }));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularBundleInput(Clock(), Projection(AvatarVestibularBundleReceptor.Otolith) with
            { CanalNanometersPerRadianPerSecond = 1 }));
    }

    [Fact]
    public void FingerprintRetainsCellParameterSourceCoefficientAndPhysicalEpoch()
    {
        var p = Projection(); string original = new AvatarVestibularBundleInput(Clock(), p).Fingerprint;
        Assert.Equal(original, new AvatarVestibularBundleInput(Clock(), p with { }).Fingerprint);
        foreach (var changed in new[] { p with { HairCellId = Guid.NewGuid() }, p with { ParameterSource = "different evidence" },
            p with { CanalNanometersPerRadianPerSecond = 101 }, p with { OffsetNanometers = 1 },
            p with { Receptor = AvatarVestibularBundleReceptor.RightHorizontal } })
            Assert.NotEqual(original, new AvatarVestibularBundleInput(Clock(), changed).Fingerprint);
        Assert.NotEqual(original, new AvatarVestibularBundleInput(Clock() with { NeuralEpochMs = 1001 }, p).Fingerprint);
    }

    [Fact]
    public void InitialObservationReleasesNoInventedPastInterval()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection() with { OffsetNanometers = 7 });
        var receipt = owner.Consume(Input(Clock(), 0));
        Assert.Null(receipt.CompletedInterval); Assert.Null(owner.Capture().PendingInterval);
        Assert.Equal(7, receipt.Sample.DisplacementNanometers); Assert.Equal(1000, receipt.Sample.Mechanics.State.NeuralTimeMs);
        Assert.Equal(0, receipt.Sample.Mechanics.State.IntegratedIntervals);
    }

    [Theory]
    [InlineData(AvatarVestibularBundleReceptor.LeftHorizontal)]
    [InlineData(AvatarVestibularBundleReceptor.RightHorizontal)]
    [InlineData(AvatarVestibularBundleReceptor.LeftAnterior)]
    [InlineData(AvatarVestibularBundleReceptor.RightAnterior)]
    [InlineData(AvatarVestibularBundleReceptor.LeftPosterior)]
    [InlineData(AvatarVestibularBundleReceptor.RightPosterior)]
    public void SixCanalDirectionsKeepTheDeclaredSignedDimensionalGain(AvatarVestibularBundleReceptor receptor)
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection(receptor) with { CanalNanometersPerRadianPerSecond = -200, OffsetNanometers = 3 });
        owner.Consume(Input(Clock(), 0)); var sample = owner.Consume(Input(Clock(), 1)).Sample;
        var c = sample.Mechanics.State.Cupula;
        double selected = receptor switch
        {
            AvatarVestibularBundleReceptor.LeftHorizontal => c.LeftHorizontal,
            AvatarVestibularBundleReceptor.RightHorizontal => c.RightHorizontal,
            AvatarVestibularBundleReceptor.LeftAnterior => c.LeftAnterior,
            AvatarVestibularBundleReceptor.RightAnterior => c.RightAnterior,
            AvatarVestibularBundleReceptor.LeftPosterior => c.LeftPosterior,
            _ => c.RightPosterior
        };
        Assert.Equal(3 - 200 * selected, sample.DisplacementNanometers);
        Assert.NotEqual(3, sample.DisplacementNanometers);
    }

    [Fact]
    public void OtolithProjectsRightDownForwardWithExplicitOffsetAndUnits()
    {
        var p = Projection(AvatarVestibularBundleReceptor.Otolith) with
            { OtolithNanometersPerMeterPerSecondSquared = new(2, -3, 5), OffsetNanometers = 7 };
        var sample = new AvatarVestibularBundleInput(Clock(), p).Consume(Input(Clock(), 0)).Sample;
        Assert.Equal(-9, sample.DisplacementNanometers); // 7 + 2*3 - 3*4 + 5*(-2)
        Assert.Equal(new AvatarVestibularVector(3, 4, -2), sample.Mechanics.State.Otolith);
    }

    [Fact]
    public void NewEndpointNeverRewritesTheReleasedHeldInput()
    {
        var p = Projection(AvatarVestibularBundleReceptor.Otolith) with { OtolithNanometersPerMeterPerSecondSquared = new(2, 0, 0) };
        var owner = new AvatarVestibularBundleInput(Clock(), p);
        var first = owner.Consume(Input(Clock(), 0, right: 3));
        var second = owner.Consume(Input(Clock(), 1, right: 100));
        Assert.Equal(6, second.CompletedInterval!.HeldDisplacementNanometers);
        Assert.Same(first.Sample, second.CompletedInterval.StartSample);
        owner.AcknowledgeInterval(second.CompletedInterval.Fingerprint);
        var third = owner.Consume(Input(Clock(), 2, right: -100));
        Assert.Equal(6, third.CompletedInterval!.HeldDisplacementNanometers);
        Assert.True(third.Sample.DisplacementNanometers > 6);
        owner.AcknowledgeInterval(third.CompletedInterval.Fingerprint);
        var fourth = owner.Consume(Input(Clock(), 3));
        Assert.Equal(third.Sample.DisplacementNanometers, fourth.CompletedInterval!.HeldDisplacementNanometers);
    }

    [Fact]
    public void ExactLastReplayDoesNotAdvanceOrReopenAnAcknowledgedInterval()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection());
        var initial = owner.Consume(Input(Clock(), 0)); Assert.Same(initial, owner.Consume(Input(Clock(), 0)));
        var receipt = owner.Consume(Input(Clock(), 1)); var state = owner.Capture();
        Assert.Same(receipt, owner.Consume(Input(Clock(), 1))); Assert.Equal(state, owner.Capture());
        owner.AcknowledgeInterval(receipt.CompletedInterval!.Fingerprint);
        Assert.Same(receipt, owner.Consume(Input(Clock(), 1))); Assert.Null(owner.Capture().PendingInterval);
    }

    [Fact]
    public void ConflictingAndMissingSamplesAreRejectedBeforeMechanicalMutation()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection()); owner.Consume(Input(Clock(), 0)); var state = owner.Capture();
        Assert.Throws<ArgumentException>(() => owner.Consume(Input(Clock(), 0, yaw: -1)));
        Assert.Throws<ArgumentException>(() => owner.Consume(Input(Clock(), 2)));
        Assert.Equal(state, owner.Capture()); Assert.Null(owner.Capture().Failure);
        owner.Consume(Input(Clock(), 1));
    }

    [Fact]
    public void AnUnconsumedIntervalStopsTheNextEndpointBeforeMechanicalAdvancement()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection()); owner.Consume(Input(Clock(), 0));
        var receipt = owner.Consume(Input(Clock(), 1)); var state = owner.Capture();
        Assert.Throws<InvalidOperationException>(() => owner.Consume(Input(Clock(), 2)));
        Assert.Equal(state, owner.Capture());
        owner.AcknowledgeInterval(receipt.CompletedInterval!.Fingerprint); owner.Consume(Input(Clock(), 2));
        Assert.Equal(2, owner.Capture().Mechanics.CompletedThroughStep);
    }

    [Fact]
    public void OnlyTheExactPendingIntervalCanBeAcknowledged()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection()); owner.Consume(Input(Clock(), 0));
        var interval = owner.Consume(Input(Clock(), 1)).CompletedInterval!;
        Assert.Throws<ArgumentException>(() => owner.AcknowledgeInterval("foreign"));
        Assert.Same(interval, owner.Capture().PendingInterval);
        owner.AcknowledgeInterval(interval.Fingerprint); owner.AcknowledgeInterval(interval.Fingerprint);
        var next = owner.Consume(Input(Clock(), 2)).CompletedInterval!;
        Assert.Throws<ArgumentException>(() => owner.AcknowledgeInterval(interval.Fingerprint));
        Assert.Same(next, owner.Capture().PendingInterval);
    }

    [Fact]
    public void ASubstepMustFitCompletelyInsideItsKnownPhysicalInterval()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection()); owner.Consume(Input(Clock(), 0));
        var interval = owner.Consume(Input(Clock(), 1)).CompletedInterval!;
        Assert.Equal(1000, interval.StartNeuralMs); Assert.Equal(1000.125, interval.EndNeuralMs);
        Assert.Equal(interval.HeldDisplacementNanometers, interval.HeldInputFor(1000.03125, 1000.0625));
        Assert.Equal(interval.HeldDisplacementNanometers, interval.HeldInputFor(1000, 1000.125));
        Assert.Throws<ArgumentException>(() => interval.HeldInputFor(999.99, 1000.01));
        Assert.Throws<ArgumentException>(() => interval.HeldInputFor(1000.1, 1000.13));
        Assert.Throws<ArgumentException>(() => interval.HeldInputFor(1000.05, 1000.05));
        Assert.Throws<ArgumentException>(() => interval.HeldInputFor(double.NaN, 1000.125));
    }

    [Fact]
    public void LaterInitialAccelerationAvailabilityDoesNotCreateEarlierSensoryHistory()
    {
        var clock = Clock(500); var owner = new AvatarVestibularBundleInput(clock, Projection(), 501);
        Assert.Throws<ArgumentException>(() => owner.Consume(Input(clock, 500)));
        Assert.Null(owner.Consume(Input(clock, 501)).CompletedInterval);
        var interval = owner.Consume(Input(clock, 502)).CompletedInterval!;
        Assert.Equal(1000.125, interval.StartNeuralMs); Assert.Equal(1000.25, interval.EndNeuralMs);
        Assert.Equal(501, interval.StartSample.Input.PhysicsStep);
        Assert.Throws<ArgumentException>(() => interval.HeldInputFor(1000, 1000.125));
    }

    [Fact]
    public void ForeignSessionAndStaleSamplesCannotResetTheOrgan()
    {
        var clock = Clock(); var owner = new AvatarVestibularBundleInput(clock, Projection());
        Assert.Throws<ArgumentException>(() => owner.Consume(Input(clock with { SessionId = Guid.NewGuid() }, 0)));
        owner.Consume(Input(clock, 0)); var interval = owner.Consume(Input(clock, 1)).CompletedInterval!;
        owner.AcknowledgeInterval(interval.Fingerprint); var state = owner.Capture();
        Assert.Throws<ArgumentException>(() => owner.Consume(Input(clock, 0))); Assert.Equal(state, owner.Capture());
    }

    [Fact]
    public void ProjectionOverflowPreservesTheAcceptedPhysicalPrefixAndFaults()
    {
        var p = Projection(AvatarVestibularBundleReceptor.Otolith) with
            { OtolithNanometersPerMeterPerSecondSquared = new(double.MaxValue, 0, 0) };
        var owner = new AvatarVestibularBundleInput(Clock(), p);
        Assert.Throws<ArithmeticException>(() => owner.Consume(Input(Clock(), 0)));
        var state = owner.Capture(); Assert.Equal(0, state.Mechanics.CompletedThroughStep);
        Assert.NotNull(state.Failure); Assert.Equal(0, state.Failure.AttemptedInput.PhysicsStep);
        Assert.Null(state.LastReceipt); Assert.Null(state.PendingInterval);
        Assert.Throws<InvalidOperationException>(() => owner.Consume(Input(Clock(), 0, right: 0)));
        Assert.Equal(state, owner.Capture());
    }

    [Fact]
    public void PhysicalInputHasOneThreadOwner()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection()); Exception? error = null;
        var thread = new Thread(() => { try { owner.Consume(Input(Clock(), 0)); } catch (Exception e) { error = e; } });
        thread.Start(); thread.Join(); Assert.IsType<InvalidOperationException>(error); Assert.Null(owner.Capture().LastReceipt);
    }

    [Fact]
    public void ProjectionDoesNotChangeTheExistingMechanicalTrajectory()
    {
        var clock = Clock(); var original = new AvatarClockedVestibularMechanics(clock);
        var owner = new AvatarVestibularBundleInput(clock, Projection());
        for (int i = 0; i < 10; i++)
        {
            var input = Input(clock, i, yaw: i % 2 == 0 ? 10 : -3, right: i);
            var expected = original.Consume(input); var receipt = owner.Consume(input);
            Assert.Equal(expected, receipt.Sample.Mechanics);
            Assert.Equal(input, receipt.Sample.Input);
            if (receipt.CompletedInterval is { } interval) owner.AcknowledgeInterval(interval.Fingerprint);
        }
    }

    [Fact]
    public void ReceiptRoundTripRetainsOriginalInputsMechanicsAndAvailability()
    {
        var owner = new AvatarVestibularBundleInput(Clock(), Projection()); owner.Consume(Input(Clock(), 0));
        var receipt = owner.Consume(Input(Clock(), 1));
        var restored = JsonSerializer.Deserialize<AvatarVestibularBundleReceipt>(JsonSerializer.Serialize(receipt));
        Assert.Equal(receipt, restored);
        Assert.Equal(Input(Clock(), 0), restored!.CompletedInterval!.StartSample.Input);
        Assert.Equal(Input(Clock(), 1), restored.CompletedInterval.EndSample.Input);
        Assert.Equal(owner.Fingerprint, restored.CompletedInterval.OwnerFingerprint);
    }

    [Fact]
    public void ActualSkullAdapterRetainsTheFirstAccelerationAvailability()
    {
        var clock = Clock(); var skull = new AvatarSkullKinematics(clock, "head", "labyrinth", new(0, 0, 0));
        var projection = Projection(AvatarVestibularBundleReceptor.Otolith) with
            { OtolithNanometersPerMeterPerSecondSquared = new(0, 2, 0) };
        var owner = new AvatarVestibularBundleInput(clock, projection, skull.FirstMechanicsStep);
        AvatarSkullObservation Raw(long step) => new(step, PhysicalSampleProtocol.Identity(clock, step), "head", "labyrinth",
            new(0, 0, 0), new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1),
            new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), new(0, -9, 0));
        Assert.Null(skull.Consume(Raw(0)).VestibularInput);
        var first = skull.Consume(Raw(1)); var initial = owner.Consume(first.VestibularInput!);
        Assert.Null(initial.CompletedInterval); Assert.Equal(18, initial.Sample.DisplacementNanometers);
        Assert.Equal(first.NeuralTimeMs, initial.Sample.Mechanics.State.NeuralTimeMs);
        Assert.Equal(first.VestibularInput, initial.Sample.Input);
        var interval = owner.Consume(skull.Consume(Raw(2)).VestibularInput!).CompletedInterval!;
        Assert.Equal(1000.125, interval.StartNeuralMs); Assert.Equal(1000.25, interval.EndNeuralMs);
    }

    [Fact]
    public void CompletedPhysicalInputCanFeedAnObservedPassiveSourceWithoutFutureFill()
    {
        // This is a wiring control using the earlier severed two-capacitor
        // fixture. Its crossing is NOT biological afferent firing.
        var clock = Clock() with { NeuralEpochMs = 0 };
        var model = AfferentEventObserverTests.Model(session: clock.SessionId);
        var projection = Projection() with { HairCellId = model.Capture().CellId, OffsetNanometers = 123 };
        var owner = new AvatarVestibularBundleInput(clock, projection);
        owner.Consume(Input(clock, 0)); var interval = owner.Consume(Input(clock, 1)).CompletedInterval!;
        var observer = new AvatarAfferentEventObserver(model, new("isolated-bundle-fixture", "afferent", "a", -60));
        var packets = new AvatarAfferentPacketSource(observer);
        for (int step = 0; step < 125; step++)
        {
            double input = interval.HeldInputFor(packets.Source.TimeAt(step), packets.Source.TimeAt(step + 1));
            var receipt = packets.AdvanceNext(step, input); Assert.Equal(123, receipt.BundleDisplacementNanometers);
        }
        var before = JsonSerializer.Serialize(model.Capture());
        Assert.Throws<ArgumentException>(() => packets.AdvanceNext(125,
            interval.HeldInputFor(packets.Source.TimeAt(125), packets.Source.TimeAt(126))));
        Assert.Equal(before, JsonSerializer.Serialize(model.Capture()));
        var packet = packets.Seal(); Assert.Single(packet.Events); Assert.Equal(interval.EndNeuralMs, packet.EndMs);
        Assert.InRange(packet.Events[0].CrossingTimeMs, interval.StartNeuralMs, interval.EndNeuralMs);
        packets.Acknowledge(packet.Fingerprint); owner.AcknowledgeInterval(interval.Fingerprint);
    }
}
