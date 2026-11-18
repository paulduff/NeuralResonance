# Sensory nerve activation continuation — 18 November 2026

## Completed precision result

The operator reported `afferent-precision-20261118-ddef1a` finished with FAILED.
The completed review verifies 2,983 files in each C:/D: copy, all 181 focused
tests, eight spectral witness controls, 21 native trajectories and 21 independent
references. Saved analysis reproduces exactly. The original failure remains.
Review: `artifacts/afferent-precision-20261118-ddef1a-review/review.json`, D: verified.

All seven independent reference comparisons now pass. All finest native direct
error limits and charge/ion accounting checks also pass. Six of seven sampled
refinement criteria still fail; only loaded-one passes. Reducing native nonlinear
stopping tolerances 1,000-fold barely changes the errors, so a loose stopping
tolerance is not their primary explanation.

| Case | Maximum normalized error, 4 / 2 / 1 ticks | Refinement |
| --- | --- | --- |
| quiet-one | 0.676569 / 0.399331 / 0.185190 | fail |
| bundle-one | 0.583272 / 0.395755 / 0.179589 | fail |
| loaded-one | 0.759835 / 0.189596 / 0.047354 | pass |
| reverse-one | 0.699100 / 0.174439 / 0.081213 | fail |
| cut-one | 0.328385 / 0.078900 / 0.054690 | fail |
| graded-two | 0.881016 / 0.244515 / 0.224951 | fail |
| bundle-eight | 0.218103 / 0.084449 / 0.052164 | fail |

The fixed comparison bounds, fixtures and all original traces are unchanged.
Numerical agreement over these short transients does not establish rest, firing,
human physiology or balance.

## Saved boundary diagnosis

`tools/diagnose-afferent-boundary.py` uses only completed saved samples and error
arithmetic. It does not evaluate any derivative, advance a model or run a test.
Its source and report are preserved in the two-file D:-verified artifact
`afferent-boundary-diagnosis-20261118`. Input hashes bind the report to the reviewed
failure. Every case refines approximately fourfold before its first sampled
crossing of the sodium activation table boundary at -55 mV. The activation gate
error grows sharply afterwards. No sampled crossings of +60 mV were observed.

The original activation timing rule is 0.2 ms outside [-55,60] mV, but its first
table value is 0.26131 ms and its last is 0.21091 ms. This creates discontinuous
gate derivatives at the table boundaries. The preserved source does not establish
a biological switch at either voltage. Correlation with the saved crossings is
strong evidence to investigate, not yet a controlled causal demonstration.

## Explicit candidate and original control

The candidate holds the nearest activation table endpoint outside the measured
range. It is continuous, but retains slope corners at the boundary and interior
knots. Below -55 mV its activation time increases by 30.655% versus the original;
above +60 mV it increases by 5.455%. This changes a modelling assumption. It is
not an equivalent numerical solver for the original equation, nor established
human sodium kinetics.

All original table values, interpolation inside the table, equilibrium gates,
other channel rates, currents, geometry, inputs, synapses and learning remain.
Equilibrium locations are algebraically unaffected by changing a positive time
constant; their dynamic stability or physiological validity is not inferred.

`AvatarSodiumActivationContinuation.EndpointHold` is explicit. Original APIs
default to `DepositedFixedValue`, retaining their fingerprint expressions.
The candidate has a distinct membrane fingerprint propagated through cable and
joint dynamics. The probe has a separately named command and writes its policy
receipt. The independent Decimal reference takes the explicit candidate source
decision; absence of that decision retains the original continuation.
Original source decisions are retained separately, including the original
deposited expressions. No live selector, editor signal or old save is changed.

## Prospective comparison

`afferent_endpoint_gate.py` prepares from the reviewed precision failure and
the separately reviewed 317-case physical bundle capture. The independent visible
PowerShell workflow will:

1. Verify frozen inputs, source dependencies, runtime and numerical environment.
2. Run eight spectral witness controls and 332 component checks: the prior 317
   plus 15 continuation cases, including boundaries, defaults, fingerprints,
   actual-state currents, clamp composition and invalid policies.
3. Replay all 21 original native trajectories. Require byte-identical inputs,
   initial/final records, derivatives, fingerprints and every CSV value against
   the completed failure. Stop if this preservation check fails.
4. Run 21 candidate trajectories at exactly the same clock, steps and settings.
5. Require exact original 60/90-digit initial derivative witness reproduction;
   generate new candidate initial witnesses before new Radau/BDF references.
6. Apply the original direct, reference, refinement and conservation limits to
   all seven candidate cases. Preserve failure or success and verify D: backup.

All 56 protected sources match the completed bundle capture. Both authoring
builds (full tests project and native probe) pass with zero warnings/errors.
Python syntax, PowerShell parsing, static 332-case inventory and original protocol
identity pass. No tests or models executed during authoring. The original replay
and candidate comparison remain unqualified until the window finishes and the
operator reports it. No live monitoring.

Only the older saved motor comparison `e9bed6` is pending before this new launch;
its output and processes remain untouched. Full DNNE/Avatar/World goal remains
active. Loaded rest, calibrated afferent firing, mechanical parameters and
closed-loop Avatar balance/recovery remain unfinished.


The explicit activation continuation comparison is now launched as
`afferent-endpoint-20261118-2b5a68`, frozen at pushed revision
`314a072e72831ea479ecb134049b9400c21ea9e9`. Static frozen preflight passed;
2,065 initial files plus manifest were D: verified before visible task
`DNNE-Afferent-Endpoint-19a507a1d31c` started. Launch returned successfully.
No post-launch output, process or status was inspected. The window runs 332
focused cases, 21 exact original native replays, 21 candidate trajectories and
21 independent references under the original acceptance bounds. No success is
inferred. Original defaults, failed captures, saves and live selection remain.
The operator has now reported older motor `e9bed6` complete; its saved review is
underway. Exactly ONE completion report is pending: new sensory `2b5a68`.
Full DNNE/Avatar/World goal remains active and unfinished.


The operator reported sensory activation window `2b5a68` FAILED. Its completed
review verifies 2,609 C:/D: files, exact 332-case inventory (331 pass, one fail),
eight spectrum controls, frozen source/binaries and runtime. The one-file review
is D: verified. No original replay, candidate trajectory or independent reference
started. The failed case is the one-ULP interior neighbour of +60 mV: subtraction
rounds the last interval index up to the final knot, then indexes beyond the
array. This defect exists in the shared original table lookup and was exposed
by the new boundary control; it is not a candidate trajectory outcome.
The repair bounds the interval index to the final valid segment, preserving
voltage, knots, interpolation, outside continuation and scientific limits.
A second regression checks both activation/inactivation tables and both policies.
The retry now has 333 focused cases; the original boundary test remains intact.
Its original 21-trajectory byte replay still guards previously successful v1.
No tests/models executed during authoring. No completion report is pending
before retry launch; all older windows are reviewed.


Retry storage admission uses the reviewed capture sizes rather than the former
unmeasured 6 GiB threshold: old total 719,985,914 bytes, native set 509,984,161
bytes. The new final footprint is projected as old total plus two native sets
(preserved baseline and original replay), or 1,739,954,236 bytes. Preparation
requires twice that projection plus a 2 GiB reserve: 5,627,392,120 bytes. Measured
available space before authoring was 6,245,572,608 bytes. The frozen package
records its actual admission values. Each new model phase also requires the
entire projected footprint plus the reserve still free, and saves a separate
check receipt. D: retains its 8 GiB minimum. This is evidence-based capacity
planning; no files, old results or saves are deleted, and no numerical acceptance
bound changes. The unrelated 8 GiB motor capture guard remains unchanged.


The boundary-index repair retry is launched as
`afferent-endpoint-20261118-501244`, frozen at pushed revision
`a3474953a1aff5b26c63cffce3eaaab884b902ef`. Full test-project authoring build
passed with zero warnings/errors; static 333-case inventory, Python/PowerShell
parsing, original protocol identity and 56 protected-source comparisons passed.
Frozen preflight passed and 2,069 initial files plus manifest were D: verified
before visible task `DNNE-Afferent-Endpoint-11b2b1598b62` started. Launch returned
successfully. No post-launch output, status or process inspected. All tests and
models execute only in the independent PowerShell window. Exactly ONE pending
completion report: retry `501244`. Failed `2b5a68`, precision `ddef1a`, bundle
`092238` and motor `e9bed6` are all reviewed, not pending. Failed traces, old
saves, original successful-replay requirement and numerical limits remain.
Full DNNE/Avatar/World goal remains active and unfinished.
