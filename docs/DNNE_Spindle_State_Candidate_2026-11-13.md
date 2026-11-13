# Canonical intrafusal state candidate

The completed `98c085` reference revealed that imposed length changes update
`y` but leave reported populations stale. The slack-force search reads those
stale populations. Initial force is also nonscalar because a column state is
multiplied elementwise by row coefficients. The preserved source and results
remain unchanged; see `DNNE_Muscle_And_Spindle_Results_2026-11-13.md`.

The next isolated candidate makes `y` the authoritative state. Reported bound
bins, detached population and total bound population become read-only dependent
properties. Initialization uses the same 84 values in consistent row form.
The positive-overlap kinetic fluxes, parameter values, interpolation, original
split ordering and Octave solver defaults are retained. The preparer records
every source edit and its before/after hashes, and retains the original AGPL-3.0
license with the separate derived source tree.

Geometry now assigns exact-zero overlap deterministically and refreshes after
zero-duration imposed shifts too. Kinetics reject zero, negative, nonfinite or
greater-than-one overlap before evolving state. This is an explicit unsupported
domain, **not a zero-overlap physiological repair**. No epsilon denominator,
population clipping, invented activation reset or automatic continuation is used.

## Why zero overlap needs a separate mechanism

In the pinned source, active eligible sites `a` include bound sites `b`, and the
fluxes contain `a/F` where `F` is geometric overlap. For admissible states write
`a = F*q`, `b = F*r`, with `0 <= r <= q <= 1`. The activation/deactivation fluxes
become `k_on*Ca*F*(1-q)*(1+q)` and `k_off*F*(q-r)*(2-q)`. They tend to zero as
`F` tends to zero along admissible states. However, jumping geometry to zero
while retaining nonzero `a` or `b` does not satisfy those assumptions. This is
an algebraic inference from the preserved equations, not a validated additional
biological rule.

A complete extension must specify how sites and attached heads enter/leave the
overlap region and how activation outside that region is represented. The
existing aggregate state does not define that history. Assigning zero geometry
alone cannot provide it. The original 30 imposed trajectories remain within
positive overlap; their domain and site availability are checked explicitly in
the new comparison. Extreme extension/injury behavior remains unfinished.

## Force-query building block

`tools/spindle_trial_stress.m` evaluates a trial length using `y`, the original
linear shift with zero outside the bin support, and passive stress at the trial
length. It returns stress in Pa, shifted populations and the detached mass,
without mutating the input fiber. It supplies the mathematical building block
for the forthcoming slack solver; it does not select a full slack controller.
The original driver's coupled slack flags and time convention still require
repair before a live causal port.

## Comparison and controls

`spindle-reference.py prepare-state` freezes clean pushed source, the unchanged
upstream and Octave compatibility copies, a separately hashed canonical-state
copy and all 30 previously reviewed baseline trajectories/two boundaries.
It verifies the reported review and selected baseline C:/D: bytes, then the
entire 59,532-file Octave runtime before any execution. Each original source
snapshot and its backup remain untouched.

The independent PowerShell window runs:

- The same 30 bag/chain trajectories at 1, 0.5 and 0.25 ms, with true initial
  states and the original imposed-length and pCa fixtures.
- Two original shift/overlap boundary records, now requiring consistent state
  views, scalar force and history-independent exact-zero geometry.
- Both fibers' read-only-property, trial-force, analytic unloaded-root, detached
  mass and rejected-domain controls. Trial queries must not mutate state; their
  results must match applying the same shift to a separate fiber instance.

State differences from saved baseline must be at most `1e-12`; post-initial
stress differences at most `1e-7 Pa`. Initial stress must equal the analytic
passive stress. All reported population views must match `y`, with myosin/actin
accounting and eligible-site bounds checked to `1e-12`. Trial stress uses the
same `1e-7 Pa` bound; its empty-crossbridge root must match slack length within
`1e-9 nm`. These are representation/equation-consistency checks, not independent
ODE convergence or physiological acceptance. The original outer-step
differences remain descriptive and must not be relabelled convergence.

Preparation and Python/PowerShell syntax checks run without models. Octave class
loading, controls and trajectories run only in the visible window, with results
reviewed after the operator reports completion. The candidate is isolated:
actual gamma firing, causal receptor transduction, afferent membrane calibration,
full slack/re-tension, Avatar/Unity selection and recovery remain outstanding.
