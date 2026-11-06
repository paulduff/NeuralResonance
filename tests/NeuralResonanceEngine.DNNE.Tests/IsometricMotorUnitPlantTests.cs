using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Events are explicitly synthetic fixtures for interface/causality checks.
// They do not establish the existence or physiology of their named source cells.
public sealed class IsometricMotorUnitPlantTests
{
    private static readonly Guid Projection = Guid.Parse("36679a8a-5550-4bd8-a285-d55f24e499ce");
    private static readonly Guid Session = Guid.Parse("060e52bf-063f-413d-9b02-dbed168fe89f");
    private static MotorAxonClock Clock => new(Session, 100, 1000);
    private static MotorAxonContact Contact(string source = "source-a", string unit = "unit-a", double delay = .0625) =>
        MotorAxonContact.Create(source, "cell-" + unit, Projection, unit, delay);
    private static SpikeMessage Spike(double time, int id = 1, string source = "source-a", string unit = "unit-a") => new()
    {
        MessageId = StableNeuralIdentity.Create("isometric-plant-fixture", source, id), TimestampMs = time,
        SourceStructure = StructureId.SpinalCordMotor, SourceInstanceKey = source, SourceNeuronId = "cell-" + unit,
        ProjectionId = Projection, SynapseId = StableNeuralIdentity.Create("outbound-fixture", source, unit),
        Neurotransmitter = NTEnum.ACETYLCHOLINE
    };
    private static Dictionary<string, MotorUnitCalciumParameters> Parameters(MotorAxonAnatomy anatomy) =>
        anatomy.Contacts.ToDictionary(c => c.MotorUnitId, _ => MotorUnitCalciumParameters.NagamoriSlowUnit1);
    private static AvatarIsometricMotorUnitPlant Plant(MotorAxonAnatomy? anatomy = null, int refinement = 0)
    {
        anatomy ??= new([Contact()]);
        return new(anatomy, Clock, Parameters(anatomy), refinement);
    }
    private static MotorAxonBatch Batch(MotorAxonAnatomy anatomy, SpikeMessage[] spikes, double through = 700, string source = "source-a") =>
        new MotorAxonPublisher(anatomy, Clock, source).Publish(spikes, through);
    private static IsometricMotorUnitState Unit(AvatarIsometricMotorUnitPlant plant, string key = "unit-a") =>
        plant.Capture().Units.Single(u => u.MotorUnitId == key);

    [Fact]
    public void FractionalArrivalAndOriginalIdentityReachOnlyTheAddressedUnit()
    {
        var contact = Contact(delay: 2.0625); var anatomy = new MotorAxonAnatomy([contact]); var plant = Plant(anatomy);
        var original = Spike(101.03125); plant.Accept(Batch(anatomy, [original]));
        Assert.Empty(plant.AdvanceToTick(24).Arrivals); Assert.Equal(0, Unit(plant).ForceNewtons);
        var result = plant.AdvanceToTick(25); var arrival = Assert.Single(result.Arrivals);
        Assert.Equal(1003.09375, arrival.ArrivalAtBodyMs); Assert.Equal(101.03125, arrival.FiredAtNeuralMs);
        Assert.Equal(original.MessageId, arrival.MessageId); Assert.Equal(original.SynapseId, arrival.SourceSynapseId);
        Assert.Equal(contact.ContactId, arrival.ContactId); Assert.Equal(original.MessageId, Unit(plant).LastMessageId);
        Assert.Equal(1, Unit(plant).Arrivals); Assert.True(Unit(plant).FreeCalcium > 0);
    }

    [Fact]
    public void PlantMatchesDirectConsumerWithNoSecondActivationOrConductionDelay()
    {
        var contact = Contact(); var anatomy = new MotorAxonAnatomy([contact]); var plant = Plant(anatomy);
        var batch = Batch(anatomy, [Spike(101.03125), Spike(105.125, 2), Spike(130, 3)]); plant.Accept(batch);
        var result = plant.AdvanceToTick(2400); var direct = new AvatarMotorUnitCalcium(Parameters(anatomy)[contact.MotorUnitId], contact, Clock);
        int next = 0;
        for (int tick = 1; tick <= 2400; ++tick)
        {
            double endpoint = 1000 + tick * .125;
            while (next < result.Arrivals.Count && result.Arrivals[next].ArrivalAtBodyMs <= endpoint)
            {
                var arrival = result.Arrivals[next++];
                if (arrival.ArrivalAtBodyMs > direct.TimeMs) direct.Advance(arrival.ArrivalAtBodyMs - direct.TimeMs);
                direct.Receive(arrival);
            }
            if (endpoint > direct.TimeMs) direct.Advance(endpoint - direct.TimeMs);
        }
        var actual = Unit(plant);
        Assert.Equal(direct.FreeCalcium, actual.FreeCalcium); Assert.Equal(direct.BoundCalcium, actual.BoundCalcium);
        Assert.Equal(direct.Activation, actual.Activation); Assert.Equal(direct.IsometricForceNewtons, actual.ForceNewtons);
        Assert.Equal(direct.ReleasedCalcium, actual.ReleasedCalcium); Assert.Equal(direct.RecoveredCalcium, actual.RecoveredCalcium);
        Assert.Equal(direct.MaximumCalciumLedgerError, actual.MaximumCalciumLedgerError);
    }

    [Fact]
    public void FramePartitioningLeavesCompleteUnitStateExactlyUnchanged()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var whole = Plant(anatomy); var frames = Plant(anatomy);
        var batch = Batch(anatomy, [Spike(101.03125), Spike(117.0625, 2), Spike(160.125, 3)]);
        whole.Accept(batch); frames.Accept(batch); whole.AdvanceToTick(3200);
        foreach (int end in new[] { 1, 7, 100, 313, 777, 800, 900, 2000, 3200 }) frames.AdvanceToTick(end);
        Assert.Equal(Unit(whole), Unit(frames)); Assert.Equal(whole.Capture().DeliveredEvents, frames.Capture().DeliveredEvents);
    }

    [Fact]
    public void PacketPartitioningAcrossBodyStepsPreservesEventsAndForceExactly()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var whole = Plant(anatomy); var split = Plant(anatomy);
        SpikeMessage[] events = [Spike(101.03125), Spike(105.0625, 2), Spike(110.03125, 3)];
        whole.Accept(Batch(anatomy, events)); var publisher = new MotorAxonPublisher(anatomy, Clock, "source-a");
        split.Accept(publisher.Publish([events[0]], 104)); var first = split.AdvanceToTick(32);
        split.Accept(publisher.Publish(events[1..], 700)); var second = split.AdvanceToTick(1600);
        var all = whole.AdvanceToTick(1600);
        Assert.Equal(all.Arrivals, first.Arrivals.Concat(second.Arrivals).ToArray()); Assert.Equal(Unit(whole), Unit(split));
    }

    [Fact]
    public void UnrelatedUnitEventsDoNotSplitOrPerturbAnotherUnitsIntegration()
    {
        var anatomy = new MotorAxonAnatomy([Contact(), Contact("source-b", "unit-b")]);
        var quietOther = Plant(anatomy); var activeOther = Plant(anatomy);
        var a = Batch(anatomy, [Spike(101.03125), Spike(145.0625, 2)]);
        quietOther.Accept(a); activeOther.Accept(a);
        quietOther.Accept(Batch(anatomy, [], source: "source-b"));
        var unrelated = Enumerable.Range(0, 20).Select(i => Spike(100.0078125 + i * 3.03125, i + 1, "source-b", "unit-b")).ToArray();
        activeOther.Accept(Batch(anatomy, unrelated, source: "source-b"));
        quietOther.AdvanceToTick(2400); activeOther.AdvanceToTick(2400);
        Assert.Equal(Unit(quietOther), Unit(activeOther)); Assert.Equal(0, Unit(quietOther, "unit-b").ForceNewtons);
        Assert.True(Unit(activeOther, "unit-b").ForceNewtons > 0);
    }

    [Fact]
    public void SourcePacketOrderPreservesSimultaneousDeliveryAndBothUnits()
    {
        var anatomy = new MotorAxonAnatomy([Contact(), Contact("source-b", "unit-b")]); var forward = Plant(anatomy); var reverse = Plant(anatomy);
        var a = Batch(anatomy, [Spike(101)]); var b = Batch(anatomy, [Spike(101, source: "source-b", unit: "unit-b")], source: "source-b");
        forward.Accept(a); forward.Accept(b); reverse.Accept(b); reverse.Accept(a);
        var x = forward.AdvanceToTick(800); var y = reverse.AdvanceToTick(800);
        Assert.Equal(x.Arrivals, y.Arrivals); Assert.Equal(x.Frame.Units, y.Frame.Units);
    }

    [Fact]
    public void ReplayedPacketCannotAddCalciumOrRepeatAnArrival()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var once = Plant(anatomy); var replayed = Plant(anatomy);
        var batch = Batch(anatomy, [Spike(101)]); once.Accept(batch); replayed.Accept(batch);
        once.AdvanceToTick(400); replayed.AdvanceToTick(400);
        Assert.Equal(MotorAxonBatchDisposition.ExactReplay, replayed.Accept(batch));
        once.AdvanceToTick(800); Assert.Empty(replayed.AdvanceToTick(800).Arrivals);
        Assert.Equal(Unit(once), Unit(replayed)); Assert.Equal(1, Unit(replayed).Arrivals);
    }

    [Fact]
    public void MissingSourceCompletenessCannotAdvanceAnyMuscle()
    {
        var anatomy = new MotorAxonAnatomy([Contact(), Contact("source-b", "unit-b")]); var plant = Plant(anatomy);
        plant.Accept(Batch(anatomy, [Spike(101)])); var before = plant.Capture();
        Assert.Throws<ArgumentOutOfRangeException>(() => plant.AdvanceToTick(800));
        Assert.Equal(before.Units, plant.Capture().Units); Assert.Equal(before.PendingEvents, plant.Capture().PendingEvents);
        Assert.Equal(0, plant.CompletedTick); Assert.False(plant.Faulted);
        plant.Accept(Batch(anatomy, [], source: "source-b")); plant.AdvanceToTick(800);
        Assert.Equal(1, Unit(plant).Arrivals);
    }

    [Fact]
    public void RewindAndUnrepresentableTicksAreRejectedWithoutCorruptingThePlant()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var plant = Plant(anatomy); plant.Accept(Batch(anatomy, [])); plant.AdvanceToTick(8);
        var before = Unit(plant);
        Assert.Throws<ArgumentOutOfRangeException>(() => plant.AdvanceToTick(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => plant.AdvanceToTick(long.MaxValue));
        Assert.Equal(before, Unit(plant)); Assert.False(plant.Faulted); plant.AdvanceToTick(9);
    }

    [Fact]
    public void MalformedPacketCannotPartiallyRecruitAUnit()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var plant = Plant(anatomy);
        var good = Batch(anatomy, [Spike(101), Spike(102, 2)]);
        var bad = good with { Spikes = new[] { good.Spikes[0], good.Spikes[1] with { Neurotransmitter = NTEnum.GABA } } };
        Assert.Throws<ArgumentException>(() => plant.Accept(bad)); Assert.Equal(0, plant.Capture().AcceptedEvents);
        Assert.Equal(0, Unit(plant).Arrivals); plant.Accept(good); plant.AdvanceToTick(800);
        Assert.Equal(2, Unit(plant).Arrivals);
    }

    [Fact]
    public void EveryDeclaredUnitNeedsAnExplicitParameterSet()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]);
        Assert.Throws<ArgumentException>(() => new AvatarIsometricMotorUnitPlant(anatomy, Clock, new Dictionary<string, MotorUnitCalciumParameters>()));
        var parameters = Parameters(anatomy); parameters.Add("unknown", MotorUnitCalciumParameters.NagamoriSlowUnit1);
        Assert.Throws<ArgumentException>(() => new AvatarIsometricMotorUnitPlant(anatomy, Clock, parameters));
        Assert.Throws<ArgumentException>(() => new AvatarIsometricMotorUnitPlant(anatomy, Clock with { BodyEpochMs = 1e16 }, Parameters(anatomy)));
    }

    [Fact]
    public void ProvenSilenceHasNoGeneratedContraction()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var plant = Plant(anatomy);
        plant.Accept(Batch(anatomy, [], 2200)); var result = plant.AdvanceToTick(16000);
        Assert.Empty(result.Arrivals); Assert.Equal(0, Unit(plant).ForceNewtons); Assert.Equal(0, Unit(plant).FreeCalcium);
        Assert.Equal(0, Unit(plant).MaximumCalciumLedgerError); Assert.Equal(0, result.Frame.AcceptedEvents);
    }

    [Fact]
    public void CapturedStatesCannotMutateThePlant()
    {
        var plant = Plant(); var snapshot = plant.Capture(); var item = Assert.Single(snapshot.Units);
        Assert.Throws<NotSupportedException>(() => ((IList<IsometricMotorUnitState>)snapshot.Units)[0] = item with { ForceNewtons = 42 });
        _ = item with { ForceNewtons = 123 }; Assert.Equal(0, Unit(plant).ForceNewtons);
    }

    [Fact]
    public void NumericalFaultStopsTheWholePlantWithoutResetOrReplay()
    {
        var anatomy = new MotorAxonAnatomy([Contact()]); var parameters = Parameters(anatomy);
        parameters["unit-a"] = parameters["unit-a"] with { ReleaseRatePerSecond = double.MaxValue / 4 };
        var plant = new AvatarIsometricMotorUnitPlant(anatomy, Clock, parameters);
        var batch = Batch(anatomy, [Spike(101)]); plant.Accept(batch);
        Assert.Throws<InvalidOperationException>(() => plant.AdvanceToTick(800));
        Assert.True(plant.Faulted); Assert.NotNull(plant.Capture().Fault); Assert.Equal(0, plant.CompletedTick);
        Assert.Equal(1, Unit(plant).Arrivals); Assert.True(Unit(plant).TimeMs > Clock.BodyEpochMs);
        Assert.Throws<InvalidOperationException>(() => plant.AdvanceToTick(800));
        Assert.Throws<InvalidOperationException>(() => plant.Accept(batch));
        Assert.Equal(1, plant.Capture().DeliveredEvents);
    }

    [Fact]
    public void FingerprintIncludesAnatomyParametersAndSolverButNotInputOrdering()
    {
        var a = Contact(); var b = Contact("source-b", "unit-b"); var anatomy = new MotorAxonAnatomy([a, b]);
        var reverse = new MotorAxonAnatomy([b, a]); var original = Plant(anatomy);
        Assert.Equal(original.Fingerprint, Plant(reverse).Fingerprint); Assert.NotEqual(original.Fingerprint, Plant(anatomy, 1).Fingerprint);
        var parameters = Parameters(anatomy); parameters["unit-a"] = parameters["unit-a"] with { PeakTetanicForceNewtons = .5 };
        Assert.NotEqual(original.Fingerprint, new AvatarIsometricMotorUnitPlant(anatomy, Clock, parameters).Fingerprint);
        Assert.NotEqual(original.Fingerprint, Plant(new([a with { ConductionDelayMs = 2 }, b])).Fingerprint);
    }
}
