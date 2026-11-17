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

The circuit review, actual package preparation and launch remain pending the
operator's completion report. The separate skull retry remains pending too.
Neither report has been inferred from a status file or process check.

This remains an isolated open-loop joint experiment. Upstream rubral numerical
accuracy and physiological calibration are open; fixed reference contact efficacy
does not transfer learned upstream strength. Sensory feedback, live recruitment,
full-body anatomy, balance, recovery and locomotion still require the continuing
DNNE/Avatar/World work. No completed component or movement result is claimed here.
