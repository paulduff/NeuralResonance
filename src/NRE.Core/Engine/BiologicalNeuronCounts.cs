namespace NRE.Core.Engine;

/// <summary>
/// Biological Neuron Counts and Proportional Allocation
/// 
/// Based on peer-reviewed neuroanatomical literature:
/// - Azevedo et al. (2009) "Equal numbers of neuronal and nonneuronal cells"
/// - Herculano-Houzel (2009) "The Human Brain in Numbers"
/// - Karlsen & Pakkenberg (2011) "Total numbers of neurons in brain regions"
/// - Schröder et al. (1975) "Striatum neuron counts"
/// - Vereecken et al. (1994) "Amygdala stereological analysis"
/// 
/// HUMAN BRAIN TOTAL: ~86 billion neurons
/// 
/// KEY INSIGHT: The cerebellum contains ~80% of all neurons despite being
/// only 10% of brain volume! This is due to densely packed granule cells.
/// 
/// This class provides biologically accurate proportions for allocating
/// simulated neurons across brain regions in the Neural Resonance Engine.
/// </summary>
public static class BiologicalNeuronCounts
{
    // ==================== ACTUAL NEURON COUNTS (HUMAN BRAIN) ====================
    
    /// <summary>Total neurons in human brain.</summary>
    public const long TOTAL_BRAIN_NEURONS = 86_000_000_000L;
    
    /// <summary>Cerebellum: ~69 billion neurons (80% of total!).</summary>
    public const long CEREBELLUM_NEURONS = 69_000_000_000L;
    
    /// <summary>Cerebral cortex: ~16-21 billion neurons.</summary>
    public const long CEREBRAL_CORTEX_NEURONS = 16_000_000_000L;
    
    /// <summary>Basal ganglia (total): ~816 million neurons.</summary>
    public const long BASAL_GANGLIA_NEURONS = 816_000_000L;
    
    /// <summary>Striatum (caudate + putamen): ~100 million neurons.</summary>
    public const long STRIATUM_NEURONS = 100_000_000L;
    
    /// <summary>Globus pallidus: ~700,000 neurons.</summary>
    public const long GLOBUS_PALLIDUS_NEURONS = 700_000L;
    
    /// <summary>Subthalamic nucleus: ~300,000 neurons.</summary>
    public const long STN_NEURONS = 300_000L;
    
    /// <summary>Thalamus: ~50 million neurons (estimated from multiple nuclei).</summary>
    public const long THALAMUS_NEURONS = 50_000_000L;
    
    /// <summary>Hippocampus (per hemisphere): ~20-30 million neurons.</summary>
    public const long HIPPOCAMPUS_NEURONS = 25_000_000L;
    
    /// <summary>Amygdala (per hemisphere): ~10-12 million neurons.</summary>
    public const long AMYGDALA_NEURONS = 12_000_000L;
    
    /// <summary>Hypothalamus: ~5-10 million neurons.</summary>
    public const long HYPOTHALAMUS_NEURONS = 7_000_000L;
    
    /// <summary>Brainstem (midbrain + pons + medulla): ~1 billion neurons.</summary>
    public const long BRAINSTEM_NEURONS = 1_000_000_000L;
    
    /// <summary>VTA (ventral tegmental area): ~400,000 dopamine neurons.</summary>
    public const long VTA_NEURONS = 400_000L;
    
    /// <summary>Substantia nigra pars compacta: ~400,000 dopamine neurons.</summary>
    public const long SNc_NEURONS = 400_000L;
    
    /// <summary>Locus coeruleus: ~50,000 noradrenaline neurons.</summary>
    public const long LC_NEURONS = 50_000L;
    
    /// <summary>Raphe nuclei: ~200,000 serotonin neurons.</summary>
    public const long RAPHE_NEURONS = 200_000L;
    
    // ==================== CORTICAL SUBDIVISION ESTIMATES ====================
    // Based on cortical surface area proportions
    
    /// <summary>Primary visual cortex (V1): ~140 million neurons.</summary>
    public const long V1_NEURONS = 140_000_000L;
    
    /// <summary>Primary auditory cortex (A1): ~100 million neurons.</summary>
    public const long A1_NEURONS = 100_000_000L;
    
    /// <summary>Primary motor cortex (M1): ~100 million neurons.</summary>
    public const long M1_NEURONS = 100_000_000L;
    
    /// <summary>Primary somatosensory cortex (S1): ~120 million neurons.</summary>
    public const long S1_NEURONS = 120_000_000L;
    
    /// <summary>Prefrontal cortex (PFC): ~2 billion neurons (~12% of cortex).</summary>
    public const long PFC_NEURONS = 2_000_000_000L;
    
    /// <summary>Broca's area: ~50 million neurons.</summary>
    public const long BROCA_NEURONS = 50_000_000L;
    
    /// <summary>Wernicke's area: ~50 million neurons.</summary>
    public const long WERNICKE_NEURONS = 50_000_000L;
    
    // ==================== RELATIVE PROPORTIONS ====================
    
    /// <summary>
    /// Get the biological proportion of neurons for each NRE region.
    /// Returns fraction of total brain neurons (0.0 - 1.0).
    /// </summary>
    public static float GetBiologicalProportion(int regionId)
    {
        return regionId switch
        {
            // Subcortical structures
            1 => THALAMUS_NEURONS / (float)TOTAL_BRAIN_NEURONS,           // 0.058%
            2 => HYPOTHALAMUS_NEURONS / (float)TOTAL_BRAIN_NEURONS,       // 0.008%
            3 => BASAL_GANGLIA_NEURONS / (float)TOTAL_BRAIN_NEURONS,      // 0.95%
            4 => AMYGDALA_NEURONS / (float)TOTAL_BRAIN_NEURONS,           // 0.014%
            5 => HIPPOCAMPUS_NEURONS / (float)TOTAL_BRAIN_NEURONS,        // 0.029%
            27 => CEREBELLUM_NEURONS / (float)TOTAL_BRAIN_NEURONS,         // 80.2%
            7 => BRAINSTEM_NEURONS / (float)TOTAL_BRAIN_NEURONS,          // 1.16%
            8 => BRAINSTEM_NEURONS / 4f / (float)TOTAL_BRAIN_NEURONS,     // Pons: ~0.29%
            
            // Cortical gyri (per-gyrus canonical IDs from RegionIds.cs)
            // Motor / somatosensory belt
            11 => M1_NEURONS / (float)TOTAL_BRAIN_NEURONS,                        // PrecentralGyrus (M1)
            12 => S1_NEURONS / (float)TOTAL_BRAIN_NEURONS,                        // PostcentralGyrus (S1)
            // Frontal gyri (split from old "PFC" region 13)
            15 => PFC_NEURONS * 0.35f / (float)TOTAL_BRAIN_NEURONS,               // SuperiorFrontalGyrus
            16 => PFC_NEURONS * 0.35f / (float)TOTAL_BRAIN_NEURONS,               // MiddleFrontalGyrus
            17 => PFC_NEURONS * 0.30f / (float)TOTAL_BRAIN_NEURONS,               // InferiorFrontalGyrus (Broca's)
            // Parietal gyri (split from old "Parietal" region 10)
            18 => 500_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // SuperiorParietal
            19 => 400_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // InferiorParietal
            20 => 300_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // SupramarginalGyrus
            21 => 300_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // AngularGyrus
            // Temporal gyri (split from old "Temporal" region 12)
            22 => A1_NEURONS / (float)TOTAL_BRAIN_NEURONS,                        // SuperiorTemporalGyrus (A1)
            23 => 500_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // MiddleTemporalGyrus
            24 => 500_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // InferiorTemporalGyrus
            // Occipital gyri (split from old "Occipital" region 9)
            25 => 400_000_000L / (float)TOTAL_BRAIN_NEURONS,                      // SuperiorOccipital
            26 => V1_NEURONS / (float)TOTAL_BRAIN_NEURONS,                        // InferiorOccipital (V1)
            
            // Corpus callosum (white matter, ~200M neurons in transit zone)
            14 => 200_000_000L / (float)TOTAL_BRAIN_NEURONS,
            
            _ => 0.001f // Unknown region: minimal allocation
        };
    }
    
    /// <summary>
    /// Get neuron count for a region scaled to a total simulation budget.
    /// </summary>
    /// <param name="regionId">NRE region ID (see RegionIds.cs)</param>
    /// <param name="totalSimNeurons">Total neurons available in simulation</param>
    /// <returns>Number of simulated neurons for this region</returns>
    public static int GetScaledNeuronCount(int regionId, int totalSimNeurons)
    {
        float proportion = GetBiologicalProportion(regionId);
        return Math.Max(1, (int)(proportion * totalSimNeurons));
    }
    
    /// <summary>
    /// Get a recommended voxel allocation that preserves biological proportions.
    /// Since NRE uses voxels (not individual neurons), this converts neuron
    /// proportions to voxel budgets while maintaining relationships.
    /// </summary>
    /// <param name="totalVoxels">Total voxels available (e.g., W*H*D)</param>
    /// <returns>Dictionary of regionId -> voxel count</returns>
    public static Dictionary<int, int> GetProportionalVoxelAllocation(int totalVoxels)
    {
        var allocation = new Dictionary<int, int>();
        
        // Raw biological proportions are dominated by cerebellum (80%)
        // For a cognitive simulation, we need to rebalance to give
        // cortex and limbic structures meaningful representation
        // while still reflecting relative importance
        
        // Adjusted weights that balance biology with computational relevance
        // Uses canonical per-gyrus region IDs from RegionIds.cs
        var weights = new Dictionary<int, float>
        {
            // Subcortical
            { RegionIds.Thalamus, 0.08f },
            { RegionIds.Hypothalamus, 0.02f },
            { RegionIds.BasalGanglia, 0.06f },
            { RegionIds.Amygdala, 0.03f },
            { RegionIds.Hippocampus, 0.05f },
            { RegionIds.Cerebellum, 0.15f },
            { RegionIds.Brainstem, 0.03f },
            { RegionIds.Pons, 0.02f },
            
            // Cortical: per-gyrus allocations
            // Frontal (was 0.18 for "PFC")
            { RegionIds.SuperiorFrontalGyrus, 0.06f },
            { RegionIds.MiddleFrontalGyrus, 0.05f },
            { RegionIds.InferiorFrontalGyrus, 0.04f },
            // Motor / somatosensory
            { RegionIds.PrecentralGyrus, 0.04f },
            { RegionIds.PostcentralGyrus, 0.04f },
            // Parietal
            { RegionIds.SuperiorParietal, 0.03f },
            { RegionIds.InferiorParietal, 0.02f },
            { RegionIds.SupramarginalGyrus, 0.02f },
            { RegionIds.AngularGyrus, 0.02f },
            // Temporal
            { RegionIds.SuperiorTemporalGyrus, 0.04f },
            { RegionIds.MiddleTemporalGyrus, 0.04f },
            { RegionIds.InferiorTemporalGyrus, 0.03f },
            // Occipital
            { RegionIds.SuperiorOccipital, 0.04f },
            { RegionIds.InferiorOccipital, 0.04f },
            // CC
            { RegionIds.CorpusCallosum, 0.05f },
        };
        
        // Normalize weights
        float totalWeight = 0f;
        foreach (var w in weights.Values) totalWeight += w;
        
        // Allocate voxels
        int allocated = 0;
        foreach (var kvp in weights)
        {
            int voxels = Math.Max(1, (int)((kvp.Value / totalWeight) * totalVoxels));
            allocation[kvp.Key] = voxels;
            allocated += voxels;
        }
        
        // Distribute any remainder to largest frontal region
        if (allocated < totalVoxels)
        {
            allocation[RegionIds.SuperiorFrontalGyrus] += (totalVoxels - allocated);
        }
        
        return allocation;
    }
    
    /// <summary>
    /// Get detailed neuron type breakdown for a region.
    /// </summary>
    public static NeuronTypeBreakdown GetNeuronTypeBreakdown(int regionId)
    {
        return regionId switch
        {
            // Cerebral cortex (all gyri): 80% pyramidal, 20% interneurons
            11 or 12 or 15 or 16 or 17 or 18 or 19 or 20 or 21
               or 22 or 23 or 24 or 25 or 26 => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.80f,
                InhibitoryFraction: 0.20f,
                PyramidalFraction: 0.75f,
                PVFraction: 0.10f,
                SOMFraction: 0.07f,
                VIPFraction: 0.03f),
            
            // Striatum: 95% MSNs (inhibitory!), 5% interneurons
            3 => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.05f,
                InhibitoryFraction: 0.95f,
                PyramidalFraction: 0f,
                PVFraction: 0.02f,
                SOMFraction: 0.02f,
                VIPFraction: 0.01f),
            
            // Thalamus: mostly relay neurons (excitatory)
            1 => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.75f,
                InhibitoryFraction: 0.25f, // TRN is inhibitory
                PyramidalFraction: 0f,
                PVFraction: 0f,
                SOMFraction: 0f,
                VIPFraction: 0f),
            
            // Cerebellum: mostly granule cells, Purkinje cells inhibitory
            27 => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.99f, // Granule cells
                InhibitoryFraction: 0.01f, // Purkinje, stellate, basket
                PyramidalFraction: 0f,
                PVFraction: 0f,
                SOMFraction: 0f,
                VIPFraction: 0f),
            
            // Hippocampus: 90% pyramidal, 10% interneurons
            5 => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.90f,
                InhibitoryFraction: 0.10f,
                PyramidalFraction: 0.85f,
                PVFraction: 0.05f,
                SOMFraction: 0.04f,
                VIPFraction: 0.01f),
            
            // Amygdala: mixed
            4 => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.70f,
                InhibitoryFraction: 0.30f, // ITCs are inhibitory
                PyramidalFraction: 0.50f,
                PVFraction: 0.10f,
                SOMFraction: 0.10f,
                VIPFraction: 0f),
            
            // Default
            _ => new NeuronTypeBreakdown(
                ExcitatoryFraction: 0.80f,
                InhibitoryFraction: 0.20f,
                PyramidalFraction: 0f,
                PVFraction: 0f,
                SOMFraction: 0f,
                VIPFraction: 0f)
        };
    }
    
    /// <summary>
    /// Get biological connectivity density (synapses per neuron) for a region.
    /// </summary>
    public static int GetSynapsesPerNeuron(int regionId)
    {
        return regionId switch
        {
            // Cerebellar granule cells: ~5 synapses each (very sparse!)
            27 => 5,
            
            // Purkinje cells: 100,000-200,000 synapses each
            // But granule cells dominate the count
            
            // Cortical pyramidal: ~7,000 synapses (all cortical gyri)
            11 or 12 or 15 or 16 or 17 or 18 or 19 or 20 or 21
               or 22 or 23 or 24 or 25 or 26 => 7000,
            
            // Hippocampal pyramidal: ~10,000 synapses
            5 => 10000,
            
            // Thalamic relay: ~5,000 synapses
            1 => 5000,
            
            // Striatal MSNs: ~10,000 synapses
            3 => 10000,
            
            // Amygdala: ~5,000 synapses
            4 => 5000,
            
            // Default
            _ => 5000
        };
    }
    
    /// <summary>
    /// Print a summary of the biological neuron distribution.
    /// </summary>
    public static string GetSummary()
    {
        return $"""
            ╔═══════════════════════════════════════════════════════════════╗
            ║         HUMAN BRAIN NEURON DISTRIBUTION                       ║
            ║         (Based on Azevedo et al. 2009, Herculano-Houzel 2009) ║
            ╠═══════════════════════════════════════════════════════════════╣
            ║ TOTAL NEURONS:           86,000,000,000 (86 billion)          ║
            ╠═══════════════════════════════════════════════════════════════╣
            ║ CEREBELLUM:              69,000,000,000 (80.2%)  ← Dominant!  ║
            ║ CEREBRAL CORTEX:         16,000,000,000 (18.6%)               ║
            ║ BRAINSTEM:                1,000,000,000 (1.2%)                ║
            ╠═══════════════════════════════════════════════════════════════╣
            ║ Subcortical Structures:                                       ║
            ║   Basal Ganglia:           816,000,000                        ║
            ║   Thalamus:                 50,000,000                        ║
            ║   Hippocampus (each):       25,000,000                        ║
            ║   Amygdala (each):          12,000,000                        ║
            ║   Hypothalamus:              7,000,000                        ║
            ╠═══════════════════════════════════════════════════════════════╣
            ║ Neuromodulatory Nuclei:                                       ║
            ║   VTA (dopamine):              400,000                        ║
            ║   SNc (dopamine):              400,000                        ║
            ║   Raphe (serotonin):           200,000                        ║
            ║   LC (noradrenaline):           50,000                        ║
            ╚═══════════════════════════════════════════════════════════════╝
            """;
    }
}

/// <summary>
/// Breakdown of neuron types within a region.
/// </summary>
public readonly record struct NeuronTypeBreakdown(
    float ExcitatoryFraction,
    float InhibitoryFraction,
    float PyramidalFraction,
    float PVFraction,
    float SOMFraction,
    float VIPFraction);

/// <summary>
/// Extension methods for applying biological proportions to NRE.
/// </summary>
public static class BiologicalProportionExtensions
{
    /// <summary>
    /// Apply biological neuron proportions to voxel density.
    /// Regions with more neurons get higher voxel activity scaling.
    /// </summary>
    public static float GetActivityScalingFactor(this int regionId)
    {
        // Normalize so frontal cortex = 1.0
        float corticalProportion = BiologicalNeuronCounts.GetBiologicalProportion(RegionIds.SuperiorFrontalGyrus);
        float regionProportion = BiologicalNeuronCounts.GetBiologicalProportion(regionId);
        
        // Clamp to reasonable range
        return Math.Clamp(regionProportion / corticalProportion, 0.1f, 10f);
    }
    
    /// <summary>
    /// Get suggested local connectivity radius based on biological density.
    /// Denser regions (more neurons) should have tighter local connectivity.
    /// </summary>
    public static int GetSuggestedConnectivityRadius(this int regionId, int baseRadius = 3)
    {
        var breakdown = BiologicalNeuronCounts.GetNeuronTypeBreakdown(regionId);
        
        // Cerebellum has very local connectivity
        if (regionId == RegionIds.Cerebellum) return Math.Max(1, baseRadius - 1);
        
        // Hippocampus has extensive recurrent connections
        if (regionId == RegionIds.Hippocampus) return baseRadius + 1;
        
        // Frontal gyri have long-range connections
        if (regionId == RegionIds.SuperiorFrontalGyrus || regionId == RegionIds.MiddleFrontalGyrus
            || regionId == RegionIds.InferiorFrontalGyrus) return baseRadius + 1;
        
        return baseRadius;
    }
}
