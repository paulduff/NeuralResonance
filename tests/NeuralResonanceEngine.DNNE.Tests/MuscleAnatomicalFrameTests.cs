using System.Globalization;
using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MuscleAnatomicalFrameTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public MuscleAnatomicalFrameTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;
    private static readonly MusclePathVector X = new(1,0,0), Y = new(0,1,0), Z = new(0,0,1), Zero = new(0,0,0);
    private static readonly string SourceHash = new('A',64), BodyHash = new('B',64);
    private static MuscleAnatomicalFrame Source(MusclePathVector origin = default) => new("tibia",origin,X,Y,Z);
    private static MuscleAnatomicalFrame Target(MusclePathVector origin = default) => new("shin",origin,Z,Y,X);
    private static AvatarMuscleFrameMap Map(double scale=1) => new(Source(new(1,2,3)),Target(new(4,5,6)),scale,SourceHash,BodyHash,"declared-test-scale");
    private static void Close(MusclePathVector expected, MusclePathVector actual, double allowance=2e-12) =>
        Assert.InRange((expected-actual).Magnitude,0,allowance);

    [Fact]
    public void Reflected_axes_origin_and_scale_map_points_in_metres()
    {
        var map=Map(2); Assert.Equal(-1,map.OrientationParity);
        Close(new(12,11,10),map.MapPoint(new(3,5,7)));
        Close(new(8,6,4),map.MapDisplacement(new(2,3,4)));
        Close(new(4,3,2),map.MapDirection(new(2,3,4)));
    }

    [Fact]
    public void Reflection_changes_axial_vector_sign_and_preserves_cross_product_rule()
    {
        var map=Map(3); var a=new MusclePathVector(2,-3,5); var b=new MusclePathVector(7,11,-13);
        var expected=MuscleAnatomicalFrame.Cross(map.MapDisplacement(a),map.MapDisplacement(b));
        Close(expected,map.MapAxialDirection(MuscleAnatomicalFrame.Cross(a,b))*9);
        Close(new(0,-1,0),map.MapAxialDirection(Y));
    }

    [Fact]
    public void Arbitrarily_rotated_frames_round_trip_points_and_directions()
    {
        double a=Math.Sqrt(.5);
        var s=new MuscleAnatomicalFrame("s",new(2,3,4),new(a,a,0),new(-a,a,0),Z);
        var t=new MuscleAnatomicalFrame("t",new(-3,5,7),Y,Z,X);
        var f=new AvatarMuscleFrameMap(s,t,.75,SourceHash,BodyHash,"forward");
        var r=new AvatarMuscleFrameMap(t,s,1/.75,BodyHash,SourceHash,"inverse");
        var p=new MusclePathVector(-2,6,9); Close(p,r.MapPoint(f.MapPoint(p)));
        Close(p,r.MapDirection(f.MapDirection(p))); Assert.Equal(1,f.OrientationParity);
    }

    [Fact]
    public void Paired_landmarks_fix_both_endpoints_without_moving_the_bone()
    {
        var sp=new MusclePathVector(.02,.03,.04); var sd=new MusclePathVector(.02,-.37,.04);
        var tp=new MusclePathVector(-.01,.05,.09); var td=new MusclePathVector(-.01,-.75,-.51);
        var s=MuscleAnatomicalFrame.FromProximalDistal("s",sp,sd,X,1);
        var t=MuscleAnatomicalFrame.FromProximalDistal("t",tp,td,Z,-1);
        var f=new AvatarMuscleFrameMap(s,t,(tp-td).Magnitude/(sp-sd).Magnitude,SourceHash,BodyHash,"paired-landmarks");
        Close(tp,f.MapPoint(sp)); Close(td,f.MapPoint(sd)); Close(tp,t.OriginMeters);
    }

    [Fact]
    public void Attachment_identity_and_original_source_frame_are_required()
    {
        var map=Map(); var a=map.BindPoint("soleus-P1","tibia",new(1,2,3));
        Assert.Equal("soleus-P1",a.AttachmentId); Assert.Equal("shin",a.BodyId); Close(new(4,5,6),a.LocalPositionMeters);
        Assert.Throws<ArgumentException>(()=>map.BindPoint("a","calcaneus",Zero));
        Assert.Throws<ArgumentException>(()=>map.BindPoint(" ","tibia",Zero));
    }

    [Fact]
    public void Mapping_identity_includes_geometry_source_basis_and_scaling_declaration()
    {
        var map=Map(); Assert.Equal(map.Fingerprint,Map().Fingerprint);
        var fingerprints=new[]{map.Fingerprint,Map(2).Fingerprint,
            new AvatarMuscleFrameMap(map.Source,map.Target,1,new('C',64),BodyHash,map.ScaleProvenance).Fingerprint,
            new AvatarMuscleFrameMap(map.Source,map.Target,1,SourceHash,new('D',64),map.ScaleProvenance).Fingerprint,
            new AvatarMuscleFrameMap(map.Source,map.Target,1,SourceHash,BodyHash,"different-declaration").Fingerprint,
            new AvatarMuscleFrameMap(Source(),map.Target,1,SourceHash,BodyHash,map.ScaleProvenance).Fingerprint};
        Assert.Equal(fingerprints.Length,fingerprints.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Reject_nonorthonormal_or_nonfinite_basis(int kind)
    {
        Assert.Throws<ArgumentException>(()=>kind switch {
            0=>new MuscleAnatomicalFrame("s",Zero,X*2,Y,Z),
            1=>new MuscleAnatomicalFrame("s",Zero,X,X,Z),
            2=>new MuscleAnatomicalFrame("s",new(double.NaN,0,0),X,Y,Z),
            _=>new MuscleAnatomicalFrame("s",Zero,X,Y,new(0,0,double.PositiveInfinity))});
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Reject_ambiguous_landmarks_or_anterior_hint(int kind)
    {
        var distal=kind==0?Zero:new MusclePathVector(0,-1,0);
        var hint=kind switch {1=>Y,2=>new(1e-10,1,0),4=>Zero,5=>new(double.NaN,0,0),_=>X};
        Assert.Throws<ArgumentException>(()=>MuscleAnatomicalFrame.FromProximalDistal("s",Zero,distal,hint,kind==3?0:1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Reject_invalid_scale(double scale) => Assert.Throws<ArgumentException>(()=>Map(scale));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Reject_missing_source_or_geometry_or_provenance(int kind) =>
        Assert.Throws<ArgumentException>(()=>new AvatarMuscleFrameMap(Source(),Target(),1,
            kind==0?"abc":SourceHash,kind==1?"":BodyHash,kind==2?" ":"declared"));

    [Fact]
    public void Reject_overflow_instead_of_clipping_coordinates() =>
        Assert.Throws<ArithmeticException>(()=>Map(4).MapPoint(new(double.MaxValue,1,1)));

    // This is a retained anatomical fixture, not a live pose fit. Neutral foot
    // axes inherit the shin similarity map because both original relative
    // source rotations cancel and both native body rotations are identity.
    // Foot size uses the explicitly DECLARED shin ratio, not a foot measurement.
    [Fact]
    public void Neutral_heel_offset_is_preserved_and_selected_capture_retained()
    {
        // Analytic two-frame control also runs when the external capture is not
        // selected. A heel-frame origin is not the ankle-frame origin.
        var heel=new AvatarMuscleFrameMap(Source(new(.05,.04,.01)),Target(),2,SourceHash,BodyHash,"declared-heel-control");
        Close(new(-.02,-.08,-.10),heel.MapPoint(Zero));
        Close(Zero,heel.MapPoint(new(.05,.04,.01)));
        string? input=Environment.GetEnvironmentVariable("NRE_MUSCLE_FRAME_INPUT");
        if(string.IsNullOrEmpty(input)) { _output.WriteLine("Retained anatomical fixture not selected; mathematical controls are separate."); return; }
        using var document=JsonDocument.Parse(File.ReadAllText(input)); var root=document.RootElement;
        Assert.Equal("paired-lower-leg-anatomical-input-v1",root.GetProperty("version").GetString());
        string sourceHash=root.GetProperty("sourceSha256").GetString()!, bodyHash=root.GetProperty("originalNativeGeometrySha256").GetString()!;
        int muscles=0,points=0;
        foreach(var side in root.GetProperty("sides").EnumerateArray())
        {
            var shin=side.GetProperty("nativeShinGeometry"); var foot=side.GetProperty("nativeFootGeometry");
            var sp=TextVector(side.GetProperty("sourceKneeInTibia")); var sd=TextVector(side.GetProperty("sourceAnkleInTibia"));
            var tp=Vector(shin.GetProperty("anchorPosition")); var td=Vector(foot.GetProperty("parentAnchorPosition"));
            var sf=MuscleAnatomicalFrame.FromProximalDistal(side.GetProperty("sourceKneeInTibia").GetProperty("parent").GetString()!,sp,sd,X,1);
            var tf=MuscleAnatomicalFrame.FromProximalDistal(shin.GetProperty("id").GetString()!,tp,td,Z,-1);
            double scale=(tp-td).Magnitude/(sp-sd).Magnitude;
            var sm=new AvatarMuscleFrameMap(sf,tf,scale,sourceHash,bodyHash,"paired-knee-ankle-landmark-ratio");
            var ac=TextVector(side.GetProperty("sourceAnkleInTalus"));
            var st=TextVector(side.GetProperty("sourceSubtalarInTalus"));
            var sc=TextVector(side.GetProperty("sourceSubtalarInCalcaneus"));
            var footOrigin=ac-st+sc;
            Assert.Equal(side.GetProperty("sourceAnkleInTibia").GetProperty("orientationText").GetString(),side.GetProperty("sourceAnkleInTalus").GetProperty("orientationText").GetString());
            Assert.Equal(side.GetProperty("sourceSubtalarInTalus").GetProperty("orientationText").GetString(),side.GetProperty("sourceSubtalarInCalcaneus").GetProperty("orientationText").GetString());
            var fm=new AvatarMuscleFrameMap(new(side.GetProperty("sourceSubtalarInCalcaneus").GetProperty("parent").GetString()!,footOrigin,sf.Anterior,sf.Superior,sf.Right),
                new(foot.GetProperty("id").GetString()!,Vector(foot.GetProperty("anchorPosition")),tf.Anterior,tf.Superior,tf.Right),scale,sourceHash,bodyHash,
                "declared-shin-ratio-for-foot-not-measured-foot-scale");
            foreach(string name in new[]{"nativeShinInitialPose","nativeFootInitialPose"})
            {
                var r=side.GetProperty(name).GetProperty("rotation"); Close(Zero,Vector(r)); Assert.Equal(1,r.GetProperty("w").GetDouble());
                Assert.All(side.GetProperty(name).GetProperty("jointPosition").EnumerateArray(),v=>Assert.Equal(0,v.GetDouble()));
            }
            Close(tp,sm.MapPoint(sp)); Close(td,sm.MapPoint(sd)); Close(Vector(foot.GetProperty("anchorPosition")),fm.MapPoint(footOrigin));
            foreach(var m in side.GetProperty("muscles").EnumerateArray())
            {
                Assert.False(m.GetProperty("requiresWrapping").GetBoolean()); Assert.Equal(0,m.GetProperty("wrapping").GetArrayLength());
                var anchors=new List<MusclePathAnchor>();
                foreach(var p in m.GetProperty("points").EnumerateArray())
                {
                    string parent=p.GetProperty("parentFrame").GetString()!; var map=parent==sf.BodyId?sm:fm;
                    var v=p.GetProperty("locationMeters"); var position=new MusclePathVector(v[0].GetDouble(),v[1].GetDouble(),v[2].GetDouble());
                    var anchor=map.BindPoint(p.GetProperty("name").GetString()!,parent,position); anchors.Add(anchor);
                    if(map==fm) Close(sm.MapPoint(sd+position-footOrigin),td+anchor.LocalPositionMeters-fm.Target.OriginMeters);
                }
                var path=new AvatarMusclePath(m.GetProperty("name").GetString()!,bodyHash,sm.Fingerprint+":"+fm.Fingerprint,anchors);
                _output.WriteLine("ANATOMICAL_PATH_JSON "+JsonSerializer.Serialize(new{side=side.GetProperty("side").GetString(),path,shinMap=sm,footMap=fm}));
                muscles++; points+=anchors.Count;
            }
        }
        Assert.Equal(10,muscles); Assert.Equal(44,points);
    }

    private static MusclePathVector Vector(JsonElement e)=>new(e.GetProperty("x").GetDouble(),e.GetProperty("y").GetDouble(),e.GetProperty("z").GetDouble());
    private static MusclePathVector TextVector(JsonElement frame)
    {
        var a=frame.GetProperty("translationText").GetString()!.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)
            .Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray(); Assert.Equal(3,a.Length); return new(a[0],a[1],a[2]);
    }
}
