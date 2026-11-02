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

Before adding or changing Avatar functionality or its Unity adapter, inspect the
corresponding existing `src/NRE.SimAvatar` implementation, receptor path and tests.
The operator explicitly requires this: **always check Avatar; substantial work
has already gone into it.** Use `docs/Unity_Avatar_Migration_Audit_2026-10-12.md` as
the migration map, and update it when preserving, replacing or omitting a feature.

DNNE is the brain and action authority. Avatar is the body/nervous system; World
and Maze are environments. Dyad/Entity provide interaction. CIV is a separate
experiment. Diagnostic commands/controllers must remain explicitly isolated from
live DNNE operation and must never be reported as neuronal or learned behavior.

Long experiments run independently in a visible PowerShell window. Do not live
monitor them; inspect saved results after the operator reports completion. Live
worlds start paused and wait for the operator to press Play. Preserve existing
brain state and verified D: backups. Commit and push applied changes once stable.
