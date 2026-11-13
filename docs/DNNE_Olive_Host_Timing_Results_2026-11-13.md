# Matched CUDA host workspace: benefit at larger batches

The operator reported `olive-host-timing-20261113-ab15ac` complete. Both variants
passed all accuracy checks: 11 focused tests, 24 control groups and exact recorded
bytes for all 144 trajectories (72 warmups and 72 measured). The optimized host
workspace reduced elapsed time consistently at 3,072 and 12,288 cells. The 192-cell
cases improved slightly but did not meet the predeclared 5% practical threshold.

| Cells | Maximum step (ms) | Original median (ms) | Candidate median (ms) | Time saved | Faster paired rounds |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 192 | 0.025 | 289.60 | 281.89 | 2.66% | 6/6 |
| 192 | 0.00625 | 408.70 | 398.76 | 2.43% | 6/6 |
| 3,072 | 0.025 | 486.94 | 372.90 | 23.42% | 6/6 |
| 3,072 | 0.00625 | 605.81 | 492.47 | 18.71% | 6/6 |
| 12,288 | 0.025 | 1,976.99 | 1,425.92 | 27.87% | 6/6 |
| 12,288 | 0.00625 | 2,458.26 | 1,900.07 | 22.71% | 6/6 |

These are median times for 128 synchronous batch `Advance` calls, including
validation, packing, transfer, GPU calculation/synchronization, returned-report
handling and commit. They exclude construction, request preparation, snapshots
and disk writes, as declared before the run. The six paired rounds and 5%/five-
of-six rule are descriptive; no statistical confidence claim is made.

Managed allocation per measured trajectory fell from approximately 14.78 MB to
2.86 MB at 192 cells, 236.27 MB to 44.15 MB at 3,072 and 950 MB to 176.27 MB at
12,288 (decimal MB). This is allocated byte volume, not peak live memory or GPU
memory use. The unchanged kernel, PTX and neuronal equations produce exact
recorded results across both variants, rounds and warmups.

Use the reviewed workspace candidate for subsequent CUDA batch integration work
at the demonstrated larger sizes, retaining the original implementation and CPU
reference. This comparison does not establish a new CPU/GPU crossover: both
timed variants use CUDA. It does not measure synaptic/service traffic, whole-brain
throughput or body stepping. CPU remains the live default; no host selection,
biological parameters, Avatar, World, editor or saves are changed by this result.

## Evidence review

The capture used revision `8a3c63c1c8cc1f4c4a5337fbb5b56001c1f87417`, RTX 5060,
and .NET 10.0.15. Device metadata matched the earlier accuracy capture. The four
recorded .NET binary hashes agree throughout all current processes; this is a
new matched run, not a comparison of timings taken under different runtimes.

`tools/review-olive-host-timing-20261113.py` verified all 3,059 files in each C:/D:
capture (398,937,317 bytes per copy), frozen Git source, executed binaries and all
eleven test identities. The frozen analyzer independently reread/decompressed
all saved traces and reproduced the full analysis exactly, without executing a
model or benchmark. The manifest SHA-256 is
`255CBE6F5DD6A81DB1389D06CBAF555A714EEC4EC5F9AE6496AA02F97D70B72C`.
The separate `artifacts/olive-host-timing-review-20261113/review.json` has a
verified D: backup plus manifest. This window is no longer pending.
