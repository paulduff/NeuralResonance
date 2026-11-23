using System.Security.Cryptography;
using System.Text.Json;

namespace NRE.SimAvatar;

// A declared anatomical basis in ONE coordinate system. Right/left handedness
// is explicit: source anatomical coordinates and native coordinates need not
// share it. These labels alone do not prove anatomical calibration.
internal sealed class MuscleAnatomicalFrame
{
    public string BodyId { get; }
    public MusclePathVector OriginMeters { get; }
    public MusclePathVector Anterior { get; }
    public MusclePathVector Superior { get; }
    public MusclePathVector Right { get; }
    public int Handedness { get; }
    private const double BasisTolerance = 1e-10;

    public MuscleAnatomicalFrame(string bodyId, MusclePathVector originMeters,
        MusclePathVector anterior, MusclePathVector superior, MusclePathVector right)
    {
        if (!AvatarMusclePath.ValidKey(bodyId) || !originMeters.IsFinite ||
            !anterior.IsFinite || !superior.IsFinite || !right.IsFinite)
            throw new ArgumentException("Declare a named body, finite origin and finite anatomical basis.");
        if (new[] { anterior, superior, right }.Any(v => Math.Abs(v.Magnitude-1) > BasisTolerance) ||
            Math.Abs(MusclePathVector.Dot(anterior, superior)) > BasisTolerance ||
            Math.Abs(MusclePathVector.Dot(anterior, right)) > BasisTolerance ||
            Math.Abs(MusclePathVector.Dot(superior, right)) > BasisTolerance)
            throw new ArgumentException("An anatomical basis must be orthonormal; no implicit scale or shear.");
        double determinant = MusclePathVector.Dot(Cross(anterior, superior), right);
        if (Math.Abs(Math.Abs(determinant)-1) > BasisTolerance)
            throw new ArgumentException("Invalid anatomical basis handedness.");
        BodyId = bodyId; OriginMeters = originMeters; Anterior = anterior; Superior = superior; Right = right;
        Handedness = determinant > 0 ? 1 : -1;
    }

    // Both points belong to this body frame. The anterior hint declares the
    // remaining orientation; segment length alone cannot fix rotation about it.
    public static MuscleAnatomicalFrame FromProximalDistal(string bodyId,
        MusclePathVector proximalMeters, MusclePathVector distalMeters,
        MusclePathVector anteriorHint, int handedness)
    {
        if (!proximalMeters.IsFinite || !distalMeters.IsFinite || !anteriorHint.IsFinite || handedness is not (1 or -1))
            throw new ArgumentException("Finite landmarks, an anterior hint and explicit handedness required.");
        var superior = Unit(proximalMeters-distalMeters);
        var hint = Unit(anteriorHint);
        var transverse = hint-superior*MusclePathVector.Dot(hint, superior);
        // Reject a poorly determined roll angle rather than amplifying roundoff.
        if (transverse.Magnitude <= 1e-8)
            throw new ArgumentException("Anterior hint is parallel or ill-conditioned relative to the segment.");
        var anterior = Unit(transverse);
        var right = Cross(anterior, superior)*handedness;
        return new(bodyId, proximalMeters, anterior, superior, right);
    }

    internal static MusclePathVector Cross(MusclePathVector a, MusclePathVector b) =>
        new(a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X);

    private static MusclePathVector Unit(MusclePathVector value)
    {
        double length = value.Magnitude;
        if (!double.IsFinite(length) || length <= 0 || !double.IsFinite(1/length))
            throw new ArgumentException("Landmarks/hint cannot define a finite nondegenerate basis.");
        return value*(1/length);
    }
}

// Maps reference attachment points into a declared body's LOCAL material frame.
// It never fits/changes body geometry, edits muscle lengths, applies forces, or
// chooses a live model. Source identity, native geometry and scaling rationale
// are part of the immutable mapping fingerprint.
internal sealed class AvatarMuscleFrameMap
{
    public const string Version = "declared-anatomical-similarity-map-v1";
    public MuscleAnatomicalFrame Source { get; }
    public MuscleAnatomicalFrame Target { get; }
    public double Scale { get; }
    public string SourceDefinitionSha256 { get; }
    public string TargetGeometrySha256 { get; }
    public string ScaleProvenance { get; }
    public int OrientationParity => Source.Handedness*Target.Handedness;
    public string Fingerprint { get; }

    public AvatarMuscleFrameMap(MuscleAnatomicalFrame source, MuscleAnatomicalFrame target,
        double scale, string sourceDefinitionSha256, string targetGeometrySha256, string scaleProvenance)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(target);
        if (!double.IsFinite(scale) || scale <= 0 || !double.IsFinite(1/scale) || 1/scale <= 0 ||
            !Hash(sourceDefinitionSha256) || !Hash(targetGeometrySha256) || !AvatarMusclePath.ValidKey(scaleProvenance))
            throw new ArgumentException("Declare positive invertible scale, both original SHA256 identities and scaling provenance.");
        Source = source; Target = target; Scale = scale; SourceDefinitionSha256 = sourceDefinitionSha256;
        TargetGeometrySha256 = targetGeometrySha256; ScaleProvenance = scaleProvenance;
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { Version, Source, Target, Scale, SourceDefinitionSha256, TargetGeometrySha256, ScaleProvenance })));
    }

    public MusclePathVector MapPoint(MusclePathVector sourcePointMeters) =>
        Checked(Target.OriginMeters+MapDisplacement(sourcePointMeters-Source.OriginMeters));

    public MusclePathVector MapDisplacement(MusclePathVector displacement) => Checked(MapDirection(displacement)*Scale);

    public MusclePathVector MapDirection(MusclePathVector direction)
    {
        if (!direction.IsFinite) throw new ArgumentException("Finite source vector required.");
        return Checked(Target.Anterior*MusclePathVector.Dot(direction, Source.Anterior) +
                       Target.Superior*MusclePathVector.Dot(direction, Source.Superior) +
                       Target.Right*MusclePathVector.Dot(direction, Source.Right));
    }

    // Angular axes and cross products are axial vectors, unlike a displacement.
    // Reflection therefore also changes their sign. No length/force units are
    // inferred or rescaled by this directional operation.
    public MusclePathVector MapAxialDirection(MusclePathVector direction) => Checked(MapDirection(direction)*OrientationParity);

    public MusclePathAnchor BindPoint(string attachmentId, string sourceBodyId, MusclePathVector positionMeters)
    {
        if (sourceBodyId != Source.BodyId) throw new ArgumentException("Attachment belongs to another source frame.");
        return new(attachmentId, Target.BodyId, MapPoint(positionMeters));
    }

    private static bool Hash(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static MusclePathVector Checked(MusclePathVector value) => value.IsFinite ? value :
        throw new ArithmeticException("Anatomical coordinate mapping overflowed; no clipping.");
}
