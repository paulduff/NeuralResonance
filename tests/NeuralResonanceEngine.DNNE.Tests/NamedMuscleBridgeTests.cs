using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using Nre.UnityWorld;
using static Nre.UnityWorld.ArticulatedTendonWire;
using static Nre.UnityWorld.MuscleHingeWire;
using Wire = Nre.UnityWorld.ArticulatedTendonWire;
using Reply = Nre.UnityWorld.ArticulatedTendonWire.Reply;
using AvatarMusclePath = NRE.SimAvatar.AvatarMusclePath;
using MusclePathAnchor = NRE.SimAvatar.MusclePathAnchor;
using MusclePathVector = NRE.SimAvatar.MusclePathVector;
using MusclePathPoint = NRE.SimAvatar.MusclePathPoint;

namespace NeuralResonanceEngine.DNNE.Tests;

// The host consumes its real causal plant and shared wire, but this peer does
// NOT run Unity. Native point force delivery remains a separate qualification.
public sealed class NamedMuscleBridgeTests
{
    private static readonly string PlantHash = new('A',64);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,NativeArticulatedBody.Json);
    private static Configuration Config(int count=3)
    {
        var c=ArticulatedTendonBridgeTests.Config(); c.protocol=NamedMuscleProtocol;
        c.declarations=Enumerable.Range(0,count).Select(i=>new Declaration {
            muscleId=$"named-muscle-{i:D3}",provenance="declared-multijoint-fixture-not-human-anatomy",
            anchors=[new(){id="origin",bodyId="left_thigh",x=.01,y=-.1,z=.02},
                     new(){id="via-knee",bodyId="left_shin",x=-.02,y=-.03,z=.01},
                     new(){id="insertion",bodyId="left_foot",x=.03,y=.02,z=.07}] }).ToArray();
        return c;
    }
    private static ArticulatedTendonBridgeTests.Peer Peer(Configuration? configuration=null)
    {
        var c=configuration??Config(); var peer=new ArticulatedTendonBridgeTests.Peer(c);
        var geometry=JsonNode.Parse(peer.Current.geometryJson)!;
        geometry["version"]=NamedGeometryVersion;
        geometry["declarationsJson"]=Json(new{paths=c.declarations.Select(d=>new{id=d.muscleId,d.provenance,
            anchors=d.anchors.Select(a=>new{id=a.id,a.bodyId,xBits=a.x.bits,yBits=a.y.bits,zBits=a.z.bits})})});
        peer.Current.protocol=c.protocol; peer.Current.geometryJson=geometry.ToJsonString(); peer.Current.bodyFingerprint=Wire.Digest(peer.Current.geometryJson);
        double length=peer.Current.paths[0].lengthMeters;
        peer.Current.paths=c.declarations.Select(d=>new MuscleHingeWire.Path{muscleId=d.muscleId,
            pathFingerprint=Canonical(d,peer.Current.bodyFingerprint).Fingerprint,lengthMeters=length,velocityMetersPerSecond=0d}).ToArray();
        return peer;
    }
    private static AvatarMusclePath Canonical(Declaration d,string geometry)=>new(d.muscleId,geometry,d.provenance,
        d.anchors.Select(a=>new MusclePathAnchor(a.id,a.bodyId,new(a.x,a.y,a.z))));
    private static MotorMechanicalEffort Effort(NativeArticulatedBody body)=>new(Guid.NewGuid(),ArticulatedTendonBridgeTests.Session,
        body.Fingerprint,body.Last.plantFingerprint,body.CompletedPhysicsStep,body.CompletedPhysicsStep*body.PhysicsStepMs,
        (body.CompletedPhysicsStep+1)*body.PhysicsStepMs,body.Last.paths.Select((p,i)=>new TendonTension(p.muscleId,.125*(i+1))).ToArray());

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(80)]
    public void Complete_named_inventory_survives_one_physical_receipt(int count)
    {
        var peer=Peer(Config(count)); var body=peer.Host(); body.Bind(PlantHash); body.CaptureGeometry(); var e=Effort(body);
        body.AdvanceOneStep(e); var after=body.CaptureGeometry();
        Assert.Equal(count,after.Paths.Count); Assert.Equal(peer.Configuration.declarations.Select(d=>d.muscleId),after.Paths.Select(p=>p.MuscleId));
        var command=Assert.Single(peer.Commands,r=>r.operation=="step");
        Assert.Equal(NamedMuscleProtocol,command.protocol); Assert.Equal(e.Tensions.Select(t=>Encode(t.AxialNewtons)),command.tensions.Select(t=>t.axialNewtons.bits));
        Assert.Equal(1,peer.PhysicalSteps); Assert.Equal(1,body.CompletedPhysicsStep); Assert.Equal(e.EffortId,after.LastAppliedEffortId);
    }

    [Fact]
    public void One_multijoint_path_has_one_tension_and_both_via_point_reactions()
    {
        var peer=Peer(Config(1)); var body=peer.Host(); body.Bind(PlantHash); body.AdvanceOneStep(Effort(body));
        Assert.Single(Assert.Single(peer.Commands,r=>r.operation=="step").tensions);
        var d=peer.Configuration.declarations[0]; var path=Canonical(d,body.Fingerprint);
        var positions=new[]{new MusclePathVector(0,1,0),new MusclePathVector(0,0,0),new MusclePathVector(1,0,0)};
        var snapshot=path.Measure(body.Fingerprint,d.anchors.Select((a,i)=>new MusclePathPoint(a.id,a.bodyId,positions[i],new(0,0,0))));
        var forces=snapshot.ApplyTension(12).PointForces;
        Assert.Equal(3,forces.Count); Assert.Equal(3,path.Anchors.Select(a=>a.BodyId).Distinct().Count());
        Assert.Equal(12,forces[1].ForceNewtons.X); Assert.Equal(12,forces[1].ForceNewtons.Y);
        Assert.Equal(0,forces.Sum(f=>f.ForceNewtons.X)); Assert.Equal(0,forces.Sum(f=>f.ForceNewtons.Y));
        Assert.Single(peer.Configuration.declarations);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("reordered")]
    public void Incomplete_or_changed_effort_inventory_faults_before_publication(string kind)
    {
        var peer=Peer(); var body=peer.Host(); body.Bind(PlantHash); var e=Effort(body); var t=e.Tensions.ToArray();
        e=e with{Tensions=kind switch{"missing"=>t[..^1],"extra"=>[..t,new("foreign",1)],"duplicate"=>[t[0],t[0],t[2]],_=>t.Reverse().ToArray()}};
        Assert.Throws<InvalidOperationException>(()=>body.AdvanceOneStep(e)); Assert.True(body.Faulted);
        Assert.Single(peer.Commands); Assert.Equal(0,peer.PhysicalSteps);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("muscle")]
    [InlineData("provenance")]
    [InlineData("frame")]
    [InlineData("coordinate")]
    [InlineData("order")]
    [InlineData("missing")]
    [InlineData("fingerprint")]
    public void Rehashed_native_anatomy_cannot_disagree_with_its_configuration(string kind)
    {
        var peer=Peer(); var r=peer.Current; var geometry=JsonNode.Parse(r.geometryJson)!;
        var declared=JsonNode.Parse(geometry["declarationsJson"]!.GetValue<string>())!;
        var p=declared["paths"]![0]!;
        switch(kind)
        {
            case "version": geometry["version"]="declared-full-avatar-tendon-paths-v2";break;
            case "muscle": p["id"]="foreign-muscle";break;
            case "provenance": p["provenance"]="another-source";break;
            case "frame": p["anchors"]![1]!["bodyId"]="right_shin";break;
            case "coordinate": p["anchors"]![1]!["xBits"]=Encode(.001);break;
            case "order": var a=p["anchors"]!.AsArray(); var first=a[0]!.DeepClone();a[0]=a[1]!.DeepClone();a[1]=first;break;
            case "missing": p["anchors"]!.AsArray().RemoveAt(1);break;
        }
        geometry["declarationsJson"]=declared.ToJsonString(); r.geometryJson=geometry.ToJsonString(); r.bodyFingerprint=Wire.Digest(r.geometryJson);
        foreach(var (path,d) in r.paths.Zip(peer.Configuration.declarations)) path.pathFingerprint=Canonical(d,r.bodyFingerprint).Fingerprint;
        if(kind=="fingerprint")r.paths[0].pathFingerprint=new string('F',64);
        Assert.Throws<InvalidOperationException>(()=>peer.Host()); Assert.Empty(peer.Commands);
    }

    [Fact]
    public void Unknown_attachment_body_is_rejected_even_with_consistent_hashes()
    {
        var c=Config(); c.declarations[0].anchors[1].bodyId="absent_body"; var peer=Peer(c);
        Assert.Throws<InvalidOperationException>(()=>peer.Host()); Assert.Empty(peer.Commands);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("request")]
    [InlineData("reply")]
    public void Protocol_cannot_switch_during_an_epoch(string stage)
    {
        var peer=Peer();
        if(stage=="hello") {peer.Current.protocol=Wire.Protocol;Assert.Throws<InvalidOperationException>(()=>peer.Host());return;}
        var body=peer.Host(); body.Bind(PlantHash);
        if(stage=="reply") {peer.Corrupt=r=>r.protocol=Wire.Protocol;Assert.Throws<InvalidOperationException>(()=>body.CaptureGeometry());Assert.True(body.Faulted);}
        else
        {
            var request=peer.Commands[0]; request.operation="inspect";request.sequence=peer.Current.sequence+1;
            Wire.Validate(request,peer.Current,peer.Configuration,peer.Current.configurationHash);
            request.protocol=Wire.Protocol;
            Assert.Throws<InvalidOperationException>(()=>Wire.Validate(request,peer.Current,peer.Configuration,peer.Current.configurationHash));
        }
    }

    [Fact]
    public void Original_protocol_keeps_its_38_path_requirement()
    {
        var c=Config();c.protocol=Wire.Protocol;Assert.Throws<InvalidOperationException>(()=>Wire.Validate(c));
        Assert.Equal(.000125f,Wire.Validate(ArticulatedTendonBridgeTests.Config()));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("one-body")]
    [InlineData("unordered")]
    public void Named_mode_still_requires_a_complete_valid_declaration(string kind)
    {
        var c=Config();
        switch(kind){case "empty":c.declarations=[];break;case "duplicate":c.declarations[1]=c.declarations[0];break;
            case "one-body":foreach(var a in c.declarations[0].anchors)a.bodyId="left_shin";break;default:Array.Reverse(c.declarations);break;}
        Assert.Throws<InvalidOperationException>(()=>Wire.Validate(c));
    }

    [Fact]
    public void Lost_named_muscle_acknowledgment_never_retries()
    {
        var peer=Peer();var body=peer.Host();body.Bind(PlantHash);peer.LoseReply=true;
        Assert.Throws<TimeoutException>(()=>body.AdvanceOneStep(Effort(body)));
        Assert.Equal(1,peer.PhysicalSteps);Assert.Equal(0,body.CompletedPhysicsStep);Assert.True(body.Faulted);
        Assert.Throws<InvalidOperationException>(()=>body.CaptureGeometry());Assert.Equal(2,peer.Commands.Count);
    }

    [Fact]
    public void Named_causal_plant_retains_each_original_motor_source_and_waits_for_all()
    {
        var peer=Peer();var body=peer.Host();var session=ArticulatedTendonBridgeTests.Session;
        var clock=new PhysicalSampleClock(session,"named-peer","paths",0,body.PhysicsStepMs,0,1000);
        var g=DynamicMuscleGeometry.NagamoriTibialisAnterior;
        var definitions=peer.Configuration.declarations.Select(d=>new CausalMuscleDefinition(d.muscleId,g,
            [SlowMotorUnitDefinition.NagamoriSlowUnit1(MotorAxonContact.Create("source-"+d.muscleId,"cell-"+d.muscleId,
            Guid.Parse("61f5f1dc-7bfd-4213-8751-2a277798f4bc"),"unit-"+d.muscleId,.015625))],g.OptimalFiberLengthMeters)).ToArray();
        var plant=new AvatarCausalMotorPlant(clock,body,definitions,2,schedule:CausalMotorSchedule.BodyThenMotor);
        var anatomy=new MotorAxonAnatomy(definitions.SelectMany(d=>d.Units).Select(u=>u.Contact));var motorClock=new MotorAxonClock(session,1000,0);
        body.Bind(plant.Fingerprint);plant.CaptureInitial();plant.Resume();Assert.Equal(CausalBodyAdvance.Advanced,plant.TryAdvanceBody().Outcome);
        var sources=peer.Configuration.declarations.Select(d=>"source-"+d.muscleId).ToArray();
        foreach(var source in sources[..^1])plant.Accept(new MotorAxonPublisher(anatomy,motorClock,source).Publish([],clock.AtStep(1).NeuralMs));
        Assert.Equal(CausalMotorAdvance.WaitingForNeuronalCompleteness,plant.TryCompleteMotorInterval().Outcome);
        plant.Accept(new MotorAxonPublisher(anatomy,motorClock,sources[^1]).Publish([],clock.AtStep(1).NeuralMs));
        Assert.Equal(CausalMotorAdvance.Advanced,plant.TryCompleteMotorInterval().Outcome);Assert.Equal(1,plant.State.CompletedPhysicsStep);
        Assert.Equal(3,Assert.Single(peer.Commands,r=>r.operation=="step").tensions.Length);
    }
}
