# Retained motor events and acceptance receipts

The existing publisher, receiver, isometric plant, original Avatar motor paths
and their tests were inspected before this change. The receiver already rejects
sequence gaps and treats exact recent replay idempotently. Its publisher previously
left retention and retransmission entirely to the caller. The new isolated
`MotorAxonReplayBuffer` adds a bounded source-side queue and explicit receipts.

It uses the existing publisher to snapshot original source emissions. Sequence,
ordinal, original message/contact route and fractional firing time are retained.
Unacknowledged batches can be retried in their original order; retry does not call
Publish again or manufacture new events. Limits on both events and batches are
checked before publisher progress. Empty completeness batches consume batch
capacity too. A full source buffer rejects publication intact. Its caller must
retain those emissions and hold neural advancement; there is no permission to
discard them, infer silence or synthesize replacement spikes.

`AvatarMotorAxonReceiver.AcceptWithAcknowledgement` returns a receipt for the same
immutable batch snapshot that passed acceptance. The existing Accept behavior
is retained. An exact recent replay yields the same receipt without queuing events
again. The isolated isometric plant exposes the same receipt path behind its
existing fault guard, without altering calcium, activation or force calculations.

A receipt binds protocol, session, instance, anatomy, batch sequence and SHA-256
of the entire serialized batch. Only a matching retained batch can release source
capacity. A receipt for a later sequence acknowledges the contiguous preceding
batches; the receiver could not accept that sequence across a gap. Recent duplicate
receipts are idempotent, while conflicting, foreign, unknown or expired receipts
are rejected without advancing the acknowledgement boundary. Caller-owned source
arrays/events cannot subsequently rewrite retained batches.

## Scope and remaining delivery requirements

Acceptance is not body delivery, muscle contraction or a durable checkpoint.
These are trusted in-process protocol receipts, not cryptographic authentication
of a remote neuronal source. Both sides remain in memory. A process crash requires
explicit recovery with the neuronal/body state and event journal; this component
does not implement that recovery or silently start a replacement epoch.

The caller must serialize access. Default source capacity is 32 unacknowledged
batches and 65,536 events; recent acknowledgement retention is 64. Receiver replay
history must be sized/orchestrated consistently with the number of in-flight
batches. Receiver pending-event capacity, conduction lookahead and finite epoch
identity retention remain separate bounds. Source acknowledgements do not make
those bounds disappear or guarantee forward progress under every configuration.
Resource failure after source publication faults this buffer instead of allowing
continued publication with uncertain retention.

No live StructureEngine, network transport, Avatar or Unity controller selects
this lane. Actual neuronal emission must enter before existing lossy dispatch
caps when the coupled motor pathway is qualified. The current population-derived
control and editor labels remain unchanged. The original vestibular, proprioceptive,
plantar and muscle mechanisms are preserved; worlds still start paused.

## Verification

Sixteen new interface cases cover lost receipt/retransmission after delivery,
both capacity limits and retry, six malformed receipt fields, cumulative receipts
before delivery, duplicate/conflicting/expired receipts, caller mutation, rejected
source input, empty batches and isometric-plant replay without double contraction.
They are fixtures, not motor-neuron or physiological qualification.

The next olive CPU-batch window also runs these sixteen cases plus all 32 existing
motor-transport, thirteen calcium, fifteen isometric-plant, ten olive-batch and one
manifest cases: 87 selected cases in total. All must pass before the six saved
olive trace comparisons. This is not the full unit suite. Release compilation
passes; runtime results remain pending. The tests run only in the independent
visible PowerShell window, with frozen source, verified D: backup and no monitoring.
