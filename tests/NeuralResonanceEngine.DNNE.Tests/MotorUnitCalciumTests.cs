using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MotorUnitCalciumTests
{
    private static readonly Guid Session = Guid.Parse("9ebee19b-cce4-46c3-8eb8-d964c6cbb27b");
    private static readonly Guid Projection = Guid.Parse("905152ca-fdfb-42e9-94bf-cd299757471d");
    private static MotorAxonClock Clock => new(Session, 0, 0);
    private static MotorAxonContact Contact => MotorAxonContact.Create("spinal-L", "alpha-1", Projection, "test-TA-unit-1", 0);
    private static AvatarMotorUnitCalcium Cell() => new(MotorUnitCalciumParameters.NagamoriSlowUnit1, Contact, Clock);
    private static AvatarMotorAxonArrival Arrival(double time, long ordinal = 1) => new(Session, Contact.SourceInstanceKey,
        StableNeuralIdentity.Create("motor-calcium-test", ordinal), ordinal, Contact.SourceNeuronId, Projection,
        StableNeuralIdentity.Create("outbound-test", ordinal), Contact.ContactId, Contact.MotorUnitId, time, time);
    private static void Advance(AvatarMotorUnitCalcium cell, double until)
    {
        while (cell.TimeMs < until) cell.Advance(Math.Min(.0625, until - cell.TimeMs));
    }

    [Fact]
    public void SilentUnitHasNoInventedActivationOrForce()
    {
        var cell = Cell(); Advance(cell, 1000);
        Assert.Equal(0, cell.Activation); Assert.Equal(0, cell.IsometricForceNewtons);
        Assert.Equal(0, cell.FreeCalcium); Assert.Equal(0, cell.BoundCalcium);
        Assert.Equal(MotorUnitCalciumParameters.NagamoriSlowUnit1.TotalCalcium, cell.StoreCalcium);
        Assert.Equal(0, cell.Arrivals); Assert.Equal(0, cell.MaximumCalciumLedgerError);
    }

    [Fact]
    public void IdentifiedArrivalPrecedesCalciumActivationAndDecayWithoutReset()
    {
        var cell = Cell(); Advance(cell, 10); var arrival = Arrival(10); cell.Receive(arrival);
        Assert.Equal(0, cell.ReleaseDrive); Assert.Equal(0, cell.Activation); Assert.Equal(0, cell.IsometricForceNewtons);
        Advance(cell, 10.0625);
        Assert.True(cell.ReleaseDrive > 0 && cell.FreeCalcium > 0 && cell.BoundCalcium > 0);
        Advance(cell, 100);
        double active = cell.Activation;
        Assert.True(active > 0); Assert.Equal(arrival.MessageId, cell.LastMessageId);
        Assert.Equal(active * MotorUnitCalciumParameters.NagamoriSlowUnit1.PeakTetanicForceNewtons, cell.IsometricForceNewtons);
        Advance(cell, 4010);
        Assert.InRange(cell.Activation, 0, 1e-6); Assert.InRange(cell.BoundCalcium, 0, 1e-6);
        Assert.InRange(cell.MaximumCalciumLedgerError, 0, 1e-11); Assert.Equal(1, cell.Arrivals);
    }

    [Fact]
    public void ReleaseWaveformMatchesIndependentEventSumAfterSeveralDischarges()
    {
        var cell = Cell(); double[] times = [10, 25.125, 80];
        for (int i = 0; i < times.Length; ++i) { Advance(cell, times[i]); cell.Receive(Arrival(times[i], i + 1)); }
        Advance(cell, 123.25);
        var p = MotorUnitCalciumParameters.NagamoriSlowUnit1;
        double expected = times.Sum(t => (1 - Math.Exp(-(123.25 - t) / 1000 / p.ReleaseRiseSeconds)) *
            Math.Exp(-(123.25 - t) / 1000 / p.ReleaseDecaySeconds));
        Assert.InRange(Math.Abs(expected - cell.ReleaseDrive), 0, 1e-12);
    }

    [Fact]
    public void CalciumTransferRetainsStoreAndBindingAccountingDuringRepeatedInput()
    {
        var cell = Cell();
        for (int i = 1; i <= 20; ++i) { Advance(cell, i * 50); cell.Receive(Arrival(i * 50, i)); }
        Advance(cell, 1100);
        Assert.True(cell.ReleasedCalcium > 0 && cell.RecoveredCalcium > 0);
        Assert.InRange(cell.StoreCalcium, 0, MotorUnitCalciumParameters.NagamoriSlowUnit1.TotalCalcium);
        Assert.True(cell.FreeStoreSites >= 0); Assert.InRange(cell.BoundCalcium, 0, 1);
        Assert.InRange(cell.Activation, 0, 1);
        Assert.InRange(Math.Abs(cell.FreeCalcium - cell.ReleasedCalcium + cell.RecoveredCalcium + cell.NetBoundCalcium), 0, 1e-10);
        Assert.InRange(Math.Abs(cell.BoundCalcium - cell.NetBoundCalcium), 0, 1e-10);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("source")]
    [InlineData("contact")]
    [InlineData("session")]
    [InlineData("future")]
    [InlineData("identity")]
    public void InvalidArrivalCannotRecruitThisUnit(string defect)
    {
        var cell = Cell(); Advance(cell, 10); var original = Arrival(10);
        var invalid = defect switch
        {
            "unit" => original with { MotorUnitId = "another-unit" },
            "source" => original with { SourceNeuronId = "another-cell" },
            "contact" => original with { ContactId = Guid.NewGuid() },
            "session" => original with { SessionId = Guid.NewGuid() },
            "future" => original with { ArrivalAtBodyMs = 11, FiredAtNeuralMs = 11 },
            "identity" => original with { MessageId = Guid.Empty },
            _ => throw new ArgumentException()
        };
        Assert.ThrowsAny<ArgumentException>(() => cell.Receive(invalid));
        Assert.Equal(0, cell.Arrivals); Assert.Equal(0, cell.ReleaseDrive); Assert.Equal(0, cell.Activation);
        cell.Receive(original); Assert.Equal(1, cell.Arrivals);
    }

    [Fact]
    public void ReplayedOrLateDischargeIsRejectedInsteadOfRecruitedAgain()
    {
        var cell = Cell(); Advance(cell, 10); var first = Arrival(10); cell.Receive(first);
        Assert.Throws<ArgumentException>(() => cell.Receive(first));
        Advance(cell, 11);
        Assert.Throws<ArgumentException>(() => cell.Receive(Arrival(10, 2)));
        Assert.Equal(1, cell.Arrivals);
    }

    [Fact]
    public void ReceiverConductionDelayIsAppliedExactlyOnceAtTheUnit()
    {
        var contact = MotorAxonContact.Create("spinal-L", "alpha-1", Projection, "test-TA-unit-1", 2.125);
        var anatomy = new MotorAxonAnatomy([contact]); var receiver = new AvatarMotorAxonReceiver(anatomy, Clock);
        var publisher = new MotorAxonPublisher(anatomy, Clock, contact.SourceInstanceKey);
        var cell = new AvatarMotorUnitCalcium(MotorUnitCalciumParameters.NagamoriSlowUnit1, contact, Clock);
        var spike = new SpikeMessage { MessageId = Guid.NewGuid(), SourceStructure = StructureId.SpinalCordMotor,
            SourceInstanceKey = contact.SourceInstanceKey, SourceNeuronId = contact.SourceNeuronId, TimestampMs = 10,
            ProjectionId = Projection, SynapseId = Guid.NewGuid(), Neurotransmitter = NTEnum.ACETYLCHOLINE };
        receiver.Accept(publisher.Publish([spike], 20)); Assert.Empty(receiver.AdvanceTo(12));
        var arrival = Assert.Single(receiver.AdvanceTo(12.125));
        Advance(cell, arrival.ArrivalAtBodyMs); cell.Receive(arrival); cell.Advance(.0625);
        Assert.True(cell.ReleaseDrive > 0); Assert.Equal(12.1875, cell.TimeMs);
        Assert.Equal(spike.MessageId, cell.LastMessageId);
    }

    [Fact]
    public void InvalidStepOrParametersCannotSilentlyChangeTheModel()
    {
        var cell = Cell(); Assert.Throws<ArgumentOutOfRangeException>(() => cell.Advance(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.Advance(double.NaN));
        Assert.Equal(0, cell.TimeMs);
        var p = MotorUnitCalciumParameters.NagamoriSlowUnit1;
        Assert.Throws<ArgumentException>(() => new AvatarMotorUnitCalcium(p with { StoreSites = 0 }, Contact, Clock));
        Assert.Throws<ArgumentException>(() => new AvatarMotorUnitCalcium(p with { ActivationSeconds = double.NaN }, Contact, Clock));
    }
}
