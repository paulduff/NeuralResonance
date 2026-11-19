# Reticulospinal recurrent ownership candidate — 19 November 2026

The next isolated connection joins actual reticular and spinal neuronal
populations with four existing connectome edges: vestibular nuclei to reticular
formation, vestibular nuclei to spinal motor, reticular formation to spinal
motor, and spinal motor feedback to reticular formation. This makes recurrent
neuronal execution possible without waiting indefinitely for both populations
to advance first. It does not establish physiological activity or Avatar balance.

## Actual neurons and original pathways

`ReticulospinalCircuit` owns fresh, isolated 256-cell reticular and spinal
populations through `VestibularProjectionReceiver`. The existing host profiles
remain Izhikevich/HomeostaticGain and Izhikevich/STDP respectively. Their neuronal,
contact, receptor, substrate, learning and conduction calculations are unchanged.
Supply is explicit; old state directories are rejected. No cell receives an
invented rate command, imposed firing threshold or artificial recovery action.

The circuit takes all three source catalogs and records the selected and omitted
edges in its fingerprint. Other vestibular, reticular and spinal destinations
are outside this four-edge experiment; no receipt claims those targets ran.
Spinal routing here is restricted to reticular feedback. Cerebellar efference
copies still require their existing separate coordinator policy.

`RecipientNucleusProjection` binds outgoing packets to each actual retained
receiving frame. Original completed steps, cellular spikes, routed copies and
the actual downstream receipts stay available. A source is released only after
the next population owns every declared packet. The circuit retains those frames
before releasing the inner owner, and requires an exact outer acknowledgement.
Duplicate retries are inert; a partial failure stops the circuit and preserves
its actual accepted and integrated prefix. It never rolls neurons back or replays
an already applied contact.

## Causal progress around the cycle

An unknown source event must occur strictly after that source's completed time.
The existing conduction calculation has a lower bound enforced by `ClampDelay`.
The new isolated observer reads the minimum of the tract and feedback bounds;
it neither changes conduction nor adds a delay. For the present profiles the
conservative bound is 2 ms into reticular formation and 1 ms into spinal motor.

With lookahead explicitly enabled, each population can integrate only before
the earliest possible unknown arrival from **every** declared source. The
floating-point bound is `BitDecrement(BitIncrement(sourceTime) + minimumDelay)`.
This also handles a binary exponent boundary where adding a delay can round
distinct source times to the same arrival time. Ordinary no-lookahead recipients
retain their previous completed-source-time bound.

The circuit alternates actual reticular and spinal progress and immediately
hands each completed output to its peer. It does not exchange endless zero-time
quiet frames. Finite vestibular input limits both populations' progress, and
grids too coarse to advance safely from the initial conduction interval are
rejected. Each outer frame retains at most 4,096 transfers; further known work
requires acknowledgement and another frame.

## Declared ownership changes

The outbox and recipient protocols become v2, with explicit source structure
and typed vestibular/reticular/spinal identities. Their fingerprints and delivery
identities change accordingly; old frozen runs stay unchanged. Original neuronal
spike validation now rejects any in-place modulation correction instead of
silently accepting a changed event. The new gate records exact before/after
hashes and diffs for the outbox, receiver and read-only observer. Earlier source
owners, coordinator methods, biological kernels and all 130 earlier test cases
are preserved. This is an isolated protocol change, not a live service migration.

## Prospective verification and next action

Sixteen new controls cover typed source identity, original modulation retention,
the spinal routing restriction, actual conduction bounds, cyclic startup,
floating-point endpoint safety, missing-source horizons, actual output retention,
external-release detection, actual contact delivery, partition invariance,
backpressure/retry, partial admission/integration failure and saved-state/grid
rejection. They join the unchanged 130 cases in a 146-case workflow.

Both projects build cleanly. The tests have **not** run at this checkpoint.
The recipient study `vestibular-recipient-20261119-49d913` must first finish and
pass its completed-result review. `review-vestibular-recipient-result.py` requires
the operator's final completion report before opening that capture. After its
review and D: backup, `reticulospinal_gate.py prepare` can freeze a clean pushed
revision. Back up the fresh package and run its `start-reticulospinal-window.ps1`
in a visible independent PowerShell window, with no monitoring.

Continuous sensory reference `canal-reference-20261119-2a402c` is also still
awaiting the operator's report. Neither pending capture was accessed during
this authoring work. No third study has been launched.

These controls use declared source-event fixtures. Actual recipient integration
and feedback ownership do not by themselves demonstrate a physiological
vestibular firing pattern, calibrated target anatomy, independent coupled
trajectory accuracy, the later motoneuron-to-muscle path, live brain authority
or recovery in World. In particular, the existing routed target-cell naming
and receiving maps are preserved, not newly anatomically calibrated here.
