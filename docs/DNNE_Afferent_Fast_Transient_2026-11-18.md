# Joint sensory nerve fast-transient comparison

Status: complete comparison workflow authored and statically checked; **not
prepared or executed**. The derivative study is reviewed/passing. The immediate
execution step is the joint dynamics component study. Its reported, reviewed
and D:-verified passing result is required before this comparison can freeze.

`tools/afferent_trajectory_protocol.py` declares seven conditions: matched
quiet/bundle single-patch states, initially loaded and reverse-current states,
a severed contact, graded two-patch and bundle-driven eight-patch states.
Each uses the declared seven-compartment nerve geometry and actual initial
gates. Matching initial cable and calyx voltages removes the initial interface
voltage jump in selected controls; it does not imply a stationary state.

The grid is 262,144 ticks/ms, with native steps of four, two and one ticks
(approximately 0.00001526, 0.00000763 and 0.00000381 ms). Common samples occur
every sixteen ticks, including the rapid initial capacitive transient. Durations
are 0.015625 or 0.0625 ms. These short intervals are intentional numerical
transient checks and cannot establish sustained firing or physiological rest.
There are 21 prescribed native runs, retaining every accepted endpoint.
Static fixture inspection reconciles 114,709 endpoint records, including the
initial state in each run, unique state/ledger labels and aligned intervals.
The prospective joint-component inventory remains 181 cases; none ran here.

The independent comparison has Radau standard/tight and BDF tight
references, separately evaluated equations, actual free gates and all charge/
ion ledgers. Original sensory state/ledger tolerances and refinement criteria
are retained; cable/interface charge residual bounds are declared as 2e-8 fC.
All accepted reference nodes and comparison samples must remain admissible.

The independent membrane/cable/joint evaluator now has an explicit optional
`allow_trial_gates` argument, default false. Ordinary derivative comparisons
retain their strict physical gate-domain checks and unchanged equations. A
joint implicit reference solver requests smooth polynomial gate extension
for internal Newton trials, as the earlier sensory reference does; accepted
states still require [0,1] gates. No clipping or alternate current is introduced.
This option has not been numerically exercised during authoring.

`AfferentTrajectoryStudy` captures initial state/derivative, all accepted states,
compensated ledgers, clock/nonlinear diagnostics and complete failure prefixes.
It is available only through the explicit probe command, with no live caller.
The authoring probe build passes with zero warnings/errors. Fixture/static
validation is separate from runtime checks; no model or test has run during
authoring.

`afferent_trajectory_reference.py` supplies a complete independent state/ledger
RHS with explicit Jacobian and structurally zero ledger columns. Source timing
tables come from the reviewed evidence. Actual cable and calyx voltages feed
the same-stage reciprocal connection. Every reference segment retains accepted
nodes, dense comparison samples, work metadata and independent 40/60/90-digit
endpoint witnesses. All 21 reference conditions are attempted, with failed
prefixes retained. Working precision is checked against 90 digits at 1e-25
absolute/relative; 60/90-digit agreement keeps the earlier 1e-40 bounds.

`afferent_trajectory_analysis.py` validates complete clocks, named state/ledger
maps, initial/final snapshots, every native endpoint and every retained reference
node/sample. Separate capacitor/incidence matrices reconcile sensory charge and
ions, cable charge and each calyx attachment. The existing sensory bath ledger
retains its subsystem meaning. No whole-axon ionic or metabolic conservation
claim is introduced. Initial full derivatives must also agree under the earlier
derivative bounds before reference integration begins.

The saved-data reviewers require explicit operator completion reports and audit
all C:/D: files, frozen source, binaries, dependencies and declared tests.
`review-afferent-dynamics.py` gates preparation of this comparison.
`afferent_trajectory_gate.py prepare REVIEW_FOLDER` requires that reviewed
181-case result and preserves 46 earlier source files. Its clean pushed Git
archive, protocol, runtime and source timing data receive an initial verified
D: backup. `start-afferent-trajectories-window.ps1` then launches a separate
visible PowerShell window. `review-afferent-trajectories.py` reproduces the saved
comparison only after the operator reports completion. No live monitoring.

Static validation covered five Python files, PowerShell syntax, seven exact
fixture/state mappings, named variable domains, the 181-case source inventory
and all 46 preserved paths. It caught an input list-order mismatch; the typed
boundary now retains fixture order while canonical state vectors use stable
node identity. No values or equations were changed. Neither ODE solvers nor
analytic controls nor tests ran during this authoring. The existing native
capture was unchanged and its earlier successful build remains applicable.

Preserve all failures and original bounds. Longer free trajectories, loaded
roots and spike-event production follow; these short checks cannot establish
a usable vestibular nerve.
