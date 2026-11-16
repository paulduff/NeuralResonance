using System.Text.Json;
using System.Numerics;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ReceptorDrivenMotoneuronTests
{
    private static readonly Guid Session = Guid.Parse("6aa81be7-f36f-4548-9cfa-08139ec168cd");
    private static MotorReceptorDefinition Definition(int compartment = 1, bool inhibitory = false) => new("source-L",
        StructureId.PremotorCortex, "source-3", Guid.Parse("e01dfce8-be84-4efb-8864-2286173df48c"),
        Guid.Parse("abc06ed4-4a86-4529-a3bd-01ed3fd8f9e8"), "spinal-L", "motor-7", 7, "site-" + compartment, compartment,
        inhibitory ? MotorReceptorKind.GabaAReference : MotorReceptorKind.AmpaReference, 100, .125);
    private static SpikeMessage Event(MotorReceptorDefinition d, double fired, Guid? id = null) => new()
    {
        MessageId = id ?? Guid.NewGuid(), TimestampMs = fired, SourceInstanceKey = d.SourceInstance,
        SourceStructure = d.SourceStructure, SourceNeuronId = d.SourceNeuron, ProjectionId = d.Projection,
        SynapseId = d.SourceSynapse, TargetStructure = StructureId.SpinalCordMotor, TargetNeuronId = d.TargetNeuron,
        Neurotransmitter = d.Transmitter, VesicleQuanta = 1
    };
    private static string Snapshot(ReceptorDrivenMotoneuron c) => JsonSerializer.Serialize(c.Capture());
    private static MotoneuronReferenceParameters UniformPassive()
    {
        var p = MotoneuronReferenceParameters.CatLowThreshold;
        var s = p.Dendrite with { LeakSiemensPerCm2 = 0 };
        return p with { Soma = s, Dendrite = s };
    }
    private static ReceptorDrivenMotoneuron Cell(params MotorReceptorDefinition[] definitions)
        => new(MotoneuronReferenceParameters.CatLowThreshold, definitions, Session,
            initialVoltageMv: -71, block: MotoneuronDiagnosticBlock.Passive);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UniformPassiveCableMatchesIntegratedConductanceClosedForm(bool inhibitory)
    {
        var p = UniformPassive(); var definitions = Enumerable.Range(0, 5).Select(i => Definition(i, inhibitory)).ToArray();
        var cell = new ReceptorDrivenMotoneuron(p, definitions, Session, -71, MotoneuronDiagnosticBlock.Passive);
        // Identical input per equal-capacitance compartment removes axial current.
        // The independently integrated ODE is V = E + (V0-E)*exp(-integral(g)/C).
        var original = Event(definitions[0], .25);
        foreach (var d in definitions) cell.Admit(Session, d.ContactId, 1, 4, [original]);
        cell.AdvanceTo(3.375, .00390625); // 1 ms transmitter + 2 ms decay after .375 arrival.
        double alpha = inhibitory ? 5 : 1.1, beta = inhibitory ? .18 : .19, reversal = inhibitory ? -80 : 0;
        double rate = alpha + beta, onFraction = alpha / rate;
        double integratedOpenMs = onFraction * (1 - (1 - Math.Exp(-rate)) / rate) +
            onFraction * (1 - Math.Exp(-rate)) * (1 - Math.Exp(-2 * beta)) / beta;
        double capacitance = .01 * Math.PI * p.Soma.DiameterUm * p.Soma.LengthUm * p.Soma.CapacitanceUfPerCm2;
        double expected = reversal + (-71 - reversal) * Math.Exp(-100 * integratedOpenMs / capacitance);
        var state = cell.Capture();
        Assert.All(state.MembraneState.Take(5), v => Assert.InRange(Math.Abs(v - expected), 0, 1e-7));
        Assert.InRange(state.MaximumChargeLedgerErrorFc, 0, 1e-6);
        Assert.Equal(5, state.DeliveredEvents); Assert.False(state.Faulted); Assert.Empty(state.Crossings);
    }

    [Fact]
    public void AccountedSilencePreservesAllOriginalMembraneStatesBitwise()
    {
        var d = Definition();
        var candidate = new ReceptorDrivenMotoneuron(MotoneuronReferenceParameters.CatLowThreshold, [d], Session);
        var original = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold, accumulation: MotoneuronAccumulation.Compensated);
        candidate.Admit(Session, d.ContactId, 1, 8, []);
        for (int i = 0; i < 1000; ++i)
        {
            original.Advance(.0078125); candidate.AdvanceTo(original.TimeMs, .0078125);
            Assert.Equal(original.CopyState(), candidate.Capture().MembraneState);
        }
        Assert.Equal(original.CopyCrossings(), candidate.Capture().Crossings);
        Assert.Contains("point-receptors-stage-evaluated", candidate.Capture().IntegrationVersion);
        Assert.Equal(MotoneuronReferenceCell.CompensatedVersion, original.IntegrationVersion);
    }

    [Fact]
    public void OneDendriticContactDoesNotBecomeUniformDendriticInjection()
    {
        var d = Definition(2); var cell = Cell(d);
        cell.Admit(Session, d.ContactId, 1, 1, [Event(d, .25)]); cell.AdvanceTo(.5, .00390625);
        var state = cell.Capture();
        Assert.True(state.MembraneState[2] > state.MembraneState[1]);
        Assert.True(state.MembraneState[2] > state.MembraneState[3]);
        Assert.True(state.MembraneState[2] > -71);
        Assert.Equal(2, Assert.Single(state.Contacts).Value.ElectricalCompartment);
        Assert.InRange(state.MaximumChargeLedgerErrorFc, 0, 1e-6);
    }

    [Fact]
    public void MissingOrIncompleteInputStopsBeforeAnyMembraneOrContactAdvance()
    {
        var a = Definition(1); var b = Definition(2); var cell = Cell(a, b);
        cell.Admit(Session, a.ContactId, 1, 2, []); string before = Snapshot(cell);
        Assert.Throws<InvalidOperationException>(() => cell.AdvanceTo(1)); Assert.Equal(before, Snapshot(cell));
        cell.Admit(Session, b.ContactId, 1, .5, []); before = Snapshot(cell);
        Assert.Throws<InvalidOperationException>(() => cell.AdvanceTo(1)); Assert.Equal(before, Snapshot(cell));
        cell.Admit(Session, b.ContactId, 2, 2, []); cell.AdvanceTo(1, .0078125);
        Assert.Equal(1, cell.Capture().TimeMs); Assert.False(cell.Capture().Faulted);
    }

    [Fact]
    public void PacketBoundariesAndRetainedReplayDoNotChangeSameStepTrajectory()
    {
        var d = Definition(); var a = Cell(d); var b = Cell(d);
        var first = Event(d, .375); var second = Event(d, 2.375);
        a.Admit(Session, d.ContactId, 1, 4, [first, second]);
        var receipt = b.Admit(Session, d.ContactId, 1, 2, [first]);
        a.AdvanceTo(2, .0078125); b.AdvanceTo(2, .0078125); string before = Snapshot(b);
        Assert.Equal(receipt, b.Admit(Session, d.ContactId, 1, 2, [first])); Assert.Equal(before, Snapshot(b));
        b.Admit(Session, d.ContactId, 2, 4, [second]);
        a.AdvanceTo(4, .0078125); b.AdvanceTo(4, .0078125);
        Assert.Equal(a.Capture().MembraneState, b.Capture().MembraneState);
        Assert.Equal(a.Capture().Contacts, b.Capture().Contacts);
        Assert.Equal(2, b.Capture().DeliveredEvents);
    }

    [Fact]
    public void CallerMutationCannotChangeAlreadyAdmittedOriginalEvents()
    {
        var d = Definition(); var a = Cell(d); var b = Cell(d); var original = Event(d, .375);
        a.Admit(Session, d.ContactId, 1, 2, [original]); b.Admit(Session, d.ContactId, 1, 2, [original]);
        original.TimestampMs = 1.5; original.VesicleQuanta = 99;
        string before = Snapshot(a);
        Assert.Throws<ArgumentException>(() => a.Admit(Session, d.ContactId, 1, 2, [original]));
        Assert.Equal(before, Snapshot(a));
        a.AdvanceTo(2, .0078125); b.AdvanceTo(2, .0078125);
        Assert.Equal(a.Capture().MembraneState, b.Capture().MembraneState);
        Assert.Equal(.5, a.Capture().Contacts[0].LastReleaseMs);
    }

    [Fact]
    public void FanoutCannotRewriteTheSameOriginalEventBetweenContacts()
    {
        var a = Definition(1); var b = Definition(2); var cell = Cell(a, b);
        var original = Event(a, .375); cell.Admit(Session, a.ContactId, 1, 2, [original]);
        var changed = Event(b, .375, original.MessageId); changed.VesicleQuanta = 2;
        string before = Snapshot(cell);
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, b.ContactId, 1, 2, [changed]));
        Assert.Equal(before, Snapshot(cell));
        cell.Admit(Session, b.ContactId, 1, 2, [original]); cell.AdvanceTo(1, .0078125);
        Assert.Equal(2, cell.Capture().DeliveredEvents);
    }

    [Fact]
    public void InvalidBatchIsAtomicAndDoesNotExtendCompleteness()
    {
        var d = Definition(); var cell = Cell(d); var first = Event(d, .375);
        cell.Admit(Session, d.ContactId, 1, 1, [first]); string before = Snapshot(cell);
        Assert.Throws<ArgumentException>(() => cell.Admit(Guid.NewGuid(), d.ContactId, 2, 4, []));
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 3, 4, []));
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 2, .5, []));
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 2, 4, [first]));
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 2, 4, [Event(d, 1.125)])); // overlap
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 2, 4, [Event(d, 2), Event(d, 1.5)]));
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 2, 4, [Event(d, 4.5)]));
        var wrong = Event(d, 2); wrong.TargetNeuronId = "another-target";
        Assert.Throws<ArgumentException>(() => cell.Admit(Session, d.ContactId, 2, 4, [wrong]));
        Assert.Equal(before, Snapshot(cell));
    }

    [Fact]
    public void ConsecutivePulsesAndZeroTimeEventRetainExactReleaseBoundaries()
    {
        var d = Definition() with { AxonalDelayMs = 0 }; var cell = Cell(d);
        cell.Admit(Session, d.ContactId, 1, 2, [Event(d, 0), Event(d, 1)]);
        cell.AdvanceTo(0); Assert.Equal(1, cell.Capture().DeliveredEvents);
        cell.AdvanceTo(1, .0078125);
        Assert.Equal(2, cell.Capture().DeliveredEvents); Assert.Equal(1, cell.Capture().Contacts[0].LastReleaseMs);
        cell.AdvanceTo(2, .0078125);
        Assert.Equal(2, cell.Capture().Contacts[0].PulseEndMs); Assert.Equal(0, cell.Capture().Contacts[0].Value.TransmitterMm);
    }

    [Fact]
    public void CapacityStopsBeforeMutationAndRetainsOldBatchReceipt()
    {
        var d = Definition(); var cell = new ReceptorDrivenMotoneuron(MotoneuronReferenceParameters.CatLowThreshold,
            [d], Session, maximumEpochEventsPerContact: 1, maximumEpochBatchesPerContact: 2);
        var first = Event(d, .375); var receipt = cell.Admit(Session, d.ContactId, 1, 1, [first]);
        string before = Snapshot(cell);
        Assert.Throws<InvalidOperationException>(() => cell.Admit(Session, d.ContactId, 2, 3, [Event(d, 2)]));
        Assert.Equal(before, Snapshot(cell));
        cell.Admit(Session, d.ContactId, 2, 2, []); before = Snapshot(cell);
        Assert.Throws<InvalidOperationException>(() => cell.Admit(Session, d.ContactId, 3, 3, []));
        Assert.Equal(before, Snapshot(cell)); Assert.Equal(receipt, cell.Admit(Session, d.ContactId, 1, 1, [first]));
    }

    [Fact]
    public void FractionalBoundariesRetainExactlyTheSuppliedBinaryDuration()
    {
        static BigInteger Units(double time)
        {
            ulong bits = BitConverter.DoubleToUInt64Bits(time);
            int exponent = (int)((bits >> 52) & 2047);
            ulong mantissa = bits & ((1UL << 52) - 1);
            return exponent == 0 ? mantissa : new BigInteger(mantissa | (1UL << 52)) << (exponent - 1);
        }
        double time = 0; BigInteger supplied = 0;
        foreach (double boundary in new[] { .000001, .1, .125000123, .2 })
            while (time < boundary)
            {
                double dt = ReceptorDrivenMotoneuron.BoundaryDuration(time, boundary, .003125);
                Assert.InRange(dt, double.Epsilon, .003125);
                Assert.Equal(Units(dt), Units(time + dt) - Units(time));
                supplied += Units(dt); time += dt;
                Assert.Equal(Units(time), supplied);
            }
        // A maximum step followed by a one-ULP remainder must be repartitioned,
        // retaining all elapsed time and a representable midpoint in both steps.
        time = 1; supplied = Units(time); double closeBoundary = Math.BitIncrement(1.0078125);
        while (time < closeBoundary)
        {
            double dt = ReceptorDrivenMotoneuron.BoundaryDuration(time, closeBoundary, .0078125);
            double end = time + dt, middle = time + dt / 2;
            Assert.True(time < middle && middle < end);
            Assert.InRange(dt, double.Epsilon, .0078125);
            Assert.Equal(Units(dt), Units(end) - Units(time));
            supplied += Units(dt); time = end; Assert.Equal(Units(time), supplied);
        }
        var d = Definition() with { AxonalDelayMs = 0 }; var cell = Cell(d);
        cell.Admit(Session, d.ContactId, 1, 1, [Event(d, .000001)]); cell.AdvanceTo(.2);
        Assert.Equal(.2, cell.Capture().TimeMs); Assert.Equal(1, cell.Capture().DeliveredEvents);
        Assert.False(cell.Capture().Faulted);
    }

    [Fact]
    public void InvalidClockAndForeignOwnerCannotChangeAcceptedState()
    {
        var d = Definition(); var cell = Cell(d); cell.Admit(Session, d.ContactId, 1, 1, []); string before = Snapshot(cell);
        Assert.Throws<ArgumentException>(() => cell.AdvanceTo(-1));
        Assert.Throws<ArgumentException>(() => cell.AdvanceTo(double.NaN));
        Assert.Throws<ArgumentException>(() => cell.AdvanceTo(1, .1));
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(() => cell.Capture())); thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(error); Assert.Equal(before, Snapshot(cell));
        Assert.Throws<ArgumentException>(() => Cell(d, d));
        Assert.Throws<ArgumentException>(() => Cell(d, Definition(2) with { TargetNeuron = "other" }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReversalEquilibriumCannotProduceChargeFromOpenReceptors(bool inhibitory)
    {
        var d = Definition(1, inhibitory); double reversal = inhibitory ? -80 : 0;
        var cell = new ReceptorDrivenMotoneuron(UniformPassive(), [d], Session, reversal, MotoneuronDiagnosticBlock.Passive);
        cell.Admit(Session, d.ContactId, 1, 2, [Event(d, .375)]); cell.AdvanceTo(2, .0078125);
        Assert.All(cell.Capture().MembraneState.Take(5), v => Assert.Equal(reversal, v));
        Assert.Equal(0, cell.Capture().MembraneState[29]);
    }
}
