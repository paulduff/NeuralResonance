using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class IsolatedRubralSourceTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "dnne-rubral-source", Guid.NewGuid().ToString("N"));
    private static readonly Guid Session = Guid.Parse("fe51298f-fab1-4d20-92a9-378934c45ac0");
    private static IsolatedRubralSource Source(string? path = null, double duration = 1,
        IReadOnlyList<SpikeMessage>? input = null) => new(Session,"isolated-rubral-source-tests",path??NewPath(),duration,input??[]);
    private static string Bytes<T>(T value) => JsonSerializer.Serialize(value);
    private static SpikeMessage Input(double at = .125) => new()
    {
        MessageId = Guid.NewGuid(),TimestampMs = at,SourceStructure = StructureId.M1,TargetStructure = StructureId.RedNucleus,
        SourceInstanceKey = "isolated-input-fixture",SourceNeuronId = "fixture-1",TargetNeuronId = "n-000",
        SynapseId = Guid.Parse("7f187512-c69d-426a-be45-35ec40d2ef27"),ProjectionId = Guid.Parse("f2efbdbe-b7d8-492b-8e04-7c566e1013a0"),
        Neurotransmitter = NTEnum.GLUTAMATE,VesicleQuanta = 1,ReuptakeRate = 8,SpikeType = SpikeTypeEnum.ACTION_POTENTIAL,
        ModulationContext = new NeuromodState()
    };

    [Fact]
    public void Construction_Preserves_Environment_And_Uses_Full_Existing_Rubral_Population()
    {
        var before=(Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),Environment.GetEnvironmentVariable("SERVICE_INSTANCE"));
        using var source=Source();
        Assert.Equal(before,(Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),Environment.GetEnvironmentVariable("SERVICE_INSTANCE")));
        Assert.Equal(256,source.Routes.Count);
        Assert.Equal(256,source.Routes.Select(r=>r.SourceNeuron).Distinct().Count());
        Assert.All(source.Routes,r=>{Assert.Equal(StructureId.SpinalCordMotor,r.TargetStructure);Assert.Equal(NTEnum.GLUTAMATE,r.Transmitter);Assert.Equal(r.Synapse,r.Projection);});
        Assert.Null(source.Capture().ActualCompletedThroughMs);
    }

    [Fact]
    public void Existing_State_Is_Rejected_Without_Changing_It()
    {
        string path=NewPath();Directory.CreateDirectory(path);string marker=Path.Combine(path,"preserved.txt");File.WriteAllText(marker,"old state");
        Assert.Throws<InvalidOperationException>(()=>Source(path));Assert.Equal("old state",File.ReadAllText(marker));
    }

    [Fact]
    public void Pending_Frame_Backpressures_Actual_Neuronal_Advancement()
    {
        using var source=Source();var frame=source.AdvanceOneSample();string before=Bytes(source.Capture());
        Assert.Throws<InvalidOperationException>(()=>source.AdvanceOneSample());
        Assert.Equal(before,Bytes(source.Capture()));Assert.Same(frame,source.Pending());
        Assert.Equal(.125,frame.CompleteThroughMs);Assert.Equal(1,frame.Sequence);
    }

    [Fact]
    public void Conflicting_Acknowledgement_Preserves_Frame_And_State()
    {
        using var source=Source();var frame=source.AdvanceOneSample();string before=Bytes(source.Capture());
        Assert.Throws<ArgumentException>(()=>source.Acknowledge(Guid.NewGuid(),1,frame.Sha256));
        Assert.Throws<ArgumentException>(()=>source.Acknowledge(Session,2,frame.Sha256));
        Assert.Throws<ArgumentException>(()=>source.Acknowledge(Session,1,"wrong"));
        Assert.Equal(before,Bytes(source.Capture()));Assert.Same(frame,source.Pending());
    }

    [Fact]
    public void Exact_Acknowledgement_Replay_Does_Not_Clear_A_Later_Frame()
    {
        using var source=Source();var first=source.AdvanceOneSample();var ack=source.Acknowledge(Session,first.Sequence,first.Sha256);
        var next=source.AdvanceOneSample();string before=Bytes(source.Capture());
        Assert.Equal(ack,source.Acknowledge(Session,first.Sequence,first.Sha256));
        Assert.Equal(before,Bytes(source.Capture()));Assert.Same(next,source.Pending());
    }

    [Fact]
    public void Decoding_Detaches_Mutable_Diagnostic_And_Event_Objects()
    {
        using var source=Source();var frame=source.AdvanceOneSample();var a=IsolatedRubralSource.Decode(frame);var b=IsolatedRubralSource.Decode(frame);
        Assert.NotSame(a.Observation.Cells,b.Observation.Cells);Assert.Equal(Bytes(a),Bytes(b));
        Assert.Equal(frame.Sha256,IsolatedRubralSource.Hash(frame.CanonicalJson));
        Assert.Throws<ArgumentException>(()=>IsolatedRubralSource.Decode(frame with {CanonicalJson=frame.CanonicalJson+" "}));
        Assert.Throws<ArgumentException>(()=>IsolatedRubralSource.Decode(frame with {CompleteThroughMs=2}));
    }

    [Fact]
    public void Observed_Firing_And_Complete_Intervals_Remain_Accounted()
    {
        using var source=Source();long total=0;
        for(int i=1;i<=8;i++)
        {
            var frame=source.AdvanceOneSample();var body=IsolatedRubralSource.Decode(frame);
            Assert.Equal(i*.125,body.CompleteThroughMs);Assert.Equal(i,body.Observation.CompletedTick);
            Assert.Equal(body.FiredCount,body.OriginalEvents.Count);Assert.Equal(256,body.Observation.Cells.Count);
            Assert.Equal(IsolatedRubralSource.TimingConvention,body.TimingConvention);
            Assert.All(body.OriginalEvents,e=>Assert.InRange(e.TimestampMs,(i-1)*.125,i*.125));
            total+=body.FiredCount;source.Acknowledge(Session,frame.Sequence,frame.Sha256);
        }
        Assert.Equal(total,source.Capture().ProducedEvents);Assert.Equal(1,source.Capture().ActualCompletedThroughMs);
    }

    [Fact]
    public void Completed_Horizon_Cannot_Be_Advanced_Or_Replaced()
    {
        using var source=Source(duration:.125);var frame=source.AdvanceOneSample();source.Acknowledge(Session,1,frame.Sha256);
        string before=Bytes(source.Capture());Assert.Throws<InvalidOperationException>(()=>source.AdvanceOneSample());
        Assert.Equal(before,Bytes(source.Capture()));
    }

    [Fact]
    public void Declared_Fixture_Input_Is_Copied_Before_Execution()
    {
        var input=Input();using var source=Source(input:[input]);input.TimestampMs=999;input.TargetStructure=StructureId.SpinalCordMotor;
        var frame=source.AdvanceOneSample();Assert.Equal(1,IsolatedRubralSource.Decode(frame).AdmittedFixtureEvents);
    }

    [Fact]
    public void Invalid_Or_Repeated_Input_Is_Rejected_Before_State_Creation()
    {
        string path=NewPath();var input=Input();Assert.Throws<ArgumentException>(()=>Source(path,input:[input,input]));
        Assert.False(Directory.Exists(path));
        Assert.Throws<ArgumentException>(()=>Source(path,input:[Input(.75),Input(.25)]));Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Wrong_Thread_Cannot_Advance_Source()
    {
        using var source=Source();Exception? failure=null;
        var thread=new Thread(()=>{try{source.AdvanceOneSample();}catch(Exception error){failure=error;}});thread.Start();thread.Join();
        Assert.IsType<InvalidOperationException>(failure);Assert.Equal(0,source.Capture().CompletedSamples);
    }

    [Fact]
    public void Disposed_Source_Cannot_Publish_Or_Advance()
    {
        var source=Source();source.Dispose();source.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>source.AdvanceOneSample());Assert.Throws<ObjectDisposedException>(()=>source.Pending());
    }
}
