# Olive batch timing: selective CUDA benefit

The operator reported `olive-cuda-timing-20261109-eb7469` complete on 9 November.
Frozen source is `c4f80f22c6cd16b94f188cc558cba65002d1e07e`; completion was
11:44:15 UTC. All 3,119 files (769,819,040 bytes) on C: and D:, matching manifests,
1,430 frozen package files and both binary manifests verify. The independent
review reproduces every saved accuracy check and timing statistic without running
the model again. Its artifact is `artifacts/olive-timing-review-20261109/review.json`.

Eleven focused tests, six GPU control groups and all 288 trials complete:
48 warmups and 240 measured trials. CPU results remain exact against the archived
reference; all GPU reports and final states meet the original numerical limits,
with exact clocks and crossing counts. Larger repeated populations retain their
identities and equal prototype trajectories. Native context cleanup is recorded.

## Measured outcome

Median milliseconds for 128 complete batch calls, including host validation,
allocation, sorting, GPU uploads, launch/synchronization and report download:

| Cells | Maximum step (ms) | Fastest measured CPU (ms) | CUDA (ms) | CPU time / CUDA time |
| --- | ---: | ---: | ---: | ---: |
| 1 | 0.025 | 0.625 | 48.349 | 0.013x |
| 32 | 0.025 | 9.935 | 165.321 | 0.060x |
| 192 | 0.025 | 51.186 | 289.611 | 0.177x |
| 768 | 0.025 | 133.358 | 323.417 | 0.412x |
| 3,072 | 0.025 | 426.928 | 511.662 | 0.834x |
| 12,288 | 0.025 | 1,709.199 | 2,037.241 | 0.839x |
| 1 | 0.00625 | 2.381 | 176.303 | 0.014x |
| 32 | 0.00625 | 20.946 | 237.270 | 0.088x |
| 192 | 0.00625 | 89.541 | 404.864 | 0.221x |
| 768 | 0.00625 | 234.121 | 447.805 | 0.523x |
| 3,072 | 0.00625 | 826.887 | 629.417 | 1.314x |
| 12,288 | 0.00625 | 3,418.885 | 2,507.832 | 1.363x |

The fastest CPU configuration uses one worker for one cell, and a sixteen-worker
limit for the other cases. These are limits on the measured CPU batch, not a
claim that the production brain already uses that implementation. Each backend
has five measured trials after its warmup; all samples and paired ratios are saved.
For 3,072 cells at the fine step, paired ratios span 1.281–1.355x; for 12,288,
1.311–1.426x. Thus the two median GPU wins recur in all five paired observations.
At 192 cells, the CPU batch is about 4.5–5.7 times faster than this CUDA batch.

The largest case owns 4,620,928 bytes of device buffers. Minimum sampled free
memory is 7,367,294,976 bytes. This fixture does not demonstrate GPU memory
pressure, nor does it measure the memory needs of a connected whole brain.

## Decision and limits

Keep CPU execution as the production default. The current 192-cell olive does
not justify deploying this CUDA implementation on speed grounds. Retain the
accurate GPU candidate for larger compatible batches and further profiling;
do not increase biological population sizes merely to make the GPU benchmark win.

The measured larger/finer advantage is modest. Before optimizing, separate
kernel time, transfers and host bookkeeping to locate the remaining cost.
These data alone cannot assign it to a particular GPU hardware limit, allocation
pattern or synchronization call. Preserve numerical precision and existing
acceptance bounds through any optimization.

The benchmark advances only a short saved active interval, repeats prototype
cells above 192, excludes synapses/receptors/metabolism/network traffic and does
not measure Unity. It does not prove sustained whole-brain throughput, physiological
validity or Avatar recovery. No live backend changed. The next functional repair
remains cortical precision through actual recurrent cells and learned-state saves;
Golgi still needs its separately identified integration-accuracy repair.
