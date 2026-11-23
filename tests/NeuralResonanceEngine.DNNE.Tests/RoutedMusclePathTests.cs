using System.Text.Json;
using NRE.SimAvatar;
using U=Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class RoutedMusclePathTests
{
    static MusclePathVector V(double x=0,double y=0,double z=0)=>new(x,y,z);
    static U.MusclePathVector UV(MusclePathVector v)=>new(v.X,v.Y,v.Z);
    static MusclePathVector Cross(MusclePathVector a,MusclePathVector b)=>MuscleCylinderFrame.Cross(a,b);
    static void Near(double expected,double actual)=>Assert.True(double.IsFinite(actual)&&Math.Abs(expected-actual)<=2e-11,$"Expected {expected:R}; actual {actual:R}");
    static void Vec(MusclePathVector a,MusclePathVector b){Near(a.X,b.X);Near(a.Y,b.Y);Near(a.Z,b.Z);}
    record Fixture(AvatarRoutedMusclePath Path,AvatarPatellarPath Patella,MusclePathAnchor[] Anchors,MusclePathPoint[] Physical,
        MuscleCylinderObservation[] Guides,PatellarMuscleObservation[] Virtual);
    static Fixture Create(double q=2.1,double rate=.7)
    {
        // Deliberately analytic geometry, not the anatomical quadriceps map.
        double[] knots=[1.8,2.4];var angle=new MuscleCoordinateSpline(knots,knots);var zero=new MuscleCoordinateSpline(knots,[0,0]);
        var patella=new AvatarPatellarPath("knee","femur","patella",new string('A',64),V(),0,angle,zero,zero);
        var rule=new PatellarMuscleConstraint("patella","femur","tibia",patella.Fingerprint);
        var local=V(3);var p=patella.Measure(q,local);
        var moving=new PatellarMuscleObservation(rule,"p2",patella.Fingerprint,p.ParentLocalPositionMeters,p.DerivativeMetersPerRadian,V(),V(),V(0,0,rate),V(0,0,1));
        var tip=V(3.5*Math.Cos(q)+Math.Sin(q),3.5*Math.Sin(q)-Math.Cos(q));
        MusclePathAnchor[] anchors=[new("p1","femur",V(3)),new("p2","patella",local),new("p3","tibia",V(3.5,-1))];
        var cylinder=new AvatarCylinderMusclePath("muscle","geometry","analytic-control",anchors[0],anchors[1],"femur",2,10,1,V(),V(1),V(0,1));
        var path=new AvatarRoutedMusclePath("muscle","geometry","analytic-control",anchors,[new(0,cylinder)],[rule]);
        return new(path,patella,anchors,[new("p1","femur",V(3),V()),new("p3","tibia",tip,Cross(V(0,0,rate),tip))],
            [new(0,new("femur",V(),V(1),V(0,1),V(),V()),0,q)],[moving]);
    }
    [Fact]
    public void StraightAssemblyRetainsTheWholePolylineAndBothViaReactions()
    {
        MusclePathAnchor[] a=[new("a","a-body",V()),new("b","b-body",V(1)),new("c","c-body",V(1,2))];
        MusclePathPoint[] p=a.Select((v,i)=>new MusclePathPoint(v.AttachmentId,v.BodyId,v.LocalPositionMeters,V(.1*i,.2*i))).ToArray();
        var old=new AvatarMusclePath("muscle","geometry","control",a).Measure("geometry",p);
        var path=new AvatarRoutedMusclePath("muscle","geometry","control",a,[],[]);var s=path.Measure("geometry",p,[],[]);
        Near(old.MusculotendonLengthMeters,s.MusculotendonLengthMeters);Near(old.MusculotendonVelocityMetersPerSecond,s.MusculotendonVelocityMetersPerSecond);
        var expected=old.ApplyTension(37);var actual=s.ApplyTension(37);Assert.Empty(actual.ConstraintTorques);
        for(int i=0;i<3;i++)Vec(expected.PointForces[i].ForceNewtons,actual.PointForces[i].ForceNewtons);
        Vec(V(-37,37),actual.PointForces[1].ForceNewtons);Near(0,actual.PowerResidualWatts);
    }
    [Theory]
    [InlineData(1.8,-.4,0)]
    [InlineData(1.8,0,37)]
    [InlineData(2.1,.7,37)]
    [InlineData(2.4,-.4,37)]
    public void CylinderAndMovingPatellaPreserveAnalyticJointLoadAndWork(double q,double rate,double tension)
    {
        var f=Create(q,rate);var s=f.Path.Measure("geometry",f.Physical,f.Guides,f.Virtual);var loads=s.ApplyTension(tension);
        double expected=2*Math.Sqrt(5)+2*(q-2*Math.Acos(2.0/3))+Math.Sqrt(1.25);
        Near(expected,s.MusculotendonLengthMeters);Near(2*rate,s.MusculotendonVelocityMetersPerSecond);
        Near(-2*tension*rate,loads.PowerOnBodiesWatts);Near(0,loads.PowerResidualWatts);
        Assert.Equal(5,loads.PointForces.Count);Assert.Equal(2,loads.ConstraintTorques.Count);
        Assert.Equal(5,loads.PointForces.Select(p=>p.Point.AttachmentId).Distinct().Count());
        Assert.DoesNotContain(loads.PointForces,p=>p.Point.BodyId=="patella");
        Vec(V(),loads.PointForces.Aggregate(V(),(a,p)=>a+p.ForceNewtons));
        var totalMoment=loads.PointForces.Aggregate(V(),(a,p)=>a+Cross(p.Point.PositionMeters,p.ForceNewtons))+
            loads.ConstraintTorques.Aggregate(V(),(a,t)=>a+t.TorqueNewtonMeters);Vec(V(),totalMoment);
        var tibialMoment=loads.PointForces.Where(p=>p.Point.BodyId=="tibia").Aggregate(V(),(a,p)=>a+Cross(p.Point.PositionMeters,p.ForceNewtons))+
            loads.ConstraintTorques.Where(t=>t.BodyId=="tibia").Aggregate(V(),(a,t)=>a+t.TorqueNewtonMeters);
        Near(-2*tension,tibialMoment.Z);
    }
    [Theory]
    [InlineData("missing-virtual")]
    [InlineData("extra-virtual")]
    [InlineData("raw-virtual")]
    [InlineData("wrong-mechanism")]
    [InlineData("wrong-constraint")]
    [InlineData("duplicate-physical")]
    [InlineData("missing-guide")]
    [InlineData("extra-guide")]
    [InlineData("wrong-span")]
    [InlineData("wrong-body")]
    [InlineData("wrong-geometry")]
    public void UnownedOrIncompleteObservationsReject(string change)
    {
        Assert.ThrowsAny<ArgumentException>(()=>
        {
            var f=Create();var physical=f.Physical;var guides=f.Guides;var moved=f.Virtual;string geometry="geometry";
            switch(change)
            {
                case "missing-virtual":moved=[];break;
                case "extra-virtual":moved=[moved[0],moved[0]];break;
                case "raw-virtual":physical=[..physical,moved[0].Point];moved=[];break;
                case "wrong-mechanism":moved=[new(moved[0].Constraint,"p2",new string('B',64),V(),V(),V(),V(),V(),V(0,0,1))];break;
                case "wrong-constraint":moved=[new(new("patella","other","tibia",f.Patella.Fingerprint),"p2",f.Patella.Fingerprint,V(),V(),V(),V(),V(),V(0,0,1))];break;
                case "duplicate-physical":physical=[physical[0],physical[0]];break;
                case "missing-guide":guides=[];break;
                case "extra-guide":guides=[guides[0],guides[0]];break;
                case "wrong-span":guides=[new(1,guides[0].Frame,0,2.1)];break;
                case "wrong-body":physical=[new("p1","other",V(3),V()),physical[1]];break;
                case "wrong-geometry":geometry="other";break;
            }
            f.Path.Measure(geometry,physical,guides,moved);
        });
    }
    [Fact]
    public void EveryConstrainedMechanismParticipatesInPathIdentity()
    {
        var f=Create();var a=f.Anchors;
        var cylinder=new AvatarCylinderMusclePath("muscle","geometry","analytic-control",a[0],a[1],"femur",2,10,1,V(),V(1),V(0,1));
        var altered=new AvatarRoutedMusclePath("muscle","geometry","analytic-control",a,[new(0,cylinder)],[new("patella","femur","tibia",new string('B',64))]);
        Assert.NotEqual(f.Path.Fingerprint,altered.Fingerprint);
        a[0]=new("changed","femur",V());Assert.Equal("p1",f.Path.Anchors[0].AttachmentId);
    }
    [Fact]
    public void UnityCopyProducesTheSameCompleteConstrainedLoads()
    {
        var f=Create();var a=f.Anchors.Select(v=>new U.MusclePathAnchor(v.AttachmentId,v.BodyId,UV(v.LocalPositionMeters))).ToArray();var b=f.Virtual[0];
        var rule=new U.PatellarMuscleConstraint("patella","femur","tibia",f.Patella.Fingerprint);
        var cylinder=new U.AvatarCylinderMusclePath("muscle","geometry","analytic-control",a[0],a[1],"femur",2,10,1,UV(V()),UV(V(1)),UV(V(0,1)));
        var path=new U.AvatarRoutedMusclePath("muscle","geometry","analytic-control",a,[new(0,cylinder)],[rule]);
        var physical=f.Physical.Select(p=>new U.MusclePathPoint(p.AttachmentId,p.BodyId,UV(p.PositionMeters),UV(p.VelocityMetersPerSecond))).ToArray();
        U.PatellarMuscleObservation[] moved=[new(rule,"p2",f.Patella.Fingerprint,UV(b.Point.PositionMeters),UV(b.JacobianMetersPerRadian),UV(b.ParentPointVelocity),UV(b.ParentAngularVelocity),UV(b.ChildAngularVelocity),UV(b.PositiveCoordinateAxis))];
        U.MuscleCylinderObservation[] guides=[new(0,new("femur",UV(V()),UV(V(1)),UV(V(0,1)),UV(V()),UV(V())),0,2.1)];
        var expected=f.Path.Measure("geometry",f.Physical,f.Guides,f.Virtual);var actual=path.Measure("geometry",physical,guides,moved);
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(expected.ApplyTension(37)),JsonSerializer.Serialize(actual.ApplyTension(37)));
    }
}
