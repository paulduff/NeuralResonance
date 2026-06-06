# Biological Neuron Proportions in NRE

## Overview

The Neural Resonance Engine now supports biologically accurate neuron proportions based on peer-reviewed neuroanatomical research. This document explains how real brain neuron counts are mapped to the simulation.

## Human Brain Neuron Distribution

Based on Azevedo et al. (2009) and Herculano-Houzel (2009):

| Structure | Neuron Count | % of Total |
|-----------|-------------|------------|
| **Total Brain** | 86 billion | 100% |
| **Cerebellum** | 69 billion | **80.2%** |
| **Cerebral Cortex** | 16 billion | 18.6% |
| **Brainstem** | 1 billion | 1.2% |

### Key Insight: The Cerebellum Dominates!

The cerebellum contains 80% of all brain neurons despite being only 10% of brain volume. This is because:
- **Granule cells** are the most numerous neurons (~50 billion)
- Each granule cell is tiny and makes only ~5 synapses
- Compare to cortical pyramidal cells: ~7,000 synapses each

### Subcortical Structures

| Structure | Neurons | Notes |
|-----------|---------|-------|
| Basal Ganglia | 816 million | Striatum: 100M, GP: 700K, STN: 300K |
| Thalamus | 50 million | Multiple nuclei |
| Hippocampus | 25 million (each) | CA1-CA4, dentate gyrus |
| Amygdala | 12 million (each) | LA, B, CeA nuclei |
| Hypothalamus | 7 million | Neuroendocrine control |

### Neuromodulatory Nuclei (Small but Critical!)

| Nucleus | Neurons | Neurotransmitter |
|---------|---------|------------------|
| VTA | 400,000 | Dopamine |
| SNc | 400,000 | Dopamine |
| Raphe | 200,000 | Serotonin |
| Locus Coeruleus | 50,000 | Noradrenaline |

Note: These tiny nuclei have massive influence through widespread projections!

## NRE Region Mapping

| Region ID | Structure | Biological Neurons | Adjusted Weight |
|-----------|-----------|-------------------|-----------------|
| 1 | Thalamus | 50M | 8% |
| 2 | Hypothalamus | 7M | 2% |
| 3 | Basal Ganglia | 816M | 6% |
| 4 | Amygdala | 12M | 3% |
| 5 | Hippocampus | 25M | 5% |
| 6 | Cerebellum | 69B | 15%* |
| 7 | Brainstem | 1B | 3% |
| 8 | Pons | 250M | 2% |
| 9 | Occipital Cortex | ~1B | 8% |
| 10 | Parietal Cortex | ~2B | 10% |
| 11 | Motor Cortex | ~1B | 8% |
| 12 | Temporal Cortex | ~3B | 12% |
| 13 | Frontal/PFC | ~4B | 18% |

*Cerebellum reduced from 80% to 15% for practical cognitive simulation.

## Configuration

In `NreEngineOptions`:

```csharp
// Enable biological proportions
EnableBiologicalProportions = true

// Weight: 0 = equal allocation, 1 = pure biological ratios
BiologicalProportionWeight = 0.5f  // Balanced (recommended)

// Region-specific synapse density
EnableBiologicalSynapseDensity = true

// Region-specific connectivity radius
EnableBiologicalConnectivityRadius = true
```

## Neuron Type Distribution by Region

### Cerebral Cortex (Regions 9-13)
- 80% Excitatory (pyramidal)
- 20% Inhibitory
  - 10% PV+ (fast-spiking basket cells)
  - 7% SOM+ (Martinotti cells)
  - 3% VIP+ (disinhibitory)

### Striatum (Region 3)
- 95% MSNs (Medium Spiny Neurons, inhibitory!)
  - 50% D1 (direct pathway)
  - 50% D2 (indirect pathway)
- 5% Interneurons

### Hippocampus (Region 5)
- 90% Pyramidal (excitatory)
- 10% Interneurons

### Cerebellum (Region 6)
- 99% Granule cells (excitatory)
- 1% Purkinje, stellate, basket (inhibitory)

### Thalamus (Region 1)
- 75% Relay neurons (excitatory)
- 25% TRN neurons (inhibitory)

## Synapses per Neuron by Region

| Region | Synapses/Neuron | Notes |
|--------|-----------------|-------|
| Cerebellar granule | 5 | Extremely sparse |
| Cortical pyramidal | 7,000 | Typical |
| Hippocampal pyramidal | 10,000 | Dense recurrent |
| Cerebellar Purkinje | 100,000+ | Most connected neurons |
| Striatal MSN | 10,000 | Integration hub |
| Thalamic relay | 5,000 | Focused projections |

## Usage in Code

```csharp
// Get biological proportion for a region
float proportion = BiologicalNeuronCounts.GetBiologicalProportion(regionId);

// Get scaled neuron count for simulation
int simNeurons = BiologicalNeuronCounts.GetScaledNeuronCount(regionId, totalBudget);

// Get neuron type breakdown
var breakdown = BiologicalNeuronCounts.GetNeuronTypeBreakdown(regionId);

// Get synapses per neuron
int synapses = BiologicalNeuronCounts.GetSynapsesPerNeuron(regionId);

// Get proportional voxel allocation
var allocation = BiologicalNeuronCounts.GetProportionalVoxelAllocation(totalVoxels);
```

## Implications for Simulation

1. **Cerebellum**: Despite having most neurons, each makes few connections. In simulation, this means:
   - Large voxel count
   - Low connectivity per voxel
   - Fast, feed-forward processing

2. **Cortex**: Fewer neurons but densely connected. This means:
   - Moderate voxel count
   - High connectivity per voxel
   - Recurrent, associative processing

3. **Subcortical**: Small but critical. This means:
   - Small voxel count
   - Strategic connections
   - Modulatory influence

4. **Neuromodulatory**: Tiny nuclei, huge impact. This means:
   - Very few voxels
   - Broadcast connections
   - Global state modulation

## References

- Azevedo FA, et al. (2009). "Equal numbers of neuronal and nonneuronal cells make the human brain an isometrically scaled-up primate brain." J Comp Neurol. 513(5):532-41.
- Herculano-Houzel S (2009). "The human brain in numbers: a linearly scaled-up primate brain." Front Hum Neurosci. 3:31.
- Karlsen AS, Pakkenberg B (2011). "Total numbers of neurons and glial cells in cortex and basal ganglia of aged brains with Down syndrome." Cereb Cortex. 21(11):2519-24.
- Schröder H, et al. (1975). "Quantitative data about the distribution of cell types and synapses in the neostriatum." J Hirnforsch. 16(5):389-401.
- Vereecken TH, et al. (1994). "Neuron loss and shrinkage in the amygdala in Alzheimer's disease." Neurobiol Aging. 15(1):45-54.

---

**Document Version:** 1.0  
**Date:** February 2026  
**Applies to:** NRE v12+
