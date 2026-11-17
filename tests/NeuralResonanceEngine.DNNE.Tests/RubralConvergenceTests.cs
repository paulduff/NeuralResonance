using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class RubralConvergenceTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "dnne-rubral-convergence", Guid.NewGuid().ToString("N"));
    private static IsolatedRubralSource Source(IsolatedRubralProjection? projection, string? path = null,
        SpikeMessage[]? inputs = null, IsolatedSynapticTargets? targets = null) =>
        new(Guid.NewGuid(), "isolated-rubral-study", path ?? NewPath(), 100, inputs ?? [], targets, projection);

    [Fact]
    public void Projection_Copies_Anatomy_And_Rejects_Ambiguous_Or_OutOfRange_Cells()
    {
        int[] sources = [81, 82]; var map = new IsolatedRubralProjection(sources, 2); sources[0] = 99;
        Assert.Equal(new[] {81, 82}, map.Sources); Assert.Equal(2, map.Resolve(81, 44)); Assert.Equal(44, map.Resolve(99, 44));
        Assert.Throws<ArgumentException>(() => new IsolatedRubralProjection([81,81], 2));
        Assert.Throws<ArgumentException>(() => new IsolatedRubralProjection([256], 2));
        Assert.Throws<ArgumentException>(() => new IsolatedRubralProjection([], 2));
        Assert.Throws<ArgumentException>(() => new IsolatedRubralProjection([81], -1));
        Assert.Throws<ArgumentException>(() => map.ValidateFor(128, 256));
        Assert.Throws<ArgumentException>(() => map.ValidateFor(256, 2));
    }

    [Fact]
    public void Projection_Fingerprint_Identifies_Anatomy_Independent_Of_Enumeration_Order()
    {
        var a = new IsolatedRubralProjection([81,82], 2);
        Assert.Equal(a.Fingerprint, new IsolatedRubralProjection([82,81], 2).Fingerprint);
        Assert.NotEqual(a.Fingerprint, new IsolatedRubralProjection([81,83], 2).Fingerprint);
        Assert.NotEqual(a.Fingerprint, new IsolatedRubralProjection([81,82], 3).Fingerprint);
    }

    [Fact]
    public void Ten_Declared_Recipients_Preserve_The_Original_Contact_And_Other_Routes()
    {
        using var original = Source(null);
        using var convergent = Source(new(Enumerable.Range(81,10), 2));
        var selected = convergent.Routes.Where(r => r.TargetIndex == 2).ToArray();
        Assert.Equal(Enumerable.Range(81,10), selected.Select(r => r.SourceIndex));
        Assert.Equal(10, selected.Select(r => r.Synapse).Distinct().Count());
        foreach (var route in original.Routes)
        {
            var changed = convergent.Routes.Single(r => r.SourceIndex == route.SourceIndex);
            if (route.SourceIndex < 82 || route.SourceIndex > 90) Assert.Equal(route, changed);
            else { Assert.Equal(2, changed.TargetIndex); Assert.Equal("auto-SpinalCordMotor-002", changed.TargetNeuron); }
        }
    }

    [Fact]
    public void Actual_Neuronal_Events_Are_Originally_Emitted_To_Declared_Recipients()
    {
        var inputs = Enumerable.Range(0,3).SelectMany(pulse => Enumerable.Range(81,10).Select(index => new SpikeMessage
        {
            MessageId=StableNeuralIdentity.Create("rubral-convergence-input",index,pulse),TimestampMs=.125+10*pulse,
            SourceStructure=StructureId.M1,TargetStructure=StructureId.RedNucleus,
            SourceInstanceKey="isolated-rubral-fixture",SourceNeuronId=$"fixture-{index:000}",TargetNeuronId=$"n-{index:000}",
            SynapseId=StableNeuralIdentity.Create("rubral-convergence-contact",index),
            ProjectionId=StableNeuralIdentity.Create("rubral-convergence-projection",index),
            Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=4,ReuptakeRate=8,
            SpikeType=SpikeTypeEnum.ACTION_POTENTIAL,ModulationContext=new()
        })).ToArray();
        var targets = new IsolatedSynapticTargets(inputs.Take(10).Select((e,i) => IsolatedSynapticTarget.From(e,81+i)),256);
        using var source = Source(new(Enumerable.Range(81,10),2), inputs:inputs, targets:targets);
        var events = new List<SpikeMessage>();
        for(int i=0;i<800;i++)
        {
            var frame=source.AdvanceOneSample(); var body=IsolatedRubralSource.Decode(frame);
            events.AddRange(body.OriginalEvents);
            Assert.Equal(frame.CanonicalJson, JsonSerializer.Serialize(body));
            source.Acknowledge(frame.Session,frame.Sequence,frame.Sha256);
        }
        var selected=events.Where(e=>e.TargetNeuronId=="auto-SpinalCordMotor-002").ToArray();
        Assert.NotEmpty(selected); Assert.Equal(selected.Length,selected.Select(e=>e.MessageId).Distinct().Count());
        foreach(var e in selected)
        {
            var route=source.Routes.Single(r=>r.SourceNeuron==e.SourceNeuronId);
            Assert.InRange(route.SourceIndex,81,90); Assert.Equal(route.Synapse,e.SynapseId);
            Assert.Equal(route.Projection,e.ProjectionId); Assert.Equal(route.SourceInstance,e.SourceInstanceKey);
        }
    }

    [Fact]
    public void Projection_Cannot_Replace_An_Existing_Save()
    {
        string path=NewPath(); Directory.CreateDirectory(path); string marker=Path.Combine(path,"preserved.txt");
        File.WriteAllText(marker,"previous brain");
        Assert.Throws<InvalidOperationException>(()=>Source(new(Enumerable.Range(81,10),2),path));
        Assert.Equal("previous brain",File.ReadAllText(marker));
    }
}
