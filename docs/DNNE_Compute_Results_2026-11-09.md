# Completed isolated CPU cost baseline

The operator reported completion of `dnne-compute-20261109-fb9716`, frozen at
`dd5e915737846c6de5c824c0e354bc22b809efeb`. The window recorded
`DIAGNOSTICS_COMPLETE` at 08:12:44 UTC on 9 November. The original failed capture
`dnne-compute-20261109-faca37` remains unchanged.

`review-dnne-compute.py` independently rehashed both completed copies and checked
their file inventories, packaged source, binaries and saved comparisons. Each
copy contains 2,059 files and 493,320,323 bytes, plus its identical manifest;
1,335 files belong to the frozen package. The separate review is
`artifacts/dnne-compute-review-20261109/review.json`.

All 56 focused cases passed. All 42 neural runs and 21 motor-unit runs completed.
The eighteen neural observation pairs and nine motor serialization pairs agree
exactly within the audit scope defined in `DNNE_Compute_Baseline_2026-11-09.md`.
All 42 earlier neural output/state hashes reproduce. Generated neural message
UUIDs remain excluded from cross-run equality; connected identities, timestamps,
payloads and selected cell/contact state remain included. The full unit suite was
not run, and the earlier intermittent consolidation assertion remains unresolved.

## Measured costs

These are medians of three runs, for one biological second per isolated workload.
Neural tick times are uninstrumented; phase fractions use instrumented measurements
and are approximate ratios of medians. They are not whole-brain throughput.

| Current-model workload | Cells | Plain tick time, ms | Instrumented/plain time ratio |
| --- | ---: | ---: | ---: |
| TemporalAssociation, STDP | 384 | 293.133 | 1.124 |
| TemporalAssociation, tag fixture | 384 | 280.841 | 1.140 |
| Granule | 512 | 328.843 | 1.157 |
| Inferior olive | 192 | 8,727.578 | 1.020 |
| Purkinje | 192 | 8,295.267 | 1.015 |
| Spinal motor, current Izhikevich profile | 256 | 164.221 | 1.146 |

CellDynamics accounts for approximately 98% of instrumented olive tick time and
97% of Purkinje tick time. This bucket includes support assignment and intrinsic
drive as well as membrane integration; it does not isolate one channel function.
The olive/Purkinje membrane work is the first candidate for narrower profiling and
batched execution. The measured contact populations are sparse (16–26 inbound
contacts), so this does not establish that learning or synapses will be cheap in
a densely connected brain. Enqueue/drain here are in-process calls, not networking.

| Isometric motor units | Plant advance, ms | JSON round trip, ms | Receiver, ms |
| --- | ---: | ---: | ---: |
| 1 | 4.914 | 0.357 | 0.286 |
| 32 | 151.297 | 2.474 | 3.103 |
| 256 | 570.391 | 14.561 | 11.768 |

These are the existing fixed-length calcium/force calculations, driven by fixture
events. Three repetitions and current JIT/background-load behavior do not support
an asymptotic scaling claim. No live service transport or Unity physics was measured.

## Next execution boundary

First qualify the standalone CUDA compiler, driver, persistent buffers and exact
double-precision transfer/arithmetic path described in
`DNNE_Cuda_Interop_2026-11-09.md`. Its PowerShell window is independent and must be
reviewed after operator-reported completion. It provides no speedup estimate.

If that passes, develop an isolated batched membrane candidate, starting with the
expensive olive work and retaining the current CPU implementation as reference.
Preserve full state and explicit cell identity, timestamped inputs and emitted
events; do not replace the brain scheduler or learning with an unrelated toy net.
Compare short-interval state accuracy and refinement at declared bounds before
interpreting long spike trains or timing a candidate as acceptable. Measure CPU
batching and GPU end-to-end execution, including transfers and synchronization,
with the same workload. Keep state resident where that preserves causal timing.

The unresolved olive numerical/physiological questions remain open on either
processor. The isolated timing/local-learning candidates are not silently enabled.
CUDA will not repair biological calibration, learning precision or the existing
population-derived motor authority. Those correctness tasks and actual Avatar
balance/recovery remain requirements of the active goal.
