namespace NRE.SimAvatar;

public enum AvatarCupulaCanal { Horizontal,Anterior,Posterior }

// Quasi-static circular shear mode, not a finite-element human reconstruction.
// u = 2 Q/A (1 - x²/R² - y²/R²) has integral Q and a fixed perimeter.
// Its elastic pressure is 8*pi*G*h*Q/A², consistent with the lambda=8*pi
// shear reduction discussed by Iversen/Rabbitt 2017. No new inertia/damping.
public sealed class AvatarCupulaShearField
{
    public const string Version="declared-circular-cupula-shear-field-v1";
    public string OrganId { get; }
    public AvatarCupulaCanal Canal { get; }
    public string GeometrySource { get; }
    public string SourceSha256 { get; }
    public double RadiusMeters { get; }
    public double ThicknessMeters { get; }
    public double ShearModulusPascal { get; }
    public double AreaSquareMeters { get; }
    public double StiffnessPascalPerCubicMeter { get; }
    public string Fingerprint { get; }

    public AvatarCupulaShearField(string organId,AvatarCupulaCanal canal,string geometrySource,string sourceSha256,
        double radiusMeters,double thicknessMeters,double shearModulusPascal)
    {
        Named(organId);Named(geometrySource);
        if(!Enum.IsDefined(canal)||sourceSha256 is null||sourceSha256.Length!=64||sourceSha256.Any(c=>!Uri.IsHexDigit(c)))
            throw new ArgumentException("Declare canal identity and geometry evidence.");
        CleftNumbers.Positive(radiusMeters);CleftNumbers.Positive(thicknessMeters);CleftNumbers.Positive(shearModulusPascal);
        OrganId=organId;Canal=canal;GeometrySource=geometrySource;SourceSha256=sourceSha256.ToUpperInvariant();
        RadiusMeters=radiusMeters;ThicknessMeters=thicknessMeters;ShearModulusPascal=shearModulusPascal;
        AreaSquareMeters=Math.PI*radiusMeters*radiusMeters;CleftNumbers.Positive(AreaSquareMeters);
        StiffnessPascalPerCubicMeter=8*Math.PI*shearModulusPascal*thicknessMeters/AreaSquareMeters/AreaSquareMeters;
        CleftNumbers.Positive(StiffnessPascalPerCubicMeter);
        Fingerprint=CleftNumbers.Fingerprint(new {Version,OrganId,Canal,GeometrySource,SourceSha256,RadiusMeters,ThicknessMeters,ShearModulusPascal,
            Shape="fixed circular perimeter; quadratic shear profile; volume-normalized solid displacement",
            Root="lower circular boundary; bundle extends along positive local y; rigid root and no-slip tip",
            Meaning="declared quasi-static closure; not measured human anatomy, dynamics or MET adaptation"});
    }

    public AvatarCupulaFieldSample Evaluate(double solidVolumeCubicMeters,double solidFlowCubicMetersPerSecond,
        double localXOverRadius,double localYOverRadius)
    {
        CleftNumbers.Finite(solidVolumeCubicMeters);CleftNumbers.Finite(solidFlowCubicMetersPerSecond);
        CleftNumbers.Finite(localXOverRadius);CleftNumbers.Finite(localYOverRadius);
        double radiusSquared=localXOverRadius*localXOverRadius+localYOverRadius*localYOverRadius;
        if(!double.IsFinite(radiusSquared)||radiusSquared>1)throw new ArgumentException("The sample must lie inside the declared cupula; no clamping.");
        double mean=solidVolumeCubicMeters/AreaSquareMeters,meanVelocity=solidFlowCubicMetersPerSecond/AreaSquareMeters;
        double shape=2*(1-radiusSquared),xGradient=-4*mean/RadiusMeters*localXOverRadius,yGradient=-4*mean/RadiusMeters*localYOverRadius;
        double pressure=StiffnessPascalPerCubicMeter*solidVolumeCubicMeters;
        var sample=new AvatarCupulaFieldSample(Fingerprint,mean,shape*mean,shape*meanVelocity,xGradient,yGradient,
            pressure,.5*pressure*solidVolumeCubicMeters);
        foreach(double v in new[]{sample.MeanDisplacementMeters,sample.DisplacementMeters,sample.VelocityMetersPerSecond,
            sample.ShearGradientX,sample.ShearGradientY,sample.ElasticPressurePascal,sample.ElasticEnergyJoules})CleftNumbers.Finite(v);
        return sample;
    }

    public AvatarCupulaBundleSample Project(AvatarCanalMechanicalPoint mechanics,AvatarCupulaBundleAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(mechanics);ArgumentNullException.ThrowIfNull(attachment);
        mechanics.VolumeMeters3.Validate();mechanics.FlowMeters3PerSecond.Validate();attachment.Validate();
        if(attachment.OrganId!=OrganId||attachment.Canal!=Canal)throw new ArgumentException("Attachment belongs to another declared organ/canal.");
        double x=attachment.RootXOverRadius,depth=Math.Sqrt((1-x)*(1+x)),height=attachment.BundleLengthMeters/RadiusMeters;
        if(!double.IsFinite(height)||height<=0||height>2*depth)throw new ArgumentException("Bundle height must remain representable and its tip inside the declared cupula; no shortening or clipping.");
        double Select(AvatarCanalVector v)=>Canal switch {
            AvatarCupulaCanal.Horizontal=>v.CupulaHC,AvatarCupulaCanal.Anterior=>v.CupulaAC,
            AvatarCupulaCanal.Posterior=>v.CupulaPC,_=>throw new InvalidOperationException()};
        double q=Select(mechanics.VolumeMeters3),flow=Select(mechanics.FlowMeters3PerSecond);
        // Evaluate the normalized tip shape directly from attachment height.
        // This avoids subtracting almost equal squared radii near the fixed root.
        double shape=2*height*(2*depth-height),mean=q/AreaSquareMeters;
        double displacement=attachment.ExcitatoryPolarity*shape*mean*1e9;
        double velocity=attachment.ExcitatoryPolarity*shape*(flow/AreaSquareMeters)*1e9;
        double rootShear=4*mean/RadiusMeters*depth;
        foreach(double value in new[]{height,shape,mean,displacement,velocity,rootShear})CleftNumbers.Finite(value);
        return new(Fingerprint,attachment.Fingerprint,attachment.HairCellId,OrganId,Canal,q,flow,
            x,-depth,-depth+height,attachment.BundleLengthMeters,attachment.ExcitatoryPolarity,
            mean,shape,rootShear,displacement,velocity);
    }

    internal static void Named(string value)
    { if(string.IsNullOrWhiteSpace(value)||value!=value.Trim())throw new ArgumentException("Explicit trimmed provenance/identity required."); }
}

public sealed record AvatarCupulaFieldSample(string GeometryFingerprint,double MeanDisplacementMeters,
    double DisplacementMeters,double VelocityMetersPerSecond,double ShearGradientX,double ShearGradientY,
    double ElasticPressurePascal,double ElasticEnergyJoules);

// Geometry and polarity are supplied by the caller; no human default, automatic
// left/right mirroring, neural gain or cell-position assignment is hidden here.
public sealed record AvatarCupulaBundleAttachment(Guid HairCellId,string OrganId,AvatarCupulaCanal Canal,
    string PlacementSource,double RootXOverRadius,double BundleLengthMeters,int ExcitatoryPolarity)
{
    public void Validate()
    {
        AvatarCupulaShearField.Named(OrganId);AvatarCupulaShearField.Named(PlacementSource);
        if(HairCellId==Guid.Empty||!Enum.IsDefined(Canal)||!double.IsFinite(RootXOverRadius)||Math.Abs(RootXOverRadius)>=1||
            !double.IsFinite(BundleLengthMeters)||BundleLengthMeters<=0||ExcitatoryPolarity is not (1 or -1))
            throw new ArgumentException("Declare an identified finite bundle on the lower circular boundary and its excitatory direction.");
    }
    public string Fingerprint { get {Validate();return CleftNumbers.Fingerprint(new {AvatarCupulaShearField.Version,HairCellId,OrganId,Canal,PlacementSource,RootXOverRadius,BundleLengthMeters,ExcitatoryPolarity});} }
}

public sealed record AvatarCupulaBundleSample(string GeometryFingerprint,string AttachmentFingerprint,Guid HairCellId,
    string OrganId,AvatarCupulaCanal Canal,double SolidVolumeCubicMeters,double SolidFlowCubicMetersPerSecond,
    double RootXOverRadius,double RootYOverRadius,double TipYOverRadius,double BundleLengthMeters,int ExcitatoryPolarity,
    double MeanCupulaDisplacementMeters,double TipShapeFactor,double RootShearGradientY,
    double BundleDisplacementNanometers,double BundleVelocityNanometersPerSecond);
