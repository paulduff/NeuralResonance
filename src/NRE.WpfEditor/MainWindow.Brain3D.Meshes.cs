using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NRE.WpfEditor;

// Static mesh-builder helpers for the brain visualization.
// Pure functions: take inputs, produce MeshGeometry3D / Material additions.
// Extracted from MainWindow.xaml.cs to consolidate geometry construction.
public partial class MainWindow
{
    private static void AddReferenceMesh(Model3DGroup root, MeshGeometry3D mesh, Color diffuseColor, Color emissiveColor)
    {
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var diffuse = new SolidColorBrush(diffuseColor) { Opacity = diffuseColor.A / 255.0 };
        var emissive = new SolidColorBrush(emissiveColor) { Opacity = emissiveColor.A / 255.0 };
        diffuse.Freeze();
        emissive.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));
        root.Children.Add(new GeometryModel3D(mesh, material));
    }

    private static void AddCorticalGyrusSurface(Model3DGroup root, string snapshotId, string hemisphere, Color baseColor)
    {
        var hemisphereSign = hemisphere == "L" ? -1.0 : 1.0;
        var mesh = BuildCorticalGyrusSurfaceMesh(snapshotId, hemisphereSign, 44, 10);
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var diffuseBase = BrightenPreserveHue(baseColor, 0.24);
        var emissiveBase = BrightenPreserveHue(baseColor, 0.70);
        var diffuse = new SolidColorBrush(Color.FromArgb(88, diffuseBase.R, diffuseBase.G, diffuseBase.B)) { Opacity = 0.34 };
        var emissive = new SolidColorBrush(Color.FromArgb(38, emissiveBase.R, emissiveBase.G, emissiveBase.B)) { Opacity = 0.15 };
        diffuse.Freeze();
        emissive.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));
        root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static void AddHomuncularCorticalBands(Model3DGroup root, string snapshotId, string hemisphere)
    {
        if (!IsHomuncularCortex(snapshotId))
        {
            return;
        }

        var hemisphereSign = hemisphere == "L" ? -1.0 : 1.0;
        var bands = new[]
        {
            new HomuncularBand(0.07, 0.24, Color.FromArgb(138, 112, 178, 255), Color.FromArgb(48, 112, 178, 255)),
            new HomuncularBand(0.28, 0.42, Color.FromArgb(138, 116, 224, 164), Color.FromArgb(46, 116, 224, 164)),
            new HomuncularBand(0.47, 0.68, Color.FromArgb(146, 96, 226, 222), Color.FromArgb(52, 96, 226, 222)),
            new HomuncularBand(0.73, 0.92, Color.FromArgb(150, 255, 184, 104), Color.FromArgb(54, 255, 184, 104))
        };

        foreach (var band in bands)
        {
            var mesh = BuildHomuncularBandMesh(snapshotId, hemisphereSign, band.AlongStart, band.AlongEnd, 8, 3);
            if (mesh.Positions.Count == 0)
            {
                continue;
            }

            TryFreeze(mesh);
            var diffuse = new SolidColorBrush(band.Diffuse) { Opacity = band.Diffuse.A / 255.0 };
            var emissive = new SolidColorBrush(band.Emissive) { Opacity = band.Emissive.A / 255.0 };
            diffuse.Freeze();
            emissive.Freeze();
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(diffuse));
            material.Children.Add(new EmissiveMaterial(emissive));
            root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }
    }

    private static void AddParietalBodySchemaFields(Model3DGroup root, string snapshotId, string hemisphere)
    {
        if (!string.Equals(snapshotId, "Ppc", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hemisphereSign = hemisphere == "L" ? -1.0 : 1.0;
        var fields = new[]
        {
            new HomuncularBand(0.08, 0.24, Color.FromArgb(116, 126, 232, 255), Color.FromArgb(42, 126, 232, 255)),
            new HomuncularBand(0.30, 0.46, Color.FromArgb(118, 126, 224, 170), Color.FromArgb(42, 126, 224, 170)),
            new HomuncularBand(0.52, 0.68, Color.FromArgb(118, 255, 204, 116), Color.FromArgb(42, 255, 204, 116)),
            new HomuncularBand(0.74, 0.91, Color.FromArgb(112, 236, 132, 214), Color.FromArgb(40, 236, 132, 214))
        };

        foreach (var field in fields)
        {
            var mesh = BuildHomuncularBandMesh(snapshotId, hemisphereSign, field.AlongStart, field.AlongEnd, 7, 4);
            if (mesh.Positions.Count == 0)
            {
                continue;
            }

            TryFreeze(mesh);
            var diffuse = new SolidColorBrush(field.Diffuse) { Opacity = field.Diffuse.A / 255.0 };
            var emissive = new SolidColorBrush(field.Emissive) { Opacity = field.Emissive.A / 255.0 };
            diffuse.Freeze();
            emissive.Freeze();
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(diffuse));
            material.Children.Add(new EmissiveMaterial(emissive));
            root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }
    }

    private static void AddDeepCircuitReferenceSurfaces(
        Model3DGroup root,
        string snapshotId,
        string hemisphere,
        Color baseColor,
        IReadOnlyList<Point3D> localPoints)
    {
        if (localPoints.Count == 0)
        {
            return;
        }

        var bounds = ComputeLocalBounds(localPoints);
        var diffuseColor = Color.FromArgb(68, BrightenPreserveHue(baseColor, 0.40).R, BrightenPreserveHue(baseColor, 0.40).G, BrightenPreserveHue(baseColor, 0.40).B);
        var emissiveColor = Color.FromArgb(24, BrightenPreserveHue(baseColor, 0.70).R, BrightenPreserveHue(baseColor, 0.70).G, BrightenPreserveHue(baseColor, 0.70).B);
        var lineColor = Color.FromArgb(110, BrightenPreserveHue(baseColor, 0.82).R, BrightenPreserveHue(baseColor, 0.82).G, BrightenPreserveHue(baseColor, 0.82).B);

        if (IsThalamicGuide(snapshotId))
        {
            AddGuideMesh(root, BuildThalamicGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
            AddThalamicRelayLines(root, bounds, lineColor);
            return;
        }

        if (IsBasalGangliaGuide(snapshotId))
        {
            AddGuideMesh(root, BuildBasalGangliaGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
            AddBasalGangliaGuideLines(root, bounds, snapshotId, lineColor, hemisphere);
            return;
        }

        if (IsCerebellarGuide(snapshotId))
        {
            AddGuideMesh(root, BuildCerebellarLocalGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
            AddCerebellarFoliaLines(root, bounds, snapshotId, lineColor);
            return;
        }

        if (IsBrainstemGuide(snapshotId))
        {
            AddGuideMesh(root, BuildBrainstemLocalGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
            AddBrainstemGuideLines(root, bounds, snapshotId, lineColor);
        }
    }

    private static void AddGuideMesh(Model3DGroup root, MeshGeometry3D mesh, Color diffuseColor, Color emissiveColor)
    {
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var diffuse = new SolidColorBrush(diffuseColor) { Opacity = diffuseColor.A / 255.0 };
        var emissive = new SolidColorBrush(emissiveColor) { Opacity = emissiveColor.A / 255.0 };
        diffuse.Freeze();
        emissive.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));
        root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static void AddGuideTube(Model3DGroup root, Point3D start, Point3D end, double radius, Color color)
    {
        var mesh = BuildTubeMesh(start, end, radius, 7);
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var brush = new SolidColorBrush(color) { Opacity = color.A / 255.0 };
        brush.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(brush));
        material.Children.Add(new EmissiveMaterial(brush));
        root.Children.Add(new GeometryModel3D(mesh, material));
    }

    private static MeshGeometry3D BuildThalamicGuideMesh(LocalBounds b, string snapshotId)
    {
        var rx = b.RadiusX * (snapshotId.Equals("Trn", StringComparison.OrdinalIgnoreCase) ? 1.10 : 0.94);
        var ry = b.RadiusY * (snapshotId.Equals("Trn", StringComparison.OrdinalIgnoreCase) ? 0.76 : 0.88);
        var rz = b.RadiusZ * (snapshotId.Equals("Pulvinar", StringComparison.OrdinalIgnoreCase) ? 1.04 : 0.88);
        var center = new Point3D(b.Center.X, b.Center.Y + (b.RadiusY * 0.04), b.Center.Z);
        return BuildEllipsoidMesh(center, Math.Max(0.004, rx), Math.Max(0.004, ry), Math.Max(0.004, rz), 20, 10);
    }

    private static void AddThalamicRelayLines(Model3DGroup root, LocalBounds b, Color color)
    {
        var radius = Math.Max(0.0025, Math.Min(b.RadiusX, Math.Min(b.RadiusY, b.RadiusZ)) * 0.035);
        for (var i = -1; i <= 1; i++)
        {
            var y = b.Center.Y + (i * b.RadiusY * 0.28);
            AddGuideTube(root, new Point3D(b.Center.X - b.RadiusX * 0.78, y, b.Center.Z), new Point3D(b.Center.X + b.RadiusX * 0.78, y, b.Center.Z), radius, color);
        }

        AddGuideTube(root, new Point3D(b.Center.X, b.Center.Y - b.RadiusY * 0.70, b.Center.Z), new Point3D(b.Center.X, b.Center.Y + b.RadiusY * 0.74, b.Center.Z), radius, color);
    }

    private static MeshGeometry3D BuildBasalGangliaGuideMesh(LocalBounds b, string snapshotId)
    {
        var rx = b.RadiusX * (snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase) ? 1.02 : 0.86);
        var ry = b.RadiusY * (snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase) ? 0.82 : 0.58);
        var rz = b.RadiusZ * (snapshotId.Equals("Stn", StringComparison.OrdinalIgnoreCase) ? 0.62 : 0.82);
        return BuildEllipsoidMesh(b.Center, Math.Max(0.004, rx), Math.Max(0.004, ry), Math.Max(0.004, rz), 18, 8);
    }

    private static void AddBasalGangliaGuideLines(Model3DGroup root, LocalBounds b, string snapshotId, Color color, string hemisphere)
    {
        var radius = Math.Max(0.0025, Math.Min(b.RadiusX, Math.Min(b.RadiusY, b.RadiusZ)) * 0.035);
        var side = hemisphere.Equals("L", StringComparison.OrdinalIgnoreCase) ? -1.0 : 1.0;
        if (snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase))
        {
            for (var i = 0; i < 4; i++)
            {
                var t0 = -0.72 + (i * 0.18);
                var t1 = t0 + 0.26;
                AddGuideTube(
                    root,
                    new Point3D(b.Center.X + side * b.RadiusX * (0.18 + t0 * 0.32), b.Center.Y + b.RadiusY * (0.52 - i * 0.20), b.Center.Z - b.RadiusZ * 0.72),
                    new Point3D(b.Center.X + side * b.RadiusX * (0.48 + t1 * 0.22), b.Center.Y - b.RadiusY * (0.16 + i * 0.06), b.Center.Z + b.RadiusZ * 0.62),
                    radius,
                    color);
            }
        }
        else
        {
            AddGuideTube(root, new Point3D(b.Center.X - b.RadiusX * 0.62, b.Center.Y, b.Center.Z), new Point3D(b.Center.X + b.RadiusX * 0.62, b.Center.Y, b.Center.Z), radius, color);
            AddGuideTube(root, new Point3D(b.Center.X, b.Center.Y, b.Center.Z - b.RadiusZ * 0.58), new Point3D(b.Center.X, b.Center.Y, b.Center.Z + b.RadiusZ * 0.58), radius, color);
        }
    }

    private static MeshGeometry3D BuildCerebellarLocalGuideMesh(LocalBounds b, string snapshotId)
    {
        var mesh = new MeshGeometry3D();
        var slices = 26;
        var stacks = 10;
        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = stack / (double)stacks;
            var phi = (v * Math.PI) - (Math.PI / 2.0);
            for (var slice = 0; slice <= slices; slice++)
            {
                var u = slice / (double)slices;
                var theta = u * Math.PI * 2.0;
                var lateral = Math.Cos(theta) * Math.Cos(phi);
                var vertical = Math.Sin(phi);
                var depth = Math.Sin(theta) * Math.Cos(phi);
                var folia = Math.Sin((v * Math.PI * 12.0) + (depth * 1.4)) * b.RadiusY * 0.030;
                var vermis = snapshotId.Equals("CerebellarVermis", StringComparison.OrdinalIgnoreCase)
                    ? 0.20 * Math.Exp(-(lateral * lateral) / 0.055)
                    : 0.06 * Math.Exp(-(lateral * lateral) / 0.090);
                var p = new Point3D(
                    b.Center.X + (lateral * b.RadiusX * 0.95),
                    b.Center.Y + (vertical * b.RadiusY * 0.76) + (vermis * b.RadiusY),
                    b.Center.Z + (depth * b.RadiusZ * 0.92) + folia);
                mesh.Positions.Add(p);
                var n = new Vector3D(lateral, vertical + vermis, depth);
                if (n.LengthSquared < 1e-8)
                {
                    n = new Vector3D(0, 1, 0);
                }
                n.Normalize();
                mesh.Normals.Add(n);
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }

        AddGridTriangles(mesh, slices + 1, stacks + 1);
        return mesh;
    }

    private static void AddCerebellarFoliaLines(Model3DGroup root, LocalBounds b, string snapshotId, Color color)
    {
        if (snapshotId.Equals("DeepCerebellarNuclei", StringComparison.OrdinalIgnoreCase))
        {
            AddGuideTube(root, new Point3D(b.Center.X - b.RadiusX * 0.50, b.Center.Y, b.Center.Z), new Point3D(b.Center.X + b.RadiusX * 0.50, b.Center.Y, b.Center.Z), Math.Max(0.0025, b.RadiusY * 0.045), color);
            return;
        }

        var radius = Math.Max(0.002, Math.Min(b.RadiusY, b.RadiusZ) * 0.018);
        for (var i = 0; i < 8; i++)
        {
            var y = b.Center.Y - b.RadiusY * 0.52 + (i * b.RadiusY * 0.15);
            var z = b.Center.Z - b.RadiusZ * 0.42 + (Math.Sin(i * 0.9) * b.RadiusZ * 0.16);
            AddGuideTube(root, new Point3D(b.Center.X - b.RadiusX * 0.82, y, z), new Point3D(b.Center.X + b.RadiusX * 0.82, y, z), radius, color);
        }
    }

    private static MeshGeometry3D BuildBrainstemLocalGuideMesh(LocalBounds b, string snapshotId)
    {
        var mesh = new MeshGeometry3D();
        var slices = 16;
        var stacks = 12;
        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = stack / (double)stacks;
            var y = b.MinY + ((b.MaxY - b.MinY) * v);
            var normalized = -1.0 + (2.0 * v);
            var pontine = snapshotId.Equals("Pons", StringComparison.OrdinalIgnoreCase)
                ? Math.Exp(-Math.Pow(normalized + 0.05, 2) / 0.22)
                : 0.0;
            var taper = 0.86 - (0.22 * Math.Abs(normalized));
            var rx = Math.Max(0.003, b.RadiusX * taper * (1.0 + 0.36 * pontine));
            var rz = Math.Max(0.003, b.RadiusZ * taper * (1.0 + 0.32 * pontine));
            for (var slice = 0; slice <= slices; slice++)
            {
                var u = slice / (double)slices;
                var theta = u * Math.PI * 2.0;
                mesh.Positions.Add(new Point3D(
                    b.Center.X + (Math.Cos(theta) * rx),
                    y,
                    b.Center.Z + (Math.Sin(theta) * rz) + (pontine * b.RadiusZ * 0.18)));
                var n = new Vector3D(Math.Cos(theta), 0.05, Math.Sin(theta));
                n.Normalize();
                mesh.Normals.Add(n);
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }

        AddGridTriangles(mesh, slices + 1, stacks + 1);
        return mesh;
    }

    private static void AddBrainstemGuideLines(Model3DGroup root, LocalBounds b, string snapshotId, Color color)
    {
        var radius = Math.Max(0.002, Math.Min(b.RadiusX, b.RadiusZ) * 0.035);
        AddGuideTube(root, new Point3D(b.Center.X, b.MinY, b.Center.Z), new Point3D(b.Center.X, b.MaxY, b.Center.Z), radius, color);
        if (snapshotId.Equals("Pons", StringComparison.OrdinalIgnoreCase))
        {
            AddGuideTube(root, new Point3D(b.Center.X - b.RadiusX * 0.74, b.Center.Y + b.RadiusY * 0.08, b.Center.Z), new Point3D(b.Center.X + b.RadiusX * 0.74, b.Center.Y + b.RadiusY * 0.08, b.Center.Z), radius, color);
        }
    }

    private static LocalBounds ComputeLocalBounds(IReadOnlyList<Point3D> points)
    {
        var minX = points[0].X;
        var maxX = points[0].X;
        var minY = points[0].Y;
        var maxY = points[0].Y;
        var minZ = points[0].Z;
        var maxZ = points[0].Z;
        for (var i = 1; i < points.Count; i++)
        {
            var p = points[i];
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
            minZ = Math.Min(minZ, p.Z);
            maxZ = Math.Max(maxZ, p.Z);
        }

        return new LocalBounds(
            minX,
            maxX,
            minY,
            maxY,
            minZ,
            maxZ,
            new Point3D((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5),
            Math.Max(0.004, (maxX - minX) * 0.5),
            Math.Max(0.004, (maxY - minY) * 0.5),
            Math.Max(0.004, (maxZ - minZ) * 0.5));
    }

    private static bool IsThalamicGuide(string snapshotId)
        => snapshotId is "Thalamus" or "MotorThalamus" or "Trn" or "Pulvinar" or "MediodorsalThalamus" or "IntralaminarThalamus";

    private static bool IsBasalGangliaGuide(string snapshotId)
        => snapshotId is "Striatum" or "NucleusAccumbens" or "GlobusPallidus" or "VentralPallidum" or "GPe" or "GPi" or "Stn" or "Snr" or "Snc";

    private static bool IsCerebellarGuide(string snapshotId)
        => snapshotId is "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarVermis" or "CerebellarLobules" or "DeepCerebellarNuclei";

    private static bool IsBrainstemGuide(string snapshotId)
        => snapshotId is "Pons" or "Medulla" or "InferiorOlive" or "LocusCoeruleus" or "RapheNuclei" or "Vta" or "ReticularFormation" or "SpinalCordMotor";

    private readonly record struct LocalBounds(
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        double MinZ,
        double MaxZ,
        Point3D Center,
        double RadiusX,
        double RadiusY,
        double RadiusZ);

    private static MeshGeometry3D BuildHomuncularBandMesh(
        string snapshotId,
        double hemisphereSign,
        double alongStart,
        double alongEnd,
        int columns,
        int rows)
    {
        var mesh = new MeshGeometry3D();
        var seed = $"homunculus_{snapshotId}_{alongStart:0.00}";
        for (var row = 0; row < rows; row++)
        {
            var width = rows <= 1 ? 0.5 : 0.30 + (0.40 * row / (double)(rows - 1));
            for (var col = 0; col < columns; col++)
            {
                var t = columns <= 1 ? 0.5 : col / (double)(columns - 1);
                var along = alongStart + ((alongEnd - alongStart) * t);
                var jitter = DeterministicJitter(col, row, 0, seed);
                var point = BuildCorticalGyrusPoint(snapshotId, along, width, MmToRender(1.90), jitter * 0.35, hemisphereSign);
                var normal = GetCorticalShellNormal(point, hemisphereSign < 0 ? "L" : "R");
                mesh.Positions.Add(point);
                mesh.Normals.Add(normal);
                mesh.TextureCoordinates.Add(new Point(t, row / (double)Math.Max(1, rows - 1)));
            }
        }

        AddGridTriangles(mesh, columns, rows);
        return mesh;
    }

    private static bool IsHomuncularCortex(string snapshotId)
        => string.Equals(snapshotId, "M1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(snapshotId, "S1", StringComparison.OrdinalIgnoreCase);

    private static MeshGeometry3D BuildCorticalGyrusSurfaceMesh(string snapshotId, double hemisphereSign, int columns, int rows)
    {
        var mesh = new MeshGeometry3D();
        var profile = GetCorticalGyrusProfile(snapshotId);
        var seed = $"gyrus_surface_{snapshotId}";
        for (var row = 0; row < rows; row++)
        {
            var width = rows <= 1 ? 0.5 : row / (double)(rows - 1);
            for (var col = 0; col < columns; col++)
            {
                var along = columns <= 1 ? 0.5 : col / (double)(columns - 1);
                var jitter = DeterministicJitter(col, row, 0, seed);
                var point = BuildCorticalGyrusPoint(snapshotId, along, width, MmToRender(1.15), jitter, hemisphereSign);
                var normal = GetCorticalShellNormal(point, hemisphereSign < 0 ? "L" : "R");
                mesh.Positions.Add(point);
                mesh.Normals.Add(normal);
                mesh.TextureCoordinates.Add(new Point(along, width));
            }
        }

        AddGridTriangles(mesh, columns, rows);

        // A light medial ridge prevents broad patches from reading as a flat plate.
        var medialCrownMm = profile.CrownLiftMm * 0.35;
        if (medialCrownMm > 0.001)
        {
            for (var col = 0; col < columns; col++)
            {
                var idx = (rows / 2 * columns) + col;
                if (idx >= 0 && idx < mesh.Positions.Count)
                {
                    var point = mesh.Positions[idx];
                    var normal = mesh.Normals[idx];
                    mesh.Positions[idx] = new Point3D(
                        point.X + (normal.X * MmToRender(medialCrownMm)),
                        point.Y + (normal.Y * MmToRender(medialCrownMm)),
                        point.Z + (normal.Z * MmToRender(medialCrownMm)));
                }
            }
        }

        return mesh;
    }

    private static CorpusCallosumVisual AddCorpusCallosumPathwayScaffold(Model3DGroup root)
    {
        var baseColor = Color.FromRgb(170, 204, 246);
        var diffuse = new SolidColorBrush(ScaleColor(baseColor, 0.18)) { Opacity = 0.18 };
        var emissive = new SolidColorBrush(Color.FromArgb(40, 210, 232, 255)) { Opacity = 0.06 };
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));

        var group = new Model3DGroup();
        var lateralLanes = new[] { -0.74, -0.42, -0.14, 0.14, 0.42, 0.74 };
        const int segmentCount = 13;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var segmentStart = -0.90 + (1.80 * ((segment + 0.10) / segmentCount));
            var segmentEnd = -0.90 + (1.80 * ((segment + 0.64) / segmentCount));
            foreach (var lane in lateralLanes)
            {
                var laneOffset = 0.018 * Math.Sin((segment * 0.9) + (lane * Math.PI));
                var p0 = GetCorpusCallosumScaffoldPoint(segmentStart, lane + laneOffset);
                var p1 = GetCorpusCallosumScaffoldPoint(segmentEnd, lane + laneOffset);
                var mesh = BuildTubeMesh(p0, p1, MmToRender(0.95), 6);
                if (mesh.Positions.Count > 0)
                {
                    group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
                }
            }
        }

        if (group.Children.Count > 0)
        {
            root.Children.Add(group);
        }

        return new CorpusCallosumVisual(baseColor, diffuse, emissive);
    }

    private static Point3D GetCorpusCallosumScaffoldPoint(double u, double v)
    {
        var center = ApplyNonCorticalGlobalShift(
            ApplyEncephalonOffset(
                ShiftSuperior(
                    ScalePointBySubcorticalRatio(MmToRender(new Point3D(0.0, 26.0, -6.0))),
                    MmToRender(10),
                    0.18),
                "CorpusCallosum",
                "M"),
            "CorpusCallosum");

        var rx = MmToRender(34.0) * SubcorticalScaleRatio.Width;
        var ry = MmToRender(7.0) * SubcorticalScaleRatio.Height;
        var rz = MmToRender(32.0) * SubcorticalScaleRatio.Length;
        var clampedU = Math.Clamp(u, -1.0, 1.0);
        var clampedV = Math.Clamp(v, -1.0, 1.0);
        var anteriorGenu = Math.Exp(-Math.Pow(clampedU - 0.84, 2) / 0.055);
        var posteriorSplenium = Math.Exp(-Math.Pow(clampedU + 0.86, 2) / 0.070);
        var bodyArch = 0.52 * ry * (1.0 - (0.38 * clampedU * clampedU));
        var yCenter = bodyArch - (0.78 * ry * anteriorGenu) - (0.28 * ry * posteriorSplenium);
        var endTaper = Math.Clamp((1.0 - Math.Abs(clampedU)) / 0.18, 0.0, 1.0);
        var bulb = Math.Max(anteriorGenu, posteriorSplenium);
        var halfWidth = rx * (0.34 + (0.34 * (1.0 - Math.Abs(clampedU))) + (0.18 * bulb)) * (0.72 + (0.28 * endTaper));
        var lateralDrape = -0.10 * ry * clampedV * clampedV * (1.0 - Math.Abs(clampedU) * 0.35);

        return new Point3D(
            center.X + (clampedV * halfWidth),
            center.Y + yCenter + lateralDrape,
            center.Z + (clampedU * rz));
    }

    private static MeshGeometry3D BuildCorticalReferenceSurfaceMesh(double hemisphereSign, int thetaSteps, int phiSteps)
    {
        var mesh = new MeshGeometry3D();
        const double thetaMin = -2.32;
        const double thetaMax = 1.74;
        const double phiMin = -1.02;
        const double phiMax = 1.34;

        for (var p = 0; p <= phiSteps; p++)
        {
            var phi = phiMin + ((phiMax - phiMin) * (p / (double)phiSteps));
            for (var t = 0; t <= thetaSteps; t++)
            {
                var theta = thetaMin + ((thetaMax - thetaMin) * (t / (double)thetaSteps));
                var point = BuildCorticalSurfacePoint(theta, phi, hemisphereSign);
                var normal = GetCorticalShellNormal(point, hemisphereSign < 0 ? "L" : "R");
                mesh.Positions.Add(point);
                mesh.Normals.Add(normal);
                mesh.TextureCoordinates.Add(new Point(t / (double)thetaSteps, p / (double)phiSteps));
            }
        }

        AddGridTriangles(mesh, thetaSteps + 1, phiSteps + 1);
        return mesh;
    }

    private static MeshGeometry3D BuildCorpusCallosumReferenceSurfaceMesh(int lengthSteps, int widthSteps)
    {
        var mesh = new MeshGeometry3D();
        var center = ApplyNonCorticalGlobalShift(
            ApplyEncephalonOffset(
                ShiftSuperior(
                    ScalePointBySubcorticalRatio(MmToRender(new Point3D(0.0, 26.0, -6.0))),
                    MmToRender(10),
                    0.18),
                "CorpusCallosum",
                "M"),
            "CorpusCallosum");

        var rx = MmToRender(34.0) * SubcorticalScaleRatio.Width;
        var ry = MmToRender(7.0) * SubcorticalScaleRatio.Height;
        var rz = MmToRender(32.0) * SubcorticalScaleRatio.Length;

        for (var zi = 0; zi <= lengthSteps; zi++)
        {
            var u = -1.0 + (2.0 * zi / Math.Max(1.0, lengthSteps));
            var anteriorGenu = Math.Exp(-Math.Pow(u - 0.84, 2) / 0.055);
            var posteriorSplenium = Math.Exp(-Math.Pow(u + 0.86, 2) / 0.070);
            var bodyArch = 0.52 * ry * (1.0 - (0.38 * u * u));
            var yCenter = bodyArch - (0.78 * ry * anteriorGenu) - (0.28 * ry * posteriorSplenium);
            var endTaper = Math.Clamp((1.0 - Math.Abs(u)) / 0.18, 0.0, 1.0);
            var bulb = Math.Max(anteriorGenu, posteriorSplenium);
            var halfWidth = rx * (0.34 + (0.34 * (1.0 - Math.Abs(u))) + (0.18 * bulb)) * (0.72 + (0.28 * endTaper));

            for (var xi = 0; xi <= widthSteps; xi++)
            {
                var v = -1.0 + (2.0 * xi / Math.Max(1.0, widthSteps));
                var x = v * halfWidth;
                var z = u * rz;
                var lateralDrape = -0.10 * ry * v * v * (1.0 - Math.Abs(u) * 0.35);
                mesh.Positions.Add(new Point3D(center.X + x, center.Y + yCenter + lateralDrape, center.Z + z));
                mesh.Normals.Add(new Vector3D(0, 1, 0));
                mesh.TextureCoordinates.Add(new Point(xi / (double)widthSteps, zi / (double)lengthSteps));
            }
        }

        AddGridTriangles(mesh, widthSteps + 1, lengthSteps + 1);
        return mesh;
    }

    private static MeshGeometry3D BuildCerebellarReferenceSurfaceMesh(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        var center = ApplyNonCorticalGlobalShift(
            ApplyEncephalonOffset(
                ShiftSuperior(
                    ScalePointBySubcorticalRatio(MmToRender(new Point3D(0.0, -39.5, -52.0))),
                    MmToRender(20),
                    0.08),
                "CerebellarLobules",
                "M"),
            "CerebellarLobules");

        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = stack / (double)stacks;
            var phi = (v * Math.PI) - (Math.PI / 2.0);
            for (var slice = 0; slice <= slices; slice++)
            {
                var u = slice / (double)slices;
                var theta = u * Math.PI * 2.0;
                var lateral = Math.Cos(theta) * Math.Cos(phi);
                var vertical = Math.Sin(phi);
                var depth = Math.Sin(theta) * Math.Cos(phi);
                var vermisRidge = 0.10 * Math.Exp(-(lateral * lateral) / 0.045) * (0.55 + (0.45 * Math.Max(0.0, depth)));
                var folia = Math.Sin((vertical + 0.62) * Math.PI * 13.0) * 0.018 * (0.45 + (0.55 * Math.Max(0.0, depth)));
                var p = new Point3D(
                    center.X + (lateral * MmToRender(48)),
                    center.Y + (vertical * MmToRender(19)) + (vermisRidge * MmToRender(18)),
                    center.Z + (depth * MmToRender(29)) + folia);
                mesh.Positions.Add(p);
                var n = new Vector3D(lateral, vertical + vermisRidge, depth);
                if (n.Length < 1e-6)
                {
                    n = new Vector3D(0, 1, 0);
                }
                n.Normalize();
                mesh.Normals.Add(n);
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }

        AddGridTriangles(mesh, slices + 1, stacks + 1);
        return mesh;
    }

    private static MeshGeometry3D BuildBrainstemReferenceSurfaceMesh(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        var top = ApplyNonCorticalGlobalShift(
            ApplyEncephalonOffset(
                ShiftSuperior(
                    ScalePointBySubcorticalRatio(MmToRender(new Point3D(0.0, -25.5, -25.0))),
                    MmToRender(15),
                    0.18),
                "Pons",
                "M"),
            "Pons");
        var bottom = ApplyNonCorticalGlobalShift(
            ApplyEncephalonOffset(
                ShiftSuperior(
                    ScalePointBySubcorticalRatio(MmToRender(new Point3D(0.0, -48.0, -24.0))),
                    MmToRender(20),
                    0.10),
                "SpinalCordMotor",
                "M"),
            "SpinalCordMotor");

        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = stack / (double)stacks;
            var center = LerpPoint(top, bottom, v);
            var pontineBulge = Math.Exp(-Math.Pow(v - 0.22, 2) / 0.025);
            var medullaTaper = 1.0 - (0.34 * v);
            var rx = MmToRender(7.2) * medullaTaper * (1.0 + (0.55 * pontineBulge));
            var rz = MmToRender(6.6) * medullaTaper * (1.0 + (0.42 * pontineBulge));
            for (var slice = 0; slice <= slices; slice++)
            {
                var u = slice / (double)slices;
                var theta = u * Math.PI * 2.0;
                var p = new Point3D(
                    center.X + (Math.Cos(theta) * rx),
                    center.Y,
                    center.Z + (Math.Sin(theta) * rz) + (MmToRender(2.0) * pontineBulge));
                mesh.Positions.Add(p);
                var normal = new Vector3D(Math.Cos(theta), 0.08, Math.Sin(theta));
                normal.Normalize();
                mesh.Normals.Add(normal);
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }

        AddGridTriangles(mesh, slices + 1, stacks + 1);
        return mesh;
    }

    private static void AddGridTriangles(MeshGeometry3D mesh, int columns, int rows)
    {
        for (var row = 0; row < rows - 1; row++)
        {
            for (var col = 0; col < columns - 1; col++)
            {
                var i0 = (row * columns) + col;
                var i1 = i0 + 1;
                var i2 = i0 + columns;
                var i3 = i2 + 1;
                mesh.TriangleIndices.Add(i0); mesh.TriangleIndices.Add(i2); mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i1); mesh.TriangleIndices.Add(i2); mesh.TriangleIndices.Add(i3);
            }
        }
    }

    private static List<Point3D> SampleWorldPoints(IReadOnlyList<Point3D> localPoints, Point3D center, int maxSamples)
    {
        if (localPoints.Count == 0)
        {
            return [];
        }

        var sampleCap = Math.Clamp(maxSamples, 8, 256);
        var stride = Math.Max(1, localPoints.Count / sampleCap);
        var samples = new List<Point3D>(Math.Min(sampleCap, localPoints.Count));
        for (var i = 0; i < localPoints.Count; i += stride)
        {
            var point = localPoints[i];
            samples.Add(new Point3D(center.X + point.X, center.Y + point.Y, center.Z + point.Z));
            if (samples.Count >= sampleCap)
            {
                break;
            }
        }

        if (samples.Count == 0)
        {
            var point = localPoints[0];
            samples.Add(new Point3D(center.X + point.X, center.Y + point.Y, center.Z + point.Z));
        }

        // Always include extrema so camera auto-fit sees the true envelope.
        var minX = localPoints[0];
        var maxX = localPoints[0];
        var minY = localPoints[0];
        var maxY = localPoints[0];
        var minZ = localPoints[0];
        var maxZ = localPoints[0];

        for (var i = 1; i < localPoints.Count; i++)
        {
            var p = localPoints[i];
            if (p.X < minX.X) minX = p;
            if (p.X > maxX.X) maxX = p;
            if (p.Y < minY.Y) minY = p;
            if (p.Y > maxY.Y) maxY = p;
            if (p.Z < minZ.Z) minZ = p;
            if (p.Z > maxZ.Z) maxZ = p;
        }

        var extrema = new[] { minX, maxX, minY, maxY, minZ, maxZ };
        foreach (var p in extrema)
        {
            var world = new Point3D(center.X + p.X, center.Y + p.Y, center.Z + p.Z);
            if (!samples.Any(existing => (existing - world).LengthSquared < 1e-8))
            {
                samples.Add(world);
            }
        }

        return samples;
    }

    private static MeshGeometry3D BuildEllipsoidMesh(Point3D center, double rx, double ry, double rz, int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();

        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = stack / (double)stacks;
            var phi = (v * Math.PI) - (Math.PI / 2.0);

            for (var slice = 0; slice <= slices; slice++)
            {
                var u = slice / (double)slices;
                var theta = u * (2.0 * Math.PI);

                var x = center.X + (Math.Cos(theta) * Math.Cos(phi) * rx);
                var y = center.Y + (Math.Sin(phi) * ry);
                var z = center.Z + (Math.Sin(theta) * Math.Cos(phi) * rz);

                mesh.Positions.Add(new Point3D(x, y, z));
                var normal = new Vector3D((x - center.X) / rx, (y - center.Y) / ry, (z - center.Z) / rz);
                normal.Normalize();
                mesh.Normals.Add(normal);
                mesh.TextureCoordinates.Add(new Point(u, v));
            }
        }

        for (var stack = 0; stack < stacks; stack++)
        {
            for (var slice = 0; slice < slices; slice++)
            {
                var rowA = stack * (slices + 1);
                var rowB = (stack + 1) * (slices + 1);

                var i0 = rowA + slice;
                var i1 = i0 + 1;
                var i2 = rowB + slice;
                var i3 = i2 + 1;

                mesh.TriangleIndices.Add(i0); mesh.TriangleIndices.Add(i2); mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i1); mesh.TriangleIndices.Add(i2); mesh.TriangleIndices.Add(i3);
            }
        }

        return mesh;
    }

    private static MeshGeometry3D BuildRepeatedMesh(MeshGeometry3D sourceMesh, IReadOnlyList<Point3D> centers)
    {
        var mesh = new MeshGeometry3D();
        if (sourceMesh.Positions.Count == 0 || centers.Count == 0)
        {
            return mesh;
        }

        var sourcePositionCount = sourceMesh.Positions.Count;
        var hasNormals = sourceMesh.Normals.Count == sourcePositionCount;
        var hasTextureCoordinates = sourceMesh.TextureCoordinates.Count == sourcePositionCount;

        foreach (var center in centers)
        {
            var offset = mesh.Positions.Count;
            for (var i = 0; i < sourcePositionCount; i++)
            {
                var p = sourceMesh.Positions[i];
                mesh.Positions.Add(new Point3D(center.X + p.X, center.Y + p.Y, center.Z + p.Z));
                if (hasNormals)
                {
                    mesh.Normals.Add(sourceMesh.Normals[i]);
                }

                if (hasTextureCoordinates)
                {
                    mesh.TextureCoordinates.Add(sourceMesh.TextureCoordinates[i]);
                }
            }

            foreach (var index in sourceMesh.TriangleIndices)
            {
                mesh.TriangleIndices.Add(offset + index);
            }
        }

        return mesh;
    }

    private static IEnumerable<MeshGeometry3D> BuildRepeatedMeshes(MeshGeometry3D sourceMesh, IReadOnlyList<Point3D> centers, int maxCentersPerMesh)
    {
        if (sourceMesh.Positions.Count == 0 || centers.Count == 0)
        {
            yield break;
        }

        var chunkSize = Math.Clamp(maxCentersPerMesh, 1, Math.Max(1, centers.Count));
        for (var offset = 0; offset < centers.Count; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, centers.Count - offset);
            var chunk = new List<Point3D>(count);
            for (var i = 0; i < count; i++)
            {
                chunk.Add(centers[offset + i]);
            }

            yield return BuildRepeatedMesh(sourceMesh, chunk);
        }
    }

    private static MeshGeometry3D BuildTubeMesh(Point3D start, Point3D end, double radius, int segments)
    {
        var mesh = new MeshGeometry3D();
        var axis = end - start;
        if (axis.Length < 0.001) return mesh;

        axis.Normalize();
        var up = Math.Abs(Vector3D.DotProduct(axis, new Vector3D(0, 1, 0))) > 0.95 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var right = Vector3D.CrossProduct(axis, up); right.Normalize();
        var forward = Vector3D.CrossProduct(right, axis); forward.Normalize();

        for (var i = 0; i <= segments; i++)
        {
            var angle = (i / (double)segments) * Math.PI * 2.0;
            var ring = (right * Math.Cos(angle) * radius) + (forward * Math.Sin(angle) * radius);
            mesh.Positions.Add(new Point3D(start.X + ring.X, start.Y + ring.Y, start.Z + ring.Z));
            mesh.Positions.Add(new Point3D(end.X + ring.X, end.Y + ring.Y, end.Z + ring.Z));
            ring.Normalize();
            mesh.Normals.Add(ring);
            mesh.Normals.Add(ring);
        }

        for (var i = 0; i < segments; i++)
        {
            var a = i * 2; var b = a + 1; var c = a + 2; var d = a + 3;
            mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(d);
        }

        return mesh;
    }
}
