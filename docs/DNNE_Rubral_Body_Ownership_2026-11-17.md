# Retaining the rubral circuit across body-clock attachment

The reviewed ten-input circuit produces one motor discharge at unit drive and
twelve at the stronger drive, with unchanged 50 nS contacts. Its previous driver
owned the source and motor branches separately. The next native muscle experiment
needs one circuit owner that can wait for body delivery without advancing upstream
neurons, resetting the motor cell or reconstructing its events.

The authoritative source result is `rubral-ten-review-20261117-v2/review.json`.
The existing `ObservedMotorBodyStudy`, `NativeHingeBody`, Avatar motor receiver,
causal muscle plant, dynamic muscle and native hinge bridge were inspected first.
Those mechanisms remain the intended physical path. This change qualifies the
neuronal owner needed to join them; it does not yet execute muscles or Unity.

## Isolated ownership candidate

`tools/NativeMotorBridgeProbe/RubralMotorCircuit.cs` owns a fresh actual 256-cell
DNNE red-nucleus source and the existing receptor-driven motor cell. Its explicit
projection remains n-081 through n-090 to spinal cell 2. Input identities, 20
prescribed pulses, anatomy, 0.125 ms source steps, 0.1 ms motor samples, membrane
solver, cubic crossings and per-contact delay/conductance match the reviewed run.
Original neuronal messages enter the corresponding receptor contacts unchanged.
Source observation, energy, every selected payload and admission receipt survive
in the evidence. The motor cell advances only through completed source time.

After 1,000 ms of retained quiet priming, the existing motor lane binds neural
time 1,000 ms to body time zero. The cell is neither reset nor replayed. Binding
requires the matching session/time, declared output anatomy and no earlier motor
crossings. Late or incorrect attachment is rejected before claiming the cell.
The one outstanding motor batch holds both source and receiving neuron. Retries
only deliver/acknowledge retained events. They do not integrate either cell.
Partial source/admission/integration failure faults the owner; there is no
automatic resume, discarded prefix or silent new epoch. Ownership is confined
to one thread and disposal cannot restart the circuit.

This class has no live host selection. Prescribed M1-labelled inputs remain
fixtures; upstream Izhikevich/STDP dynamics and indexed projection are not a
calibrated human rubrospinal circuit. Contact efficacy remains the fixed reference
pulse, not a transfer of learned upstream synaptic strength.

## Prospective visible comparison

`rubral_circuit_gate.py prepare` requires clean pushed source, the D:-verified
completed ten-input review and unchanged shared neuronal, protocol and receiver
sources. It copies 22 reference files and the qualified managed runtime inventory
into a fresh frozen package; preparation runs no tests or models.

`start-rubral-circuit-window.ps1` opens a separate visible PowerShell window with
no live monitoring. It builds/runs exactly 161 focused cases (the prior 155 plus
six ownership cases), checks the exact method inventory, executes ten fresh
circuits and performs a complete independent saved-data audit. Only the operator's
completion report authorizes inspection of that capture.

Four drive conditions each publish every one or seventeen source samples; the
strong-drive disconnected control runs at both cadences too. Every condition
integrates the original 1,500 ms horizon. This retains 120,000 upstream frames,
150,010 motor CSV rows including initialization, all receptor admissions and all
output batches. Original recorded trajectories are comparisons, never model
inputs. Requirements are exact original observation/energy/firing-time equality,
byte-identical motor trajectories and final electrical/crossing states, intact
quiet priming, unchanged state during blocked delivery, and complete once-only
axon accounting with the declared 2.0625 ms conduction delay. No tolerance or
physiological parameter is adjusted for a pass.

The packet intervals here count upstream frames after priming; the earlier study
grouped input frames and published on motor-sample intervals. The audit explicitly
reconstructs the new publication watermarks from both original fixed grids. A
different publication boundary must preserve every underlying sample/event.

Builds and static inventory/syntax are authoring checks only. The focused suite
and whole comparison remain prospective until the visible run is reviewed.
All old captures and saves are preserved, and each new package receives a
verified initial/final D: backup. The full brain/body/world goal remains active:
native muscle coupling, coupled numerical accuracy, sensory return, physiological
calibration, live neuronal authority and Avatar recovery remain open.
