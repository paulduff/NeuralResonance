# Folded Archive Entry 027: Biologically Accurate Neural Circuits + Language System

## Summary

Comprehensive refactoring of core neural subsystems to match established neuroanatomy and physiology, plus a complete language processing system. All changes are grounded in peer-reviewed literature (see citations).

---

## 1. HIPPOCAMPUS: Trisynaptic Circuit

### Anatomical Basis
Amaral & Witter 1989, Andersen et al. 1971, McNaughton & Morris 1987

### Implementation

```
Entorhinal Cortex (EC Layer II)
       ↓ Perforant Path
Dentate Gyrus (DG) - Pattern Separation
  - Granule cells with competitive lateral inhibition
  - 10:1 sparse coding compression ratio
       ↓ Mossy Fibers  
CA3 - Autoassociative Network
  - Recurrent collaterals enable attractor dynamics
  - Pattern completion from partial cues
  - Hebbian weight learning
       ↓ Schaffer Collaterals
CA1 - Comparator/Output
  - Integrates CA3 output + direct EC input
  - Computes novelty/mismatch signal
       ↓
Subiculum → Back to EC + other regions
```

### Key Properties
- **DG Sparsity**: Measures pattern separation effectiveness (lower = sparser = better)
- **CA3 Coherence**: Measures pattern completion strength
- **CA1 Novelty Signal**: High when input differs from CA3 prediction

---

## 2. CEREBELLUM: Cortex and Deep Nuclei

### Anatomical Basis
Eccles, Ito & Szentágothai 1967, Ito 2006, Ramnani 2006

### Implementation

```
INPUT PATHWAYS:
├── MOSSY FIBERS (from pons, spinal cord, vestibular)
│   └── → Granular Layer → Parallel Fibers
└── CLIMBING FIBERS (from inferior olive)
    └── → Purkinje Cells (teaching signal)

CEREBELLAR CORTEX:
┌─────────────────────────────────────────────┐
│ MOLECULAR LAYER                              │
│   Parallel fibers + Purkinje dendrites       │
├─────────────────────────────────────────────┤
│ PURKINJE CELL LAYER (THE output neurons)     │
│   GABAergic (inhibitory) → Deep Nuclei       │
│   LTD learning from climbing fiber + PF      │
├─────────────────────────────────────────────┤
│ GRANULAR LAYER                               │
│   Sparse expansion (4:1 ratio)               │
│   Receives mossy fiber input                 │
└─────────────────────────────────────────────┘

DEEP CEREBELLAR NUCLEI:
├── Dentate (lateral) - Planning, timing → 40%
├── Interposed - Limb movements → 35%
└── Fastigial (medial) - Balance, posture → 25%

OUTPUT: Deep Nuclei → VL Thalamus → Motor Cortex
```

### Learning Rule
- **LTD**: Climbing fiber + parallel fiber = weaken synapse (error correction)
- **LTP**: Active parallel fiber without error = strengthen synapse

---

## 3. THALAMUS: TRN and Relay Nuclei

### Anatomical Basis
Jones 2007, Sherman & Guillery 2006, Crick 1984

### Implementation

```
┌─────────────────────────────────────────────┐
│ THALAMIC RETICULAR NUCLEUS (TRN)            │
│   GABAergic shell surrounding dorsal thalamus│
│   - Lateral inhibition between relay nuclei  │
│   - Attention "searchlight" mechanism        │
│   - Sleep spindle generation (12-14Hz)       │
└─────────────────────────────────────────────┘
                    │ (GABAergic inhibition)
                    ↓
┌─────────────────────────────────────────────┐
│ RELAY NUCLEI                                │
│   Sensory: VPL/VPM, LGN, MGN                │
│   Motor: VL, VA                             │
│   Association: Pulvinar, MD                 │
│                                             │
│   TWO MODES:                                │
│   - TONIC (waking): Faithful relay, 40Hz   │
│   - BURST (sleep): T-type Ca²⁺, rhythmic   │
└─────────────────────────────────────────────┘
```

### Oscillatory Dynamics
- **Gamma (40Hz)**: Waking attention binding
- **Sleep spindles (12-14Hz)**: NREM sleep, memory consolidation
- **Delta (<4Hz)**: Deep sleep

---

## 4. AMYGDALA: LA→B→CeA Pathway

### Anatomical Basis
LeDoux 2000, Sah et al. 2003, Phelps & LeDoux 2005

### Implementation

```
Sensory Input (thalamus/cortex)
        ↓
┌─────────────────────────────────────┐
│ LATERAL NUCLEUS (LA)                │
│   Primary sensory input             │
│   Fear conditioning site            │
│   Plasticity for threat learning    │
└─────────────────────────────────────┘
        ↓
┌─────────────────────────────────────┐
│ BASAL NUCLEUS (B/BLA)               │
│   Integration hub                   │
│   Receives hippocampal context      │
│   Bidirectional cortical links      │
└─────────────────────────────────────┘
        ↓
┌─────────────────────────────────────┐
│ INTERCALATED CELLS (ITC)            │
│   GABAergic gate clusters           │
│   Extinction learning               │
│   PFC-mediated fear inhibition      │
└─────────────────────────────────────┘
        ↓
┌─────────────────────────────────────┐
│ CENTRAL NUCLEUS (CeA)               │
│   CeM: OUTPUT to brainstem          │
│   → Autonomic responses             │
│   → Noradrenaline release           │
└─────────────────────────────────────┘
```

---

## 5. BRAINSTEM MODULATION (formerly PonsNucleus)

### Anatomical Basis
Steriade & McCarley 2005, Jones 2003, Saper et al. 2010

### Implementation

```
LOCUS COERULEUS (LC) - Noradrenaline
  - HIGH during waking, stress
  - LOW during REM (nearly silent)

RAPHE NUCLEI - Serotonin (5-HT)
  - HIGH during quiet waking
  - LOW during REM (nearly silent)

CHOLINERGIC NUCLEI (PPT/LDT) - Acetylcholine
  - HIGH during waking AND REM
  - LOW during NREM

VENTRAL TEGMENTAL AREA (VTA) - Dopamine
  - Reward, motivation signals
```

### Sleep State Signatures
| State | LC (NE) | Raphe (5-HT) | ACh | Effect |
|-------|---------|--------------|-----|--------|
| Wake  | HIGH    | HIGH         | HIGH| Alert  |
| NREM  | LOW     | LOW          | LOW | Spindles, consolidation |
| REM   | ~0      | ~0           | HIGH| Dreams, theta |

---

## 6. INTEGRATION IN NreEngine

### Execution Order

```
1. Thalamus.Step(dt, sleepPhase)
   - Mode-dependent oscillation (tonic vs burst)
   - TRN dynamics
   - Sleep spindle generation

2. Sleep.Step(dt, atp, activity)
   - NREM ↔ REM cycling
   - Wake/sleep transitions

3. Sensory processing (if awake)

4. Hemisphere step with thalamic binding

5. Amygdala.Step(dt, spikes, hippoContext, pfcInhibition)
   - LA→B→ITC→CeA pathway
   - Noradrenaline output

6. Cerebellum.Step(dt, spikes, totalVoxels, motorCommand, sensoryMismatch)
   - Mossy fiber input
   - Climbing fiber error signal
   - Purkinje cell learning
   - Deep nuclei output

7. PredictiveCoding.Step(...)
   - Hierarchical prediction errors

8. Hippocampus.Step(stepIndex, spikes, dt)
   - Trisynaptic processing: DG→CA3→CA1
   - Novelty detection
   - Episode auto-capture on high salience OR novelty

9. Memory replay during REM
```

---

## 7. LANGUAGE SYSTEM

### Anatomical Basis
Hickok & Poeppel 2007, Friederici 2012, Hagoort 2013

### Dual-Stream Architecture

```
                     AUDITORY INPUT
                          ↓
                 Primary Auditory Cortex (A1)
                      Phoneme Processing
                          ↓
           ┌──────────────┴──────────────┐
           ↓                              ↓
    VENTRAL STREAM                 DORSAL STREAM
    (Sound→Meaning)                (Sound→Articulation)
           ↓                              ↓
   ┌───────────────┐             ┌───────────────┐
   │ WERNICKE'S    │             │ SENSORIMOTOR  │
   │ (pSTG/MTG)    │             │ INTERFACE     │
   │ - Lexical     │             │ - Phonological │
   │   access      │             │   working mem  │
   └───────┬───────┘             └───────┬───────┘
           ↓                              ↓
   ┌───────────────┐             ┌───────────────┐
   │ ANGULAR GYRUS │◄── ARCUATE ──│ BROCA'S AREA │
   │ - Semantic    │   FASCICULUS │ - Syntax     │
   │   integration │              │ - Sequencing │
   └───────┬───────┘             └───────┬───────┘
           ↓                              ↓
   ┌───────────────┐             ┌───────────────┐
   │ CONCEPTUAL    │             │ MOTOR CORTEX  │
   │ NETWORK       │             │ - Articulation│
   └───────────────┘             └───────────────┘
```

### Processing Levels

1. **Phonological**: ~44 English phonemes, grapheme-to-phoneme conversion
2. **Lexical**: 10,000+ word lexicon with semantic vectors (128-dim)
3. **Semantic**: Distributed concept representations, cosine similarity
4. **Syntactic**: Bottom-up phrase structure parsing, SVO ordering

### Key Components

- **A1**: Phoneme encoding from text
- **Wernicke's**: Lexical access, word recognition
- **Angular Gyrus**: Semantic integration hub
- **Broca's**: Syntactic planning and production
- **Arcuate Fasciculus**: Bidirectional stream connection
- **Phonological Loop**: 7±2 item verbal working memory (Baddeley model)
- **Motor Speech**: Articulatory gesture planning

### API Methods

```csharp
// Comprehension
var result = Language.ProcessInput("The cat sat on the mat", dt);
// Returns: SemanticTokens[], ParseTree, Confidence

// Production  
var response = Language.GenerateOutput(semanticIntention, dt);
// Returns: Text, Words[], ArticulatoryPlan

// Simple interface
string reply = Language.Respond("Hello world", dt);

// Learning
Language.LearnWord("quasar", semanticVector, "noun");
```

---

## 8. FILES CHANGED

1. **Hippocampus.cs** - Complete rewrite with DG, CA3, CA1 subregions
2. **Cerebellum.cs** - Added Purkinje cells, deep nuclei, mossy/climbing fibers
3. **Thalamus.cs** - Added TRN, tonic/burst modes, sleep spindles
4. **Amygdala.cs** - Added LA, B, ITC, CeA nuclei pathway
5. **PonsNucleus.cs** - Renamed to BrainstemModulation with LC, raphe, ACh nuclei
6. **LanguageSystem.cs** - NEW: Complete dual-stream language processing
7. **Dtos.cs** - Updated status DTOs for new circuit metrics
8. **NreEngineOptions.cs** - Added EnableLanguageSystem option
9. **NreEngine.cs** - Updated component calls and integration

---

## 9. VERIFICATION

The implementation can be verified by observing:

1. **Hippocampus**: DGSparsity should be ~0.1, CA3Coherence rises with repeated patterns
2. **Cerebellum**: Purkinje output decreases with climbing fiber teaching, DeepNuclei output increases
3. **Thalamus**: Mode switches to Burst during NREM, spindles appear
4. **Amygdala**: CeA output triggers noradrenaline, ITC gating reduces output
5. **Sleep**: NREM→REM cycling visible, REM shows high ACh + low NE/5-HT
6. **Language**: WernickeActivity rises on known words, BrocaActivity on syntax processing

---

## 10. REFERENCES

- Amaral & Witter (1989): Hippocampal formation anatomy
- Andersen et al. (1971): Trisynaptic circuit
- Crick (1984): TRN searchlight hypothesis
- Eccles, Ito & Szentágothai (1967): Cerebellum as a neuronal machine
- Friederici (2012): The cortical language circuit
- Hagoort (2013): MUC (Memory, Unification, Control)
- Hickok & Poeppel (2007): Dual-stream model of speech processing
- Ito (2006): Cerebellar circuit as detection machine
- Jones (2007): The Thalamus, 2nd edition
- LeDoux (2000): Fear circuit neural pathways
- McNaughton & Morris (1987): Hippocampal synaptic plasticity
- Phelps & LeDoux (2005): Contributions to human emotion
- Ramnani (2006): Cerebellar forward model
- Sah et al. (2003): Amygdaloid complex anatomy/physiology
- Saper et al. (2010): Sleep neurobiology
- Sherman & Guillery (2006): Exploring the Thalamus
- Steriade & McCarley (2005): Brainstem Control of Wakefulness

---

**Document Prepared By:** Claude (Anthropic)  
**Date:** February 5, 2026  
**Version:** NRE v11
