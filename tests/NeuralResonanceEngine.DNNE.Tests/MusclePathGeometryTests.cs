using System.Security.Cryptography;
using System.Text;
using NRE.SimAvatar;
using U = Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

// Geometric/analytic fixtures, not human attachment calibration or native PhysX.
public sealed class MusclePathGeometryTests
{
    private static MusclePathVector V(double x = 0, double y = 0, double z = 0) => new(x, y, z);
    private static MusclePathPoint P(string id, MusclePathVector p, MusclePathVector v = default, string body = "body") => new(id, body, p, v);
    private static AvatarMusclePath PathFor(params MusclePathPoint[] points) => new("fixture", "geometry-1", "analytic fixture only",
        points.Select(p => new MusclePathAnchor(p.AttachmentId, p.BodyId, p.PositionMeters)));
    private static MusclePathVector Cross(MusclePathVector a, MusclePathVector b) => V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static void Near(double expected, double actual, double tolerance = 2e-13) => Assert.True(double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance, $"Expected {expected:R}, actual {actual:R}, bound {tolerance:R}");
    private static void Vec(MusclePathVector expected, MusclePathVector actual, double bound = 2e-13)
    { Near(expected.X, actual.X, bound); Near(expected.Y, actual.Y, bound); Near(expected.Z, actual.Z, bound); }
    private static void Balanced(MusclePathTension tension)
    {
        var force = V(); var torque = V();
        foreach (var node in tension.PointForces) { force += node.ForceNewtons; torque += Cross(node.Point.PositionMeters, node.ForceNewtons); }
        Vec(V(), force); Vec(V(), torque); Near(0, tension.PowerResidualWatts);
    }
    private static void UnityParity(AvatarMusclePath path, MusclePathPoint[] points, double tension)
    {
        static U.MusclePathVector UV(MusclePathVector p) => new(p.X, p.Y, p.Z);
        var unity = new U.AvatarMusclePath(path.MuscleId, path.BodyGeometryFingerprint, path.Provenance,
            path.Anchors.Select(a => new U.MusclePathAnchor(a.AttachmentId, a.BodyId, UV(a.LocalPositionMeters))));
        Assert.Equal(path.Fingerprint, unity.Fingerprint);
        var a = path.Measure(path.BodyGeometryFingerprint, points);
        var b = unity.Measure(path.BodyGeometryFingerprint, points.Select(p => new U.MusclePathPoint(p.AttachmentId, p.BodyId, UV(p.PositionMeters), UV(p.VelocityMetersPerSecond))));
        Assert.Equal(a.MusculotendonLengthMeters, b.MusculotendonLengthMeters);
        Assert.Equal(a.MusculotendonVelocityMetersPerSecond, b.MusculotendonVelocityMetersPerSecond);
        var af = a.ApplyTension(tension); var bf = b.ApplyTension(tension);
        Assert.Equal(af.PowerOnBodiesWatts, bf.PowerOnBodiesWatts); Assert.Equal(af.PowerFromPathShorteningWatts, bf.PowerFromPathShorteningWatts);
        for (int i = 0; i < af.PointForces.Count; ++i)
        {
            Assert.Equal(af.PointForces[i].Point.AttachmentId, bf.PointForces[i].Point.AttachmentId);
            Assert.Equal(af.PointForces[i].ForceNewtons.X, bf.PointForces[i].ForceNewtons.X);
            Assert.Equal(af.PointForces[i].ForceNewtons.Y, bf.PointForces[i].ForceNewtons.Y);
            Assert.Equal(af.PointForces[i].ForceNewtons.Z, bf.PointForces[i].ForceNewtons.Z);
        }
    }

    [Fact]
    public void StraightPathHasMeasuredLengthSpeedAndEqualOppositeForces()
    {
        var points = new[] { P("a", V()), P("b", V(3, 4), V(.3, .4)) };
        var path = PathFor(points); var measured = path.Measure("geometry-1", points);
        Near(5, measured.MusculotendonLengthMeters); Near(.5, measured.MusculotendonVelocityMetersPerSecond);
        var force = measured.ApplyTension(10);
        Vec(V(6, 8), force.PointForces[0].ForceNewtons); Vec(V(-6, -8), force.PointForces[1].ForceNewtons);
        Near(-5, force.PowerOnBodiesWatts); Balanced(force); UnityParity(path, points, 10);
    }

    [Fact]
    public void ViaPointReceivesBothReactionsAndTransfersItsShareOfPower()
    {
        var points = new[] { P("a", V()), P("via", V(1), V(.2, -.3)), P("b", V(1, 1), V(-.4, .5)) };
        var path = PathFor(points); var measured = path.Measure("geometry-1", points);
        Near(2, measured.MusculotendonLengthMeters); Near(1, measured.MusculotendonVelocityMetersPerSecond);
        var forces = measured.ApplyTension(2);
        Vec(V(2), forces.PointForces[0].ForceNewtons); Vec(V(-2, 2), forces.PointForces[1].ForceNewtons);
        Vec(V(0, -2), forces.PointForces[2].ForceNewtons); Near(-2, forces.PowerOnBodiesWatts);
        Balanced(forces); UnityParity(path, points, 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RigidTranslationOrRotationProducesNoLengthChangeOrPower(bool rotating)
    {
        var positions = new[] { V(-.2, .4, .1), V(.5, .2, -.3), V(.7, .9, .8) };
        var points = positions.Select((p, i) => P(i.ToString(), p, V(.3, -.7, .9) + (rotating ? Cross(V(.2, .5, -.4), p) : V()))).ToArray();
        var path = PathFor(points); var measured = path.Measure("geometry-1", points);
        Near(0, measured.MusculotendonVelocityMetersPerSecond);
        var force = measured.ApplyTension(3); Near(0, force.PowerOnBodiesWatts); Balanced(force); UnityParity(path, points, 3);
    }

    [Theory]
    [InlineData(-1.2)]
    [InlineData(-.3)]
    [InlineData(.4)]
    [InlineData(1.7)]
    public void HingeMomentArmAndVelocityMatchAnalyticLengthDerivative(double q)
    {
        const double a = .3, b = .2, omega = .7, tension = 17;
        double length = Math.Sqrt(a * a + b * b - 2 * a * b * Math.Cos(q));
        double derivative = a * b * Math.Sin(q) / length;
        var points = new[] { P("parent", V(a)), P("child", V(b * Math.Cos(q), b * Math.Sin(q)), V(-omega * b * Math.Sin(q), omega * b * Math.Cos(q)), "child-body") };
        var path = PathFor(points); var measured = path.Measure("geometry-1", points); var force = measured.ApplyTension(tension);
        Near(length, measured.MusculotendonLengthMeters); Near(derivative * omega, measured.MusculotendonVelocityMetersPerSecond);
        Near(-tension * derivative, Cross(points[1].PositionMeters, force.PointForces[1].ForceNewtons).Z);
        // Independent central length difference, with an explicit finite-difference bound.
        double L(double angle) => Math.Sqrt(a * a + b * b - 2 * a * b * Math.Cos(angle));
        Near(derivative, (L(q + 1e-5) - L(q - 1e-5)) / 2e-5, 2e-10);
        Balanced(force); UnityParity(path, points, tension);
    }

    [Fact]
    public void CoordinateFrameChangeRotatesForcesWithoutChangingLengthOrPower()
    {
        static MusclePathVector Rotate(MusclePathVector p) => V(-p.Y, p.Z, -p.X); // Proper orthogonal rotation.
        var points = new[] { P("a", V(.1, -.2, .3), V(.7, -.4, .2)), P("v", V(.4, .5, -.2), V(.2, .1, -.3)), P("b", V(-.1, .9, .8), V(-.1, -.3, .6)) };
        var transformed = points.Select(p => P(p.AttachmentId, Rotate(p.PositionMeters) + V(2, -3, 4), Rotate(p.VelocityMetersPerSecond))).ToArray();
        var path = PathFor(points); var a = path.Measure("geometry-1", points); var b = path.Measure("geometry-1", transformed);
        Near(a.MusculotendonLengthMeters, b.MusculotendonLengthMeters); Near(a.MusculotendonVelocityMetersPerSecond, b.MusculotendonVelocityMetersPerSecond);
        var af = a.ApplyTension(8); var bf = b.ApplyTension(8); Near(af.PowerOnBodiesWatts, bf.PowerOnBodiesWatts);
        for (int i = 0; i < points.Length; ++i) Vec(Rotate(af.PointForces[i].ForceNewtons), bf.PointForces[i].ForceNewtons);
        Balanced(bf); UnityParity(path, transformed, 8);
    }

    [Fact]
    public void ReversingPathPreservesPhysicalForcesAtEveryAttachment()
    {
        var points = new[] { P("a", V(), V(.2)), P("v", V(1, .4), V(0, .1)), P("b", V(.3, 1), V(.3, .7)) };
        var path = PathFor(points); var reverse = PathFor(points.Reverse().ToArray());
        Assert.NotEqual(path.Fingerprint, reverse.Fingerprint);
        var a = path.Measure("geometry-1", points); var b = reverse.Measure("geometry-1", points);
        Near(a.MusculotendonLengthMeters, b.MusculotendonLengthMeters); Near(a.MusculotendonVelocityMetersPerSecond, b.MusculotendonVelocityMetersPerSecond);
        var af = a.ApplyTension(9); var bf = b.ApplyTension(9);
        foreach (var f in af.PointForces) Vec(f.ForceNewtons, bf.PointForces.Single(g => g.Point.AttachmentId == f.Point.AttachmentId).ForceNewtons);
    }

    [Fact]
    public void DeclarationAndSnapshotsOwnImmutableCopiesAndObservationOrderIsIrrelevant()
    {
        var points = new[] { P("a", V()), P("b", V(1)) };
        var anchors = points.Select(p => new MusclePathAnchor(p.AttachmentId, p.BodyId, p.PositionMeters)).ToArray();
        var path = new AvatarMusclePath("fixture", "geometry-1", "fixture", anchors);
        var snapshot = path.Measure("geometry-1", points.Reverse()); var forces = snapshot.ApplyTension(2);
        anchors[0] = new("changed", "body", V(9)); points[1] = P("b", V(99));
        Assert.Equal("a", path.Anchors[0].AttachmentId); Near(1, snapshot.MusculotendonLengthMeters);
        Vec(V(1), snapshot.Points[1].PositionMeters); Vec(V(-2), forces.PointForces[1].ForceNewtons);
        Assert.Throws<NotSupportedException>(() => ((IList<MusclePathPoint>)snapshot.Points)[0] = points[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<MusclePathAnchor>)path.Anchors)[0] = anchors[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<MusclePathPointForce>)forces.PointForces)[0] = forces.PointForces[1]);
    }

    [Fact]
    public void FingerprintIncludesDeclarationGeometryAndProvenanceWithCanonicalSignedZero()
    {
        string Fingerprint(string muscle = "m", string body = "g", string provenance = "p", string attachment = "a", string link = "b", double x = 0) =>
            new AvatarMusclePath(muscle, body, provenance, new[] { new MusclePathAnchor(attachment, link, V(x)), new MusclePathAnchor("z", "b", V(1)) }).Fingerprint;
        string baseline = Fingerprint(); Assert.Equal(baseline, Fingerprint(x: -0.0));
        var changes = new[] { Fingerprint(muscle: "n"), Fingerprint(body: "h"), Fingerprint(provenance: "q"), Fingerprint(attachment: "c"), Fingerprint(link: "d"), Fingerprint(x: .001) };
        Assert.All(changes, changed => Assert.NotEqual(baseline, changed)); Assert.Equal(changes.Length, changes.Distinct().Count());
    }

    [Fact]
    public void InvalidDeclarationsRejectAbsentIdentityNonfiniteCoordinatesAndDuplicateAnchors()
    {
        Assert.Throws<ArgumentException>(() => new MusclePathAnchor("", "body", V()));
        Assert.Throws<ArgumentException>(() => new MusclePathAnchor("a", " body", V()));
        Assert.Throws<ArgumentException>(() => new MusclePathAnchor("a", "body", V(double.NaN)));
        var a = new MusclePathAnchor("a", "body", V());
        Assert.Throws<ArgumentException>(() => new AvatarMusclePath("m", "g", "", new[] { a, a }));
        Assert.Throws<ArgumentException>(() => new AvatarMusclePath("m", "g", "fixture", new[] { a }));
        Assert.Throws<ArgumentException>(() => new AvatarMusclePath("m", "g", "fixture", new[] { a, a }));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("wrong-body")]
    [InlineData("unknown-id")]
    [InlineData("nonfinite-position")]
    [InlineData("nonfinite-velocity")]
    [InlineData("wrong-geometry")]
    public void InvalidObservationsRejectWithoutChangingThePath(string problem)
    {
        var good = new[] { P("a", V()), P("b", V(1)) }; var path = PathFor(good);
        var bad = (MusclePathPoint[])good.Clone(); string geometry = "geometry-1";
        switch (problem)
        {
            case "missing": bad = new[] { good[0] }; break;
            case "duplicate": bad[1] = good[0]; break;
            case "wrong-body": bad[1] = P("b", V(1), body: "other"); break;
            case "unknown-id": bad[1] = P("c", V(1)); break;
            case "nonfinite-position": bad[1] = P("b", V(double.NaN)); break;
            case "nonfinite-velocity": bad[1] = P("b", V(1), V(double.PositiveInfinity)); break;
            case "wrong-geometry": geometry = "geometry-2"; break;
        }
        Assert.Throws<ArgumentException>(() => path.Measure(geometry, bad));
        Near(1, path.Measure("geometry-1", good).MusculotendonLengthMeters);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollapsedOrUnrepresentableSegmentsReject(bool overflow)
    {
        var points = new[] { P("a", V(overflow ? -double.MaxValue : 0)), P("b", V(overflow ? double.MaxValue : 0)) };
        Assert.Throws<InvalidOperationException>(() => PathFor(points).Measure("geometry-1", points));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidTensionRejectsWithoutChangingMeasuredGeometry(double tension)
    {
        var points = new[] { P("a", V()), P("b", V(1)) }; var measured = PathFor(points).Measure("geometry-1", points);
        Assert.Throws<ArgumentException>(() => measured.ApplyTension(tension));
        Near(1, measured.MusculotendonLengthMeters); Vec(V(2), measured.ApplyTension(2).PointForces[0].ForceNewtons);
    }

    [Theory]
    [InlineData(1e-200)]
    [InlineData(1e200)]
    public void NormAvoidsPrematureSquareUnderflowAndOverflow(double scale)
    {
        var points = new[] { P("a", V()), P("b", V(3 * scale, 4 * scale)) };
        var path = PathFor(points); var measured = path.Measure("geometry-1", points);
        Near(5, measured.MusculotendonLengthMeters / scale); Vec(V(.6, .8), measured.ApplyTension(1).PointForces[0].ForceNewtons);
        UnityParity(path, points, 1);
    }

    [Fact]
    public void SubnormalLengthRoundingDoesNotChangeTensionMagnitude()
    {
        var points = new[] { P("a", V()), P("b", V(double.Epsilon, double.Epsilon)) };
        var path = PathFor(points); var measured = path.Measure("geometry-1", points);
        var force = measured.ApplyTension(1);
        Vec(V(1 / Math.Sqrt(2), 1 / Math.Sqrt(2)), force.PointForces[0].ForceNewtons);
        Near(1, force.PointForces[0].ForceNewtons.Magnitude); UnityParity(path, points, 1);
    }

    [Fact]
    public void ZeroTensionProducesNoAttachmentForceOrPower()
    {
        var points = new[] { P("a", V(), V(1)), P("v", V(1, 2), V(.4)), P("b", V(1), V(-1)) };
        var force = PathFor(points).Measure("geometry-1", points).ApplyTension(0);
        Assert.All(force.PointForces, f => Vec(V(), f.ForceNewtons)); Near(0, force.PowerOnBodiesWatts); Balanced(force);
    }

    [Fact]
    public void UnitySourceIsAnExactNamespaceOnlyGeneratedCopy()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(System.IO.Path.Combine(root.FullName, "NeuralResonanceEngine.DNNE.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        string source = File.ReadAllText(System.IO.Path.Combine(root.FullName, "src/NRE.SimAvatar/AvatarMusclePath.cs")).Replace("\r\n", "\n");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        string expected = "// Generated by tools/prepare-unity-muscle-paths.py; do not edit.\n// Canonical UTF-8/LF SHA256: " + hash + "\n" + source.Replace("namespace NRE.SimAvatar\n", "namespace Nre.UnityWorld\n");
        string actual = File.ReadAllText(System.IO.Path.Combine(root.FullName, "UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs")).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }
}
