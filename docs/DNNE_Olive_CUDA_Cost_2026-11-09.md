# Olive CUDA cost attribution

The reviewed timing result in `DNNE_Olive_CUDA_Timing_Results_2026-11-09.md`
showed that CUDA loses at the current 192-cell population, with modest wins only
for larger populations and the finer solver step. This experiment measures where
the existing batch API spends time before changing its execution strategy. It
does not enlarge the biological population or promote the CUDA candidate.

## Measurement design

`tools/OliveCudaCostProbe` builds three separate executables from the same source:

* `control`: the optional timing hooks have no implementations and are compiled
  away. This is the uninstrumented execution control.
* `host`: timestamps split each `Advance` into input validation/packing, the upload
  call, launch/synchronization, the download call, and report validation/sorting/
  commit. Launch submission and synchronization also have nested host intervals.
* `device`: the same host measurements plus two CUDA events per kernel invocation,
  recorded in its existing default stream. Events are allocated before timing,
  queried after the trajectory and explicitly destroyed before context disposal.

The optional partial methods use C#'s documented
[omission of unimplemented partial methods](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods).
No hook implementation is linked into the earlier CUDA/timing probes. Preparation
removes the explicitly marked hook lines and checks that the remaining execution
source equals the reviewed source. Seven other membrane/kernel/driver/control
files match exactly. The neuron equations and CUDA kernel remain unchanged.

CUDA event operations follow NVIDIA's
[driver event API](https://docs.nvidia.com/cuda/cuda-driver-api/cuda_driver_api/group__CUDA__EVENT.html).
The event interval measures the device timeline around submission and may include
scheduling or idle time; it is not a claim of pure arithmetic execution time.
Event recording itself can change scheduling. Its overhead is measured against
host-only and uninstrumented execution, rather than subtracted from a result.

The five host phases are contiguous portions of one call. Their sum must not
exceed the enclosing trajectory timer; the remainder retains caller, profiling
bookkeeping and timestamp overhead. Nested launch/sync intervals and device event
time are **not additional additive phases**. Upload/download timings include
ownership validation, pinning and download allocation, so they are not isolated
PCIe transfer measurements. Request generation and batch construction are timed
separately. No garbage-collection time inside the main timer is subtracted.

## Fixed work and independent controls

The populations are 192, 3,072 and 12,288, each at 0.025 and 0.00625 ms internal
steps. Every trajectory starts from the same saved CPU state at frame 256 and
executes the same 128 calls through frame 383. Each cell advances 8 or 16 ms.
The larger fixtures repeat the same 192 prototypes with unique explicit identities;
they do not represent additional connectivity or greater neuronal diversity.

Five rounds rotate mode order by round and case index. Each of the **90 processes**
runs one complete warmup trajectory followed by one measured trajectory from a
fresh identical state: **180 trajectories, 90 measured**. Every process has its own
CUDA context. Compilation, module loading, file I/O, initial/full-state downloads,
verification and event queries are outside the timer. Tiered JIT compilation is
disabled; each process warms its own code/context before measurement. This design
measures profiling effects within this experiment, not a direct historical speed
ratio against the earlier process layout.

The exact reviewed PTX and its compiler/device metadata are preserved from the
completed timing capture. Architecture and driver/device metadata must match.
No recompilation or precision-option change occurs. One GiB of reported free
device memory beyond owned buffers remains a preallocation requirement. Event and
driver resources are additional to the reported five-buffer allocation total.

The visible runner first executes eleven CPU/manifest checks and the six GPU
ownership/atomicity control groups in each variant (eighteen groups total).
Every trajectory saves all interval reports and the final full state. Independent
analysis checks every prototype and every repeated copy against the old CPU bytes,
with the same voltage/gate/calcium/current limits and exact clocks/spike counts.
Reports and final states must also be bit-identical across all three variants,
rounds and warmups. Identity, ordering, immutable parameters, guards, complete
coverage, event cleanup and source/binary hashes are checked. Failed trajectories
preserve already returned reports and the error; no automatic retry is introduced.

All five wall times, paired mode ratios, phase totals and per-call records are
retained. A mode median outside 0.9–1.1 times the uninstrumented median is marked
as materially perturbed for interpretation. This is not an accuracy failure or a
requirement that profiling be fast. Phase medians need not sum to the median total;
individual trajectory accounting is checked exactly in stopwatch ticks.

## Current status and execution

All three variants build with zero warnings/errors. Python and PowerShell syntax
checks pass. A read-only source audit against the already reviewed timing capture
verifies the unchanged arithmetic and optional-hook-only differences, on C:/D:.
These checks execute no model, GPU operations, tests or benchmark. Runtime
qualification and all measurements are pending the visible window.

`prepare-olive-cuda-cost.py` requires clean pushed source and the verified timing
review, then freezes sources, inputs, protocol, process schedule and PTX. The
launcher requires an initial verified D: backup and the operator's report that
other studies have finished. It uses an independent visible PowerShell window,
preserves failures and backs up the final evidence. No live monitoring.

The Golgi comparison `golgi-sdirk3-20261109-210c3d` is still awaiting the operator's
completion report. Hold this benchmark; do not poll that run or infer completion
from elapsed time. No live brain, Avatar, Unity, editor or old-save behavior changes.
This cost study will not qualify whole-brain throughput, cell physiology or Avatar
recovery, and a measured bottleneck is not yet an optimized implementation.

The frozen package is `olive-cuda-cost-20261109-06f78b`, revision
`f8561578320143a30f0fd0a95c9c914d72e711b0`. Its initial 1,441 files plus manifest
have a verified D: copy. It is **prepared only**, with no launch requested and no
GPU/test execution. After the operator reports the Golgi run finished, review its
evidence and launch this frozen runner with `-OtherStudiesFinished`. Avoid competing
builds, heavy offline audits or numerical work during the subsequent timed study.

A separate clock-arithmetic diagnosis has subsequently launched as
`motor-clock-20261109-6aaa98`. Wait for the operator to report **both** that window
and Golgi complete before launching this prepared timing package. It remains held.

The operator has now reported the clock window complete and its saved evidence
has been reviewed. Only Golgi's completion report remains outstanding. The later
motor neuronal comparison is prepared but held until this cost study finishes,
so it will not compete with the benchmark.

## Launched after completed-study reviews

The operator reported both Golgi SDIRK3 and the subsequently launched physical
sample transport checks complete. Both C:/D: captures have now been verified;
Golgi remains numerically unqualified and the 23 sample-transport checks pass.
Their offline reviews and source inspection finished before starting this timing.

The existing frozen cost package was launched with `-OtherStudiesFinished` as
independent visible task `DNNE-Olive-Cost-101087822647`. It is the only pending
window. Do not inspect live output, run builds, launch competing experiments or
perform heavy offline audits until the operator reports completion. The launcher
reverified the original source and D: backup before launch. No result or speedup
is claimed by launch; the prepared motor scheduling comparison remains held.

## First attempt: launcher failure before timing

The operator reported `FAILED`. The verified review is
`artifacts/olive-cost-startup-review-20261109/review.json`: all 2,201 files
(171,057,067 bytes per copy), frozen source and executed binaries match on C:/D:.
All 11 focused/manifest tests passed. Each of the three instrumentation variants
completed six CUDA control checks, reported context disposal and retained zero
owned buffer bytes. No timed process or measured trial started; no cost analysis
exists. There is no new speed or profiling-overhead result.

The 90-entry JSON schedule itself exactly matches the declared protocol. The
launcher wrapped the `ConvertFrom-Json` pipeline in `@(...)`. Windows PowerShell
5.1 emits an array as one pipeline object, so that wrapper nests all entries
inside an array of length one. The subsequent count check therefore rejected it.
The version-dependent behavior is documented in the
[PowerShell project discussion](https://github.com/PowerShell/PowerShell/issues/3424).

The repair assigns the deserialized array directly. Before building or creating
a CUDA context, it now checks all 90 population/step/round/mode/path entries and
their exact rotated order. A saved `schedule-check.json` records the shell
version and both old-wrapper and corrected shapes; the final analyzer requires
this check. This regression will execute in the next visible window, not inline.
The membrane implementation, GPU kernel, input schedule, timing cases and
numerical bounds are unchanged. Preserve the failed capture and use a fresh
frozen package for the retry; the motor comparison waits until timing is done.
