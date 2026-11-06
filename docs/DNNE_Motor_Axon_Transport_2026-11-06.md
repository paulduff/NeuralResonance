# Preserved motor-axon events: isolated transport candidate

The [motor-path audit](DNNE_Motor_Authority_Path_Audit_2026-11-06.md) identified
two different breaks: population activity is converted into artificial body
events, and the muscle plant uses a joint-error controller. This increment builds
the transport boundary needed to replace the first break. It does **not** connect
the new boundary to live neurons or muscles, or qualify the second break.

Existing Avatar nervous-system, dispatch parser/bridge, muscle equations and the
Unity muscle generator were inspected first. The current dispatch trace omits
message identity and substitutes batch delivery time for individual firing time;
the Avatar parser retains wall time. It cannot be reused as the motor-event lane.
The existing muscle activation/fatigue mechanics and receptor paths are retained
for their later, separately qualified integration.

## Source and anatomy

`Protocol/MotorAxonProtocol.cs` declares a versioned batch, individual discharge,
explicit anatomy and simulation-clock epoch. `MotorAxonPublisher.cs` snapshots
original `SpikeMessage` fields without deriving a spike count from a firing rate.
It accepts only the configured spinal instance, an explicitly bound source cell
and projection, and cholinergic non-background, non-feedback events. It preserves
the original message ID, source cell, projection, source synapse ID and fractional
neural firing time. A new ordinal describes transport order; it does not replace
the original message ID. Session identity is explicitly supplied by the run owner.

Each declared motor neuron maps to one distinct motor unit. The endplate-group
contact ID is stable across processes and derived from instance, cell, projection
and unit identity. It represents the motor unit as a group, not individually
modelled muscle fibres or endplates. The original source synapse identity remains
separate from this body contact. Antagonists require different neurons/units;
negative excitation and inhibitory skeletal-muscle events are not supported.

The anatomy fingerprint includes identities and declared conduction delays. It
is insensitive to input ordering, but changes if a delay or binding changes.
The code supplies no anatomical wiring or fitted delay values automatically.
Test bindings such as `alpha-0` and `left-soleus-0` are explicit synthetic fixtures,
not newly deployed neuronal populations. Valid protocol fields are not evidence
of a biological source or a security authentication mechanism.

## Ordering, missing traffic and replay

Each source has contiguous batch sequence numbers and discharge ordinals.
Firing times are nondecreasing. An inclusive completeness watermark promises that
no later batch will introduce an event at or before that neural time. An empty
batch can prove silence; a missing packet cannot. A missing sequence or ordinal,
repeated event ID, unknown contact, wrong session/fingerprint, nonfinite time,
late event or invalid transmitter rejects the entire packet without consuming
its valid prefix. The source publisher likewise commits only a fully valid batch.

The Avatar receiver snapshots caller-owned event arrays and queues immutable
arrival records. Exact recent replay is idempotent, including after delivery.
A changed or expired replay is rejected. Equal-time events from different sources
are ordered by ordinal source-instance comparison and then source event ordinal,
independently of packet arrival order. Packet partitioning does not change the
source events, their conduction delays or the returned arrival order.

The default pending queue is 65,536 events and replay history 64 batches per
source. Original event IDs are retained for an explicit finite epoch, up to
1,000,000 events by default on each publisher and the complete receiver. Capacity
failure rejects a batch intact: it never silently truncates, merges or converts
events into a rate. Pending-queue pressure can be relieved by consuming already
complete input and retrying the same batch. Epoch exhaustion requires explicit
operator/run orchestration; it does not reset the brain or re-anchor time.

These are bounded verification components, not a production transport service.
Durable retransmission, acknowledgement, source backpressure, epoch transition
and a joint neural/body checkpoint remain necessary before long-running or
cluster deployment. A source must publish directly at its neuronal emission
boundary, before existing dispatch caps. Once events have already been discarded,
a well-formed batch cannot reconstruct them or prove their absence.

## Neural and body time

`AvatarMotorAxonReceiver` uses a fixed one-to-one simulation-time map:

```
arrival_body_ms = body_epoch_ms + (fired_neural_ms - neural_epoch_ms) + axonal_delay_ms
```

Wall-clock delivery time never enters that expression. All declared source
instances must share the stated neural simulation clock. The body may advance
only as far as the minimum completeness watermark across those sources. The
receiver conservatively does not add conduction delay to that completeness bound.
It retains later arrivals across body steps, and never retimes a late discharge
to the present. A paused body does not advance this clock; it does not imply that
the receiver has proved its future input complete.

The caller must integrate contraction at each returned event's own arrival time,
not apply every event at the end of a frame. The receiver itself supplies no
activation filter, muscle force, angle target, reciprocal inhibition, balance
policy or locomotor rhythm. Access is serialized by the caller.

## Verification and integration status

The Release test-project build passes with no warnings/errors. The visible-window
rig contains 32 transport cases plus the solution-manifest case. Tests cover
fractional timing and JSON round trip; original message/synapse/contact identity;
multi-source completeness; empty-batch silence; partition and source-order
invariance; missing batches; replay before/after delivery; atomic malformed-batch
rejection; capacity/retry; long-delay retention; and anatomical fingerprints.
All source events in these tests are declared fixtures. These cases do not prove
neuronal firing, neurotransmitter release, muscle contraction or body recovery.

`tools/prepare-motor-axon-verification.py` freezes pushed source, and
`tools/start-motor-axon-window.ps1` builds/tests inside that snapshot. It records
source/binary hashes and TRX/logs, verifies a D: backup, and leaves its visible
PowerShell window open. No live monitoring is used. Results await the operator's
completion report. The separate Golgi and cell-local timing captures are untouched.

The editor now labels its existing channels as population-derived commands and
its counter as derived motor events. Unity no longer claims “no scripted balance
or gait” for that existing path. Those are source changes; no live editor/world
was restarted or rebuilt as a Unity player during this increment.

Next connect a qualified motor-neuron population to this lane and independently
qualify an event-driven isometric twitch response before dynamic force, fatigue
and joint coupling. Then replace the arithmetic joint-error and phase rules with
connected spinal sensory/interneuron/motor populations. Update the shared Unity
port and actual authority telemetry together at promotion. Existing saves,
fingerprints and live control remain on their previous path until that explicit
qualification; a transport test cannot certify balance or a usable Avatar.
