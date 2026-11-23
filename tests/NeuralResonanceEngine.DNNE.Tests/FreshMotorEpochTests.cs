using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Clock ownership controls, with prescribed input events. No source-neuron or
// independent coupled accuracy/physiological firing claim follows from these.
public sealed class FreshMotorEpochTests
{
    static readonly Guid Session = Guid.Parse("78f675ae-6736-43f4-8b0b-3f82624c0e43");
    const string Input = "isolated-epoch-input", PoolName = "isolated-epoch-pool";
    static readonly ReceptorMotorIdentity Identity = new("isolated-epoch-motor", "motor-epoch",
        StableNeuralIdentity.Create("epoch-output-projection"), StableNeuralIdentity.Create("epoch-output-synapse"));
    static readonly SynapticConnection Projection = new(StructureId.SpinalCordMotor,
        StableNeuralIdentity.Create("epoch-input-projection"), NTEnum.GLUTAMATE, "epoch-control");
    static MotorReceptorDefinition Contact => new(Input, StructureId.VestibularNuclei, "source-cell",
        StableNeuralIdentity.Create("epoch-input-synapse"), Projection.SynapseId, Identity.Instance, Identity.Neuron,
        0, "soma-contact", 0, MotorReceptorKind.AmpaReference, 10_000, .125);
    static string Json(object value) => JsonSerializer.Serialize(value);
    static ReceptorMotorSource Source(double epoch) => new(MotoneuronReferenceParameters.CatLowThreshold,
        [Contact], Session, Identity, maximumStepMs: .00390625, sampleMs: .125,
        crossingInterpolation: MotoneuronCrossingInterpolation.CubicHermite, initialNeuralTimeMs: epoch);
    static SpikeMessage Event(double at, int ordinal = 0) => new()
    {
        MessageId = StableNeuralIdentity.Create("epoch-control-event", ordinal), TimestampMs = at,
        SourceStructure = Contact.SourceStructure, SourceInstanceKey = Input, SourceNeuronId = Contact.SourceNeuron,
        SynapseId = Contact.SourceSynapse, ProjectionId = Contact.Projection, TargetStructure = StructureId.SpinalCordMotor,
        TargetNeuronId = Contact.TargetNeuron, Neurotransmitter = Contact.Transmitter, VesicleQuanta = 1, ReuptakeRate = 8,
        SpikeType = SpikeTypeEnum.ACTION_POTENTIAL
    };
    static MotorAxonAnatomy Anatomy => new([MotorAxonContact.Create(Identity.Instance, Identity.Neuron, Identity.Projection, "epoch-unit", 2.0625)]);

    [Theory]
    [InlineData(10)]
    [InlineData(1010)]
    [InlineData(1024)]
    public void FreshEpochHasNoInventedPrehistoryOrImplicitSilence(double epoch)
    {
        var source = Source(epoch); var before = source.Capture(); var zero = Source(0).Capture();
        Assert.Equal(epoch, before.Cell.TimeMs); Assert.Equal(0, before.CompletedSamples);
        Assert.Equal(zero.Cell.MembraneState, before.Cell.MembraneState); Assert.Empty(before.Emissions); Assert.Empty(before.Cell.Crossings);
        Assert.All(before.Cell.Contacts, c => Assert.Equal(epoch, c.Value.TimeMs));
        Assert.All(before.Cell.InputLanes, l => { Assert.Equal(0, l.Batches); Assert.Null(l.CompleteThroughSourceMs); });
        Assert.Throws<InvalidOperationException>(() => source.AdvanceOneSample());
        Assert.Equal(Json(before), Json(source.Capture()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void InvalidOrUnrepresentableEpochIsRejectedBeforeAnyCellExists(double epoch)
    {
        Assert.Throws<ArgumentException>(() => Source(epoch));
        Assert.Throws<ArgumentException>(() => new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold, initialNeuralTimeMs: epoch));
    }

    [Fact]
    public void ExplicitZeroRetainsExistingDefaultStateAndFingerprint()
    {
        var implicitZero = new ReceptorMotorSource(MotoneuronReferenceParameters.CatLowThreshold, [Contact], Session, Identity,
            maximumStepMs: .00390625, sampleMs: .125, crossingInterpolation: MotoneuronCrossingInterpolation.CubicHermite);
        Assert.Equal(Json(implicitZero.Capture()), Json(Source(0).Capture()));
        Assert.NotEqual(implicitZero.ConfigurationFingerprint, Source(1024).ConfigurationFingerprint);
        Assert.NotEqual(Source(1024).ConfigurationFingerprint, Source(1025).ConfigurationFingerprint);
    }

    [Fact]
    public void PreEpochEventsAreRejectedEvenIfTheirAxonWouldArriveLater()
    {
        var source = Source(1024); string before = Json(source.Capture());
        Assert.Throws<ArgumentException>(() => source.Admit(Session, Contact.ContactId, 1, 1023.5, []));
        Assert.Throws<ArgumentException>(() => source.Admit(Session, Contact.ContactId, 1, 1025, [Event(1024 - .0625)]));
        Assert.Equal(before, Json(source.Capture()));
        var receipt = source.Admit(Session, Contact.ContactId, 1, 1025, [Event(1024)]);
        Assert.Equal(1, receipt.Events); Assert.Equal(0, source.Capture().Cell.DeliveredEvents);
        source.AdvanceOneSample(); Assert.Equal(1, source.Capture().Cell.DeliveredEvents);
        Assert.Equal(1024.125, source.Capture().Cell.Contacts[0].LastReleaseMs);
    }

    [Fact]
    public void DyadicClockTranslationPreservesActivePhysicalStatesAndOriginalEventTimes()
    {
        var zero = Source(0); var shifted = Source(1024);
        zero.Admit(Session, Contact.ContactId, 1, 8, [Event(.125), Event(3.125, 1)]);
        var originals = new[] { Event(1024.125), Event(1027.125, 1) }; string originalJson = Json(originals);
        shifted.Admit(Session, Contact.ContactId, 1, 1032, originals);
        for (int i = 0; i < 64; i++)
        {
            var a = zero.AdvanceOneSample(); var b = shifted.AdvanceOneSample();
            Assert.Equal(a.Cell.TimeMs + 1024, b.Cell.TimeMs);
            Assert.Equal(a.Cell.MembraneState, b.Cell.MembraneState);
            Assert.Equal(a.Cell.Contacts[0].Value.OpenFraction, b.Cell.Contacts[0].Value.OpenFraction);
            Assert.Equal(a.Cell.DeliveredEvents, b.Cell.DeliveredEvents);
            Assert.Equal(a.Cell.IntegrationSteps, b.Cell.IntegrationSteps);
            Assert.Equal(a.Emissions.Count, b.Emissions.Count);
            for (int e = 0; e < a.Emissions.Count; e++)
                Assert.InRange(Math.Abs(a.Emissions[e].FiredAtNeuralMs + 1024 - b.Emissions[e].FiredAtNeuralMs), 0, 2 * (Math.BitIncrement(1032d) - 1032));
        }
        Assert.Equal(originalJson, Json(originals)); Assert.Equal(2, shifted.Capture().Cell.DeliveredEvents);
    }

    [Fact]
    public void ActualMotorTransportUsesDeclaredEpochWithoutChangingCellState()
    {
        var source = Source(1010); var before = Json(source.Capture()); var clock = new MotorAxonClock(Session, 1010, 10);
        Assert.Throws<ArgumentException>(() => new ReceptorMotorLane(source, Anatomy, new(Session, 0, 10)));
        var lane = new ReceptorMotorLane(source, Anatomy, clock); Assert.Equal(before, Json(lane.CaptureSource()));
        lane.Admit(Session, Contact.ContactId, 1, 1011, []);
        for (int i = 0; i < 8; i++) lane.AdvanceOneSample();
        var batch = lane.PublishCompleted(); var receiver = new AvatarMotorAxonReceiver(Anatomy, clock);
        lane.Acknowledge(receiver.AcceptWithAcknowledgement(batch));
        Assert.Equal(1011, batch.CompleteThroughNeuralMs); Assert.Equal(11, receiver.CompleteThroughBodyMs);
        Assert.Empty(receiver.AdvanceTo(11)); Assert.Equal(1011, lane.CaptureSource().Cell.TimeMs);
    }

    static (DescendingMotorPool Pool, VestibularProjectionOutbox Box) Pool(double epoch)
    {
        var box = new VestibularProjectionOutbox(Input, "L", epoch, .125, [Projection],
            [new(StructureId.SpinalCordMotor, PoolName, "L", new Uri("http://127.0.0.1:1"))]);
        var pool = new DescendingMotorPool(Session, PoolName,
            [new(Identity, MotoneuronReferenceParameters.CatLowThreshold, [Contact], SampleMs: .125, MaximumStepMs: .00390625)],
            [new(Input, box.Fingerprint, epoch, .125, [Projection])]);
        return (pool, box);
    }

    [Fact]
    public void OriginalNonzeroProjectionClockReachesDetailedMotorAndBodyUnchanged()
    {
        var (pool, box) = Pool(1010); Assert.Equal(1010, pool.Capture().KnownSourceThroughMs);
        Assert.Throws<InvalidOperationException>(() => pool.PublishCompleted());
        var clock = new MotorAxonClock(Session, 1010, 10); pool.AttachMotorOutput(Anatomy, clock);
        var original = Event(1010.125); string originalJson = Json(original);
        var steps = Enumerable.Range(1, 8).Select(tick => Json(new StructureStepResult(
            new(StructureId.VestibularNuclei, tick, tick == 1 ? 1 : 0, 0, 0, 0, tick == 1 ? 1 : 0, 256, default, new()),
            tick == 1 ? [original] : [], []))).ToArray();
        string frame = Json(new { ExplicitPrescribedEpochControl = true, steps });
        var staged = box.Stage(1, frame, VestibularProjectionOutbox.Hash(frame), 8, 1011, steps);
        var packet = Assert.Single(staged.Packets); box.Accept(pool.Admit(packet)); box.Release(staged.Sha256);
        var output = pool.PublishCompleted(); var cell = Assert.Single(pool.Capture().Cells);
        Assert.Equal(8, cell.CompletedSamples); Assert.Equal(1011, cell.Cell.TimeMs); Assert.Equal(1, cell.Cell.DeliveredEvents);
        Assert.Equal(originalJson, Json(original)); Assert.Contains(original.MessageId.ToString(), output.CanonicalJson);
        var receiver = new AvatarMotorAxonReceiver(Anatomy, clock);
        foreach (var batch in output.MotorBatches) pool.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(batch));
        pool.Acknowledge(output.Sha256); Assert.Equal(11, receiver.CompleteThroughBodyMs);
    }

    [Fact]
    public void MixedInputEpochsCannotInitializeOneMotorPool()
    {
        var inputs = new[] {
            new VestibularProjectionInput(Input, new string('A',64),1010,.125,[Projection]),
            new VestibularProjectionInput(Input+"-other",new string('B',64),1011,.125,[Projection]) };
        Assert.Throws<ArgumentException>(() => new DescendingMotorPool(Session,PoolName,
            [new(Identity,MotoneuronReferenceParameters.CatLowThreshold,[Contact])],inputs));
    }
}
