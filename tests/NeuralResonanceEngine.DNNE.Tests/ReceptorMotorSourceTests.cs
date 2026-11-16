using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ReceptorMotorSourceTests
{
    private static readonly Guid Session = Guid.Parse("cd2c51b8-9ba0-46e0-837a-dd60ae28fdcf");
    private static readonly ReceptorMotorIdentity Identity = new("receptor-motor-L", "motor-7",
        Guid.Parse("18eb9a67-9334-47fb-957f-f4b99e16ac40"), Guid.Parse("4b139106-6914-45a0-b0e2-8b993811792d"));
    private static MotorReceptorDefinition Input(int compartment = 1, bool inhibitory = false) => new(
        inhibitory ? "fixture-inh" : "fixture-exc", StructureId.PremotorCortex, "fixture-cell",
        StableNeuralIdentity.Create("receptor-source-test-synapse", inhibitory),
        StableNeuralIdentity.Create("receptor-source-test-projection", inhibitory),
        Identity.Instance, Identity.Neuron, 7, (inhibitory ? "inh-" : "exc-") + compartment, compartment,
        inhibitory ? MotorReceptorKind.GabaAReference : MotorReceptorKind.AmpaReference, 10_000, .125);
    private static MotorAxonAnatomy Anatomy() => new([
        MotorAxonContact.Create(Identity.Instance, Identity.Neuron, Identity.Projection, "fixture-unit", 2.0625)]);
    private static MotorAxonClock Clock(double epoch = 0) => new(Session, epoch, 0);
    private static string Bytes(object value) => JsonSerializer.Serialize(value);
    private static ReceptorMotorSource Quiet() => new(MotoneuronReferenceParameters.CatLowThreshold, [Input()], Session, Identity);

    private static (ReceptorMotorSource Source, MotorReceptorDefinition[] Inputs) Passive(double sampleMs = .1)
    {
        // An explicitly labelled passive cable control. Its genuine voltage
        // crossings test event wiring, not active neuronal firing physiology.
        var p = MotoneuronReferenceParameters.CatLowThreshold;
        var section = p.Dendrite with { LeakSiemensPerCm2 = 0 };
        var inputs = Enumerable.Range(0, 5).Select(i => Input(i))
            .Concat(Enumerable.Range(0, 5).Select(i => Input(i, true))).ToArray();
        return (new(p with { Soma = section, Dendrite = section }, inputs, Session, Identity,
            initialVoltageMv: -71, block: MotoneuronDiagnosticBlock.Passive, sampleMs: sampleMs), inputs);
    }

    private static void Drive(Func<Guid, Guid, long, double, IReadOnlyList<SpikeMessage>, MotorReceptorBatchReceipt> admit,
        IEnumerable<MotorReceptorDefinition> inputs)
    {
        foreach (var d in inputs)
        {
            double[] times = d.Kind == MotorReceptorKind.AmpaReference ? [.25, 20.25, 40.25] : [10.25, 30.25];
            var events = times.Select(t => new SpikeMessage
            {
                MessageId = StableNeuralIdentity.Create("receptor-source-test-input", Session, d.SourceSynapse, t),
                TimestampMs = t, SourceInstanceKey = d.SourceInstance, SourceStructure = d.SourceStructure,
                SourceNeuronId = d.SourceNeuron, ProjectionId = d.Projection, SynapseId = d.SourceSynapse,
                TargetStructure = StructureId.SpinalCordMotor, TargetNeuronId = d.TargetNeuron,
                Neurotransmitter = d.Transmitter, VesicleQuanta = 1
            }).ToArray();
            admit(Session, d.ContactId, 1, 80, events);
        }
    }

    [Fact]
    public void Missing_Input_Cannot_Advance_Or_Publish_Silence_And_Can_Be_Recovered()
    {
        var lane = new ReceptorMotorLane(Quiet(), Anatomy(), Clock());
        string before = Bytes(lane.CaptureSource());
        Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
        Assert.Throws<InvalidOperationException>(() => lane.PublishCompleted());
        Assert.Equal(before, Bytes(lane.CaptureSource()));
        lane.Admit(Session, Input().ContactId, 1, 0, []); // Completeness plus .125 ms propagation allows only the first sample.
        Assert.Equal(.1, lane.AdvanceOneSample().Cell.TimeMs);
        var batch = lane.PublishCompleted(); Assert.Empty(batch.Spikes);
        Assert.Equal(.1, batch.CompleteThroughNeuralMs);
        before = Bytes(lane.CaptureSource());
        Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
        Assert.Equal(before, Bytes(lane.CaptureSource()));
        lane.Admit(Session, Input().ContactId, 2, 1, []);
        Assert.Equal(.2, lane.AdvanceOneSample().Cell.TimeMs); Assert.False(lane.CaptureSource().Faulted);
    }

    [Fact]
    public void Readout_Preserves_The_Unwrapped_Active_Membrane_State_Bitwise()
    {
        var d = Input(); var source = Quiet();
        var bare = new ReceptorDrivenMotoneuron(MotoneuronReferenceParameters.CatLowThreshold, [d], Session);
        source.Admit(Session, d.ContactId, 1, 2, []); bare.Admit(Session, d.ContactId, 1, 2, []);
        for (int i = 1; i <= 10; ++i)
        {
            bare.AdvanceTo(i * .1); var actual = source.AdvanceOneSample();
            Assert.Equal(Bytes(bare.Capture()), Bytes(actual.Cell));
            Assert.Equal(actual.Cell.Crossings, actual.Emissions.Select(e => e.FiredAtNeuralMs));
        }
    }

    [Fact]
    public void Output_Identity_Must_Be_The_Target_Of_The_Receptor_Contacts()
    {
        Assert.Throws<ArgumentException>(() => new ReceptorMotorSource(MotoneuronReferenceParameters.CatLowThreshold,
            [Input()], Session, Identity with { Neuron = "some-other-cell" }));
        Assert.Throws<ArgumentException>(() => new ReceptorMotorSource(MotoneuronReferenceParameters.CatLowThreshold,
            [Input()], Session, Identity with { Instance = "another-instance" }));
    }

    [Fact]
    public void Quiet_Prehistory_Attaches_Without_Reset_And_Transfers_Exclusive_Ownership()
    {
        var source = Quiet(); source.Admit(Session, Input().ContactId, 1, 2, []); source.AdvanceOneSample();
        string before = Bytes(source.Capture());
        var lane = new ReceptorMotorLane(source, Anatomy(), Clock(.1));
        Assert.Equal(before, Bytes(lane.CaptureSource()));
        Assert.Throws<InvalidOperationException>(() => source.AdvanceOneSample());
        Assert.Throws<InvalidOperationException>(() => source.Admit(Session, Input().ContactId, 2, 3, []));
        Assert.Throws<InvalidOperationException>(() => new ReceptorMotorLane(source, Anatomy(), Clock(.1)));
        Assert.Throws<InvalidOperationException>(() => lane.PublishCompleted());
        var receiver = new AvatarMotorAxonReceiver(Anatomy(), Clock(.1));
        lane.AdvanceOneSample(); lane.Acknowledge(receiver.AcceptWithAcknowledgement(lane.PublishCompleted()));
        Assert.Equal(.1, receiver.CompleteThroughBodyMs);
    }

    [Fact]
    public void A_Cell_Cannot_Claim_Another_Session_Epoch_Or_Population()
    {
        var source = Quiet(); string before = Bytes(source.Capture());
        Assert.Throws<ArgumentException>(() => new ReceptorMotorLane(source, Anatomy(), Clock() with { SessionId = Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => new ReceptorMotorLane(source, Anatomy(), Clock(.1)));
        var population = new MotorAxonAnatomy(Anatomy().Contacts.Append(
            MotorAxonContact.Create(Identity.Instance, "second-cell", Guid.NewGuid(), "second-unit", 2)));
        Assert.Throws<ArgumentException>(() => new ReceptorMotorLane(source, population, Clock()));
        Assert.Equal(before, Bytes(source.Capture()));
        _ = new ReceptorMotorLane(source, Anatomy(), Clock());
    }

    [Fact]
    public void Diagnostic_Crossings_Need_Explicit_Opt_In_And_Cannot_Be_Discarded_As_Prehistory()
    {
        var fixture = Passive(64); var source = fixture.Source;
        Assert.Throws<ArgumentException>(() => new ReceptorMotorLane(source, Anatomy(), Clock()));
        Drive(source.Admit, fixture.Inputs); var state = source.AdvanceOneSample();
        Assert.Equal(3, state.Emissions.Count); Assert.Equal(MotoneuronDiagnosticBlock.Passive, state.DiagnosticBlock);
        Assert.Throws<ArgumentException>(() => new ReceptorMotorLane(source, Anatomy(), Clock(64), allowDiagnosticSource: true));
        Assert.Equal(Bytes(state), Bytes(source.Capture()));
    }

    [Fact]
    public void Actual_Observed_Crossings_Reach_Avatar_With_Original_Identity_And_Delay()
    {
        var fixture = Passive(64); var anatomy = Anatomy(); var clock = Clock();
        var lane = new ReceptorMotorLane(fixture.Source, anatomy, clock, allowDiagnosticSource: true);
        Drive(lane.Admit, fixture.Inputs); var state = lane.AdvanceOneSample();
        Assert.Equal(3, state.Cell.Crossings.Length);
        var batch = lane.PublishCompleted(); string frozen = Bytes(lane.CaptureSource());
        var receiver = new AvatarMotorAxonReceiver(anatomy, clock);
        var receipt = receiver.AcceptWithAcknowledgement(batch);
        Assert.Equal(receipt, receiver.AcceptWithAcknowledgement(Assert.Single(lane.Unacknowledged())));
        lane.Acknowledge(receipt); lane.Acknowledge(receipt);
        Assert.Equal(frozen, Bytes(lane.CaptureSource())); Assert.Empty(lane.Unacknowledged());
        var arrivals = receiver.AdvanceTo(64); Assert.Equal(3, arrivals.Count);
        for (int i = 0; i < arrivals.Count; ++i)
        {
            var original = state.Emissions[i]; var actual = arrivals[i];
            Assert.Equal(state.Cell.Crossings[i], original.FiredAtNeuralMs);
            Assert.Equal(original.MessageId, actual.MessageId); Assert.Equal(original.Ordinal, actual.Ordinal);
            Assert.Equal(original.FiredAtNeuralMs, actual.FiredAtNeuralMs);
            Assert.Equal(original.FiredAtNeuralMs + 2.0625, actual.ArrivalAtBodyMs);
            Assert.Equal(Identity.Instance, actual.SourceInstanceKey); Assert.Equal(Identity.Neuron, actual.SourceNeuronId);
            Assert.Equal(Identity.Projection, actual.ProjectionId); Assert.Equal(Identity.Synapse, actual.SourceSynapseId);
        }
        Assert.Empty(receiver.AdvanceTo(64));
    }

    [Fact]
    public void Small_Packets_Drain_A_Completed_Source_Prefix_Without_Dropping_The_Suffix()
    {
        var fixture = Passive(64); var lane = new ReceptorMotorLane(fixture.Source, Anatomy(), Clock(),
            maximumBatches: 1, maximumEvents: 1, allowDiagnosticSource: true);
        Drive(lane.Admit, fixture.Inputs); var state = lane.AdvanceOneSample();
        Assert.Equal(3, lane.UnpublishedEmissions); string before = Bytes(state);
        var receiver = new AvatarMotorAxonReceiver(Anatomy(), Clock());
        for (int i = 0; i < 3; ++i)
        {
            var batch = lane.PublishCompleted(); var spike = Assert.Single(batch.Spikes);
            Assert.Equal(state.Emissions[i].MessageId, spike.MessageId);
            Assert.Equal(i == 2 ? 64 : spike.FiredAtNeuralMs, batch.CompleteThroughNeuralMs);
            Assert.Equal(2 - i, lane.UnpublishedEmissions);
            Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
            Assert.Equal(before, Bytes(lane.CaptureSource()));
            lane.Acknowledge(receiver.AcceptWithAcknowledgement(batch));
        }
        Assert.Equal(64, lane.PublishedThroughNeuralMs); Assert.Equal(3, receiver.AdvanceTo(64).Count);
        Assert.Throws<InvalidOperationException>(() => lane.PublishCompleted());
    }

    [Fact]
    public void Full_Retained_Batch_Stops_Advancement_Until_The_Same_Batch_Is_Acknowledged()
    {
        var lane = new ReceptorMotorLane(Quiet(), Anatomy(), Clock(), maximumBatches: 1);
        lane.Admit(Session, Input().ContactId, 1, 1, []); lane.AdvanceOneSample(); var batch = lane.PublishCompleted();
        string before = Bytes(lane.CaptureSource());
        Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
        Assert.Equal(before, Bytes(lane.CaptureSource())); Assert.Same(batch, Assert.Single(lane.Unacknowledged()));
        var receiver = new AvatarMotorAxonReceiver(Anatomy(), Clock());
        lane.Acknowledge(receiver.AcceptWithAcknowledgement(batch));
        Assert.Equal(.2, lane.AdvanceOneSample().Cell.TimeMs);
    }

    [Fact]
    public void Publication_Cadence_Does_Not_Change_Neuronal_State_Or_Event_Identity()
    {
        var a = Passive(); var b = Passive();
        var fast = new ReceptorMotorLane(a.Source, Anatomy(), Clock(), allowDiagnosticSource: true);
        var slow = new ReceptorMotorLane(b.Source, Anatomy(), Clock(), allowDiagnosticSource: true);
        Drive(fast.Admit, a.Inputs); Drive(slow.Admit, b.Inputs);
        var first = new AvatarMotorAxonReceiver(Anatomy(), Clock()); var second = new AvatarMotorAxonReceiver(Anatomy(), Clock());
        for (int i = 1; i <= 640; ++i)
        {
            Assert.Equal(Bytes(fast.AdvanceOneSample()), Bytes(slow.AdvanceOneSample()));
            fast.Acknowledge(first.AcceptWithAcknowledgement(fast.PublishCompleted()));
            if (i % 10 == 0) slow.Acknowledge(second.AcceptWithAcknowledgement(slow.PublishCompleted()));
        }
        Assert.Equal(3, fast.CaptureSource().Emissions.Count);
        Assert.Equal(Bytes(first.AdvanceTo(64)), Bytes(second.AdvanceTo(64)));
    }

    [Fact]
    public void Full_Epoch_Retains_Unpublishable_Crossings_Without_Advancing_Or_Reidentifying_Them()
    {
        var fixture = Passive(64); var lane = new ReceptorMotorLane(fixture.Source, Anatomy(), Clock(),
            maximumEpochEvents: 1, allowDiagnosticSource: true);
        Drive(lane.Admit, fixture.Inputs); var state = lane.AdvanceOneSample();
        var batch = lane.PublishCompleted(); Assert.Single(batch.Spikes);
        Assert.Equal(state.Emissions[0].FiredAtNeuralMs, batch.CompleteThroughNeuralMs);
        var receiver = new AvatarMotorAxonReceiver(Anatomy(), Clock()); lane.Acknowledge(receiver.AcceptWithAcknowledgement(batch));
        string before = Bytes(lane.CaptureSource());
        Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
        Assert.Throws<InvalidOperationException>(() => lane.PublishCompleted());
        Assert.Equal(before, Bytes(lane.CaptureSource())); Assert.Equal(2, lane.UnpublishedEmissions);
    }

    [Fact]
    public void Wrong_Receipt_Cannot_Release_Backpressure_Or_Advance_The_Cell()
    {
        var lane = new ReceptorMotorLane(Quiet(), Anatomy(), Clock(), maximumBatches: 1);
        lane.Admit(Session, Input().ContactId, 1, 1, []); lane.AdvanceOneSample(); var batch = lane.PublishCompleted();
        string before = Bytes(lane.CaptureSource());
        var receipt = MotorAxonBatchIntegrity.Receipt(batch, MotorAxonBatchIntegrity.Digest(batch));
        Assert.Throws<ArgumentException>(() => lane.Acknowledge(receipt with { BatchSha256 = new string('0', 64) }));
        Assert.Throws<InvalidOperationException>(() => lane.AdvanceOneSample());
        Assert.Same(batch, Assert.Single(lane.Unacknowledged())); Assert.Equal(before, Bytes(lane.CaptureSource()));
    }

    [Fact]
    public void Detached_Snapshots_Cannot_Edit_The_Private_Cell_Or_Its_Original_Emissions()
    {
        var fixture = Passive(64); Drive(fixture.Source.Admit, fixture.Inputs); fixture.Source.AdvanceOneSample();
        var snapshot = fixture.Source.Capture(); string before = Bytes(snapshot);
        snapshot.Cell.MembraneState[0] = 900; snapshot.Cell.Crossings[0] = 800;
        Assert.Throws<NotSupportedException>(() => ((IList<ReceptorMotorEmission>)snapshot.Emissions)[0] = snapshot.Emissions[0] with { FiredAtNeuralMs = 700 });
        Assert.Equal(before, Bytes(fixture.Source.Capture()));
    }

    [Fact]
    public void Other_Threads_Cannot_Admit_Advance_Publish_Or_Acknowledge()
    {
        var lane = new ReceptorMotorLane(Quiet(), Anatomy(), Clock());
        lane.Admit(Session, Input().ContactId, 1, 1, []); lane.AdvanceOneSample(); var batch = lane.PublishCompleted();
        var receipt = MotorAxonBatchIntegrity.Receipt(batch, MotorAxonBatchIntegrity.Digest(batch));
        string before = Bytes(lane.CaptureSource()); var failures = new List<Exception>();
        var thread = new Thread(() =>
        {
            Action[] actions = [() => lane.Admit(Session, Input().ContactId, 2, 2, []),
                () => lane.AdvanceOneSample(), () => lane.PublishCompleted(), () => lane.Acknowledge(receipt)];
            foreach (var action in actions) try { action(); } catch (Exception error) { failures.Add(error); }
        });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Equal(4, failures.Count); Assert.All(failures, e => Assert.IsType<InvalidOperationException>(e));
        Assert.Equal(before, Bytes(lane.CaptureSource())); Assert.Same(batch, Assert.Single(lane.Unacknowledged()));
    }
}
