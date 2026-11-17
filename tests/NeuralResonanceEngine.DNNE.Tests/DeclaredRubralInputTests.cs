using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class DeclaredRubralInputTests
{
    private static string NewPath()=>Path.Combine(Path.GetTempPath(),"dnne-declared-rubral",Guid.NewGuid().ToString("N"));
    private static SpikeMessage Input(int index, int pulse=0)=>new()
    {
        MessageId=StableNeuralIdentity.Create("declared-rubral-test",index,pulse),TimestampMs=.125+10*pulse,
        SourceStructure=StructureId.M1,TargetStructure=StructureId.RedNucleus,
        SourceInstanceKey="isolated-rubral-input-fixture",SourceNeuronId=$"fixture-{index:000}",TargetNeuronId=$"n-{index:000}",
        SynapseId=StableNeuralIdentity.Create("declared-rubral-contact",index),
        ProjectionId=StableNeuralIdentity.Create("declared-rubral-projection",index),
        Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=.25f,ReuptakeRate=8,
        SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,ModulationContext=new()
    };
    private static IsolatedRubralSource Source(SpikeMessage[] inputs, IsolatedSynapticTargets? map, string? path=null)=>
        new(Guid.NewGuid(),"isolated-declared-rubral-test",path??NewPath(),100,inputs,map);
    private static RubralInputDelivery[] Complete(IsolatedRubralSource source)
    {
        var rows=new List<RubralInputDelivery>();
        for(int i=0;i<800;i++)
        {
            var frame=source.AdvanceOneSample();var body=IsolatedRubralSource.Decode(frame);
            rows.AddRange(body.InputDeliveries??[]);
            Assert.Equal(JsonSerializer.Serialize(body.InputDeliveries??[]),JsonSerializer.Serialize(source.PendingInputDeliveries()));
            source.Acknowledge(frame.Session,frame.Sequence,frame.Sha256);
            Assert.Empty(source.PendingInputDeliveries());
        }
        return rows.ToArray();
    }

    [Fact]
    public void Every_Declared_Cell_Receives_Its_Original_Contact_Exactly_Once()
    {
        var inputs=Enumerable.Range(0,256).Select(i=>Input(i)).ToArray();
        var map=new IsolatedSynapticTargets(inputs.Select((input,index)=>IsolatedSynapticTarget.From(input,index)),256);
        using var source=Source(inputs,map);var rows=Complete(source);
        Assert.Equal(256,rows.Length);Assert.Equal(256,rows.Select(r=>r.ActualTargetIndex).Distinct().Count());
        Assert.Equal(256,rows.Select(r=>r.ReceivingContactId).Distinct().Count());
        foreach(var row in rows)
        {
            var original=inputs.Single(e=>e.MessageId==row.OriginalMessageId);
            Assert.Equal(original.TargetNeuronId,row.NamedTarget);Assert.Equal(row.NamedTarget,row.ActualTarget);
            Assert.Equal(JsonSerializer.Serialize(original),row.OriginalJson);
            Assert.Equal(row.ArrivalAtMs,row.AppliedAtMs);Assert.True(row.ArrivalAtMs>=original.TimestampMs);
        }
        Assert.Contains(rows,r=>r.ActualTarget=="n-081");
    }

    [Fact]
    public void Repeated_Spikes_Keep_The_Same_Receiving_Synapse_And_Cell()
    {
        var inputs=new[]{Input(81),Input(81,1),Input(81,2)};
        var map=new IsolatedSynapticTargets([IsolatedSynapticTarget.From(inputs[0],81)],256);
        using var source=Source(inputs,map);var rows=Complete(source);
        Assert.Equal(3,rows.Length);Assert.Single(rows.Select(r=>r.ReceivingContactId).Distinct());
        Assert.All(rows,r=>Assert.Equal(81,r.ActualTargetIndex));
    }

    [Fact]
    public void Missing_Contact_Is_Rejected_Before_State_Creation()
    {
        var map=new IsolatedSynapticTargets([IsolatedSynapticTarget.From(Input(1),1)],256);string path=NewPath();
        Assert.Throws<ArgumentException>(()=>Source([Input(81)],map,path));Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Contradictory_Named_And_Declared_Recipients_Are_Rejected()
    {
        var input=Input(81);var map=new IsolatedSynapticTargets([IsolatedSynapticTarget.From(input,34)],256);string path=NewPath();
        Assert.Throws<ArgumentException>(()=>Source([input],map,path));Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Explicit_Input_Anatomy_Preserves_Existing_Outgoing_Routes()
    {
        var input=Input(81);var map=new IsolatedSynapticTargets([IsolatedSynapticTarget.From(input,81)],256);
        using var legacy=Source([],null);using var candidate=Source([],map);
        Assert.Equal(JsonSerializer.Serialize(legacy.Routes),JsonSerializer.Serialize(candidate.Routes));
        Assert.Null(legacy.InputTargetFingerprint);Assert.Equal(map.Fingerprint,candidate.InputTargetFingerprint);
    }

    [Fact]
    public void Declared_Rubral_State_Cannot_Replace_Previous_Saves()
    {
        var input=Input(81);var map=new IsolatedSynapticTargets([IsolatedSynapticTarget.From(input,81)],256);
        string path=NewPath();Directory.CreateDirectory(path);string marker=Path.Combine(path,"old-state.txt");
        File.WriteAllText(marker,"preserve");
        Assert.Throws<InvalidOperationException>(()=>Source([input],map,path));Assert.Equal("preserve",File.ReadAllText(marker));
    }
}
