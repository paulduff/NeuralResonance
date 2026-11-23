# Fresh motor-cell clock origin — 23 November 2026

## Source finding

The actual head/sensory study initializes its nerve source at neural time
1010 ms: the declared neural epoch is 1000 ms and the mechanical prelude ends
at physical time 10 ms. `CanalConvergenceConnection`, `CanalNucleusProjection`,
`VestibularProjectionReceiver` and `ReticulospinalCircuit` carry an explicit
source epoch. `DescendingMotorPool` previously rejected every nonzero input
epoch and computed completeness/sample endpoints from zero. Its detailed
`ReceptorMotorSource` and `MotoneuronReferenceCell` also always began at zero.
This prevented direct attachment of these existing isolated owners without
additional initialization or an incorrect timestamp rewrite.

This source-level finding is independent of the pending full-body mechanics
study `avatar-tendons-20261123-bb320d`; none of that package's source, captures,
configuration or status is changed by this repair.

## Fresh-state candidate

The cell constructor now accepts an optional `initialNeuralTimeMs` (default
zero). It initializes the same physical state at that declared absolute time.
There is no time setter, reset, restore or retiming operation for an existing
cell. Existing defaults retain their integration version and configuration
fingerprint construction. A nonzero fresh origin has a distinct integration
suffix and source configuration identity; it cannot inherit old numerical
qualification automatically.

`ReceptorDrivenMotoneuron` gives every existing receptor contact the same origin,
using its already-supported initial-time parameter. Completeness initially
remains unknown: no source batch and no invented quiet history are supplied.
The first batch cannot contain pre-origin firings, even when their axonal arrival
would lie after initialization. Source times, receptor delays and original event
identities remain absolute and unchanged.

`ReceptorMotorSource` samples at origin plus integer sample count times period.
`DescendingMotorPool` requires a common finite representable epoch for all
declared inputs and its freshly created cells. Completeness, availability and
sampling use that origin. The existing motor-lane attachment still requires the
cell's actual present time, retained quiet history and no past motor emissions;
there is no workaround that drops real preceding spikes to permit attachment.

No membrane current, channel gate, receptor kinetics, integration formula,
crossing threshold, numerical acceptance bound or learning rule is changed.
Absolute-time floating-point representability still matters, so the new origin
needs explicit tests and later coupled numerical qualification.

## Authored controls; not yet executed

`FreshMotorEpochTests` contains 13 cases:

- Fresh 10, 1010 and 1024 ms starts have the same initial physical state, no
  prior samples/events, unknown source completeness and no implicit advancement.
- Negative, nonfinite and unrepresentable origins reject.
- Explicit zero retains default state/configuration; distinct origins have
  distinct source fingerprints.
- Pre-origin events reject even if delayed arrival would be later; an event at
  the declared origin is admitted and reaches its original delayed boundary.
- At exactly representable dyadic sample/integration intervals, a 1024 ms
  translation is compared to zero for active physical states, receptor state,
  integration count and emissions. Crossing-time allowance is two ULPs at the
  declared endpoint; this is a clock-translation control, not a relaxed old
  numerical bound or independent reference solution.
- Actual motor transport retains the declared source epoch and body mapping.
- Original nonzero projection packets reach the detailed pool and body receipt
  without changed source timestamps or fake prehistory.
- Mixed input epochs cannot initialize a common motor pool.

The full test project builds with zero warnings/errors at
`D:/DNNE-build-work/motor-epoch-authoring-20261123`, with log
`D:/DNNE-build-work/motor-epoch-authoring-20261123-build.log`. No tests, models or
reference comparisons ran in that authoring build. The next visible workflow
must run all new cases and the existing receptor, membrane, crossing, motor
source, descending projection and transport regressions with an exact inventory,
frozen source and verified D: backups. No clock-test window is launched yet.

## Remaining integration work

A fresh origin is an explicit initial condition, not a biological claim about
how the cell reached that state. Existing captures, learned brain saves and
past emission histories remain untouched. The integrated run must declare one
physical/neural mapping for sensory and motor paths, including the real body
steps needed for its initial mechanical samples. In particular, mapping neural
1010 ms to physical zero would disagree with the current sensory mapping to
physical 10 ms. Audit the body attachment baseline and actual completed-step
contract before joining the whole loop; do not conceal this with a clock reset.

The current native tendon study tests prescribed mechanical forces and synthetic
attachment fixtures. The fresh motor epoch candidate neither supplies real
attachment anatomy nor completes its host wire bridge. Active sensory accuracy,
original neuronal motor causality, anatomical calibration, physiological firing
and Avatar standing/recovery remain open.

## Operator-requested stall inspection

Paul explicitly asked to check `bb320d`. A bounded process/log inspection found
PowerShell PID 18336 still alive after frozen preflight, with no test/Unity child
started. Its read counter advanced from 407,843,146 to 471,156,285 bytes during
the check. The runner was hashing the .NET shared directory: 3,099 files totaling
985,574,876 bytes. That supports ongoing slow I/O, not a terminated process;
it does not identify the cause of the slowness. No restart, cancellation or
frozen-package edit occurred. The working-tree runner now reports its file count
and progress every 100 files for future packages. Normal no-monitoring behavior
resumes; the operator's final footer is still required for result review.

## Subsequent completion and next workflow

Paul subsequently reported `bb320d` complete. The full mechanical review and
its verified D: backup are recorded in `DNNE_Full_Avatar_Tendons_2026-11-23.md`;
the earlier pending/stall statements above are historical. No report remains
pending before the next launch.

`tools/fresh_motor_epoch_study.py` and
`tools/start-fresh-motor-epoch-window.ps1` prepare a clean pushed Git archive
and run the following exact inventory in a visible, independent PowerShell:

| Class | Cases |
| --- | ---: |
| FreshMotorEpochTests | 13 |
| MotoneuronReferenceTests | 11 |
| MotoneuronCrossingTests | 8 |
| MotorReceptorContactTests | 14 |
| ReceptorDrivenMotoneuronTests | 15 |
| ReceptorMotorSourceTests | 19 |
| DescendingMotorPoolTests | 18 |
| DeclaredDescendingProjectionTests | 8 |
| MotorAxonTransportTests | 32 |
| MotorAxonReplayBufferTests | 16 |
| SolutionManifestTests | 1 |
| **Total** | **155** |

Static source inventory contains 110 methods with those 155 exact multiplicities.
The runner records separate test/probe binary manifests, matching framework
declarations and selected managed runtime hashes before/after. Hash progress is
printed; unused installed runtime versions are not scanned. The helper's new
messages change no runtime manifest content or neuronal equation. Original
source files and Python dependencies are checked before/after. Tests write new
isolated state and any declared connection evidence into this package. Unity,
live services and previous saved brains are not selected.

A passed inventory would qualify these component clock controls and existing
regressions, not a full independent coupled trajectory, physiological firing or
standing. The next body connection still needs the real 10 ms physical prelude
and all intervening motor history; no source timestamp rewrite is permitted.
At authoring, syntax/inventory checks passed without executing tests or models.
The updated dependency helper also builds with zero warnings/errors at
`D:/DNNE-build-work/motor-epoch-runtime-authoring-20261123`; its build log is
`D:/DNNE-build-work/motor-epoch-runtime-authoring-20261123-build.log`. The built
probe/test runtime configurations both declare .NETCore and ASP.NETCore 10.0.0.
Record the frozen revision, folder, verified initial backup and actual visible
task after launch; do not monitor the run afterward.

## Launch record

Frozen package: `D:/DNNE-build-work/motor-epoch-20261123-6889f0` at pushed
`07294c30fce30ea0ef0a2b465aa91964a3519f88`. Frozen preflight and both initial
2,196-file copies plus manifest verify. The independent visible task
`DNNE-Motor-Epoch-14ea39bd5935` was started on 23 November. No post-launch
output, status or process has been inspected. This is the sole pending report;
await Paul's final backup/footer. Launch does not establish a passing result.

## Completed result

Paul reported `6889f0` complete on 23 November. All **155 cases passed**:
13 fresh-clock controls and 142 existing regressions. The declared active
dyadic-time translation, pre-origin rejection, original timestamp preservation,
nonzero descending pool admission and actual motor-axon clock mapping are covered
by those exact controls. This remains prescribed source input, not qualification
of an intact brain or the complete physical prelude.

`tools/review-fresh-motor-epoch-result.py` verified both 2,785-file copies plus
manifest, full frozen Git archive/extraction, executed test/probe bytes, exact
TRX multiplicities and identical managed-runtime manifests (335 files in two
selected directories). It did not execute any model/test or rehash the external
runtime installation. Review four files plus manifest are verified at
`D:/DNNE-desktop-runs/UnityWorld/motor-epoch-20261123-6889f0-review`.

Independent coupled numerical accuracy, physiological firing, the full physical
attachment baseline and standing remain unqualified. The next native study
investigates tiny tendon-driven joint responses with direct native force/torque
observation; it does not change these neuronal equations or increase brainstem
complexity to mask a mechanical issue.
