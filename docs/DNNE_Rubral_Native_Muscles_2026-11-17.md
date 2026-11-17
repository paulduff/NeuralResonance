# Actual rubral events through Avatar muscle and native joint mechanics

The ten-input circuit has a reviewed motor response, but its connection to a
moving body remains unqualified. The circuit/body-clock gate `7fc785` is still
awaiting the operator. This document describes independent authoring done while
that gate and the separate native skull retry `1e2f21` remain unmonitored.

## Preserved physical and neuronal mechanisms

The existing `ObservedMotorBodyStudy`, `NativeHingeBody`, `AvatarCausalMotorPlant`,
`AvatarDynamicMuscle`, `AvatarMotorUnitCalcium`, receiver and Unity hinge/wire
implementations were inspected before this work. The new diagnostic driver uses
those same physical mechanisms and parameters. It introduces no rate-to-spike
converter, current injection into the motor cell, joint controller or direct
angle prescription. No live host, Avatar feature, editor selection or saved brain
is changed.

`RubralNativeBodyStudy` owns a fresh `RubralMotorCircuit`. Actual original events
from its ten declared DNNE sources enter the motor-cell receptor contacts; that
cell's observed crossings enter the existing axon receiver. Received discharges
drive the existing calcium and dynamic muscle equations. Tendon forces available
at the start of a physical step act through the native attachment points. The
completed Unity geometry then becomes the next muscle/body endpoint.

The opposing muscle has a separately integrated quiet reference motor neuron.
Its complete trace must reproduce the original quiet reference prefix. Empty
transport packets therefore represent a checked quiet source; a missing source
is never interpreted as quiet. Both source watermarks are required before the
body advances. Waiting/retries retain the original neurons and events.

Both motor cells retain their complete 1,000 ms quiet initialization before
binding the explicitly declared neural-1,000/body-zero clock. The native world
starts paused. The diagnostic owner resumes it after initial capture, advances
only permitted physical steps, pauses at the final endpoint and stops its owned
player. An uncertain native command is never resent.

The neuronal run retains its original 1,500 ms horizon. The unchanged native
binary32 time step cannot divide 500 ms exactly. At physics resolution one,
7,999 full steps finish just below 500 ms; the next would exceed the proven
neuronal horizon. The driver stops at that declared full-step endpoint instead
of fabricating an extra interval of silence. This is seven times 7,999 physical
steps, or 55,993, in the proposed experiment.

## Prospective conditions and evidence

| Condition | Original source drive | Active attachment | Source packet interval | Special control |
| --- | ---: | --- | ---: | --- |
| Quiet | 0 | Left | 17 frames | Native completed-reply read lock |
| Unit-left | 1 | Left | 17 frames | Weaker original neuronal response |
| Strong-left | 4 | Left | 17 frames | Main causal condition |
| Strong-right | 4 | Right | 17 frames | Declared opposite muscle attachment |
| Strong-cut | 4 | Left | 17 frames | Receptor inputs disconnected |
| Strong-fast | 4 | Left | 1 frame | Packet cadence |
| Strong-retry | 4 | Left | 17 frames | Source backpressure and exact retries |

The attachment is declared before each fresh trial. No recorded neuronal event
is rewritten or redirected after firing. The quiet/disconnected angular bound
remains 1e-5 radians, the required stronger opposite responses exceed +/-1e-7
radians, and their mirror discrepancy must remain within 1e-5 radians. The
unit-input outcome is measured without adding a new minimum movement threshold.

`RubralCircuitTrace` retains all original upstream frames and receptor admissions
and every motor sample, including initialization. They must reproduce the
reviewed ten-input source observations/energy/firing times and complete motor
CSV bytes. Old records are comparisons, not the driving input. Native commands,
replies, body states, muscle states and all delivered events are retained too.

`analyse-rubral-native-body.py` independently checks original source/target/
contact identity, full input and output accounting, exact clock watermarks,
packet/retry invariance, actual force/impulse steps, and held-body commands.
For every physical interval it checks the commanded tensions against the
previous recorded muscle endpoint. CaptureGeometry's later read-only inspection
has a different sequence from the physical reply; their states and paths must
agree, rather than mistaking different command metadata for a changed body.

The existing continuous hinge/muscle reference is reused for five distinct
conditions at all three already declared numerical settings (15 references).
Fast/retry cases must reproduce the strong-left trajectory exactly. Original
reference/candidate/domain/ledger bounds remain unchanged. The comparison helper
accepts an explicit `allowed_steps=(7999,)` only for this experiment; its default
512/1,024/2,048-step acceptance is unchanged for existing callers. No reference
has been solved for the new conditions yet. Agreement here would concern sampled
motion at one resolution, not a new step-convergence demonstration.

## Current state and remaining launch requirements

The driver, trace recorder, independent audit/reference workflow and frozen
PowerShell pipeline are authored. The Release probe build passes with zero
warnings/errors. Python/PowerShell syntax and the static combined inventory pass:
281 cases in 21 distinct classes, including the 161 circuit cases and existing
muscle/path/plant/calcium/clock/read regressions. Source inspection confirms all
20 reused body/reference files match the completed native-body capture. The only
decoder difference is the explicit additional step-count argument described above.
No new model, test, Unity player or reference solve has run. Existing pending
packages were not inspected or changed.

Do not launch this driver from the working checkout. The explicit sequence is:

1. After the operator reports circuit completion, use
   `review-rubral-circuit.py CAPTURE NEW_REVIEW --operator-reported-complete`.
   It verifies complete C:/D: files, frozen Git, binaries, exact tests, the full
   saved circuit audit and preserved runtime. It runs no neurons or tests and
   refuses to overwrite a review. A failed capture needs diagnosis instead.
2. Back up and verify that new review with `backup-unity-run.ps1`.
3. Run `rubral_native_gate.py prepare NEW_REVIEW` using the pinned numerical
   environment (`artifacts/numerical-reference-env-20261102/Scripts/python.exe`,
   with `-B`). Preparation requires clean pushed source and the verified completed
   review. It checks shared neuronal/core, existing native/muscle/reference source
   preservation, the exact documented decoder extension and pinned dependency
   hashes before copying the original references into a fresh source archive.
   This is metadata preparation, not test/model execution.
4. Back up and verify the prepared package, then invoke its frozen
   `start-rubral-native-window.ps1 -PackageDirectory PACKAGE`.

The launcher verifies both initial copies before opening an independent visible
PowerShell window. Inside that window it checks metadata/dependencies, builds the
test and probe assemblies, verifies the managed runtime before model execution,
runs the exact 281-case suite, builds a separate Unity player, executes seven
native conditions and all fifteen references, and audits every endpoint. It
preserves compiled source/player/binary inventories, rechecks runtime and
numerical dependencies even on failure, and performs the final verified D:
backup. The window remains available for the operator; no live monitoring is
authorized. Failed evidence is never retried in place.

The operator has now reported circuit `7fc785` complete. Its independent review
and D: backup passed: 2,451 files per copy, 161 exact test cases, all 120,000 source
frames/150,010 motor CSV rows and preserved runtime/source activity. See
`DNNE_Rubral_Body_Ownership_2026-11-17.md`. This satisfies the prerequisite for
package preparation/launch. Native skull retry `1e2f21` still awaits the operator;
its outputs/status/process have not been inspected.

This remains an isolated open-loop joint experiment. Upstream rubral numerical
accuracy and physiological calibration are open; fixed reference contact efficacy
does not transfer learned upstream strength. Sensory feedback, live recruitment,
full-body anatomy, balance, recovery and locomotion still require the continuing
DNNE/Avatar/World work. No completed component or movement result is claimed here.

## Explicit numerical-runtime transition before launch

Preparation at `c192c8c` stopped before package creation or any model/test because
the historical numerical-runtime inventory no longer matched this machine.
`rubral-native-preparation-20261117` preserves both complete inventories, their
comparison and the failure record (five files plus manifest, D: verified).
The exact changes are `python.exe`, `python3.dll` and `python312.dll` in the primary
runtime directory. Python's reported version remains 3.12.14; all 2,433 other
recorded dependency files, including NumPy 2.2.6 and SciPy 1.16.3, match by hash.
This inspection does not establish numerical equivalence of the interpreters.

Preparation now optionally accepts `--environment-review` naming that D:-verified
declaration. With no declaration it still requires exact historical equality.
With one, the historical source inventory, current full inventory and each of
the three old/new hashes must match the recorded declaration; changed versions,
numerical libraries, paths or extra/missing dependencies are rejected. The old
`reference/qualified-environment.json` stays unchanged. A separate declaration
and current preparation inventory are frozen into the new package; all later
checks require that exact current environment throughout the run.

`rubral_reference_environment.py` provides ten prospective validation controls,
including rejection of undeclared/partial runtime changes, changed numerical
libraries, duplicates and altered paths. The visible PowerShell window runs
those and the unchanged independent hinge controls (49 geometry cases, six
mechanical integrations, three cross-method comparisons) before the .NET/native
experiment. The fifteen full references still run at their three original
settings with unchanged bounds. Both solving and final analysis require the
saved control report. Numerical compatibility with the historical interpreter
is not assumed or reported as proved. Any future numerical pass applies to the
tested comparison under the newly declared runtime. No controls or numerical
models run inline.

## Visible launch record

Prepared from clean pushed revision
`827f04554fb61191ae2cf489b3aa54618f76c4c0`, using the D:-verified circuit review
and explicit runtime declaration. All 1,819 initial files plus manifest were
D: verified before opening independent visible task
`DNNE-Rubral-Native-149a80212e4b` for
`rubral-native-20261117-4cdba5`. No post-launch output/status/process has been
inspected. The numerical controls, 281 tests, seven native conditions and fifteen
full references are pending the operator's completion report; no success is
inferred from launch. Native skull retry `1e2f21` also remains pending.

## Reported completion: comparison loader failure

The operator reported the final FAILED footer and verified D: backup for
`rubral-native-20261117-4cdba5` (360,428 files plus manifest). Its saved status
records all 281 focused cases passed, a successful Unity build and completed
native driver. The saved native summary contains all seven cases and 55,993
physical steps. Quiet and cut cases end at zero angle; unit-left ends at
0.00020332417625468224 rad, strong-left at 0.0011702291667461395 rad, and
strong-right at the equal opposite angle. Fast/retry summaries match strong-left.
These are recorded candidate results awaiting the complete independent audit,
not a numerical or physiological qualification.

The reference failed before its first solve. `native_audit` delegated loading to
the preserved `analyse-observed-native-body.py` loader. That older experiment
requires exact equality with its historical Python binaries. The new rubral
preflight correctly accepts only the explicitly declared three-binary transition,
but the downstream historical loader does not know that policy. Both checks
behaved as coded; reusing the old loader in the new study was the integration bug.
The captured current/previous inventories differ only in the three already
declared Python binaries. All ten transition controls and the 49 geometry, six
mechanical and three cross-method controls have saved passing reports.

The repair uses a rubral-specific `load_reference` that runs the existing full
rubral preflight and saved-control verification before loading the unchanged
comparison/mechanics modules and checking the original hinge review. The older
loader remains unchanged for its callers. No historical interpreter equivalence
is asserted and no numerical acceptance bound changes.

`rubral_native_continuation.py` prepares a fresh comparison package from clean
pushed source, copying the original references/controls and exact current runtime
inventory. It binds the original capture by absolute path, Git revision and backup
manifest SHA. It never writes into that capture or repeats its neural/native run.
The independent visible `start-rubral-comparison-window.ps1` first verifies every
original file against both C:/D: copies, all original Git files, binaries and exact
281-test inventory. Only then does it reproduce the old loader failure, exercise
the repaired production loader with the actual package, and rerun ten transition
fixtures. These checks execute in the window, not during authoring.

The continuation then audits the complete saved neuronal/native records, solves
the missing fifteen references and compares every endpoint using unchanged
equations/settings/bounds. The full original file audit runs again afterward;
new outputs receive their own verified D: backup. Both old and new status remain
visible and separate. A failed comparison cannot be converted into a pass by
changing the old capture. The complete read audit and new numerical work remain
pending until the operator reports this continuation finished.

The continuation is now launched as `rubral-comparison-20261117-a6b77a`, frozen
from pushed revision `509c3b8400a54ec43a51f535990d916b2a7aa47f`. All 1,846 initial
files plus manifest were D: verified before opening visible task
`DNNE-Rubral-Comparison-fccfb9b34200`. Preparation checked saved metadata and source
preservation only. Full input verification, loader controls and numerical work
belong to that window; no post-launch output/status/process has been inspected.
