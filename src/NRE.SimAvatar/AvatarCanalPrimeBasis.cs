using System.Globalization;
using System.Text.Json;

namespace NRE.SimAvatar;

public sealed record AvatarCanalBasisProjection(AvatarSkullVector SourceAccelerationRadiansPerSecondSquared,
    AvatarCanalPrimeAcceleration Components, AvatarSkullVector ReconstructedSourceAcceleration,
    double MaximumBackwardError);

// P20 of Ifediba 2007 defines alpha = sum(alpha'_i * n'_i). These
// nonorthogonal basis coordinates require a solve, not three dot products.
// The printed PC vector is not unit length. Keep that source limitation visible:
// silently normalizing it would change the meaning of Eq. 26's coefficients.
public sealed class AvatarCanalPrimeBasis
{
    public const string Version="printed-prime-coordinate-reconstruction-v1";
    public AvatarSkullVector HC { get; }
    public AvatarSkullVector AC { get; }
    public AvatarSkullVector PC { get; }
    public string Source { get; }
    public string SourceSha256 { get; }
    public string Fingerprint { get; }
    private readonly CanalLinearAlgebra.Factor _factor;

    public AvatarCanalPrimeBasis(string source,string sourceSha256,AvatarSkullVector hc,AvatarSkullVector ac,AvatarSkullVector pc)
    {
        if(string.IsNullOrWhiteSpace(source)||source!=source.Trim()||sourceSha256 is null||sourceSha256.Length!=64||
            sourceSha256.Any(c=>!Uri.IsHexDigit(c)))throw new ArgumentException("Explicit source identity and SHA-256 required.");
        foreach(var v in new[]{hc,ac,pc})if(v is null||!v.Finite)throw new ArgumentException("Three finite source directions required.");
        Source=source;SourceSha256=sourceSha256.ToUpperInvariant();HC=hc;AC=ac;PC=pc;
        _factor=new(new double[,] {{hc.X,ac.X,pc.X},{hc.Y,ac.Y,pc.Y},{hc.Z,ac.Z,pc.Z}});
        Fingerprint=CleftNumbers.Fingerprint(new {Version,Source,SourceSha256,HC,AC,PC,
            Rule="alpha_source = HC*componentHC + AC*componentAC + PC*componentPC; no normalization",
            SourceAxes="posterior, right lateral, superior",AnatomicalCalibrationQualified=false});
    }

    public AvatarCanalBasisProjection Resolve(AvatarSkullVector sourceAcceleration)
    {
        ArgumentNullException.ThrowIfNull(sourceAcceleration);
        if(!sourceAcceleration.Finite)throw new ArgumentException("Finite source acceleration required.");
        var result=_factor.Solve([sourceAcceleration.X,sourceAcceleration.Y,sourceAcceleration.Z]);
        var c=new AvatarCanalPrimeAcceleration(result.Values[0],result.Values[1],result.Values[2]);
        var reconstructed=Reconstruct(c);
        return new(sourceAcceleration,c,reconstructed,result.MaximumBackwardError);
    }

    public AvatarSkullVector Reconstruct(AvatarCanalPrimeAcceleration components)
    {
        var c=components.Values();
        var value=new AvatarSkullVector(HC.X*c[0]+AC.X*c[1]+PC.X*c[2],
            HC.Y*c[0]+AC.Y*c[1]+PC.Y*c[2],HC.Z*c[0]+AC.Z*c[1]+PC.Z*c[2]);
        if(!value.Finite)throw new ArithmeticException("Nonfinite source reconstruction.");return value;
    }

    // Polar coordinates: posterior=-forward, right=right, superior=up.
    // det(S)=-1, so angular quantities transform as det(S)*S*omega.
    // This preserves S(omega cross r) = (det(S)*S*omega) cross (S*r).
    public static AvatarSkullVector PolarSkullToSource(AvatarSkullVector value)
    {
        ArgumentNullException.ThrowIfNull(value);if(!value.Finite)throw new ArgumentException("Finite skull vector required.");
        return new(-value.Z,value.X,value.Y);
    }
    public static AvatarSkullVector AxialSkullToSource(AvatarSkullVector value)
    {
        ArgumentNullException.ThrowIfNull(value);if(!value.Finite)throw new ArgumentException("Finite skull axial vector required.");
        return new(value.Z,-value.X,-value.Y);
    }

    public static AvatarCanalPrimeBasis PublishedPrintedRightLabyrinth1()
    {
        using var stream=typeof(AvatarCanalPrimeBasis).Assembly.GetManifestResourceStream("NRE.SimAvatar.Ifediba2007.json")
            ??throw new InvalidOperationException("Missing pinned canal source.");
        using var document=JsonDocument.Parse(stream);var root=document.RootElement;
        if(root.GetProperty("version").GetString()!="ifediba-2007-published-matrix-transcription-v1")
            throw new InvalidOperationException("Unexpected canal source catalog.");
        var directions=root.GetProperty("primeDirections");
        if(!directions.GetProperty("sourceFrame").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"posterior","right lateral","superior"}))
            throw new InvalidOperationException("Unexpected source frame.");
        var vectors=directions.GetProperty("vectors").EnumerateArray().Select(row=>
        {
            var x=row.EnumerateArray().Select(v=>double.Parse(v.GetString()!,NumberStyles.Float,CultureInfo.InvariantCulture)).ToArray();
            if(x.Length!=3)throw new InvalidOperationException("Three source coordinates required.");return new AvatarSkullVector(x[0],x[1],x[2]);
        }).ToArray();
        if(vectors.Length!=3)throw new InvalidOperationException("Three printed directions required.");
        return new("Ifediba 2007 P20/P45; printed right labyrinth 1 basis, including nonunit PC literal",
            root.GetProperty("retainedHtmlSha256").GetString()!,vectors[0],vectors[1],vectors[2]);
    }
}
