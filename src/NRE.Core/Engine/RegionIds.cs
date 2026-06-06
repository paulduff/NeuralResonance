namespace NRE.Core.Engine;

/// <summary>
/// Canonical region identifiers (byte) used across engine, API and renderer.
///
/// Canon (Paul): Cortex is built as distinct gyri (separate circuits) placed and
/// sized anatomically (no visual warping needed to "fake" lobes).
/// </summary>
public static class RegionIds
{
    // Subcortex / nuclei
    public const byte Thalamus      = 1;
    public const byte Hypothalamus  = 2;
    public const byte BasalGanglia  = 3;
    public const byte Amygdala      = 4;
    public const byte Hippocampus   = 5; // treated as midline/deep structure
    public const byte Cerebellum    = 27;
    public const byte Brainstem     = 7;
    public const byte Pons          = 8;

    // Cortex (gyri / pieces)
    public const byte PrecentralGyrus       = 11; // primary motor
    public const byte PostcentralGyrus      = 12; // primary somatosensory

    public const byte SuperiorFrontalGyrus  = 15;
    public const byte MiddleFrontalGyrus    = 16;
    public const byte InferiorFrontalGyrus  = 17;

    public const byte SuperiorParietal      = 18;
    public const byte InferiorParietal      = 19;
    public const byte SupramarginalGyrus    = 20;
    public const byte AngularGyrus          = 21;

    public const byte SuperiorTemporalGyrus = 22; // primary auditory belt lives here
    public const byte MiddleTemporalGyrus   = 23;
    public const byte InferiorTemporalGyrus = 24;

    public const byte SuperiorOccipital     = 25;
    public const byte InferiorOccipital     = 26; // primary visual belt lives here

    // Midline white-matter tract (represented as a voxel band for rendering and relay)
    public const byte CorpusCallosum        = 14; // callosal tract (connections always present; not user-toggled)

    public const byte Inert                = 255;

    public const byte MaxRegionIdUsed = 27;

    /// <summary>All cortical gyrus regions in stable order.</summary>
    public static readonly byte[] AllCorticalRegions =
    {
        PrecentralGyrus,
        PostcentralGyrus,
        SuperiorFrontalGyrus,
        MiddleFrontalGyrus,
        InferiorFrontalGyrus,
        SuperiorParietal,
        InferiorParietal,
        SupramarginalGyrus,
        AngularGyrus,
        SuperiorTemporalGyrus,
        MiddleTemporalGyrus,
        InferiorTemporalGyrus,
        SuperiorOccipital,
        InferiorOccipital,
    };

    /// <summary>Executive / frontal association subset (used as broadcasting targets).</summary>
    public static readonly byte[] FrontalAssociation =
    {
        SuperiorFrontalGyrus,
        MiddleFrontalGyrus,
        InferiorFrontalGyrus,
    };

    public static bool IsCortex(byte r)
        => r is PrecentralGyrus or PostcentralGyrus
            or SuperiorFrontalGyrus or MiddleFrontalGyrus or InferiorFrontalGyrus
            or SuperiorParietal or InferiorParietal or SupramarginalGyrus or AngularGyrus
            or SuperiorTemporalGyrus or MiddleTemporalGyrus or InferiorTemporalGyrus
            or SuperiorOccipital or InferiorOccipital;

    public static bool IsMidlineDeep(byte r)
        => r is Thalamus or Hypothalamus or Brainstem or Pons or Hippocampus;

    /// <summary>
    /// Cortical neighbors for biologically-plausible short/long-range cortico-cortical bias.
    /// This is not a full connectome; it is a stable adjacency scaffold.
    /// </summary>
    public static ReadOnlySpan<byte> Neighbors(byte r)
        => r switch
        {
            // Frontal belt
            PrecentralGyrus => new byte[] { PostcentralGyrus, MiddleFrontalGyrus, InferiorFrontalGyrus, SuperiorFrontalGyrus },
            SuperiorFrontalGyrus => new byte[] { MiddleFrontalGyrus, PrecentralGyrus },
            MiddleFrontalGyrus => new byte[] { SuperiorFrontalGyrus, InferiorFrontalGyrus, PrecentralGyrus },
            InferiorFrontalGyrus => new byte[] { MiddleFrontalGyrus, PrecentralGyrus, SuperiorTemporalGyrus, SupramarginalGyrus },

            // Central sulcus
            PostcentralGyrus => new byte[] { PrecentralGyrus, SuperiorParietal, InferiorParietal },

            // Parietal
            SuperiorParietal => new byte[] { PostcentralGyrus, InferiorParietal, SuperiorOccipital },
            InferiorParietal => new byte[] { PostcentralGyrus, SuperiorParietal, SupramarginalGyrus, AngularGyrus },
            SupramarginalGyrus => new byte[] { InferiorParietal, AngularGyrus, SuperiorTemporalGyrus, InferiorFrontalGyrus },
            AngularGyrus => new byte[] { InferiorParietal, SupramarginalGyrus, SuperiorOccipital, MiddleTemporalGyrus },

            // Temporal
            SuperiorTemporalGyrus => new byte[] { MiddleTemporalGyrus, SupramarginalGyrus, InferiorFrontalGyrus },
            MiddleTemporalGyrus => new byte[] { SuperiorTemporalGyrus, InferiorTemporalGyrus, AngularGyrus },
            InferiorTemporalGyrus => new byte[] { MiddleTemporalGyrus, InferiorOccipital },

            // Occipital
            SuperiorOccipital => new byte[] { InferiorOccipital, SuperiorParietal, AngularGyrus },
            InferiorOccipital => new byte[] { SuperiorOccipital, InferiorTemporalGyrus },

            _ => ReadOnlySpan<byte>.Empty,
        };

    public static string NameOf(byte r)
        => r switch
        {
            Thalamus => "Thalamus",
            Hypothalamus => "Hypothalamus",
            BasalGanglia => "Basal Ganglia",
            Amygdala => "Amygdala",
            Hippocampus => "Hippocampus",
            CorpusCallosum => "Corpus Callosum",
            PrecentralGyrus => "Precentral Gyrus",
            PostcentralGyrus => "Postcentral Gyrus",
            SuperiorFrontalGyrus => "Superior Frontal Gyrus",
            MiddleFrontalGyrus => "Middle Frontal Gyrus",
            InferiorFrontalGyrus => "Inferior Frontal Gyrus",
            SuperiorParietal => "Superior Parietal Lobule",
            InferiorParietal => "Inferior Parietal Lobule",
            SupramarginalGyrus => "Supramarginal Gyrus",
            AngularGyrus => "Angular Gyrus",
            SuperiorTemporalGyrus => "Superior Temporal Gyrus",
            MiddleTemporalGyrus => "Middle Temporal Gyrus",
            InferiorTemporalGyrus => "Inferior Temporal Gyrus",
            SuperiorOccipital => "Superior Occipital",
            InferiorOccipital => "Inferior Occipital",
            Cerebellum => "Cerebellum",
            Brainstem => "Brainstem",
            Pons => "Pons",
            Inert => "Inactive",
            _ => $"Region {r}"
        };

}
