# Olive CPU/CUDA batch timing protocol

The arithmetic comparison is reviewed in `DNNE_Olive_CUDA_Results_2026-11-09.md`.
The next question is whether the unchanged resident GPU batch is useful at the
tested population sizes after transfer and host overhead. No production runtime
selects this benchmark and no biological parameters or accepted bounds change.

`tools/OliveCudaTimingProbe` links the exact reviewed CPU kernel/batch, CUDA
kernel/batch, driver and compiler sources. Package preparation checks these bytes
against the reviewed capture on C: and D:. The only new native function is a
probe-local memory observation using NVIDIA's documented
[cuMemGetInfo](https://docs.nvidia.com/cuda/cuda-driver-api/cuda_driver_api/group__CUDA__MEM.html).
Its reported free memory is a point-in-time observation, not a reservation for
DNNE or proof that other graphics workloads can coexist without contention.

## Fixed work and timing boundary

Six populations (1, 32, 192, 768, 3,072 and 12,288 cells) run at each of the two
already tested maximum internal steps, 0.025 and 0.00625 ms. Each trial starts
from exact saved CPU state at frame 256, then executes frames 256 through 383:
128 calls during the original stimulated interval. Cells retain their original
0.125 or 0.0625 ms interval, so the trial advances 16 or 8 ms respectively.
This is a short active-work timing fixture, not a long-time solver study.

Above 192 cells, complete prototype state/input sets repeat with unique explicit
instance/cell identities. This measures scaling of the same numerical work;
it does not test new neuron diversity, connectivity or increased biological size.

Every case uses CPU batches with worker limits of 1, 8 and 16, and the unchanged
CUDA batch. A full warmup trajectory precedes five measured trials per backend.
Backend order rotates each round. There are 48 warmup and 240 measured trials,
288 total. Each trial starts from the same state; no run continues another.

The wall timer encloses all 128 `Advance` calls and retention of their returned
report arrays. It includes request validation, allocations, sorting, CPU worker
scheduling, GPU input uploads, kernel launch, synchronization and report downloads.
It excludes input preparation, construction/initial upload, compilation, full-state
snapshot downloads, verification, compression and file writing. Construction is
timed separately. Compilation, allocation totals, process CPU time, garbage
collections and UTC intervals are recorded. Tiered JIT compilation is disabled
explicitly in this isolated process; native contexts and JIT code remain warm.
Garbage collection is requested before each timed trajectory, identically for
both paths. No garbage-collection pause within the timer is subtracted.

These boundaries measure the existing batch API. They do not measure network
transport, synaptic/receptor work, metabolic coupling, an entire brain timestep,
Unity graphics or physics. Do not extrapolate the short interval to sustained
whole-brain throughput without a subsequent integrated measurement.

## Accuracy and evidence

Eleven CPU/manifest regressions and all six GPU ownership/atomicity control groups
run before timing. Every trial verifies exact initial state, parameters and
identity/order, then saves all returned interval reports and final states in
gzip binary files outside the timer. Failure retains returned partial reports.

The independent Python audit checks every saved byte, including exact equality
between repeated prototypes. Each report's clock and interval crossing counts
must equal the corresponding original CPU trace. CPU final states and reports
must match byte for byte. GPU final state and report metrics retain the original
absolute bounds (1e-6 mV voltage, 1e-9 gates, 1e-6 micromolar calcium,
1e-6 load/current; exact clocks and counts). All numerical values must remain
finite and in domain. Larger populations are checked at every report and final
state, not by downloading all intermediate membrane states inside the timer.

The audit reports medians, ranges and all five paired speed ratios against the
fastest measured CPU configuration for each case. Speed is a measured outcome;
there is no requirement to make CUDA win. A slower GPU result is still a completed
study. A numerical failure cannot be traded for speed or hidden by an average.

Five resident buffers require `(47 * cellCount + 80) * 8` bytes, approximately
4.41 MiB at 12,288 cells. Driver/compiler overhead is additional. Free and total
device memory, owned-buffer bytes and host platform details are recorded.
Each allocation requires at least one GiB of reported free space beyond the
planned buffers; this is operating headroom, not a biological parameter.

## Execution and status

The C# build and Python/PowerShell syntax checks are performed before freezing.
No benchmark has run. Preparation requires clean pushed source and the verified
CUDA review, freezes sources/reference traces/compiler with hashes, and creates
a separate package and D: backup. The visible PowerShell window repeats the tests,
runs timing, audits saved bytes and verifies a final D: capture.

Do not launch while another study is running: the Golgi comparison is still
awaiting its operator-reported completion. `start-olive-cuda-timing-window.ps1`
requires the explicit `-OtherStudiesFinished` fact before launch and never polls
other jobs. Once that report arrives, use the normal independent visible window
with no live monitoring. Avoid starting other heavy work during the timing study.

This does not replace the outstanding cortical precision integration through
actual neuronal receptors and learned-state persistence. No live model, saved
brain, Avatar, editor or World behavior changes with this timing rig.

The prepared package is `olive-cuda-timing-20261109-eb7469`, frozen revision
`c4f80f22c6cd16b94f188cc558cba65002d1e07e`. Its initial D: backup verifies
1,431 files plus manifest. The C# build passes with zero warnings/errors and
Python/PowerShell syntax checks pass. It has not launched; the completion question
for `golgi-sdirk-20261109-440bb8` is still pending. No live Golgi results were read.

Subsequently, the operator reported Golgi finished. Its failed numerical outcome
was reviewed and preserved in `DNNE_Golgi_Coupled_Results_2026-11-09.md` before
launching this timing package. Task `DNNE-Olive-Timing-ef9e3d7627f9` now runs the
unchanged frozen revision above in an independent visible PowerShell window.
The launch succeeded; no live results have been inspected. Await the operator's
completion report and avoid other builds, numerical studies or heavy audits
during the timed work. No speed ratio is known yet.
