# Matched CUDA host timing comparison

The host-workspace candidate passed its accuracy review in
`DNNE_Olive_CUDA_Host_Results_2026-11-09.md`. Its speed remains unmeasured. This
prospective comparison measures the original and candidate implementations on
the same current machine, with complete recorded-byte accuracy checks retained.
No live runtime selects either isolated probe.

## Unchanged work and measured boundary

`tools/OliveCudaHostTimingProbe` builds separate `reference` and `candidate`
executables from the exact reviewed batch implementations. The membrane, driver,
CUDA source and frozen PTX are unchanged. No phase-profiling implementation is
linked; the executable rejects compiled cost hooks. Both variants include the
six original and six host-ownership/error control groups.

The six workloads retain 192, 3,072 and 12,288 cells at maximum steps of 0.025 and
0.00625 ms. Every trajectory starts from recorded CPU frame 256 and advances
128 frames. Caller order reverses on alternate frames, as in the passing accuracy
experiment. Each cell retains its identity, prescribed interval and original
inputs. The timing harness does not change integration, rounding or report order.

The timer surrounds all 128 synchronous `Advance` calls: request validation and
packing, transfers, GPU execution/synchronization, report validation, sorting and
commit. Request construction, initial transfer checks, snapshots, accuracy checks,
compression and disk writes are outside that timer. Constructor/setup time is
recorded separately. This measures a persistent batch-call workload, not total
service startup, network traffic, contact delivery or whole-brain throughput.

Allocation counters begin after creating the measurement scaffolding. Managed
allocation, process CPU time and collection counts accompany elapsed time; they
are observations of this process, not general memory/performance guarantees.

## Order, replication and interpretation

Each workload has six paired rounds. Order alternates by round and case index,
so each variant goes first three times per case. Every variant/round uses a new
process with one complete warmup followed by one measured trajectory. There are
72 scheduled processes, 72 warmups and 72 measured trajectories. Controls run in
two additional processes before those trials. Tiered compilation is disabled
equally for both variants; identical explicit garbage collection precedes each
trajectory. These conventions match the previous timing approach except for the
declared alternating requests and paired two-variant schedule.

The report retains every warmup and measured sample, medians, paired time ratios,
allocation medians and setup/CPU observations. The prospective practical-benefit
rule is at least a 5% reduction in median elapsed time, with the candidate faster
in at least five of six paired rounds. This is a descriptive decision rule, not
a statistical confidence claim. A slower or inconclusive result is valid evidence;
the run does not fail merely because the optimization offers no benefit. Do not
extend a benefit at one population size to another, or infer a new CPU/GPU
crossover without a matched CPU comparison. CPU remains the current default.

## Accuracy and provenance gates

Both warmup and measured reports and final states must exactly reproduce the
reviewed recorded bytes. The offline analyzer independently checks decompressed
lengths and hashes for all 144 trajectories, as well as seed identity, complete
schedule, return order evidence, timing/accounting metadata and buffer cleanup.
The 24 control groups must reproduce the saved accuracy-control evidence exactly.
This retains the earlier numerical result through exact bytes; it is not a new
adaptive-reference solve or a physiological calibration.

Every process records its current device and .NET runtime metadata plus hashes
of CoreLib, coreclr, clrjit and hostpolicy. These must agree throughout the new
comparison. The device may differ from the older capture only while matching
the frozen PTX architecture and reproducing all recorded results; that difference
is explicitly reported. The comparison is between the two new matched variants,
not between a new timing and an old timing measured under another driver.
The analysis interpreter fingerprint is recorded before execution and checked
again afterward. These inventories are scoped evidence, not a frozen operating
system or proof of absence of unrelated machine load.

The preparer requires clean pushed source, the verified passing host-accuracy
review, full C:/D: capture verification and unchanged mechanism/control sources.
It copies exact CPU inputs, PTX, recorded traces and control evidence into a new
package. The visible runner checks its 72-entry schedule directly before any
CUDA execution, retaining the PowerShell 5.1 JSON-array fix. It runs 11 focused
CPU/manifest tests, builds both variants, executes controls and timed trials,
checks all evidence and creates the final verified D: backup.

## Current status and execution gate

Both variants build with zero warnings/errors. Python, project/solution XML and
PowerShell syntax checks pass. The new project appears in the solution manifest.
No runtime test, GPU control or timing trajectory has run for this harness.
The static source/build inventory is
`artifacts/olive-host-timing-preparation-20261113/source-review.json`, with a
verified D: copy. All eleven mechanism/control sources match the reviewed
accuracy capture after newline normalization; the capture itself will be fully
reverified by preparation before any later launch.

This rig is coded only: no benchmark package has been frozen or launched. Await
the operator's completion report and review of `golgi-sdirk3-fine-20261113-ca813f`.
Then prepare with `prepare-olive-host-timing.py --host-review REVIEW
--other-studies-finished`, verify the initial D: backup, and launch the frozen
`start-olive-host-timing-window.ps1 -PackageDirectory PACKAGE -OtherStudiesFinished`.
Do not run competing builds, experiments or heavy evidence hashing during timing.
No live monitoring; the operator reports completion. Old saves, Avatar dynamics,
World/Unity behavior and editor qualification labels remain unchanged.
