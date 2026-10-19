# DNNE learning and structure review — 19 October 2026

The current DNNE does not meet the requested standard of biologically equivalent
synaptic learning. It contains real spiking cells, receptor currents and persistent
plasticity state, but several causal mechanisms connecting these parts are wrong
or incomplete. Passing service, routing and movement checks did not establish the
biological learning contract. The appropriate next development is a foundational
repair of connection ownership, timing and learning events.

Paul's requirement is explicit: **the biological mechanisms are the purpose of
DNNE**. Individual connections must learn from the actual neurons they connect.
Hebbian/STDP labels, active populations and a brain-shaped display are insufficient.
This review replaces the narrower recommendation to separate weights and immediately
repeat the balance experiment. Connection identity is only one of the problems.

Reviewed production base: `228e874`. No production learning, model, protocol,
connectome, saved brain state or running service was changed in this review.
New audit code executes only isolated diagnostic cells and newly created state
directories. Forced stimulation and seeded eligibility are explicitly diagnostic,
not learned or biological behaviour. This is a source/mechanism review of the
current substrate, not completed experimental validation of every named nucleus.

## What the biological requirement means

A synapse is a connection between neurons. For the intended model, `A -> B` and
`A -> C` need separate contact identities, strengths and local learning histories.
Changes must depend on the relevant presynaptic activity, postsynaptic activity,
their timing, receptor/cell type, and appropriate local modulation. A sender's own
activity cannot establish what happened at its target.

Timing and cell type matter experimentally: Bi and Poo's hippocampal culture
experiments distinguished potentiation and depression by pre/post order and
reported target-cell and initial-strength dependence. That is evidence for
specific tested synapses, not a universal timing curve to apply to every region.
[Bi & Poo, 1998](https://www.jneurosci.org/content/18/24/10464.full.pdf).

Slow activity regulation and delayed reinforcement also have distinct evidence.
Neocortical synaptic scaling adjusts efficacy following altered activity;
striatal spine experiments demonstrate a limited post-input dopamine window.
Those support separate homeostatic and eligibility/modulation mechanisms, rather
than one generic weight adjustment everywhere.
[Turrigiano et al., 1998](https://www.nature.com/articles/36103),
[Yagishita et al., 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4225776/).

Reduced spiking models can be used, but the causal mechanism and its observable
behaviour need explicit validation against the relevant preparation. This audit
does not claim that naming a cell or reproducing one firing trace establishes
biological equivalence.

## Current structure inventory

The executable inventory reads all structure-host profiles and uses the actual
compiled runtime mappings:

| Measure | Current source/configuration |
| --- | ---: |
| Structure IDs, registry entries and matched host profiles | 119 each |
| Bilateral instances configured | 238 |
| Actual model neurons, one instance of each structure | 34,912 |
| Actual model neurons if all configured bilateral instances run | 69,824 |
| Neurons per structure instance | 160–512 |
| Named cortical areas using laminar populations | 35 |
| Circuit-family kernels | 17 |
| Curated structure-level connectome edges | 452 |
| Izhikevich / LIF / HH-labelled host profiles | 88 / 27 / 4 |
| Plasticity-rule labels | 11 |

`audit-dnne-circuits.ps1` reports 119 `OK` structures. Its stated definition of
`OK` is project/registry/route/drive coverage, not synaptic learning or anatomical
fidelity. These counts describe source and configuration, not a new observation
of current service health. Old documents discussing `NreEngineOptions`, volume
neurons or an earlier 90-structure system do not describe the current execution
path: the active hosts use `Structures/_SharedRuntime/StructureEngine.cs` and
`ModelNeuron.cs`.

## Findings requiring correction

### 1. Pathway identity is used as receiving-contact identity

`ControlProgram/Program.cs`, `RewriteSpikeForRoute`, replaces the emitted
`SynapseId` with `route.SynapseId`. `GetOrCreateInboundSynapse` in the structure
engine keys receiving weight, pre/post traces, eligibility, tagging and budget
only by that ID. Target selection is a separate calculation. Distinct cell pairs
on one tract therefore share learning state.

The executable probe routes two distinct source cells to two distinct target
cells, delivers both events, and observes one incoming learning record. The
completed vestibular capture independently shows one weak receiving state serving
five sampled target cells. Some local cortical contacts already retain distinct
source/target IDs; the finding is not that every connection everywhere is pooled.
The failure is at routed receiving contacts and other branched paths.

The wire contract also lacks source-instance identity. `n-000` alone cannot
distinguish two source hemispheres arriving at the same target instance. A repair
must retain global cell identity, tract/projection identity and actual contact
identity separately, including branch targets and receptor context.

### 2. Wiring changes between identical process starts

`TopographicMap` and several kernels use `HashCode.Combine` for target selection.
These hashes are process-randomized. Two independent executions of the same audit
binary and input cell IDs select different targets for **109 of 128** tested
vestibular source cells. This is measured mapping instability, not a hypothesis
about response quality. Cell biases and conduction jitter also use process hashes.

A checkpoint cannot reliably preserve a learned contact if its cell mapping can
change after restart. Reproducible structure needs stable hashes or stored graph
identities, explicit seeds for stochastic mechanisms, and versioned topology.
Changing the cluster's allocation must not silently change who connects to whom.

### 3. Incoming STDP does not receive actual postsynaptic spike events

`ProcessDueQueue` invokes plasticity on an incoming event, using the target's
current `ActivityTrace`. Later `ModelNeuron.Step` can fire without updating the
incoming synapses' post traces. There is no target-indexed post-event update path.

In the controlled audit, one input creates a synapse with pre trace 1, post trace
0, weight 1 and update count 1. Diagnostic stimulation then makes its target fire
nine times, without another incoming event. All four values remain unchanged.
This establishes an absent post-event learning path. It does not assert that one
biological pairing must always produce a measurable weight increase. The current
rule is an arrival-triggered activity approximation, not complete pair-based STDP.

### 4. Sender learning invents a postsynaptic proxy

`ComputeOutboundPlasticityDelta` calls `UpdateTraceState` with
`sourceActivity * 0.35` as post activity. Actual target-cell activity is not an
argument. Both ordinary outbound and local cortical emission use this rule.

The audit calls the real rule for 120 biological seconds with sender activity
fixed at 0.16 and no receiver activity supplied. At a 5 ms event interval the
weight falls from 1.08 to about 0.9302; at other cadences it changes differently.
This proves sender-only long-term adjustment exists. It does not establish the
cause or rate of historical weight-floor accumulation.

Presynaptic release dynamics may legitimately depend on presynaptic activity.
They must be distinguished from Hebbian connection learning. Long-term learning
needs an explicit owner with access to the real pre/post evidence; two unrelated
weight learners must not approximate opposite sides using invented coactivity.

### 5. A delayed modulator does not update previously eligible contacts by itself

Plasticity normally acts on the state belonging to the currently arriving
message. A dopamine event updates its own receiving synapse. It does not traverse
the previously eligible glutamatergic contacts at the same target cell. Subsequent
glutamatergic arrivals can use lingering modulation, which is a narrower mechanism.

The striatal probe seeds a controlled glutamatergic eligibility trace of 0.8 at a
target cell and delivers only dopamine later. Local dopamine and teaching both
reach 1, but the eligible contact's weight/update count remain unchanged. This is
a mechanism characterization using seeded state, not a calibrated dopamine-window
experiment. The receiver needs a defined eligibility-to-modulation update path.

### 6. Learning activity depends on integration step size

`ModelNeuron.Step` applies `0.84/0.16` activity smoothing and `0.72/0.28` rate
smoothing per call. The coefficients are not derived from elapsed biological
time. The phase increment is also per call. Homeostasis and incoming plasticity
consume the resulting activity trace.

The cadence probe imposes the same ten spikes per second and measures the second
second, after warmup:

| Integration step | True Hz | Mean reported Hz | Mean learning activity |
| --- | ---: | ---: | ---: |
| 1 ms | 10 | approximately 10 | 0.01 |
| 2 ms | 10 | approximately 10 | 0.02 |
| 4 ms | 10 | approximately 10 | 0.04 |

Mean firing rate is not the problem in this particular probe. The activity used
for learning and regulation changes fourfold, and the instantaneous rate smoothing
window changes too. Increased hardware speed or different service intervals must
not change the biological meaning of a spike train. Time constants and event
impulses need physical-time definitions and convergence tests.

### 7. Restored learning timestamps can be ahead of a reset brain clock

Synapses automatically reload persisted timestamps and budgets. The coordinator
starts at zero and `ResetForSimulationRestart` resets its clock to zero; explicit
network import is a separate path. Budget refill requires time to advance beyond
the saved budget timestamp.

The saved 19 October afferent state has budget timestamps up to 7,026,684 ms, while
the completed trial's pre-run coordinator capture is at 15,988 ms. All three saved
incoming entries in each vestibular nucleus have future budget timestamps relative
to that capture. The direct budget probe applies zero change with an exhausted
future-dated budget at timestamp 1,000, but permits change with a continuing clock.

This establishes a mismatch and the conditional failure mechanism, not a measured
current live starvation rate. Existing isolated replays also reset their clocks
while loading these states, which limits interpretation of their learning dynamics.
Future comparisons must continue a common saved clock or perform an explicit,
verified rebase of every temporal state. A restart/import needs one coherent epoch.

### 8. Region-specific learning labels do not guarantee their mechanism

The vermis host declares `CerebellarLTD`, but its receiving branch requires
`climbingCoincident`, which can be true only for `PurkinjeCellLayer`. The vermis
receiving LTD branch therefore always returns zero. This conflicts with the host's
description of climbing-fibre-supervised adaptation.

In the Purkinje path, the current message's own state is modified when climbing
coincidence is recognized. There is no explicit update across the coactive
parallel-fibre contacts at that neuron. The parameter named parallel-fibre
activity receives total postsynaptic activity. These are code findings requiring
contact-specific teaching tests, not justification for applying the same excitatory
STDP rule to cerebellar cells.

Outgoing rule dispatch also falls through to generic trace adjustment for labels
such as `CerebellarLTD`, `HomeostaticGain` and `DopamineHomeostasis`. Incoming and
outgoing mechanisms therefore need separate, explicit definitions and rule coverage.

### 9. Some structural contacts bypass independent target-cell learning

The striatal arbor integrates one message into a primary cell and two additional
cells. Plasticity is called once, using the primary cell's activity. These branch
contacts cannot learn independently from their respective receiving neurons.
Local cortical projections have a real source/target identity but currently choose
one local target per emitted spike. Long-range fan-out is mostly structure-level,
with a useful D1/D2 striatal exception. This is not a stored anatomical neuron-level
connectome with independently learned contacts and convergence/divergence statistics.

### 10. Persistence does not preserve a complete neural state or model identity

The store retains synaptic weights, baselines, traces, tags, update counts and
budgets, with per-instance schema checks and atomic/coalesced writes. Graceful
shutdown correctly drains the writer before the final save. These are useful
existing guarantees.

It does not save each cell's voltage, receptor currents, adaptation, refractory
state, intracellular traces or all queued events. It also lacks a persisted
cell-model/topology fingerprint that proves old target indices still identify
the same cells. A synapse-file reload is a warm initialization from learned state,
not continuation of the complete brain state. The checkpoint contract must state
which operation is requested and preserve coherent identity and time for it.

## Biological model and observation limits

- The `HH` branch uses instantaneous voltage-dependent gates and a spike/reset
  threshold. It is an HH-like hybrid approximation, not the original dynamical
  gating equations. Model names must match the implemented equations and their
  calibration evidence. [Hodgkin & Huxley, 1952](https://pmc.ncbi.nlm.nih.gov/articles/PMC1392413/).
- The same model kind is used across a host's cells with subtype/receptor parameter
  variation. Some noncortical groups use generic cortical subtype fallbacks.
  Anatomical cell-type census, contact geometry and preparation-specific firing
  curves are not established by the current structure profiles.
- Existing homeostasis adjusts excitability/inhibition, not a demonstrated
  contact-preserving synaptic scaling mechanism. It also consumes the cadence-
  dependent activity described above. Receptor, calcium, adaptation, metabolic
  and glial-like state exists, but its parameters need calibration rather than
  being accepted from variable names.
- The connectome contains a direct excitatory vermis-to-fastigial path alongside
  separate Purkinje inhibition. Its interpretation and redundancy require an
  anatomical circuit definition. Purkinje-to-nucleus timing and inhibition have
  experimental consequences; generic aggregate activity cannot substitute for
  that circuit. [Person & Raman, 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3268051/).
- The coordinator can synthesize `spont-*` messages, including forced fallback,
  without a modeled source neuron firing. Background input can be modeled, but
  it needs explicit provenance and an identified source population. It must not
  be counted as proof that a simulated source cell or learning circuit fired.
  Pooling its route ID into real receiving state is another contamination path.
- Source/queue budgets can discard genuine neuronal spikes. Such losses alter
  the experienced presynaptic train; they are not purely a rendering/performance
  detail. Delivery, source drops, learning events and membrane responses need
  conserved accounting and timing tolerance on the cluster.
- The balance readout subtracts population mean activities as presumed
  compensation. The completed controls show this does not reliably identify
  posture error. Fixing synapses does not by itself validate that decoder.
  DNNE remains authority; substituting Avatar's scalar error as a motor command
  would evade the requirement.

## What was tested and what was not

`tools/LearningStructureAudit` compiles against the actual runtime and coordinator
into isolated artifacts. It does not rewrite the production code to make its
diagnostics pass. Its checks capture actual routing/contact state, a real
postsynaptic firing event after a single input, seeded delayed modulation, the
actual sender learning rule, controlled spike-train cadence, and budget clocks.
The contact/mapping probes were repeated in separate processes. Original captures
are preserved; reviewed source hashes and all host-profile hashes are recorded.

The 197 existing focused learning, persistence, routing, laminar, closed-loop,
memory, action and motor/balance tests pass. Their success alongside the measured
failures is a coverage finding. They do not qualify independent receiving
contacts, complete pre/post event learning, delayed reward credit assignment,
cross-process wiring stability or biological-time trace invariance.

No full live-brain trial, training curriculum, whole-brain causal ablation or
physiological calibration was run for this review. The Unity body, original Avatar
receptors and mechanics, Dyad/Entity and unrelated CIV were not modified.

### Evidence and reproduction

The assembled evidence is `artifacts/dnne-biology-review-20261019/`, with a
SHA-256-verified copy at `D:\DNNE-desktop-runs\UnityWorld\dnne-biology-review-20261019`.
It contains all four original audit captures, the two-process projection comparison,
the captured-clock comparison, the circuit coverage report, the 197-test TRX,
the reviewed source snapshot and the final compiled diagnostic. `review-summary.json`
maps confirmed findings to captures or source locations. The v2/v3 captures used
the same audit binary as each other; the final v4 binary adds the delayed-modulation
probe. Existing evidence remains unchanged in its original artifact directories.

Build/run instructions are in
[`tools/LearningStructureAudit/README.md`](../tools/LearningStructureAudit/README.md).
The deliberately imposed cell voltage and seeded eligibility isolate software
mechanisms. They cannot establish physiological parameters or learned competence.

## Repair order and acceptance

1. **Stable anatomy and contact ownership.** Introduce persistent global neuron
   identities and per-contact receiving state, keep tract identity separately,
   and make projections stable across hosts/restarts. Branch contacts need their
   own owners. Validate identical graph fingerprints across processes and reloads.
2. **A coherent biological clock and checkpoint contract.** Define cold start,
   learned-state warm start and complete-state continuation separately. Preserve
   or explicitly rebase all temporal state. Verify budgets, traces and spike
   delays across resume without creating extra learning capacity.
3. **Actual pre/post event learning.** Maintain incoming adjacency by target cell.
   Arrival updates the appropriate pre state; actual post spikes update the
   corresponding post state. Implement and test family-specific LTP/LTD timing,
   independent contacts and unpaired/control conditions. Sender release and
   receiving long-term plasticity need distinct biological roles.
4. **Local teaching and regulation.** Apply real modulators to eligible contacts
   with bounded biological windows; implement actual parallel/climbing-fibre
   teaching and appropriate inhibitory/cell-type rules. Correct time constants,
   activity normalization and homeostatic regulation before tuning gains.
5. **Cell/circuit calibration.** Choose documented experimental preparations for
   representative cortical, striatal, vestibular and cerebellar cells/circuits.
   Test firing curves, adaptation, refractory periods, receptor kinetics, plasticity
   curves and stability. List remaining reduced-model approximations explicitly.
6. **Integrated neuronal behaviour.** Repeat stationary, head-motion, tilt/support,
   muted, unilateral and return-to-stable controls with accounting and matched
   epochs. Require persistent discrimination and appropriately bounded motor
   authority, then assess the actual body and recovery in a paused-start world.

Old pooled weights cannot supply missing individual learning histories. Preserve
them as immutable evidence. Compare conservative strength inheritance, explicitly
new contact histories and fresh-state controls; do not silently invent learned
detail or wipe the existing saves. A migration must record provenance and make
biological learning claims only from the corrected, measured mechanisms.

The biological learning layer needs this repair before further training is used
as evidence of progress. More layers, more examples or the future cluster cannot
repair incorrect learning ownership, timing or causal events.
