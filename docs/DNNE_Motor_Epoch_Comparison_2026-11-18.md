# Saved motor partition comparison

The operator reported the final FAILED footer and verified D: backup for
`rubral-refinement-20261118-3d8835`: 583,562 files plus manifest. All five native
conditions completed (95,988 physical steps), and the recorded 287 focused
tests passed. The numerical analysis stopped at its first cross-run event
comparison, before assessing the new trajectory errors. Completion does not
establish numerical accuracy or physiological motor control.

## Cause and correction

`RubralNativeBodyStudy.RunAt` creates a fresh session for each condition.
`RubralMotorCircuit` includes that session in the motor projection and synapse
identities. `MotorAxonContact.Create` includes the projection in the endplate
contact identity. The original analyser nevertheless required identical raw
contact IDs between fresh sessions. The six saved initial/topology records
confirm distinct sessions and contacts for the same declared motor neuron.

The new `rubral_epoch_comparison.py` validates each epoch's own projection,
synapse, contact and message IDs, then requires exact agreement in physical
source/target, event order, arrival time and conduction delay. No time tolerance
is introduced. Every original ID is retained in the mapping proof. The existing
full source, packet and native endpoint audits remain mandatory; mapping alone
does not establish complete delivery. This is an isolated experiment comparison,
not permission to change identities inside a continuing brain or checkpoint.

Twelve prospective controls include a valid fresh session and rejection of
cross-session contact/message substitutions, neuron/muscle/route/clock/delay
changes, one-ULP arrival changes, missing events, reused sessions and damaged
original identity. Their purpose is to test comparison admission. Independent
source identity validation remains in the preserved native-record audit.

## Preservation and execution

`rubral_epoch_gate.py prepare CAPTURE --operator-reported-complete` freezes a
new package from clean, pushed source. It requires the reported completed
original backup, exact failure/revision/runtime and unchanged numerical bounds.
The visible `start-rubral-epoch-window.ps1` performs the complete C:/D: original
file audit, source/binary verification, identity controls, and all five saved
native comparisons. Three existing independent references are reused. No neural
simulation, Unity player or reference ODE is started. Full original verification
will take time because of the file count; its window prints progress.

All mapping proofs and the corrected analysis go to the new package. The failed
original capture and its D: backup remain unchanged. Preservation is rechecked
and the new result receives its own verified backup, including on failure.
The original trajectory bounds, repeat and refinement criteria remain unchanged.
No full-direction, upstream neuronal accuracy, physiology or Avatar recovery
qualification follows from a successful strong-left comparison.

Authoring checks passed Python AST parsing, PowerShell parsing and diff hygiene.
No prospective control or model has been executed inline. At this authoring
checkpoint, the complete 583,562-file audit and corrected numerical outcome
remain pending in the new visible workflow.
