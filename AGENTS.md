# Avatar and environment work

DNNE's biological mechanisms are a correctness requirement, not a naming or
presentation theme. Learning must be implemented at individual synaptic contacts
using the actual connected neurons' activity, biological timing, appropriate
cell/circuit-specific plasticity and locally arriving modulation. Preserve stable
cell/connection identity across processes and checkpoints. Do not describe a
pathway-shared weight, a sender-only postsynaptic proxy, service health or visible
movement as evidence of biologically equivalent Hebbian learning. Use mechanistic
tests and explicitly state unvalidated approximations. The current foundation
review is `docs/DNNE_Learning_And_Structure_Review_2026-10-19.md`.
Its implemented contact/event repair and remaining acceptance work are recorded in
`docs/DNNE_Biological_Foundation_Repair_2026-10-19.md`. Schema-4 saves are learned-state
warm starts, not exact brain continuation. Preserve incompatible legacy saves;
use explicit isolated state for fresh experiments, never a silent schema upgrade.
The subsequent cell/timing repair is documented in
`docs/DNNE_Cell_Dynamics_Repair_2026-10-30.md`. Its dynamics version changes the
model fingerprint; preserve earlier schema-4 saves too. HH is now a reference
dynamic-gate membrane, not a physiologically calibrated human cerebellar cell.
The completed comparison and biological pacemaker clock correction are recorded
in `docs/DNNE_Cell_Comparison_And_Circuit_Return_2026-10-30.md`; the clock version
also participates in the save fingerprint. The next 14/20-instance comparison
is isolated and does not qualify whole-brain balance or physical recovery.
Its completed results are in `docs/DNNE_Connected_Circuit_Results_2026-10-30.md`.
Reserve/fatigue and potassium clamps in the subsequent cell probe are artificial
diagnostic interventions only; they are not approved replacements for biology.
Their completed causal findings and energy/recovery repair requirements are in
`docs/DNNE_Support_State_Diagnosis_2026-10-30.md`. Native quiet firing collapses;
held energy restores sustained activity but does not establish recovery after
inhibition. Do not promote the diagnostic clamps or pulse into live control.
The substrate-limited energy and channel recovery candidate is documented in
`docs/DNNE_Energy_And_Recovery_Repair_2026-10-30.md`. Dynamics v3 invalidates older
model fingerprints without modifying their saves. Normalized energy, reduced
Avatar supply proxies and hybrid membrane parameters remain uncalibrated.
Missing/stale live body supply must remain visible; isolated probes use an explicit
reference bath. Channel ablations are diagnostic only. Do not confuse successful
accounting or recovered firing with a calibrated olive or physical balance.
The completed 120-condition findings are in
`docs/DNNE_Energy_Recovery_Results_2026-11-02.md`. Candidate cells sustain energy and
recover, but recovery survives removal of both added channel mechanisms at the
higher drive; the lower drive still loses Purkinje recovery. Do not attribute
success to HCN alone. The fast tonic olive remains uncalibrated; establish its
cell-specific physiology before interpreting connected LTD or physical recovery.
The v4 olive candidate is documented in
`docs/DNNE_Olivary_Calibration_2026-11-02.md`. It uses a three-compartment reference
with endogenous oscillations, per-contact receptor conductance and explicit
somatic/axonal spike accounting. Do not reinstate a tonic sine-wave drive or demand
spontaneous spiking from every isolated olive cell. Evoked responsiveness, slow
oscillations, timing, inhibition and supply recovery must be assessed separately.
Reference conductances and input-density conversion are not fitted human anatomy;
gap junctions and connected teaching remain unqualified. Preserve v3 saves too.
The completed 180-condition findings are in
`docs/DNNE_Olivary_Results_2026-11-02.md`. The reference olive oscillates near 8.2 Hz
without tonic spikes, responds to stronger input and restores its baseline waveform
after challenges. All outer-step traces match. CaT removal abolishes oscillation;
HCN removal does not. Restored oscillations are not proof of recovered evoked output:
test matched input before/during/after challenges before connected qualification.
The weak paired cases cannot rank output timing; correct their unequal endpoint
counts in a future matched comparison. Do not overwrite the completed package.
The next matched response rig is documented in
`docs/DNNE_Olivary_Response_Recovery_2026-11-02.md`: fixed production channels,
identical four-contact probes before/during/after challenges, matched sham,
rest-during-challenge controls, complete windows and preserved off-window spikes.
Assess recovered evoked output and inhibitory sensitivity before connecting the
cerebellar loop. These diagnostic probes are not a live restart mechanism.
The completed matched response findings are in
`docs/DNNE_Olivary_Response_Results_2026-11-02.md`. Late outgoing responses return
to 95–100% versus 95% in matched sham, with matching outer-step traces. Stronger
inhibition roughly halves burst output but does not monotonically reduce hit
probability. Brief delayed spikes must remain visible; no off-window activity
persists into late recovery. Next assess actual olive-to-Purkinje event delivery
and per-contact learning in an isolated circuit, preserving all axonal events.
The operator subsequently authorized the calcium learning change described in
`docs/DNNE_Purkinje_Calcium_Candidate_2026-11-02.md`. Purkinje cells now have an
explicit climbing AMPA compartment and contact-local voltage/IP3/calcium LTD.
Its reference-inspired parameters, passive electrical reduction, peak-induction
approximation and missing LTP remain unqualified. Do not call it a complete
Purkinje model or assume it is better because it contains calcium. The isolated
128-condition comparison uses matched legacy learning and entry/release ablations,
all real olive axonal events, fresh state, and no live monitoring. Old Purkinje
saves have incompatible fingerprints and must stay preserved. Other nuclei's
learning and the unresolved connected decoder issue are not repaired by this change.
The completed 128-condition review is recorded in
`docs/DNNE_Purkinje_Calcium_Results_2026-11-02.md`. The candidate's stored-weight
effect depends on calcium entry and gains contact contrast from internal release,
but is tiny and includes dormant-contact depression. Retaining only the maximum
historical calcium dose can obscure later teaching; simply waiting longer or
increasing a learning multiplier is not a demonstrated repair. The connected
host also has outer-step sensitivity upstream of learning. Next establish event
and voltage/calcium convergence, then calibrate local dose-to-LTD and repeated
teaching. The agreed sequence is Purkinje learning, fastigial/vestibular integration,
reliable balance readout, then Avatar recovery; this result does not yet qualify
advancing to learned fastigial/postural control. Preserve all completed evidence.
The subsequent timing repair is documented in
`docs/DNNE_Connected_Timing_Repair_2026-11-02.md`. Arrival boundaries no longer
act backward over elapsed intervals; calcium uses start/end voltage. Integration
version `event-aligned-calcium-v1` invalidates older StructureEngine fingerprints,
without modifying their saves. The isolated 120-condition numerical study varies
host packet cadence independently of solver resolution, retaining fractional
arrival times and all olive events. Exact packet invariance is not a convergence
proof. Review successive refinements before fitting learning strength or replacing
the maximum-history induction approximation. Physiology and balance remain open.
The completed timing findings are in
`docs/DNNE_Connected_Timing_Results_2026-11-02.md`: all 60 packet-cadence pairs
match exactly, but finer solvers change olive burst counts substantially and
non-monotonically. The active LTD target is established early, so similar final
weights conceal later source divergence. Do not claim source convergence from
packet invariance, short regressions or similar learning totals. Next isolate
olive numerical accuracy against an independent error-controlled reference and
test Purkinje refinement with one fixed recorded olive train. Distinguish solver
error from nonlinear trajectory sensitivity before fitting biological parameters.
The source/reference and fixed-input replay rig is documented in
`docs/DNNE_Split_Numerical_Qualification_2026-11-02.md`. It leaves production
runtime and biological parameters unchanged. Its 80-condition study separates
source coupling/membrane refinement, an independent Python/DOP853 reference,
initial-state sensitivity and Purkinje replay of exact saved olive events.
The independent reference omits metabolic feedback: compare it directly only
with matched energy-omitted production controls, never as a validation of the
energy-enabled brain. Reference tolerances and diagnostic perturbations need
their own review. Preserved spike identity/timing and complete accounting are
necessary checks, not physiological or long-trajectory convergence claims.
The completed split-study review is in
`docs/DNNE_Split_Numerical_Results_2026-11-06.md`. Fixed-input calcium metrics
improve with refinement, but receiving spike timing remains unresolved. The
independent olive reference agrees closely under inhibition yet diverges later
under paired stimulation; do not declare its strictest long trajectory exact
or infer chaos. Global arrival boundaries split every receiving cell's step,
providing a numerical route for unrelated-event effects. Test no-op boundaries
and complete-state short-interval accuracy before tuning learning. Purkinje
still has provisional imposed drives; endogenous pacemaking is unqualified.
The next causal diagnostic is documented in
`docs/DNNE_Boundary_Timing_Study_2026-11-06.md`: connected/disconnected input and
disconnected input retaining no-op arrival boundaries. Its fresh isolated-only
hook releases no transmitter and is not a production fix. The eight-second
preflight supports the shared-boundary mechanism and reproduces 16 archived
baselines; review the full 60-second result before advancing. The full test suite
has one cortical-consolidation failure also reproduced with the pre-change binary;
do not call the whole suite green or conceal that separate unresolved issue.

Before adding or changing Avatar functionality or its Unity adapter, inspect the
corresponding existing `src/NRE.SimAvatar` implementation, receptor path and tests.
The operator explicitly requires this: **always check Avatar; substantial work
has already gone into it.** Use `docs/Unity_Avatar_Migration_Audit_2026-10-12.md` as
the migration map, and update it when preserving, replacing or omitting a feature.

DNNE is the brain and action authority. Avatar is the body/nervous system; World
and Maze are environments. Dyad/Entity provide interaction. CIV is a separate
experiment. Diagnostic commands/controllers must remain explicitly isolated from
live DNNE operation and must never be reported as neuronal or learned behavior.

The continuing neuronal-development objective and acceptance gates are recorded in
`docs/DNNE_Neuronal_Development_2026-11-06.md`. The operator authorizes justified
missing populations and mechanism repairs, with regular commits and pushes, until
Avatar usability is demonstrated or the operator stops the work. This is not
permission to claim human-brain equivalence or promote unqualified candidates.
The isolated Golgi channel implementation is described in
`docs/DNNE_Golgi_Channels_2026-11-06.md`. It is not wired to ModelNeuron or a host.
Its voltage-clamp/SK2 checks do not establish free-cell firing, a functioning
granular-layer circuit, or human physiology. Review the operator-reported results
before advancing to coupled membrane/cable dynamics; preserve the pinned source
convention and explicitly documented corrections.
The completed channel results are in
`docs/DNNE_Golgi_Channel_Results_2026-11-06.md`: 16 focused tests and 36 clamp
conditions passed, with independent SK2 propagation agreement. This permits
candidate coupled-membrane work, not live Golgi integration. The source-level
motor path audit is `docs/DNNE_Motor_Authority_Path_Audit_2026-11-06.md`; both the
population-derived output and joint error controller require neuronal replacements.
Do not equate ownership/freshness gates or generated pseudo-spikes with preserved
motor-neuron events. The completed boundary-timing result is recorded in
`docs/DNNE_Boundary_Timing_Results_2026-11-06.md`: all eight untouched-cell
contrasts exactly match when only the removed timing boundaries are restored.
Unrelated arrivals can change electrical/support states and even a disconnected
cell's spike count. The isolated repair candidate is described in
`docs/DNNE_Cell_Local_Timing_2026-11-06.md`; it advances each cell at its own real
arrivals and the regular grid. Its tests/comparison are awaiting operator-reported
completion. It is not enabled in service hosts and does not qualify general
recurrent scheduling, arbitrary host endpoints, solver accuracy or physiology.

Tests and experiments run independently in a visible PowerShell window. Do not live
monitor them; inspect saved results after the operator reports completion. Builds
and static inspection can run during development. Live
worlds start paused and wait for the operator to press Play. Preserve existing
brain state and verified D: backups. Commit and push applied changes once stable.
