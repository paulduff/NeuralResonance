# Completed Golgi coupled comparison: timestep error remains

The operator reported `golgi-sdirk-20261109-440bb8` finished with `FAILED`.
All 30 integrations completed; the accuracy assessment deliberately returned
nonzero because five of eight protocol assessments failed their original limits.
This was not an integration crash or an interrupted solve. The frozen source is
`fb6c6e1028748d0b965d544ac9858cc0f269db25`, completed at 11:17:14 UTC.

The offline review verifies all 2,058 files (1,735,633,548 bytes) on both C: and D:,
matching manifests, 1,480 frozen package files and all recorded binaries. All
32 focused tests pass with the required class counts (7 coupled, 8 original cell,
16 channel, 1 solution manifest). Rereading all physical traces and references
reproduces the original analysis exactly. No ODE was rerun, no original file
overwritten and no bound changed. The new review is
`artifacts/golgi-sdirk-review-20261109/review.json` with a verified D: copy.

## What passed and what failed

All 30 conditions pass finite/domain, charge/calcium ledger, Kirchhoff, gate-stage,
calcium-stage, SK mass and nonlinear-residual checks. All active crossing counts
match both references at every resolution. Every refinement assessment passes,
and all six default/tighter nonlinear-tolerance comparisons pass. Both passive
protocols and the nonfiring persistent-sodium-removal protocol satisfy all gates.

The five firing protocols still fail the absolute waveform, gate, SK, calcium
and crossing-time limits at the finest step, 0.00390625 ms. Against the preselected
BDF reference:

| Protocol | Matching spike count | Maximum voltage error (mV) | Maximum timing error (ms) |
| --- | ---: | ---: | ---: |
| Quiet | 37 | 13.0214 | 0.04240 |
| Depolarizing input | 43 | 13.8691 | 0.04733 |
| Hyperpolarizing input | 32 | 5.3007 | 0.02108 |
| HCN removed | 27 | 13.7636 | 0.04306 |
| SK removed | 128 | 8.0115 | 0.02530 |
| Persistent sodium removed | 0 | 6.713e-8 | 0 |

The original voltage and timing limits remain 0.05 mV and 0.01 ms respectively.
The passive finest errors are 2.182e-7 and 2.263e-7 mV, below their 0.0002 mV
limit. The active references retain their passing original comparisons and
prospective full-state uncertainty budgets.

## Interpretation and next numerical change

Halving the timestep reduces the maximum crossing error by factors of
3.965–4.106 across the firing conditions. This is consistent with the implemented
second-order method's accumulated discretization error. It is evidence for that
mechanism, not proof that every remaining error has the same cause.

For the finest quiet trace, the first spike is about 0.0000285 ms early and the
last is 0.04240 ms early. Depolarized, hyperpolarized and HCN-removal traces also
finish early; SK removal finishes 0.02530 ms late. Small accumulated offsets can
produce large same-time voltage differences on steep action potentials. Do not
align away that phase error or replace the waveform acceptance test with counts.

Eightfold tighter Newton tolerances change the finest voltage traces by at most
2.163e-6 mV and crossing times by at most 7.058e-9 ms. Tightening those tolerances
again is not supported as the principal repair. Reference uncertainty, passive
coupling and stage convergence also passed their respective checks.

The next numerical candidate should improve the integration order and/or control
local timestep error while retaining the coupled voltage/gate/calcium equations,
massless junction constraints, independent charge/calcium accounting and explicit
input boundaries. Check event interpolation separately. Establish component
accuracy and failure isolation before repeating the unchanged full reference
comparison. Do not retune channels, impose firing, loosen bounds or promote the
current cell into a live granular-layer circuit to make this result appear passed.

The independent olive CPU/CUDA timing package may now run: its prior competing
Golgi study has finished and been reviewed. Cortical precision through receiving
neurons and persistence remains the separate integration task. Golgi physiology,
active axonal propagation, circuit function and Avatar recovery remain unqualified.
