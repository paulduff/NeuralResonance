# Optional native spike localization

The saved-endpoint cubic diagnostic `receptor-crossing-20261116-c099e3`
passes its original timing and refinement criteria. It does not execute the
native membrane or produce motor events. The native candidate therefore remains
explicitly optional until the new implementation and its output are qualified.

`MotoneuronCrossingInterpolation.CubicHermite` uses the start and accepted end
voltages and their membrane derivatives. RK4's last trial stage is not its
accepted endpoint derivative; the latter is evaluated separately, only when
an upward threshold crossing occurred. This readout does not reset voltage,
alter integration, advance receptors or predict a future endpoint.
Nonmonotone polynomials fail closed instead of selecting a root or silently
falling back to linear interpolation. The fixed residual bound is 1e-8 mV.

The default remains the original linear calculation. Its integration version,
source fingerprint field order and event identity are preserved. Cubic mode
adds `+cubic-hermite-crossings-v1` to the cell version and uses
`isolated-receptor-motor-source-cubic-v2` for its source fingerprint/event IDs.
Old saves and frozen captures are unchanged. There is no live StructureEngine,
Avatar/Unity or editor mode switch in this change.

The prospective isolated gate contains 124 cases: the prior 111 cases,
eight new crossing cases and five added receptor-source cases. It checks known
cubic roots, the linear limit, invalid/ambiguous brackets, every accepted state
of two active injected-current trajectories, and receptor/source state equality.
Receptor-driven passive cable controls check actual observed crossing delivery,
delay, original identity, retries, retained suffixes and publication cadence in
both modes. Those passive controls are not active firing physiology.
The five complete original six-second source conditions must also reproduce
their saved trajectories and event times exactly in default mode.

`native_crossing_gate.py` prepares a frozen Git archive only after commit/push,
verifying the previous reviews, D: evidence and unchanged channels/transport.
`start-native-crossing-window.ps1` builds and executes the tests and source
regressions in independent visible PowerShell, retains its window and logs,
checks exact test inventory and binary/input hashes, and verifies a D: backup.
No live monitoring is allowed. All results remain pending until the operator
reports completion and the saved evidence is reviewed.

Even a successful component gate does not repair the original failed full
receptor comparison. The 18-condition native receptor matrix, its independent
reference/refinement checks and 36 source/receiver cases still require a new
cubic-mode comparison with unchanged numerical bounds. Presynaptic neuronal
authority, physiological calibration and whole-Avatar recovery remain separate.

The gate was launched as `native-crossing-20261116-0b35ae` from revision
`492c2a491340a7b1b7b3c29ce0464a5d095e850b`, visible task
`DNNE-Native-Crossing-119c16e5b6f9`. All 1,714 initial files plus manifest were
verified on D: before launch. C# compilation and Python/PowerShell syntax checks
completed during authoring; no tests or models were executed inline. No outputs,
status or processes were inspected after launch. The operator report is pending.
