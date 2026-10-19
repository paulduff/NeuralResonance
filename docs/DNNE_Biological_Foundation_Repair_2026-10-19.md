# DNNE contact-learning foundation repair — 19 October 2026

The receiving-contact, event and clock defects identified in the
[foundation review](DNNE_Learning_And_Structure_Review_2026-10-19.md) are corrected
in source and exercised in isolated tests. Avatar and the editor now carry the
associated identity and learning telemetry. This is a foundation repair, not
qualification of biologically equivalent learning across all nuclei or of physical
balance/recovery. No running brain was restarted, reset or deployed during this work.

## Implemented mechanisms

- **Individual receiving contacts.** A contact is identified by the source
  instance, source cell, receiving instance/cell, projection, transmitter and
  feedback/background provenance. A tract ID no longer owns the weights of every
  cell it reaches. Each target of the striatal three-cell arbor owns separate
  state. Source release IDs survive coordinator rewriting; projection IDs travel
  separately. The deterministic identity version is `contact-events-v1`.
- **Stable wiring.** Process-random hashes in projection, cell bias and conduction
  mapping are replaced with invariant deterministic hashes. Outgoing release IDs
  are reproducible. Event IDs remain unique for transport deduplication; they no
  longer choose anatomical delay jitter.
- **Actual pre/post events.** Arrival updates its contact's pre trace. A real
  receiving-cell spike updates its incoming contacts' post traces. Target-local
  post history also supports a first incoming event after an earlier target spike.
  No sender-only substitute for receiving activity remains in outbound emission.
- **Local teaching.** Eligible receiving contacts can change when later dopamine
  arrives, without requiring another glutamate arrival. Tag capture, BCM and
  homeostatic regulation advance in biological time. Climbing input changes
  coactive granule/parallel-fibre contacts on the receiving cell, rather than its
  own climbing-fibre weight. Excitatory timing and inhibitory homeostasis use
  separate reduced rules. Synthetic background contacts cannot accumulate
  Hebbian credit.
- **No assigned behavioural outcome.** The unconditional corticostriatal
  eligibility bonus and direct extinction/reacquisition weight changes are removed.
  Learning requires the events and local conditions specified by the rule.
- **Time and efficacy.** Activity and rate traces use fixed spike impulses with
  exponential decay in milliseconds. Oscillator phase uses elapsed time. Receiving
  efficacy is presynaptic release multiplied by receiving gain, with the existing
  bounds; the previous square root compressed that relationship. Continuous
  consolidation cannot retroactively act before a contact arrived or was updated.

The explicit current trace constants are 20 ms pre, 35 ms post, 900 ms eligibility
and 8,000 ms tag. Activity/rate filters use 20/100 ms. These are model parameters,
not measurements establishing a universal rule for all structures. The existing
0.02 learning scale and bounded plasticity budget remain provisional numerical
constraints. They require calibration against an identified preparation.

## Saved state and reinitialization

Persistence schema **4** records contact provenance and a version/configuration
fingerprint. Topology-affecting changes require a version bump; this is not a full
connectome or whole-brain-state fingerprint. It rejects incompatible generations/
configurations, wrong instance
identity, missing/duplicate contact entries, invalid target cells and contact IDs
that do not match their recorded endpoints. Failure preserves the original file.
Automatic pruning is removed: exceeding a contact capacity raises an explicit
error rather than deleting learned connections. Capacity/memory requirements must
be measured before whole-brain deployment.

A compatible reload is explicitly **learned-state warm initialization**. It keeps
weights, baseline strengths, threshold state, learning history and remaining
plasticity budget, clears transient pre/post/eligibility/tag traces and anchors
the budget clock to the new biological interval. It does not reload membrane,
receptor, event-queue or whole-brain clock state. Exact continuation is not
implemented. Initializing an exhausted budget does not refill it; a later elapsed
biological second permits the normal refill.

For the next controlled experiment, use a **fresh, separate state directory** set
through `NRE_SYNAPSE_STATE_DIR`, with consistent `SERVICE_INSTANCE` names in all
hosts. Keep the prior learned state and verified D: backups intact. Do not point
the corrected runtime at schema-3 saves and expect an automatic upgrade. Do not
edit a schema number to bypass validation. Pooled weights cannot reconstruct
missing individual learning histories; no legacy-state migration was implemented.

This is the recommended reinitialization, not an action already taken on the live
brain. A membrane-only reset with the old weights would carry forward the previous
learning defects. Fresh state establishes a baseline, not competent behaviour.

## Avatar and editor

The original Avatar nervous system, musculature, labyrinth, balance and physical
receptor paths were inspected. Physical receptor messages now carry their stable
fibre/projection and Avatar source-instance identity. The dispatch wire/parser
preserve global source instance, projection and synthetic-background provenance.
The nervous system excludes coordinator-generated background messages from direct
motor/effector interpretation; genuine downstream neuronal responses still pass
through the existing authority path. Body mechanics and reflex definitions are
unchanged. DNNE remains the action authority.

The editor's selected-region inspector shows cell/contact counts, pre/post contact
events since service start, saved-state scope and sampled receiving connections.
Counts are contact events: one cell spike can update several incoming contacts.
The sample includes source and target owners and receiving strength, with IDs and
sample time in the tooltip. Up to eight contacts per host are sampled at 250 ms
biological intervals (also on the first nonempty sample); the combined sample is bounded
at sixteen. This is an illustration of the measured contacts, not the entire graph.

Missing host telemetry remains unavailable. A stale source or stale frame is
labelled stale. Every display says that physiological calibration is pending.
The browser verification used an explicitly labelled saved diagnostic fixture,
including current, missing and stale states; it was not attached to a live brain.

## Evidence

Accepted isolated captures: `artifacts/biological-repair-20261019/audit-final-e`
and `audit-final-f`. The executable calls production routing, receiving learning
and outbound-spike construction. It does not contact live services or read live
weights. Forced postsynaptic stimulation and seeded eligibility are diagnostic.
Earlier attempts/captures are retained separately.

| Check | Observed result | Limit |
| --- | --- | --- |
| Independent process starts | 0 of 128 target mappings differ | Sampled mappings, not a complete graph census |
| Two routed cell pairs | Two receiving states | Source/hemisphere separation is also covered in unit checks |
| One input followed by nine target spikes | Update count 1 → 10; weight 1 → 1.0011995 | Imposed drive, not physiological firing calibration |
| Later local dopamine | Eligible strength 1 → 1.0000238, with no new glutamate input | Eligibility was seeded to isolate the delayed path |
| Sender activity only | Release/weight remains 1.08 at all six tested emission intervals | No receiving activity supplied |
| Same spike train, 1/2/4 ms steps | Activity about 0.03374336 and rate about 14.60348 Hz at the same 8 ms offset | Sampled time averages still have finite-step quadrature error |
| Warm budget initialization | Zero immediate change; normal change possible after 1 s | Warm initialization, not exact continuation |

`checks/regression-accepted.trx` records **394 passed, 0 failed, 0 skipped**.
Coverage includes learning/persistence, routing, circuit and receptor behaviour,
motor/righting/withdrawal, Avatar balance/labyrinth, scheduler, editor aggregation
and wire compatibility. The audit build has no warnings/errors. Nine JavaScript
brain-display checks pass; browser verification confirms the learning inspector.
After bounding contact sample refresh cost, the 22 contact/telemetry checks also
pass in `checks/learning-telemetry-accepted.trx`. The two accepted captures use
that final runtime. `tools/LearningStructureAudit/verify_repair.py` verifies them
against source hashes and writes `accepted-mechanisms.json` in the evidence bundle.
Source hashes, accepted captures, compiled diagnostics, screenshots and test results
are retained in the evidence bundle and copied with SHA-256 verification to
`D:\DNNE-desktop-runs\UnityWorld\biological-repair-20261019`.

Two previous tests encoded invalid assurances. The action-reversal fixture changed
the sending cell between conditions and therefore tested shared-state behaviour.
The extinction fixture depended on direct assigned weight changes. They are
replaced with independent-contact and actual-pairing controls; these replacements
do **not** certify learned action reversal or extinction. Those behavioural claims
remain unqualified.

## Remaining acceptance work

Cell-type and circuit calibration is still required. The `HH` option still uses
instantaneous conductance gates and a hybrid spike/reset mechanism; this repair
does not turn it into the original dynamic-gate Hodgkin–Huxley model. Anatomical
cell counts, inhibitory rules, local dopamine encoding, cerebellar projection
definitions, firing curves, plasticity magnitudes, receptor kinetics and synaptic
scaling need explicit experimental targets. The generic gain/trace rules above
must not be represented as biological equivalence merely because tests pass.

Next establish corrected fresh-state sensory transfer and stationary/motion/tilt/
support discrimination, including muted, unilateral and return-to-stable controls,
with spike accounting and matched epochs. Then connect Avatar in a paused-start
world to assess feedback, balance and recovery. The world supplies essential
sensorimotor experience; it cannot repair incorrect learning ownership or prove
that an uncalibrated neuronal model is equivalent to a biological brain. Long
experiments remain detached visible PowerShell runs without live monitoring.
