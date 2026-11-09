# CUDA interoperability: helper loading failure and repair

The operator reported completion of `cuda-interop-20261109-4b5d36`. Its actual
status is FAILED, recorded at 08:24:43 UTC on 9 November. The frozen source is
`5cd3a850814ae373bfdc35f92891df0ce53ec70d`; preserve that capture unchanged.

`review-cuda-interop-failure.py` verifies the complete C:/D: inventories and hashes,
package and built binaries. Each copy contains 1,381 files and 467,079,312 bytes
plus its manifest; 1,345 files belong to the frozen package. The separate review
is `artifacts/cuda-interop-failure-review-20261109/review.json`.

The driver query identified the RTX 5060, device architecture 120 and 8,518,041,600
device-memory bytes. NVRTC then failed during compilation with error 7:
`failed to open nvrtc-builtins64_130.dll`. The helper is present and matches the
pinned NVIDIA archive's expected SHA-256. No PTX was saved and no arithmetic case
ran. This does not qualify GPU arithmetic or speed, and it is not evidence of
failed neuronal dynamics.

The original binding loaded the compiler by absolute path, but NVRTC opens its
helper later. Flags governing the compiler's initial dependency search do not
configure every future dynamic load. The repair explicitly loads the verified
helper by absolute path first and retains its handle throughout compilation.
Both loaded module paths and their basename handle lookups must match the
intended libraries. The compiler is released before our helper reference.
No PATH, system DLL search policy, installed driver or existing environment is
changed. An actual dependency failure will now surface at the explicit helper
load instead of being hidden behind NVRTC's later generic diagnostic.

This approach follows Windows' documented
[loaded-module lookup and full-path loading rules](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order).
It is a repair candidate until the independent PowerShell retry completes.

Protocol `cuda-double-interop-v3` adds saved loaded-library paths and requires
their validation against the compiler manifest. The six counts, seven launches,
float64 precision criteria, guard regions and context-cleanup checks are unchanged.
The source builds cleanly and static checks pass. Runtime success is pending;
keep the failed package and initial review backed up and use a fresh retry package.

The CPU performance baseline remains successfully reviewed in
`DNNE_Compute_Results_2026-11-09.md`. The newly prepared olive CPU layout comparison
is independent numerical work, described in `DNNE_Olive_CPU_Batch_2026-11-09.md`.
Keep that comparison prepared while the single CUDA retry window awaits the
operator's report, to avoid confusing two new outcomes. Do not live-monitor either.
