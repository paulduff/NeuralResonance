# CUDA host workspace: completed accuracy comparison

The operator reported `olive-cuda-host-20261109-aeb5f2` complete. All 11 focused
tests passed. Reference and candidate each passed six original and six added
GPU control groups. All six complete workloads reproduce the reviewed recorded
reports and final states exactly, with no precision or acceptance-bound change.
This qualifies the candidate for a subsequent matched timing experiment; it does
not establish that the optimization is faster.

The read-only review is `artifacts/olive-cuda-host-review-20261109/review.json`.
All 2,174 files (186,019,116 bytes per copy), frozen source, inputs and executed
binaries verify on C: and D:. Revision is
`91c53eab1ae51f9270a732e051eb0cc30782d352`; capture manifest SHA256 is
`C524A1BBACAC4B526376C1D305A5F5813548D62A716FED24254D6BDA19DC62B6`.
The saved analysis reproduces exactly from immutable stored bytes. No GPU work,
model execution or benchmark occurred during review. The separate review has
a verified D: backup.

## Coverage

The six workloads cover 192, 3,072 and 12,288 cells at maximum steps of 0.025 and
0.00625 ms, each for 128 frames. Every report and final cell state is checked
across reference, host candidate and the reviewed cost capture. Decompressed
lengths and hashes are verified, not just selected state values or final counters.
The original CPU numerical comparison remains applicable through those identical
recorded bytes; this run did not execute a new CPU trajectory.

Controls preserve request validation, ownership, untouched cells, rejected-request
atomicity, numeric-failure recovery and identity permutations. Added checks cover
40 frames of changing subsets/intervals/order, retained uploaded inputs after
rejection, empty requests, returned-report ownership, failed work followed by a
different subset, and disposed-owner rejection. Both variants' complete control
evidence agrees exactly. Both report zero owned device-buffer bytes after their
batches and explicit context disposal. These are exercised controls, not an
exhaustive concurrency or failure proof.

The focused TRX contains ten olive-batch cases and one solution-manifest case.
It is not a new full DNNE unit-suite pass.

## Next measurement

Compare unchanged reference and the reviewed host candidate using matched inputs,
warmups, rotated execution order and repeated processes. Include all packing,
validation, transfer, synchronization and report handling in the measured call;
exclude fixture generation and disk writes consistently. Check saved results as
well as elapsed time. Predeclare the protocol before running, preserve the exact
kernel/driver and retain CPU as the current default. Do not infer whole-brain
speed from one batch or assume small populations benefit.

Both this accuracy window and the somatic motor-unit window are now reported and
reviewed; neither remains pending. The prepared finer Golgi comparison runs next.
Host timing must wait for that operator completion report to avoid competing CPU
load. No live host selects this candidate, and no physiological calibration or
Avatar recovery claim follows from numerical agreement.
