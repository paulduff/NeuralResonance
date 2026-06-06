# Folded Archive Entry 023: Anatomical Corrections, Sleep Fixes, Resonance/Thought Improvements

## Summary

Comprehensive fixes to:
1. Neural structure placement, sizing, and orientation
2. Sleep/wake state machine for proper NREM→REM cycling
3. Resonance detection and thought clustering
4. UI button feedback and sidebar functionality

---

## Neural Structure Corrections

### Coordinate System

This entry follows the **NRE unified coordinate convention** (see `Coordinate_System_Specification.md`).

**Canonical normalized space (preferred):**
- **X**: lateral (left = **-X**, right = **+X**), midline at **X = 0**
- **Y**: superior/dorsal = **+Y**, inferior/ventral = **-Y**
- **Z**: anterior/rostral = **-Z**, posterior/caudal = **+Z**

**If you see legacy fractional positions** like `z≈0.42` in older notes, that is the *voxel fraction* `z01` in **[0..1]** (0 = anterior, 1 = posterior).
Convert between them with:

- `nz = z01 * 2 - 1`
- `z01 = (nz + 1) / 2`

Likewise for Y (note the voxel-grid flip used by the engine/renderer):

- `ny = 1 - y01 * 2`
- `y01 = (1 - ny) / 2`

### Corrected Structure Placements

| Structure | Volume (mL) | Position | Notes |
|-----------|-------------|----------|-------|
| **Thalamus** | ~14 | Central hub, **Z≈-0.16** (z01≈0.42) | Relay station for all cortical traffic |
| **Hypothalamus** | ~1 | Below thalamus, **Z≈-0.24** (z01≈0.38) | Tiny but critical for homeostasis |
| **Basal Ganglia** | ~10/side | Lateral to thalamus, **Z≈-0.28** (z01≈0.36) | Motor planning, habit formation |
| **Amygdala** | ~2/side | Medial temporal, **Z≈-0.30** (z01≈0.35) | Salience, emotion |
| **Hippocampus** | ~3.3/side | Arc through medial temporal | Memory encoding, spatial navigation |
| **Cerebellum** | ~57/side | Posterior-inferior, **Z≈+0.70** (z01≈0.85) | LARGE - motor coordination, timing |
| **Pons** | ~8-10 | Brainstem bulge, **Z≈+0.56** (z01≈0.78) | Arousal, relay |
| **Brainstem** | ~5-6 | Inferior column, **Z≈+0.40..+0.84** (z01≈0.70..0.92) | Vital functions |

---

## Sleep State Machine Fixes

### Problem
The original state machine had:
- Conflicting wake conditions checking pressure at different thresholds
- Wake checks happening BEFORE phase transition checks
- Slow pressure accumulation (~75s to trigger natural sleep)
- No minimum sleep dwell time (causing immediate wakes)

### Solution: Corrected State Machine

```
SLEEP CYCLE STATE MACHINE
═════════════════════════

    ┌──────────────────────────────────────────────────────┐
    │                                                      │
    ▼                                                      │
[AWAKE] ──(ATP<0.40 OR pressure>0.50)──► [NREM]           │
    ▲          (after 3s awake dwell)      │              │
    │                                      │              │
    │                              (3s elapsed)           │
    │                                      │              │
    │                                      ▼              │
    │◄────(pressure<0.08)──────────────[REM]◄─────────────┤
    │                                      │              │
    │                              (2s elapsed)           │
    │                                      │              │
    │◄────(pressure<0.08 OR ATP>0.70)──────┘              │
    │                                                      │
    └──────────(30s max sleep cap)─────────────────────────┘
```

### Key Parameters (Demo-Tuned)

```csharp
// Sleep onset
SleepTriggerAtp = 0.40f           // ATP below this → sleep
SleepTriggerPressure = 0.50f      // Pressure above this → sleep

// Wake triggers (ONLY checked at cycle boundaries)
WakeTriggerPressure = 0.08f       // Pressure below this → wake
WakeTriggerAtp = 0.70f            // ATP above this (+ low pressure) → wake

// Timing
MinAwakeDwellSeconds = 3.0f       // Prevents immediate sleep bounce
MinSleepDwellSeconds = 6.0f       // Must complete full NREM+REM before wake check
MaxSleepEpisodeSeconds = 30.0f    // Hard cap

// Cycle timing
RemCycleDurationSeconds = 5.0f    // Total cycle time
NremToRemRatio = 1.5f             // 3s NREM, 2s REM

// Pressure dynamics
SleepPressureRate = 0.025f        // Reach 0.50 in ~20 seconds
SleepRecoveryRate = 0.012f        // Slow recovery during sleep
```

### ForcePhase Improvements

When forcing a sleep phase via UI:
- **ForcePhase(Nrem)**: Sets pressure ≥ 0.45 to ensure full cycle completion
- **ForcePhase(Rem)**: Sets pressure ≥ 0.35 and sleepEpisodeTimer to 3.5s
- **ForcePhase(Awake)**: Resets dwell timers appropriately

---

## Resonance & Thought Detection Improvements

### ResonanceDetector Changes

| Parameter | Before | After | Reason |
|-----------|--------|-------|--------|
| windowSteps | 24 | 32 | Longer window for better pattern detection |
| minDensity default | 0.25 | 0.15 | Lower threshold to catch more activity |
| Adaptive threshold | None | Yes | Lowers threshold when activity is sparse |

### New Features
- **Count cache**: Reuses array to reduce allocations
- **Activity tracking**: Monitors total spikes in window
- **Adaptive thresholds**: When activity < 10 spikes/window, threshold drops to 0.06

### ThoughtClusterer Changes

| Parameter | Before | After | Reason |
|-----------|--------|-------|--------|
| radius | 3.0 | 2.5 | Tighter clusters for smaller assemblies |
| minPts | 3 | 2 | Detect smaller coherent groups |
| minDensity | 0.12 | 0.08 | Lower bar for thought detection |
| maxClusters | 16 | 20 | Allow more simultaneous thoughts |

---

## UI Improvements

### Button Feedback
- **2px borders** instead of 1px for better visibility
- **translateY(-2px)** on hover with larger shadow
- **scale(0.98)** on active for obvious click feedback
- **Ripple animation** on click
- **Focus-visible outline** for accessibility

### Sleep Phase Buttons
- **Wake**: Orange gradient (☀ icon)
- **NREM**: Blue gradient (💤 icon)
- **REM**: Pink gradient (🌙 icon)

### Sleep Phase Display
- **Awake**: Default styling
- **NREM**: Blue color (#5a7aef)
- **REM**: Pink color (#ef6a9f) with glow animation

### Sidebar Refresh
- UiLoop now calls RefreshStatus every 1 second
- Ensures sleep pressure/phase updates are visible in real-time

---

## Files Changed

1. **NreEngine.cs/ApplyRegionLayout()**: Corrected subcortical positions and sizes
2. **NreEngine.cs/PickCortexRegion()**: Better cortical parcellation
3. **SleepController.cs**: Completely rewritten state machine
4. **ResonanceDetector.cs**: Larger window, adaptive thresholds
5. **NreEngine.cs/GetThoughtClusters()**: Better detection parameters
6. **Home.razor**: UI refresh loop, button classes
7. **site.css**: Button feedback, sleep phase colors
