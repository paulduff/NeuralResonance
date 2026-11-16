using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MotorReceptorContactTests
{
    private static readonly Guid Session = Guid.Parse("b65aaafc-6a8d-40d6-8f74-6c61fffa2584");
    private static MotorReceptorDefinition Definition(bool inhibitory = false) => new("premotor-L",
        StructureId.PremotorCortex, "premotor-17", Guid.Parse("450d9825-2784-4c85-a88f-602045b651c1"),
        Guid.Parse("d8fe96d4-7bb5-4874-905c-31388ce07dd3"), "spinal-L", "motor-3", 3, "dendrite-1-contact-a", 1,
        inhibitory ? MotorReceptorKind.GabaAReference : MotorReceptorKind.AmpaReference, 2, .25);

    private static SpikeMessage Spike(MotorReceptorDefinition d, double arrival, Guid? id = null) => new()
    {
        MessageId = id ?? Guid.NewGuid(), SourceInstanceKey = d.SourceInstance, SourceStructure = d.SourceStructure,
        SourceNeuronId = d.SourceNeuron, SynapseId = d.SourceSynapse, ProjectionId = d.Projection,
        TargetStructure = StructureId.SpinalCordMotor, TargetNeuronId = d.TargetNeuron,
        TimestampMs = arrival - d.AxonalDelayMs, Neurotransmitter = d.Transmitter, IsFeedback = d.IsFeedback,
        VesicleQuanta = 1, ReuptakeRate = 8
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SinglePulseMatchesClosedFormAndLongTailIsNotCutOff(bool inhibitory)
    {
        var d = Definition(inhibitory); var contact = new MotorReceptorContact(d, Session);
        contact.AdvanceTo(1); contact.Release(Session, Spike(d, 1));
        double rate = d.BindingPerMsMm + d.UnbindingPerMs;
        double plateau = d.BindingPerMsMm / rate;
        Assert.Equal(0, contact.Capture().Value.OpenFraction);
        var end = contact.AdvanceTo(2).Value;
        Assert.InRange(Math.Abs(end.OpenFraction - plateau * (1 - Math.Exp(-rate))), 0, 2e-15);
        Assert.Equal(0, end.TransmitterMm);
        var tail = contact.AdvanceTo(102).Value;
        Assert.True(tail.OpenFraction > 0); // The upstream lookup truncates exp(x) below -10; this candidate does not.
        Assert.InRange(Math.Abs(tail.OpenFraction - end.OpenFraction * Math.Exp(-100 * d.UnbindingPerMs)), 0, 1e-22);
        Assert.Equal(end.OpenFraction * 2, end.ConductanceNs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservationPartitionDoesNotChangeReceptorState(bool inhibitory)
    {
        var d = Definition(inhibitory); var a = new MotorReceptorContact(d, Session); var b = new MotorReceptorContact(d, Session);
        var first = Spike(d, 1); var second = Spike(d, 4);
        foreach (var c in new[] { a, b }) { c.AdvanceTo(1); c.Release(Session, first); }
        for (int i = 1; i <= 3000; ++i) a.AdvanceTo(1 + i / 1000.0);
        b.AdvanceTo(4);
        Assert.Equal(a.Capture(), b.Capture());
        foreach (var c in new[] { a, b }) c.Release(Session, second);
        for (int i = 1; i <= 7000; ++i) a.AdvanceTo(4 + i / 1000.0);
        b.AdvanceTo(11);
        Assert.Equal(a.Capture(), b.Capture());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentUsesConductanceUnitsAndActualReversal(bool inhibitory)
    {
        var d = Definition(inhibitory); var c = new MotorReceptorContact(d, Session);
        c.AdvanceTo(1); c.Release(Session, Spike(d, 1)); var value = c.AdvanceTo(1.5).Value;
        Assert.Equal(0, value.InwardCurrentPa(d.ReversalMv));
        Assert.Equal(value.ConductanceNs * 10, value.InwardCurrentPa(d.ReversalMv - 10));
        Assert.Equal(-value.ConductanceNs * 10, value.InwardCurrentPa(d.ReversalMv + 10));
        Assert.Throws<ArgumentException>(() => value.InwardCurrentPa(double.NaN));
    }

    [Fact]
    public void DistinctSitesRetainIndependentSaturatingOccupancyAndSameOriginalEvent()
    {
        var d = Definition(); var another = d with { TargetSite = "dendrite-1-contact-b" };
        var a = new MotorReceptorContact(d, Session); var b = new MotorReceptorContact(another, Session);
        Assert.NotEqual(a.Capture().ContactId, b.Capture().ContactId);
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
        for (int i = 1; i <= 12; ++i)
        {
            var original = Spike(d, 2 * i);
            a.AdvanceTo(2 * i); b.AdvanceTo(2 * i);
            var ar = a.Release(Session, original); var br = b.Release(Session, original);
            Assert.Equal(ar.EventId, br.EventId); Assert.NotEqual(ar.ContactId, br.ContactId);
        }
        var av = a.AdvanceTo(25).Value; var bv = b.AdvanceTo(25).Value;
        Assert.Equal(av, bv); Assert.InRange(av.OpenFraction, 0, 1);
        Assert.Equal(2 * av.ConductanceNs, av.ConductanceNs + bv.ConductanceNs);
        Assert.Equal(12, a.Capture().AcceptedEvents); Assert.Equal(12, b.Capture().AcceptedEvents);
        var untouched = b.Capture();
        a.Release(Session, Spike(d, 25));
        a.AdvanceTo(26);
        Assert.Equal(untouched, b.Capture());
        b.AdvanceTo(26);
        Assert.True(a.Capture().Value.OpenFraction > b.Capture().Value.OpenFraction);
        Assert.Equal(13, a.Capture().AcceptedEvents); Assert.Equal(12, b.Capture().AcceptedEvents);
    }

    [Fact]
    public void ExactReplayIsInertAfterTimeAdvancesAndMetadataConflictIsRejected()
    {
        var d = Definition(); var c = new MotorReceptorContact(d, Session); var original = Spike(d, 1);
        c.AdvanceTo(1); var receipt = c.Release(Session, original); c.AdvanceTo(4); var before = c.Capture();
        Assert.Equal(receipt, c.Release(Session, original)); Assert.Equal(before, c.Capture());
        original.VesicleQuanta = 2;
        Assert.Throws<ArgumentException>(() => c.Release(Session, original)); Assert.Equal(before, c.Capture());
        Assert.NotEmpty(receipt.OriginalEventSha256);
    }

    [Fact]
    public void FuturePreviewDoesNotAdvanceOrChangeEarlierValues()
    {
        var d = Definition(); var c = new MotorReceptorContact(d, Session); c.AdvanceTo(1); c.Release(Session, Spike(d, 1));
        var state = c.Capture(); var earlier = c.EvaluateKnownReleaseAt(1.5);
        _ = c.EvaluateKnownReleaseAt(100);
        Assert.Equal(state, c.Capture()); Assert.Equal(earlier, c.EvaluateKnownReleaseAt(1.5));
        c.AdvanceTo(1.5); Assert.Equal(earlier, c.Capture().Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.EvaluateKnownReleaseAt(1));
    }

    [Fact]
    public void NewEventsMustMatchContactAndExactArrivalBoundary()
    {
        var d = Definition(); var c = new MotorReceptorContact(d, Session); c.AdvanceTo(1); var before = c.Capture();
        Assert.Throws<ArgumentException>(() => c.Release(Guid.NewGuid(), Spike(d, 1)));
        Assert.Equal(before, c.Capture());
        var wrong = new Action<SpikeMessage>[] {
            s => s.SourceInstanceKey = "another-source", s => s.SourceNeuronId = "another-cell",
            s => s.ProjectionId = Guid.NewGuid(), s => s.SynapseId = Guid.NewGuid(),
            s => s.TargetNeuronId = "another-target", s => s.TargetStructure = StructureId.PremotorCortex,
            s => s.Neurotransmitter = NTEnum.GABA, s => s.IsSyntheticBackground = true,
            s => s.IsFeedback = true, s => s.MessageId = Guid.Empty,
            s => s.TimestampMs += .001, s => s.TimestampMs -= .001, s => s.TimestampMs = double.NaN };
        foreach (var mutate in wrong)
        {
            var s = Spike(d, 1); mutate(s);
            Assert.Throws<ArgumentException>(() => c.Release(Session, s)); Assert.Equal(before, c.Capture());
        }
    }

    [Fact]
    public void UnsupportedOverlapDoesNotDropOrMergeTheOriginalRelease()
    {
        var d = Definition(); var c = new MotorReceptorContact(d, Session); c.AdvanceTo(1); c.Release(Session, Spike(d, 1));
        c.AdvanceTo(1.5); var before = c.Capture(); var overlapping = Spike(d, 1.5);
        Assert.Throws<InvalidOperationException>(() => c.Release(Session, overlapping)); Assert.Equal(before, c.Capture());
        c.AdvanceTo(2); c.Release(Session, Spike(d, 2));
        Assert.Equal(2, c.Capture().AcceptedEvents); Assert.Equal(2, c.Capture().LastReleaseMs);
    }

    [Fact]
    public void EpochCapacityStopsReleaseWithoutForgettingEarlierReplay()
    {
        var d = Definition(); var c = new MotorReceptorContact(d, Session, maximumEpochEvents: 1);
        var first = Spike(d, 1); c.AdvanceTo(1); var receipt = c.Release(Session, first); c.AdvanceTo(4);
        var before = c.Capture();
        Assert.Throws<InvalidOperationException>(() => c.Release(Session, Spike(d, 4)));
        Assert.Equal(before, c.Capture()); Assert.Equal(receipt, c.Release(Session, first));
    }

    [Fact]
    public void InvalidParametersAndClockChangesCannotRepairOrResetState()
    {
        var d = Definition();
        Assert.Throws<ArgumentException>(() => new MotorReceptorContact(d, Guid.Empty));
        foreach (var bad in new[] { d with { MaximumConductanceNs = 0 }, d with { AxonalDelayMs = -1 },
            d with { TargetSite = "" }, d with { ElectricalCompartment = 5 }, d with { Kind = (MotorReceptorKind)999 } })
            Assert.Throws<ArgumentException>(() => new MotorReceptorContact(bad, Session));
        var c = new MotorReceptorContact(d, Session); c.AdvanceTo(1); var before = c.Capture();
        foreach (double bad in new[] { -1, .5, double.NaN, double.PositiveInfinity, 1e16 })
        { Assert.Throws<ArgumentOutOfRangeException>(() => c.AdvanceTo(bad)); Assert.Equal(before, c.Capture()); }
    }

    [Fact]
    public void OneOwnerAndTinyPositiveIntervalsPreserveCausality()
    {
        var d = Definition() with { AxonalDelayMs = 0 }; var c = new MotorReceptorContact(d, Session);
        c.Release(Session, Spike(d, 0)); var tiny = c.AdvanceTo(1e-18).Value;
        Assert.True(tiny.OpenFraction > 0); Assert.InRange(tiny.OpenFraction, 1.09e-18, 1.11e-18);
        var before = c.Capture();
        Exception? exception = null;
        var otherOwner = new Thread(() => exception = Record.Exception(() => c.Capture()));
        otherOwner.Start(); otherOwner.Join();
        Assert.IsType<InvalidOperationException>(exception); Assert.Equal(before, c.Capture());
    }
}
