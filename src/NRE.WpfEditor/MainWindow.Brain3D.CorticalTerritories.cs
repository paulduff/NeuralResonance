using System.Windows;
using System.Windows.Media.Media3D;

namespace NRE.WpfEditor;

// Anatomical territories for implemented cortical circuits. Functional records
// occupy these fitted parcels; the neutral reference mantle represents cortex
// that the runtime does not yet model explicitly.
public partial class MainWindow
{
    private enum CorticalTerritoryShape
    {
        RoundedParcel,
        VerticalStrip,
        HorizontalStrip,
        Crescent,
        Triangular,
        MedialBand,
        VentralBand,
        OccipitalBelt,
        TwinLobule
    }

    private sealed record CorticalTerritoryProfile(
        string Name,
        double HalfTheta,
        double HalfPhi,
        double RotationDeg,
        CorticalTerritoryShape Shape,
        double SurfaceOffsetMm,
        double FoldReliefMm,
        double CenterThetaOffset = 0.0,
        double CenterPhiOffset = 0.0);

    private static CorticalTerritoryProfile GetCorticalTerritoryProfile(string snapshotId)
    {
        return snapshotId switch
        {
            // Frontal lobe and peri-Rolandic cortex.
            "Pfc" => new("Dorsolateral prefrontal cortex", 0.34, 0.27, -7, CorticalTerritoryShape.RoundedParcel, 1.15, 0.90, 0.03, 0.04),
            "DorsomedialPrefrontalCortex" => new("Dorsomedial prefrontal cortex", 0.20, 0.20, -6, CorticalTerritoryShape.MedialBand, -3.40, 0.34),
            "VentromedialPrefrontalCortex" => new("Ventromedial prefrontal cortex", 0.23, 0.14, -4, CorticalTerritoryShape.VentralBand, -3.20, 0.30),
            "FrontalEyeFields" => new("Frontal eye fields", 0.13, 0.18, 4, CorticalTerritoryShape.VerticalStrip, 1.20, 0.56),
            "OrbitofrontalCortex" => new("Orbitofrontal cortex", 0.32, 0.13, -5, CorticalTerritoryShape.VentralBand, 0.75, 0.50, -0.02, -0.03),
            "BrocaBa44Ba45" => new("Inferior frontal gyrus, BA44/45", 0.20, 0.17, -8, CorticalTerritoryShape.Triangular, 1.20, 0.72),
            "PremotorCortex" => new("Lateral premotor cortex", 0.13, 0.35, 3, CorticalTerritoryShape.VerticalStrip, 1.25, 0.72),
            "M1" => new("Precentral gyrus, M1", 0.09, 0.44, 2, CorticalTerritoryShape.VerticalStrip, 1.35, 0.60),
            "S1" => new("Postcentral gyrus, S1", 0.10, 0.44, -2, CorticalTerritoryShape.VerticalStrip, 1.35, 0.58),
            "Sma" => new("Supplementary motor area", 0.17, 0.23, -7, CorticalTerritoryShape.MedialBand, -2.80, 0.38),

            // Parietal lobe.
            "Ppc" => new("Posterior parietal cortex", 0.27, 0.24, 4, CorticalTerritoryShape.RoundedParcel, 1.15, 0.82),
            "SupramarginalAngular" => new("Supramarginal and angular gyri", 0.22, 0.16, -9, CorticalTerritoryShape.TwinLobule, 1.20, 0.78),
            "TemporoparietalJunction" => new("Temporoparietal junction", 0.17, 0.15, -7, CorticalTerritoryShape.Crescent, 1.25, 0.62),
            "Precuneus" => new("Precuneus", 0.28, 0.18, -7, CorticalTerritoryShape.MedialBand, -3.60, 0.32),

            // Occipital visual fields. V1 is chiefly medial; V2 and V4 form
            // progressively more lateral belts around the occipital pole.
            "V1" => new("Calcarine cortex, V1", 0.10, 0.22, -4, CorticalTerritoryShape.MedialBand, -2.60, 0.34),
            "V2" => new("Extrastriate visual belt, V2", 0.14, 0.24, 5, CorticalTerritoryShape.Crescent, 0.75, 0.52, 0.02),
            "V3" => new("Intermediate extrastriate cortex, V3", 0.15, 0.22, 1, CorticalTerritoryShape.Crescent, 0.85, 0.55),
            "V4" => new("Ventral occipital visual cortex, V4", 0.20, 0.16, -11, CorticalTerritoryShape.OccipitalBelt, 0.95, 0.64, 0.02, -0.04),
            "Mt" => new("Lateral occipitotemporal cortex, MT", 0.18, 0.12, -8, CorticalTerritoryShape.HorizontalStrip, 1.05, 0.62),

            // Temporal lobe and language cortex.
            "A1" => new("Heschl and superior temporal cortex, A1", 0.20, 0.08, -5, CorticalTerritoryShape.HorizontalStrip, 0.85, 0.42, 0.03, -0.03),
            "AuditoryAssociationCortex" => new("Auditory association cortex", 0.24, 0.10, -6, CorticalTerritoryShape.HorizontalStrip, 0.95, 0.48),
            "SecondarySomatosensoryCortex" => new("Parietal operculum, S2", 0.18, 0.10, -4, CorticalTerritoryShape.HorizontalStrip, -1.80, 0.34),
            "WernickePstgPsts" => new("Posterior superior temporal language cortex", 0.21, 0.09, -8, CorticalTerritoryShape.HorizontalStrip, 1.10, 0.48),
            "TemporalAssociation" => new("Middle temporal association cortex", 0.31, 0.15, -6, CorticalTerritoryShape.HorizontalStrip, 1.05, 0.75, 0.08, -0.06),
            "InferotemporalCortex" => new("Inferotemporal cortex", 0.29, 0.13, -7, CorticalTerritoryShape.HorizontalStrip, 0.95, 0.58),
            "FusiformGyrus" => new("Fusiform gyrus", 0.27, 0.10, -8, CorticalTerritoryShape.VentralBand, -1.20, 0.42),
            "TemporalPole" => new("Anterior temporal pole", 0.19, 0.17, -3, CorticalTerritoryShape.RoundedParcel, 1.00, 0.54),
            "EntorhinalCortex" => new("Entorhinal cortex", 0.16, 0.08, -9, CorticalTerritoryShape.VentralBand, -3.20, 0.28),
            "ParahippocampalCortex" => new("Parahippocampal gyrus", 0.26, 0.10, -8, CorticalTerritoryShape.VentralBand, -2.20, 0.36),
            "PerirhinalCortex" => new("Perirhinal cortex", 0.19, 0.09, -7, CorticalTerritoryShape.VentralBand, -1.40, 0.34),

            // Buried and medial cortical surfaces.
            "Insula" => new("Insular cortex", 0.20, 0.17, 0, CorticalTerritoryShape.Triangular, -9.50, 0.24, 0.00, -0.04),
            "Acc" => new("Anterior cingulate cortex", 0.26, 0.08, 11, CorticalTerritoryShape.MedialBand, -4.20, 0.30),
            "MidcingulateCortex" => new("Midcingulate cortex", 0.22, 0.08, 3, CorticalTerritoryShape.MedialBand, -4.10, 0.28),
            "PosteriorCingulate" => new("Posterior cingulate cortex", 0.20, 0.09, -8, CorticalTerritoryShape.MedialBand, -4.00, 0.30),
            "RetrosplenialCortex" => new("Retrosplenial cortex", 0.15, 0.07, -5, CorticalTerritoryShape.MedialBand, -4.40, 0.24),

            _ => new("Cortical territory", 0.28, 0.24, 0, CorticalTerritoryShape.RoundedParcel, 1.0, 0.55)
        };
    }

    private static bool TryBuildCorticalTerritoryPoint(
        string snapshotId,
        double along,
        double width,
        double laminaDepth,
        Vector3D jitter,
        double hemisphereSign,
        out Point3D point)
    {
        var localTheta = ((Math.Clamp(along, 0.0, 1.0) - 0.5) * 2.0) + (jitter.X * 0.012);
        var localPhi = ((Math.Clamp(width, 0.0, 1.0) - 0.5) * 2.0) + (jitter.Z * 0.012);
        var profile = GetCorticalTerritoryProfile(snapshotId);
        if (!IsInsideCorticalTerritory(profile.Shape, localTheta, localPhi))
        {
            point = default;
            return false;
        }

        point = BuildCorticalTerritoryPointUnchecked(
            snapshotId,
            localTheta,
            localPhi,
            laminaDepth + (jitter.Y * MmToRender(0.18)),
            hemisphereSign);
        return true;
    }

    private static Point3D BuildCorticalTerritoryPointUnchecked(
        string snapshotId,
        double localTheta,
        double localPhi,
        double laminaDepth,
        double hemisphereSign)
    {
        var profile = GetCorticalTerritoryProfile(snapshotId);
        var warped = WarpCorticalTerritoryCoordinates(profile.Shape, localTheta, localPhi);
        var rotation = DegreesToRadians(profile.RotationDeg);
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        var thetaOffset = ((warped.Theta * cos) - (warped.Phi * sin)) * profile.HalfTheta;
        var phiOffset = ((warped.Theta * sin) + (warped.Phi * cos)) * profile.HalfPhi;

        var anchor = UnrotateCorticalShellFromMidlineAroundZ(
            GetCorticalStructureAnchor(snapshotId, "R"),
            1.0);
        var center = GetCorticalSurfaceParameters(anchor);
        var theta = Math.Clamp(center.Theta + profile.CenterThetaOffset + thetaOffset, -1.50, 1.50);
        var phi = Math.Clamp(center.Phi + profile.CenterPhiOffset + phiOffset, -1.46, 1.46);
        var normalizedTheta = Math.Clamp((theta + 1.52) / 3.04, 0.0, 1.0);
        var normalizedPhi = Math.Clamp((phi + 1.52) / 3.04, 0.0, 1.0);

        var foldedSurface = BuildFoldedCorticalReferencePoint(
            theta,
            phi,
            hemisphereSign,
            normalizedTheta,
            normalizedPhi);
        var smoothSurface = BuildCorticalSurfacePoint(theta, phi, hemisphereSign);
        var normal = GetCorticalShellNormal(smoothSurface, hemisphereSign < 0 ? "L" : "R");
        var edgeDistance = Math.Max(Math.Abs(localTheta), Math.Abs(localPhi));
        var foldEnvelope = Math.Clamp(1.0 - (edgeDistance * edgeDistance), 0.0, 1.0);
        var localFoldMm = profile.FoldReliefMm * foldEnvelope *
                          Math.Sin((theta * 12.2) + (phi * 7.4) + (snapshotId.Length * 0.37));
        var depth = laminaDepth + MmToRender(profile.SurfaceOffsetMm + localFoldMm);

        return new Point3D(
            foldedSurface.X + (normal.X * depth),
            foldedSurface.Y + (normal.Y * depth),
            foldedSurface.Z + (normal.Z * depth));
    }

    private static bool IsInsideCorticalTerritory(CorticalTerritoryShape shape, double theta, double phi)
    {
        static double Superellipse(double x, double y, double exponent) =>
            Math.Pow(Math.Abs(x), exponent) + Math.Pow(Math.Abs(y), exponent);

        if (Math.Abs(theta) > 1.08 || Math.Abs(phi) > 1.08)
        {
            return false;
        }

        return shape switch
        {
            CorticalTerritoryShape.VerticalStrip =>
                Superellipse(theta / (0.78 + (0.10 * (1.0 - Math.Abs(phi)))), phi, 2.8) <= 1.0,
            CorticalTerritoryShape.HorizontalStrip =>
                Superellipse(theta, phi / (0.72 + (0.14 * (1.0 - Math.Abs(theta)))), 2.7) <= 1.0,
            CorticalTerritoryShape.Crescent =>
                Superellipse(theta, phi + (0.23 * ((theta * theta) - 0.35)), 2.45) <= 1.0,
            CorticalTerritoryShape.Triangular =>
                phi is >= -1.0 and <= 1.0 &&
                Math.Abs(theta) <= (0.34 + (0.58 * ((1.0 - phi) * 0.5))),
            CorticalTerritoryShape.MedialBand =>
                Superellipse(theta, phi / 0.70, 2.65) <= 1.0,
            CorticalTerritoryShape.VentralBand =>
                Superellipse(theta, (phi + (0.12 * theta)) / 0.64, 2.75) <= 1.0,
            CorticalTerritoryShape.OccipitalBelt =>
                Superellipse(theta, (phi + (0.18 * theta) + (0.10 * theta * theta)) / 0.78, 2.55) <= 1.0,
            CorticalTerritoryShape.TwinLobule =>
                ((Math.Pow((theta + 0.32) / 0.70, 2) + Math.Pow(phi / 0.88, 2)) <= 1.0) ||
                ((Math.Pow((theta - 0.34) / 0.72, 2) + Math.Pow((phi + 0.05) / 0.84, 2)) <= 1.0),
            _ => Superellipse(theta, phi, 2.45) <= 1.0
        };
    }

    private static (double Theta, double Phi) WarpCorticalTerritoryCoordinates(
        CorticalTerritoryShape shape,
        double theta,
        double phi)
    {
        return shape switch
        {
            CorticalTerritoryShape.Crescent => (theta, phi + (0.23 * ((theta * theta) - 0.35))),
            CorticalTerritoryShape.Triangular => (theta * (0.82 - (0.12 * phi)), phi),
            CorticalTerritoryShape.MedialBand => (theta, phi * 0.72),
            CorticalTerritoryShape.VentralBand => (theta, (phi * 0.66) - (0.12 * theta)),
            CorticalTerritoryShape.OccipitalBelt => (theta, (phi * 0.78) - (0.18 * theta) - (0.10 * theta * theta)),
            CorticalTerritoryShape.TwinLobule => (theta + (0.08 * Math.Sin(phi * Math.PI)), phi),
            _ => (theta, phi)
        };
    }

    private static MeshGeometry3D BuildCorticalTerritorySurfaceMesh(
        string snapshotId,
        double hemisphereSign,
        int columns,
        int rows)
    {
        var mesh = new MeshGeometry3D();
        var angularSegments = Math.Max(24, columns);
        var radialRings = Math.Max(5, rows);
        var profile = GetCorticalTerritoryProfile(snapshotId);
        var hemisphere = hemisphereSign < 0 ? "L" : "R";

        for (var ring = 0; ring <= radialRings; ring++)
        {
            var radialFraction = ring / (double)radialRings;
            for (var segment = 0; segment <= angularSegments; segment++)
            {
                var angle = segment * Math.PI * 2.0 / angularSegments;
                var boundaryRadius = FindCorticalTerritoryBoundaryRadius(profile.Shape, angle);
                var localTheta = Math.Cos(angle) * boundaryRadius * radialFraction;
                var localPhi = Math.Sin(angle) * boundaryRadius * radialFraction;
                var point = BuildCorticalTerritoryPointUnchecked(
                    snapshotId,
                    localTheta,
                    localPhi,
                    0.0,
                    hemisphereSign);
                mesh.Positions.Add(point);
                mesh.Normals.Add(GetCorticalShellNormal(point, hemisphere));
                mesh.TextureCoordinates.Add(new Point(
                    0.5 + (localTheta * 0.5),
                    0.5 + (localPhi * 0.5)));
            }
        }

        AddGridTriangles(mesh, angularSegments + 1, radialRings + 1);
        return mesh;
    }

    private static double FindCorticalTerritoryBoundaryRadius(CorticalTerritoryShape shape, double angle)
    {
        var directionTheta = Math.Cos(angle);
        var directionPhi = Math.Sin(angle);
        var low = 0.0;
        var high = 1.55;
        for (var iteration = 0; iteration < 14; iteration++)
        {
            var candidate = (low + high) * 0.5;
            if (IsInsideCorticalTerritory(shape, directionTheta * candidate, directionPhi * candidate))
            {
                low = candidate;
            }
            else
            {
                high = candidate;
            }
        }

        return low;
    }
}
