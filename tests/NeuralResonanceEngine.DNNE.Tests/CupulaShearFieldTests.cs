using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Declared synthetic geometry. These controls do not calibrate a human organ.
public sealed class CupulaShearFieldTests
{
    private static AvatarCupulaShearField Field(AvatarCupulaCanal canal=AvatarCupulaCanal.Horizontal,double radius=.0005)=>
        new("fixture-right",canal,"synthetic shear control",new string('A',64),radius,.0002,.37);
    private static AvatarCupulaBundleAttachment Attachment(double x=0,double length=.00005,int polarity=1,AvatarCupulaCanal canal=AvatarCupulaCanal.Horizontal)=>
        new(Guid.Parse("6abfa889-bb51-4719-bb63-b1041096b057"),"fixture-right",canal,"synthetic lower boundary",x,length,polarity);
    private static AvatarCanalMechanicalPoint Point(double q,double flow=0)=>new(new(11,12,13,q,2*q,3*q),new(14,15,16,flow,2*flow,3*flow));
    private static void Close(double expected,double actual,double relative=1e-12,double absolute=1e-30)=>
        Assert.InRange(Math.Abs(actual-expected),0,absolute+relative*Math.Abs(expected));

    [Fact]
    public void FixedPerimeterAndCentralMaximumDifferFromMeanDisplacement()
    {
        var field=Field();double q=field.AreaSquareMeters*1e-6;
        var centre=field.Evaluate(q,0,0,0);Close(1e-6,centre.MeanDisplacementMeters);Close(2e-6,centre.DisplacementMeters);
        foreach(var (x,y) in new[]{(1d,0d),(-1d,0d),(0d,1d),(0d,-1d)})Assert.Equal(0,field.Evaluate(q,0,x,y).DisplacementMeters);
        Assert.Equal(0,centre.ShearGradientX);Assert.Equal(0,centre.ShearGradientY);
    }

    [Theory]
    [InlineData(-1e-12)]
    [InlineData(0)]
    [InlineData(1e-12)]
    public void IndependentDiskQuadratureRecoversTheSuppliedSolidVolume(double q)
    {
        var field=Field();double integral=0;
        // Gauss quadrature in r² and periodic trapezoids in angle integrate
        // this polynomial exactly, independently of the production normalization.
        foreach(double t in new[]{.5-.5/Math.Sqrt(3),.5+.5/Math.Sqrt(3)})
            for(int i=0;i<8;i++)
            {
                double angle=(i+.5)*2*Math.PI/8,r=Math.Sqrt(t);
                integral+=field.AreaSquareMeters/16*field.Evaluate(q,0,r*Math.Cos(angle),r*Math.Sin(angle)).DisplacementMeters;
            }
        Close(q,integral);
    }

    [Fact]
    public void IntegratedShearStrainEnergyMatchesTheGeneralizedElasticEnergy()
    {
        var field=Field();double q=1e-12,energy=0;
        foreach(double t in new[]{.5-.5/Math.Sqrt(3),.5+.5/Math.Sqrt(3)})
            for(int i=0;i<8;i++)
            {
                double angle=(i+.5)*2*Math.PI/8,r=Math.Sqrt(t);var s=field.Evaluate(q,0,r*Math.Cos(angle),r*Math.Sin(angle));
                energy+=.5*field.ShearModulusPascal*field.ThicknessMeters*field.AreaSquareMeters/16*
                    (s.ShearGradientX*s.ShearGradientX+s.ShearGradientY*s.ShearGradientY);
            }
        var centre=field.Evaluate(q,0,0,0);Close(centre.ElasticEnergyJoules,energy);
        Close(.5*centre.ElasticPressurePascal*q,energy);Assert.True(energy>0);
    }

    [Theory]
    [InlineData(-.6)]
    [InlineData(0)]
    [InlineData(.6)]
    [InlineData(.8)]
    public void AttachedBundleMatchesTheSpatialTipRelativeToItsFixedRoot(double x)
    {
        var field=Field();var point=Point(1e-12,2e-11);var bundle=field.Project(point,Attachment(x));
        var tip=field.Evaluate(point.VolumeMeters3.CupulaHC,point.FlowMeters3PerSecond.CupulaHC,bundle.RootXOverRadius,bundle.TipYOverRadius);
        Close(tip.DisplacementMeters*1e9,bundle.BundleDisplacementNanometers);
        Close(tip.VelocityMetersPerSecond*1e9,bundle.BundleVelocityNanometersPerSecond);
        Assert.Equal(Attachment(x).HairCellId,bundle.HairCellId);Assert.Equal(field.Fingerprint,bundle.GeometryFingerprint);
    }

    [Fact]
    public void PositionAndBundleLengthAlterLocalDeflectionWithoutChangingGlobalVolume()
    {
        var field=Field();var p=Point(1e-12);
        var centre=field.Project(p,Attachment());var peripheral=field.Project(p,Attachment(.6));var mirrored=field.Project(p,Attachment(-.6));
        var shorter=field.Project(p,Attachment(length:.000025));
        Assert.True(centre.BundleDisplacementNanometers>peripheral.BundleDisplacementNanometers);
        Assert.Equal(peripheral.BundleDisplacementNanometers,mirrored.BundleDisplacementNanometers);
        Assert.True(centre.BundleDisplacementNanometers>shorter.BundleDisplacementNanometers);
        Assert.NotEqual(centre.MeanCupulaDisplacementMeters*1e9,centre.BundleDisplacementNanometers);
        Assert.Equal(p.VolumeMeters3.CupulaHC,centre.SolidVolumeCubicMeters);
    }

    [Theory]
    [InlineData(AvatarCupulaCanal.Horizontal,1)]
    [InlineData(AvatarCupulaCanal.Anterior,2)]
    [InlineData(AvatarCupulaCanal.Posterior,3)]
    public void OnlyTheSelectedSolidCupulaCoordinateDrivesItsBundle(AvatarCupulaCanal canal,int multiplier)
    {
        var field=Field(canal);var p=Point(1e-12,2e-12);var a=Attachment(canal:canal);var original=field.Project(p,a);
        var altered=p with{VolumeMeters3=p.VolumeMeters3 with{EndolymphHC=999,EndolymphAC=-888,EndolymphPC=777},
            FlowMeters3PerSecond=p.FlowMeters3PerSecond with{EndolymphHC=-999,EndolymphAC=888,EndolymphPC=-777}};
        Assert.Equal(original,field.Project(altered,a));Close(multiplier*1e-12,original.SolidVolumeCubicMeters);
        Close(multiplier*2e-12,original.SolidFlowCubicMetersPerSecond);
    }

    [Fact]
    public void ExcitatoryPolarityReversesTheSensoryCoordinateWithoutReversingMechanics()
    {
        var field=Field();var point=Point(1e-12,2e-12);var plus=field.Project(point,Attachment());var minus=field.Project(point,Attachment(polarity:-1));
        Assert.Equal(-plus.BundleDisplacementNanometers,minus.BundleDisplacementNanometers);
        Assert.Equal(-plus.BundleVelocityNanometersPerSecond,minus.BundleVelocityNanometersPerSecond);
        Assert.Equal(plus.SolidVolumeCubicMeters,minus.SolidVolumeCubicMeters);Assert.Equal(plus.RootShearGradientY,minus.RootShearGradientY);
        Assert.NotEqual(plus.AttachmentFingerprint,minus.AttachmentFingerprint);
    }

    [Fact]
    public void FlowChangesVelocityWithoutInventingAnElapsedDisplacement()
    {
        var field=Field();var still=field.Project(Point(1e-12),Attachment());var moving=field.Project(Point(1e-12,-3e-12),Attachment());
        Assert.Equal(still.BundleDisplacementNanometers,moving.BundleDisplacementNanometers);
        Assert.Equal(0,still.BundleVelocityNanometersPerSecond);Assert.True(moving.BundleVelocityNanometersPerSecond<0);
    }

    [Fact]
    public void ForeignAttachmentCannotBeSilentlyReboundToThisOrgan()
    {
        var field=Field();var a=Attachment();var p=Point(0);
        Assert.Throws<ArgumentException>(()=>field.Project(p,a with{OrganId="fixture-left"}));
        Assert.Throws<ArgumentException>(()=>field.Project(p,a with{Canal=AvatarCupulaCanal.Posterior}));
        Assert.Throws<ArgumentException>(()=>field.Project(p,a with{HairCellId=Guid.Empty}));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidGeometryIsRejectedInsteadOfReceivingADefault(double radius)
    { Assert.Throws<ArgumentException>(()=>Field(radius:radius)); }

    [Fact]
    public void OutsideSamplesAndProtrudingTipsAreRejectedWithoutClipping()
    {
        var field=Field();var p=Point(1e-12);
        Assert.Throws<ArgumentException>(()=>field.Evaluate(1e-12,0,1.00001,0));
        Assert.Throws<ArgumentException>(()=>field.Project(p,Attachment(.9,.001)));
        Assert.Throws<ArgumentException>(()=>field.Project(p,Attachment(1)));
        Assert.Throws<ArgumentException>(()=>field.Project(p,Attachment(length:0)));
        Assert.Throws<ArgumentException>(()=>field.Project(p,Attachment(polarity:0)));
    }

    [Fact]
    public void FiniteButUnrepresentableAttachmentHeightCannotSilentlyBecomeZero()
    {
        var field=new AvatarCupulaShearField("fixture-right",AvatarCupulaCanal.Horizontal,"range control",new string('A',64),1e50,1,1e200);
        Assert.Throws<ArgumentException>(()=>field.Project(Point(0),Attachment(length:1e-300)));
    }

    [Fact]
    public void ParametersAndCellPlacementParticipateInStableIdentity()
    {
        var f=Field();Assert.Equal(f.Fingerprint,Field().Fingerprint);Assert.NotEqual(f.Fingerprint,Field(radius:.0006).Fingerprint);
        var a=Attachment();Assert.NotEqual(a.Fingerprint,(a with{HairCellId=Guid.NewGuid()}).Fingerprint);
        Assert.NotEqual(a.Fingerprint,(a with{PlacementSource="different declared placement"}).Fingerprint);
        Assert.NotEqual(a.Fingerprint,(a with{RootXOverRadius=.1}).Fingerprint);
        Assert.Throws<ArgumentException>(()=>new AvatarCupulaShearField("fixture-right",AvatarCupulaCanal.Horizontal,"unknown","not-a-hash",.001,.001,1));
    }
}
