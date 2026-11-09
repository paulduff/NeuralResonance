# Olive CUDA membrane: completed comparison

The operator reported completion of `olive-cuda-20261109-cd1fee` on 9 November.
Its frozen revision is `f8de323a5ef989219d10be8906f9f46aa13cbdbf`; the window
completed at 11:06:18 UTC. The complete capture and D: copy contain 2,009 files
(652,886,707 bytes) plus matching manifests. Every byte hash, all 1,419 frozen
package files and all recorded test/interop/probe binaries were verified.

`tools/review-olive-cuda-20261109.py` independently rereads the existing binary
traces without executing a model or accessing the GPU. Its saved review is
`artifacts/olive-cuda-review-20261109/review.json`, also verified on D:.
The original capture remains unchanged.

Eleven focused olive/solution tests, six native interoperability cases and all
six GPU ownership/atomicity control groups pass. All six membrane comparisons
pass their prospectively declared bounds: 1, 32 and 192 cells, each at maximum
internal steps of 0.025 and 0.00625 ms, with 1,024 input frames. Across 461,250
cell-frame records, initial state transfers are exact, biological clocks and
cumulative soma/axon counts agree at every frame, and all states remain finite
and in their declared domains. The saved-byte audit reproduces the window's
analysis exactly, including context cleanup evidence.

| Quantity | Largest absolute difference | Original limit |
| --- | ---: | ---: |
| Membrane voltage | 1.615e-10 mV | 1e-6 mV |
| Gate state | 5.833e-13 | 1e-9 |
| Calcium | 1.492e-10 micromolar | 1e-6 micromolar |
| Biological time | 0 | Exact |
| Per-frame cumulative crossings | 0 | Exact |

At either step, the 32-cell case records 26 soma and 439 axon crossings; the
192-cell case records 160 soma and 2,762 axon crossings. The one-cell fixture is
quiet. These are interval counts, not interpolated network spike timestamps.

This is a successful float64 arithmetic port for the exercised membrane inputs.
It does not establish speedup, convergence of the underlying biological equations,
human physiology, receptor/metabolic coupling, a recurrent network or Avatar
recovery. No live host selects the candidate and no saved brain was loaded.

Next measure the existing batch API, including validation, allocation, request
upload, device synchronization and returned reports. Compare the same work against
single-worker and parallel CPU batches, preserve accuracy checks at larger sizes,
and record memory headroom. Avoid overlapping the timing study with the pending
Golgi study. Cortical contact precision through actual receptors and persistence
remains a separate integration repair; GPU arithmetic does not resolve it.
