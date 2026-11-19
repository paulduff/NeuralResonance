# Midpoint sampling of completed canal movement

The full replay is now launched at
`D:/DNNE-build-work/canal-midpoint-trajectory-20261119-e503fd`, frozen from pushed
`4488f9da1590efbed84c7316c03b5ec941e5a66e`. Frozen preflight and both initial
2,554-file copies plus manifest verify. Task
`DNNE-Canal-Midpoint-Trajectory-729856f67461` runs visibly and independently;
no post-launch inspection occurred. Await the operator's final backup/footer
before reviewing its new capture or comparison. No outcome is inferred here.

The 172-case component workflow completed at
`D:/DNNE-build-work/canal-midpoint-20261119-f8a807`, frozen from pushed
`7b50528fb3a2e29bccc52a5e8a01984dd791b6d3`. After the operator's final footer,
both final 2,742-file copies, frozen source/runtime/binaries, exact case inventory
and saved analysis were verified. All 172 cases passed; the three-file review
plus manifest is D: verified. No live monitoring occurred. No candidate full
trajectory accuracy result is available yet.

The completed continuous-input comparison `canal-reference-20261119-2a402c`
failed its moving-head accuracy limits. All six independent reference solutions
agree within their specified limits; stationary-head passes. Both 2,538-file
copies, source/dependencies and saved criteria were verified after the operator
reported completion. The failure and its four-file D:-verified review remain
unchanged. The reviewer reconciles saved report criteria and verifies all array
bytes; it does not claim to have independently recomputed every error envelope.

| Moving native resolution | Worst error / original bound on common grid |
| --- | ---: |
| 16,384 steps per physical interval | 5.813469959370386 |
| 32,768 steps per physical interval | 2.9067430863890196 |
| 65,536 steps per physical interval | 1.4533849339385192 |

The finest all-endpoint ratio is 1.453385383035444 at source tick 637359,
`apex/apex/sodiumToBathAttomoles`. Charge and ion conservation pass. The
observed convergence factor is approximately 0.5, failing the existing
`finer <= 0.4 * coarser + 0.01` requirement. Small voltage errors do not justify
dropping cumulative ion/charge accounting or widening the limits.

## Candidate and causal boundary

`AvatarCanalAfferentInput` previously always sampled bundle displacement at the
beginning of each neuronal step and held that value for the step. This introduces
a first-order forcing approximation even when the neuronal integrator itself is
more accurate. The observed halving of error is consistent with that source,
but a controlled comparison is required to establish the cause and remedy.

The explicit `AvatarCanalForcingPolicy.MidpointHeld` option instead samples the
exact middle mechanical tick and holds it for that neuronal step. It requires
an even mechanical/source tick ratio of at least two. It never rounds a half
tick or extrapolates. The entire mechanical interval must already have been
released by the original body-clock owner. The closing head observation cannot
retroactively drive that interval; the midpoint is obtained from its retained
dense mechanical trace. No future body state is requested.

The default remains `StartHeld`, with its original fingerprint. The midpoint
mode has a separate owner version/fingerprint. Every original neuronal equation,
initial state, gate, ion concentration, compensation, flux ledger, step boundary,
crossing observer, event identity and acknowledgement remains in the existing
path. The receipt records the actual midpoint bundle sample. No resettling,
extra stimulus, threshold change or firing-rate shortcut is introduced.

The explicit probe command `--canal-afferent-midpoint` now selects the candidate
through `CanalAfferentStudy.RunMidpoint`. The original command and start-held
capture schema remain available. A separate protocol version and `sampling.json`
record the actual selected owner, fingerprint and midpoint offset. The full
replay is prospective; no live/editor consumer selects this option, and improved
trajectory accuracy has not yet been demonstrated.

## First gate: input and ownership controls

The candidate adds eight controls while preserving all 164 reviewed motor,
recurrent, recipient and sensory controls. Both authoring projects build with
zero warnings/errors. Tests run only through the independent visible
`start-canal-midpoint-window.ps1` workflow after a clean pushed source snapshot,
verified prerequisites and initial D: backup. No live monitoring is permitted.

The new cases cover distinct versioning without initial evaluation, rejection
of unaddressable midpoints, waiting for completed history, exact dense midpoint
delivery into the existing passive and active kernels, original clock/event
preservation, retries/backpressure, packet partition across physical intervals
and rejection of retroactive closing-head input. These fixtures are component
controls, not physiological sensory firing or complete trajectory qualification.
The gate records the exact declared owner diff and hashes while preserving all
other previous kernels and test sources. Full recurrent traces remain required.

## Required continuous comparison after the controls

After the operator reports completion and the 172 cases are reviewed, capture
the same two conditions at the original three neuronal resolutions with the
explicit midpoint option. Reuse the six completed independent continuous
references only after verifying source/problem identity and every reference
byte. Preserve the original mechanical input, loaded initial state, neuronal
equations, solver settings, event criteria, endpoint set and accuracy bounds.

Compare all 2,293,760 steps, including early transients and every cumulative
ledger, and require both absolute accuracy and the original refinement checks.
Retain original and candidate outcomes separately. If the candidate still fails,
inspect which components and intervals dominate before changing another factor.
Even a passing comparison will establish sampled numerical accuracy only;
physiological firing, live brain authority and Avatar balance need later evidence.

## Full replay implementation and preservation

`canal_midpoint_trajectory_gate.py` requires the completed, D:-verified midpoint
review, original failed reference review and earlier connected capture review.
It freezes clean pushed source and copies all 373 files of the six preserved
reference solutions, plus the original mechanical and loaded-state evidence.
It reserves space for both copies and the new native capture before preparation.
The original reference problem and every original bound are retained exactly.

The visible `start-canal-midpoint-trajectory-window.ps1` rebuilds, checks the
matched managed runtime, runs all 172 unchanged component controls and 24 Python
controls, then captures six midpoint conditions. Accounting reconstructs every
held movement value from the retained mechanical stages at the exact midpoint;
it also verifies original body history, neuron states, clocks, conservation,
observed events, packets and acknowledgements. The same continuous comparison
then reads every new endpoint and the saved independent solutions. No ODE is
rerun to regenerate references and no saved trace is rewritten to change input.

Four harness files have declared normalized before/after hashes and full diffs:
the probe dispatcher, capture adapter, input-accounting analyzer and comparison
entry point. The comparison tail is checked textually against the original,
allowing only supplied native/reference root arguments. All neuronal and
mechanical runtime kernels are unchanged from the passed component snapshot;
all 396 original Python sources outside the two declared analyzers are protected,
including dynamically imported modules. Original failure status remains FAILED.

The 24 analytic controls comprise the original 16 comparison/sampling cases and
eight new protocol/coordinate controls: forcing-only changes, version identity,
exact start/midpoint coordinates at all resolutions, unaddressable half ticks,
invalid/noninteger/duplicate coordinates and overflow. They run in PowerShell
before native capture. Both authoring projects build with zero warnings/errors;
static syntax, inventories and preserved-source checks pass. These authoring
checks have not executed the new tests or candidate trajectories.
