# Complete body and contact observations

Current status: the original 65-check window was reported failed and reviewed.
All 24 failures share one computed-property JSON metadata defect. The narrow
repair and two additional checks build; the fresh 67-check PowerShell retry
`014b85` passed all 67 checks and its evidence review. See the result and repair
section below. Earlier preparation
and launch descriptions record what was known at that time.

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

## Launch record — 13 November 2026

The focused check package is `physical-observation-20261113-8c18b7`, frozen
revision `f263abf83675c53f26a804648794adf8bcffc767`, launched in independent
visible PowerShell task `DNNE-Physical-Observations-026be2d85734`. Its 1,512
initial files plus manifest have a verified matching D: copy under
`D:\DNNE-desktop-runs\UnityWorld\physical-observation-20261113-8c18b7`.
The launcher rechecked both copies before starting. No outputs are inspected
until the operator reports completion; no pass is inferred from launching it.

Three reports are now pending: this 65-check window, actual Unity body sampling
`unity-physical-sample-20261113-1ff02e`, and corrected finer Golgi
`golgi-sdirk3-fine-20261113-ca813f`. CUDA host timing remains held until all
reports and evidence reviews are complete. This window does not run Unity,
DNNE, a receptor model or any new physical solve.

## Reported failure and serialization repair

The operator reported `physical-observation-20261113-8c18b7` failed: 41 passed,
24 failed, 65 total. The full review verifies all 1,903 files in each C:/D: copy
(92,712,589 bytes), executed test binaries, all 1,509 frozen Git source files and
the exact test identities. Every failed case is in `PhysicalObservationTests`
and reports the same exception: computed getter-only `NeuralSubstrateSupply.IsValid`
was marked required without a setter. The metadata is constructed even when the
optional supply value is null, preventing serialization before the affected
behavior assertions can run. All 39 prior body/manifest regressions passed;
only two of the 26 new combined cases passed. Combined behavior is unqualified.

The failure capture manifest SHA-256 is
`AB1A8E6932C184C5A11AF2DAEF5D79C5D43C143BE433906E0725324D25EE4AD2`.
`tools/review-unity-and-contacts-20261113.py` records the complete failure list in
`artifacts/unity-contact-review-20261113/contact-failure-review.json`. This review
has a verified D: copy. The original failed source, status and evidence remain
unchanged. The separately reported actual Unity body study passes its review.

The repair removes exactly the computed `IsValid` property from the combined
protocol's JSON metadata for `NeuralSubstrateSupply`. It still requires each
actual supply input (glucose, oxygen, perfusion and source), every other body or
contact member, and explicit nulls for optional values. Unknown fields remain
forbidden, including a forged transmitted validity flag. Decoded validity is
derived from the retained measurements. Finite out-of-range values remain visible
as invalid observations; transport does not clamp them or certify physiology.
The shared supply type and existing body-only protocol are unchanged.

Two added cases cover non-null and null supply snapshots, exact preservation,
derived validity for valid and out-of-range observations, removal of each actual
input, missing whole optional-field declaration, and forged validity despite a
matching payload digest. The retry requires **67 tests**: 28 combined, 16 body
producer, 22 body transport and one manifest. Both the test assembly and Unity
.NET adapter build with zero warnings/errors after the repair. Tests execute
only in a fresh frozen visible PowerShell window; their outcome is pending.

Once the retry passes review, exercise complete body/contact delivery against
actual Unity using original per-step evidence. Corrected Golgi `ca813f` still
awaits its own operator report and remains uninspected. Any new retry also gates
exclusive CUDA timing until its completion report and review.

The first repair package `physical-observation-20261113-205752` (revision
`900005780a0039a25e03f33cf7b2d54a1ddf7da9`) was rejected before task creation:
one launcher guard still expected 65 despite the new 67-case configuration.
No launch or window-status file exists and no tests started. Its initial 1,514
files plus manifest were verified on D:; the explicit setup rejection is also
preserved. Correct the guard and freeze a fresh package, leaving this source
unchanged. This is a setup failure, not another test result or pending window.

## Retry launch — 13 November 2026

The corrected package is `physical-observation-20261113-014b85`, frozen revision
`8a273337542e83433ee92be53a6fb07fee00d233`, launched as independent visible
PowerShell task `DNNE-Physical-Observations-4fc27636ac75`. Its 1,514 initial files
plus manifest have a verified D: copy, rechecked before launch. All 67 checks
remain pending the operator's completion report; no live output is read and no
pass is inferred from starting the task. This window and corrected Golgi
`ca813f` are the two outstanding reports. CUDA timing remains held.

## Retry result and verified review

The operator reported `014b85` complete: all 67 tests passed. The review verifies
all 1,905 files in each C:/D: copy (92,351,519 bytes), all 1,511 frozen Git files,
executed test binaries and all exact test identities. This includes 28 combined
cases, 16 body-producer cases, 22 body-transport cases and the manifest check.
The supply serialization repair and required/missing/forged-field checks pass.
The manifest SHA-256 is
`23F537B01A92210E3741F98EEA56A922287FEE28E9B7040D3D5076D93ADB0BDF`.

`tools/review-golgi-and-contacts-20261113.py` records this in
`artifacts/golgi-contact-review-20261113/contacts-review.json`; the three-file
review has its own verified D: copy. No tests or models ran during review. This
establishes the tested fixture/transport behavior, not combined actual Unity
delivery or afferent physiology. The next physical check must transport original
body and all 15 contact observations together from the same actual Unity endpoint.
The operator's re-pasted `8c18b7` failure is the already reviewed old capture.

Corrected Golgi `ca813f` also passed its review. Both reports are resolved. Run
the prepared CUDA host timing study exclusively before another physical or
spindle experiment; no live brain/body/editor path is promoted by these checks.
