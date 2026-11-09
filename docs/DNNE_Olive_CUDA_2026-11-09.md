# Resident olive membrane CUDA candidate

The reviewed CPU batch in `DNNE_Batch_And_Component_Results_2026-11-09.md`
matches all original membrane bytes across six cases. This permits the first
model calculation on CUDA. `tools/OliveCudaProbe` now contains that candidate;
no live DNNE, Avatar, World, editor or saved brain selects it. This is an
arithmetic port of the existing three-compartment olive, not a new neuron model.

## Implementation

`olivary.cu` transcribes the 14-state `OlivaryBatchKernel`, including all channel
rates, adaptive internal RK4 steps, input fields, calcium, load/current metrics
and soma/axon crossings. One GPU thread owns one cell. State and parameters use
structure-of-arrays buffers; the 14-state membrane remains resident between calls.
Explicit instance/cell identities map requests to lanes on the host. Each cell
has its own clock; missing, duplicated, stale, gapped or invalid requests are
rejected before upload. Reordering lanes or requests must not change results.

The kernel writes a separate candidate state and per-lane reports. Only when
every lane succeeds and all report/counter checks pass does the host swap the
accepted state buffers and advance clocks/counters. A numerical failure leaves
public accepted state untouched; it does not substitute a CPU result, clip state,
discard an event or retry automatically. A later explicitly supplied valid request
is checked against a fresh GPU batch. Device/driver faults are separate from this
numerical transaction guarantee; context disposal handles failed device calls.

Unrequested cells are copied unchanged to the staging buffer. Eight guard doubles
surround each buffer; state snapshots, reports and the final buffer audit check
guards, and final audits compare immutable parameters/inputs with uploaded values.
Seed and snapshot arrays cannot mutate device state. Native calls are restricted
to the creating thread/context; the typed launch rejects wrong sizes, aliases
and foreign kernel handles. The existing simple interoperability route also now
checks its kernel handle against the context's loaded function.

The declared population bound is 32,768 cells. Per-cell kernel work is capped at
100,000 internal steps: exceeding it explicitly fails the whole batch without
committing. This additional execution bound is not a hidden shortened interval.
The first study exercises 1, 32 and 192 cells; it does not qualify every supported
population size, high-drive condition or long biological trajectory.

Float64 is used throughout the device calculation. The pinned NVRTC 13.0.88
compiler/builtins retain their verified absolute-path loading. Compilation uses
the actual device architecture, C++17, `--fmad=false`, `--ftz=false`, precise
division and precise square root; no fast-math flag is enabled. Device min/max
helpers propagate NaN rather than silently selecting the other finite operand.
The full source, PTX, options, compiler hashes and device identity are saved.

The binding follows NVIDIA's documented [kernel argument pointer convention](https://docs.nvidia.com/cuda/cuda-programming-guide/03-advanced/driver-api.html).
The [CUDA numerical comparison guidance](https://docs.nvidia.com/cuda/cuda-c-best-practices-guide/index.html#numerical-accuracy-and-precision)
supports explicit error checks rather than assuming identical CPU/GPU transcendental
results. These references motivate the implementation and checks, not a claim
that this candidate has passed or is faster.

## First visible comparison

The frozen package copies the 21 required CPU protocol/design/result/reference
files from `olive-batch-20261109-11018c`, after checking both C: and D: copies
and the completed review. Its original membrane, CPU kernel and batch source
must match the reviewed archived bytes. It also copies the verified NVRTC archive,
compiler/builtins and licenses. Old captures are never overwritten.

The independent PowerShell window performs:

1. Eleven existing olive/solution tests against frozen source, including the
   newly registered probe project in the solution-manifest check.
2. The six prior native interoperability/guard cases and saved-byte audit.
3. Six new GPU control groups: exact seed/counter transfer and caller ownership;
   six invalid request variants with unchanged state; thread ownership;
   untouched-cell state/clock; 64 frames of lane/request permutation; and
   failed-lane atomicity followed by an explicit valid request.
4. The six complete original CPU fixture designs on the GPU. They retain 1,024
   input frames, both maximum internal steps (0.025 and 0.00625 ms), all 14
   states, per-cell clocks/load/current, and int64 crossing counters. Input order
   is permuted, while trace rows retain the original declared identity order.
5. An independent Python audit of every saved record, final source/binary and
   dependency hashes, context cleanup, and a verified final D: backup.

GPU physical trajectories are saved before assessing agreement. A comparison
failure does not discard later complete conditions. Numerical execution failures
retain partial traces and exception details; a driver fault may prevent later
conditions. Startup/control failures retain source, compiler evidence and logs.

Predeclared maximum absolute differences at every saved state:

| Quantity | Limit |
| --- | ---: |
| Each of three voltages | 1e-6 mV |
| Each gating state | 1e-9 |
| Calcium | 1e-6 micromolar |
| Load and synaptic-current metrics | 1e-6 in the reference model's units |
| Biological clock | Exact |
| Cumulative soma/axon counts at every frame | Exact |

Initial physical states/counters must transfer exactly. All values must remain
finite and within the original model's gate/calcium domains. The numerical bounds
are fixed before execution and much smaller than the scales of the model's
variables; spike-count equality is separately mandatory. Passing both step-size
cases qualifies this CPU/GPU comparison, not convergence of the underlying olive
equations against an independent solver. No bound changes after seeing a failure.

These are membrane interval reports, not interpolated firing-time records or
network `SpikeMessage`s. Receptors, local chemical/learning states, metabolic
feedback and recurrent scheduling are outside this initial batch boundary.
Actual event transport and longer trajectories remain prerequisites for promotion.

## Status and continuation

Both C# probe builds pass with zero warnings/errors; Python and PowerShell syntax
checks pass. CUDA compilation and execution occur only inside the visible window.
Prepare from clean pushed source with `prepare-olive-cuda.py`, verify its initial
D: backup, then use the frozen `start-olive-cuda-window.ps1`. No live monitoring;
review only after the operator reports completion. The independently running
Golgi comparison remains untouched.

This study does **not** measure speedup. After a reported numerical pass, measure
end-to-end batch time including uploads, report downloads and synchronization,
across population sizes, with the same CPU work and explicit GPU memory headroom.
Qualification trace downloads/file writing must be separated from that measurement.
Neither faster execution nor passing this comparison establishes physiological
calibration or Avatar balance/recovery.
