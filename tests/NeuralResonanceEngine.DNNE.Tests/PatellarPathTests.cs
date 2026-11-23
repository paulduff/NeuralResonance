using System.Globalization;
using System.Reflection;
using System.Text.Json;
using NRE.SimAvatar;
using Xunit.Abstractions;
using U=Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

// Source kinematics and massless force transfer, not a dynamically integrated
// kneecap, native body attachment, physiological muscle or standing trial.
public sealed class PatellarPathTests(ITestOutputHelper output)
{
    record Curve(double[] Knots,double[] Values,string OriginalKnots,string OriginalValues);
    record Point(string Muscle,string Id,double[] LocalMeters,string OriginalLocation);
    record SideData(string Side,string Coordinate,string Parent,string Patella,double[] ParentOffsetMeters,double ZTranslationMeters,
        Dictionary<string,Curve> Curves,Point[] Points,double[] SupportedRange,double SourceMassKg);
    record Input(string Version,string SourceSha256,bool IdentityCoordinateCoupling,SideData[] Sides);
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static MusclePathVector V(double x=0,double y=0,double z=0)=>new(x,y,z);
    static MusclePathVector V(double[] p)=>V(p[0],p[1],p[2]);
    static U.MusclePathVector UV(MusclePathVector v)=>new(v.X,v.Y,v.Z);
    static void Near(double a,double b,double bound=8e-12)=>Assert.True(double.IsFinite(b)&&Math.Abs(a-b)<=bound,$"Expected {a:R}, actual {b:R}, bound {bound:R}");
    static void Vec(MusclePathVector a,MusclePathVector b,double bound=8e-12){Near(a.X,b.X,bound);Near(a.Y,b.Y,bound);Near(a.Z,b.Z,bound);}
    static Input Source(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PatellarSource.json");Assert.NotNull(stream);return JsonSerializer.Deserialize<Input>(stream,Json)!;}
    static MuscleCoordinateSpline CurveFor(Curve c)=>new(c.Knots,c.Values);
    static AvatarPatellarPath Mechanism(SideData s,string sha)=>new(s.Coordinate,s.Parent,s.Patella,sha,V(s.ParentOffsetMeters),s.ZTranslationMeters,
        CurveFor(s.Curves["angle"]),CurveFor(s.Curves["x"]),CurveFor(s.Curves["y"]));
    static double[] Numbers(string value)=>value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public void EndpointConditionReproducesLinearQuadraticAndCubicControls(int count)
    {
        int degree=Math.Min(3,count-1);double[] x=Enumerable.Range(0,count).Select(i=>.1*i*(i+1)).ToArray();
        double F(double q)=>Math.Pow(q,degree)+.3*q-.2;
        var c=new MuscleCoordinateSpline(x,x.Select(F));
        foreach(double at in Enumerable.Range(0,51).Select(i=>i==50?x[^1]:x[^1]*i/50))
        {
            var r=c.Evaluate(at);
            Near(F(at),r.Value);Near(degree*Math.Pow(at,degree-1)+.3,r.FirstDerivative);
            Near(degree==1?0:degree*(degree-1)*Math.Pow(at,degree-2),r.SecondDerivative);
        }
    }
    [Fact]
    public void InputArraysCannotRewriteADeclaredCurve()
    {
        double[] x=[0,.5,1,2],y=[1,2,3,5];var c=new MuscleCoordinateSpline(x,y);var before=c.Evaluate(.8);
        x[1]=100;y[1]=100;Assert.Equal(before,c.Evaluate(.8));Assert.Equal(.5,c.Knots[1]);
    }
    [Theory]
    [InlineData("duplicate")]
    [InlineData("decreasing")]
    [InlineData("nonfinite")]
    [InlineData("missing")]
    [InlineData("size")]
    public void InvalidCurveDeclarationsReject(string kind)
    {
        double[] x=[0,1,2],y=[0,1,4];
        switch(kind){case "duplicate":x[1]=0;break;case "decreasing":x[1]=3;break;case "nonfinite":y[1]=double.NaN;break;case "missing":x=[];y=[];break;case "size":y=[0,1];break;}
        Assert.Throws<ArgumentException>(()=>new MuscleCoordinateSpline(x,y));
    }
    [Theory]
    [InlineData(-0.0000001)]
    [InlineData(2.45)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SourceRangeRejectsExtrapolationOrHiddenClipping(double q)
    {
        var source=Source();var m=Mechanism(source.Sides[0],source.SourceSha256);
        Assert.Throws<ArgumentOutOfRangeException>(()=>m.Measure(q,V()));
    }
    [Fact]
    public void SourceIdentityAndNativeRangeGapAreExplicit()
    {
        var s=Source();Assert.Equal("retained-patellar-source-data-v1",s.Version);Assert.True(s.IdentityCoordinateCoupling);
        Assert.Equal("3F5C5F23E486073F2AD2AA4A4967FFE2FCDD582B1E355512BC54F70C36376BF4",s.SourceSha256);
        Assert.Equal(new[]{"l","r"},s.Sides.Select(v=>v.Side));
        foreach(var side in s.Sides)
        {
            Assert.Equal(9,side.Points.Length);Assert.Equal(13,side.Curves["angle"].Knots.Length);Assert.Equal(.0862,side.SourceMassKg);
            Assert.Equal(new[]{0,2.0944},side.SupportedRange);Assert.True(AvatarArticulatedBody.MaximumKneeAngleRadians>side.SupportedRange[1]);
            foreach(var curve in side.Curves.Values){Assert.Equal(Numbers(curve.OriginalKnots),curve.Knots);Assert.Equal(Numbers(curve.OriginalValues),curve.Values);}
            foreach(var p in side.Points)Assert.Equal(Numbers(p.OriginalLocation),p.LocalMeters);
        }
    }
    [Fact]
    public void PureRotationAndTranslationGiveAnalyticPointDerivatives()
    {
        var angle=new MuscleCoordinateSpline([0,1],[0,1]);var x=new MuscleCoordinateSpline([0,1],[0,.1]);var y=new MuscleCoordinateSpline([0,1],[0,-.2]);
        var m=new AvatarPatellarPath("q","femur","patella",new string('A',64),V(.3,.4,.5),.1,angle,x,y);
        var p=m.Measure(.5,V(.1,.2,.3));double c=Math.Cos(.5),s=Math.Sin(.5);var r=V(c*.1-s*.2,s*.1+c*.2,.3);
        Vec(V(.35,.3,.6)+r,p.ParentLocalPositionMeters);Vec(V(.1-r.Y,-.2+r.X),p.DerivativeMetersPerRadian);
        Vec(V(-r.X,-r.Y),p.SecondDerivativeMetersPerRadianSquared);
    }
    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(2)]
    public void ConstraintReactionBalancesPowerAndAddsNoNetCouple(double speed)
    {
        var t=new PatellarForceTransfer(V(.3,.2,.1),V(3,-4,5),V(.04,-.02,.01),V(.1,.2,-.3),V(.2,.3,-.1),V(.2+speed,.3,-.1),V(1));
        Near(.25,t.GeneralizedForceNewtonMeters);Near(speed,t.RelativeCoordinateSpeedRadiansPerSecond);
        Vec(V(),t.ChildTorqueNewtonMeters+t.ParentCounterTorqueNewtonMeters);Vec(V(3,-4,5),t.ParentPointForceNewtons);
        Near(t.MovingPointPowerWatts,t.DeliveredPowerWatts);Near(0,t.PowerResidualWatts);
    }
    [Theory]
    [InlineData("axis")]
    [InlineData("force")]
    [InlineData("jacobian")]
    [InlineData("point")]
    public void InvalidForceTransferRejects(string kind)
    {
        var invalid=V(double.NaN);
        Assert.Throws<ArgumentException>(()=>new PatellarForceTransfer(kind=="point"?invalid:V(),kind=="force"?invalid:V(1),
            kind=="jacobian"?invalid:V(.01),V(),V(),V(),kind=="axis"?V(2):V(1)));
    }
    [Fact]
    public void RetainEverySourcePatellarPointAtEveryKnotAndMidpointWithUnityParity()
    {
        var input=Source();int index=0;
        foreach(var side in input.Sides)
        {
            var m=Mechanism(side,input.SourceSha256);var curves=side.Curves;
            var u=new U.AvatarPatellarPath(side.Coordinate,side.Parent,side.Patella,input.SourceSha256,UV(V(side.ParentOffsetMeters)),side.ZTranslationMeters,
                new(curves["angle"].Knots,curves["angle"].Values),new(curves["x"].Knots,curves["x"].Values),new(curves["y"].Knots,curves["y"].Values));
            Assert.Equal(m.Fingerprint,u.Fingerprint);
            var knots=curves["angle"].Knots;var samples=knots.Concat(knots.Zip(knots.Skip(1)).Select(p=>(p.First+p.Second)/2)).OrderBy(v=>v).ToArray();Assert.Equal(25,samples.Length);
            foreach(var point in side.Points)
            foreach(double q in samples)
            {
                var p=m.Measure(q,V(point.LocalMeters));var up=u.Measure(q,UV(V(point.LocalMeters)));
                Assert.Equal(JsonSerializer.Serialize(p,Json),JsonSerializer.Serialize(up,Json));
                var force=V(31,-17,8);var velocity=V(.02,-.03,.01);var parentAngular=V(.2,.3,.1);var childAngular=V(.2,.3,1.4);var axis=V(0,0,1);
                var transfer=new PatellarForceTransfer(p.ParentLocalPositionMeters,force,p.DerivativeMetersPerRadian,velocity,parentAngular,childAngular,axis);
                var ut=new U.PatellarForceTransfer(UV(p.ParentLocalPositionMeters),UV(force),UV(p.DerivativeMetersPerRadian),UV(velocity),UV(parentAngular),UV(childAngular),UV(axis));
                Assert.Equal(JsonSerializer.Serialize(transfer,Json),JsonSerializer.Serialize(ut,Json));Near(0,transfer.PowerResidualWatts);
                output.WriteLine("PATELLAR_PATH_JSON "+JsonSerializer.Serialize(new{index=index++,side=side.Side,pointId=point.Id,q,localPoint=point.LocalMeters,
                    point=p,transfer,force,parentVelocity=velocity,parentAngular,childAngular,axis},Json));
            }
        }
        Assert.Equal(450,index);
    }
}
