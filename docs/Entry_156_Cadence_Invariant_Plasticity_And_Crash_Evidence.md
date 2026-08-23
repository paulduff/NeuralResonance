# Entry 156 - Cadence-Invariant Plasticity and Crash Evidence

Date: 2026-08-23

## Status

Implemented and verified. The complete DNNE stack remains stopped.

## Triggering evidence

The Entry 155 acceptance run began at approximately 17:42 on 2026-08-22 and
ended in an unplanned laptop power-off between approximately 22:23 and 22:31.
No final world report survived. The current-generation synapse directory did:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\synapses\entry155-observation-20260822-174216`

All 236 saved files parse as generation-two JSON. They contain 67,195 inbound
and 73,111 outbound synapses. The latest neural timestamp is only 82.281
seconds despite roughly 4.75 hours of wall time. The laptop therefore advanced
at about 0.5 percent of biological real time.

Entry 155 accumulated at least 345,570,436 inbound synaptic updates. Weight
clamping was severe:

- 20.9 percent of inbound weights reached the minimum;
- 5.4 percent of inbound weights reached the maximum;
- 73.9 percent of outbound weights reached the minimum;
- 9.2 percent of outbound weights reached the maximum.

Entry 154 had no comparable outbound collapse. Entry 155 produced about nine
times as many plasticity updates in slightly less neural time.

Windows recorded firmware speed limiting on all eight logical processors,
ControlProgram frame requests as slow as 11.4 seconds, an incomplete spike
request near the end of the run, and an unexpected shutdown. It did not record
a display-driver reset, WHEA fault, storage fault, resource-exhaustion event,
or application crash. The former Turmo display driver was not present. The
supported explanation is sustained CPU, persistence, and thermal contention.

## Root cause

Entry 155 correctly restored elapsed biological time and subdivided long
service intervals into steps of at most four milliseconds. That allows a
neuron to emit multiple biologically timed spikes during one host scheduling
interval. The older engine could emit at most one spike per selected service
tick.

The plasticity amplitudes were calibrated under that older, artificially
sparse spike stream. Every newly restored spike still received the complete
legacy weight adjustment. The due queues consume each message once, so this is
not duplicate ingress. It is event-rate amplification combined with learning
gains that have no biologically timed cumulative bound.

Due spikes were also consumed at the end timestamp before all integration
substeps ran. Multiple outbound spikes emitted by those substeps consequently
shared one timestamp. That obscured the biological interval between events and
made cadence-sensitive traces harder to constrain.

Persistence compounded the load. A full per-structure snapshot could be
constructed after every 4,096 mutations. Hundreds of millions of mutations
therefore caused repeated dictionary copies and serialized full-state writes.

## Repair

### Neural time and plasticity

1. Deliver due spikes at the integration substep in which their conduction
   delay expires.
2. Timestamp outbound spikes at that substep rather than at the host tick end.
3. Preserve event-based STDP, BCM, tagging, neuromodulation, and Hebbian
   teaching.
4. Scale legacy event amplitudes to the restored biological spike density.
5. Give each synapse a refillable plasticity budget measured in weight change
   per biological second. Bursts may consume a small reserve but cannot make
   unbounded cumulative changes at one timestamp.
6. Persist the budget state and start a new persistence generation. Entry 155
   remains diagnostic evidence and is not resumed.

Ordinary newly formed synapses begin with a 0.005-quanta reserve, can hold at
most 0.04 quanta, and refill at 0.01 quanta per biological second. Their
legacy event amplitude is scaled to 0.02 under the restored spike density.
Content-bearing glutamatergic synapses in hippocampal and consolidation
circuits receive the bounded 0.04-quanta formation reserve and retain full
event amplitude for one-shot encoding. Neuromodulatory afferents gate that
encoding but are no longer counted as episode-bearing engram synapses.

No host policy, action preference, scripted movement, ML model, or reward
bypass is introduced.

### Persistence

1. Replace mutation-count-triggered full snapshots with dirty checkpoints due
   after either a biological-time interval or a wall-time fallback.
2. Keep the single coalescing background writer and atomic temporary-file
   replacement.
3. Materialize an empty generation file when a service starts so missing files
   distinguish a service that never started from a population that remained
   synaptically quiet.
4. Preserve a synchronous final save on graceful shutdown.

### Crash evidence and load protection

1. Write one atomic rolling world report per session on a bounded wall cadence.
   Each heartbeat replaces its predecessor, limiting storage growth while
   preserving recent authority, body, gait, contact, damage, and world state.
2. Track brain-frame latency in WorldSim.
3. Pause the physical world and write an overload report only after sustained
   consecutive multi-second brain responses. This is a host safety boundary,
   not behavioural authority.

### Recruitment audit

1. Require every launched structure instance to materialize a persistence
   file, including both Ventral Pallidum instances.
2. Verify Ventral Pallidum retains afferent Nucleus Accumbens inhibition and
   efferent habenular and thalamic projections.
3. Report bilateral synapse and update asymmetry after each run so anatomical
   mapping defects can be separated from asymmetric experience.

## Acceptance

- Equivalent biological spike trains must produce equivalent weights across
  one-millisecond and sparse catch-up scheduling.
- A high-density burst cannot consume more than the configured biological
  plasticity budget.
- A healthy repeated input still changes its synapse measurably.
- Persistence snapshots remain atomic and are not mutation-amplified.
- An abruptly terminated world leaves a readable rolling report.
- A transient slow frame does not pause the world; sustained overload does.
- Both Ventral Pallidum instances have inspectable generation files on startup.
- Focused tests, the complete suite, and the Release solution build pass.

## Implementation outcome

- Due spikes are delivered within their biological integration substep and
  outbound spikes carry that substep timestamp.
- Every inbound, local, and outbound weight update passes through a persisted
  biological-time token bucket. Generation three stores its reserve,
  timestamp, and cumulative absolute change.
- Mutation counts no longer trigger full persistence snapshots. Dirty state is
  sampled cheaply and saved on bounded biological or wall-clock intervals,
  with an atomic empty generation materialized at service startup.
- WorldSim writes an atomic rolling heartbeat report while a run is live. The
  fixed per-session file is replaced rather than accumulated.
- WorldSim records brain-frame latency and safety-pauses physical time only
  after three consecutive frames at or above four seconds. A fast connection
  failure or one transient slow frame cannot trigger the pause.
- Ventral Pallidum connectivity is protected by tests for accumbal inhibition
  and habenular, mediodorsal thalamic, and motor thalamic output gating.
- `tools/analyze-synapse-state.ps1` reports unreadable files, weight floors and
  ceilings, missing bilateral populations, and left/right synapse and update
  asymmetry. Against Entry 155 it correctly identifies both absent Ventral
  Pallidum files.

## Verification

- Focused Entry 156 acceptance set: 61 passed, 0 failed.
- Memory and cadence regression set after hippocampal correction: 14 passed,
  0 failed.
- Complete DNNE test suite: 827 passed, 0 failed.
- `dotnet build NeuralResonanceEngine.DNNE.slnx -c Release --no-restore`:
  succeeded with 0 warnings and 0 errors.
- PowerShell parser validation for `tools/analyze-synapse-state.ps1`: passed.
- Entry 155 audit reproduction: 236/236 files parsed, 140,306 synapses,
  345,570,436 updates, and Ventral Pallidum reported missing bilaterally.
