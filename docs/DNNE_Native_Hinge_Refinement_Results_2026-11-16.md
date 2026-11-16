# Native hinge refinement: completed review

The operator reported `native-hinge-refinement-20261116-421387` complete.
All 93 focused tests, 20 physical cases / 26,624 native steps and 40 native
rejection controls pass. All eight physical-step contrasts, eight muscle-step
contrasts and four baseline repetitions pass their unchanged prospective
criteria. This qualifies sampled convergence of the isolated native hinge and
its muscle partition over the declared 128.00000607967377 ms window.

The read-only review is
`artifacts/native-hinge-refinement-review-20261116/review.json`, produced by
`tools/review-native-hinge-refinement-20261116.py`. It verifies all 163,006 files
on C: and D: (878,993,362 bytes per copy), 1,712 protected package files, 1,629 Git
files, the test/probe/player binaries and all 36 compiled Unity source files.
Frozen revision is `e59d9ea89adc049376b71bd933fa40986cb3d196`; the capture manifest
SHA256 is `82A3C63DBBE30FEFC46C2F248964BAB5A8FCBE649DE3ED51C94E48E588EADD5F`.
All five command/receipt audits and the complete saved-array refinement report
reproduce exactly. No test, neuronal integration, muscle integration or native
physics ran during review. The derived review has its own verified D: backup.

## Findings

The original baseline's observed state columns reproduce the earlier capture
bit for bit in quiet, left, right and balanced cases. All five native codec
preflights and deliberate completed-reply read locks pass. Each lock recovers
the same completed reply without repeating a physical command.

The physical comparisons use 513 identical recorded endpoints across all three
step sizes. Representative left-drive errors against the tight DOP853 reference
are below; these are maximum errors on that common grid, not just final values.

| Quantity | Nominal 0.25 ms step | Nominal 0.125 ms step | Nominal 0.0625 ms step |
| --- | ---: | ---: | ---: |
| Joint angle (rad) | 7.11532e-8 | 3.57629e-8 | 1.78640e-8 |
| Joint angular speed (rad/s) | 9.38856e-6 | 4.69730e-6 | 2.34780e-6 |
| Muscle force (N) | 1.92486e-4 | 9.55659e-5 | 4.81550e-5 |
| Fiber length (m) | 6.58914e-8 | 3.31010e-8 | 1.66688e-8 |

These errors approximately halve when the physical step is halved, consistent
with the first-order start-held partition. No compared metric increases during
either physical refinement in this capture. The prospective acceptance rule
still includes its declared numerical floor; passing it alone is not proof of
an asymptotic order for every quantity.

Both additional muscle halvings pass at every native endpoint. Their differences
stay below five percent of each original absolute bound. Baseline and both finer
physical/muscle variants pass the original absolute bounds against all three
reference methods. Original source events, geometry, release waveforms, domains
and calcium/momentum/energy accounting pass the saved-data checks. The 12 checked
reference traces were reused; no new independent solve was needed.

## Decision and remaining scope

Use the finer physical step as the starting candidate for the forthcoming
neuronal-source/native-body connection. Its actual step is the exact promoted
Unity binary32 value, not rounded decimal 0.0625 ms. The source clock must remain
independent of that physical clock and of packet cadence. The new source adapter
first faces the full trace/identity gate described in
[the observed motor-source plan](DNNE_Observed_Motor_Source_2026-11-16.md).

This result applies to the declared synthetic opposing-muscle pair and window.
The finer native half steps have domain/geometry/ledger checks, but independent
reference accuracy at those extra endpoints was not assessed. Continuous
all-time accuracy and `fullCoupledTrajectoryQualified` therefore remain false.
No running neuronal source, active axon, human anatomy, sensory feedback,
whole-brain authority, balance, fall recovery or walking was assessed here.
No live brain, Avatar, World, editor or old save was selected or changed.
