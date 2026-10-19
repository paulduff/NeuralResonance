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
