# DNNE, Avatar and World: compute direction

## Operator direction

On 9 November the operator directed development toward DNNE, Avatar and World,
and away from continued LM/Entity and Dyad development. Preserve that work and
its saves; it is parked, not deleted. A language model or Dyad interaction is no
longer an acceptance prerequisite for the current brain/body/world objective.
CIV remains a separate experiment. The editor remains the observation and
diagnostic interface for this work.

The operator proposed using CUDA to take advantage of this machine. Adopt
GPU acceleration as an engineering direction, with implementation selected by
measured cost and accuracy. This is not evidence that an existing cell or circuit
is physiologically qualified, or a reason to enlarge every population first.

## Inspected platform and source

Read-only hardware inspection on 9 November reported:

* NVIDIA GeForce RTX 5060, 8,151 MiB reported device memory, driver 616.92.
* Intel Core i9-9900K, eight cores and sixteen logical processors.
* 31.9 GiB physical RAM.
* `nvcc` was not found on PATH. This does not establish that no CUDA toolkit or
  runtime is installed elsewhere; inspect installations before choosing tooling.
* The Unity project records version 6000.3.19f1.

`StructureEngine` currently advances individual `ModelNeuron` objects in its
receiving-cell loop, with local plasticity and spike generation interleaved.
A targeted search of Structures, ControlProgram, NRE.SimAvatar and Unity's NRE
runtime found no CUDA/ILGPU/ManagedCUDA/ComputeShader neuronal backend. The CUDA
setting found in `EntityLanguageClient` selects the separate language service;
it does not accelerate DNNE. This is a source inspection, not a timing profile.
No benchmark, experiment or outstanding-run monitoring was performed here.

## Recommended implementation sequence

1. Establish saved timing and memory measurements for the existing neuronal
   calculation, synaptic delivery, service transport and body stepping. Record
   biological time advanced per wall second, population/contact counts, queue
   delays and numerical settings. Avoid interpreting service health as throughput.
2. Separate numerical state from per-cell managed objects at a narrow execution
   boundary. Keep a CPU reference implementation. Group compatible cell models
   into batches with explicit cell/contact identity; nuclei remain biologically
   distinct even when they share a compute device.
3. Implement a CUDA execution path for a measured expensive batch. Keep its state
   resident on the device across steps where practical; exchange timestamped
   inputs and emitted events in batches. Avoid one transfer or kernel launch for
   each neuron, spike or separately hosted nucleus. Select native CUDA versus a
   .NET-compatible backend only after checking installed tools, RTX support,
   double precision support and the ability to control numerical operations.
4. Compare identical CPU/GPU inputs and initial conditions before promotion.
   Preserve actual event times, source/target/contact identities, local chemical
   state, learning and accounting. Predeclare state and event tolerances, verify
   refinement and record end-to-end speed including copies and synchronization.
   Do not change a failed bound retrospectively or require bitwise identity where
   the arithmetic does not support it. Start from required precision, not a
   low-precision performance target.
5. Integrate passing batches into the brain/body path and then expand only where
   measurements justify it. Candidate motor-unit calcium calculations may also
   be batched, but preserve Avatar's existing receptor, muscle and tendon/body
   interfaces. CUDA calculations do not automatically move Unity articulations
   and collision handling to the GPU. Profile and improve those separately.
6. Demonstrate the closed loop in World: sensory input, neuronal motor events,
   muscle response, posture under disturbance and recovery after falls. The world
   opens paused so the operator can watch. Track these outcomes alongside compute
   improvements; GPU microbenchmarks alone do not advance the usability gate.

The same RTX must also serve visualization. Its 8 GB is a capacity constraint,
not the amount of memory available exclusively to DNNE. Establish headroom from
measurements before increasing neuron/contact counts. Cluster work can distribute
the same explicitly identified populations later; do not assume GPU memory is
pooled or that more nodes remove biological-time synchronization requirements.

## Existing correctness work still required

Friday's cortical precision result passed its isolated numerical tests, but
double state is not yet integrated through actual cells and persistence. The
float storage and small-change transmission bypass remain relevant to any CPU or
GPU backend. Preserve the reviewed reference and address these boundaries during
integration. Faster execution cannot establish correct learning by itself.

The two experiment reports listed in `DNNE_Continuation_2026-11-06.md` were
confirmed finished by the operator on 9 November and reviewed in
`DNNE_Compute_And_Cell_Results_2026-11-09.md`. The motor-cell run still fails its
original refinement gate; the Golgi run was interrupted before its final solve.
A source whose numerical/physiological gates are open stays unqualified on the GPU.

All tests, comparisons and benchmarks continue in visible independent PowerShell
windows, without live monitoring. Preserve old saves and verified D: backups;
commit/push coherent changes. Update Avatar, Unity parity and editor diagnostics
when their behavior changes. No live runtime, model parameters, saves or services
were changed by this direction document.

## Engineering references

[NVIDIA's CUDA best-practices guide](https://docs.nvidia.com/cuda/cuda-c-best-practices-guide/index.html)
supports profiling before porting, reducing host/device transfers, batching small
transfers, and explicit CPU/GPU numerical comparison. These are design constraints,
not a prediction of DNNE's speedup.

[Unity's physics CPU optimization guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/physics-optimization-cpu.html)
describes profiling and CPU-side physics costs. A separate DNNE CUDA backend does
not constitute a Unity physics migration.
