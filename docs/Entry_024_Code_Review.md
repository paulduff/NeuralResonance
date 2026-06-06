# Folded Archive Entry 024: Comprehensive Code Review

## Summary

Deep review of NRE codebase identifying bugs, potential issues, and improvements.

---

## Bugs Fixed

### 1. ResonanceDetector Ring Buffer Logic (CRITICAL)

**File:** `ResonanceDetector.cs`

**Problem:** The original code counted removed spikes BEFORE advancing the head pointer:
```csharp
// WRONG - counts from slot we're about to write TO, not FROM
var oldBits = _history[_head];  // Still pointing at current slot
int removed = 0;
for (...) removed++;
_head = (_head + 1) % _window;  // Now advance
_history[_head].SetAll(false);  // Clear the NEW slot (not the old one!)
```

This caused `_totalSpikesInWindow` to drift because it was subtracting spikes from the wrong slot.

**Fix:** Advance head FIRST, then count from the slot being overwritten:
```csharp
_head = (_head + 1) % _window;  // Advance to oldest slot
var oldBits = _history[_head];   // This is now the oldest data
int removed = 0;
for (...) removed++;             // Count what we're removing
oldBits.SetAll(false);           // Clear this slot
// Write new spikes...
```

### 2. ResonanceDetector Index Bounds Check

**Problem:** No bounds checking when setting BitArray bits, could cause IndexOutOfRangeException if spike index is out of bounds.

**Fix:** Added bounds check:
```csharp
if (idx >= 0 && idx < oldBits.Length)
    oldBits[idx] = true;
```

### 3. ThoughtClusterer Duplicate Voxels (MODERATE)

**File:** `ThoughtClusterer.cs`

**Problem:** `ExpandCluster` could add the same voxel multiple times because it processed queue items without checking cluster membership.

**Fix:** Added HashSet to track cluster membership:
```csharp
var inCluster = new HashSet<int> { seedIdx };
// ...
if (inCluster.Add(j))  // Only add if not already present
    outVox.Add(voxels[j]);
```

### 4. PredictiveCoding Memory Allocation (PERFORMANCE)

**File:** `PredictiveCoding.cs`

**Problem:** Calling `.ToArray()` on dictionary keys every step:
```csharp
var keys = state.Predictions.Keys.ToArray();  // Allocates new array every time!
```

**Fix:** Cache key array per region, only rebuild when keys change:
```csharp
private byte[]? _cachedKeys;
public byte[] GetPredictionKeys() {
    if (_keysDirty || _cachedKeys == null)
        _cachedKeys = Predictions.Keys.ToArray();
    return _cachedKeys;
}
```

### 5. Hippocampus Dictionary Modification During Iteration (SAFETY)

**File:** `Hippocampus.cs`

**Problem:** Modifying `_associations[kv.Key] = newVal` while iterating over the dictionary. While this works in modern .NET, it's error-prone.

**Fix:** Two-phase approach - collect updates in a list, then apply after iteration.

---

## New Features Added

### 1. SleepController.BuildPressure() Method

Allows manual increase of sleep pressure for testing/demo purposes:
```csharp
public void BuildPressure(float amount = 0.3f)
{
    _sleepPressure = MathF.Min(1f, _sleepPressure + amount);
    _awakeDwellTimer = MathF.Max(_awakeDwellTimer, MinAwakeDwellSeconds);
}
```

### 2. SleepController.GetPressure() Method

Read-only access to current sleep pressure for UI display.

### 3. API Endpoint: POST /api/engine/sleep/buildpressure

Query param: `amount` (default 0.3)

### 4. UI: Build Pressure Button + Progress Bar

Visual progress bar showing sleep pressure from 0-100%, plus button to manually increase pressure.

---

## Architecture Observations

### Strengths

1. **Multi-timescale scheduling** - Fast/Intermediate/Slow lanes prevent expensive operations from blocking spike processing.

2. **Lock-free reads** - Published render frames use volatile fields for UI polling without blocking simulation.

3. **Stackalloc usage** - PredictiveCoding and other subsystems use stack allocation for small arrays.

4. **Reusable buffers** - Most subsystems maintain reusable List/Dictionary buffers to reduce GC pressure.

5. **Protected tracts** - Structural plasticity won't prune biologically-essential connections.

### Potential Improvements

1. **NeuralVolume.Buffer allocation** - Each voxel has its own Queue<DelayedSignal>. For 32³ voxels × 2 hemispheres = 65,536 queues. Consider a single priority queue per hemisphere.

2. **SynapseMap.ApplyStpOnSpike adds to HashSet unconditionally** - Could check if synapse is already in _recentlyActivated.

3. **Traffic events list growth** - `_trafficEvents` can grow to 120K before trimming. Consider ring buffer.

4. **Cerebellum region inhibition caching** - Already caches per-step, but could cache for longer if inhibition changes slowly.

---

## Code Quality Notes

### Thread Safety
- All subsystems properly use `lock(_gate)` for state mutations
- Volatile fields used for lock-free read paths
- No obvious race conditions

### Memory Management
- No obvious memory leaks
- Most allocations are in initialization, not hot paths
- Episode and association limits prevent unbounded growth

### Numerical Stability
- Proper clamping of values to valid ranges
- No division by zero issues found
- Float precision adequate for simulation scale

---

## Test Recommendations

1. **Sleep cycle integration test** - Verify NREM→REM→NREM cycling completes before wake
2. **Resonance detection test** - Inject known pattern, verify detection
3. **ThoughtClusterer test** - Verify no duplicate voxels in clusters
4. **Memory stress test** - Run for extended period, check association count stays bounded

---

## Files Modified

| File | Changes |
|------|---------|
| ResonanceDetector.cs | Ring buffer fix, bounds check, improved comments |
| PredictiveCoding.cs | Key caching to reduce allocations |
| ThoughtClusterer.cs | HashSet for cluster deduplication |
| Hippocampus.cs | Two-phase dictionary update |
| SleepController.cs | BuildPressure(), GetPressure() methods |
| EngineController.cs | /sleep/buildpressure endpoint |
| Home.razor | Build pressure button, pressure progress bar |
