using System.Text.Json;
using NRE.SimAvatar;
using Xunit.Abstractions;
using U=Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

// Analytic/kinematic geometry controls only. No neural, native PhysX, muscle
// dynamics or calibrated knee/patellar model is executed by these cases.
public sealed class CylinderMusclePathTests(ITestOutputHelper output)
{
    static MusclePathVector V(double x=0,double y=0,double z=0)=>new(x,y,z);
    static MusclePathVector Cross(MusclePathVector a,MusclePathVector b)=>MuscleCylinderFrame.Cross(a,b);
    static MusclePathPoint P(string id,MusclePathVector p,MusclePathVector v=default)=>new(id,id=="a"?"femur":"tibia",p,v);
    static MuscleCylinderFrame Frame()=>new("guide",V(),V(1),V(0,1),V(),V());
    static AvatarCylinderMusclePath Path(int sign=1,double radius=1,double half=2,MusclePathVector local=default)=>
        new("knee-fixture","geometry","analytic cylinder fixture; not calibrated anatomy",new("a","femur",V()),new("b","tibia",V()),"guide",radius,half,sign,local,V(1),V(0,1));
    static void Near(double a,double b,double bound=2e-12)=>Assert.True(double.IsFinite(b)&&Math.Abs(a-b)<=bound,$"Expected {a:R}, actual {b:R}, bound {bound:R}");
    static void Vec(MusclePathVector a,MusclePathVector b,double bound=2e-12){Near(a.X,b.X,bound);Near(a.Y,b.Y,bound);Near(a.Z,b.Z,bound);}
    static U.MusclePathVector UV(MusclePathVector v)=>new(v.X,v.Y,v.Z);
    static void Parity(AvatarCylinderMusclePath path,MuscleCylinderFrame f,MusclePathPoint a,MusclePathPoint b,double aa,double ab,double tension)
    {
        var u=new U.AvatarCylinderMusclePath("knee-fixture","geometry","analytic cylinder fixture; not calibrated anatomy",
            new("a","femur",UV(V())),new("b","tibia",UV(V())),"guide",path.RadiusMeters,path.HalfLengthMeters,path.Direction,
            UV(path.GuideLocalOrigin),UV(path.GuideLocalX),UV(path.GuideLocalY));
        var uf=new U.MuscleCylinderFrame("guide",UV(f.OriginMeters),UV(f.X),UV(f.Y),UV(f.VelocityMetersPerSecond),UV(f.AngularVelocityRadiansPerSecond));
        var us=u.Measure("geometry",uf,new(a.AttachmentId,a.BodyId,UV(a.PositionMeters),UV(a.VelocityMetersPerSecond)),
            new(b.AttachmentId,b.BodyId,UV(b.PositionMeters),UV(b.VelocityMetersPerSecond)),aa,ab);
        var s=path.Measure("geometry",f,a,b,aa,ab);var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Equal(path.Fingerprint,u.Fingerprint);Assert.Equal(JsonSerializer.Serialize(s,options),JsonSerializer.Serialize(us,options));
        Assert.Equal(JsonSerializer.Serialize(s.ApplyTension(tension),options),JsonSerializer.Serialize(us.ApplyTension(tension),options));
    }
    static void Balance(CylinderMusclePathSnapshot s,double tension)
    {
        var force=s.ApplyTension(tension);var total=V();var moment=V();
        foreach(var p in force.PointForces){total+=p.ForceNewtons;moment+=Cross(p.Point.PositionMeters,p.ForceNewtons);}
        Vec(V(),total);Vec(V(),moment);Near(force.PowerFromPathShorteningWatts,force.PowerOnBodiesWatts);
    }
    [Theory]
    [InlineData(1,0)]
    [InlineData(-1,0)]
    [InlineData(1,0.7)]
    [InlineData(-1,0.7)]
    public void TangentsAndHelicalArcHaveClosedFormLengthAndBalancedWrench(int sign,double height)
    {
        var a=P("a",V(-2,0,-height/2));var b=P("b",V(2,0,height/2));var p=Path(sign);
        var s=p.Measure("geometry",Frame(),a,b,Math.PI,Math.PI+sign*Math.PI);
        double h=2*Math.Sqrt(3)+Math.PI/3;
        Assert.True(s.Wrapped);Near(Math.Sqrt(h*h+height*height),s.MusculotendonLengthMeters);Near(Math.PI/3,s.ArcRadians);
        Near(-.5,s.Points[1].PositionMeters.X);Near(.5,s.Points[2].PositionMeters.X);
        Near(-sign*Math.Sqrt(3)/2,s.Points[1].PositionMeters.Y);Near(-sign*Math.Sqrt(3)/2,s.Points[2].PositionMeters.Y);
        Balance(s,40);Parity(p,Frame(),a,b,Math.PI,Math.PI+sign*Math.PI,40);
    }
    [Fact]
    public void ClearRouteEqualsOriginalStraightPath()
    {
        var a=P("a",V(2,0,.1),V(.1,.2));var b=P("b",V(2*Math.Cos(.2),2*Math.Sin(.2),-.1),V(-.3,.1));
        var s=Path().Measure("geometry",Frame(),a,b,0,.2);Assert.False(s.Wrapped);Assert.Equal(2,s.Points.Count);
        var old=new AvatarMusclePath("straight","geometry","fixture",[new("a","femur",V()),new("b","tibia",V())]).Measure("geometry",[a,b]);
        Near(old.MusculotendonLengthMeters,s.MusculotendonLengthMeters);Near(old.MusculotendonVelocityMetersPerSecond,s.MusculotendonVelocityMetersPerSecond);
        for(int i=0;i<2;i++)Vec(old.ApplyTension(12).PointForces[i].ForceNewtons,s.ApplyTension(12).PointForces[i].ForceNewtons);
    }
    [Fact]
    public void CommonRigidMotionCannotPerformInternalWork()
    {
        var f=new MuscleCylinderFrame("guide",V(.3,.1,-.2),V(0,1),V(0,0,1),V(.3,-.2,.4),V(.8,.6,-.2));
        var pa=f.World(-2,0,-.4);var pb=f.World(2,0,.4);
        var s=Path().Measure("geometry",f,P("a",pa,f.VelocityAt(pa)),P("b",pb,f.VelocityAt(pb)),Math.PI,2*Math.PI);
        Near(0,s.MusculotendonVelocityMetersPerSecond);Near(0,s.ApplyTension(27).PowerOnBodiesWatts);Balance(s,27);
    }
    [Fact]
    public void TendonZeroForceIsZeroAndSnapshotIsReusable()
    {
        var s=Path().Measure("geometry",Frame(),P("a",V(-2)),P("b",V(2)),Math.PI,2*Math.PI);
        foreach(var f in s.ApplyTension(0).PointForces)Vec(V(),f.ForceNewtons);
        Balance(s,31);Balance(s,0);Balance(s,31);
    }
    [Fact]
    public void GuideLocationAndRoutingChangeIdentity()
    {
        Assert.NotEqual(Path().Fingerprint,Path(local:V(.01)).Fingerprint);
        Assert.NotEqual(Path().Fingerprint,Path(-1).Fingerprint);
        Assert.NotEqual(Path().Fingerprint,Path(radius:1.01).Fingerprint);
        Assert.NotEqual(Path().Fingerprint,Path(half:3).Fingerprint);
    }
    [Fact]
    public void DeclaredRouteDoesNotSwitchToShorterSide()
    {
        double aa=.2,ab=5.6;var a=P("a",V(2*Math.Cos(aa),2*Math.Sin(aa)));var b=P("b",V(2*Math.Cos(ab),2*Math.Sin(ab)));
        var s=Path().Measure("geometry",Frame(),a,b,aa,ab);
        Assert.True(s.Wrapped);Assert.True(s.ArcRadians>Math.PI);Balance(s,40);
        Assert.Throws<ArgumentException>(()=>Path().Measure("geometry",Frame(),a,b,aa,ab-2*Math.PI));
    }
    [Fact]
    public void TangencyEngagementHasContinuousLengthAndEndpointForces()
    {
        double boundary=2*Math.Acos(.5);var p=Path();var a=P("a",V(2));
        CylinderMusclePathSnapshot At(double angle)=>p.Measure("geometry",Frame(),a,P("b",V(2*Math.Cos(angle),2*Math.Sin(angle))),0,angle);
        var before=At(boundary-1e-7);var after=At(boundary+1e-7);
        Assert.False(before.Wrapped);Assert.True(after.Wrapped);
        Near(before.MusculotendonLengthMeters,after.MusculotendonLengthMeters,3e-7);
        Vec(before.ApplyTension(1).PointForces[0].ForceNewtons,after.ApplyTension(1).PointForces[0].ForceNewtons,1e-7);
        Vec(before.ApplyTension(1).PointForces[^1].ForceNewtons,after.ApplyTension(1).PointForces[^1].ForceNewtons,2e-7);
    }
    [Theory]
    [InlineData("radius")]
    [InlineData("half")]
    [InlineData("direction")]
    [InlineData("parallel")]
    [InlineData("scale")]
    [InlineData("velocity")]
    public void InvalidGuideDefinitionsReject(string kind)
    {
        Assert.Throws<ArgumentException>(()=>
        {
            if(kind=="radius")Path(radius:double.NaN);
            else if(kind=="half")Path(half:0);
            else if(kind=="direction")Path(sign:0);
            else new MuscleCylinderFrame("guide",V(),kind=="scale"?V(2):V(1),kind=="parallel"?V(1):V(0,1),
                kind=="velocity"?V(double.NaN):V(),V());
        });
    }
    [Theory]
    [InlineData("inside")]
    [InlineData("surface")]
    [InlineData("width")]
    [InlineData("phase")]
    [InlineData("winding")]
    [InlineData("geometry")]
    [InlineData("body")]
    [InlineData("nonfinite")]
    [InlineData("collapsed")]
    public void InvalidSamplesRejectWithoutChangingDeclaration(string condition)
    {
        var p=Path();string before=p.Fingerprint;var a=P("a",V(-2));var b=P("b",V(2));var f=Frame();double aa=Math.PI,ab=2*Math.PI;string geometry="geometry";
        switch(condition)
        {
            case "inside":a=P("a",V(-.5));break;
            case "surface":a=P("a",V(-1));break;
            case "width":a=P("a",V(-2,0,3));b=P("b",V(2,0,3));break;
            case "phase":aa+=.01;break;
            case "winding":ab+=2*Math.PI;break;
            case "geometry":geometry="other";break;
            case "body":a=new("a","other",V(-2),V());break;
            case "nonfinite":a=P("a",V(double.NaN));break;
            case "collapsed":b=P("b",V(-2));ab=Math.PI;break;
        }
        var error=Record.Exception(()=>p.Measure(geometry,f,a,b,aa,ab));
        if(condition is "inside" or "surface" or "width" or "collapsed")Assert.IsType<InvalidOperationException>(error);
        else Assert.IsType<ArgumentException>(error);
        Assert.Equal(before,p.Fingerprint);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidTensionRejects(double tension)
    {
        var s=Path().Measure("geometry",Frame(),P("a",V(-2)),P("b",V(2)),Math.PI,2*Math.PI);
        Assert.Throws<ArgumentException>(()=>s.ApplyTension(tension));Balance(s,2);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    public void RetainFiniteSourceScaleCasesForIndependentLengthGradientAndPower(int id)
    {
        double r=id<12?.025:.042;int kind=id%6,sign=id%12<6?1:-1;
        double ra=r*(kind==5?1.005:2.5),rb=3*r,aa=.2;
        double arc=kind switch{1=>.1,2=>.6,3=>1.5,4=>2.45,_=>.8};
        double ab=aa+sign*(kind==0?.4:Math.Acos(r/ra)+Math.Acos(r/rb)+arc);
        var f=new MuscleCylinderFrame("guide",V(.3,-.2,.1),id%3==0?V(0,1):V(1),id%3==0?V(0,0,1):V(0,1),V(.04,-.02,.03),V(.2,-.1,.3));
        var a=P("a",f.World(ra*Math.Cos(aa),ra*Math.Sin(aa),-.3*r),V(.1,-.2,.05));
        var b=P("b",f.World(rb*Math.Cos(ab),rb*Math.Sin(ab),kind%2==0?-.3*r:.4*r),V(-.1,.15,.02));
        var path=Path(sign,r,2*r);var s=path.Measure("geometry",f,a,b,aa,ab);Balance(s,40);Parity(path,f,a,b,aa,ab,40);
        Assert.Equal(kind!=0,s.Wrapped);
        output.WriteLine("CYLINDER_PATH_JSON "+JsonSerializer.Serialize(new{id,radius=r,halfLength=2*r,direction=sign,originAzimuth=aa,insertionAzimuth=ab,
            frame=f,origin=a,insertion=b,snapshot=s,tension=40,forces=s.ApplyTension(40)},new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
