# Physical sample clock and delivery candidate — 9 November 2026

The [sensory audit](DNNE_Sensory_Boundary_Audit_2026-11-09.md) found that the
existing sensory path mixes physical capture time, wall time and neural delivery
time. Its generated input events also depend on incoming frame cadence. This
candidate provides an isolated lane for original body measurements, before any
receptor dynamics or afferent spike generation. It is implemented in
`Shared.Contracts/PhysicalSampleProtocol.cs` and
`src/NRE.SimAvatar/AvatarPhysicalSampleTransport.cs`.

## Clock and observation contract

A stream declares session, body and producer identities, initial physics step,
fixed step duration, physical epoch and corresponding neural epoch. Integer
physics steps are authoritative. Both mapped timestamps are calculated from
those epochs and the step count; wall time never sets the mapping. Adjacent
steps that cannot be distinguished in either double timestamp are rejected.
The configured duration must describe the producer's actual physics step,
including its float representation where applicable. This does not establish
the accuracy of the physical integrator or exact floating-point elapsed sums.

Every grid sample is required, starting with the initial state. The existing
`PhysicalBodyFrameRequest` is serialized once, including nested muscle arrays,
and retained as immutable text with its digest. Its legacy timestamp and
sequence remain capture metadata. Decoding produces a new object graph and
does not restamp, rectify, smooth or reinterpret measurements. The source must
capture from a stable observation under single ownership; concurrent mutation
during capture is outside this API's contract.

Stable sample identity includes the stream's full clock fingerprint and physical
step. Different bodies, sources, sessions or clock mappings cannot silently
share this stream. Nonfinite serialized measurements, inconsistent origins and
oversized observations are rejected. Physiological plausibility, measurement
units and receptor-specific domains still require separate validation.

## Delivery and bounded storage

The source retains all original observations and published packets until an
exact acceptance receipt arrives. Sequence gaps, changed replays, expired
receipts and silent epoch changes are rejected. A receiver validates every
sample in a packet before committing any of it. Repeating an accepted packet
returns its original receipt without adding or consuming observations again.

Counts and payload bytes are bounded at both ends. An overflowing receiver
rejects the entire packet; the source retains it for retry. Packet size must
be compatible with receiver capacity at setup: an oversized immutable packet
cannot be rewritten under the same sequence after uncertain acceptance. The
source conservatively checks space for the largest permitted observation. Its
single owner must check before advancing physics; this class does not itself
reserve a concurrent slot, pause Unity, drive physics or recover a lost sample.

Completeness means all captured samples through a declared physics step. It
does **not** mean every afferent event or physical event between samples is
known. Empty packets cannot assert missing observations or silence. Consumption
returns original samples in order, never interpolated measurements or invented
spikes. Acceptance is not receptor consumption or durable storage. These
in-memory objects have no restart journal, network endpoint or checkpoint.
Unexpected failures during a state commit fault the object and prevent continued
use of that epoch; they do not claim transactional crash recovery.

## Prospective checks and execution

`PhysicalSampleTransportTests` contains 22 checks: explicit epoch mapping,
wall-time independence, nested mutation isolation, fixed-grid preservation under
different packet sizes and retries, missing traffic, gaps and reordering,
whole-packet validation, stream isolation, altered timestamps/identity, source
and receiver backpressure, byte limits, receipt history, nonfinite payloads and
separation of acceptance from consumption. The solution manifest check makes
23 expected cases. Packet experiments retain the same 61 physical samples;
they do not vary actual physical sampling resolution.

The preparer freezes clean pushed source. The visible independent PowerShell
runner verifies the initial C:/D: copies, builds under frozen source, records
binary hashes, runs exactly those 23 cases, checks the TRX and source/binary
hashes, then preserves the outcome with a verified D: backup. A completed
transport check is not physiological qualification. The full DNNE unit suite,
an active neuronal model and a live service are not run by this window.

The focused checks may run alongside the pending Golgi accuracy comparison.
Both operator completion reports are required before launching the prepared
CUDA cost benchmark, which requires no competing studies or builds. Preserve
the subsequent CUDA cost → motor scheduling → gated somatic motor-unit order.
Never inspect live output to infer completion.

## Integration boundary

No live caller selects this candidate. Existing Avatar mechanics, muscle,
tendon, vestibular and plantar mechanisms, Unity capture, legacy transduction,
editor labels and saved brains retain their current behavior. The world still
starts paused. No UI claim of a repaired sensory or neuronal authority pathway
is justified by adding this API.

After the transport results are reviewed, receptor mechanics and actual
afferent membrane crossings need their own numerical and physiological gates.
A future physical producer must capture every declared step with explicit
backpressure and map its clock to the consuming neuronal scheduler. Closed-loop
coupling, source completeness between samples, dynamic motor output and actual
Avatar recovery remain open. Transport alone cannot supply those mechanisms.

## Preparation status

The candidate and its test assembly build; runtime checks are prospective until
the operator reports the frozen PowerShell outcome and its evidence is reviewed.
