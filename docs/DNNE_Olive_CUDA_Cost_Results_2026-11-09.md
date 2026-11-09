# Olive CUDA cost results

The operator reported the corrected cost window complete. The capture
`olive-cuda-cost-20261109-fedff9`, frozen at
`0d94dd2cd7d0b402451e03c508ef253cacd93c26`, finished with
`DIAGNOSTICS_COMPLETE`. The earlier `06f78b` launcher failure remains preserved.
The initial FAILED footer reported during this review did not match the retry's
then-unfinished final analysis; the operator subsequently supplied its completed
footer before the full review proceeded.

## Verified evidence

`artifacts/olive-cost-review-20261109/review.json` records verification of all
3,319 files and 456,584,263 bytes in each C:/D: capture, including frozen inputs,
sources and executed binaries. The backup manifest SHA256 is
`413658027F5A445A4808DE6C9848228BAF5A0240D14C0CA6FEDB6E01EF1DE2E0`.
The review reruns only the saved-byte analyzer, reproducing `cost-analysis.json`
exactly. It runs no neuronal simulation, CUDA operation or benchmark. The review
itself is separately backed up on D:.

All 11 selected tests pass: ten olive batch cases and one solution-manifest case.
The PowerShell 5.1 regression records the old wrapper's one nested array and the
corrected direct array's 90 entries, with every scheduled entry checked. All 18
CUDA control groups pass. All 90 processes complete both their warmup and measured
trajectory: 180 trajectories, each containing 128 batch advances. Reported owned
buffers are zero after cleanup; device-mode events are created and destroyed in
matched counts.

Saved reports and final states are bit-identical across modes, rounds and warmups
at each population/step. Every copied prototype is checked against the preserved
CPU trace at the original limits, including exact interval clocks and spike
counts. All phase-accounting checks pass. This validates the tested arithmetic
and instrumentation, not physiological calibration or a functioning whole brain.

## Measured costs

Each time below is the median of five measured 128-call trajectories, in ms.
The three variants use the same frozen inputs and numerical implementation.

| Cells | Internal step (ms) | Uninstrumented | Host profiling | Host + device events |
|---:|---:|---:|---:|---:|
| 192 | 0.025 | 289.500 | 290.332 | 290.550 |
| 192 | 0.00625 | 405.387 | 408.124 | 406.093 |
| 3,072 | 0.025 | 483.375 | 482.603 | 482.328 |
| 3,072 | 0.00625 | 599.392 | 602.965 | 600.569 |
| 12,288 | 0.025 | 1,961.017 | 1,974.048 | 1,974.884 |
| 12,288 | 0.00625 | 2,444.818 | 2,446.139 | 2,446.847 |

Profiling median/control ratios range from 0.99784 to 1.00707. None reaches the
predeclared 10% material-difference threshold. These five-round observations are
not a statistical bound on overhead under every workload. All individual timings
and paired ratios remain in the saved analysis.

Host-only phase medians, again in ms:

| Cells / step | Validate + pack | Upload call | Launch + synchronize | Download call | Validate + sort + commit |
|---|---:|---:|---:|---:|---:|
| 192 / 0.025 | 3.703 | 1.901 | 274.008 | 2.918 | 7.838 |
| 192 / 0.00625 | 3.567 | 1.810 | 392.931 | 2.704 | 7.382 |
| 3,072 / 0.025 | 48.553 | 3.701 | 289.126 | 8.060 | 133.123 |
| 3,072 / 0.00625 | 48.588 | 3.798 | 406.302 | 7.945 | 134.128 |
| 12,288 / 0.025 | 235.206 | 10.265 | 1,097.420 | 24.241 | 611.375 |
| 12,288 / 0.00625 | 225.260 | 10.494 | 1,579.051 | 24.880 | 604.937 |

Ratios of phase medians to host total medians put launch/synchronization near
94–96% for 192 cells and 56–67% for the larger batches. Packing plus report
processing accounts for roughly 30–43% in the larger batches. Upload/download
calls together are about 1–2.5%. These are descriptive ratios of separate medians;
they need not add exactly to one. Individual trajectory sums were checked in
integer stopwatch ticks, including the small time outside the phases.

The device-event medians range from 272.537 to 1,560.884 ms and are close to the
corresponding launch/synchronization interval. Device events include scheduling
and possible idle time, so this does not distinguish kernel arithmetic from all
GPU scheduling costs. Upload/download calls include allocation, pinning and
ownership checks; these are not isolated PCIe bandwidth measurements.

## Consequences and next work

The result does not support treating transfers as the dominant cost. Keep CPU
production defaults: this experiment contains no contemporary CPU speed comparison
and does not overturn the earlier finding that CPU is faster at 192 cells.
Larger fixtures repeat 192 prototypes; they do not establish dense connectivity,
whole-brain throughput or a reason to enlarge biological populations.

A useful next CUDA candidate is to reduce repeated host allocation and identity
sorting at the existing API boundary while preserving arbitrary subset/order,
per-cell times, immutable returned reports and all-or-nothing state commits.
Input packing and report processing are substantial in larger batches. That is
an optimization hypothesis, not an implemented or measured improvement. GPU
execution/scheduling still needs separate investigation; do not weaken precision,
skip intermediate biological events or merge future inputs to amortize launches.

The timing hold is satisfied. The already frozen motor-boundary comparison can
now run visibly in PowerShell without monitoring: 12 focused checks, 30 unchanged
original neuronal controls and 30 endpoint-scheduled candidates. It addresses the
remaining motor-cell numerical gate before the somatic motor-unit circuit. The
finer Golgi rig remains coded but unlaunched. Live DNNE, Avatar, Unity and editor
behavior, biological parameters and old saves are unchanged by this review.
