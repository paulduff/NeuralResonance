using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NRE.WpfEditor;

// Brain 3D layout, anchor, and atlas math.
// - Neuron-matrix sampling and per-structure layout dispatch (CorticalSheet,
//   HippocampalArc, CerebellarSheet, BrainstemColumn, OlfactoryBulbShell, NucleusBlock)
// - Cortical patch/gyrus profiles, surface points, normals, shell warping
// - Atlas / encephalon coordinate transforms (mm to render units, hemisphere centers,
//   cortical shell registration, and measured deep-structure placement)
// - Structure and pathway definitions used by the renderer
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    // Representative adult cerebral envelope in millimetres. Keeping these
    // dimensions in physical units prevents display-axis changes from silently
    // distorting the anatomy.
    private const double CortexMidlineGapMm = 1.5;
    private const double CortexHalfWidthMm = 68.5;
    private const double CortexHalfHeightMm = 46.5;
    private const double CortexAnteriorRadiusMm = 86.0;
    private const double CortexPosteriorRadiusMm = 81.0;
    private const double CortexVerticalCenterMm = 4.0;

    private IEnumerable<Point3D> GenerateNeuronMatrix(StructureDefinition def, StructureLayout effectiveLayout, int targetCount)
    {
        var baseCount = Math.Max(1, def.GridX * def.GridY * def.GridZ);
        var densityScale = Math.Cbrt(Math.Max(1.0, targetCount / (double)baseCount));
        var gridX = Math.Clamp((int)Math.Round(def.GridX * densityScale), 3, 48);
        var gridY = Math.Clamp((int)Math.Round(def.GridY * densityScale), 3, 36);
        var gridZ = Math.Clamp((int)Math.Round(def.GridZ * densityScale), 3, 36);
        if (effectiveLayout == StructureLayout.CorticalSheet)
        {
            // Dense cortical sampling to read as a continuous hemispheric mantle.
            gridX = Math.Clamp((int)Math.Round(gridX * 1.35), 18, 72);
            gridY = Math.Clamp((int)Math.Round(gridY * 2.20), 14, 44);
            gridZ = Math.Clamp((int)Math.Round(gridZ * 2.10), 14, 44);
        }
        else if (effectiveLayout == StructureLayout.CerebellarSheet)
        {
            // Increase folia fidelity for posterior and lateral cerebellar views.
            gridX = Math.Clamp((int)Math.Round(gridX * 1.20), 18, 64);
            gridY = Math.Clamp((int)Math.Round(gridY * 1.45), 10, 36);
            gridZ = Math.Clamp((int)Math.Round(gridZ * 1.65), 14, 44);
        }

        var points = new List<Point3D>(gridX * gridY * gridZ);
        var xStep = def.RadiusX / Math.Max(1, gridX - 1);
        var yStep = def.RadiusY / Math.Max(1, gridY - 1);
        var zStep = def.RadiusZ / Math.Max(1, gridZ - 1);

        for (var z = 0; z < gridZ; z++)
        {
            for (var y = 0; y < gridY; y++)
            {
                for (var x = 0; x < gridX; x++)
                {
                    var cellJitter = DeterministicJitter(x, y, z, def.SnapshotId);
                    var lx = (x * xStep) - (def.RadiusX * 0.5) + (cellJitter.X * xStep * 0.12);
                    var ly = (y * yStep) - (def.RadiusY * 0.5) + (cellJitter.Y * yStep * 0.12);
                    var lz = (z * zStep) - (def.RadiusZ * 0.5) + (cellJitter.Z * zStep * 0.12);
                    var local = ApplyLayout(effectiveLayout, lx, ly, lz, x, y, z, gridX, gridY, gridZ, def);
                    if (local.HasValue)
                    {
                        var point = local.Value;
                        if (effectiveLayout != StructureLayout.CorticalSheet)
                        {
                            // Atlas dimensions are full physical extents, not loose
                            // display hints. Keep every sampled neuron inside them.
                            point = new Point3D(
                                Math.Clamp(point.X, -def.RadiusX * 0.5, def.RadiusX * 0.5),
                                Math.Clamp(point.Y, -def.RadiusY * 0.5, def.RadiusY * 0.5),
                                Math.Clamp(point.Z, -def.RadiusZ * 0.5, def.RadiusZ * 0.5));
                        }
                        points.Add(point);
                    }
                }
            }
        }

        if (points.Count <= targetCount)
        {
            return points;
        }

        return points
            .Select((p, idx) => new { Point = p, Score = DeterministicPointScore(p, idx, def.SnapshotId) })
            .OrderBy(t => t.Score)
            .Take(targetCount)
            .Select(t => t.Point)
            .ToList();
    }

    private static List<int> SelectSpikeNeuronIndices(IReadOnlyList<Point3D> points, int targetCount, string seed)
    {
        if (points.Count == 0 || targetCount <= 0)
        {
            return [];
        }

        if (points.Count <= targetCount)
        {
            return Enumerable.Range(0, points.Count).ToList();
        }

        return points
            .Select((p, idx) => new { Index = idx, Score = DeterministicPointScore(p, idx, $"spike_{seed}") })
            .OrderBy(t => t.Score)
            .Take(targetCount)
            .Select(t => t.Index)
            .OrderBy(idx => idx)
            .ToList();
    }

    private static Point3D? ApplyLayout(StructureLayout layout, double x, double y, double z, int gx, int gy, int gz, int maxX, int maxY, int maxZ, StructureDefinition def)
    {
        return layout switch
        {
            StructureLayout.CorticalSheet => CorticalSheet(x, y, z, gx, gy, gz, maxX, maxY, maxZ, def),
            StructureLayout.HippocampalArc => HippocampalArc(x, y, z, gx, gy, maxX, maxY, def),
            StructureLayout.CerebellarSheet => CerebellarSheet(x, y, z, gx, gy, gz, maxX, maxY, maxZ, def),
            StructureLayout.BrainstemColumn => BrainstemColumn(x, y, z, def),
            StructureLayout.OlfactoryBulbShell => OlfactoryBulbShell(x, y, z, def),
            _ => NucleusBlock(x, y, z, def)
        };
    }

    private static Point3D? CorticalSheet(double x, double y, double z, int gx, int gy, int gz, int maxX, int maxY, int maxZ, StructureDefinition def)
    {
        var u = gx / (double)Math.Max(1, maxX - 1);
        var v = gy / (double)Math.Max(1, maxY - 1);
        var w = gz / (double)Math.Max(1, maxZ - 1);
        var jitter = DeterministicJitter(gx, gy, gz, $"cortex_{def.SnapshotId}");
        var along = Math.Clamp((u * 0.992) + (jitter.X * 0.008), 0.0, 1.0);
        var width = Math.Clamp((w * 0.990) + (0.006 * Math.Sin(u * Math.PI * 1.2)) + (jitter.Z * 0.010), 0.0, 1.0);
        var lamina = ((v - 0.5) * MmToRender(3.9)) + (jitter.Y * MmToRender(0.24));
        return TryBuildCorticalTerritoryPoint(def.SnapshotId, along, width, lamina, jitter, 1.0, out var point)
            ? point
            : null;
    }

    private static Point3D BuildCorticalGyrusPoint(
        string snapshotId,
        double along,
        double width,
        double laminaDepth,
        Vector3D jitter,
        double hemisphereSign)
    {
        if (TryBuildCorticalTerritoryPoint(
                snapshotId,
                along,
                width,
                laminaDepth,
                jitter,
                hemisphereSign,
                out var point))
        {
            return point;
        }

        return BuildCorticalTerritoryPointUnchecked(
            snapshotId,
            (Math.Clamp(along, 0.0, 1.0) - 0.5) * 2.0,
            0.0,
            laminaDepth,
            hemisphereSign);
    }

    private static (bool InsideTube, double TubeBulge, double SulcusDepth) ComputeCorticalGyrusTubeField(
        double theta,
        double phi,
        (double ThMin, double ThMax, double PhMin, double PhMax, double PieceAx, double PieceAy, double RidgePhase, double FoldScale, double GyrusCenter, double GyrusWidth) patch)
    {
        static double SmoothStep01(double value)
        {
            var t = Math.Clamp(value, 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }

        var thetaSpan = Math.Max(0.001, patch.ThMax - patch.ThMin);
        var phiSpan = Math.Max(0.001, patch.PhMax - patch.PhMin);
        var normalizedTheta = Math.Clamp((theta - patch.ThMin) / thetaSpan, 0.0, 1.0);
        var normalizedGyrusCenter = Math.Clamp(patch.GyrusCenter, 0.14, 0.86);
        var normalizedGyrusWidth = Math.Clamp(patch.GyrusWidth, 0.24, 1.20);
        var posteriorPole = Math.Clamp(-Math.Sin(theta), 0.0, 1.0);
        var superiorPole = Math.Clamp(Math.Sin(phi), 0.0, 1.0);
        var posteriorContinuity = SmoothStep01((posteriorPole - 0.26) / 0.74);
        var baseCenterPhi = patch.PhMin + (phiSpan * normalizedGyrusCenter);
        var turns = 2.0 + (1.6 * Math.Clamp(normalizedGyrusWidth, 0.28, 1.10));
        var phase = patch.RidgePhase * 1.6;
        var primaryAmp = phiSpan * (0.10 + (0.14 * normalizedGyrusWidth)) * (0.85 + (0.20 * patch.FoldScale));
        var secondaryAmp = primaryAmp * 0.38;
        var tubeCenterPhi = baseCenterPhi
            + (Math.Sin((normalizedTheta * Math.PI * 2.0 * turns) + phase) * primaryAmp)
            + (Math.Sin((normalizedTheta * Math.PI * 2.0 * ((turns * 0.55) + 0.65)) - (phase * 0.6)) * secondaryAmp);
        var silhouetteFill = 1.0 + (0.20 * posteriorPole) + (0.14 * superiorPole);
        var tubeHalfWidth = phiSpan * (0.15 + (0.12 * normalizedGyrusWidth)) * silhouetteFill;
        var radialPhi = Math.Abs(phi - tubeCenterPhi) / Math.Max(0.001, tubeHalfWidth);
        var capSpan = 0.07 + (0.04 * Math.Clamp(normalizedGyrusWidth, 0.24, 1.10));
        var startCap = normalizedTheta < capSpan
            ? (capSpan - normalizedTheta) / Math.Max(0.001, capSpan)
            : 0.0;
        var endCap = normalizedTheta > (1.0 - capSpan)
            ? (normalizedTheta - (1.0 - capSpan)) / Math.Max(0.001, capSpan)
            : 0.0;
        var capDistance = Math.Max(startCap, endCap);
        var radial = Math.Sqrt((radialPhi * radialPhi) + (capDistance * capDistance));
        radial *= 1.0 - (0.12 * posteriorContinuity);
        var edgeGuard = SmoothStep01(Math.Min(normalizedTheta, 1.0 - normalizedTheta) / 0.14);
        var insideTubeThreshold =
            1.02 + (0.28 * edgeGuard) + (0.18 * posteriorPole) + (0.10 * superiorPole) + (0.22 * posteriorContinuity);
        var insideTube = radial <= insideTubeThreshold;
        var silhouetteRidgeDamp = Math.Clamp(1.0 - (0.26 * posteriorPole) - (0.18 * superiorPole), 0.58, 1.0);
        var tubeBulge = Math.Clamp(1.0 - (radial * radial), 0.0, 1.0) * (0.78 + (0.22 * edgeGuard)) * silhouetteRidgeDamp;
        tubeBulge = Math.Clamp(tubeBulge + (0.14 * posteriorContinuity), 0.0, 1.0);
        var sulcusDepth = Math.Clamp((radial - 0.48) / 0.52, 0.0, 1.0) * (0.35 + (0.65 * edgeGuard)) * silhouetteRidgeDamp;
        sulcusDepth *= 1.0 - (0.42 * posteriorContinuity);
        return (insideTube, tubeBulge, sulcusDepth);
    }

    private static (double ThMin, double ThMax, double PhMin, double PhMax, double PieceAx, double PieceAy, double RidgePhase, double FoldScale, double GyrusCenter, double GyrusWidth) GetCorticalPatch(string snapshotId)
    {
        // Patch extents provide visual area; their centres are derived from the
        // canonical cortical atlas rather than an independent angular map.
        var template = snapshotId switch
        {
            "Pfc" => (0.56, 1.74, -0.20, 1.36, 0.74, 0.68, 0.12, 1.08, 0.55, 0.60),
            "OrbitofrontalCortex" => (0.42, 1.40, -0.88, 0.18, 0.72, 0.56, 0.24, 1.04, 0.25, 0.54),
            "Insula" => (-0.82, 0.28, -0.72, 0.22, 0.56, 0.60, 0.66, 0.94, -0.05, 0.50),
            "Sma" => (0.08, 0.98, 0.74, 1.52, 0.58, 0.62, 0.48, 1.02, 0.78, 0.48),
            "M1" => (-0.20, 0.78, 0.20, 1.10, 0.64, 0.62, 0.82, 1.00, 0.46, 0.52),
            "S1" => (-0.92, 0.18, 0.16, 1.06, 0.66, 0.66, 1.15, 0.98, 0.18, 0.54),
            "Ppc" => (-1.56, -0.24, 0.24, 1.26, 0.74, 0.74, 1.52, 0.98, -0.18, 0.58),
            "V1" => (-2.05, -1.10, -0.08, 0.92, 0.62, 0.64, 1.96, 0.94, -0.52, 0.50),
            "V2" => (-1.92, -0.96, 0.00, 0.98, 0.62, 0.64, 2.08, 0.96, -0.46, 0.50),
            "V4" => (-1.74, -0.54, -0.28, 0.78, 0.66, 0.64, 2.22, 0.98, -0.36, 0.52),
            "Mt" => (-1.56, -0.30, -0.58, 0.46, 0.70, 0.58, 2.50, 1.00, -0.30, 0.52),
            "A1" => (-1.08, -0.10, -0.72, 0.24, 0.64, 0.54, 2.34, 1.00, -0.32, 0.50),
            "TemporalAssociation" => (-1.72, 0.08, -1.46, 0.08, 0.84, 0.70, 2.76, 1.02, -0.45, 0.56),
            "WernickePstgPsts" => (-1.28, -0.22, -0.66, 0.38, 0.66, 0.56, 2.58, 1.00, -0.28, 0.52),
            "SupramarginalAngular" => (-1.34, -0.20, -0.18, 0.76, 0.68, 0.60, 2.42, 1.00, -0.16, 0.52),
            "Acc" => (0.18, 1.12, 0.56, 1.54, 0.58, 0.66, 3.08, 1.00, 0.52, 0.44),
            "EntorhinalCortex" => (-1.02, 0.16, -1.62, -0.78, 0.62, 0.58, 3.44, 0.90, -0.68, 0.40),
            "ParahippocampalCortex" => (-1.40, -0.26, -1.34, -0.42, 0.70, 0.56, 3.30, 0.92, -0.60, 0.46),
            "PerirhinalCortex" => (-1.34, -0.16, -1.08, -0.20, 0.66, 0.54, 3.18, 0.92, -0.54, 0.44),
            "BrocaBa44Ba45" => (0.42, 1.44, -0.22, 0.78, 0.64, 0.62, 0.58, 1.04, 0.38, 0.52),
            "PremotorCortex" => (-0.12, 0.94, 0.34, 1.26, 0.62, 0.62, 0.34, 1.02, 0.58, 0.50),
            "PosteriorCingulate" => (-0.40, 0.52, 0.46, 1.36, 0.58, 0.64, 2.86, 0.98, 0.32, 0.48),
            "RetrosplenialCortex" => (-0.72, 0.18, 0.40, 1.22, 0.56, 0.62, 2.98, 0.96, 0.24, 0.46),
            _ => (-0.60, 0.40, -0.30, 0.70, 0.52, 0.52, 0.0, 1.0, 0.0, 0.95)
        };

        return AnchorCorticalPatchToAtlas(snapshotId, template);
    }

    private static (double ThMin, double ThMax, double PhMin, double PhMax, double PieceAx, double PieceAy, double RidgePhase, double FoldScale, double GyrusCenter, double GyrusWidth) AnchorCorticalPatchToAtlas(
        string snapshotId,
        (double ThMin, double ThMax, double PhMin, double PhMax, double PieceAx, double PieceAy, double RidgePhase, double FoldScale, double GyrusCenter, double GyrusWidth) template)
    {
        var anchor = GetCorticalStructureAnchor(snapshotId, "R");
        var unrolled = UnrotateCorticalShellFromMidlineAroundZ(anchor, 1.0);
        var (theta, phi) = GetCorticalSurfaceParameters(unrolled);
        var extentScale = snapshotId switch
        {
            // V1 and V2 are adjacent fields, not two broad coincident sheets.
            "V1" or "V2" => 0.34,
            "Pfc" => 0.58,
            "BrocaBa44Ba45" => 0.62,
            _ => 0.72
        };
        var halfTheta = (template.ThMax - template.ThMin) * extentScale * 0.5;
        var halfPhi = (template.PhMax - template.PhMin) * extentScale * 0.5;
        // A hemisphere occupies one lateral half of the ellipsoid. Extending
        // theta beyond +/- pi/2 changes the sign of cos(theta); the old shell
        // then folded those vertices back with Abs(x), producing the tall,
        // doubled posterior outline visible in the reference screenshots.
        const double thetaMin = -1.52;
        const double thetaMax = 1.52;
        const double phiMin = -1.00;
        const double phiMax = 1.32;

        return (
            Math.Clamp(theta - halfTheta, thetaMin, thetaMax),
            Math.Clamp(theta + halfTheta, thetaMin, thetaMax),
            Math.Clamp(phi - halfPhi, phiMin, phiMax),
            Math.Clamp(phi + halfPhi, phiMin, phiMax),
            template.PieceAx,
            template.PieceAy,
            template.RidgePhase,
            template.FoldScale,
            template.GyrusCenter,
            template.GyrusWidth);
    }

    private static CorticalGyrusProfile GetCorticalGyrusProfile(string snapshotId)
    {
        return snapshotId switch
        {
            // Frontal gyri and premotor strip.
            "Pfc" => new CorticalGyrusProfile("Superior/Middle Frontal Gyri", 0.62, 0.36, 1.35, 0.055, 2.10, 1.18, 0.42, 0.030),
            "OrbitofrontalCortex" => new CorticalGyrusProfile("Orbitofrontal Gyrus Shelf", 0.28, 0.34, 0.95, 0.034, 1.65, 0.95, 0.30, 0.018),
            "BrocaBa44Ba45" => new CorticalGyrusProfile("Inferior Frontal Gyrus", 0.36, 0.28, 1.25, 0.050, 1.90, 1.08, 0.34, 0.026),
            "PremotorCortex" => new CorticalGyrusProfile("Premotor Gyrus", 0.58, 0.30, 1.05, 0.040, 1.95, 1.05, 0.34, 0.018),
            "Sma" => new CorticalGyrusProfile("Supplementary Motor Medial Gyrus", 0.70, 0.24, 0.82, 0.026, 1.80, 0.88, 0.30, 0.012),
            "M1" => new CorticalGyrusProfile("Precentral Gyrus", 0.48, 0.26, 0.76, 0.018, 2.30, 1.25, 0.42, 0.010),
            "S1" => new CorticalGyrusProfile("Postcentral Gyrus", 0.50, 0.26, 0.78, 0.018, 2.20, 1.22, 0.40, 0.010),

            // Parietal bridge and posterior association gyri.
            "Ppc" => new CorticalGyrusProfile("Superior/Inferior Parietal Lobules", 0.58, 0.34, 1.10, 0.050, 2.00, 1.12, 0.36, 0.022),
            "SupramarginalAngular" => new CorticalGyrusProfile("Supramarginal and Angular Gyri", 0.43, 0.30, 1.45, 0.070, 2.10, 1.18, 0.40, 0.030),
            "PosteriorCingulate" => new CorticalGyrusProfile("Posterior Cingulate Gyrus", 0.45, 0.22, 0.90, 0.026, 1.65, 0.82, 0.26, 0.010),
            "RetrosplenialCortex" => new CorticalGyrusProfile("Retrosplenial Gyrus", 0.40, 0.22, 0.85, 0.024, 1.55, 0.78, 0.24, 0.010),

            // Occipital and visual association cortex.
            "V1" => new CorticalGyrusProfile("Calcarine/Primary Visual Cortex", 0.50, 0.30, 1.20, 0.045, 1.95, 1.05, 0.34, 0.018),
            "V2" => new CorticalGyrusProfile("Occipital Visual Belt", 0.54, 0.31, 1.25, 0.048, 1.95, 1.05, 0.34, 0.020),
            "V4" => new CorticalGyrusProfile("Inferior Occipital/Temporal Visual Gyrus", 0.45, 0.30, 1.30, 0.052, 1.95, 1.05, 0.34, 0.024),
            "Mt" => new CorticalGyrusProfile("Lateral Occipito-temporal Gyrus", 0.42, 0.28, 1.15, 0.045, 1.85, 1.00, 0.32, 0.020),

            // Auditory, language, and temporal gyri.
            "A1" => new CorticalGyrusProfile("Superior Temporal Gyrus", 0.50, 0.28, 1.05, 0.036, 1.90, 1.05, 0.34, 0.018),
            "WernickePstgPsts" => new CorticalGyrusProfile("Posterior Superior Temporal Gyrus", 0.50, 0.28, 1.10, 0.042, 1.95, 1.08, 0.34, 0.020),
            "TemporalAssociation" => new CorticalGyrusProfile("Middle/Inferior Temporal Gyri", 0.38, 0.34, 1.35, 0.060, 2.05, 1.12, 0.38, 0.026),
            "EntorhinalCortex" => new CorticalGyrusProfile("Entorhinal Cortex", 0.28, 0.22, 0.80, 0.020, 1.50, 0.72, 0.20, 0.008),
            "ParahippocampalCortex" => new CorticalGyrusProfile("Parahippocampal Gyrus", 0.34, 0.26, 0.92, 0.026, 1.60, 0.80, 0.22, 0.010),
            "PerirhinalCortex" => new CorticalGyrusProfile("Perirhinal Cortex", 0.36, 0.24, 0.90, 0.024, 1.55, 0.78, 0.22, 0.010),

            // Insula is tucked deeper and should not dominate the outer shell.
            "Insula" => new CorticalGyrusProfile("Insular Gyri", 0.46, 0.24, 1.25, 0.050, 1.15, 0.72, 0.18, 0.020),
            "Acc" => new CorticalGyrusProfile("Anterior Cingulate Gyrus", 0.48, 0.22, 0.86, 0.024, 1.55, 0.78, 0.24, 0.010),
            _ => new CorticalGyrusProfile("Cortical Gyrus", 0.50, 0.30, 1.0, 0.035, 1.80, 0.90, 0.28, 0.016)
        };
    }

    private static Point3D HippocampalArc(double x, double y, double z, int gx, int gy, int maxX, int maxY, StructureDefinition def)
    {
        var t = gx / (double)Math.Max(1, maxX - 1);
        var theta = (t - 0.5) * Math.PI * 0.9;
        var radius = (def.RadiusX * 0.42) + (y * 0.10);
        // The mean cosine over this arc is approximately 0.70. Removing it
        // keeps the subfield centroid on its measured atlas coordinate.
        var arcX = (Math.Cos(theta) - 0.70) * radius;
        var arcZ = Math.Sin(theta) * def.RadiusZ * 0.46;
        var lamina = ((gy / (double)Math.Max(1, maxY - 1)) - 0.5) * def.RadiusY * 0.40;
        return new Point3D(arcX, lamina, arcZ + (z * 0.08));
    }

    private static Point3D? CerebellarSheet(double x, double y, double z, int gx, int gy, int gz, int maxX, int maxY, int maxZ, StructureDefinition def)
    {
        static double SuperEllipsoidMetric(double px, double py, double pz, double ax, double ay, double az, double ex, double ey, double ez)
        {
            var xTerm = Math.Pow(Math.Abs(px / Math.Max(0.001, ax)), ex);
            var yTerm = Math.Pow(Math.Abs(py / Math.Max(0.001, ay)), ey);
            var zTerm = Math.Pow(Math.Abs(pz / Math.Max(0.001, az)), ez);
            return xTerm + yTerm + zTerm;
        }

        var u = gx / (double)Math.Max(1, maxX - 1);
        var v = gy / (double)Math.Max(1, maxY - 1);
        var w = gz / (double)Math.Max(1, maxZ - 1);
        var nx = (u - 0.5) * 2.0;
        var ny = (v - 0.5) * 2.0;
        var nz = (w - 0.5) * 2.0;

        // Bilobed cerebellar envelope: compact paired lobes with a shallow
        // anterior notch so the top view reads like two rounded lobules.
        var leftBody = SuperEllipsoidMetric(nx + 0.52, ny + 0.02, nz + 0.04, 0.64, 0.66, 0.70, 2.0, 2.2, 2.0) <= 1.0;
        var rightBody = SuperEllipsoidMetric(nx - 0.52, ny + 0.02, nz + 0.04, 0.64, 0.66, 0.70, 2.0, 2.2, 2.0) <= 1.0;
        var vermis = SuperEllipsoidMetric(nx, ny + 0.04, nz + 0.08, 0.22, 0.70, 0.66, 2.0, 2.0, 2.0) <= 1.0;
        if (!leftBody && !rightBody && !vermis)
        {
            return null;
        }

        if (Math.Abs(nx) < 0.18 && nz > 0.18 && ny > -0.62)
        {
            return null;
        }

        // Vallecula and posterior midline notch.
        if (Math.Abs(nx) < 0.055 && ny < -0.52 && nz < 0.35)
        {
            return null;
        }

        var curvedX = Math.Sign(nx) * Math.Pow(Math.Abs(nx), 1.08);
        var posteriorRound = 1.0 + (0.16 * Math.Clamp(-nz + 0.36, 0.0, 1.0));
        var inferiorCompression = ny < 0 ? 0.70 + (0.16 * (1.0 - Math.Abs(nx))) : 1.0;
        var superiorDome = 0.16 * Math.Exp(-(nx * nx) / 0.55) * Math.Exp(-Math.Pow(nz + 0.02, 2) / 0.32);
        var vermisRidge = 0.11 * Math.Exp(-(nx * nx) / 0.08) * Math.Exp(-Math.Pow(nz + 0.08, 2) / 0.34);

        // Fine folia corrugation; dominant along superior-inferior axis to match posterior ridges.
        var foliaEnvelope = Math.Clamp(1.0 - (Math.Abs(ny) * 0.72), 0.0, 1.0) * (0.55 + (0.45 * Math.Clamp(nz + 0.25, 0.0, 1.0)));
        var folia = (
            Math.Sin((ny + 0.58) * Math.PI * 15.5 + (Math.Abs(nx) * 2.6)) +
            (0.35 * Math.Sin((w * Math.PI * 9.0) - (u * Math.PI * 3.2))))
            * def.RadiusZ * 0.045 * foliaEnvelope;

        return new Point3D(
            curvedX * def.RadiusX * 0.48 * posteriorRound,
            (ny * def.RadiusY * 0.84 * inferiorCompression) + ((superiorDome + vermisRidge) * def.RadiusY),
            (nz * def.RadiusZ * 0.46) + folia);
    }

    private static Vector3D DeterministicJitter(int x, int y, int z, string seed)
    {
        unchecked
        {
            var h = 2166136261u;
            h = (h ^ (uint)x) * 16777619u;
            h = (h ^ (uint)(y * 31)) * 16777619u;
            h = (h ^ (uint)(z * 131)) * 16777619u;
            foreach (var c in seed)
            {
                h = (h ^ c) * 16777619u;
            }

            static double Axis(uint bits, int shift)
            {
                var v = ((bits >> shift) & 0x3FFu) / 1023.0;
                return (v * 2.0) - 1.0;
            }

            return new Vector3D(Axis(h, 0), Axis(h, 10), Axis(h, 20));
        }
    }

    private static Point3D? NucleusBlock(double x, double y, double z, StructureDefinition def)
    {
        static double Sq(double v) => v * v;
        static double ExpMetric(double px, double py, double pz, double ax, double ay, double az, double ex, double ey, double ez)
        {
            return Math.Pow(Math.Abs(px / Math.Max(0.001, ax)), ex) +
                   Math.Pow(Math.Abs(py / Math.Max(0.001, ay)), ey) +
                   Math.Pow(Math.Abs(pz / Math.Max(0.001, az)), ez);
        }

        var rx = Math.Max(0.001, def.RadiusX * 0.5);
        var ry = Math.Max(0.001, def.RadiusY * 0.5);
        var rz = Math.Max(0.001, def.RadiusZ * 0.5);

        switch (def.SnapshotId)
        {
            case "ArcuateFasciculus":
            {
                var u = z / rz;
                if (Math.Abs(u) > 1.0)
                {
                    return null;
                }

                var curve = Math.Sin((u + 0.10) * Math.PI * 0.72);
                var xCenter = -0.14 * rx + (0.46 * rx * curve);
                var yCenter = (0.18 * ry * Math.Cos((u - 0.10) * Math.PI)) + (0.08 * ry);
                var tubeRx = rx * (0.18 + (0.05 * (1.0 - Math.Abs(u))));
                var tubeRy = ry * (0.28 + (0.04 * (1.0 - Math.Abs(u))));
                var m = Sq((x - xCenter) / Math.Max(0.001, tubeRx)) +
                        Sq((y - yCenter) / Math.Max(0.001, tubeRy));
                return m <= 1.0 ? new Point3D(x, y, z) : null;
            }
            case "CorpusCallosum":
            {
                var u = z / rz;
                if (Math.Abs(u) > 1.0)
                {
                    return null;
                }

                var anteriorGenu = Math.Exp(-Math.Pow(u - 0.84, 2) / 0.055);
                var posteriorSplenium = Math.Exp(-Math.Pow(u + 0.86, 2) / 0.070);
                var bodyArch = 0.52 * ry * (1.0 - (0.38 * u * u));
                var yCenter = bodyArch - (0.78 * ry * anteriorGenu) - (0.28 * ry * posteriorSplenium);
                var endTaper = Math.Clamp((1.0 - Math.Abs(u)) / 0.18, 0.0, 1.0);
                var bulb = Math.Max(anteriorGenu, posteriorSplenium);
                var halfWidth = rx * (0.34 + (0.34 * (1.0 - Math.Abs(u))) + (0.18 * bulb)) * (0.72 + (0.28 * endTaper));
                var halfHeight = ry * (0.22 + (0.26 * bulb));

                if (Math.Abs(x) > halfWidth)
                {
                    return null;
                }

                var lateralDrape = -0.10 * ry * Math.Pow(x / Math.Max(0.001, halfWidth), 2) * (1.0 - Math.Abs(u) * 0.35);
                if (Math.Abs(y - yCenter - lateralDrape) > halfHeight)
                {
                    return null;
                }

                var roundedEdge = Sq(x / Math.Max(0.001, halfWidth)) + Sq((y - yCenter - lateralDrape) / Math.Max(0.001, halfHeight));
                if (roundedEdge > 1.0)
                {
                    return null;
                }

                return new Point3D(x, y, z);
            }
            case "Striatum":
            {
                var outer = ExpMetric(x, y, z, rx * 1.10, ry * 0.92, rz * 0.92, 1.7, 2.0, 1.8) <= 1.0;
                if (!outer)
                {
                    return null;
                }

                // Carve medial ventricle-side indentation for a caudate/putamen crescent feel.
                var innerX = Math.Abs(x) - (rx * 0.20);
                var inner = (Sq(innerX / (rx * 0.42)) + Sq((y + (ry * 0.08)) / (ry * 0.62)) + Sq(z / (rz * 0.62))) <= 1.0;
                if (inner)
                {
                    return null;
                }

                return new Point3D(x, y, z);
            }
            case "NucleusAccumbens":
            {
                var m = ExpMetric(x, y, z, rx * 0.90, ry * 0.78, rz * 0.74, 1.8, 2.0, 1.8);
                return m <= 1.0 ? new Point3D(x, y, z) : null;
            }
            case "GlobusPallidus":
            case "VentralPallidum":
            case "GPe":
            case "GPi":
            case "Stn":
            {
                // Lentiform nuclei are flattened dorsoventrally.
                var m = ExpMetric(x, y, z, rx * 1.04, ry * 0.62, rz * 0.82, 1.5, 2.8, 1.8);
                return m <= 1.0 ? new Point3D(x, y, z) : null;
            }
            case "Thalamus":
            case "MotorThalamus":
            case "Trn":
            case "Pulvinar":
            case "MediodorsalThalamus":
            case "IntralaminarThalamus":
            {
                var ovoid = ExpMetric(x, y, z, rx * 1.06, ry * 0.94, rz * 0.88, 1.7, 2.0, 1.7) <= 1.0;
                if (!ovoid)
                {
                    return null;
                }

                // Mild dorsal convexity for thalamic contour.
                var dorsalLift = 0.08 * def.RadiusY * Math.Exp(-(Sq(z / rz) + Sq(x / rx)) / 1.2);
                return new Point3D(x, y + dorsalLift, z);
            }
            case "Amygdala":
            {
                var zNorm = Math.Clamp(z / rz, -1.0, 1.0);
                var taper = 1.0 - (0.24 * Math.Clamp((zNorm + 1.0) * 0.5, 0.0, 1.0));
                var m = ExpMetric(x, y, z, rx * taper, ry * 0.84, rz * 0.84, 1.6, 2.0, 1.6);
                return m <= 1.0 ? new Point3D(x, y, z) : null;
            }
            case "Hypothalamus":
            {
                var m = ExpMetric(x, y + (0.10 * ry), z, rx * 0.86, ry * 0.74, rz * 0.70, 1.8, 2.2, 1.8);
                if (m > 1.0)
                {
                    return null;
                }

                // Slight ventral point.
                var ventral = 0.10 * def.RadiusY * Math.Clamp((1.0 - Math.Abs(z / rz)) * (1.0 - Math.Abs(x / rx)), 0.0, 1.0);
                return new Point3D(x, y - ventral, z);
            }
            case "SuperiorColliculus":
            case "InferiorColliculus":
            {
                // Dorsal midbrain collicular domes.
                if (y < -0.22 * ry)
                {
                    return null;
                }

                var m = ExpMetric(x, y - (0.10 * ry), z, rx * 0.74, ry * 0.66, rz * 0.68, 1.8, 2.1, 1.8);
                return m <= 1.0 ? new Point3D(x, y, z) : null;
            }
            case "Habenula":
            case "PeriaqueductalGray":
            case "CochlearNucleus":
            case "SuperiorOlive":
            case "VestibularNuclei":
            case "NucleusTractusSolitarius":
            {
                var m = ExpMetric(x, y, z, rx * 0.86, ry * 0.82, rz * 0.80, 1.9, 2.2, 1.9);
                return m <= 1.0 ? new Point3D(x, y, z) : null;
            }
            default:
            {
                var nx = x / rx;
                var ny = y / ry;
                var nz = z / rz;
                return (nx * nx) + (ny * ny) + (nz * nz) <= 1.0 ? new Point3D(x, y, z) : null;
            }
        }
    }

    private static Point3D? BrainstemColumn(double x, double y, double z, StructureDefinition def)
    {
        var ny = y / Math.Max(0.001, def.RadiusY * 0.5);
        var taper = 0.86 - (0.22 * Math.Abs(ny));
        var pontineBulge = 1.0 + (0.12 * Math.Exp(-Math.Pow(ny + 0.15, 2) / 0.18));
        var rx = Math.Max(0.001, def.RadiusX * 0.42 * taper * pontineBulge);
        var rz = Math.Max(0.001, def.RadiusZ * 0.42 * taper * pontineBulge);

        var nx = x / rx;
        var nz = z / rz;
        if ((nx * nx) + (nz * nz) > 1.0)
        {
            return null;
        }

        // Mild anterior convexity so the profile reads as a rounded brainstem column.
        var anteriorCurve = 0.10 * def.RadiusZ * (1.0 - Math.Min(1.0, Math.Abs(ny)));
        return new Point3D(x, y * 1.08, z + anteriorCurve);
    }

    private static Point3D? OlfactoryBulbShell(double x, double y, double z, StructureDefinition def)
    {
        var nx = x / Math.Max(0.001, def.RadiusX * 0.5);
        var ny = y / Math.Max(0.001, def.RadiusY * 0.5);
        var nz = z / Math.Max(0.001, def.RadiusZ * 0.5);
        var d2 = (nx * nx) + (ny * ny) + (nz * nz);
        return d2 is >= 0.52 and <= 1.0 ? new Point3D(x, y, z) : null;
    }

    private static Point3D GetHemisphereCenter(Point3D baseCenter, string hemisphere)
    {
        if (hemisphere == "M")
        {
            return baseCenter;
        }

        var magnitude = Math.Abs(baseCenter.X);
        var hemiX = hemisphere == "L" ? -magnitude : magnitude;
        return new Point3D(hemiX, baseCenter.Y, baseCenter.Z);
    }

    private static Point3D GetEnforcedAtlasCenter(string snapshotId, string hemisphere, Point3D fallback, StructureLayout effectiveLayout)
    {
        if (effectiveLayout == StructureLayout.CorticalSheet)
        {
            return fallback;
        }

        if (!TryGetSubcorticalAtlasGeometry(snapshotId, hemisphere, out var geometry))
        {
            return fallback;
        }

        return MmToRender(geometry.CenterMm);
    }

    // The atlas is stored in physical millimetres. Rendering is a single uniform
    // conversion; no display-only translations or per-structure scale corrections
    // are applied to positions.
    private static Point3D GetCanonicalAtlasCenter(string snapshotId, string hemisphere)
    {
        if (!TryGetSubcorticalAtlasGeometry(snapshotId, hemisphere, out var geometry))
        {
            return new Point3D();
        }

        return MmToRender(geometry.CenterMm);
    }

    private static bool TryGetSubcorticalAtlasCenterMm(string snapshotId, out Point3D centerMm)
    {
        if (!TryGetSubcorticalAtlasGeometry(snapshotId, "R", out var geometry))
        {
            centerMm = default;
            return false;
        }

        centerMm = geometry.CenterMm;
        return true;
    }

    private static Point3D ComputeAnchorPoint(Point3D center, IReadOnlyList<Point3D> localPoints)
    {
        if (localPoints.Count == 0)
        {
            return center;
        }

        var sx = 0.0;
        var sy = 0.0;
        var sz = 0.0;
        foreach (var p in localPoints)
        {
            sx += p.X;
            sy += p.Y;
            sz += p.Z;
        }

        var n = 1.0 / localPoints.Count;
        return new Point3D(center.X + (sx * n), center.Y + (sy * n), center.Z + (sz * n));
    }

    private static Point3D GetCorticalHemisphereCenter(string hemisphere)
    {
        // Cortical sheet points are generated directly on the shared anatomical shell.
        // Do not apply a second hemisphere offset here, or the lower cortex is pulled
        // away from the shell and can disappear from preset views.
        return new Point3D(0, 0, 0);
    }

    private static Point3D GetCorticalStructureAnchor(string snapshotId, string hemisphere)
    {
        // Approximate atlas anchor points in millimeters (x lateral, y superior, z anterior).
        var mm = snapshotId switch
        {
            "Pfc" => new Point3D(42, 38, 34),
            "DorsomedialPrefrontalCortex" => new Point3D(12, 50, 34),
            "VentromedialPrefrontalCortex" => new Point3D(12, -4, 38),
            "FrontalEyeFields" => new Point3D(32, 50, 20),
            "OrbitofrontalCortex" => new Point3D(38, -12, 44),
            "Insula" => new Point3D(40, 4, 0),
            "Sma" => new Point3D(12, 52, 26),
            "M1" => new Point3D(34, 38, 14),
            "S1" => new Point3D(34, 42, -2),
            "Ppc" => new Point3D(30, 48, -28),
            "V1" => new Point3D(18, 24, -62),
            "V2" => new Point3D(26, 26, -54),
            "V3" => new Point3D(34, 24, -48),
            "V4" => new Point3D(44, 18, -42),
            "Mt" => new Point3D(52, 12, -34),
            "A1" => new Point3D(50, 12, -20),
            "AuditoryAssociationCortex" => new Point3D(56, 8, -12),
            "SecondarySomatosensoryCortex" => new Point3D(52, 16, -6),
            "TemporalAssociation" => new Point3D(58, 6, -28),
            "InferotemporalCortex" => new Point3D(50, -4, -32),
            "FusiformGyrus" => new Point3D(38, -12, -34),
            "TemporalPole" => new Point3D(50, -8, 30),
            "WernickePstgPsts" => new Point3D(56, 14, -24),
            "SupramarginalAngular" => new Point3D(46, 28, -30),
            "TemporoparietalJunction" => new Point3D(54, 26, -24),
            "Precuneus" => new Point3D(10, 48, -28),
            "Acc" => new Point3D(8, 38, 18),
            "MidcingulateCortex" => new Point3D(8, 42, -2),
            "EntorhinalCortex" => new Point3D(22, -12, -22),
            "ParahippocampalCortex" => new Point3D(30, -2, -20),
            "PerirhinalCortex" => new Point3D(38, 2, -18),
            "BrocaBa44Ba45" => new Point3D(52, 18, 20),
            "PremotorCortex" => new Point3D(28, 48, 20),
            "PosteriorCingulate" => new Point3D(6, 30, -48),
            "RetrosplenialCortex" => new Point3D(6, 12, -52),
            _ => new Point3D(26, 30, 0)
        };

        var sign = hemisphere == "L" ? -1.0 : 1.0;
        return MmToRender(new Point3D(sign * Math.Abs(mm.X), mm.Y, mm.Z));
    }

    private static Point3D WarpToCorticalShell(Point3D p, string hemisphere)
    {
        // Shared cortical mantle approximation (ellipsoidal shell) for all cortical structures.
        const double rx = 1.95;
        const double ry = 1.06;
        const double rz = 1.40;
        const double shellScale = 1.0;

        var signX = hemisphere == "L" ? -1.0 : 1.0;
        var xAbs = Math.Abs(p.X);

        var nx = xAbs / rx;
        var ny = p.Y / ry;
        var nz = p.Z / rz;

        var norm = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
        if (norm < 1e-6)
        {
            norm = 1.0;
        }

        var sx = (nx / norm) * rx * shellScale;
        var sy = (ny / norm) * ry * shellScale;
        var sz = (nz / norm) * rz * shellScale;

        // Slight dorsal convexity to better read as cortical crown.
        sy += 0.07 * (1.0 - Math.Clamp(Math.Abs(sz) / rz, 0.0, 1.0));

        return new Point3D(signX * sx, sy, sz);
    }

    private static IEnumerable<Point3D> WarpCorticalLocalPointsToShell(IEnumerable<Point3D> localPoints, Point3D center, string hemisphere, string snapshotId)
    {
        var anchor = GetCorticalStructureAnchor(snapshotId, hemisphere);
        var anchorProjected = ProjectPointToCorticalShell(anchor, hemisphere);
        var anchorBias = GetCorticalAnchorBias(snapshotId);

        foreach (var p in localPoints)
        {
            var world = new Point3D(center.X + p.X, center.Y + p.Y, center.Z + p.Z);
            var projected = ProjectPointToCorticalShell(world, hemisphere);
            if (anchorBias > 0.0)
            {
                projected = LerpPoint(projected, anchorProjected, anchorBias);
            }
            var normal = GetCorticalShellNormal(projected, hemisphere);
            var laminarRipple = Math.Sin((p.X * 7.1) + (p.Z * 5.3)) * 0.0028;
            var depth = (p.Y * 0.20) + (p.Z * 0.04) + (p.X * 0.03) + laminarRipple; // preserve cortical laminar thickness while reducing AP striping in every view
            var warpedWorld = new Point3D(
                projected.X + (normal.X * depth),
                projected.Y + (normal.Y * depth),
                projected.Z + (normal.Z * depth));

            yield return new Point3D(warpedWorld.X - center.X, warpedWorld.Y - center.Y, warpedWorld.Z - center.Z);
        }
    }

    private static IEnumerable<Point3D> AlignCorticalLocalPointsToAtlasAnchor(IEnumerable<Point3D> localPoints, Point3D center, string hemisphere, string snapshotId)
    {
        var points = localPoints.ToList();
        if (points.Count == 0)
        {
            return points;
        }

        var anchorBias = GetCorticalAnchorBias(snapshotId) * 0.35;
        if (anchorBias <= 0.0)
        {
            return points;
        }

        var currentAnchor = ComputeAnchorPoint(center, points);
        var targetAnchor = ProjectPointToCorticalShell(GetCorticalStructureAnchor(snapshotId, hemisphere), hemisphere);
        var delta = targetAnchor - currentAnchor;

        return points.Select(p => new Point3D(
            p.X + (delta.X * anchorBias),
            p.Y + (delta.Y * anchorBias),
            p.Z + (delta.Z * anchorBias)));
    }

    private static Point3D ProjectPointToCorticalShell(Point3D p, string hemisphere)
    {
        var signX = hemisphere == "L" ? -1.0 : 1.0;
        var unrolled = UnrotateCorticalShellFromMidlineAroundZ(p, signX);
        var (theta, phi) = GetCorticalSurfaceParameters(unrolled);
        return BuildCorticalSurfacePoint(theta, phi, signX);
    }

    private static Vector3D GetCorticalShellNormal(Point3D p, string hemisphere)
    {
        var signX = hemisphere == "L" ? -1.0 : 1.0;
        var unrolled = UnrotateCorticalShellFromMidlineAroundZ(p, signX);
        var (theta, phi) = GetCorticalSurfaceParameters(unrolled);
        const double eps = 0.012;
        var c = BuildCorticalSurfacePoint(theta, phi, signX);
        var t = BuildCorticalSurfacePoint(theta + eps, phi, signX);
        var u = BuildCorticalSurfacePoint(theta, phi + eps, signX);
        var n = Vector3D.CrossProduct(t - c, u - c);
        if (n.Length < 1e-6)
        {
            return new Vector3D(signX, 0, 0);
        }

        if ((n.X * signX) < 0)
        {
            n *= -1;
        }

        n.Normalize();
        return n;
    }

    private static Point3D BuildCorticalSurfacePoint(double theta, double phi, double hemisphereSign)
    {
        theta = Math.Clamp(theta, -Math.PI / 2.0, Math.PI / 2.0);
        phi = Math.Clamp(phi, -Math.PI / 2.0, Math.PI / 2.0);

        var cosPhi = Math.Cos(phi);
        var lateral = Math.Max(0.0, cosPhi * Math.Cos(theta));
        var vertical = Math.Sin(phi);
        var longitudinal = cosPhi * Math.Sin(theta);
        var anterior = Math.Max(0.0, longitudinal);
        var posterior = Math.Max(0.0, -longitudinal);
        var superior = Math.Max(0.0, vertical);
        var inferior = Math.Max(0.0, -vertical);
        var lateralShoulder = Math.Pow(lateral, 0.72);

        static double Bell(double value, double center, double variance) =>
            Math.Exp(-Math.Pow(value - center, 2.0) / Math.Max(0.001, variance));

        // The cerebrum is not a scaled sphere. These restrained lobar terms are
        // taken from the bundled anterior, lateral, and superior references.
        var frontalLobe = Math.Pow(anterior, 1.18);
        var parietalCrown = Bell(longitudinal, -0.08, 0.24) * Math.Pow(superior, 1.15);
        var temporalLobe =
            Bell(longitudinal, 0.04, 0.40) *
            Bell(vertical, -0.48, 0.22) *
            (0.28 + (0.72 * lateralShoulder));
        var temporalPole =
            Bell(longitudinal, 0.55, 0.12) *
            Bell(vertical, -0.34, 0.16) *
            (0.34 + (0.66 * lateralShoulder));
        var occipitalLobe = Math.Pow(posterior, 1.15);

        var widthRadiusMm = CortexHalfWidthMm *
            (1.0 + (0.035 * frontalLobe) + (0.115 * temporalLobe) + (0.045 * temporalPole) - (0.060 * occipitalLobe));
        var xMm = hemisphereSign * (CortexMidlineGapMm + (lateral * widthRadiusMm));

        var yMm = CortexVerticalCenterMm + (vertical * CortexHalfHeightMm);
        yMm += 3.6 * frontalLobe * (0.30 + (0.70 * superior));
        yMm += 2.4 * parietalCrown;
        yMm -= 12.5 * temporalLobe;
        yMm -= 3.0 * temporalPole;
        yMm -= 1.8 * Math.Pow(superior, 4.0);
        yMm += 1.4 * Math.Pow(inferior, 5.0);

        // Form the orbitofrontal shelf without flattening the whole ventral
        // surface. The temporal lobe remains lower and posterior to this shelf.
        var orbitalShelf = frontalLobe * Math.Pow(inferior, 1.10) * (0.28 + (0.72 * lateralShoulder));
        if (orbitalShelf > 0.01)
        {
            var shelfTargetMm = -27.0 - (3.0 * lateralShoulder);
            var shelfBlend = Math.Clamp(orbitalShelf * 0.64, 0.0, 0.62);
            yMm = (yMm * (1.0 - shelfBlend)) + (shelfTargetMm * shelfBlend);
        }

        var longitudinalRadiusMm = longitudinal >= 0.0
            ? CortexAnteriorRadiusMm
            : CortexPosteriorRadiusMm;
        var zMm = longitudinal * longitudinalRadiusMm;
        zMm += 1.8 * frontalLobe;
        zMm -= 1.2 * occipitalLobe;
        zMm += 2.6 * temporalLobe;
        zMm += 4.4 * temporalPole;

        // The lateral (Sylvian) fissure separates the superior temporal gyrus
        // from frontal and parietal cortex. Its posterior end sits higher than
        // the anterior end, matching the bundled lateral anatomy reference.
        var normalizedLongitudinal = Math.Clamp((longitudinal + 0.72) / 1.42, 0.0, 1.0);
        var sylvianVertical = 0.07 - (0.27 * normalizedLongitudinal);
        var sylvianFissure =
            Bell(vertical, sylvianVertical, 0.0065) *
            Bell(longitudinal, 0.02, 0.46) *
            Math.Pow(lateralShoulder, 1.35);
        xMm = hemisphereSign * Math.Max(
            CortexMidlineGapMm,
            Math.Abs(xMm) - (3.2 * sylvianFissure));
        yMm -= 1.2 * sylvianFissure;

        return RotateCorticalShellTowardMidlineAroundZ(
            MmToRender(new Point3D(xMm, yMm, zMm)),
            hemisphereSign);
    }

    private static (double Theta, double Phi) GetCorticalSurfaceParameters(Point3D renderPoint)
    {
        var xMm = Math.Max(0.0, (Math.Abs(renderPoint.X) / AtlasMmToRender) - CortexMidlineGapMm);
        var yMm = (renderPoint.Y / AtlasMmToRender) - CortexVerticalCenterMm;
        var zMm = renderPoint.Z / AtlasMmToRender;
        var xNorm = xMm / CortexHalfWidthMm;
        var yNorm = yMm / CortexHalfHeightMm;
        var zNorm = zMm / (zMm >= 0.0 ? CortexAnteriorRadiusMm : CortexPosteriorRadiusMm);
        var equatorial = Math.Sqrt((xNorm * xNorm) + (zNorm * zNorm));
        return (
            Math.Atan2(zNorm, Math.Max(0.0001, xNorm)),
            Math.Atan2(yNorm, Math.Max(0.0001, equatorial)));
    }

    private static Point3D RotateCorticalShellTowardMidlineAroundZ(Point3D p, double hemisphereSign)
    {
        // Left hemisphere rolls clockwise; right hemisphere counter-clockwise,
        // bringing both cortical shells 8 degrees toward the longitudinal fissure.
        var signedSin = hemisphereSign < 0.0 ? -CorticalShellMedialRollSin : CorticalShellMedialRollSin;
        var x = (p.X * CorticalShellMedialRollCos) - (p.Y * signedSin);
        var y = (p.X * signedSin) + (p.Y * CorticalShellMedialRollCos);
        return new Point3D(x, y, p.Z);
    }

    private static Point3D UnrotateCorticalShellFromMidlineAroundZ(Point3D p, double hemisphereSign)
    {
        var signedSin = hemisphereSign < 0.0 ? -CorticalShellMedialRollSin : CorticalShellMedialRollSin;
        var x = (p.X * CorticalShellMedialRollCos) + (p.Y * signedSin);
        var y = (-p.X * signedSin) + (p.Y * CorticalShellMedialRollCos);
        return new Point3D(x, y, p.Z);
    }

    private static Vector3D GetNonCorticalLocalScale(string snapshotId)
    {
        return snapshotId switch
        {
            // Diencephalon: ovoid and slightly AP-compressed.
            "Thalamus" => new Vector3D(1.08, 1.00, 0.88),
            "MotorThalamus" => new Vector3D(0.96, 0.92, 0.80),
            "Trn" => new Vector3D(1.18, 0.70, 0.78),
            "Pulvinar" => new Vector3D(1.10, 0.94, 0.92),
            "MediodorsalThalamus" => new Vector3D(0.94, 0.90, 0.80),
            "IntralaminarThalamus" => new Vector3D(0.80, 0.92, 0.74),
            "Hypothalamus" => new Vector3D(0.82, 0.74, 0.72),
            "Habenula" => new Vector3D(0.62, 0.68, 0.58),

            // Basal ganglia / limbic nuclei.
            "Striatum" => new Vector3D(1.16, 0.92, 0.86),
            "NucleusAccumbens" => new Vector3D(0.84, 0.76, 0.72),
            "GlobusPallidus" => new Vector3D(0.86, 0.70, 0.70),
            "VentralPallidum" => new Vector3D(0.78, 0.68, 0.66),
            "GPe" => new Vector3D(0.76, 0.66, 0.62),
            "GPi" => new Vector3D(0.70, 0.64, 0.60),
            "Stn" => new Vector3D(0.58, 0.58, 0.54),
            "Snr" => new Vector3D(0.60, 0.62, 0.56),
            "Snc" => new Vector3D(0.56, 0.62, 0.54),
            "Amygdala" => new Vector3D(0.84, 0.76, 0.74),
            "BasalForebrain" => new Vector3D(0.78, 0.72, 0.70),

            // Hippocampal arch nuclei.
            "DentateGyrus" => new Vector3D(0.84, 0.70, 0.76),
            "CA3" => new Vector3D(0.78, 0.68, 0.72),
            "CA2" => new Vector3D(0.70, 0.62, 0.66),
            "CA1" => new Vector3D(0.80, 0.68, 0.74),
            "Subiculum" => new Vector3D(0.76, 0.66, 0.70),
            "Presubiculum" => new Vector3D(0.72, 0.62, 0.66),
            "Parasubiculum" => new Vector3D(0.68, 0.58, 0.62),

            // Midbrain / brainstem.
            "SuperiorColliculus" => new Vector3D(0.66, 0.58, 0.58),
            "InferiorColliculus" => new Vector3D(0.62, 0.56, 0.54),
            "PeriaqueductalGray" => new Vector3D(0.66, 0.72, 0.60),
            "ReticularFormation" => new Vector3D(0.74, 0.98, 0.72),
            "Pons" => new Vector3D(0.92, 0.88, 0.86),
            "Medulla" => new Vector3D(0.74, 0.96, 0.70),
            "SpinalCordMotor" => new Vector3D(0.58, 1.12, 0.58),
            "InferiorOlive" => new Vector3D(0.52, 0.64, 0.50),
            "LocusCoeruleus" => new Vector3D(0.48, 0.74, 0.46),
            "RapheNuclei" => new Vector3D(0.46, 0.82, 0.44),
            "Vta" => new Vector3D(0.52, 0.70, 0.50),
            "CochlearNucleus" => new Vector3D(0.58, 0.64, 0.56),
            "SuperiorOlive" => new Vector3D(0.56, 0.60, 0.52),
            "VestibularNuclei" => new Vector3D(0.60, 0.66, 0.56),
            "NucleusTractusSolitarius" => new Vector3D(0.54, 0.68, 0.50),

            // Cerebellum.
            "CerebellarGranule" => new Vector3D(1.62, 1.22, 1.44),
            "PurkinjeCellLayer" => new Vector3D(1.54, 1.18, 1.38),
            "CerebellarVermis" => new Vector3D(0.98, 1.10, 1.06),
            "CerebellarLobules" => new Vector3D(1.66, 1.26, 1.48),
            "DeepCerebellarNuclei" => new Vector3D(0.78, 0.74, 0.72),

            // Commissural/fiber tracts and peripheral sensory anchors.
            "CorpusCallosum" => new Vector3D(0.72, 0.42, 0.84),
            "ArcuateFasciculus" => new Vector3D(1.16, 0.72, 1.42),
            "OlfactoryBulb" => new Vector3D(0.58, 0.52, 0.56),
            "Retina" => new Vector3D(0.34, 0.34, 0.34),
            "Cochlea" => new Vector3D(0.42, 0.40, 0.40),
            _ => new Vector3D(1.0, 1.0, 1.0)
        };
    }

    private static Point3D ApplyNonCorticalGlobalShift(Point3D center, string snapshotId)
    {
        var lateralCompression = snapshotId switch
        {
            "OlfactoryBulb" => 0.10,
            "Retina" => 0.05,
            "Cochlea" => 0.08,
            "Thalamus" or "MotorThalamus" or "Trn" or "Pulvinar" or "MediodorsalThalamus" or "IntralaminarThalamus" or
            "Hypothalamus" or "Habenula" or "Stn" or "Snr" or "Snc" or "Striatum" or "NucleusAccumbens" or
            "GlobusPallidus" or "VentralPallidum" or "GPe" or "GPi" or "Amygdala" or "BasalForebrain" or
            "CochlearNucleus" or "SuperiorOlive" or "InferiorColliculus" or "VestibularNuclei" or "NucleusTractusSolitarius" or "PeriaqueductalGray" => 0.92,
            "Pons" or "Medulla" or "InferiorOlive" or "LocusCoeruleus" or "RapheNuclei" or "Vta" or "ReticularFormation" or "SpinalCordMotor" => 0.76,
            "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarVermis" or "CerebellarLobules" or "DeepCerebellarNuclei" => 1.00,
            _ => 1.0
        };

        var (dxMm, dyMm, dzMm) = snapshotId switch
        {
            // Move deep nuclei slightly superior/anterior to sit inside telencephalic bowl.
            "Thalamus" or "MotorThalamus" or "Trn" or "Pulvinar" or "MediodorsalThalamus" or "IntralaminarThalamus" =>
                (0.0, 4.2, 3.6),
            "Striatum" or "NucleusAccumbens" or "GlobusPallidus" or "VentralPallidum" or "GPe" or "GPi" or "Stn" =>
                (0.0, 3.8, 3.0),
            "Snr" or "Snc" or "SuperiorColliculus" or "Vta" =>
                (0.0, 3.0, 5.2),
            "Amygdala" or "BasalForebrain" or "Hypothalamus" or "Habenula" =>
                (0.0, 3.2, 3.2),
            "CochlearNucleus" or "SuperiorOlive" or "InferiorColliculus" or "VestibularNuclei" or "NucleusTractusSolitarius" or "PeriaqueductalGray" =>
                (0.0, 3.8, 3.0),
            // Brainstem+cerebellum: up and slightly anterior to maintain biological relation under occipital lobe.
            "Pons" or "Medulla" or "InferiorOlive" or "LocusCoeruleus" or "RapheNuclei" =>
                (0.0, 11.5, 12.0),
            "ReticularFormation" or "SpinalCordMotor" =>
                (0.0, 10.8, 10.8),
            "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarVermis" or "CerebellarLobules" or "DeepCerebellarNuclei" =>
                (0.0, 4.0, 6.0),
            // Bring olfactory bulbs closer to frontal base.
            "OlfactoryBulb" =>
                (0.0, 4.0, 1.5),
            "Retina" =>
                (0.0, -2.0, -12.0),
            "Cochlea" =>
                (0.0, -4.0, 0.5),
            _ => (0.0, 0.0, 0.0)
        };

        return new Point3D(
            (center.X * lateralCompression) + MmToRender(dxMm),
            center.Y + MmToRender(dyMm),
            center.Z + MmToRender(dzMm));
    }

    private static Point3D LerpPoint(Point3D from, Point3D to, double t)
    {
        var clamped = Math.Clamp(t, 0.0, 1.0);
        return new Point3D(
            from.X + ((to.X - from.X) * clamped),
            from.Y + ((to.Y - from.Y) * clamped),
            from.Z + ((to.Z - from.Z) * clamped));
    }

    private static double GetCorticalAnchorBias(string snapshotId)
    {
        return snapshotId switch
        {
            // Frontal/executive cortex gets slightly stronger anchoring to preserve telencephalic partitioning.
            "Pfc" or "OrbitofrontalCortex" or "BrocaBa44Ba45" or "PremotorCortex" or "M1" or "Sma" => 0.30,
            // Posterior cortices.
            "Ppc" or "V1" or "V2" or "V4" or "Mt" or "PosteriorCingulate" or "RetrosplenialCortex" => 0.28,
            // Lateral temporal and auditory.
            "TemporalAssociation" or "WernickePstgPsts" or "A1" or "SupramarginalAngular" => 0.27,
            // Medial temporal lobe / allocortical rim.
            "EntorhinalCortex" or "ParahippocampalCortex" or "PerirhinalCortex" => 0.24,
            // Default cortical partition pull.
            _ => 0.22
        };
    }

    private static Point3D ApplyEncephalonOffset(Point3D center, string snapshotId, string hemisphere)
    {
        static double TowardMidline(string hemi, double mm)
        {
            return hemi switch
            {
                "L" => mm,
                "R" => -mm,
                _ => 0.0
            };
        }

        var (dxMm, dyMm, dzMm) = snapshotId switch
        {
            // Telencephalic deep nuclei: keep slightly superior/anterior relative to diencephalon.
            "Striatum" or "NucleusAccumbens" or "GlobusPallidus" or "VentralPallidum" or "GPe" or "GPi" or "Amygdala" or "BasalForebrain" =>
                (TowardMidline(hemisphere, 0.8), 1.0, 1.0),

            // Medial temporal telencephalon.
            "DentateGyrus" or "CA3" or "CA2" or "CA1" or "Subiculum" or "Presubiculum" or "Parasubiculum" =>
                (TowardMidline(hemisphere, 0.9), 0.6, -2.0),

            // Diencephalon.
            "Thalamus" or "MotorThalamus" or "Trn" or "Pulvinar" or "MediodorsalThalamus" or "IntralaminarThalamus" or "Hypothalamus" or "Habenula" or "Stn" =>
                (TowardMidline(hemisphere, 0.9), 1.5, -0.4),

            // Mesencephalon.
            "SuperiorColliculus" or "InferiorColliculus" or "PeriaqueductalGray" or "Snr" or "Snc" or "Vta" =>
                (TowardMidline(hemisphere, 0.6), 0.6, -2.8),

            // Metencephalon.
            "Pons" or "SuperiorOlive" or "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarVermis" or "CerebellarLobules" or "DeepCerebellarNuclei" =>
                (0.0, -0.5, -5.6),

            // Myelencephalon / caudal brainstem.
            "Medulla" or "InferiorOlive" or "LocusCoeruleus" or "RapheNuclei" or "CochlearNucleus" or "VestibularNuclei" or "NucleusTractusSolitarius" or "ReticularFormation" or "SpinalCordMotor" =>
                (0.0, -1.5, -7.6),

            // Olfactory bulbs: inferoanterior frontal base, close to midline rim.
            "OlfactoryBulb" =>
                (TowardMidline(hemisphere, 1.2), -1.5, -2.0),
            "Retina" =>
                (TowardMidline(hemisphere, 0.0), -2.0, -26.0),
            "Cochlea" =>
                (TowardMidline(hemisphere, 3.0), -8.0, -2.0),

            // Commissural bridge.
            "CorpusCallosum" =>
                (0.0, 1.2, 0.0),

            _ => (0.0, 0.0, 0.0)
        };

        return new Point3D(
            center.X + MmToRender(dxMm),
            center.Y + MmToRender(dyMm),
            center.Z + MmToRender(dzMm));
    }

    private static Point3D ShiftSuperior(Point3D p, double structureRadiusY, double fraction)
    {
        var shift = structureRadiusY * Math.Clamp(fraction, 0.0, 2.0);
        return new Point3D(p.X, p.Y + shift, p.Z);
    }

    private static double GetNonCorticalShiftFraction(string snapshotId)
    {
        return snapshotId switch
        {
            // Keep brainstem closer to native inferior position.
            "Pons" => 0.18,
            "Medulla" => 0.18,
            "InferiorOlive" => 0.18,
            "LocusCoeruleus" => 0.18,
            "RapheNuclei" => 0.18,
            "Vta" => 0.20,
            "SuperiorColliculus" => 0.22,
            "InferiorColliculus" => 0.22,
            "PeriaqueductalGray" => 0.24,
            "ReticularFormation" => 0.16,
            "CochlearNucleus" => 0.18,
            "SuperiorOlive" => 0.18,
            "VestibularNuclei" => 0.18,
            "NucleusTractusSolitarius" => 0.18,
            "SpinalCordMotor" => 0.10,
            // Cerebellar lamina should not be over-shifted.
            "CerebellarGranule" => 0.08,
            "PurkinjeCellLayer" => 0.08,
            "CerebellarVermis" => 0.08,
            "CerebellarLobules" => 0.08,
            "Thalamus" => 0.28,
            "MotorThalamus" => 0.28,
            "Trn" => 0.24,
            "Pulvinar" => 0.24,
            "MediodorsalThalamus" => 0.22,
            "IntralaminarThalamus" => 0.22,
            "Striatum" => 0.24,
            "NucleusAccumbens" => 0.18,
            "GlobusPallidus" => 0.20,
            "VentralPallidum" => 0.18,
            "GPe" => 0.20,
            "GPi" => 0.20,
            "Amygdala" => 0.16,
            "Hypothalamus" => 0.14,
            "BasalForebrain" => 0.16,
            "CorpusCallosum" => 0.10,
            "Retina" => 0.00,
            "Cochlea" => 0.00,
            "OlfactoryBulb" => 0.0,
            _ => 0.28
        };
    }

    private static bool UsesSubcorticalSizingRatio(string snapshotId)
    {
        return snapshotId switch
        {
            "CorpusCallosum" => true,
            "Thalamus" => true,
            "MotorThalamus" => true,
            "Trn" => true,
            "Pulvinar" => true,
            "MediodorsalThalamus" => true,
            "IntralaminarThalamus" => true,
            "DentateGyrus" => true,
            "CA3" => true,
            "CA2" => true,
            "CA1" => true,
            "Subiculum" => true,
            "Presubiculum" => true,
            "Parasubiculum" => true,
            "Striatum" => true,
            "NucleusAccumbens" => true,
            "GlobusPallidus" => true,
            "VentralPallidum" => true,
            "GPe" => true,
            "GPi" => true,
            "Stn" => true,
            "Snr" => true,
            "Snc" => true,
            "Habenula" => true,
            "SuperiorColliculus" => true,
            "InferiorColliculus" => true,
            "PeriaqueductalGray" => true,
            "ReticularFormation" => true,
            "Retina" => true,
            "Cochlea" => true,
            "CochlearNucleus" => true,
            "SuperiorOlive" => true,
            "VestibularNuclei" => true,
            "NucleusTractusSolitarius" => true,
            "SpinalCordMotor" => true,
            "Hypothalamus" => true,
            "Amygdala" => true,
            "DeepCerebellarNuclei" => true,
            "InferiorOlive" => true,
            "Pons" => true,
            "Medulla" => true,
            "LocusCoeruleus" => true,
            "RapheNuclei" => true,
            "BasalForebrain" => true,
            "Vta" => true,
            "OlfactoryBulb" => true,
            _ => false
        };
    }

    private IEnumerable<HemispherePairing> GetHemispherePathwayPairs(string sourceBase, string targetBase, string projectionType)
    {
        var sourceMidline = !IsBilaterallyDuplicated(sourceBase);
        var targetMidline = !IsBilaterallyDuplicated(targetBase);
        var callosalProjection = projectionType.Contains("callosal", StringComparison.OrdinalIgnoreCase) ||
                                 projectionType.Contains("commissural", StringComparison.OrdinalIgnoreCase);

        if (sourceMidline && targetMidline)
        {
            yield return new HemispherePairing($"M_{sourceBase}", $"M_{targetBase}", "M");
            yield break;
        }

        if (sourceMidline)
        {
            yield return new HemispherePairing($"M_{sourceBase}", $"L_{targetBase}", "L");
            yield return new HemispherePairing($"M_{sourceBase}", $"R_{targetBase}", "R");
            yield break;
        }

        if (targetMidline)
        {
            yield return new HemispherePairing($"L_{sourceBase}", $"M_{targetBase}", "L");
            yield return new HemispherePairing($"R_{sourceBase}", $"M_{targetBase}", "R");
            yield break;
        }

        if (callosalProjection)
        {
            yield return new HemispherePairing($"L_{sourceBase}", $"R_{targetBase}", "LtoR");
            yield return new HemispherePairing($"R_{sourceBase}", $"L_{targetBase}", "RtoL");
            yield break;
        }

        yield return new HemispherePairing($"L_{sourceBase}", $"L_{targetBase}", "L");
        yield return new HemispherePairing($"R_{sourceBase}", $"R_{targetBase}", "R");
    }

    private static bool IsBilaterallyDuplicated(string snapshotId)
    {
        return snapshotId switch
        {
            "CorpusCallosum" => false,
            "ReticularFormation" => false,
            "PeriaqueductalGray" => false,
            "RapheNuclei" => false,
            "CerebellarGranule" => false,
            "CerebellarVermis" => false,
            "CerebellarLobules" => false,
            "PurkinjeCellLayer" => false,
            "Pons" => false,
            "Medulla" => false,
            _ => true
        };
    }

    private static SnapshotPayload ParseSnapshotPayload(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement latest;
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            latest = doc.RootElement;
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            latest = doc.RootElement[doc.RootElement.GetArrayLength() - 1];
        }
        else
        {
            return new SnapshotPayload();
        }

        return ParseSnapshotPayload(latest);
    }

    private static SnapshotPayload ParseSnapshotPayload(JsonElement latest)
    {
        var payload = new SnapshotPayload();
        if (TryGetProperty(latest, "structureStates", out var states) && states.ValueKind == JsonValueKind.Array)
        {
            foreach (var state in states.EnumerateArray())
            {
                var structureId = ParseStructureId(state);
                if (string.IsNullOrWhiteSpace(structureId))
                {
                    continue;
                }

                payload.StructureStates.Add(new StructureTick(
                    structureId,
                    GetSingle(state, "meanFiringRateHz", "mean_firing_rate_hz"),
                    GetInt(state, "spikeOutCount", "spike_out_count"),
                    GetInt(state, "spikeInCount", "spike_in_count"),
                    ParseTopNeuronIds(state),
                    ParseMicrotubuleDiagnostics(state),
                    ParseBodySchemaDiagnostics(state),
                    ParseBasalGangliaDiagnostics(state),
                    ParseCerebellarDiagnostics(state),
                    ParseVestibuloReticularDiagnostics(state),
                    ParseSuperiorColliculusDiagnostics(state),
                    ParseHippocampalSpatialDiagnostics(state),
                    ParseSalienceAffectDiagnostics(state),
                    ParsePrefrontalWorkingMemoryDiagnostics(state),
                    ParseThalamicAttentionGateDiagnostics(state),
                    ParseHypothalamicHomeostasisDiagnostics(state),
                    ParseSleepWakeArousalDiagnostics(state),
                    ParseDescendingDefenseDiagnostics(state),
                    ParseDopamineRewardDiagnostics(state),
                    ParseSeptohippocampalThetaDiagnostics(state),
                    ParseSpinalProprioceptiveDiagnostics(state),
                    ParseOlfactoryLimbicMemoryDiagnostics(state),
                    ParseAuditoryLanguageMotorDiagnostics(state),
                    ParseVisualObjectRecognitionDiagnostics(state)));
            }
        }

        if (TryGetProperty(latest, "activePathways", out var pathways) && pathways.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in pathways.EnumerateArray())
            {
                var source = ParseAnyStructureId(p, "source");
                var target = ParseAnyStructureId(p, "target");
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                payload.Pathways.Add(new PathwayTick(source, target, GetInt(p, "spikeVolume", "spike_volume")));
            }
        }

        return payload;
    }

    private static MicrotubuleTick? ParseMicrotubuleDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "microtubuleDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "microtubule_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new MicrotubuleTick(
            GetString(diagnostics, "mode"),
            GetBool(diagnostics, "enabled"),
            GetBool(diagnostics, "experimental"),
            GetSingle(diagnostics, "meanStability", "mean_stability"),
            GetSingle(diagnostics, "meanSpineInvasionEligibility", "mean_spine_invasion_eligibility"),
            GetSingle(diagnostics, "meanTransportSupport", "mean_transport_support"),
            GetSingle(diagnostics, "meanOpticalCollectiveBias", "mean_optical_collective_bias"),
            GetSingle(diagnostics, "meanRadicalPairSensitivity", "mean_radical_pair_sensitivity"),
            GetSingle(diagnostics, "meanPlasticitySupport", "mean_plasticity_support"),
            GetSingle(diagnostics, "meanTracePersistenceSupport", "mean_trace_persistence_support"),
            GetSingle(diagnostics, "meanIntegrationGain", "mean_integration_gain"),
            GetSingle(diagnostics, "meanConsolidationSupport", "mean_consolidation_support"));
    }

    private static BodySchemaTick? ParseBodySchemaDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "bodySchemaDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "body_schema_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new BodySchemaTick(
            GetString(diagnostics, "dominantBodyZone", "dominant_body_zone"),
            GetString(diagnostics, "dominantSpatialZone", "dominant_spatial_zone"),
            GetSingle(diagnostics, "faceHeadActivation", "face_head_activation"),
            GetSingle(diagnostics, "handArmActivation", "hand_arm_activation"),
            GetSingle(diagnostics, "trunkActivation", "trunk_activation"),
            GetSingle(diagnostics, "legFootActivation", "leg_foot_activation"),
            GetSingle(diagnostics, "nearBodyActivation", "near_body_activation"),
            GetSingle(diagnostics, "leftPeripersonalActivation", "left_peripersonal_activation"),
            GetSingle(diagnostics, "rightPeripersonalActivation", "right_peripersonal_activation"),
            GetSingle(diagnostics, "farSpaceActivation", "far_space_activation"));
    }

    private static BasalGangliaTick? ParseBasalGangliaDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "basalGangliaDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "basal_ganglia_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new BasalGangliaTick(
            GetString(diagnostics, "dominantMode", "dominant_mode"),
            GetSingle(diagnostics, "directPathwayActivation", "direct_pathway_activation"),
            GetSingle(diagnostics, "indirectPathwayActivation", "indirect_pathway_activation"),
            GetSingle(diagnostics, "hyperdirectPathwayActivation", "hyperdirect_pathway_activation"),
            GetSingle(diagnostics, "outputNucleusInhibition", "output_nucleus_inhibition"),
            GetSingle(diagnostics, "thalamicDisinhibition", "thalamic_disinhibition"),
            GetSingle(diagnostics, "dopamineModulation", "dopamine_modulation"),
            GetSingle(diagnostics, "actionSelectionBias", "action_selection_bias"));
    }

    private static CerebellarTick? ParseCerebellarDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "cerebellarDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "cerebellar_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new CerebellarTick(
            GetString(diagnostics, "correctionMode", "correction_mode"),
            GetSingle(diagnostics, "mossyFiberDrive", "mossy_fiber_drive"),
            GetSingle(diagnostics, "climbingFiberError", "climbing_fiber_error"),
            GetSingle(diagnostics, "purkinjeInhibition", "purkinje_inhibition"),
            GetSingle(diagnostics, "deepNucleusOutput", "deep_nucleus_output"),
            GetSingle(diagnostics, "vermisStabilization", "vermis_stabilization"),
            GetSingle(diagnostics, "correctionGain", "correction_gain"),
            GetSingle(diagnostics, "predictionError", "prediction_error"));
    }

    private static VestibuloReticularTick? ParseVestibuloReticularDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "vestibuloReticularDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "vestibulo_reticular_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new VestibuloReticularTick(
            GetString(diagnostics, "postureMode", "posture_mode"),
            GetSingle(diagnostics, "vestibularDrive", "vestibular_drive"),
            GetSingle(diagnostics, "reticularArousal", "reticular_arousal"),
            GetSingle(diagnostics, "vermisBalanceCorrection", "vermis_balance_correction"),
            GetSingle(diagnostics, "spinalMotorTone", "spinal_motor_tone"),
            GetSingle(diagnostics, "postureStability", "posture_stability"),
            GetSingle(diagnostics, "balanceError", "balance_error"));
    }

    private static SuperiorColliculusTick? ParseSuperiorColliculusDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "superiorColliculusDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "superior_colliculus_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new SuperiorColliculusTick(
            GetString(diagnostics, "orientingMode", "orienting_mode"),
            GetSingle(diagnostics, "visualOrientingDrive", "visual_orienting_drive"),
            GetSingle(diagnostics, "auditoryOrientingDrive", "auditory_orienting_drive"),
            GetSingle(diagnostics, "nigrotectalInhibition", "nigrotectal_inhibition"),
            GetSingle(diagnostics, "pulvinarAttention", "pulvinar_attention"),
            GetSingle(diagnostics, "headEyeCommand", "head_eye_command"),
            GetSingle(diagnostics, "saccadeReadiness", "saccade_readiness"),
            GetSingle(diagnostics, "salienceBias", "salience_bias"));
    }

    private static HippocampalSpatialTick? ParseHippocampalSpatialDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "hippocampalSpatialDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "hippocampal_spatial_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new HippocampalSpatialTick(
            GetString(diagnostics, "memoryMode", "memory_mode"),
            GetSingle(diagnostics, "entorhinalGridDrive", "entorhinal_grid_drive"),
            GetSingle(diagnostics, "dentatePatternSeparation", "dentate_pattern_separation"),
            GetSingle(diagnostics, "ca3PatternCompletion", "ca3_pattern_completion"),
            GetSingle(diagnostics, "ca1PlaceIndex", "ca1_place_index"),
            GetSingle(diagnostics, "subicularOutput", "subicular_output"),
            GetSingle(diagnostics, "headDirectionAlignment", "head_direction_alignment"),
            GetSingle(diagnostics, "spatialCoherence", "spatial_coherence"),
            GetSingle(diagnostics, "noveltyMismatch", "novelty_mismatch"));
    }

    private static SalienceAffectTick? ParseSalienceAffectDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "salienceAffectDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "salience_affect_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new SalienceAffectTick(
            GetString(diagnostics, "salienceMode", "salience_mode"),
            GetSingle(diagnostics, "threatSalience", "threat_salience"),
            GetSingle(diagnostics, "interoceptiveDrive", "interoceptive_drive"),
            GetSingle(diagnostics, "conflictMonitoring", "conflict_monitoring"),
            GetSingle(diagnostics, "autonomicArousal", "autonomic_arousal"),
            GetSingle(diagnostics, "attentionGain", "attention_gain"),
            GetSingle(diagnostics, "defensiveReadiness", "defensive_readiness"),
            GetSingle(diagnostics, "controlBias", "control_bias"),
            GetSingle(diagnostics, "affectIntensity", "affect_intensity"));
    }

    private static PrefrontalWorkingMemoryTick? ParsePrefrontalWorkingMemoryDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "prefrontalWorkingMemoryDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "prefrontal_working_memory_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PrefrontalWorkingMemoryTick(
            GetString(diagnostics, "controlMode", "control_mode"),
            GetSingle(diagnostics, "pfcPersistentActivity", "pfc_persistent_activity"),
            GetSingle(diagnostics, "mediodorsalThalamicSupport", "mediodorsal_thalamic_support"),
            GetSingle(diagnostics, "frontoparietalContext", "frontoparietal_context"),
            GetSingle(diagnostics, "semanticContext", "semantic_context"),
            GetSingle(diagnostics, "striatalGate", "striatal_gate"),
            GetSingle(diagnostics, "accControlDemand", "acc_control_demand"),
            GetSingle(diagnostics, "topDownBias", "top_down_bias"),
            GetSingle(diagnostics, "taskSetStability", "task_set_stability"));
    }

    private static ThalamicAttentionGateTick? ParseThalamicAttentionGateDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "thalamicAttentionGateDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "thalamic_attention_gate_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ThalamicAttentionGateTick(
            GetString(diagnostics, "gateMode", "gate_mode"),
            GetSingle(diagnostics, "thalamocorticalRelay", "thalamocortical_relay"),
            GetSingle(diagnostics, "trnInhibitoryGate", "trn_inhibitory_gate"),
            GetSingle(diagnostics, "pulvinarSpotlight", "pulvinar_spotlight"),
            GetSingle(diagnostics, "mediodorsalAccess", "mediodorsal_access"),
            GetSingle(diagnostics, "intralaminarBroadcast", "intralaminar_broadcast"),
            GetSingle(diagnostics, "sensoryGain", "sensory_gain"),
            GetSingle(diagnostics, "corticalAccess", "cortical_access"),
            GetSingle(diagnostics, "relaySelectionBias", "relay_selection_bias"));
    }

    private static HypothalamicHomeostasisTick? ParseHypothalamicHomeostasisDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "hypothalamicHomeostasisDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "hypothalamic_homeostasis_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new HypothalamicHomeostasisTick(
            GetString(diagnostics, "homeostasisMode", "homeostasis_mode"),
            GetSingle(diagnostics, "visceralAfferentDrive", "visceral_afferent_drive"),
            GetSingle(diagnostics, "hypothalamicSetpointError", "hypothalamic_setpoint_error"),
            GetSingle(diagnostics, "insulaBodyFeeling", "insula_body_feeling"),
            GetSingle(diagnostics, "limbicHomeostaticPressure", "limbic_homeostatic_pressure"),
            GetSingle(diagnostics, "autonomicBrainstemDrive", "autonomic_brainstem_drive"),
            GetSingle(diagnostics, "arousalPressure", "arousal_pressure"),
            GetSingle(diagnostics, "comfortDeficit", "comfort_deficit"),
            GetSingle(diagnostics, "defensiveBodyCommand", "defensive_body_command"));
    }

    private static SleepWakeArousalTick? ParseSleepWakeArousalDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "sleepWakeArousalDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "sleep_wake_arousal_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new SleepWakeArousalTick(
            GetString(diagnostics, "arousalMode", "arousal_mode"),
            GetSingle(diagnostics, "hypothalamicSleepPressure", "hypothalamic_sleep_pressure"),
            GetSingle(diagnostics, "reticularActivatingDrive", "reticular_activating_drive"),
            GetSingle(diagnostics, "pontomedullaryStateTone", "pontomedullary_state_tone"),
            GetSingle(diagnostics, "locusCoeruleusWakeTone", "locus_coeruleus_wake_tone"),
            GetSingle(diagnostics, "rapheStabilizationTone", "raphe_stabilization_tone"),
            GetSingle(diagnostics, "basalForebrainWakeDrive", "basal_forebrain_wake_drive"),
            GetSingle(diagnostics, "intralaminarArousalBroadcast", "intralaminar_arousal_broadcast"),
            GetSingle(diagnostics, "corticalReadiness", "cortical_readiness"));
    }

    private static DescendingDefenseTick? ParseDescendingDefenseDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "descendingDefenseDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "descending_defense_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new DescendingDefenseTick(
            GetString(diagnostics, "defenseMode", "defense_mode"),
            GetSingle(diagnostics, "amygdalaThreatDrive", "amygdala_threat_drive"),
            GetSingle(diagnostics, "hypothalamicDefenseDrive", "hypothalamic_defense_drive"),
            GetSingle(diagnostics, "pagDefensiveCommand", "pag_defensive_command"),
            GetSingle(diagnostics, "raphePainModulation", "raphe_pain_modulation"),
            GetSingle(diagnostics, "medullaryAutonomicSupport", "medullary_autonomic_support"),
            GetSingle(diagnostics, "reticularPatternRelease", "reticular_pattern_release"),
            GetSingle(diagnostics, "spinalWithdrawalDrive", "spinal_withdrawal_drive"),
            GetSingle(diagnostics, "protectionReadiness", "protection_readiness"));
    }

    private static DopamineRewardTick? ParseDopamineRewardDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "dopamineRewardDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "dopamine_reward_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new DopamineRewardTick(
            GetString(diagnostics, "rewardMode", "reward_mode"),
            GetSingle(diagnostics, "vtaPhasicDopamine", "vta_phasic_dopamine"),
            GetSingle(diagnostics, "sncActionTeaching", "snc_action_teaching"),
            GetSingle(diagnostics, "nucleusAccumbensIncentive", "nucleus_accumbens_incentive"),
            GetSingle(diagnostics, "striatalActionValue", "striatal_action_value"),
            GetSingle(diagnostics, "habenulaNegativePrediction", "habenula_negative_prediction"),
            GetSingle(diagnostics, "orbitofrontalExpectedValue", "orbitofrontal_expected_value"),
            GetSingle(diagnostics, "pfcGoalBias", "pfc_goal_bias"),
            GetSingle(diagnostics, "rewardPredictionError", "reward_prediction_error"),
            GetSingle(diagnostics, "learningReadiness", "learning_readiness"));
    }

    private static SeptohippocampalThetaTick? ParseSeptohippocampalThetaDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "septohippocampalThetaDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "septohippocampal_theta_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new SeptohippocampalThetaTick(
            GetString(diagnostics, "thetaMode", "theta_mode"),
            GetSingle(diagnostics, "septalThetaDrive", "septal_theta_drive"),
            GetSingle(diagnostics, "entorhinalGridPhase", "entorhinal_grid_phase"),
            GetSingle(diagnostics, "dentateEncodingGate", "dentate_encoding_gate"),
            GetSingle(diagnostics, "ca3SequenceReplay", "ca3_sequence_replay"),
            GetSingle(diagnostics, "ca1PlaceTiming", "ca1_place_timing"),
            GetSingle(diagnostics, "subicularNavigationOutput", "subicular_navigation_output"),
            GetSingle(diagnostics, "headDirectionAlignment", "head_direction_alignment"),
            GetSingle(diagnostics, "retrosplenialSceneAnchor", "retrosplenial_scene_anchor"),
            GetSingle(diagnostics, "vestibularPathIntegration", "vestibular_path_integration"),
            GetSingle(diagnostics, "thetaCoherence", "theta_coherence"));
    }

    private static SpinalProprioceptiveTick? ParseSpinalProprioceptiveDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "spinalProprioceptiveDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "spinal_proprioceptive_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new SpinalProprioceptiveTick(
            GetString(diagnostics, "reflexMode", "reflex_mode"),
            GetSingle(diagnostics, "spinalReflexDrive", "spinal_reflex_drive"),
            GetSingle(diagnostics, "s1ProprioceptiveMap", "s1_proprioceptive_map"),
            GetSingle(diagnostics, "m1DescendingCommand", "m1_descending_command"),
            GetSingle(diagnostics, "cerebellarMossyFeedback", "cerebellar_mossy_feedback"),
            GetSingle(diagnostics, "vestibularBalanceInput", "vestibular_balance_input"),
            GetSingle(diagnostics, "reticularPosturalSet", "reticular_postural_set"),
            GetSingle(diagnostics, "thalamicRelayTone", "thalamic_relay_tone"),
            GetSingle(diagnostics, "reflexReadiness", "reflex_readiness"),
            GetSingle(diagnostics, "proprioceptiveCoherence", "proprioceptive_coherence"));
    }

    private static OlfactoryLimbicMemoryTick? ParseOlfactoryLimbicMemoryDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "olfactoryLimbicMemoryDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "olfactory_limbic_memory_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new OlfactoryLimbicMemoryTick(
            GetString(diagnostics, "memoryMode", "memory_mode"),
            GetSingle(diagnostics, "olfactoryCueDrive", "olfactory_cue_drive"),
            GetSingle(diagnostics, "temporalPiriformAssociation", "temporal_piriform_association"),
            GetSingle(diagnostics, "amygdalaAffectiveTag", "amygdala_affective_tag"),
            GetSingle(diagnostics, "entorhinalMemoryGate", "entorhinal_memory_gate"),
            GetSingle(diagnostics, "hippocampalEpisodeIndex", "hippocampal_episode_index"),
            GetSingle(diagnostics, "orbitofrontalValenceContext", "orbitofrontal_valence_context"),
            GetSingle(diagnostics, "pfcAutobiographicalControl", "pfc_autobiographical_control"),
            GetSingle(diagnostics, "familiaritySignal", "familiarity_signal"),
            GetSingle(diagnostics, "autobiographicalCoherence", "autobiographical_coherence"));
    }

    private static AuditoryLanguageMotorTick? ParseAuditoryLanguageMotorDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "auditoryLanguageMotorDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "auditory_language_motor_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AuditoryLanguageMotorTick(
            GetString(diagnostics, "languageMode", "language_mode"),
            GetSingle(diagnostics, "a1AuditoryDrive", "a1_auditory_drive"),
            GetSingle(diagnostics, "wernickeComprehension", "wernicke_comprehension"),
            GetSingle(diagnostics, "arcuatePhonologicalRelay", "arcuate_phonological_relay"),
            GetSingle(diagnostics, "brocaSpeechSequence", "broca_speech_sequence"),
            GetSingle(diagnostics, "premotorArticulationPlan", "premotor_articulation_plan"),
            GetSingle(diagnostics, "m1SpeechMotorCommand", "m1_speech_motor_command"),
            GetSingle(diagnostics, "basalGangliaSpeechGate", "basal_ganglia_speech_gate"),
            GetSingle(diagnostics, "motorThalamicRelay", "motor_thalamic_relay"),
            GetSingle(diagnostics, "languageMotorCoherence", "language_motor_coherence"));
    }

    private static VisualObjectRecognitionTick? ParseVisualObjectRecognitionDiagnostics(JsonElement state)
    {
        if (!TryGetProperty(state, "visualObjectRecognitionDiagnostics", out var diagnostics) &&
            !TryGetProperty(state, "visual_object_recognition_diagnostics", out diagnostics))
        {
            return null;
        }

        if (diagnostics.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new VisualObjectRecognitionTick(
            GetString(diagnostics, "recognitionMode", "recognition_mode"),
            GetSingle(diagnostics, "v1EdgeDrive", "v1_edge_drive"),
            GetSingle(diagnostics, "v2ContourIntegration", "v2_contour_integration"),
            GetSingle(diagnostics, "v4ObjectFeatureBinding", "v4_object_feature_binding"),
            GetSingle(diagnostics, "mtMotionCue", "mt_motion_cue"),
            GetSingle(diagnostics, "temporalObjectIdentity", "temporal_object_identity"),
            GetSingle(diagnostics, "perirhinalFamiliarity", "perirhinal_familiarity"),
            GetSingle(diagnostics, "pulvinarVisualAttention", "pulvinar_visual_attention"),
            GetSingle(diagnostics, "thalamicRelayGain", "thalamic_relay_gain"),
            GetSingle(diagnostics, "pfcObjectContext", "pfc_object_context"),
            GetSingle(diagnostics, "objectRecognitionCoherence", "object_recognition_coherence"));
    }

    private static Point3D RotateLocalPoint(Point3D p, double pitchDeg, double yawDeg, double rollDeg)
    {
        var pitch = DegreesToRadians(pitchDeg);
        var yaw = DegreesToRadians(yawDeg);
        var roll = DegreesToRadians(rollDeg);

        // X (pitch)
        var x1 = p.X;
        var y1 = (p.Y * Math.Cos(pitch)) - (p.Z * Math.Sin(pitch));
        var z1 = (p.Y * Math.Sin(pitch)) + (p.Z * Math.Cos(pitch));

        // Y (yaw)
        var x2 = (x1 * Math.Cos(yaw)) + (z1 * Math.Sin(yaw));
        var y2 = y1;
        var z2 = (-x1 * Math.Sin(yaw)) + (z1 * Math.Cos(yaw));

        // Z (roll)
        var x3 = (x2 * Math.Cos(roll)) - (y2 * Math.Sin(roll));
        var y3 = (x2 * Math.Sin(roll)) + (y2 * Math.Cos(roll));

        return new Point3D(x3, y3, z2);
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);

    private int GetTargetNeuronCountPerHemisphere(string structureId)
    {
        const int corticalEngineMinimum = 384;
        const int nonCorticalEngineMinimum = 112;
        const double fallbackMillions = 8.0;
        var counts = GetAdjustedDisplayNeuronWeightsMillions();
        var knownTotal = counts.Values.Sum();
        var missingCount = Math.Max(0, GetStructureDefinitions().Count() - counts.Count);
        var total = knownTotal + (fallbackMillions * missingCount);
        var estimate = counts.TryGetValue(structureId, out var specific) ? specific : fallbackMillions;
        var count = (int)Math.Round((estimate / total) * _displayNeuronsPerHemisphereBudget);
        if (IsCorticalSnapshotId(structureId))
        {
            count = (int)Math.Round(count * 2.05);
            var corticalMinimum = Math.Max(corticalEngineMinimum, _displayNeuronGridEdge * 4);
            return Math.Max(corticalMinimum, count);
        }

        if (IsCerebellarSnapshotId(structureId))
        {
            count = (int)Math.Round(count * 0.62);
            return Math.Max(80, count);
        }

        if (string.Equals(structureId, "OlfactoryBulb", StringComparison.OrdinalIgnoreCase))
        {
            count = (int)Math.Round(count * 0.35);
            return Math.Max(72, count);
        }

        if (structureId is "Retina" or "Cochlea" or "CochlearNucleus" or "SuperiorOlive" or "InferiorColliculus" or "VestibularNuclei" or "NucleusTractusSolitarius" or "PeriaqueductalGray")
        {
            count = (int)Math.Round(count * 0.55);
            return Math.Max(64, count);
        }

        // Keep deep nuclei readable but avoid over-dense non-cortical blocks.
        count = (int)Math.Round(count * 0.80);
        return Math.Max(nonCorticalEngineMinimum, count);
    }

    private static bool IsCorticalSnapshotId(string structureId)
    {
        return structureId switch
        {
            "V1" => true,
            "V2" => true,
            "V3" => true,
            "V4" => true,
            "Mt" => true,
            "A1" => true,
            "AuditoryAssociationCortex" => true,
            "S1" => true,
            "SecondarySomatosensoryCortex" => true,
            "Pfc" => true,
            "DorsomedialPrefrontalCortex" => true,
            "VentromedialPrefrontalCortex" => true,
            "FrontalEyeFields" => true,
            "BrocaBa44Ba45" => true,
            "WernickePstgPsts" => true,
            "SupramarginalAngular" => true,
            "PremotorCortex" => true,
            "OrbitofrontalCortex" => true,
            "Insula" => true,
            "Ppc" => true,
            "TemporalAssociation" => true,
            "InferotemporalCortex" => true,
            "FusiformGyrus" => true,
            "TemporalPole" => true,
            "TemporoparietalJunction" => true,
            "Precuneus" => true,
            "MidcingulateCortex" => true,
            "ParahippocampalCortex" => true,
            "PerirhinalCortex" => true,
            "PosteriorCingulate" => true,
            "RetrosplenialCortex" => true,
            "Acc" => true,
            "M1" => true,
            "Sma" => true,
            "EntorhinalCortex" => true,
            _ => false
        };
    }

    private static bool IsCerebellarSnapshotId(string structureId)
    {
        return structureId switch
        {
            "CerebellarGranule" => true,
            "PurkinjeCellLayer" => true,
            "DeepCerebellarNuclei" => true,
            "CerebellarVermis" => true,
            "CerebellarLobules" => true,
            _ => false
        };
    }

    private static StructureLayout GetEffectiveStructureLayout(string snapshotId, StructureLayout configuredLayout)
    {
        if (IsCorticalSnapshotId(snapshotId))
        {
            return StructureLayout.CorticalSheet;
        }

        return snapshotId switch
        {
            "DentateGyrus" or "CA3" or "CA2" or "CA1" or "Subiculum" or "Presubiculum" or "Parasubiculum" =>
                StructureLayout.HippocampalArc,
            "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarVermis" or "CerebellarLobules" =>
                StructureLayout.CerebellarSheet,
            "Pons" or "Medulla" or "InferiorOlive" or "LocusCoeruleus" or "RapheNuclei" or "Vta" or
            "ReticularFormation" or "SpinalCordMotor" =>
                StructureLayout.BrainstemColumn,
            "OlfactoryBulb" =>
                StructureLayout.OlfactoryBulbShell,
            _ => configuredLayout == StructureLayout.CorticalSheet ? StructureLayout.NucleusBlock : configuredLayout
        };
    }

    private static Dictionary<string, double> GetAdjustedDisplayNeuronWeightsMillions()
    {
        var counts = GetBiologicalNeuronCountEstimatesMillions();
        var total = counts.Values.Sum();

        var cerebellarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CerebellarGranule",
            "PurkinjeCellLayer",
            "DeepCerebellarNuclei",
            "CerebellarVermis",
            "CerebellarLobules"
        };

        var cerebellarTotal = counts.Where(kv => cerebellarIds.Contains(kv.Key)).Sum(kv => kv.Value);
        // Keep cerebellum visually present, but free additional point budget for cortical shell continuity.
        var cerebellarCap = total * 0.14;
        if (cerebellarTotal <= 0.0 || cerebellarTotal <= cerebellarCap)
        {
            return counts;
        }

        var scale = cerebellarCap / cerebellarTotal;
        foreach (var id in cerebellarIds)
        {
            if (counts.TryGetValue(id, out var v))
            {
                counts[id] = v * scale;
            }
        }

        var removed = cerebellarTotal - cerebellarCap;
        var recipientIds = counts.Keys.Where(id => !cerebellarIds.Contains(id)).ToArray();
        var recipientTotal = recipientIds.Sum(id => counts[id]);
        if (recipientTotal > 0.0)
        {
            foreach (var id in recipientIds)
            {
                var v = counts[id];
                counts[id] = v + (removed * (v / recipientTotal));
            }
        }

        // Reallocate a controlled slice from deep/subcortical pools to cortical mantle
        // so cortical sectors have enough density to read as contiguous fitted lobes.
        var corticalRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "V1", "V2", "V4", "Mt", "A1", "S1", "Pfc", "BrocaBa44Ba45", "WernickePstgPsts", "SupramarginalAngular", "PremotorCortex", "OrbitofrontalCortex", "Insula", "Ppc", "TemporalAssociation", "ParahippocampalCortex", "PerirhinalCortex", "PosteriorCingulate", "RetrosplenialCortex", "Acc", "M1", "Sma", "EntorhinalCortex"
        };

        var subcorticalDonors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CorpusCallosum",
            "Thalamus", "MotorThalamus", "Trn", "Pulvinar", "MediodorsalThalamus", "IntralaminarThalamus",
            "DentateGyrus", "CA3", "CA2", "CA1", "Subiculum", "Presubiculum", "Parasubiculum",
            "Striatum", "NucleusAccumbens", "GlobusPallidus", "VentralPallidum", "GPe", "GPi", "Stn", "Snr", "Snc", "Habenula", "SuperiorColliculus", "Amygdala", "Hypothalamus",
            "InferiorOlive", "Pons", "Medulla",
            "LocusCoeruleus", "RapheNuclei", "BasalForebrain", "Vta", "OlfactoryBulb",
            "Retina", "Cochlea", "CochlearNucleus", "SuperiorOlive", "InferiorColliculus", "VestibularNuclei", "NucleusTractusSolitarius", "ReticularFormation", "PeriaqueductalGray", "SpinalCordMotor"
        };

        var donorTotal = counts.Where(kv => subcorticalDonors.Contains(kv.Key)).Sum(kv => kv.Value);
        var recipientMantleTotal = counts.Where(kv => corticalRecipients.Contains(kv.Key)).Sum(kv => kv.Value);
        const double transferFraction = 0.46;
        var transfer = donorTotal * transferFraction;
        if (donorTotal > 0.0 && recipientMantleTotal > 0.0 && transfer > 0.0)
        {
            foreach (var id in subcorticalDonors)
            {
                if (counts.TryGetValue(id, out var v))
                {
                    counts[id] = v * (1.0 - transferFraction);
                }
            }

            foreach (var id in corticalRecipients)
            {
                if (counts.TryGetValue(id, out var v))
                {
                    counts[id] = v + (transfer * (v / recipientMantleTotal));
                }
            }
        }

        return counts;
    }

    private static ulong DeterministicPointScore(Point3D point, int index, string seed)
    {
        unchecked
        {
            ulong h = 1469598103934665603UL;
            h ^= (ulong)index; h *= 1099511628211UL;
            h ^= (ulong)(int)Math.Round(point.X * 10000.0); h *= 1099511628211UL;
            h ^= (ulong)(int)Math.Round(point.Y * 10000.0); h *= 1099511628211UL;
            h ^= (ulong)(int)Math.Round(point.Z * 10000.0); h *= 1099511628211UL;
            foreach (var c in seed)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }
    }

    private static Dictionary<string, double> GetBiologicalNeuronCountEstimatesMillions()
    {
        // Rough order-of-magnitude neuron estimates (millions) for proportional visualization.
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["CerebellarGranule"] = 28000,
            ["PurkinjeCellLayer"] = 15,
            ["CerebellarVermis"] = 3,
            ["CerebellarLobules"] = 160,
            ["V1"] = 220,
            ["V2"] = 200,
            ["V3"] = 145,
            ["V4"] = 120,
            ["Mt"] = 95,
            ["A1"] = 80,
            ["AuditoryAssociationCortex"] = 105,
            ["S1"] = 180,
            ["SecondarySomatosensoryCortex"] = 75,
            ["Pfc"] = 320,
            ["DorsomedialPrefrontalCortex"] = 95,
            ["VentromedialPrefrontalCortex"] = 90,
            ["FrontalEyeFields"] = 55,
            ["BrocaBa44Ba45"] = 90,
            ["WernickePstgPsts"] = 95,
            ["SupramarginalAngular"] = 110,
            ["ArcuateFasciculus"] = 18,
            ["PremotorCortex"] = 160,
            ["OrbitofrontalCortex"] = 140,
            ["Insula"] = 30,
            ["Ppc"] = 170,
            ["TemporalAssociation"] = 210,
            ["InferotemporalCortex"] = 145,
            ["FusiformGyrus"] = 90,
            ["TemporalPole"] = 70,
            ["TemporoparietalJunction"] = 80,
            ["Precuneus"] = 110,
            ["MidcingulateCortex"] = 65,
            ["ParahippocampalCortex"] = 90,
            ["PerirhinalCortex"] = 85,
            ["PosteriorCingulate"] = 70,
            ["RetrosplenialCortex"] = 65,
            ["M1"] = 90,
            ["Sma"] = 50,
            ["CorpusCallosum"] = 12,
            ["Striatum"] = 450,
            ["NucleusAccumbens"] = 90,
            ["Thalamus"] = 90,
            ["MotorThalamus"] = 18,
            ["Pulvinar"] = 10,
            ["MediodorsalThalamus"] = 8,
            ["IntralaminarThalamus"] = 6,
            ["Amygdala"] = 35,
            ["Hypothalamus"] = 8,
            ["Acc"] = 70,
            ["DentateGyrus"] = 45,
            ["CA3"] = 20,
            ["CA2"] = 8,
            ["CA1"] = 35,
            ["EntorhinalCortex"] = 25,
            ["Subiculum"] = 12,
            ["Presubiculum"] = 8,
            ["Parasubiculum"] = 8,
            ["OlfactoryBulb"] = 10,
            ["Retina"] = 2.4,
            ["Cochlea"] = 0.2,
            ["CochlearNucleus"] = 0.5,
            ["SuperiorOlive"] = 0.3,
            ["InferiorColliculus"] = 0.9,
            ["VestibularNuclei"] = 0.4,
            ["NucleusTractusSolitarius"] = 0.3,
            ["Trn"] = 6,
            ["GlobusPallidus"] = 8,
            ["GPe"] = 5,
            ["GPi"] = 4,
            ["VentralPallidum"] = 3.5,
            ["Habenula"] = 1.4,
            ["SuperiorColliculus"] = 4.5,
            ["Stn"] = 3,
            ["Snr"] = 2,
            ["Snc"] = 1.2,
            ["DeepCerebellarNuclei"] = 2,
            ["InferiorOlive"] = 1.5,
            ["ReticularFormation"] = 1.2,
            ["PeriaqueductalGray"] = 0.35,
            ["Pons"] = 18,
            ["Medulla"] = 16,
            ["SpinalCordMotor"] = 1.5,
            ["LocusCoeruleus"] = 0.15,
            ["RapheNuclei"] = 0.4,
            ["BasalForebrain"] = 1.0,
            ["Vta"] = 0.25
        };
    }

    private const double AtlasMmToRender = 0.024;

    private static Point3D MmToRender(Point3D mm) =>
        new(mm.X * AtlasMmToRender, mm.Y * AtlasMmToRender, mm.Z * AtlasMmToRender);

    private static double MmToRender(double mm) => mm * AtlasMmToRender;

    private IEnumerable<StructureDefinition> GetStructureDefinitions() => new[]
    {
        // Cortical entries are initial layout definitions. Non-cortical centres,
        // extents, and orientations are replaced by per-hemisphere atlas profiles.
        new StructureDefinition("V1","V1",MmToRender(new Point3D(18,24,-62)),Color.FromRgb(128,168,248),"Izhikevich","BCM",StructureLayout.CorticalSheet,12,6,4,MmToRender(26),MmToRender(14),MmToRender(12),10,-25,4),
        new StructureDefinition("V2","V2",MmToRender(new Point3D(26,26,-54)),Color.FromRgb(120,176,244),"Izhikevich","BCM+STDP",StructureLayout.CorticalSheet,11,6,4,MmToRender(24),MmToRender(13),MmToRender(11),8,-18,4),
        new StructureDefinition("V3","V3",MmToRender(new Point3D(34,24,-48)),Color.FromRgb(118,184,238),"Izhikevich","BCM+STDP",StructureLayout.CorticalSheet,10,6,4,MmToRender(22),MmToRender(12),MmToRender(10),7,-13,3),
        new StructureDefinition("V4","V4",MmToRender(new Point3D(44,18,-42)),Color.FromRgb(128,188,242),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,10,6,4,MmToRender(22),MmToRender(12),MmToRender(10),6,-8,2),
        new StructureDefinition("MT","Mt",MmToRender(new Point3D(52,12,-34)),Color.FromRgb(112,198,236),"Izhikevich","STDP",StructureLayout.CorticalSheet,10,6,4,MmToRender(22),MmToRender(12),MmToRender(10),6,30,-6),
        new StructureDefinition("A1","A1",MmToRender(new Point3D(50,12,-20)),Color.FromRgb(126,192,238),"Izhikevich","STDP",StructureLayout.CorticalSheet,10,6,4,MmToRender(24),MmToRender(14),MmToRender(12),6,-40,-8),
        new StructureDefinition("Auditory Association","AuditoryAssociationCortex",MmToRender(new Point3D(56,8,-12)),Color.FromRgb(106,198,224),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,10,6,4,MmToRender(24),MmToRender(12),MmToRender(10),4,-34,-7),
        new StructureDefinition("S1","S1",MmToRender(new Point3D(34,42,-2)),Color.FromRgb(120,212,210),"LIF","STDP",StructureLayout.CorticalSheet,11,6,4,MmToRender(26),MmToRender(14),MmToRender(12),-6,-10,5),
        new StructureDefinition("S2","SecondarySomatosensoryCortex",MmToRender(new Point3D(52,16,-6)),Color.FromRgb(96,204,176),"LIF","STDP",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(11),MmToRender(9),-3,6,-5),
        new StructureDefinition("Retina","Retina",MmToRender(new Point3D(72,8,52)),Color.FromRgb(238,154,126),"HH","BCM",StructureLayout.NucleusBlock,7,6,6,MmToRender(8),MmToRender(6),MmToRender(6),0,0,0),
        new StructureDefinition("Cochlea","Cochlea",MmToRender(new Point3D(60,-22,8)),Color.FromRgb(234,176,122),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(9),MmToRender(7),MmToRender(7),0,0,0),
        new StructureDefinition("Olfactory Bulb","OlfactoryBulb",MmToRender(new Point3D(6,20,14)),Color.FromRgb(240,170,122),"Izhikevich","STDP",StructureLayout.OlfactoryBulbShell,10,9,9,MmToRender(10),MmToRender(8),MmToRender(9),0,0,0),
        new StructureDefinition("Corpus Callosum","CorpusCallosum",MmToRender(new Point3D(0,31.5,-4)),Color.FromRgb(204,224,246),"LIF","STDP",StructureLayout.NucleusBlock,24,5,28,MmToRender(54),MmToRender(6),MmToRender(48),0,0,0),

            new StructureDefinition("Thalamus","Thalamus",MmToRender(new Point3D(8,4,-8)),Color.FromRgb(244,186,128),"Izhikevich","STDP",StructureLayout.NucleusBlock,10,8,8,MmToRender(18),MmToRender(14),MmToRender(14),0,0,0),
            new StructureDefinition("Motor Thalamus","MotorThalamus",MmToRender(new Point3D(10,8,-10)),Color.FromRgb(238,178,122),"Izhikevich","STDP",StructureLayout.NucleusBlock,8,6,6,MmToRender(14),MmToRender(10),MmToRender(10),0,8,0),
            new StructureDefinition("TRN","Trn",MmToRender(new Point3D(10,6,-8)),Color.FromRgb(198,142,205),"LIF","STDP",StructureLayout.NucleusBlock,7,5,6,MmToRender(12),MmToRender(8),MmToRender(10),0,25,0),
            new StructureDefinition("Pulvinar","Pulvinar",MmToRender(new Point3D(12,10,-24)),Color.FromRgb(232,178,144),"Izhikevich","STDP",StructureLayout.NucleusBlock,8,6,6,MmToRender(14),MmToRender(10),MmToRender(10),0,8,0),
            new StructureDefinition("Mediodorsal Thalamus","MediodorsalThalamus",MmToRender(new Point3D(8,12,2)),Color.FromRgb(236,170,132),"Izhikevich","STDP",StructureLayout.NucleusBlock,8,6,6,MmToRender(14),MmToRender(10),MmToRender(10),0,12,0),
            new StructureDefinition("Intralaminar Thalamus","IntralaminarThalamus",MmToRender(new Point3D(4,6,-2)),Color.FromRgb(228,162,120),"Izhikevich","STDP",StructureLayout.NucleusBlock,8,6,6,MmToRender(12),MmToRender(10),MmToRender(10),0,0,0),
            new StructureDefinition("Superior Colliculus","SuperiorColliculus",MmToRender(new Point3D(10,18,-22)),Color.FromRgb(236,186,118),"Izhikevich","STDP",StructureLayout.NucleusBlock,7,5,5,MmToRender(11),MmToRender(8),MmToRender(8),0,10,0),
            new StructureDefinition("Inferior Colliculus","InferiorColliculus",MmToRender(new Point3D(10,12,-26)),Color.FromRgb(228,178,112),"Izhikevich","STDP",StructureLayout.NucleusBlock,7,5,5,MmToRender(11),MmToRender(8),MmToRender(8),0,10,0),
            new StructureDefinition("Periaqueductal Gray","PeriaqueductalGray",MmToRender(new Point3D(0,8,-18)),Color.FromRgb(224,162,138),"Izhikevich","DopamineModulatedSTDP",StructureLayout.NucleusBlock,8,6,6,MmToRender(10),MmToRender(8),MmToRender(8),0,0,0),

            new StructureDefinition("EC","EntorhinalCortex",MmToRender(new Point3D(22,-4,-16)),Color.FromRgb(232,196,122),"Izhikevich","STDP",StructureLayout.CorticalSheet,9,5,4,MmToRender(18),MmToRender(10),MmToRender(12),4,35,2),
            new StructureDefinition("DG","DentateGyrus",MmToRender(new Point3D(18,-2,-14)),Color.FromRgb(201,226,138),"LIF","MossyFiberLTP",StructureLayout.HippocampalArc,10,4,3,MmToRender(14),MmToRender(8),MmToRender(10),0,42,0),
            new StructureDefinition("CA3","CA3",MmToRender(new Point3D(19,-1,-12)),Color.FromRgb(159,236,144),"Izhikevich","MossyFiberLTP",StructureLayout.HippocampalArc,10,4,3,MmToRender(13),MmToRender(8),MmToRender(9),0,34,0),
            new StructureDefinition("CA2","CA2",MmToRender(new Point3D(20,-1,-11)),Color.FromRgb(170,236,162),"Izhikevich","MossyFiberLTP",StructureLayout.HippocampalArc,8,4,3,MmToRender(11),MmToRender(8),MmToRender(8),0,28,0),
            new StructureDefinition("CA1","CA1",MmToRender(new Point3D(20,0,-10)),Color.FromRgb(138,224,194),"Izhikevich","SynapticTaggingCapture",StructureLayout.HippocampalArc,10,4,3,MmToRender(13),MmToRender(8),MmToRender(9),0,22,0),
            new StructureDefinition("Subiculum","Subiculum",MmToRender(new Point3D(21,-1,-8)),Color.FromRgb(108,196,222),"LIF","STDP",StructureLayout.HippocampalArc,8,4,3,MmToRender(10),MmToRender(7),MmToRender(8),0,18,0),
            new StructureDefinition("Presubiculum","Presubiculum",MmToRender(new Point3D(22,0,-7)),Color.FromRgb(126,206,232),"LIF","SynapticTaggingCapture",StructureLayout.HippocampalArc,8,4,3,MmToRender(10),MmToRender(7),MmToRender(8),0,14,0),
            new StructureDefinition("Parasubiculum","Parasubiculum",MmToRender(new Point3D(24,-1,-6)),Color.FromRgb(118,196,240),"LIF","SynapticTaggingCapture",StructureLayout.HippocampalArc,8,4,3,MmToRender(10),MmToRender(7),MmToRender(8),0,10,0),
            new StructureDefinition("Parahippocampal Cortex","ParahippocampalCortex",MmToRender(new Point3D(30,-2,-20)),Color.FromRgb(120,210,224),"Izhikevich","SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(11),MmToRender(10),4,42,-4),
            new StructureDefinition("Perirhinal Cortex","PerirhinalCortex",MmToRender(new Point3D(38,2,-18)),Color.FromRgb(130,202,230),"Izhikevich","SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(18),MmToRender(10),MmToRender(10),3,38,-4),

        new StructureDefinition("PFC","Pfc",MmToRender(new Point3D(42,38,34)),Color.FromRgb(143,160,250),"Izhikevich","DopamineModulatedSTDP",StructureLayout.CorticalSheet,12,6,4,MmToRender(28),MmToRender(16),MmToRender(14),8,28,-4),
        new StructureDefinition("Dorsomedial PFC","DorsomedialPrefrontalCortex",MmToRender(new Point3D(12,50,34)),Color.FromRgb(160,150,232),"Izhikevich","DopamineModulatedSTDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(12),MmToRender(10),5,18,0),
        new StructureDefinition("Ventromedial PFC","VentromedialPrefrontalCortex",MmToRender(new Point3D(12,-4,38)),Color.FromRgb(224,154,126),"Izhikevich","DopamineModulatedSTDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(11),MmToRender(10),5,12,-2),
        new StructureDefinition("Frontal Eye Fields","FrontalEyeFields",MmToRender(new Point3D(32,50,20)),Color.FromRgb(120,202,150),"Izhikevich","STDP",StructureLayout.CorticalSheet,8,5,4,MmToRender(18),MmToRender(11),MmToRender(9),2,8,0),
        new StructureDefinition("Broca (BA44/45)","BrocaBa44Ba45",MmToRender(new Point3D(52,18,20)),Color.FromRgb(148,174,252),"Izhikevich","DopamineModulatedSTDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,10,6,4,MmToRender(22),MmToRender(12),MmToRender(11),4,36,-4),
        new StructureDefinition("Wernicke (pSTG/pSTS)","WernickePstgPsts",MmToRender(new Point3D(56,14,-24)),Color.FromRgb(108,206,236),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,10,6,4,MmToRender(24),MmToRender(13),MmToRender(11),4,46,-8),
        new StructureDefinition("Supramarginal/Angular","SupramarginalAngular",MmToRender(new Point3D(46,28,-30)),Color.FromRgb(126,214,242),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,10,6,4,MmToRender(22),MmToRender(12),MmToRender(11),2,32,-4),
        new StructureDefinition("Arcuate Fasciculus","ArcuateFasciculus",MmToRender(new Point3D(54,20,-8)),Color.FromRgb(182,214,244),"LIF","STDP",StructureLayout.NucleusBlock,9,5,7,MmToRender(20),MmToRender(10),MmToRender(10),0,24,0),
        new StructureDefinition("Premotor Cortex","PremotorCortex",MmToRender(new Point3D(28,48,20)),Color.FromRgb(132,214,188),"Izhikevich","DopamineModulatedSTDP",StructureLayout.CorticalSheet,10,6,4,MmToRender(22),MmToRender(13),MmToRender(11),2,14,0),
        new StructureDefinition("Orbitofrontal Cortex","OrbitofrontalCortex",MmToRender(new Point3D(38,22,44)),Color.FromRgb(132,176,246),"Izhikevich","DopamineModulatedSTDP",StructureLayout.CorticalSheet,10,5,4,MmToRender(22),MmToRender(12),MmToRender(10),6,18,-4),
        new StructureDefinition("Insula","Insula",MmToRender(new Point3D(52,18,-6)),Color.FromRgb(102,184,238),"Izhikevich","DopamineModulatedSTDP",StructureLayout.CorticalSheet,8,5,4,MmToRender(16),MmToRender(11),MmToRender(10),2,8,2),
        new StructureDefinition("PPC","Ppc",MmToRender(new Point3D(30,48,-28)),Color.FromRgb(110,190,244),"LIF","STDP",StructureLayout.CorticalSheet,11,6,4,MmToRender(26),MmToRender(15),MmToRender(12),-4,10,0),
        new StructureDefinition("Temporal Association","TemporalAssociation",MmToRender(new Point3D(58,6,-28)),Color.FromRgb(122,224,214),"Izhikevich","STDP",StructureLayout.CorticalSheet,10,6,4,MmToRender(24),MmToRender(14),MmToRender(12),2,45,-6),
        new StructureDefinition("Inferotemporal Cortex","InferotemporalCortex",MmToRender(new Point3D(50,-4,-32)),Color.FromRgb(86,186,206),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,10,6,4,MmToRender(24),MmToRender(12),MmToRender(10),2,42,-8),
        new StructureDefinition("Fusiform Gyrus","FusiformGyrus",MmToRender(new Point3D(38,-12,-34)),Color.FromRgb(232,170,102),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(22),MmToRender(10),MmToRender(10),2,38,-8),
        new StructureDefinition("Temporal Pole","TemporalPole",MmToRender(new Point3D(50,-8,30)),Color.FromRgb(222,132,160),"Izhikevich","DopamineModulatedSTDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(12),MmToRender(11),3,28,-5),
        new StructureDefinition("Temporoparietal Junction","TemporoparietalJunction",MmToRender(new Point3D(54,26,-24)),Color.FromRgb(106,200,164),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(12),MmToRender(10),1,26,-2),
        new StructureDefinition("Precuneus","Precuneus",MmToRender(new Point3D(10,48,-28)),Color.FromRgb(178,162,226),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,9,5,4,MmToRender(22),MmToRender(13),MmToRender(10),5,8,0),
        new StructureDefinition("Midcingulate Cortex","MidcingulateCortex",MmToRender(new Point3D(8,42,-2)),Color.FromRgb(220,126,188),"Izhikevich","DopamineModulatedSTDP",StructureLayout.CorticalSheet,8,5,4,MmToRender(18),MmToRender(10),MmToRender(9),6,16,1),
        new StructureDefinition("Posterior Cingulate","PosteriorCingulate",MmToRender(new Point3D(12,44,-18)),Color.FromRgb(176,188,242),"Izhikevich","STDP+SynapticTaggingCapture",StructureLayout.CorticalSheet,8,5,4,MmToRender(18),MmToRender(11),MmToRender(10),8,8,0),
        new StructureDefinition("Retrosplenial Cortex","RetrosplenialCortex",MmToRender(new Point3D(16,40,-24)),Color.FromRgb(162,198,236),"Izhikevich","SynapticTaggingCapture",StructureLayout.CorticalSheet,8,5,4,MmToRender(18),MmToRender(11),MmToRender(10),6,4,0),

            new StructureDefinition("Striatum","Striatum",MmToRender(new Point3D(16,4,0)),Color.FromRgb(242,142,158),"LIF","DopamineModulatedSTDP",StructureLayout.NucleusBlock,9,8,8,MmToRender(20),MmToRender(16),MmToRender(15),0,0,0),
            new StructureDefinition("Nucleus Accumbens","NucleusAccumbens",MmToRender(new Point3D(16,-2,8)),Color.FromRgb(236,154,166),"LIF","DopamineModulatedSTDP",StructureLayout.NucleusBlock,8,7,7,MmToRender(14),MmToRender(11),MmToRender(11),0,0,0),
            new StructureDefinition("Globus Pallidus","GlobusPallidus",MmToRender(new Point3D(12,3,-4)),Color.FromRgb(244,134,126),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(12),MmToRender(9),MmToRender(9),0,8,0),
            new StructureDefinition("Ventral Pallidum","VentralPallidum",MmToRender(new Point3D(12,-3,4)),Color.FromRgb(238,146,118),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(11),MmToRender(8),MmToRender(8),0,6,0),
            new StructureDefinition("GPe","GPe",MmToRender(new Point3D(14,2,-6)),Color.FromRgb(236,126,118),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(10),MmToRender(8),MmToRender(8),0,5,0),
            new StructureDefinition("GPi","GPi",MmToRender(new Point3D(10,2,-6)),Color.FromRgb(230,118,112),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(10),MmToRender(8),MmToRender(8),0,10,0),
            new StructureDefinition("STN","Stn",MmToRender(new Point3D(9,2,-8)),Color.FromRgb(236,160,108),"Izhikevich","STDP",StructureLayout.NucleusBlock,6,5,5,MmToRender(9),MmToRender(7),MmToRender(7),0,12,0),
            new StructureDefinition("SNr","Snr",MmToRender(new Point3D(8,0,-12)),Color.FromRgb(214,122,168),"LIF","STDP",StructureLayout.NucleusBlock,6,5,5,MmToRender(9),MmToRender(7),MmToRender(7),0,20,0),
            new StructureDefinition("SNc","Snc",MmToRender(new Point3D(7,-1,-14)),Color.FromRgb(252,200,74),"Izhikevich","DopamineHomeostasis",StructureLayout.NucleusBlock,6,5,5,MmToRender(8),MmToRender(6),MmToRender(6),0,18,0),
            new StructureDefinition("Habenula","Habenula",MmToRender(new Point3D(4,10,0)),Color.FromRgb(218,176,124),"Izhikevich","STDP",StructureLayout.NucleusBlock,5,4,4,MmToRender(7),MmToRender(6),MmToRender(6),0,0,0),

            new StructureDefinition("Amygdala","Amygdala",MmToRender(new Point3D(17,1,-6)),Color.FromRgb(246,132,132),"Izhikevich","STDP",StructureLayout.NucleusBlock,8,7,7,MmToRender(14),MmToRender(11),MmToRender(10),0,35,0),
            new StructureDefinition("Hypothalamus","Hypothalamus",MmToRender(new Point3D(6,-4,4)),Color.FromRgb(238,142,132),"Izhikevich","HomeostaticGain",StructureLayout.NucleusBlock,8,7,7,MmToRender(12),MmToRender(10),MmToRender(9),0,12,0),
        new StructureDefinition("ACC","Acc",MmToRender(new Point3D(8,38,18)),Color.FromRgb(230,132,214),"Izhikevich","STDP",StructureLayout.CorticalSheet,8,5,4,MmToRender(18),MmToRender(11),MmToRender(10),8,22,2),

        new StructureDefinition("Granule Layer","CerebellarGranule",MmToRender(new Point3D(0,-50,-46)),Color.FromRgb(184,186,248),"LIF","MossyFiberLTP",StructureLayout.CerebellarSheet,26,12,22,MmToRender(64),MmToRender(26),MmToRender(48),0,0,0),
        new StructureDefinition("Purkinje Layer","PurkinjeCellLayer",MmToRender(new Point3D(0,-49,-46)),Color.FromRgb(186,154,250),"HH","CerebellarLTD",StructureLayout.CerebellarSheet,24,10,22,MmToRender(62),MmToRender(22),MmToRender(46),0,0,0),
        new StructureDefinition("Cerebellar Vermis","CerebellarVermis",MmToRender(new Point3D(0,-50,-44)),Color.FromRgb(202,180,252),"HH","CerebellarLTD",StructureLayout.CerebellarSheet,16,9,16,MmToRender(36),MmToRender(20),MmToRender(34),0,0,0),
        new StructureDefinition("Cerebellar Lobules","CerebellarLobules",MmToRender(new Point3D(0,-51,-48)),Color.FromRgb(170,184,248),"LIF","MossyFiberLTP",StructureLayout.CerebellarSheet,22,11,18,MmToRender(58),MmToRender(24),MmToRender(42),0,0,0),
            new StructureDefinition("DCN","DeepCerebellarNuclei",MmToRender(new Point3D(8,-44,-42)),Color.FromRgb(158,214,252),"Izhikevich","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(14),MmToRender(10),MmToRender(10),0,-10,0),
            new StructureDefinition("Cochlear Nucleus","CochlearNucleus",MmToRender(new Point3D(14,-24,-20)),Color.FromRgb(224,174,122),"Izhikevich","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(10),MmToRender(8),MmToRender(8),0,-2,0),
            new StructureDefinition("Superior Olive","SuperiorOlive",MmToRender(new Point3D(12,-22,-16)),Color.FromRgb(220,166,112),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(10),MmToRender(8),MmToRender(8),0,6,0),
            new StructureDefinition("Vestibular Nuclei","VestibularNuclei",MmToRender(new Point3D(13,-26,-18)),Color.FromRgb(216,186,132),"LIF","STDP",StructureLayout.NucleusBlock,7,6,6,MmToRender(10),MmToRender(8),MmToRender(8),0,-2,0),
            new StructureDefinition("Nucleus Tractus Solitarius","NucleusTractusSolitarius",MmToRender(new Point3D(6,-30,-20)),Color.FromRgb(214,178,126),"LIF","HomeostaticGain",StructureLayout.NucleusBlock,6,6,6,MmToRender(9),MmToRender(8),MmToRender(8),0,-2,0),
            new StructureDefinition("Inferior Olive","InferiorOlive",MmToRender(new Point3D(4,-38,-22)),Color.FromRgb(248,184,96),"HH","STDP",StructureLayout.BrainstemColumn,6,8,6,MmToRender(8),MmToRender(12),MmToRender(8),0,-4,0),
            new StructureDefinition("Reticular Formation","ReticularFormation",MmToRender(new Point3D(0,-24,-16)),Color.FromRgb(226,154,120),"Izhikevich","HomeostaticGain",StructureLayout.BrainstemColumn,8,12,8,MmToRender(10),MmToRender(18),MmToRender(10),0,0,0),
            new StructureDefinition("Pons","Pons",MmToRender(new Point3D(2,-28,-14)),Color.FromRgb(236,174,112),"LIF","STDP",StructureLayout.BrainstemColumn,8,10,8,MmToRender(10),MmToRender(15),MmToRender(10),0,-2,0),
            new StructureDefinition("Medulla","Medulla",MmToRender(new Point3D(0,-34,-20)),Color.FromRgb(228,166,104),"LIF","HomeostaticGain",StructureLayout.BrainstemColumn,8,10,8,MmToRender(10),MmToRender(15),MmToRender(10),0,-4,0),
            new StructureDefinition("Spinal Cord Motor","SpinalCordMotor",MmToRender(new Point3D(4,-46,-12)),Color.FromRgb(210,152,108),"Izhikevich","STDP",StructureLayout.BrainstemColumn,8,12,8,MmToRender(10),MmToRender(20),MmToRender(10),0,-2,0),

            new StructureDefinition("LC","LocusCoeruleus",MmToRender(new Point3D(6,-22,-24)),Color.FromRgb(248,214,96),"LIF","HomeostaticGain",StructureLayout.BrainstemColumn,5,8,5,MmToRender(8),MmToRender(13),MmToRender(8),0,-6,0),
            new StructureDefinition("Raphe","RapheNuclei",MmToRender(new Point3D(0,-20,-22)),Color.FromRgb(246,160,218),"LIF","HomeostaticGain",StructureLayout.BrainstemColumn,5,8,5,MmToRender(8),MmToRender(13),MmToRender(8),0,6,0),
            new StructureDefinition("Basal Forebrain","BasalForebrain",MmToRender(new Point3D(10,2,2)),Color.FromRgb(190,236,120),"LIF","HomeostaticGain",StructureLayout.NucleusBlock,6,6,6,MmToRender(11),MmToRender(9),MmToRender(9),0,15,0),
            new StructureDefinition("VTA","Vta",MmToRender(new Point3D(6,-16,-18)),Color.FromRgb(254,214,86),"Izhikevich","DopamineHomeostasis",StructureLayout.BrainstemColumn,5,8,5,MmToRender(8),MmToRender(12),MmToRender(8),0,10,0),

        new StructureDefinition("SMA","Sma",MmToRender(new Point3D(12,52,26)),Color.FromRgb(136,238,184),"LIF","STDP",StructureLayout.CorticalSheet,8,5,4,MmToRender(18),MmToRender(10),MmToRender(10),2,12,0),
        new StructureDefinition("M1","M1",MmToRender(new Point3D(34,38,14)),Color.FromRgb(114,236,164),"Izhikevich","STDP",StructureLayout.CorticalSheet,9,5,4,MmToRender(20),MmToRender(11),MmToRender(10),4,18,0)
    };

    private StructureDefinition CreateFallbackStructureDefinition(string snapshotId, int index)
    {
        var displayName = ResolveDisplayNameForSnapshotId(snapshotId);
        var ring = index % 12;
        var layer = index / 12;
        var angle = (Math.PI * 2.0 * ring) / 12.0;
        var radius = MmToRender(18 + (layer * 8));
        var center = new Point3D(
            Math.Cos(angle) * radius,
            MmToRender(6 + (layer * 5)),
            Math.Sin(angle) * radius);
        var color = Color.FromRgb(
            (byte)(120 + ((index * 17) % 96)),
            (byte)(140 + ((index * 23) % 88)),
            (byte)(160 + ((index * 31) % 80)));

        return new StructureDefinition(
            displayName,
            snapshotId,
            center,
            color,
            "Izhikevich",
            "STDP",
            StructureLayout.NucleusBlock,
            7,
            6,
            6,
            MmToRender(12),
            MmToRender(9),
            MmToRender(9),
            0,
            0,
            0);
    }

    private string ResolveDisplayNameForSnapshotId(string snapshotId)
    {
        var direct = _displayToSnapshotId.FirstOrDefault(kv => string.Equals(kv.Value, snapshotId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(direct.Key))
        {
            return direct.Key;
        }

        return snapshotId;
    }

    private IEnumerable<PathwayDefinition> LoadPathwayDefinitions()
    {
        if (_pathwayDefinitionsCache is not null)
        {
            return _pathwayDefinitionsCache;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "connectivity", "dnne-connectivity.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "connectivity", "dnne-connectivity.json"),
            Path.Combine(Environment.CurrentDirectory, "connectivity", "dnne-connectivity.json")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var path = Path.GetFullPath(candidate);
                if (!File.Exists(path)) continue;

                var json = File.ReadAllText(path);
                var rules = JsonSerializer.Deserialize<List<ConnectivityRuleJson>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (rules is null || rules.Count == 0) continue;

                _pathwayDefinitionsCache = rules
                    .Where(r => !string.IsNullOrWhiteSpace(r.Source))
                    .SelectMany(r => (r.Connections ?? [])
                        .Where(c => !string.IsNullOrWhiteSpace(c.Target))
                        .Select(c => new PathwayDefinition(
                            r.Source!,
                            c.Target!,
                            string.IsNullOrWhiteSpace(c.Neurotransmitter) ? "GLUTAMATE" : c.Neurotransmitter!,
                            c.ProjectionType ?? "unspecified",
                            (c.ProjectionType ?? string.Empty).Contains("feedback", StringComparison.OrdinalIgnoreCase))))
                    .ToList();
                return _pathwayDefinitionsCache;
            }
            catch
            {
                // try next candidate
            }
        }

        _pathwayDefinitionsCache = Array.Empty<PathwayDefinition>();
        return _pathwayDefinitionsCache;
    }

    private static string PathwayKey(string source, string target) => $"{source}>{target}";

    private static Color GetPathwayColor(string neurotransmitter) => neurotransmitter.ToUpperInvariant() switch
    {
        "GLUTAMATE" => Color.FromRgb(116, 194, 248),
        "GABA" => Color.FromRgb(246, 128, 146),
        "DOPAMINE" => Color.FromRgb(255, 214, 84),
        "SEROTONIN" => Color.FromRgb(248, 170, 224),
        "ACETYLCHOLINE" => Color.FromRgb(182, 242, 120),
        "NOREPINEPHRINE" => Color.FromRgb(255, 190, 110),
        _ => Color.FromRgb(142, 178, 240)
    };
}
