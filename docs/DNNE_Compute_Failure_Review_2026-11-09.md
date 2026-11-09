# CPU cost study: completed neural evidence and serialization repair

The operator reported `dnne-compute-20261109-faca37` failed. Its frozen revision
is `67e7ea55c997276614f88243fe895f14afc48c0e`; completion was recorded at
07:51:58 UTC on 9 November. Preserve its FAILED status and all files.

`review-dnne-compute-failure.py` rehashed both the complete C: capture and D: copy,
their manifests, packaged source and recorded binaries. Both contain 1,983 files
and 403,916,161 bytes, including 1,325 packaged files. All 53 selected test results
passed. The review is saved separately in
`artifacts/dnne-compute-failure-review-20261109/review.json`.

All 42 neural runs completed. Independent inspection of saved output/state files
confirmed all eighteen plain/instrumented equality pairs under the explicitly
documented generated-message-ID exclusion. The observed median uninstrumented
tick costs for one biological second were:

| Current isolated workload | Tick time, ms | Instrumented/plain median ratio |
| --- | ---: | ---: |
| TemporalAssociation/STDP | 290.096 | 1.151 |
| TemporalAssociation/tag fixture | 286.290 | 1.180 |
| Granule | 329.443 | 1.172 |
| Olive | 8,713.997 | 1.027 |
| Purkinje | 8,333.953 | 1.010 |
| Spinal motor | 166.632 | 1.122 |

These are isolated current-model costs under this workload, runtime and machine
load. They are not whole-brain throughput, physiological qualification or an
estimate of CUDA speedup. The heavier olive/Purkinje workloads warrant attention,
but the missing body stage and numerical limitations still matter.

The run failed while serializing the first motor-plant warmup's private state,
after its stepping/delivery checks, with `System.NotSupportedException` for
`System.IntPtr`. The generic reflection helper traversed the receiver priority
queue's comparison delegate. Native delegate entry-point addresses are process
implementation details and are not suitable JSON comparison state. This is an
evidence-encoding fault, not evidence that the muscle solver overflowed or the
neural equations failed. The motor comparison and complete study remain unfinished.

The repair records delegate invocation order, declaring type, module identity,
method token and captured closure fields. It does not silently skip the comparator
or its state. Unexplained native pointers now cause an explicit error. Three new
regression cases exercise serializable queue comparers, preserved closure state
and rejection of unknown pointers. Only diagnostic encoding changes; Avatar's
receiver, plant, calcium dynamics and DNNE equations are unchanged.

The replacement uses the same inputs, populations, durations and equality gates.
Its 56 focused cases include the three new regressions. The verified earlier
neural hashes are copied into the frozen package; all 42 earlier neural semantic
outputs and selected state snapshots must reproduce, independently of new timing
values. The retry uses protocol version `isolated-cpu-costs-v2` to identify the
evidence encoding change. It does not erase or reinterpret the failed run.

Build and static checks pass; replacement runtime results await the operator's
report. All actual testing stays in an independent visible PowerShell window,
with no live monitoring. Prepared CUDA interoperability tooling is separate and
has not executed a GPU kernel.
