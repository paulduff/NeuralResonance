# Connected arrival and calcium timing repair

The completed olive-to-Purkinje comparison exposed outer-step sensitivity before
learning was interpreted. The next experiment isolates numerical scheduling and
voltage/chemistry coupling. It does not fit a stronger learning effect or claim
physiological qualification.

## Production changes

Previously, StructureEngine consumed all arrivals due through an integration
interval's end, then advanced the cells over the entire interval. A release at
the end could therefore influence time preceding its delivery. The new scheduler
integrates up to each known arrival boundary, applies release at that boundary,
and continues from there. A final-boundary release is consumed without advancing
chemistry over elapsed time. Late transport is processed at the current time;
scheduled and processed times remain distinct in observations. No rewind, event
merging or fabricated activity is introduced.

The integration grid is anchored to biological time, rather than restarted
after an event. Olive and Purkinje engines use a maximum 0.125 ms coupled step.
Other models retain their existing ceilings, but receive the same causal arrival
scheduling repair. A fresh isolated diagnostic constructor permits smaller
ceilings; service configuration cannot select that override.

Contact calcium now uses the receiving voltage's start and end values, with a
linear trajectory and midpoint samples for its chemical substeps. Previously it
received only the final voltage, held over the whole interval. Linear
reconstruction remains an approximation whose resolution must be checked. The
passive climbing compartment, channel equations, calcium density/buffering,
IP3/store equations, LTD dose response, maximum-history induction and expression
timescale are unchanged. Missing active dendrites, LTP and physiological chemical
energy accounting remain outstanding.

`event-aligned-calcium-v1` participates in **all StructureEngine save fingerprints**.
Older saves are preserved and rejected on mismatch; there is no silent migration.
No live brain, world or Avatar is started. Editor learning diagnostics expose the
integration version through their existing model-status field. Avatar physiology
and the editor's display interfaces require no change for this isolated repair.

## Tests and experiment

Five new regression tests check a release exactly at the interval end, fractional
feed-forward/feedback delivery, truthful late-arrival reporting, packet-period
invariance at a fixed physical grid, and voltage-ramp coupling of chemistry.
The complete DNNE test suite passes 1,049 tests.

`PosturalCellProbe --connected-timing` runs 120 fresh conditions:

- Calcium candidate and calcium-entry-blocked control.
- PF-leading 100 ms, PF-leading 20 ms, disconnected olive, olive inhibition and
  olive glucose withdrawal.
- Two onset offsets, 0 and 60 ms.
- Host packet periods of 1 and 0.125 ms, varied independently of the solver.
- Coupled solver ceilings of 0.125, 0.0625 and 0.03125 ms.

Each full condition lasts 60 biological seconds. It preserves the previous 58
stimulus probes and 10–30 s challenges. All fixture emission times are shifted by
0.03125 ms so releases do not always coincide with the coarser grids. The actual
olive spikes and conduction delays determine the climbing-fibre arrival times;
no nominal PF label is treated as an exact PF-to-CF interval. All axonal spikes
are transported individually. Both receiving cells, dormant contacts and
nonparallel contacts remain observable. The source is unidirectional, without
body/world feedback or a simulated granule population.

The short technical preflight lasts four seconds per condition. `--quick` uses
one onset offset (60 conditions); the independent PowerShell run uses both
offsets in preflight and study. Cell/contact traces are sampled every 20 ms, with
all release and outgoing spike events recorded separately.

The validator requires complete schedules, exact arrival times, contact ownership,
finite energy/calcium accounting and no direct calcium-rule weight decrement on
arrival. At each fixed solver ceiling, the two host packet periods must produce
identical electrical/chemical trace bytes and identical source/receiver spike
times. This is a necessary transport/scheduling check, not proof of mathematical
convergence: equal packets use the same underlying numerical grid.

Separate successive-refinement comparisons retain spike counts, ordinal spike
timing differences when counts match, peak calcium, induction and final weight
differences. A count mismatch is reported explicitly, with no ordinal timing
claim for unmatched trains. These differences require review after completion;
the script does not equate FINISHED with solver or biological qualification.
Further refinement is required if the changes remain material relative to the
very small learning effects, or fail to diminish as the step is reduced.

The prior completed captures and their review stay immutable. The new committed
source, executable, tests, preparation evidence and protected-file hashes are
frozen into a new package with a verified D: backup. The experiment runs in an
independent visible PowerShell window with no live monitoring. Upon completion,
assess numerical timing before changing calcium-dose calibration or consolidation.
