using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class RubralMotorCircuitTests
{
    private static RubralMotorCircuit Fresh(Guid session, bool disconnected = false) => new(session,
        Path.Combine(Path.GetTempPath(),"dnne-rubral-motor-owner",Guid.NewGuid().ToString("N")),0,disconnected);
    private static MotorAxonAnatomy Anatomy(RubralMotorCircuit circuit) => new([
        MotorAxonContact.Create(circuit.Identity.Instance,circuit.Identity.Neuron,circuit.Identity.Projection,"owned-unit",2.0625)]);
    private static string State(RubralMotorCircuit circuit) => JsonSerializer.Serialize(new
    { source=circuit.CaptureSource(), motor=circuit.CaptureMotor(), pending=circuit.PendingSource(), faulted=circuit.Faulted });

    [Fact]
    public void Quiet_Priming_Is_Retained_Across_Explicit_Body_Epoch_Binding()
    {
        Guid session=Guid.NewGuid();using var circuit=Fresh(session);
        var frames=Enumerable.Range(0,4).Select(_=>circuit.AdvanceOneSourceSample()).ToArray();
        Assert.Equal(new[]{.125,.25,.375,.5},frames.Select(f=>f.OriginalSource.CompleteThroughMs));
        Assert.Equal(5,circuit.CaptureMotor().CompletedSamples);Assert.Equal(.5,circuit.CaptureMotor().Cell.TimeMs);
        Assert.Empty(circuit.CaptureMotor().Emissions);Assert.Null(circuit.PendingSource());
        string before=State(circuit);
        circuit.AttachMotorOutput(Anatomy(circuit),new(session,.5,100));
        Assert.Equal(before,State(circuit));
        var next=circuit.AdvanceOneSourceSample();
        Assert.Equal(.625,next.OriginalSource.CompleteThroughMs);
        Assert.Equal(6*.1,Assert.Single(next.MotorSamples).Cell.TimeMs);
        var batch=circuit.PublishCompleted();Assert.Equal(6*.1,batch.CompleteThroughNeuralMs);Assert.Empty(batch.Spikes);
        Assert.Equal(10,next.Admissions.Count);Assert.All(next.Admissions,r=>Assert.Equal(5,r.Receipt.Sequence));
        Assert.Equal(next.OriginalSource.Sequence,next.SourceAcknowledgement.Sequence);
        Assert.Equal(next.OriginalSource.Sha256,next.SourceAcknowledgement.Sha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unacknowledged_Motor_Output_Holds_Upstream_And_Downstream_State(bool disconnected)
    {
        Guid session=Guid.NewGuid();using var circuit=Fresh(session,disconnected);
        var anatomy=Anatomy(circuit);var clock=new MotorAxonClock(session,0,0);circuit.AttachMotorOutput(anatomy,clock);
        circuit.AdvanceOneSourceSample();var batch=circuit.PublishCompleted();string before=State(circuit);
        Assert.Throws<InvalidOperationException>(()=>circuit.AdvanceOneSourceSample());
        Assert.Equal(before,State(circuit));Assert.Same(batch,Assert.Single(circuit.Unacknowledged()));Assert.False(circuit.Faulted);
        var receiver=new AvatarMotorAxonReceiver(anatomy,clock);var receipt=receiver.AcceptWithAcknowledgement(batch);
        Assert.Equal(receipt,receiver.AcceptWithAcknowledgement(batch));
        circuit.Acknowledge(receipt);circuit.Acknowledge(receipt);
        Assert.Equal(before,State(circuit));Assert.Empty(circuit.Unacknowledged());
        Assert.Equal(2,circuit.AdvanceOneSourceSample().OriginalSource.Sequence);
    }

    [Fact]
    public void Failed_Clock_Binding_Does_Not_Claim_Or_Reset_The_Cell()
    {
        Guid session=Guid.NewGuid();using var circuit=Fresh(session);var anatomy=Anatomy(circuit);
        circuit.AdvanceOneSourceSample();string before=State(circuit);
        Assert.Throws<ArgumentException>(()=>circuit.AttachMotorOutput(anatomy,new(session,0,0)));
        Assert.Throws<ArgumentException>(()=>circuit.AttachMotorOutput(anatomy,new(Guid.NewGuid(),.1,0)));
        Assert.Equal(before,State(circuit));
        circuit.AttachMotorOutput(anatomy,new(session,.1,0));Assert.Equal(before,State(circuit));
        Assert.Throws<InvalidOperationException>(()=>circuit.AttachMotorOutput(anatomy,new(session,.1,0)));
        Assert.Equal(before,State(circuit));
    }

    [Fact]
    public void Source_And_Output_Reject_A_Second_Thread_Without_Mutation()
    {
        Guid session=Guid.NewGuid();using var circuit=Fresh(session);string before=State(circuit);
        Exception? failure=null;var worker=new Thread(()=>
        {
            try {
            Assert.Throws<InvalidOperationException>(()=>circuit.AdvanceOneSourceSample());
            Assert.Throws<InvalidOperationException>(()=>circuit.CaptureMotor());
            Assert.Throws<InvalidOperationException>(()=>circuit.PublishCompleted());
            } catch(Exception error) {failure=error;}
        }) { IsBackground=true };
        worker.Start();Assert.True(worker.Join(TimeSpan.FromSeconds(5)));Assert.Null(failure);
        Assert.Equal(before,State(circuit));
    }

    [Fact]
    public void Publication_Requires_Binding_And_Disposal_Cannot_Resume_An_Epoch()
    {
        Guid session=Guid.NewGuid();var circuit=Fresh(session);
        try
        {
            string before=State(circuit);Assert.Throws<InvalidOperationException>(()=>circuit.PublishCompleted());
            Assert.Equal(before,State(circuit));circuit.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>circuit.AdvanceOneSourceSample());
            Assert.Throws<ObjectDisposedException>(()=>circuit.CaptureMotor());
        }
        finally {circuit.Dispose();}
    }
}
