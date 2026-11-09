# Reported CUDA, motor-cell and Golgi outcomes

The operator reported the CUDA retry and confirmed both older cell studies had
finished. `review-compute-and-cells-20261109.py` reads their saved evidence without
executing a solver or GPU. Reports are in `artifacts/compute-cell-review-20261109`.
Original captures retain their original statuses and bytes.

## CUDA interoperability passes its narrow checks

`cuda-interop-20261109-589561`, source
`f52c4f8663354ed82169efd32b7794dec18a4dc2`, completed at 08:49:49 UTC on 9 November.
Both complete C:/D: copies contain 1,411 files and 468,307,687 bytes, plus identical
manifests; all hashes, packaged source and binaries verify.

The explicit helper preload succeeded. Loaded compiler/helper paths match the
verified package. All six persistent-buffer cases, seven launches per buffer,
exact float64 results and guard regions pass. The review independently recreates
and compares every saved transfer/result byte. The executable reports successful
context destruction and restoration, verified by its native calls before success.

This qualifies the isolated compiler/driver/transfer/arithmetic route on this
RTX 5060. It does not execute a neuronal model, measure speedup or qualify solver
accuracy, physiology or Avatar recovery. The earlier helper-load failure remains
preserved. Next run the prepared CPU membrane-layout comparisons, then develop
and compare the actual CUDA membrane calculation at explicit accuracy bounds.

## Motor-cell numerical repair: substantial improvement, original gate still fails

`motoneuron-numerical-20261106-640f85`, source
`22d295951135b2b04992884ddfd309607cc40f6c`, completed at 14:19:34 UTC on 6 November.
Both complete C:/D: copies contain 1,817 files and 1,400,815,272 bytes plus their
identical manifests. All 12 focused tests passed. The original FAILED status is
preserved, and physical inputs and original acceptance bounds match the prior run.

The review recomputes all 30 reference metrics, twenty reference contrasts and
30 candidate comparisons from saved NPZ/CSV state arrays, then independently
re-evaluates the declared gates. All three reference methods pass their physical
and accounting checks in every case, and all twenty reference contrasts pass.
All 30 compensated candidates pass physical/accounting checks. Twenty-seven pass
their absolute error checks; the three failures are coarse-step dendritic,
inhibited-dendritic and no-KCa cases. Every finest-step candidate passes the
absolute accuracy gate, as required by the original protocol.

Eight of ten scenarios pass the complete original gate. The remaining three
refinement comparisons fail only on calcium:

| Scenario | Step refinement, ms | Coarse error, mM | Fine error, mM | Original permitted fine error, mM |
| --- | --- | ---: | ---: | ---: |
| Soma 1,000 pA | .00625 to .003125 | 5.6921e-18 | 8.8227e-18 | 6.6678e-18 |
| No transient sodium | .0125 to .00625 | 1.2816e-15 | 4.7373e-15 | 2.0878e-15 |
| No transient sodium | .00625 to .003125 | 4.7373e-15 | 5.8564e-15 | 5.2110e-15 |

These are far below the original absolute calcium bound of 5e-7 mM, but the
declared refinement rule still fails. Do not relabel this capture as passed or
relax its limits after seeing the results. The finest sampled charge-ledger errors
are at most 1.1642e-10 fC, against the unchanged 1e-5 fC bound. The earlier
accounting failure is absent with compensated accumulation.

The remaining issue calls for a numerical precision diagnosis: compare local
derivatives/accumulation and reference uncertainty at fixed saved states, using
an independent higher-precision calculation where necessary. Determine whether
the calcium discrepancy is integration error, algebraic rounding or a reference
floor before changing a criterion prospectively. It does not justify changing
channel conductances, adding imposed drive or treating the cell as physiologically
qualified. A native NEURON reproduction and live neuronal motor source remain open.

## Golgi reference refinement was interrupted before its last solve

`motor-axon-20261106-42c421`, source
`73db8166d81e0f88c46e6368aee196bf5b647070`, still contains a RUNNING status file.
After the operator's completion report, Task Scheduler showed Ready with result
3221225786, and no matching process remained. The stored RUNNING field is stale;
it is not evidence of an active or successful study. The precise cause of its
termination is not established by the saved experiment evidence.

All 33 transport/manifest cases passed. Eleven of twelve adaptive solves exist;
the missing solve is `no-sk / radau-strict`. All ten saved reference contrasts
for the first five protocols reproduce exactly from their NPZ arrays and pass the
unchanged count, 0.01 ms timing and 0.05 mV soma criteria. There is no final analysis
file, so the six-protocol reference qualification is incomplete. The existing
candidate comparisons are retained; this review does not recompute or qualify
the C# coupled solver.

The old backup contains only the initial 1,334 files (478,522,271 bytes), all of
which verify on C: and D:. It did not cover the later partial outputs. The stopped
capture was therefore copied and byte-checked into the new review as
`golgi-stopped-capture`: 1,740 files, 792,947,788 bytes, including the unchanged
original manifest/status. The new review's D: backup verifies all 1,746 files plus
its manifest, preserving the complete partial evidence without overwriting either
original capture.

Complete only the missing Radau solve in a fresh package, retaining the eleven
verified completed solves and original criteria. Recompute all required contrasts
before deciding the Golgi solver repair. Do not restart the old directory or
assume that the five completed protocols qualify the missing sixth.

## Next independent window

The next olive CPU-batch window runs 87 focused checks, including the new retained
motor-event/acknowledgement interface and all existing transport/calcium/plant
cases, then six complete CPU membrane trace comparisons. The initial prepared
eleven-test package was never launched and remains preserved. Use fresh source
for the expanded selection; no captured experiment is modified. No live world,
motor source or editor backend is enabled, and no live monitoring is used.
