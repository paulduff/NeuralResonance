# Native hinge physical and muscle step refinement

The reviewed `native-hinge-comparison-20261116-967542` passes trajectory accuracy
at the original native step. Before interpreting neuronal drive through this
fixture, test whether the physical partition converges and whether muscle
integration error is independently small. This is one combined visible study,
not a live Avatar trial or a performance benchmark.

## Prospective matrix

Five conditions each run quiet, left, right and balanced cases plus all eight
native rejection controls. The baseline repeats the earlier protocol to expose
behavioral drift from the configurable clock. All finish at exactly the same
128.00000607967377 ms and preserve the original motor arrivals and identities.

| Condition | Nominal physical step (ms) | Steps per case | Muscle substeps |
| --- | ---: | ---: | ---: |
| coarse | 0.25 | 512 | 4 |
| baseline | 0.125 | 1,024 | 2 |
| fine | 0.0625 | 2,048 | 1 |
| baseline-muscle-fine | 0.125 | 1,024 | 4 |
| fine-muscle-fine | 0.0625 | 2,048 | 2 |

The first three conditions preserve the same maximum muscle step, allowing
physical partition error to be distinguished from muscle integration error.
The latter two halve the muscle step at fixed physical resolution. Events still
split their actual arrival intervals. Values above are nominal labels; the
actual physical times use exact promotion of Unity binary32 seconds, scaled by
powers of two, and integer step multiplication. Common endpoints match exactly.

Total coverage is 20 physical conditions / 26,624 steps and 40 native rejection
controls. Each resolution retains the exact scalar codec preflight and deliberate
completed-reply read lock. Invalid commands must be rejected before stepping;
read recovery must never resubmit a physical command. Every command, reply,
muscle state, source batch, physical state and failure prefix is retained.

## Implementation

The isolated wire and Unity owner support only resolutions -1, 0 and +1.
The owner freezes its selected physical step at initialization, includes it in
geometry identity and uses it for both simulation and exact receipt times.
The host requires that same declared clock. Its default is unchanged; permitted
muscle/physical combinations are restricted to the five rows above. This does
not make packet arrival time a physical clock or select a production body.

Existing Avatar muscle/calcium/causal/path equations are unchanged. Twelve
`MuscleHingeClockTests` cover all three exact clocks/common endpoints, rejection
of nominal decimal endpoints, nonmoving reads, unsupported resolutions and an
unrecognized current clock. They join the previous 81 checks for 93 focused
cases. The Unity project and driver share the same wire source.

The existing command/geometry/impulse audit now takes explicit resolution and
muscle-substep parameters. Its default analysis exactly reproduces the complete
`e7ddb6` result. Its original impulse/angle formulas and tolerances are retained,
evaluated at each actual step. The capture parser accepts only the declared
512/1,024/2,048 counts, preserving exact receipt, event and state checks.

## Prospective numerical criteria

The 12 already-qualified continuous reference traces are copied from `967542`,
verified against its C:/D: manifest and reused without numerical reintegration.
There are 1,025 reference samples at the original physical cadence. Native and
reference arrays are compared only at exactly matching recorded timestamps;
there is no interpolation. Coarse uses 513 reference samples, baseline 1,025,
and fine uses its 1,025 even-numbered endpoints. All native endpoints, including
the fine half steps, still receive domain, event, release, geometry and ledger
checks. Independent reference accuracy at those extra half steps is not assessed.

Physical convergence uses 513 common endpoints across coarse/baseline/fine.
For each recorded quantity, finer error must be at most 0.8 times coarser error,
or below 0.02 times its existing absolute bound. These are the earlier analytic
coupled study's prospective ratio and numerical floor, unchanged. Baseline and
both fine-step/muscle-refined variants must meet the original absolute bounds
against every reference method. Coarse absolute errors are reported but are
not required to pass: it deliberately probes larger splitting error.

For each same-physical-step muscle pair, differences at every native endpoint
must remain below 0.05 times the original absolute bounds. Baseline repetition
must agree with the original captured run within 0.01 times those bounds;
bitwise state equality is also reported, without being required. The protocol
constants live in `native_hinge_refinement_protocol.py` and are frozen before
execution. Bounds must not be changed after observing results.

This study can establish sampled resolution convergence for this isolated
synthetic muscle pair. It does not establish a running neuronal source, all-time
continuous accuracy, human joint anatomy, physiology, sensory feedback, balance
or Avatar recovery. `fullCoupledTrajectoryQualified` stays false. The intended
following experiment uses genuine neuronal motor events through this qualified
mechanical path, with those limitations retained.

## Validation and execution

The driver, test assembly and native authoring compilation pass with zero
warnings/errors. Python and PowerShell syntax validate. The generalized default
capture audit and protocol reproduce the previously reported captures exactly.
These authoring checks ran no tests, numerical integration or native physics.

Prepare with the unchanged numerical environment using
`tools/prepare-native-motor-bridge.py --refinement`. The preparer requires clean
pushed code, verified original reviews, source parity for the qualified numerical
and muscle cores, exact dependency hashes, metadata validity and complete test
method counts. The existing visible PowerShell launcher builds one isolated
player, runs 93 tests and the five-condition native matrix, audits each condition,
compares references, checks final source/dependency identity, and verifies D:
backup. All worlds in this fixture start paused; no old save is loaded. Do not
live-monitor; inspect outcomes only after the operator reports completion.

## Launch record

`native-hinge-refinement-20261116-421387`, frozen revision
`e59d9ea89adc049376b71bd933fa40986cb3d196`, is launched in independent visible
PowerShell task `DNNE-Native-Hinge-Refinement-4fa325232e75`. All 1,713 initial
files plus manifest are D: verified and rechecked by the launcher. Preparation
verified the qualified references, exact dependency environment, metadata and
93-case method coverage. This is the only pending operator completion report.
No post-launch test, build, native or comparison output has been inspected;
do not monitor. The original comparison and all earlier captures are preserved.

The operator subsequently reported this run complete. The full C:/D:/source
and saved-analysis review passed; see
[the completed refinement results](DNNE_Native_Hinge_Refinement_Results_2026-11-16.md).
This run is no longer pending. All original physical/muscle refinement criteria
passed; no bounds were changed.
