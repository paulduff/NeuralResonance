# Coupled Golgi reference-cell candidate

The completed [channel checks](DNNE_Golgi_Channel_Results_2026-11-06.md) permit
testing free membrane voltage. `GolgiReferenceCell` now couples those channels
to the deposited soma, dendrites, passive axon and recording electrode, with
two calcium pools. No live host or `ModelNeuron` selects this class. It cannot
read or write a brain save and does not change production fingerprints.

## Biological definition

The reference remains Solinas et al.,
[Computational reconstruction of pacemaking and intrinsic electroresponsiveness
in cerebellar Golgi cells](https://doi.org/10.3389/neuro.03.002.2007), with
[ModelDB 112685](https://modeldb.science/112685) revision
`2ffaeca558235d61222240fc74247cab1cd02fe7`. This is a rat-slice reference at 23 C,
using the deposit's uncorrected channel voltage convention and corresponding
reversals. It is not a human Golgi calibration.

The soma is 27 by 27 micrometres, with three 113 by 3 micrometre dendrites
(10 segments each), a 1,200 by 2.4 micrometre passive axon (100 segments), and the
deposited seal/electrode. The cable has 133 capacitive nodes and two massless soma
endpoints. The three dendrites and seal share soma(1); the axon joins soma(0).
The shared endpoint prevents accidentally giving each branch a separate soma
half-resistance. The small deposited electrode/seal capacitances are retained.
Units are pF, nS, pA, mV and ms; membrane-area conversion is explicit.

The soma contains the channel candidate's ionic mechanisms. High-threshold and
low-threshold calcium currents fill separate 0.2 micrometre submembrane pools.
Each pool has the reference first-order removal term. BK and SK read the
high-threshold pool; low-threshold calcium cannot silently activate those gates.
This is an explicit reference bath and reduced calcium pump, without ATP,
perfusion, synaptic input, plasticity or glial supply calibration.

Somatic upward crossings of -20 mV are interpolated and recorded without resetting
voltage or forcing spikes. The reference axon is passive: these are somatic
events, not demonstrated active axonal propagation. No sinusoidal pacemaker,
predefined spike sequence or restart pulse is added. Diagnostic current steps
and conductance removals belong only to this isolated test.

## Numerical method and accounting

Within one declared step, ordinary gates use exponential relaxation and SK uses
its conserved Markov update at starting voltage/calcium. A Hines tree solve then
advances cable voltage by backward Euler with those conductances and starting
Nernst reversals. The two calcium pools integrate their constant-current pump
equation. This sequential coupling is **first order**, not error-controlled or
partition invariant. The study compares 0.015625, 0.0078125 and 0.00390625 ms.

Every step checks finiteness and records whole-cell capacitive charge versus net
injected/ionic charge, nodal Kirchhoff residuals, and calcium influx/removal
balances. Axial current cancels between neighbours. Numerical failure does not
clip voltage, floor calcium, renormalize SK or reset the cell; a failed cell
cannot resume. Successful conservation would not prove an accurate spike time.

## Independent verification

The visible PowerShell window freezes committed source and the pinned reference,
then runs 25 focused cases: 16 existing channel cases, eight new cable/cell cases,
and the solution-manifest check. Builds are beneath frozen `source/artifacts`,
so source-reading tests remain inside the snapshot. The new cases cover geometry,
shared endpoints, injected-charge conservation, passive equilibrium, consistency
of implicit conductances with the earlier current equations, calcium/charge
accounting, and invalid-input rejection.

The clamp matrix is repeated first. The existing SciPy matrix exponential checks
SK2; a separate implementation reading the deposited numeric parameters checks
all 18 ordinary gates at every saved clamp sample (4,536 scalar comparisons).
The known continuous-singularity corrections, bounded HCN fractions, analytic
formulas instead of lookup tables, and deposited unused HCN temperature factor
are shared explicitly. These definitions are not independently proven biology.

The coupled capture has 24 fresh conditions. At each resolution, quiet firing,
+50 pA depolarization, -50 pA hyperpolarization, and removal of persistent sodium,
HCN or SK run for six seconds. Challenges last from 2 to 3 seconds. Two passive
controls inject 25 pA at the soma or electrode from 20 to 60 ms, over 100 ms.
All somatic crossings and quarter-millisecond voltage/calcium samples are saved.
The passive controls additionally save every cable node. Sampling can miss fast
waveform extrema, so per-step extrema and event times are retained too.

Independent passive checks reconstruct geometry from the hashed HOC source and
solve the resulting equations using SciPy sparse LU. The allowed discrete
voltage difference is 2e-6 mV. A continuous BDF reference, after eliminating only
the truly massless endpoints, runs at two tolerance settings; their allowed
voltage difference is also 2e-6 mV. Candidate-versus-continuous errors at each
resolution are retained rather than hidden behind a conservation check.

The active reference reads the deposited channel parameters and advances voltage,
all ordinary gates, all SK occupancies and both calcium pools **together** using
BDF with a sparse Jacobian pattern. It does not use the C# cell's split solver or
gate/SK update code. Each of six protocols runs at rtol 1e-8 and 1e-10, with
separate absolute tolerances for voltage, gates and calcium. Timestep ceilings,
state arrays, crossing times, evaluation counts and occupancy error are retained.
Its own tolerance comparison requires matching counts, ordinal timing difference
below 0.01 ms, and sampled soma voltage difference below 0.05 mV. A failing
reference is reported as unresolved, not treated as an exact oracle.

The candidate's first-20-ms and full-trace voltage errors, calcium errors, spike
counts and ordinal differences are measured against the stricter reference.
Equal counts do not establish equal trajectories; ordinal comparison alone is
not a calibrated spike-distance metric. These measurements guide the next solver
repair/refinement. No active numerical or physiological qualification is granted
automatically. Native NEURON reproduction and physiological comparisons remain
separate requirements; the current reference is an independent solver for the
declared corrected analytic equations.

## Preservation and next gate

`tools/prepare-golgi-channel-verification.py --cell` creates a unique committed
source package. Its existing visible-window launcher selects the cell protocol
from that frozen configuration. It records/verifies source and binary hashes,
retains logs/TRX/CSV/JSON/NPZ, backs the capture up to D:, and leaves the window
open. No live monitoring is performed. Existing brain saves are never loaded.

Development Release builds passed without warnings/errors. Verification results
are pending the operator's report. Review free firing, challenge/recovery,
channel-removal effects and numerical accuracy before adding this population to
the granule layer. That circuit will still need real granule/Golgi contacts,
appropriate synaptic release, metabolic support and output-event identity.
The timing comparison remains a separate frozen run. Neither study qualifies
whole-brain balance or Avatar recovery.
