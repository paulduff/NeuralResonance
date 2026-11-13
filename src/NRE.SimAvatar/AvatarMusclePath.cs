using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NRE.SimAvatar
{
    // Double-precision world geometry. The native adapter must declare the
    // precision of its own measurements; converting float to double adds none.
    internal readonly struct MusclePathVector
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public MusclePathVector(double x, double y, double z) { X = x; Y = y; Z = z; }
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static MusclePathVector operator +(MusclePathVector a, MusclePathVector b) => new MusclePathVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static MusclePathVector operator -(MusclePathVector a, MusclePathVector b) => new MusclePathVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static MusclePathVector operator *(MusclePathVector a, double value) => new MusclePathVector(a.X * value, a.Y * value, a.Z * value);
        public static double Dot(MusclePathVector a, MusclePathVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public double Magnitude
        {
            get
            {
                double scale = Math.Max(Math.Abs(X), Math.Max(Math.Abs(Y), Math.Abs(Z)));
                if (scale == 0) return 0;
                double x = X / scale, y = Y / scale, z = Z / scale;
                return scale * Math.Sqrt(x * x + y * y + z * z);
            }
        }
    }

    internal sealed class MusclePathAnchor
    {
        public string AttachmentId { get; }
        public string BodyId { get; }
        public MusclePathVector LocalPositionMeters { get; }
        public MusclePathAnchor(string attachmentId, string bodyId, MusclePathVector localPositionMeters)
        {
            if (!AvatarMusclePath.ValidKey(attachmentId) || !AvatarMusclePath.ValidKey(bodyId) || !localPositionMeters.IsFinite)
                throw new ArgumentException("Declare stable attachment/body identities and finite local coordinates in metres.");
            AttachmentId = attachmentId; BodyId = bodyId; LocalPositionMeters = localPositionMeters;
        }
    }

    // The physical owner transforms each declared local anchor and measures its
    // world point velocity at ONE completed endpoint. This type does not claim
    // to authenticate native poses, a clock, or anatomical calibration.
    internal sealed class MusclePathPoint
    {
        public string AttachmentId { get; }
        public string BodyId { get; }
        public MusclePathVector PositionMeters { get; }
        public MusclePathVector VelocityMetersPerSecond { get; }
        public MusclePathPoint(string attachmentId, string bodyId, MusclePathVector positionMeters, MusclePathVector velocityMetersPerSecond)
        {
            AttachmentId = attachmentId; BodyId = bodyId; PositionMeters = positionMeters; VelocityMetersPerSecond = velocityMetersPerSecond;
        }
    }

    internal sealed class MusclePathPointForce
    {
        public MusclePathPoint Point { get; }
        public MusclePathVector ForceNewtons { get; }
        internal MusclePathPointForce(MusclePathPoint point, MusclePathVector force) { Point = point; ForceNewtons = force; }
    }

    internal sealed class MusclePathTension
    {
        public string PathFingerprint { get; }
        public double AxialTensionNewtons { get; }
        public IReadOnlyList<MusclePathPointForce> PointForces { get; }
        public double PowerOnBodiesWatts { get; }
        public double PowerFromPathShorteningWatts { get; }
        public double PowerResidualWatts => PowerOnBodiesWatts - PowerFromPathShorteningWatts;
        internal MusclePathTension(string fingerprint, double tension, MusclePathPointForce[] forces, double power, double expectedPower)
        {
            PathFingerprint = fingerprint; AxialTensionNewtons = tension; PointForces = Array.AsReadOnly(forces);
            PowerOnBodiesWatts = power; PowerFromPathShorteningWatts = expectedPower;
        }
    }

    internal sealed class MusclePathSnapshot
    {
        private readonly MusclePathVector[] directions;
        public string MuscleId { get; }
        public string PathFingerprint { get; }
        public IReadOnlyList<MusclePathPoint> Points { get; }
        public double MusculotendonLengthMeters { get; }
        public double MusculotendonVelocityMetersPerSecond { get; }
        internal MusclePathSnapshot(string muscleId, string fingerprint, MusclePathPoint[] points,
            MusclePathVector[] segmentDirections, double length, double velocity)
        {
            MuscleId = muscleId; PathFingerprint = fingerprint; Points = Array.AsReadOnly(points);
            directions = segmentDirections; MusculotendonLengthMeters = length; MusculotendonVelocityMetersPerSecond = velocity;
        }
        public MusclePathTension ApplyTension(double axialTensionNewtons)
        {
            if (!MusclePathVector.Finite(axialTensionNewtons) || axialTensionNewtons < 0)
                throw new ArgumentException("Uniform axial tension must be finite and nonnegative; no force clipping.");
            var forces = new MusclePathVector[Points.Count];
            for (int i = 0; i < directions.Length; ++i)
            {
                var segmentForce = directions[i] * axialTensionNewtons;
                forces[i] = forces[i] + segmentForce;
                forces[i + 1] = forces[i + 1] - segmentForce;
            }
            var output = new MusclePathPointForce[Points.Count]; double power = 0;
            for (int i = 0; i < Points.Count; ++i)
            {
                if (!forces[i].IsFinite) throw new InvalidOperationException("Nonfinite attachment force.");
                output[i] = new MusclePathPointForce(Points[i], forces[i]);
                power += MusclePathVector.Dot(forces[i], Points[i].VelocityMetersPerSecond);
            }
            double expected = -axialTensionNewtons * MusculotendonVelocityMetersPerSecond;
            if (!MusclePathVector.Finite(power) || !MusclePathVector.Finite(expected) || !MusclePathVector.Finite(power - expected))
                throw new InvalidOperationException("Nonfinite muscle/body power accounting.");
            return new MusclePathTension(PathFingerprint, axialTensionNewtons, output, power, expected);
        }
    }

    // Fixed attachment/via-point polyline, with uniform tension. Each segment
    // pulls both endpoints; a via point receives BOTH adjacent reactions. There
    // is no independent moment-arm table or hidden torque at the body boundary.
    // Surface wrapping, tendon compliance and muscle dynamics belong elsewhere.
    // This geometry alone does not establish calibrated human attachment sites.
    internal sealed class AvatarMusclePath
    {
        public const string Version = "fixed-anchor-musculotendon-polyline-v1";
        public string MuscleId { get; }
        public string BodyGeometryFingerprint { get; }
        public string Provenance { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<MusclePathAnchor> Anchors { get; }
        public AvatarMusclePath(string muscleId, string bodyGeometryFingerprint, string provenance, IEnumerable<MusclePathAnchor> anchors)
        {
            if (!ValidKey(muscleId) || !ValidKey(bodyGeometryFingerprint) || !ValidKey(provenance) || anchors == null)
                throw new ArgumentException("Declare muscle/body geometry identities and attachment provenance.");
            var copied = anchors.ToArray();
            if (copied.Length < 2 || copied.Any(a => a == null) || copied.Select(a => a.AttachmentId).Distinct(StringComparer.Ordinal).Count() != copied.Length)
                throw new ArgumentException("Declare at least two distinct ordered attachments.");
            MuscleId = muscleId; BodyGeometryFingerprint = bodyGeometryFingerprint; Provenance = provenance;
            Anchors = Array.AsReadOnly(copied);
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write(Version); writer.Write(muscleId); writer.Write(bodyGeometryFingerprint); writer.Write(provenance); writer.Write(copied.Length);
                    foreach (var anchor in copied)
                    {
                        writer.Write(anchor.AttachmentId); writer.Write(anchor.BodyId);
                        // Numerically identical signed zeros have one identity.
                        writer.Write(CanonicalZero(anchor.LocalPositionMeters.X)); writer.Write(CanonicalZero(anchor.LocalPositionMeters.Y));
                        writer.Write(CanonicalZero(anchor.LocalPositionMeters.Z));
                    }
                }
                using (var hash = SHA256.Create()) Fingerprint = BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
            }
        }
        internal static bool ValidKey(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
        private static double CanonicalZero(double value) => value == 0 ? 0 : value;

        public MusclePathSnapshot Measure(string bodyGeometryFingerprint, IEnumerable<MusclePathPoint> observations)
        {
            if (bodyGeometryFingerprint != BodyGeometryFingerprint || observations == null)
                throw new ArgumentException("Path measurements must belong to the declared body geometry.");
            var input = observations.ToArray();
            if (input.Length != Anchors.Count || input.Any(p => p == null || !ValidKey(p.AttachmentId) || !ValidKey(p.BodyId) ||
                    !p.PositionMeters.IsFinite || !p.VelocityMetersPerSecond.IsFinite) ||
                input.Select(p => p.AttachmentId).Distinct(StringComparer.Ordinal).Count() != input.Length)
                throw new ArgumentException("Require every distinct attachment's finite position and point velocity.");
            var byId = input.ToDictionary(p => p.AttachmentId, StringComparer.Ordinal);
            var points = new MusclePathPoint[Anchors.Count];
            for (int i = 0; i < points.Length; ++i)
            {
                MusclePathPoint point;
                if (!byId.TryGetValue(Anchors[i].AttachmentId, out point!) || point.BodyId != Anchors[i].BodyId)
                    throw new ArgumentException("Attachment/body identity differs from the declared path.");
                points[i] = point;
            }
            var directions = new MusclePathVector[points.Length - 1]; double length = 0, speed = 0;
            for (int i = 0; i < directions.Length; ++i)
            {
                var delta = points[i + 1].PositionMeters - points[i].PositionMeters;
                var velocity = points[i + 1].VelocityMetersPerSecond - points[i].VelocityMetersPerSecond;
                double segmentLength = delta.Magnitude;
                if (!delta.IsFinite || !velocity.IsFinite || !MusclePathVector.Finite(segmentLength) || segmentLength <= 0)
                    throw new InvalidOperationException("Collapsed or nonfinite muscle path segment; no skipped attachment or geometry reset.");
                // Normalize at unit scale. A subnormal length can round to a
                // single quantum; dividing by that rounded length would make
                // tension direction non-unit even though its direction is known.
                double scale = Math.Max(Math.Abs(delta.X), Math.Max(Math.Abs(delta.Y), Math.Abs(delta.Z)));
                var scaled = new MusclePathVector(delta.X / scale, delta.Y / scale, delta.Z / scale);
                double norm = scaled.Magnitude;
                var unit = new MusclePathVector(scaled.X / norm, scaled.Y / norm, scaled.Z / norm);
                directions[i] = unit; length += segmentLength; speed += MusclePathVector.Dot(unit, velocity);
            }
            if (!MusclePathVector.Finite(length) || !MusclePathVector.Finite(speed))
                throw new InvalidOperationException("Nonfinite path length/velocity.");
            return new MusclePathSnapshot(MuscleId, Fingerprint, points, directions, length, speed);
        }
    }
}
