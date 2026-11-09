# Completed olive batch, motor arithmetic and Golgi components

The operator reported all three pending windows complete. Reviews read saved
evidence and recompute its comparisons; they do not integrate a new trajectory
or execute a GPU. Both original captures and their D: backups are retained.

## Olive CPU batch

`olive-batch-20261109-11018c`, revision
`a723d71f92bd3a0bb5a424cb5123f28c47429a4b`, completed at 09:28:33 UTC.
All **87 selected tests** pass, covering olive batching, motor replay/transport,
motor calcium, the isometric plant and solution membership. The review verified
all 1,825 files (302,889,955 bytes) in each C:/D: copy, 1,355 packaged files and
the frozen probe/test binaries.

All six cases and twelve serial/parallel contrasts match the original membrane
**byte for byte**, across every cell at every saved frame. Cases contain 1, 32
or 192 cells, each at maximum membrane steps 0.025 and 0.00625 ms. Every trace
contains the initial state and 1,024 subsequent frames, with explicit identities,
per-cell clocks, state, current/load metrics and spike counters.

The one-cell cases have no spikes; the 32-cell cases each retain 26 soma and 439
axon events, and the 192-cell cases each retain 160 soma and 2,762 axon events.
These counts agree across implementations. Equality at both resolutions does
not establish independent solver convergence or physiological calibration.

This clears the CPU layout gate for a CUDA membrane candidate. It does not
measure speed, integrate receptors/metabolic feedback or qualify distributed
spike delivery. The original membrane remains the CPU reference. GPU comparison
must preserve cell identities, clocks, all state, event counts and numerical
failure handling; end-to-end timing and biological accuracy remain separate.

## Motor-cell fixed-state arithmetic

`motor-precision-20261109-7703bd`, revision
`dc3a789be10fde11c194c74fd3a1865dffdf39b7`, completed at 09:56:04 UTC.
All 396 selected physical states were accepted; none was clipped or rejected.
The review verified all 1,534 files (579,183,354 bytes) in each C:/D: copy,
1,408 packaged files and binaries. It reproduced the selection, archived errors,
reference RHS values and 80/120-digit arithmetic comparisons exactly.

At the same supplied states, C# and Python have **identical calcium derivatives**.
Their largest voltage derivative difference is 7.10543e-15 mV/ms and largest
gate derivative difference is 2.44249e-15/ms. Their calcium reversal constants
are also identical in binary64. Canonical geometry comparison detects small
coupling-rounding differences: maximum C# error 1.27694e-14 nS and Python error
4.11911e-14 nS; the declared geometry itself is unchanged.

For the two scenarios that failed the earlier refinement gate, the largest
local calcium arithmetic bias multiplied by the 60 ms removal time is
2.00789e-22 mM (`soma-1000`) and 1.10112e-19 mM (`without-fast-sodium`). Those
are diagnostic scales, **not accumulated trajectory error bounds**. They do
not explain the earlier calcium refinement differences by themselves. Neither
an integration-roundoff mechanism nor amplification of voltage/gate differences
has been established. The original motor study remains FAILED under its original
limits; no physiological motor source is qualified. A future diagnosis must
retain full solver state and separate local stepping from long-trajectory drift.

## Golgi coupled-solver components

`golgi-sdirk-components-20261109-e347de`, revision
`160656d36da113252fc23474cbf2c31e49bc3a3b`, completed at 10:13:05 UTC.
All **32 component tests** pass: seven new coupled-solver cases, eight original
cell/cable cases, sixteen channel cases and the solution-manifest check.
The review verified all 1,763 files (89,582,509 bytes) in each C:/D: copy and
the packaged source and frozen binaries.

This permits the already prepared full comparison in
`DNNE_Golgi_Coupled_Comparison_2026-11-09.md`. The component results establish
neither six-second timing accuracy nor physiology. The full window repeats
the component cases and includes the new trace-export assertions before running
its 30 conditions. No live host selects this solver.

## Review artifacts and continuation

`tools/review-olive-and-motor-20261109.py` produced
`artifacts/olive-motor-review-20261109` (three review files plus verified D:
manifest). `tools/review-golgi-sdirk-components.py` produced
`artifacts/golgi-sdirk-components-review-20261109` (one review file plus verified
D: manifest). The analyzers now expose read-only audit functions, so reviewing
a completed capture does not overwrite its original analysis file.

While waiting for these reports, source inspection traced the cortical precision
integration boundary. `ModelNeuron` already stores receptor currents as doubles,
but `ApplyLearnedInboundStrength` combines float contact weights into a new float
message and bypasses differences below 1e-6. That multiplication is local to the
receiver: a double local efficacy argument can preserve the learned change
without changing the transmitted presynaptic release field. Contact ownership,
per-cell postsynaptic history and warm-start persistence must preserve the same
double state; float diagnostic mirrors must never become authoritative state.
No cortical integration code was changed during this review. Its reduced learning
biology and the original consolidation assertion remain unresolved.
