# Cortical contact precision result — 6 November 2026

The operator reported `cortical-contact-numerics-20261106-f9f915` complete.
Frozen revision `6445104403219786dfb8f9edd4366c825eadcaaa` completed at
13:38:05 UTC. All 27 candidate cadence cases satisfy the predetermined numerical
criteria. All 16 candidate fixed-history replays complete, and the complete old
16-replay/27-cadence baseline reproduces exactly.

`tools/review-cortical-contact-numerics.py` independently recomputed the analytic
comparisons, original bounds, event/weight accounting and retained-state checks
without executing learning. It verified all 1,480 files (69,752,584 bytes) in
each C:/D: capture, 1,319 packaged files and frozen binaries. Review:
`artifacts/cortical-contact-numerics-review-20261106/review.json`.

## Numerical evidence

| Fixture | Error at 4 ms | Error at 0.015625 ms | Finest final weight |
|---|---:|---:|---:|
| Pre-before-post | 1.43648e-8 | 5.61016e-11 | 1.000057471032613 |
| Post-before-pre | 3.46068e-9 | 1.35156e-11 | 0.9999861545272747 |
| Unpaired | 0 | 0 | 1 |

Errors decrease with refinement for both signs. All steps retain strengthening
or weakening, unlike the legacy calculation. No cadence case charges the limiter
without changing its stored weight, and the blank contacts remain unchanged.
The analytic comparisons are within the unchanged 3e-8 overall and 1e-9 finest
bounds. The three invalid-input controls also preserve accepted state.

The candidate's recorded colocated history ends at 1.0066832238992056, versus
1.006630840730931 with capture disabled: a fixed-history difference of
0.00005238316827460565. All sixteen recorded-history interventions report zero
charged updates lost to weight rounding. The neighbour and other non-modulated
histories remain invariant to the chemical interventions. This repair does not
boost the learning rate or guarantee larger final weights.

## Qualified scope and next integration checks

The isolated numerical calculation passes over the tested domain. This does not
qualify the normalized receptor multiplier, provisional limiter, biochemical
consolidation, actual cortical network integration, a full unit suite, or Avatar
recovery. No live service selects the candidate.

Before enabling it in actual cortical cells, preserve double state across the
learning/contact boundary and define explicit save identity and restore behavior.
The current `SynapseState`/persistence fields are floats; merely copying the new
weight back into those fields and reinitializing from them would lose the repair.
`StructureEngine.ApplyLearnedInboundStrength` also bypasses efficacy differences
below 1e-6 and uses a float message before `ModelNeuron.Integrate`. These are
inspected integration boundaries, not repaired by this result.

Use fresh isolated cortex with actual neuron-generated postsynaptic events,
baseline reproduction, explicit contact targeting and retained full-precision
evidence. Test save rejection/preservation before considering live promotion.
The original random consolidation assertion remains unresolved and its derived
ensemble score must not be used as proof of biochemical memory.
