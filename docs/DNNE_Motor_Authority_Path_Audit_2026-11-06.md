# Motor authority path: neuronal activity is not yet an axonal motor pathway

This source audit follows the continuing neuronal-development mandate. Sources
were inspected at `d683601`. No live process was queried and no running experiment
was monitored. The existing Avatar implementations were inspected before
proposing changes. These findings refine the development priorities; they do not
change live control in this increment.

## Actual paths

| Stage | Owning source | Observed operation |
| --- | --- | --- |
| Structure dynamics | `Structures/_SharedRuntime/StructureEngine.cs` | Individual ModelNeuron firings create outbound SpikeMessages with cell, instance, projection and event identity. |
| Host sampling | `ControlProgram/Program.cs`, TickHostedService | Processed structure snapshots enter NeuronalMotorPopulationWindow; NeuronalMotorPopulationDecoder runs with snapshots and previous decoded state. |
| Action selection | `ControlProgram/NeuronalActionSelection.cs` | Scores 38 action channels, adds persistence/inhibition biases, ranks scores and chooses a winner using programmed thresholds. |
| Motor readout | `ControlProgram/NeuronalMotorControl.cs` | Averages weighted structure firing rates, calculates support gains, selects effector/posture drives, applies persistence, a righting latch and smoothing. |
| Original Avatar bridge | `src/NRE.SimAvatar/AvatarNeuronalMotorBridge.cs` | Removes original traffic from catalogued motor structures. Converts drive magnitudes into newly generated population/effector events, with new pseudo-neuron names and wall-clock timestamps. |
| Unity input | `UnityWorld/Assets/NRE/Runtime/WorldController.cs` | Uses MotorFrame drive fields directly. Checks freshness/authority, advances the population spinal rhythm and calls the muscle plant. It does not use the original Avatar's pseudo-spike bridge for this step. |
| Spinal rhythm | `src/NRE.SimAvatar/AvatarSpinalLocomotorCircuit.cs`, generated Unity `SpinalCircuit.cs` | Four population-rate variables, reciprocal terms, adaptation and plantar input. Explicit time-in-swing/dominance limits suppress prolonged swing. No individual spiking interneurons or contacts. |
| Unity target mapping | `UnityWorld/Assets/NRE/Runtime/MusclePlant.cs` | Converts drive and stand values into bounded joint targets and recruitment/tone. Leg swing multiplies spinal cycle values by descending drive. |
| Joint excitation | `src/NRE.SimAvatar/AvatarMuscle.cs`, generated Unity `MuscleCore.cs` | Computes error from target minus measured angle. Chooses flexor/extensor excitation from error and velocity signs using fixed gains. |
| Muscle mechanics | Same muscle core; Unity `MotorUnitPool.cs` | Activation, force-length/velocity, fatigue and recovery turn excitation into force. Unity applies measured joint torque and physics; the canonical legacy Avatar also integrates its own joints. |

Both motor paths are **derived from neural population activity**, but neither is
an identity- and timing-preserving motor-axon-to-muscle pathway. The distinction
matters even when the world says “DNNE authority.” That transport/ownership gate
does not prove that the intervening behavioural controller is neuronal.

## Concrete consequences

### Event identity and timing

The original bridge's `AppendPopulationEvents`, `AppendPostureEvents` and
`AppendSignedEffectorEvents` round a drive magnitude into an event count. Identities
such as `population:l:excitatory:<tick>:<i>` and
`effector:<name>:<polarity>:<tick>:<i>:n<count>` describe generated codes, not
identified motor neurons. They do not preserve the source SpikeMessage identity,
presynaptic firing time or a neuromuscular contact. Increasing the number of these
transported events cannot restore that missing biological information.

Unity bypasses those pseudo-events but still receives the same derived motor
drives. Its muscles' angle-error controller is a separate non-neuronal conversion
between drive and excitation. This remains true even when root movement is purely
physical and no poses are teleported.

### Balance readout

`VestibuloReticularPopulationDecoder.Compose` calculates:

```
balanceError = max(0, meanVestibularHz - .55 * meanVermisHz - .25 * meanSpinalHz)
```

The corresponding StructureEngine diagnostic also uses this formula. This is a
weighted difference of firing rates, not measured balance error or an identified
neuronal error population. It discards the signed sensory channels and can be
zero when vestibular activity is substantial but the other structures are tonic.
The motor decoder compares it with thresholds such as .40, .18 and .65, and uses
it in a term `1 - BalanceError`. There is no demonstrated calibration making these
raw-Hz and nominally unit-interval uses equivalent. The previously completed
connected study observed zero error in all 12,800 samples; see
`DNNE_Connected_Circuit_Results_2026-10-30.md`.

Do not fix this merely by taking an absolute value, lowering a threshold or
subtracting a fitted baseline. A biological replacement requires opponent
vestibular channels, actual inhibitory/excitatory contacts and downstream cells
whose activity has a measured relationship to the relevant sensory perturbation.
Ground-truth body tilt/COM/support information belongs in validation and receptors,
not as a privileged non-neuronal control shortcut.

### Hand-written selection and reflexes

Righting-latch recovery after four sampled ticks, timer-based action persistence,
hard winner selection and the joint error/velocity controller are behavioural
approximations. Fixed source-to-target anatomy, transport freshness checks and
force/length laws have different roles and should not be removed indiscriminately.

In particular, preserve the existing vestibular labyrinth, muscle/plant physics,
plantar and proprioceptive transducers. The analogous spinal stretch/withdrawal
path needs to be represented by afferents, interneurons and motor neurons; calling
an arithmetic error term “reflex” does not establish that circuit.

## Replacement requirements

The follow-up source check at `825d17f` confirms that
`Structures/SpinalCordMotor/Program.cs` selects the generic Izhikevich/STDP
profile. `ModelNeuron` uses its shared reduced compartments, spike threshold,
voltage reset and adaptation increment for that branch. No dedicated calibrated
alpha-motoneuron membrane is selected there. A reduced model is not disqualified
merely by being reduced, but this source does not yet establish the required
motor-cell recruitment, afterhyperpolarization, inhibitory response and recovery.
The new axon transport and muscle candidate cannot establish those source
properties. Qualify them explicitly before treating the joined path as neuronal
motor authority.

1. Define and instantiate the relevant excitatory/inhibitory spinal interneurons
   and cholinergic motor-neuron populations, with explicit body targets. Preserve
   separate agonist and antagonist populations. A negative drive value must not
   stand in for inhibitory skeletal neuromuscular transmission.
2. Preserve each real output event's session, source instance/cell, projection,
   contact and firing time through an explicit motor-axon interface. Do not
   generate a new spike train from population means. Define ordering, replay,
   dropped-event and neural/body-clock handling before live transport.
3. Couple motor-axon events to an identified motor unit's excitation/contraction.
   A twitch response is a physical transduction model, not a policy. Start with
   an independently checked isometric reference before applying dynamic muscle
   force-length/velocity/fatigue effects; avoid double activation or fatigue.
4. Route measured stretch/velocity/load through the existing receptor path into
   actual spinal neurons. Replace the angle-error controller and phase-duration
   gates with those circuits. Keep the old path explicitly labelled while the
   candidate is isolated; do not silently mix the paths in one qualification.
5. Move competition, persistence and protective inhibition into their appropriate
   connected neuronal populations. Restrict the final decoder to declared motor
   projections, observational telemetry and explicit transport/ownership checks.
6. Test the actual coupled path with source silencing, contact disconnection,
   agonist/antagonist perturbations, batching/delay controls, load changes and
   fatigue/recovery. Trace force back to the contributing motor-axon events.
   Then assess posture, fall recovery and movement with held-out disturbances.

The published isometric twitch component of
[Fuglevand, Winter and Patla (1993)](https://doi.org/10.1152/jn.1993.70.6.2470)
is a possible muscle reference, not a replacement for DNNE neurons. Its stochastic
rate-based recruitment generator must not be imported as neuronal motor authority.
No new axon interface or twitch model was implemented by this audit. The later
isolated event-transport candidate is described in
`DNNE_Motor_Axon_Transport_2026-11-06.md`. It preserves original discharge identity
and time through explicit contacts/clock epochs, but no live neuronal publisher
or muscle consumer selects it yet. The coupled pathway remains unqualified.

The subsequent isolated muscle candidate is documented in
`DNNE_Motor_Unit_Calcium_2026-11-06.md`. It uses the slow-unit calcium/binding
equations of Nagamori et al. (2021), replacing the earlier possible reference
choice with an explicit release/binding/activation component. It accepts
identified arrivals and produces isometric unit force; no live plant selects it.
Its [completed numerical study](DNNE_Motor_Unit_Calcium_Results_2026-11-06.md)
passes the 46 required unit cases and all 21 numerical conditions. A complete
neuronal source, neuromuscular junction, dynamic plant and spinal reflex circuit
remain open. The isolated [motor-cell reference](DNNE_Motoneuron_Reference_2026-11-06.md)
now supplies a candidate five-compartment conductance model; its initial
verification covers channels and passive coupling, not yet active firing.

## Editor and migration implications

When promoting the replacement, update both Avatar implementations and their
tests, the Unity source generators, and the editor's authority/activity display.
Distinguish real emitted axon events, population-derived commands, transport
eligibility and physical performance. The current UI phrase “no scripted balance
or gait” is too broad to establish the cell-level claim requested by the operator.
The world must still start paused and the operator must initiate Play.

This work is complementary to cerebellar development. Better Golgi/Purkinje
mechanisms cannot by themselves repair this downstream identity and authority
break, so the motor-path replacement is a required workstream, not an optional
visual or naming improvement.
