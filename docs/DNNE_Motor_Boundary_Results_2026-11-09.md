# Motor-cell scheduling comparison results

The reported `motor-boundary-20261109-a79744` window passes all ten original
scenario gates with endpoint-difference scheduling. The original scheduler still
fails the same two scenarios. All thirty original control traces reproduce their
earlier CSV bytes and case results exactly. No cell equation, input, physiological
parameter or acceptance bound changed.

## Verified capture

The frozen source is `2465fdb8026768f2348e7bda53a4375ff203eb28`.
`artifacts/motor-boundary-review-20261109/review.json` verifies all 2,057 files
and 2,440,327,269 bytes in each C:/D: capture, including package sources and
executed binaries. Its manifest SHA256 is
`CF76F026FDEB0DCD896682F5BDB28D7946E6BE22BBCEBB04BB16F771F67C0557`.
The review also has its own verified D: backup.

All 12 focused motor-cell/manifest tests pass. Thirty original controls and thirty
endpoint-scheduled candidates complete. Independent saved-state analysis exactly
reproduces the window's report, all retained reference metrics/contrasts and the
earlier failed control analysis. The review integrates no model or reference ODE.
Each candidate uses the unchanged three resolutions and six-second physical
inputs. The endpoint scheduler verifies exact represented duration throughout
the cell integration; final duration totals and call counts match the earlier
clock diagnostic.

## What changed

All eight previously passing scenarios remain passing. `soma-1000` and
`without-fast-sodium`, which failed the tiny-calcium refinement condition under
the original schedule, now pass it too. Finest calcium discrepancies in these
two cases change from 8.8227e-18 to 5.6921e-18 mM, and from 5.8564e-15 to
1.7347e-18 mM, respectively. Their improvement resolves the original criteria;
it should not be described as a large physiological calcium change.

Across the ten scenarios, the maximum finest-step discrepancies are:

| Quantity | Maximum observed | Original bound |
|---|---:|---:|
| Voltage | 0.0111071 mV | 0.05 mV |
| Gate probability | 0.000163851 | 0.0005 |
| Calcium | 2.17916e-8 mM | 5e-7 mM |
| Matched crossing time | 0.000108858 ms | 0.01 ms |

Crossing counts agree exactly. All scenario refinement gates and required
physical/charge/calcium accounting checks pass. The strong-input references
exercise action potentials and increased calcium; success is not based solely
on quiet cells. A scenario gate requires finest absolute accuracy, successive
refinement and accounting across the resolutions; this does not mean every
coarser trace passes the finest absolute limits.

The controlled reproduction supports a causal conclusion for this harness:
changing supplied step durations to represented endpoint differences resolves
the remaining numerical failures. It is not a universal proof about every
future input, network, solver or runtime scheduler. The old failed captures and
original scheduler remain preserved.

## Next acceptance gate

The numerically checked source can now enter the already prepared isolated
somatic-source-to-muscle comparison described in
`DNNE_Somatic_Motor_Unit_Circuit_2026-11-09.md`. That experiment must preserve
these exact source traces/crossings while delivering their events through
Avatar's existing transport, calcium and isometric force components. Packet
cadence, retries, disconnection and muscle refinement have separate prospective
checks. No firing-rate generator or joint-target controller substitutes for the
source membrane.

This result does not establish physiological calibration, active axonal
propagation, a neuromuscular junction, live spinal authority, whole-brain balance
or Avatar recovery. Existing live DNNE/Avatar/Unity/editor behavior and old saves
are unchanged. The separate CUDA host-workspace accuracy window remains pending
its own operator report and is not monitored.
