# Neuronal development toward a usable Avatar

## Mandate and current limits

On 6 November 2026 the operator authorized continuing brain development, including
anatomically justified missing structures, until the Avatar is usable or the
operator stops the work. Tests run in visible PowerShell windows; the operator
reports completion. Do not live-monitor those windows. Commit and push coherent
increments. Preserve earlier saves and completed experiments, including D: copies.

DNNE remains the brain and action authority; Avatar the body and nervous system;
World and Maze the environments; Dyad/Entity the interaction. CIV is unrelated.
“Neuronal operation” means that sensory integration, learning, selection and motor
commands arise from connected neurons. It does not mean removing glia, substrate
supply, receptor physics, muscles, or transport infrastructure. Those are necessary
supporting mechanisms, not alternative decision makers.

No whole-brain biological equivalence or usable closed-loop Avatar has been
established. The 119 `StructureId` entries are a software inventory, not a count of
qualified biological structures. The current isolated boundary-timing experiment
is awaiting the operator's report; its full result has not been inspected for this
development audit. The separate cortical-consolidation test failure remains open.

## Source-level gaps, in implementation order

These findings describe source inspected at `084c6e2`. Absence of an explicit cell
type in the inspected runtime is distinguished from absence of every possible
functional approximation.

| Priority | Present implementation | Gap and next work |
| --- | --- | --- |
| 1 | `StructureEngine` delivers real synaptic events and stores contact-local learning. The olive and Purkinje probes preserve individual event identity. | Resolve the shared receiving-step boundary effect after the current experiment. Require refinement of voltage, events and contact calcium, not only matching final weights. |
| 2 | `CerebellarGranule` hosts LIF cells; no explicit Golgi population was found in runtime, protocol or structure hosts. | Build Golgi ionic mechanisms, then an isolated granule/Golgi feedforward and feedback inhibitory microcircuit. Qualify it before adding a deployed population. |
| 3 | The Purkinje candidate has climbing input and contact-local calcium LTD. | Endogenous pacemaking, active dendrites, repeated teaching, LTP and molecular-layer basket/stellate inhibition remain missing or unqualified. Do not simply increase the learning multiplier. |
| 4 | Fastigial, interposed and dentate nuclei exist. Vestibular nuclei are one grouped structure. | Distinguish appropriate excitatory and inhibitory cell populations and their output targets; qualify fastigial/vestibular sensorimotor integration. Split vestibular populations only where the connection/function requires it. |
| 5 | Avatar already has a bilateral vestibular labyrinth, proprioception, plantar feedback, muscles and spinal rhythm code. | Preserve these transducers and muscle mechanics. The spinal half-centre currently uses population-rate equations and explicit swing-duration thresholds, not individual spiking interneurons. Replace/calibrate that approximation with connected excitatory/inhibitory spinal populations before claiming cell-level equivalence. |
| 6 | `NeuronalMotorControl` consumes neural populations but also contains threshold decisions, smoothing and a righting latch. | Separate legitimate readout, transport freshness and motor-unit filtering from non-neuronal behavioural selection. Trace actual authority paths; remove behavioural substitutions only with a tested neuronal replacement. The previously observed zero-clipped balance readout remains unresolved. |
| 7 | Somatic/proprioceptive afferents, spinal motor, thalamic and pontine routes exist. | Audit the dorsal-column and spinocerebellar relays. No explicit gracile, cuneate or Clarke population was found in the inspected source; decide which relays are required for the Avatar's actual sensors before extending services. |
| 8 | Cortex, hippocampus, basal ganglia and modulatory structures have named hosts and local learning components. | Audit cell populations, contact specificity and modulation; investigate the cortical-consolidation failure. Language reporting must remain grounded in DNNE state. |

Additional concrete issues:

- `CerebellarCircuitKernel.SelectSpikeType` labels every emitted Purkinje and olive
  event `COMPLEX`. A Purkinje complex spike is not the identity of every Purkinje
  action potential. This label also adds a timing offset in `StructureEngine`, so
  correcting it requires explicit timing tests and checkpoint/version review.
- `IntrinsicCircuitDriveTopology` still supplies imposed Purkinje drive. Activity
  under that drive does not qualify endogenous pacemaking.
- `CorpusCallosum` and `ArcuateFasciculus` are represented as structure services.
  Review whether their implementation treats axonal tracts as invented neuronal
  nuclei. Routing abstractions must not silently introduce extra learning cells.
- No explicit basket, stellate or unipolar-brush populations were found in the
  inspected runtime. Their appropriate local targets matter more than additional
  names in the editor.

## First addition: cerebellar Golgi mechanisms

Golgi inhibition is justified by direct evidence for mossy-fibre feedforward and
granule-cell feedback recruitment: [Cesana et al., 2013](https://doi.org/10.1523/JNEUROSCI.4897-11.2013)
and [Tabuchi et al., 2019](https://pmc.ncbi.nlm.nih.gov/articles/PMC6381242/).
The working reference is the ion-channel model of
[Solinas et al., 2007](https://doi.org/10.3389/neuro.03.002.2007), with deposited
mechanisms in [ModelDB 112685](https://modeldb.science/112685), repository revision
`2ffaeca558235d61222240fc74247cab1cd02fe7`.

Begin with voltage-clamp/channel verification before free membrane dynamics,
passive cable loading, synapses and network integration. The deposit uses a rat
slice reference at 23 C; it is not a fitted human cell. The deposited voltage
convention, numerical singularities and table-domain behaviour must be documented
explicitly. Do not silently substitute another parameter set.

E-GLIF was considered but not selected as the first mechanistic reference. It is
a useful reduced model, but its current lumps and spike reset do not expose the
channel mechanisms needed here. The current
[DBBS implementation notes](https://cerebellar-models.readthedocs.io/en/latest/configurations/mouse/nest/eglif_cond_alpha_multisyn.html)
also identify reproduction and parameter-rounding limitations. This does not
disprove the model; it argues for establishing a channel-level reference first.

## Acceptance gates

1. **Cell:** finite states, physical signs/units, probability and calcium accounting,
   reference voltage-clamp responses, numerical refinement, spontaneous/evoked
   output and recovery appropriate to the specific cell. No externally scheduled
   spikes disguised as pacemaking.
2. **Circuit:** stable source/target/contact identity, causal delayed transmission,
   appropriate excitatory/inhibitory action, dependence on the intended contacts,
   and local learning driven by the actual connected cells. Include disconnection
   and mechanism-ablation controls. Transport batching must not supply behaviour.
3. **Body loop:** actual Avatar receptor signals reach the intended populations;
   descending neuronal events recruit the existing motor-unit/muscle plant.
   Account separately for world time, neural time and stale transport.
4. **Usable Avatar:** sustained closed-loop posture, corrective responses to
   controlled disturbances, recovery after falls, then basic movement and
   grounded Dyad/Entity reporting. Require repeat trials and withheld disturbances;
   predeclare task-specific metrics before observing outcomes. Worlds start paused.
5. **Integration:** inspect/update Avatar and Unity parity, editor anatomy/activity
   mapping, save fingerprints and diagnostics whenever a candidate is promoted.
   A source-only candidate does not appear as a live/qualified structure.

These are engineering acceptance gates for this project, not a claim of human
brain equivalence. The goal remains open until the relevant closed-loop outcomes
are demonstrated. A completed process, service coverage, plausible pictures or
movement alone cannot close it.
