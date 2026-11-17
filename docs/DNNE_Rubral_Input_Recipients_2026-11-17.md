# Rubral input recipient diagnosis and explicit wiring candidate

## Completed experiment

The operator reported `rubral-motor-20261117-844975` complete. The separate
`rubral-motor-review-20261117` verifies all 2,360 C:/D: files, frozen source,
executable manifests, all 136 focused cases, the managed runtime and the saved
analysis. All 48,000 source frames and twelve motor branches reconcile. No model
or test was rerun during review. The original failed output-lifecycle capture
`e42efc` remains unchanged.

The quiet condition emitted zero source events. Quarter, unit and four-quanta
fixtures emitted 261, 1,178 and 5,710 original rubral events, respectively. They
came from 155, 172 and 172 source cells and reached 107, 119 and 119 declared
outgoing target labels. **None was addressed to the selected motor target**,
`auto-SpinalCordMotor-002`. Its sole presynaptic cell, `n-081`, showed zero
synaptic current and zero emitted events in every condition. All twelve motor
branches consequently received zero input and emitted zero motor events. This
experiment did not test an evoked motor response or its learned strength.

## Setup error, not demonstrated structural plasticity

The follow-up `rubral-input-routing-review-20261117` checks the saved receiving
contact states against all three completed input fixtures. Each has 256 contacts
and 5,120 original input events, but only **172 actual recipient cells**. There
are 101 singly innervated, 58 doubly innervated and 13 triply innervated recipients
under this fixture; 84 cells receive none. All three drive strengths have exactly
the same contact/recipient mapping. No named target index matches the actual
recipient in this particular fixture. The event labelled for `n-081` is received
by `n-034`; no contact reaches `n-081`.

The experiment's assumption was wrong: these legacy target labels are routing
hints, not assured literal cell addresses. `StructureEngine.ResolveInboundNeuronIndex`
delegates this pathway to `BrainstemCranialCircuitKernel`. `ResolveSignalIndex`
prefers the source suffix; `ProjectChannel` applies the existing channel map and
stable jitter. Receiving contacts then belong to that resolved cell. Ordinary
synaptic strength learning can occur there, but the identical recipient mapping
does not demonstrate plastic rewiring. Increasing fixture strength cannot correct
which cell owns the synapse.

Both reviews are separately D:-verified. Rounded population observations are not
complete neuronal numerical trajectories; no upstream physiological conclusion
follows from this diagnosis.

## Simpler explicit connection rule

The operator requested simplification while staying close to biology. The next
isolated experiment gives each incoming synaptic contact one explicit receiving
cell. Strength plasticity retains its existing equations and works on that
contact. Structural rewiring would require an explicit anatomy change, not a
silent reinterpretation of an event's destination.

Reuse `IsolatedSynapticTargets`, already used by isolated cortical studies,
instead of adding a second remapping algorithm. Its constructor guard now permits
fresh, full-size isolated RedNucleus state as well. Public service construction
still supplies no map. Existing live routing and the default isolated rubral
mode remain unchanged. The map participates in the model/save fingerprint;
nonempty state directories are rejected and old saves are preserved.

The declared rubral owner checks every fixture event against the exact contact
map before creating neuronal state. Missing contacts or disagreement between a
named and declared target fail instead of falling back to the legacy mapping.
The receiving engine records original event bytes, receiving contact identity,
named and actual cell, scheduled arrival and actual application time after
receptor integration and before learning. Receipts remain in the immutable
source frame until acknowledgement; failed-step receipts remain available for
diagnosis. A repeated event or an unintended actual recipient faults the source.

This is explicit **fixture anatomy**, not a fitted human corticorubral arbor.
The generic existing rubral Izhikevich/STDP parameters, substep event timing and
their numerical/physiological qualifications remain open. The source's original
outgoing routes and the preselected motor cell stay unchanged. The receptor's
fixed diagnostic conductance is still not learned source efficacy.

## Next visible comparison

`rubral_motor_gate.py prepare-declared` freezes a distinct experiment. It runs
150 focused cases: the original 136, eight existing declared-contact cases and
six new rubral delivery/ownership cases. The four source conditions retain the
same original fixture identities, 1,500 ms horizon, 0.25/1/4-quanta stimuli and
twenty pulses. All four declare the same 256 contacts, including the silent
condition. The motor target, 50 nS receptor, delays, solver and packet/disabled
branches are unchanged.

The audit requires each of the 15,360 active fixture events to appear exactly
once in an actual delivery receipt, with its original bytes and literal intended
receiving cell. Final receiving contact states must agree. Each active condition
must cover all 256 declared cells. The complete quiet population observations
and energy diagnostics must match the preserved prior quiet capture exactly;
this is a saved-observation regression, not full-state numerical qualification.

Source/receiver accounting and actual input delivery are separate from the
`ObservedNeuronalMotorResponse` flag. If correctly delivered input still produces
no motor response, retain that result and investigate the neuronal/contact
mechanism. Do not move the target after observing activity, increase gains to
force a pass or fabricate output events. No live brain, Unity body, muscle,
sensory feedback, balance or recovery is qualified by this experiment.

Authoring builds and syntax/inventory checks execute no tests or neurons. Launch
only clean frozen pushed source with an initial verified D: backup in visible
PowerShell, and inspect output only after the operator reports completion.
