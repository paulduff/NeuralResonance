# Motor/body partition diagnosis

The completed `f5e01c` comparison misses only muscle yield accuracy under strong
drive. Its maximum `1.039360238175302e-5` error exceeds the unchanged `1e-5`
bound, peaking near 264.94 ms in the unstimulated opposite muscle. See
`DNNE_Coupled_Outcomes_2026-11-18.md`. This motivates separating muscle solver
error from the held-force/held-length body partition; neither cause is assumed.

## Declared experiment

Five **strong-left** epochs use the original neuronal circuit, receptor contact,
motor events, calcium/muscle equations, native geometry, initial state and
physical duration. Every epoch evolves fresh neurons; saved events are comparison
evidence and never drive the body. Native bodies begin paused. No live brain,
Avatar selection or movement controller is involved.

Let `h` be the original exact native step, `StepSecondsAt(1)*1000` ms (about
0.0625 ms). The original duration is `7999*h`, slightly below the 500 ms body
coverage of the preserved neuronal epoch.

| Condition | Body step | Muscle substeps/body step | Muscle step | Body steps |
| --- | --- | --- | --- | --- |
| repeat | h | 2 | h/2 | 7999 |
| muscle-fine | h | 4 | h/4 | 7999 |
| body-fine | h/2 | 2 | h/4 | 15998 |
| body-finer | h/4 | 1 | h/4 | 31996 |
| body-finer-muscle-fine | h/4 | 2 | h/8 | 31996 |

The middle three vary body exchange frequency while holding the internal muscle
step fixed. The first pair and last pair separately refine the muscle step at a
fixed body step. The exact powers of two retain common physical endpoints.
Native wire/driver clock support extends through resolution 3; original default
resolutions and the full original seven-condition entry point remain available.

Total: five neuronal epochs and 95,988 physical steps. This is a targeted cause
diagnosis, not a new qualification of right drive, quiet, cut or retry controls
at finer resolutions. Those remain supported only by their earlier captures.
The future full matrix is still required if a new physical resolution is chosen
for wider use. Full brain feedback, physiological calibration and recovery remain
unqualified regardless of this result.

## Comparison and evidence

The visible workflow first runs 287 focused cases: the prior 281-case inventory,
with six additional exact-clock/invalid-decimal/nonmoving-read cases for the new
resolutions (18 clock cases total). The unsupported upper-resolution test moves
from 2 to 4 to reflect the explicit range change. No earlier accuracy bound changes.

Before new native execution, the generalized saved-record auditor must exactly
reproduce all original seven-case source/body accounting and the original
comparison input from `f5e01c`. This is read-only arithmetic on completed records,
not a model rerun. The native player is then built separately from frozen Unity
source. Every new request, reply, physical endpoint, muscle state, source frame,
motor event, receipt and wire preflight is retained and checked. Original neuronal
prefixes must remain identical, apart from the declared session identity.

Three already-completed strong-left references (DOP853, tighter DOP853 and Radau)
are copied with their dependencies and original bounds; no reference ODE is solved
again. Their common grid has 8,000 endpoints. All new native endpoints undergo
source/transport/mechanics/ledger checks, while independent trajectory accuracy
is claimed **only on that original common grid**. Intermediate fine endpoints
are not silently described as independently qualified.

Prospective criteria in `rubral_refinement_protocol.py` retain all original
absolute accuracy limits. Both finest variants must pass them. For each measured
metric, each body halving must leave at most 0.8 of the previous normalized error
plus a 0.02 floor. Repeated original-step error magnitudes may differ by at most
0.01 of the original bounds; this is an error-magnitude check, not a claim of
bitwise old/new trajectories. Muscle-step contrasts report their differences
against 0.05 of the original bound as a separate diagnostic, not a forced cause.
The final numerical pass requires finest accuracy, repeat and body refinement.

## Execution and storage

Authoring builds pass with zero warnings/errors; Python/PowerShell syntax and
the static 287-case inventory pass. All tests and native/reference comparisons
execute only in an independent visible PowerShell window, without live monitoring.
The runtime, unchanged neuronal/muscle source, copied reference evidence, built
Unity source/player and final C:/D: contents are checked and retained.

C: had approximately 13.7 GB free before authoring. The full 35-case matrix would
create excessive new recordings there, so this first causal diagnosis uses the
five targeted epochs above. Unity's disposable build workspace is placed on D:;
frozen source and recordings remain on C:, with a separate verified D: copy.
Preparation/start require 8 GiB free on C: and 20 GiB on D:. The native driver
stops before beginning another epoch if less than 2 GiB remains, preserving all
completed conditions. No old evidence, saves or working directories are deleted.

Motor partition diagnosis launched as `rubral-refinement-20261118-3d8835`, frozen
at pushed revision `778c7f2fddcd385922fc62e95719bb0e3754f88d`. All 1,947 initial
files plus manifest verified on D: before visible task
`DNNE-Rubral-Refinement-561e9ba124b9` was started. Build scratch is the fresh
`D:/DNNE-build-work/rubral-refinement-20261118-3d8835` directory. No post-launch
outputs, status or processes have been inspected; await operator completion.

There is ONE pending window: this five-epoch strong-left motor diagnosis.
Sensory `884934` is fully reviewed/passing, and old motor `f5e01c` is reviewed
with its original yield failure retained. No separate sensory or stationary
window is pending. Full goal remains active; physiological resting state,
afferent return, complete feedback, balance and recovery are still open.


## Completed outcome

The separate corrected epoch comparison `e9bed6` passes all prospective
strong-left numerical criteria. Its reviewed analysis reproduces exactly.
Body-step refinement reduces the limiting yield error roughly twofold per
halving; muscle-only refinement has negligible effect. The finest yield error
is 0.26142 of the unchanged bound. See
`DNNE_Motor_Epoch_Comparison_2026-11-18.md` for the complete evidence and limits.
The original failed capture remains preserved, and live defaults are unchanged.
