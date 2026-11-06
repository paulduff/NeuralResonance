# Motor-unit calcium: transport and isolated numerical checks pass

The operator reported `motor-calcium-20261106-7cde4b` complete. Frozen revision
`825d17f4890fb99310c5afc48c64d8e44fac2836` finished with exit 0 at
2026-11-06 11:11:21 UTC. All 46 required cases passed: 32 motor-axon transport,
13 calcium/activation and one solution-manifest case. This provides a completed
transport verdict despite the earlier motor-only window's interrupted build.
It does not assert that the full DNNE suite is green; the separate cortical-
consolidation failure remains open.

All 21 candidate conditions completed and met the predeclared numerical criteria.
The seven protocols were silence, one arrival, a pair, and three-second trains
at 5/10/20/40 Hz, each at three integration steps. The independent implementation
read the pinned author parameter data and compared two DOP853 settings and
Radau, retaining every sampled state and force trace. No live brain, muscle plant
or old save was accessed.

`tools/review-motor-calcium.py` independently re-reads those saved arrays and
reproduces every stored state/force comparison exactly without solving a new
ODE or rerunning tests. It checks the TRX groups and source/binary manifests,
and rehashes all 1,740 files in both the capture and D: copy: 181,368,726 bytes
per copy. Its report is `artifacts/motor-calcium-review-20261106/review.json`.
The original evidence and earlier saves are unchanged.

## Numerical evidence

| Measurement | Observed maximum | Predeclared limit |
| --- | ---: | ---: |
| Reference calcium/activation contrast | 7.050e-10 | 1e-8 |
| Reference cumulative-flux contrast | 6.248e-10 | 1e-7 |
| Finest candidate state error versus tight DOP853 | 6.007e-11 | 1e-6 |
| Finest force error versus tight DOP853 | 8.727e-13 N | 1e-6 N |
| Calcium-accounting residual | 2.509e-11 | 1e-9 |

All seven refinement checks passed. These are errors against specified numerical
references, not claims of exact arithmetic or matching a living muscle to that
precision. Numerical uncertainty in the references remains distinct from the
candidate's discrepancy against the selected trace.

The silent condition had exactly zero release, activation and force. One arrival
produced a 0.244491 N peak 82.625 ms after its arrival; the pair reached 0.355815 N.
During the late-drive window, mean force rose and force ripple fell with higher
prescribed input frequency:

| Diagnostic train | Mean force | Peak-to-peak range |
| --- | ---: | ---: |
| 5 Hz | 0.165251 N | 0.211931 N |
| 10 Hz | 0.368496 N | 0.037282 N |
| 20 Hz | 0.441868 N | 0.001094 N |
| 40 Hz | 0.456278 N | 0.000029 N |

All driven conditions relaxed after input stopped; the largest final activation
was 2.743e-16. No force reset or floor was used. The force scale belongs to one
selected slow-unit parameter set, not an entire Avatar muscle. These numbers
describe the implemented reference component; no empirical Avatar twitch or
force-frequency acceptance band was tested.

## Consequence

The isolated identity-preserving event boundary and event-to-calcium/force
calculation have passed their declared checks. Keep them separate from the old
joint-angle controller until a qualified neuronal source, motor-unit anatomy and
dynamic body plant are connected. The receiver tests preserve event identity and
timing; their synthetic fixtures do not prove neuronal firing.

Endplate transmission is still assumed. Muscle action potentials, metabolic
supply, fatigue, tendon and changing length/velocity remain outside this fixed-
length component. Do not double-apply the old activation/fatigue equations.
Physiological calibration, actual neuronal motor authority, posture, recovery and
whole-body usability remain unqualified. The next source candidate and its
channel/passive checks are described in `DNNE_Motoneuron_Reference_2026-11-06.md`.
