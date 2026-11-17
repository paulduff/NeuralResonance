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

The driver, complete trace recorder and independent audit/reference workflow are
authored. The Release probe build passes with zero warnings/errors; both Python
files pass syntax parsing. No new model, test, Unity player or reference solve has
run. Existing pending packages were not inspected or changed.

Do not launch this driver from the working checkout. First review the operator-
reported circuit gate and preserve its verified source/binary/runtime evidence.
The remaining work is to package the native study: freeze a clean pushed source,
copy the reviewed circuit and original motor/muscle/native references, verify
unchanged core implementations and numerical dependencies, define the combined
exact focused-test inventory, build a separate player, and launch the full
pipeline in one visible independent PowerShell window with initial/final D:
verification and no live monitoring. The current driver entry point checks the
gate's complete analysis, but it does not replace that provenance/packaging work.

This remains an isolated open-loop joint experiment. Upstream rubral numerical
accuracy and physiological calibration are open; fixed reference contact efficacy
does not transfer learned upstream strength. Sensory feedback, live recruitment,
full-body anatomy, balance, recovery and locomotion still require the continuing
DNNE/Avatar/World work. No completed component or movement result is claimed here.
