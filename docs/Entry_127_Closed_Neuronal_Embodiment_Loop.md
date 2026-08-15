# Folded Archive Entry 127: Closed Neuronal Embodiment Loop

## Purpose

Close the causal loop between DNNE, avatar, and world without introducing a
host policy, semantic outcome label, scripted action, or learned ML component.
The world continues to expose physical facts. Neuronal structures alone turn
those facts into valuation, plasticity, action selection, and memory.

## Signed neuronal teaching

The physical body transducer now compares consecutive receptor frames from the
same body source. It derives bounded changes in tissue integrity, stored energy,
hydration, homeostatic deviation, and body motion.

- Rapid tissue loss and terminal integrity excite the habenula. Existing
  habenular GABA projections suppress VTA and SNc dopaminergic activity.
- Genuine restoration of energy, tissue, hydration, or homeostasis excites VTA
  and SNc. Their existing dopamine projections modulate striatal and cortical
  synaptic plasticity.
- Respawning from terminal tissue integrity is not treated as an appetitive
  event, so death cannot reinforce the action that preceded it.
- The public world-to-brain contract remains physical only. It contains no
  reward, fear, pain meaning, target identity, action success, or desired move.

The WorldSim now queues one terminal physical body frame before resetting a
dead avatar. The nervous system therefore receives the zero-integrity event
that the old immediate reset could hide.

## Authoritative action selection

Raw corticospinal population activity can no longer bypass the basal ganglia.
Locomotion and manipulation remain suppressed unless the complete neuronal
action circuit is observed, selects a lane, and exceeds its confidence gate.

The five action lanes remain population codes rather than symbolic commands:

- bilateral locomotor release
- left-biased locomotor release
- right-biased locomotor release
- bilateral reverse population
- manipulator effector population

If striatal, subthalamic, pallidal/nigral, or motor-thalamic evidence is absent,
motor output fails closed at zero.

## Intrinsic electrophysiology

Normally tonic or oscillatory nuclei now receive deterministic intrinsic cell
drive before membrane integration. This is a property of each simulated neuron,
not a host-generated spike or behavior. It covers Purkinje cells, cerebellar
vermis and granule populations, inferior and superior olives, striatum, GPe,
GPi, SNr, SNc, VTA, habenula, ventral pallidum, septal/diagonal-band nuclei,
DMH, and VLPO.

Sparse phase and lane variation prevents simultaneous uniform activation and
preserves competition across basal-ganglia channels.

Focused membrane tests run each covered population for six simulated seconds.
Every population must produce spikes without exceeding 200 Hz per neuron. The
Hodgkin-Huxley current conversion is kept separate from the compact LIF and
Izhikevich current scale.

## Bilateral editor telemetry

The Blazor editor now preserves source and target hemisphere labels from each
dispatch trace. Activity is applied to the matching left, right, or midline
atlas instance, and pathway curves connect those same instances. Missing
hemisphere labels still degrade to an aggregate display instead of hiding data.

All six anatomical presets are available. Anterior views the face of the brain,
posterior views the back, left and right view the named sides, superior looks
down, and inferior looks up. Every preset defines both a viewing axis and an up
vector so superior and inferior views cannot roll unpredictably.

## Embodied curriculum

The previously inert curriculum accumulators now observe neuronal and physical
evidence:

- sensory discrimination measures afferent population coverage and changing
  physical contrast;
- feature binding measures neuronal perceptual and attention-workspace
  coverage;
- action-outcome association requires an authoritative neuronal action plus a
  measurable bodily consequence;
- working-memory stability reads persisted synaptic memory diagnostics.

The perceptual stage can advance to sensorimotor grounding only after sustained
scores and sample counts. Language and abstraction stages are intentionally not
advanced by world evidence; they require their own neuronal criteria.

## Atomic persistence

ControlProgram now has an explicit quiescence barrier. A pause request waits for
the current whole-brain tick to finish, blocks the next tick, and reports zero
active ticks. Network export uses this barrier, so its document is one immutable
neural instant.

The local shutdown sequence is now:

1. Pause and stop the authoritative Blazor WorldSim.
2. Persist an atomic world-run JSON report.
3. Quiesce the whole-brain tick coordinator.
4. Export the network checkpoint while frozen.
5. Stop ControlProgram and let each structure persist synapses during disposal.
6. Use the existing forced process cleanup only as a timeout fallback.

World reports are stored under
`%LOCALAPPDATA%\NeuralResonanceEngine\world-runs` by default. Reports include the
session, physical state, movement, sensory counts, interactions, deaths,
collisions, neuronal dispatch totals, and final entity state.

## Guardrails

- No ML model, classifier, host reward scalar, or semantic outcome endpoint was
  introduced.
- WorldSim owns mechanics and consequences but never chooses behavior.
- ControlProgram coordinates transport and snapshots but cannot invent an
  action when the neuronal selection circuit is absent.
- Neuromodulation still travels through neurotransmitter spikes and local
  receptors; legacy global modulation fields remain inert.
- Reset creates a new physical body but cannot erase the terminal afferent event
  or turn respawn into positive teaching.

## Verification

- The complete solution builds with zero warnings and zero errors.
- All 12 intrinsic circuit populations pass bounded membrane-spiking tests.
- Bilateral dispatch rendering and six-axis view contracts are covered.
- The full suite passes: 524 tests, zero failures, zero skipped.
- Regression coverage includes teaching polarity, respawn exclusion,
  authoritative motor suppression, intrinsic drive, curriculum sampling,
  quiescence blocking, atomic world-report persistence, and binary/JSON spike
  transport compatibility.

## Live qualification, 13 August 2026

The full bilateral stack reached 238 healthy instances across 119 named
structures. Live dispatch samples contained both hemispheres: 524 left and 676
right source events, with the same counts at their labelled targets. The editor
rendered paired bilateral activity rather than collapsing every event onto the
first, normally left, atlas mesh.

The authoritative WorldSim remained brain-connected for 488.9 seconds and
accepted 1,919 left-eye and 1,918 right-eye retinal frames, 3,397 physical-body
frames, and 3,399 somatic frames. It recorded 354 neuronal manipulator
dispatches but no locomotor dispatch, movement, restoration, injury, or death.
This is a useful fail-closed result: the host did not manufacture movement when
the basal-ganglia circuit did not release a locomotor lane. The next run must
determine why manipulation dominated while the locomotor lanes remained silent.

One startup spontaneous dispatch encountered HTTP 415 before subsequent ticks
recovered. Inspection found a real compatibility mismatch: ControlProgram can
fall back to JSON, while the structure endpoint advertised binary only and the
single-spike handler parsed binary only. Both spike endpoints now accept and
parse binary or JSON. The last live transport tick had zero dispatch errors;
the compatibility regression passes.

Graceful shutdown persisted world report
`world-run-7bed74e8c0534afca5785d13e7ee883b-0000013303-stopped-20260813T135945310Z.json`
and atomic checkpoint `last-graceful-network-state.json` at brain tick 1,048.
The differing world tick, 13,303, is expected because world physics and neural
simulation clocks have independent rates.

## Next live criterion

Run the full bilateral brain and authoritative Blazor WorldSim long enough to
observe a selected Go lane, movement, food or water restoration, predator
injury, and a clean shutdown. Confirm non-zero habenular activity after damage,
phasic VTA/SNc activity after earned restoration, non-silent basal-ganglia and
cerebellar populations, advancing curriculum samples, a stable checkpoint tick,
and a persisted world-run report.

## Selected-lane motor gate correction, 13 August 2026

A later live run exposed a contradictory motor state after physical reset. The
action-selection decoder continued to observe complete circuitry, sufficient
confidence, changing selected channels, and lane-specific GPi/SNr release, but
the motor decoder reported `selectionGate=0` and `outputInhibition=1` for every
sample. Motor dispatch consequently decayed to zero even though the brain was
awake and long past startup.

Two incompatible measurements had been combined. Action selection used each
candidate lane's normalized GPi/SNr activity, while the coarse motor gate
averaged raw firing rates from all basal-ganglia output populations. Tonic
activity and inhibited competing lanes therefore saturated that global value
and vetoed the selected lane.

Basal-ganglia diagnostics are now normalized onto their documented bounded
activation scale. When an action circuit is available, motor release is derived
from the selected lane's own output-nucleus inhibition; the coarse aggregate is
retained only as a fallback when lane diagnostics are absent. A selected lane
that is genuinely inhibited still suppresses motor output.

Regression coverage recreates the exact failure: globally high output-nucleus
activity alongside a disinhibited selected lane. The selected lane now remains
authoritative and produces motor drive. The focused suite passes 31 tests, the
complete suite passes 524 tests, and the Release solution builds with zero
warnings and zero errors.

A clean integration run confirmed that the formerly pinned telemetry changed
from `selectionGate=0, outputInhibition=1` to
`selectionGate=1, outputInhibition=0`. During the short observation window the
new network had not yet produced a positive action proposal, so no lane was
selected and WorldSim correctly remained stationary. This is neuronal
suppression rather than the repaired global-gate veto; no host movement fallback
was introduced.
