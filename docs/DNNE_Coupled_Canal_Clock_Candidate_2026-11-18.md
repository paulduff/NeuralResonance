# Coupled canal clock candidate — 18 November 2026

This is a saved, build-checked candidate, not a qualified runtime path.
`AvatarClockedCoupledCanal` and 21 authored test cases are present. The full
test project compiled with zero warnings/errors into
`D:/DNNE-build-work/canal-clock-authoring-20261118`; no tests or mechanical
trajectories have been executed for this owner. No qualification package or
window has been launched. The operator requested a stop after the two remaining
studies, so qualification is deferred to a future authorized continuation.

The owner composes the unchanged skull-to-prime-coordinate adapter and coupled
canal solver. It requires an explicit initial volume/flow state and a bounded
power-of-two number of substeps per original physical interval. The source
identity, initial state, timing, subdivision and handoff policy participate in
its fingerprint. There is no default state reset, automatic timestep selection,
parameter normalization or live selector change.

The first head observation has no angular acceleration. The second makes the
first acceleration available and establishes the explicit mechanical initial
state at that endpoint; the earlier interval is not silently integrated. Each
subsequent interval uses the input available at its starting endpoint. Newly
arriving acceleration can affect the following interval only. This delayed
zero-order hold is an explicit sampling approximation, not a claim of biological
conduction latency. Its error against continuously moving head input still
needs independent investigation.

Exactly one completed mechanical interval may await acknowledgement. A new head
endpoint is rejected until that interval is acknowledged; exact latest retries
are inert. Each original head identity and every mechanical substep are retained.
Physical and neural timestamps use the original epoch and an exact integer
substep index. Exhausted timestamp resolution fails instead of merging samples.
Physical and numerical dissipation remain separate, with compensated cumulative
work/loss sums and an energy residual. State commits only after a whole interval.
If a later substep fails after its closing head sample was accepted, the owner
retains that accepted input, the successfully attempted substeps and the last
committed mechanical state; it faults rather than silently restarting.

The 21 authored cases cover initialization, causality, subdivisions, original
epochs, handoff pressure, replay/conflicts/gaps, immutable returned evidence,
separate dissipation, identity, clock exhaustion, retained failure prefixes and
single-thread ownership. They remain unexecuted. The next qualification needs
those controls, earlier component regressions, saved native-head replay and
independent organ trajectories at prospectively declared subdivisions. Preserve
every attempted interval and reference sample, and report sampled forcing error
separately from the core solver's numerical agreement.

The printed posterior-canal direction ambiguity remains unresolved. This owner
does not establish anatomical alignment, bilateral registration, a map to bundle
deformation, physiological calibration, sensory nerve firing, live brain
authority or Avatar balance. No editor or live World selection has changed.
