using System.Windows.Media.Media3D;

namespace NRE.WpfEditor;

// Representative adult atlas geometry in MNI-style millimetres. The editor's
// render axes are lateral (X), superior (Y), and anterior (Z). Left and right
// measurements remain separate so normal atlas asymmetry is not discarded.
public partial class MainWindow
{
    private const string Cit168Source = "Pauli/CIT168 deterministic atlas, MNI space";
    private const string Aal3Source = "AAL3 v1 1 mm atlas, MNI space";
    private const string HarvardOxfordSource = "Harvard-Oxford subcortical max-probability 25% 1 mm atlas";
    private const string AtlasGuidedSource = "MNI152 atlas-guided representative adult bounds";

    private static readonly Dictionary<string, SubcorticalAtlasProfile> SubcorticalAtlasProfiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Commissural and basal forebrain anatomy.
            ["CorpusCallosum"] = Midline(0.0, 24.0, -2.0, 68.0, 14.0, 78.0, AtlasGuidedSource),
            ["Striatum"] = Bilateral(
                Geometry(-17.93, 3.41, 3.35, 30.1, 38.5, 60.2, Cit168Source),
                Geometry(18.49, 4.11, 4.02, 30.1, 38.5, 60.2, Cit168Source)),
            ["NucleusAccumbens"] = Bilateral(
                Geometry(-8.35, -9.05, 7.36, 12.6, 11.2, 8.4, Cit168Source),
                Geometry(8.75, -8.50, 7.96, 12.6, 9.8, 9.1, Cit168Source)),
            ["GlobusPallidus"] = Bilateral(
                Geometry(-16.17, -1.98, -2.75, 16.1, 14.0, 20.3, Cit168Source),
                Geometry(16.76, -1.46, -2.13, 16.1, 14.0, 20.3, Cit168Source)),
            ["VentralPallidum"] = Bilateral(
                Geometry(-10.15, -8.14, 2.35, 10.5, 4.2, 5.6, Cit168Source),
                Geometry(10.89, -7.52, 3.09, 9.8, 4.2, 6.3, Cit168Source)),
            ["GPe"] = Bilateral(
                Geometry(-16.73, -0.78, -1.49, 16.1, 11.9, 20.3, Cit168Source),
                Geometry(17.36, -0.18, -0.84, 16.1, 11.2, 20.3, Cit168Source)),
            ["GPi"] = Bilateral(
                Geometry(-15.12, -4.23, -5.10, 13.3, 9.1, 12.6, Cit168Source),
                Geometry(15.70, -3.74, -4.43, 14.0, 9.1, 12.6, Cit168Source)),
            ["Amygdala"] = Bilateral(
                Geometry(-22.57, -18.07, -4.87, 21.0, 20.0, 19.0, HarvardOxfordSource),
                Geometry(22.95, -18.00, -3.71, 22.0, 20.0, 20.0, HarvardOxfordSource)),
            ["BasalForebrain"] = Symmetric(9.0, -8.0, 4.0, 16.0, 10.0, 16.0, AtlasGuidedSource),

            // Thalamic nuclei measured from AAL3; TRN follows the lateral thalamic envelope.
            ["Thalamus"] = Bilateral(
                Geometry(-12.91, 5.05, -18.32, 28.0, 28.0, 36.0, Aal3Source),
                Geometry(12.13, 4.78, -17.33, 28.0, 28.0, 36.0, Aal3Source)),
            ["MotorThalamus"] = Bilateral(
                Geometry(-14.41, 5.90, -14.14, 18.0, 24.0, 30.0, Aal3Source),
                Geometry(13.77, 5.47, -13.01, 20.0, 24.0, 30.0, Aal3Source)),
            ["Trn"] = Bilateral(
                Geometry(-17.0, 4.8, -18.3, 5.0, 27.0, 36.0, AtlasGuidedSource),
                Geometry(16.2, 4.5, -17.3, 5.0, 27.0, 36.0, AtlasGuidedSource)),
            ["Pulvinar"] = Bilateral(
                Geometry(-14.19, 6.06, -27.52, 22.0, 23.0, 19.0, Aal3Source),
                Geometry(13.02, 6.19, -26.26, 22.0, 24.0, 20.0, Aal3Source)),
            ["MediodorsalThalamus"] = Bilateral(
                Geometry(-5.41, 4.46, -15.46, 10.0, 21.0, 20.0, Aal3Source),
                Geometry(4.89, 3.91, -14.81, 13.0, 20.0, 19.0, Aal3Source)),
            ["IntralaminarThalamus"] = Bilateral(
                Geometry(-9.75, -0.30, -18.04, 10.0, 19.0, 19.0, Aal3Source),
                Geometry(9.38, -0.37, -17.05, 11.0, 19.0, 19.0, Aal3Source)),
            ["Hypothalamus"] = Bilateral(
                Geometry(-3.62, -8.97, -4.05, 9.1, 16.1, 16.8, Cit168Source),
                Geometry(4.19, -8.81, -3.69, 9.1, 16.1, 16.8, Cit168Source)),
            ["Habenula"] = Bilateral(
                Geometry(-2.31, 2.15, -23.30, 3.5, 7.7, 5.6, Cit168Source),
                Geometry(2.93, 2.15, -22.78, 3.5, 7.0, 5.6, Cit168Source)),
            ["Stn"] = Bilateral(
                Geometry(-9.33, -6.94, -12.33, 9.1, 6.3, 11.9, Cit168Source),
                Geometry(9.96, -6.69, -11.93, 9.1, 7.0, 11.9, Cit168Source)),

            // Midbrain and neuromodulatory nuclei.
            ["SuperiorColliculus"] = Symmetric(5.5, -3.5, -27.0, 11.0, 8.0, 12.0, AtlasGuidedSource),
            ["InferiorColliculus"] = Symmetric(6.0, -10.0, -32.0, 11.0, 9.0, 12.0, AtlasGuidedSource),
            ["PeriaqueductalGray"] = Midline(0.0, -8.0, -26.0, 10.0, 30.0, 12.0, AtlasGuidedSource),
            ["Snr"] = Bilateral(
                Geometry(-8.59, -12.04, -15.67, 10.5, 10.5, 17.5, Cit168Source),
                Geometry(9.40, -11.75, -15.69, 10.5, 9.8, 17.5, Cit168Source)),
            ["Snc"] = Bilateral(
                Geometry(-7.07, -12.61, -18.85, 9.8, 9.1, 14.0, Cit168Source),
                Geometry(7.84, -12.36, -18.82, 9.1, 9.1, 14.0, Cit168Source)),
            ["Vta"] = Bilateral(
                Geometry(-2.75, -15.21, -20.51, 4.9, 7.7, 9.8, Cit168Source),
                Geometry(3.70, -14.99, -20.45, 4.9, 7.7, 9.8, Cit168Source)),

            // The application models hippocampal subfields separately. Their bounds
            // tile the AAL3 whole-hippocampus envelope rather than each claiming it.
            ["DentateGyrus"] = Symmetric(25.5, -13.5, -25.0, 12.0, 13.0, 24.0, AtlasGuidedSource),
            ["CA3"] = Symmetric(25.8, -12.5, -23.0, 11.0, 12.0, 22.0, AtlasGuidedSource),
            ["CA2"] = Symmetric(26.0, -11.5, -21.5, 8.0, 10.0, 16.0, AtlasGuidedSource),
            ["CA1"] = Symmetric(26.0, -11.5, -20.0, 13.0, 12.0, 24.0, AtlasGuidedSource),
            ["Subiculum"] = Symmetric(25.0, -14.0, -19.0, 12.0, 10.0, 20.0, AtlasGuidedSource),
            ["Presubiculum"] = Symmetric(24.0, -15.0, -18.0, 10.0, 9.0, 18.0, AtlasGuidedSource),
            ["Parasubiculum"] = Symmetric(23.0, -16.0, -17.0, 9.0, 8.0, 16.0, AtlasGuidedSource),

            // Cerebellum, brainstem, and lower nuclei.
            ["CerebellarGranule"] = Midline(2.57, -36.58, -61.36, 116.0, 58.0, 64.0, Aal3Source),
            ["PurkinjeCellLayer"] = Midline(2.57, -36.58, -61.36, 120.0, 62.0, 68.0, Aal3Source),
            ["CerebellarVermis"] = Midline(2.28, -19.49, -58.23, 14.0, 52.0, 48.0, Aal3Source),
            ["CerebellarLobules"] = Midline(2.57, -36.58, -61.36, 122.0, 64.0, 70.0, Aal3Source),
            ["DeepCerebellarNuclei"] = Symmetric(15.0, -33.0, -56.0, 18.0, 12.0, 24.0, AtlasGuidedSource),
            ["Pons"] = Midline(0.5, -31.0, -29.0, 34.0, 28.0, 32.0, AtlasGuidedSource),
            ["CochlearNucleus"] = Symmetric(10.0, -30.0, -37.0, 8.0, 10.0, 12.0, AtlasGuidedSource),
            ["SuperiorOlive"] = Symmetric(6.0, -30.0, -28.0, 8.0, 8.0, 10.0, AtlasGuidedSource),
            ["VestibularNuclei"] = Symmetric(7.0, -28.0, -38.0, 10.0, 15.0, 14.0, AtlasGuidedSource),
            ["NucleusTractusSolitarius"] = Symmetric(4.0, -39.0, -40.0, 7.0, 18.0, 10.0, AtlasGuidedSource),
            ["ReticularFormation"] = Midline(0.0, -34.0, -32.0, 16.0, 48.0, 18.0, AtlasGuidedSource),
            ["InferiorOlive"] = Symmetric(5.0, -43.0, -33.0, 9.0, 20.0, 11.0, AtlasGuidedSource),
            ["Medulla"] = Midline(0.0, -44.0, -33.0, 24.0, 32.0, 24.0, AtlasGuidedSource),
            ["LocusCoeruleus"] = Bilateral(
                Geometry(-4.40, -27.06, -34.98, 4.0, 14.0, 5.0, Aal3Source),
                Geometry(6.02, -27.74, -35.26, 5.0, 14.0, 6.0, Aal3Source)),
            ["RapheNuclei"] = Midline(0.42, -13.89, -27.85, 8.0, 19.0, 9.0, Aal3Source),
            ["SpinalCordMotor"] = Symmetric(4.0, -58.0, -31.0, 8.0, 30.0, 10.0, AtlasGuidedSource),
            ["SomaticAfferents"] = Symmetric(13.0, -58.0, -31.0, 12.0, 30.0, 12.0, AtlasGuidedSource),

            // White-matter and peripheral reference anchors remain in the same
            // physical coordinate system even though they are not deep nuclei.
            ["ArcuateFasciculus"] = Symmetric(42.0, 20.0, -8.0, 20.0, 10.0, 42.0, AtlasGuidedSource),
            ["OlfactoryBulb"] = Symmetric(7.0, -13.0, 42.0, 12.0, 10.0, 22.0, AtlasGuidedSource),
            ["Retina"] = Symmetric(32.0, -32.0, 70.0, 24.0, 24.0, 8.0, AtlasGuidedSource),
            ["Cochlea"] = Symmetric(48.0, -28.0, 8.0, 10.0, 10.0, 10.0, AtlasGuidedSource)
        };

    private static bool TryGetSubcorticalAtlasGeometry(
        string snapshotId,
        string hemisphere,
        out AtlasGeometry geometry)
    {
        if (!SubcorticalAtlasProfiles.TryGetValue(snapshotId, out var profile))
        {
            geometry = default!;
            return false;
        }

        geometry = hemisphere.Equals("L", StringComparison.OrdinalIgnoreCase)
            ? profile.Left
            : hemisphere.Equals("R", StringComparison.OrdinalIgnoreCase)
                ? profile.Right
                : profile.IsMidline
                    ? profile.Left
                    : AverageGeometry(profile.Left, profile.Right);
        return true;
    }

    private static StructureDefinition ApplySubcorticalAtlasGeometry(
        StructureDefinition definition,
        string hemisphere)
    {
        if (!TryGetSubcorticalAtlasGeometry(definition.SnapshotId, hemisphere, out var geometry))
        {
            return definition;
        }

        return definition with
        {
            Center = MmToRender(geometry.CenterMm),
            RadiusX = MmToRender(geometry.DimensionsMm.X),
            RadiusY = MmToRender(geometry.DimensionsMm.Y),
            RadiusZ = MmToRender(geometry.DimensionsMm.Z),
            PitchDeg = 0.0,
            YawDeg = 0.0,
            RollDeg = 0.0
        };
    }

    private static AtlasGeometry AverageGeometry(AtlasGeometry left, AtlasGeometry right)
    {
        return Geometry(
            (left.CenterMm.X + right.CenterMm.X) * 0.5,
            (left.CenterMm.Y + right.CenterMm.Y) * 0.5,
            (left.CenterMm.Z + right.CenterMm.Z) * 0.5,
            Math.Max(left.DimensionsMm.X, right.DimensionsMm.X),
            Math.Max(left.DimensionsMm.Y, right.DimensionsMm.Y),
            Math.Max(left.DimensionsMm.Z, right.DimensionsMm.Z),
            left.Source);
    }

    private static SubcorticalAtlasProfile Bilateral(AtlasGeometry left, AtlasGeometry right) =>
        new(left, right, false);

    private static SubcorticalAtlasProfile Symmetric(
        double lateralMm,
        double superiorMm,
        double anteriorMm,
        double widthMm,
        double heightMm,
        double depthMm,
        string source) =>
        Bilateral(
            Geometry(-Math.Abs(lateralMm), superiorMm, anteriorMm, widthMm, heightMm, depthMm, source),
            Geometry(Math.Abs(lateralMm), superiorMm, anteriorMm, widthMm, heightMm, depthMm, source));

    private static SubcorticalAtlasProfile Midline(
        double lateralMm,
        double superiorMm,
        double anteriorMm,
        double widthMm,
        double heightMm,
        double depthMm,
        string source)
    {
        var geometry = Geometry(lateralMm, superiorMm, anteriorMm, widthMm, heightMm, depthMm, source);
        return new SubcorticalAtlasProfile(geometry, geometry, true);
    }

    private static AtlasGeometry Geometry(
        double lateralMm,
        double superiorMm,
        double anteriorMm,
        double widthMm,
        double heightMm,
        double depthMm,
        string source) =>
        new(
            new Point3D(lateralMm, superiorMm, anteriorMm),
            new Vector3D(widthMm, heightMm, depthMm),
            source);

    private sealed record SubcorticalAtlasProfile(AtlasGeometry Left, AtlasGeometry Right, bool IsMidline);
    private sealed record AtlasGeometry(Point3D CenterMm, Vector3D DimensionsMm, string Source);
}
