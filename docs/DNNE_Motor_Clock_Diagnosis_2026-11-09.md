# Motor-cell elapsed-time diagnosis

The completed fixed-state review in
`artifacts/olive-motor-review-20261109/motor-review.json` reproduces 396 states.
Both implementations return identical calcium derivatives at those points.
For the two scenarios with remaining refinement failures, local calcium arithmetic
bias times the removal constant is much smaller than the observed trajectory
discrepancy. That product is not a trajectory error bound, but the result does not
support repairing a mismatched local calcium equation. The original failure and
its limits remain preserved.

## A separate timing discrepancy in the run harness

`ActiveStudy.Run` chooses `dt = min(maximumStep, sampleOrCommandEnd - cell.TimeMs)`.
The cell integrates that requested `dt` and then executes `TimeMs += dt`.
The represented floating-point change in `TimeMs` need not equal the supplied
`dt`. The run harness uses that rounded clock to shorten subsequent steps until
the sample boundary. Consequently, a sampled timestamp can differ from the exact
sum of the durations actually supplied to the membrane integrator. Compensating
the physical-state additions does not compensate this clock-dependent scheduling.

This is a concrete discrepancy in the arithmetic path, but its magnitude over
this protocol and its contribution to the calcium failure need measurement.
No channel, conductance, calcium equation, current, solver step or original
accuracy limit is changed by this diagnosis.

The endpoint-difference alternative first chooses a represented absolute endpoint
at or below the requested boundary, then supplies `endpoint - previousTime`.
If rounding an endpoint upward would exceed the maximum step, it uses the adjacent
lower representable endpoint. Each supplied interval must remain positive, within
the original maximum, and add back to the chosen endpoint. This is only a scheduling
candidate; it is not selected by the active neuronal study or a live host.

## Visible clock-only experiment

`MotoneuronReferenceProbe --clock-audit` reconstructs the original scheduling and
the endpoint-difference alternative for all three original steps: 0.0125, 0.00625
and 0.003125 ms. Each has the original 0.1 ms sample grid through 6,000 ms and
explicit command boundaries drawn from the original protocols. Those boundaries
all coincide with samples, so their union does not introduce new split locations.
There are six cases. No cell, channel RHS, brain, muscle, world or GPU is advanced.

The diagnostic decodes every supplied binary64 duration into exact integer units
of 2^-80 ms, rejecting any duration not exactly representable in that unit. Int128
has ample declared range for this 6,000 ms study; accumulation is checked. This
measures the arithmetic actually represented in the old protocol, not ideal decimal
values. It retains supplied duration, reported clock, signed discrepancy, call
count and rounded-addition count at all 60,001 samples. All command boundaries,
maximum discrepancies and exact final totals remain available in saved CSV/JSON.

The Python audit independently replays both schedules and converts each duration
through its exact integer ratio, without using the C# bit decoder. Every saved
sample, accumulated total, step count and rounding count must agree. The candidate
requires zero discrepancy in these exact units at every sample. Original-clock
mismatch is reported as an outcome; a completed diagnostic does not pretend to
have rerun or repaired the original failed neuronal comparison.

The preparer verifies the completed fixed-state review on C:/D:, checks that the
active run harness, cell and channel source still match that captured source, and
freezes clean pushed source. The visible PowerShell runner builds the probe,
captures the six schedules, independently audits them, rehashes sources/binaries
and backs up the final evidence. No live monitoring. Its outcome is clock accuracy,
not a speed benchmark, so it does not require an otherwise idle CPU.

The probe builds with zero warnings/errors and Python/PowerShell syntax checks
pass. Runtime diagnostic results are pending. If the discrepancy is reproduced,
the next causal test should change the scheduling alone and compare the same
neuronal trajectories against the preserved references and original bounds.
Until then, calcium attribution, active motor-source qualification, physiological
calibration and Avatar recovery remain open. No old saves or live behavior change.
