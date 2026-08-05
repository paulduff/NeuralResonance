using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NRE.WpfEditor;

// Static mesh-builder helpers for the brain visualization.
// Pure functions: take inputs, produce MeshGeometry3D / Material additions.
// Extracted from MainWindow.xaml.cs to consolidate geometry construction.
public partial class MainWindow
{
    private static readonly SpecularMaterial NeuralStructureSpecularMaterial =
        CreateFrozenSpecularMaterial(Color.FromRgb(214, 224, 242), 0.18, 38.0);

    private static void AddReferenceMesh(Model3DGroup root, MeshGeometry3D mesh, Color diffuseColor, Color emissiveColor)
    {
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var material = CreateFrozenSurfaceMaterial(diffuseColor, emissiveColor, 0.22, 44.0);
        root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static MaterialGroup CreateFrozenSurfaceMaterial(
        Color diffuseColor,
        Color emissiveColor,
        double specularOpacity,
        double specularPower)
    {
        // Keep alpha in one place. Using both an ARGB brush and Brush.Opacity
        // multiplies transparency and made the anatomical shell almost disappear.
        var diffuse = new SolidColorBrush(Color.FromRgb(diffuseColor.R, diffuseColor.G, diffuseColor.B))
        {
            Opacity = diffuseColor.A / 255.0
        };
        var emissive = new SolidColorBrush(Color.FromRgb(emissiveColor.R, emissiveColor.G, emissiveColor.B))
        {
            Opacity = emissiveColor.A / 255.0
        };
        diffuse.Freeze();
        emissive.Freeze();

        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));
        material.Children.Add(CreateFrozenSpecularMaterial(Color.FromRgb(246, 224, 224), specularOpacity, specularPower));
        TryFreeze(material);
        return material;
    }

    private static SpecularMaterial CreateFrozenSpecularMaterial(Color color, double opacity, double power)
    {
        var brush = new SolidColorBrush(color) { Opacity = Math.Clamp(opacity, 0.0, 1.0) };
        brush.Freeze();
        var material = new SpecularMaterial(brush, Math.Max(1.0, power));
        material.Freeze();
        return material;
    }

    private static MeshGeometry3D BuildNeuronMarkerMesh(double radius)
    {
        // An octahedron is sufficient at neuron-display scale: 6 vertices and 8
        // faces instead of the 42 vertices and 60 faces in the old sphere proxy.
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(new Point3D(0, radius, 0));
        mesh.Positions.Add(new Point3D(0, -radius, 0));
        mesh.Positions.Add(new Point3D(radius, 0, 0));
        mesh.Positions.Add(new Point3D(-radius, 0, 0));
        mesh.Positions.Add(new Point3D(0, 0, radius));
        mesh.Positions.Add(new Point3D(0, 0, -radius));

        foreach (var point in mesh.Positions)
        {
            var normal = new Vector3D(point.X, point.Y, point.Z);
            normal.Normalize();
            mesh.Normals.Add(normal);
            mesh.TextureCoordinates.Add(new Point(0.5 + (point.X / (radius * 2.0)), 0.5 + (point.Z / (radius * 2.0))));
        }

        var faces = new[]
        {
            (0, 2, 4), (0, 4, 3), (0, 3, 5), (0, 5, 2),
            (1, 4, 2), (1, 3, 4), (1, 5, 3), (1, 2, 5)
        };
        foreach (var (a, b, c) in faces)
        {
            mesh.TriangleIndices.Add(a);
            mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(c);
        }

        return mesh;
    }

    private static void AddCorticalGyrusSurface(Model3DGroup root, string snapshotId, string hemisphere, Color baseColor)
    {
        var hemisphereSign = hemisphere == "L" ? -1.0 : 1.0;
        var mesh = BuildCorticalGyrusSurfaceMesh(snapshotId, hemisphereSign, 48, 10);
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var diffuseBase = BrightenPreserveHue(baseColor, 0.24);
        var emissiveBase = BrightenPreserveHue(baseColor, 0.70);
        var material = CreateFrozenSurfaceMaterial(
            Color.FromArgb(142, diffuseBase.R, diffuseBase.G, diffuseBase.B),
            Color.FromArgb(18, emissiveBase.R, emissiveBase.G, emissiveBase.B),
            0.10,
            34.0);
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
        IReadOnlyList<Point3D> localPoints,
        AtlasGeometry? atlasGeometry)
    {
        if (localPoints.Count == 0)
        {
            return;
        }

        var bounds = atlasGeometry is null
            ? ComputeLocalBounds(localPoints)
            : BuildAtlasLocalBounds(atlasGeometry);
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
            AddGuideMesh(root, BuildBasalGangliaGuideMesh(bounds, snapshotId, hemisphere), diffuseColor, emissiveColor);
            AddBasalGangliaGuideLines(root, bounds, snapshotId, lineColor, hemisphere);
            return;
        }

        if (IsCerebellarGuide(snapshotId))
        {
            // Granule, Purkinje, and lobular records are functional layers of
            // one cerebellum, not three nested organs. The shared anatomical
            // reference shell and folia already show the outer envelope.
            if (snapshotId is "CerebellarGranule" or "PurkinjeCellLayer" or "CerebellarLobules")
            {
                return;
            }

            AddGuideMesh(root, BuildCerebellarLocalGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
            AddCerebellarFoliaLines(root, bounds, snapshotId, lineColor);
            return;
        }

        if (IsBrainstemGuide(snapshotId))
        {
            AddGuideMesh(root, BuildBrainstemLocalGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
            AddBrainstemGuideLines(root, bounds, snapshotId, lineColor);
            return;
        }

        AddGuideMesh(root, BuildGenericSubcorticalGuideMesh(bounds, snapshotId), diffuseColor, emissiveColor);
    }

    private static void AddGuideMesh(Model3DGroup root, MeshGeometry3D mesh, Color diffuseColor, Color emissiveColor)
    {
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var material = CreateFrozenSurfaceMaterial(diffuseColor, emissiveColor, 0.15, 28.0);
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
        var material = CreateFrozenSurfaceMaterial(color, Color.FromArgb((byte)Math.Min(40, (int)color.A), color.R, color.G, color.B), 0.08, 18.0);
        root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static MeshGeometry3D BuildThalamicGuideMesh(LocalBounds b, string snapshotId)
    {
        return BuildEllipsoidMesh(
            b.Center,
            Math.Max(0.004, b.RadiusX),
            Math.Max(0.004, b.RadiusY),
            Math.Max(0.004, b.RadiusZ),
            24,
            12);
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

    private static MeshGeometry3D BuildBasalGangliaGuideMesh(LocalBounds b, string snapshotId, string hemisphere)
    {
        if (snapshotId.Equals("Striatum", StringComparison.OrdinalIgnoreCase))
        {
            return BuildStriatalGuideMesh(hemisphere);
        }

        return BuildEllipsoidMesh(
            b.Center,
            Math.Max(0.004, b.RadiusX),
            Math.Max(0.004, b.RadiusY),
            Math.Max(0.004, b.RadiusZ),
            22,
            11);
    }

    private static MeshGeometry3D BuildStriatalGuideMesh(string hemisphere)
    {
        var mesh = new MeshGeometry3D();
        var left = hemisphere.Equals("L", StringComparison.OrdinalIgnoreCase);
        var side = left ? -1.0 : 1.0;

        // Putamen centroid and extents measured independently inside the CIT168
        // striatal envelope. Coordinates are local to the combined striatum.
        var putamenCenter = new Point3D(
            MmToRender(left ? -4.93 : 4.99),
            MmToRender(left ? -3.38 : -3.53),
            MmToRender(-2.91));
        AppendMesh(
            mesh,
            BuildEllipsoidMesh(
                putamenCenter,
                MmToRender(9.8),
                MmToRender(left ? 15.05 : 14.0),
                MmToRender(left ? 21.7 : 21.35),
                24,
                12));

        // The caudate is a C-shaped nucleus, not the second half of an oval.
        // Overlapping low-poly lobules preserve that course while remaining one
        // frozen mesh and therefore one WPF draw model.
        const int segments = 11;
        for (var i = 0; i < segments; i++)
        {
            var t = i / (double)(segments - 1);
            var angle = t * Math.PI;
            var localCenter = new Point3D(
                MmToRender((-side * 5.6) - (side * 3.0 * Math.Sin(angle))),
                MmToRender(4.0 + (5.0 * Math.Cos(angle)) - (3.0 * t)),
                MmToRender(28.0 - (52.0 * t)));
            var headWeight = 1.0 - t;
            var radiusX = MmToRender(2.8 + (2.6 * headWeight));
            var radiusY = MmToRender(3.0 + (3.8 * headWeight));
            var radiusZ = MmToRender(3.8 + (2.2 * headWeight));
            AppendMesh(mesh, BuildEllipsoidMesh(localCenter, radiusX, radiusY, radiusZ, 14, 7));
        }

        return mesh;
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

    private static MeshGeometry3D BuildGenericSubcorticalGuideMesh(LocalBounds bounds, string snapshotId)
    {
        var stacks = snapshotId is "Habenula" or "LocusCoeruleus" ? 8 : 11;
        var slices = snapshotId is "Habenula" or "LocusCoeruleus" ? 16 : 22;
        return BuildEllipsoidMesh(
            bounds.Center,
            Math.Max(0.004, bounds.RadiusX),
            Math.Max(0.004, bounds.RadiusY),
            Math.Max(0.004, bounds.RadiusZ),
            slices,
            stacks);
    }

    private static LocalBounds BuildAtlasLocalBounds(AtlasGeometry geometry)
    {
        var radiusX = Math.Max(0.001, MmToRender(geometry.DimensionsMm.X) * 0.5);
        var radiusY = Math.Max(0.001, MmToRender(geometry.DimensionsMm.Y) * 0.5);
        var radiusZ = Math.Max(0.001, MmToRender(geometry.DimensionsMm.Z) * 0.5);
        var center = new Point3D();
        return new LocalBounds(
            -radiusX,
            radiusX,
            -radiusY,
            radiusY,
            -radiusZ,
            radiusZ,
            center,
            radiusX,
            radiusY,
            radiusZ);
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
            // M1/S1 somatotopy runs inferior-to-superior along the precentral
            // and postcentral strips. Rows span the narrow AP width of the gyrus.
            var localTheta = rows <= 1
                ? 0.0
                : -0.58 + (1.16 * row / (double)(rows - 1));
            for (var col = 0; col < columns; col++)
            {
                var t = columns <= 1 ? 0.5 : col / (double)(columns - 1);
                var along = alongStart + ((alongEnd - alongStart) * t);
                var localPhi = -0.92 + (1.84 * along);
                var jitter = DeterministicJitter(col, row, 0, seed) * 0.012;
                var point = BuildCorticalTerritoryPointUnchecked(
                    snapshotId,
                    localTheta + jitter.X,
                    localPhi + jitter.Z,
                    MmToRender(0.55),
                    hemisphereSign);
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
        return BuildCorticalTerritorySurfaceMesh(snapshotId, hemisphereSign, columns, rows);
    }

    private static CorpusCallosumVisual AddCorpusCallosumPathwayScaffold(Model3DGroup root)
    {
        var baseColor = Color.FromRgb(170, 204, 246);
        var diffuse = new SolidColorBrush(ScaleColor(baseColor, 0.18)) { Opacity = 0.18 };
        var emissive = new SolidColorBrush(Color.FromArgb(40, 210, 232, 255)) { Opacity = 0.06 };
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));

        var combinedMesh = new MeshGeometry3D();
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
                    AppendMesh(combinedMesh, mesh);
                }
            }
        }

        if (combinedMesh.Positions.Count > 0)
        {
            TryFreeze(combinedMesh);
            root.Children.Add(new GeometryModel3D(combinedMesh, material) { BackMaterial = material });
        }

        return new CorpusCallosumVisual(baseColor, diffuse, emissive);
    }

    private static Point3D GetCorpusCallosumScaffoldPoint(double u, double v)
    {
        var center = GetCanonicalAtlasCenter("CorpusCallosum", "M");

        var rx = MmToRender(34.0);
        var ry = MmToRender(7.0);
        var rz = MmToRender(32.0);
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
        const double thetaMin = -1.52;
        const double thetaMax = 1.52;
        const double phiMin = -1.52;
        const double phiMax = 1.52;

        for (var p = 0; p <= phiSteps; p++)
        {
            var phi = phiMin + ((phiMax - phiMin) * (p / (double)phiSteps));
            for (var t = 0; t <= thetaSteps; t++)
            {
                var theta = thetaMin + ((thetaMax - thetaMin) * (t / (double)thetaSteps));
                var point = BuildFoldedCorticalReferencePoint(
                    theta,
                    phi,
                    hemisphereSign,
                    t / (double)thetaSteps,
                    p / (double)phiSteps);
                mesh.Positions.Add(point);
                mesh.TextureCoordinates.Add(new Point(t / (double)thetaSteps, p / (double)phiSteps));
            }
        }

        AddGridTriangles(mesh, thetaSteps + 1, phiSteps + 1);
        CalculateSmoothNormals(mesh, hemisphereSign);
        return mesh;
    }

    private static Point3D BuildFoldedCorticalReferencePoint(
        double theta,
        double phi,
        double hemisphereSign,
        double normalizedTheta,
        double normalizedPhi)
    {
        var surface = BuildCorticalSurfacePoint(theta, phi, hemisphereSign);
        var normal = GetCorticalShellNormal(surface, hemisphereSign < 0 ? "L" : "R");

        static double SmoothStep(double value)
        {
            var t = Math.Clamp(value, 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }

        var edgeFade =
            SmoothStep(Math.Min(normalizedTheta, 1.0 - normalizedTheta) / 0.08) *
            SmoothStep(Math.Min(normalizedPhi, 1.0 - normalizedPhi) / 0.10);
        var lateralVisibility = Math.Clamp(Math.Abs(surface.X) / Math.Max(0.001, MmToRender(38.0)), 0.28, 1.0);
        var primary = Math.Sin((theta * 9.2) + (phi * 5.1) + (hemisphereSign * 0.35));
        var secondary = Math.Sin((theta * 15.7) - (phi * 8.4) + 1.15);
        var tertiary = Math.Sin((theta * 24.3) + (phi * 13.1) - (hemisphereSign * 0.70));
        var reliefMm = ((primary * 2.25) + (secondary * 1.05) + (tertiary * 0.48)) *
                       (0.48 + (0.52 * lateralVisibility)) * edgeFade;

        // Major sulci are impressed into the shell; separate dark landmark
        // meshes trace them above the surface so they remain visible at a glance.
        var centralSulcus = Math.Exp(-Math.Pow(theta + (0.03 * Math.Sin(phi * 2.2)), 2) / 0.010) *
                            Math.Clamp((phi + 0.12) / 0.95, 0.0, 1.0);
        var sylvianProgress = Math.Clamp((theta + 1.16) / 1.92, 0.0, 1.0);
        var sylvianPhi = 0.08 - (0.28 * sylvianProgress) + (0.025 * Math.Sin(sylvianProgress * Math.PI * 2.0));
        var sylvianFissure = Math.Exp(-Math.Pow(phi - sylvianPhi, 2) / 0.0045) *
                              Math.Clamp((theta + 1.30) / 0.24, 0.0, 1.0) *
                              Math.Clamp((0.88 - theta) / 0.18, 0.0, 1.0);
        var parietoOccipital = Math.Exp(-Math.Pow(theta + 1.20 - (0.06 * Math.Sin(phi * 2.0)), 2) / 0.014) *
                                 Math.Clamp((phi - 0.20) / 0.90, 0.0, 1.0);
        reliefMm -= (centralSulcus * 2.8) + (sylvianFissure * 2.5) + (parietoOccipital * 2.0);

        var depth = MmToRender(reliefMm);
        return new Point3D(
            surface.X + (normal.X * depth),
            surface.Y + (normal.Y * depth),
            surface.Z + (normal.Z * depth));
    }

    private static void AddAnatomicalLandmarks(Model3DGroup root)
    {
        var corticalLandmarks = new MeshGeometry3D();
        AppendMesh(corticalLandmarks, BuildCorticalLandmarkMesh(-1.0));
        AppendMesh(corticalLandmarks, BuildCorticalLandmarkMesh(1.0));
        AddLandmarkMesh(
            root,
            corticalLandmarks,
            Color.FromArgb(172, 73, 42, 52),
            Color.FromArgb(18, 210, 116, 132));

        AddLandmarkMesh(
            root,
            BuildCerebellarFoliaLandmarkMesh(),
            Color.FromArgb(152, 68, 39, 47),
            Color.FromArgb(14, 196, 104, 120));

        AddLandmarkMesh(
            root,
            BuildBrainstemBoundaryLandmarkMesh(),
            Color.FromArgb(142, 78, 47, 37),
            Color.FromArgb(12, 204, 126, 94));
    }

    private static void AddLandmarkMesh(Model3DGroup root, MeshGeometry3D mesh, Color diffuseColor, Color emissiveColor)
    {
        if (mesh.Positions.Count == 0)
        {
            return;
        }

        TryFreeze(mesh);
        var diffuse = new SolidColorBrush(Color.FromRgb(diffuseColor.R, diffuseColor.G, diffuseColor.B))
        {
            Opacity = diffuseColor.A / 255.0
        };
        var emissive = new SolidColorBrush(Color.FromRgb(emissiveColor.R, emissiveColor.G, emissiveColor.B))
        {
            Opacity = emissiveColor.A / 255.0
        };
        diffuse.Freeze();
        emissive.Freeze();
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(diffuse));
        material.Children.Add(new EmissiveMaterial(emissive));
        TryFreeze(material);
        root.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
    }

    private static MeshGeometry3D BuildCorticalLandmarkMesh(double hemisphereSign)
    {
        var mesh = new MeshGeometry3D();
        const int samples = 38;
        var radius = MmToRender(0.62);

        AppendCorticalLandmarkCurve(
            mesh,
            hemisphereSign,
            samples,
            radius,
            static s =>
            {
                var phi = -0.02 + (1.24 * s);
                var theta = -0.01 - (0.10 * Math.Sin(s * Math.PI)) + (0.025 * Math.Sin(s * Math.PI * 3.0));
                return (theta, phi);
            });
        AppendCorticalLandmarkCurve(
            mesh,
            hemisphereSign,
            samples,
            radius,
            static s =>
            {
                var theta = -1.16 + (1.92 * s);
                var phi = 0.08 - (0.28 * s) + (0.025 * Math.Sin(s * Math.PI * 2.0));
                return (theta, phi);
            });
        AppendCorticalLandmarkCurve(
            mesh,
            hemisphereSign,
            34,
            radius * 0.72,
            static s =>
            {
                var theta = -1.04 + (1.76 * s);
                var phi = -0.31 - (0.09 * s) + (0.022 * Math.Sin((s * Math.PI * 2.2) + 0.4));
                return (theta, phi);
            });
        AppendCorticalLandmarkCurve(
            mesh,
            hemisphereSign,
            32,
            radius * 0.66,
            static s =>
            {
                var theta = -0.94 + (1.54 * s);
                var phi = -0.58 - (0.06 * s) + (0.018 * Math.Sin((s * Math.PI * 2.0) - 0.3));
                return (theta, phi);
            });
        AppendCorticalLandmarkCurve(
            mesh,
            hemisphereSign,
            28,
            radius * 0.88,
            static s =>
            {
                var phi = 0.30 + (0.84 * s);
                var theta = -1.21 + (0.075 * Math.Sin(s * Math.PI * 1.35));
                return (theta, phi);
            });
        AppendCorticalLandmarkCurve(
            mesh,
            hemisphereSign,
            30,
            radius * 0.82,
            static s =>
            {
                var theta = -1.48 + (0.50 * s);
                var phi = 0.16 + (0.09 * Math.Sin(s * Math.PI));
                return (theta, phi);
            });

        return mesh;
    }

    private static void AppendCorticalLandmarkCurve(
        MeshGeometry3D destination,
        double hemisphereSign,
        int samples,
        double radius,
        Func<double, (double Theta, double Phi)> curve)
    {
        Point3D? previous = null;
        for (var i = 0; i <= samples; i++)
        {
            var s = i / (double)samples;
            var (theta, phi) = curve(s);
            var point = BuildFoldedCorticalReferencePoint(
                theta,
                phi,
                hemisphereSign,
                Math.Clamp((theta + 1.52) / 3.04, 0.0, 1.0),
                Math.Clamp((phi + 1.02) / 2.36, 0.0, 1.0));
            var shell = BuildCorticalSurfacePoint(theta, phi, hemisphereSign);
            var normal = GetCorticalShellNormal(shell, hemisphereSign < 0 ? "L" : "R");
            point = new Point3D(
                point.X + (normal.X * MmToRender(0.20)),
                point.Y + (normal.Y * MmToRender(0.20)),
                point.Z + (normal.Z * MmToRender(0.20)));

            if (previous is Point3D start)
            {
                AppendMesh(destination, BuildTubeMesh(start, point, radius, 6));
            }

            previous = point;
        }
    }

    private static MeshGeometry3D BuildCerebellarFoliaLandmarkMesh()
    {
        var mesh = new MeshGeometry3D();
        const int slices = 44;
        for (var band = 1; band <= 12; band++)
        {
            var phi = -1.20 + (band * (2.30 / 13.0));
            Point3D? previous = null;
            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = slice * Math.PI * 2.0 / slices;
                var point = BuildCerebellarReferencePoint(theta, phi);
                if (previous is Point3D start)
                {
                    AppendMesh(mesh, BuildTubeMesh(start, point, MmToRender(0.42), 5));
                }
                previous = point;
            }
        }

        return mesh;
    }

    private static MeshGeometry3D BuildBrainstemBoundaryLandmarkMesh()
    {
        var mesh = new MeshGeometry3D();
        const int slices = 30;
        foreach (var v in new[] { 0.36, 0.68 })
        {
            Point3D? previous = null;
            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = slice * Math.PI * 2.0 / slices;
                var point = BuildBrainstemReferencePoint(theta, v);
                if (previous is Point3D start)
                {
                    AppendMesh(mesh, BuildTubeMesh(start, point, MmToRender(0.38), 5));
                }
                previous = point;
            }
        }

        return mesh;
    }

    private static MeshGeometry3D BuildCorpusCallosumReferenceSurfaceMesh(int lengthSteps, int widthSteps)
    {
        var mesh = new MeshGeometry3D();
        var center = GetCanonicalAtlasCenter("CorpusCallosum", "M");
        TryGetSubcorticalAtlasGeometry("CorpusCallosum", "M", out var atlasGeometry);
        var rx = MmToRender(atlasGeometry?.DimensionsMm.X * 0.5 ?? 34.0);
        var ry = MmToRender(atlasGeometry?.DimensionsMm.Y * 0.5 ?? 7.0);
        var rz = MmToRender(atlasGeometry?.DimensionsMm.Z * 0.5 ?? 39.0);

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
                var p = BuildCerebellarReferencePoint(theta, phi);
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

    private static Point3D BuildCerebellarReferencePoint(double theta, double phi)
    {
        var center = GetCanonicalAtlasCenter("CerebellarLobules", "M");
        TryGetSubcorticalAtlasGeometry("CerebellarLobules", "M", out var atlasGeometry);
        // AAL label bounds include sparse extreme voxels. A slightly inset
        // envelope better represents the visible cerebellar surface while the
        // functional samples retain their full atlas coordinates.
        var radiusX = MmToRender(atlasGeometry?.DimensionsMm.X * 0.46 ?? 56.0);
        var radiusY = MmToRender(atlasGeometry?.DimensionsMm.Y * 0.44 ?? 28.0);
        var radiusZ = MmToRender(atlasGeometry?.DimensionsMm.Z * 0.44 ?? 31.0);
        var lateral = Math.Cos(theta) * Math.Cos(phi);
        var vertical = Math.Sin(phi);
        var depth = Math.Sin(theta) * Math.Cos(phi);
        var vermisRidge = 0.10 * Math.Exp(-(lateral * lateral) / 0.045) * (0.55 + (0.45 * Math.Max(0.0, depth)));
        var folia = Math.Sin((vertical + 0.62) * Math.PI * 13.0) * MmToRender(0.72) * (0.45 + (0.55 * Math.Max(0.0, depth)));
        return new Point3D(
            center.X + (lateral * radiusX),
            center.Y + (vertical * radiusY * 0.94) + (vermisRidge * radiusY * 0.16),
            center.Z + (depth * radiusZ) + folia);
    }

    private static MeshGeometry3D BuildBrainstemReferenceSurfaceMesh(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        for (var stack = 0; stack <= stacks; stack++)
        {
            var v = stack / (double)stacks;
            for (var slice = 0; slice <= slices; slice++)
            {
                var u = slice / (double)slices;
                var theta = u * Math.PI * 2.0;
                var p = BuildBrainstemReferencePoint(theta, v);
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

    private static Point3D BuildBrainstemReferencePoint(double theta, double v)
    {
        var top = GetCanonicalAtlasCenter("Pons", "M");
        var bottom = GetCanonicalAtlasCenter("SpinalCordMotor", "M");
        var center = LerpPoint(top, bottom, v);
        var pontineBulge = Math.Exp(-Math.Pow(v - 0.22, 2) / 0.025);
        var medullaTaper = 1.0 - (0.34 * v);
        var rx = MmToRender(7.2) * medullaTaper * (1.0 + (0.55 * pontineBulge));
        var rz = MmToRender(6.6) * medullaTaper * (1.0 + (0.42 * pontineBulge));
        return new Point3D(
            center.X + (Math.Cos(theta) * rx),
            center.Y,
            center.Z + (Math.Sin(theta) * rz) + (MmToRender(2.0) * pontineBulge));
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

    private static void CalculateSmoothNormals(MeshGeometry3D mesh, double hemisphereSign)
    {
        var accumulated = new Vector3D[mesh.Positions.Count];
        for (var i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
        {
            var i0 = mesh.TriangleIndices[i];
            var i1 = mesh.TriangleIndices[i + 1];
            var i2 = mesh.TriangleIndices[i + 2];
            var normal = Vector3D.CrossProduct(mesh.Positions[i1] - mesh.Positions[i0], mesh.Positions[i2] - mesh.Positions[i0]);
            if (normal.LengthSquared <= 1e-12)
            {
                continue;
            }

            accumulated[i0] += normal;
            accumulated[i1] += normal;
            accumulated[i2] += normal;
        }

        mesh.Normals.Clear();
        for (var i = 0; i < accumulated.Length; i++)
        {
            var normal = accumulated[i];
            if (normal.LengthSquared <= 1e-12)
            {
                normal = new Vector3D(hemisphereSign, 0.0, 0.0);
            }
            else
            {
                normal.Normalize();
                var radial = new Vector3D(mesh.Positions[i].X, mesh.Positions[i].Y, mesh.Positions[i].Z);
                if (Vector3D.DotProduct(normal, radial) < 0.0)
                {
                    normal *= -1.0;
                }
            }

            mesh.Normals.Add(normal);
        }
    }

    private static void AppendMesh(MeshGeometry3D destination, MeshGeometry3D source)
    {
        if (source.Positions.Count == 0)
        {
            return;
        }

        var offset = destination.Positions.Count;
        var hasNormals = source.Normals.Count == source.Positions.Count;
        var hasTextureCoordinates = source.TextureCoordinates.Count == source.Positions.Count;
        for (var i = 0; i < source.Positions.Count; i++)
        {
            destination.Positions.Add(source.Positions[i]);
            if (hasNormals)
            {
                destination.Normals.Add(source.Normals[i]);
            }
            if (hasTextureCoordinates)
            {
                destination.TextureCoordinates.Add(source.TextureCoordinates[i]);
            }
        }

        for (var i = 0; i < source.TriangleIndices.Count; i++)
        {
            destination.TriangleIndices.Add(offset + source.TriangleIndices[i]);
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
        => BuildRepeatedMesh(sourceMesh, centers, 0, centers.Count);

    private static MeshGeometry3D BuildRepeatedMesh(
        MeshGeometry3D sourceMesh,
        IReadOnlyList<Point3D> centers,
        int centerOffset,
        int centerCount)
    {
        var mesh = new MeshGeometry3D();
        if (sourceMesh.Positions.Count == 0 || centerCount <= 0)
        {
            return mesh;
        }

        var sourcePositionCount = sourceMesh.Positions.Count;
        var hasNormals = sourceMesh.Normals.Count == sourcePositionCount;
        var hasTextureCoordinates = sourceMesh.TextureCoordinates.Count == sourcePositionCount;

        var centerEnd = Math.Min(centers.Count, centerOffset + centerCount);
        for (var centerIndex = centerOffset; centerIndex < centerEnd; centerIndex++)
        {
            var center = centers[centerIndex];
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
            yield return BuildRepeatedMesh(sourceMesh, centers, offset, count);
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
