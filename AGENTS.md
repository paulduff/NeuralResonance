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
