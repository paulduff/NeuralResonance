# Complete body and contact observations

The existing physical-sample v1 lane transports only a body frame. The pending
actual Unity study preserves all contact JSON separately, but that does not
deliver a complete body/contact endpoint to a future sensory consumer. A lost
contact frame must not be interpreted as an unloaded body region.

This candidate adds an explicitly separate combined observation protocol and
producer. No live service, physical controller, receptor model or neuronal host
selects it. The frozen body-only Unity study remains unchanged and uninspected.

## Existing Avatar boundary inspected

The inspection covered `AvatarControlApi` somatic posting, the shared body/contact
contracts, `SomaticContactDescriptor` validation, the somatic transducer, existing
wire/producer/transport tests, Unity `SensoryFrames`, `ArticulatedAvatar.Create`,
`ContactSensor`, and the isolated physical bridge. The existing contact frame
contains position, normal, force, impulse, penetration, slip speed, area, duration,
source, sequence and wall capture time. Preserve these rather than replacing
them with generated afferent spikes or an action/standing label.

Unity currently declares 15 physical links and emits one encoded contact frame
per link in the isolated bridge. Position is rotated into pelvis coordinates
with the initial pelvis height added to its Y component; normals are rotated
into pelvis coordinates. Force, impulse and other values retain the existing
`ContactFrame.Capture` saturation. The collector aggregates each physics step:
it sums impulse/force but retains the last collision's representative point and
normal. `BeginStep` resets force/impulse/penetration/slip, while point, normal and
duration can retain earlier values on an unloaded step. The new transport
preserves that encoding exactly; it neither qualifies those contact mechanics
nor silently resets or normalizes their values. Contact absence means an
explicit zero-load measurement, not necessarily zero in every metadata field.

## Contract and ownership

`PhysicalObservationLayout` snapshots and ordinally sorts a declared contact-site
list. Each site and source is unique and separate from the body source. The clock,
body/session identity, site/source mapping and named contact encoding participate
in its fingerprint. Changing any of those declares a different stream. The site
list is not inferred from arriving frames, so a missing link cannot shrink the
required observation set.

One `PhysicalObservationSample` contains the typed body snapshot and every
declared contact. Each frame's sequence must equal the actual physics step;
original wall stamps remain separate metadata and can differ across captures at
one held endpoint. The existing fixed physical-to-neural clock mapping supplies
simulation time. Every declared site is required even when unloaded. Missing,
extra, duplicate, substituted or stale contacts reject the entire endpoint.

The immutable JSON snapshot retains signed muscle velocity and contact vectors.
It is the canonical typed representation, not a claim of byte equality to Unity's
original serializer. All transmitted members must be present, including explicit
nulls for optional body fields. Unknown members, duplicate keys and nonfinite
numbers reject decoding; missing force cannot silently deserialize as zero.
This does not make an optional body measurement that the original Unity encoder
does not populate into a newly measured quantity.

The combined outbox/receiver use the reviewed body-lane queue algorithm, with
separate public types and layout-bound receipts. The existing body-only v1 types
and source are unchanged, with no implicit save or wire upgrade. Maximum combined
payload is 1 MiB, batches contain at most 128 samples, and the default retained
payload budget is 16 MiB. Byte accounting covers encoded observation payloads,
not total managed heap use. Source capacity for a maximum-size endpoint is
reserved before movement. Receipt proves in-memory acceptance only. Full queues
hold physics; invalid final samples reject a whole packet without partial commit.
Retries retain exact original bytes, and consumption is separate from acceptance.

`AvatarPhysicalObservationProducer` exclusively owns one synchronous body/contact
capture on its owner thread. It begins paused, captures the initial endpoint and
checks actual counters and fixed duration before/after capture. Missing contacts
after movement fault the epoch without publishing the body's partial endpoint.
Earlier complete samples remain drainable. There is no later replacement,
invented silence, rollback, crash journal or durable receipt guarantee.

## Unity adapter and prospective qualification

`UnityObservationEncoding` declares the current 15 links from
`ArticulatedAvatar.Create`; the encoding identity records its coordinate
convention. It parses every contact member explicitly, rejecting missing fields,
unknown keys and repeated members. `UnityPhysicalSampleBody` implements the new
interface explicitly: it obtains body and contacts from the same retained bridge
reply, without a second capture or physics request. Its existing body-only
interface and experiment remain selected by the current study. A later combined
Unity study must explicitly choose the new producer and layout.

The prepared focused PowerShell rig has **65 checks**: 26 combined-observation
cases, the existing 16 body-producer and 22 body-transport cases, plus the solution
manifest. These cover topology identity, unloaded/missing/stale contacts, exact
numeric/wall metadata preservation, immutable payloads, incomplete/invalid packet
rejection, capacity holds, complete replay, receipt conflicts/expiration, owner
thread/reentry, failure after movement, clock changes/exhaustion, and the explicit
Unity contact parser. These are fixtures and existing recorded body observations,
not new Unity physics or neuronal firing. The full unit suite is not this scope.

Both the test assembly and Unity .NET probe build with zero warnings/errors.
Preparation Python and launcher PowerShell parse. The new tests have not yet run
at preparation time; they execute only in a visible independent PowerShell
window. Preserve its frozen source, binaries, logs and verified D: capture.
Review only after the operator reports completion. Native Unity combination,
live clock handshake, causal receptor dynamics, actual afferent firing and Avatar
recovery remain unqualified. No editor label or live service is promoted.

After the pending Unity body study and this candidate's focused checks are
reported and reviewed, exercise the combined producer against actual Unity,
retaining every original body/contact reply and verifying joint completeness.
Do not substitute this fixture result for that physical integration. All pending
reports and reviews must precede the separately prepared CUDA host timing run.
