# Native motor/body bridge

The corrected native hinge capture `native-muscle-hinge-20261116-5a98f8` passes
75 regressions and 24 single-step native cases. Its verified review is in
`artifacts/native-hinge-review-20261116`; the original mechanical bounds remain
unchanged. The analytic causal motor comparison `2acbfd` separately qualifies
the existing explicit muscle/body partition against independent numerical
references. Neither capture couples that plant to actual Unity physics.

This next isolated experiment implements that connection. It selects no live
DNNE, Avatar, World, editor or old save. It uses synthetic motor events, not a
running motor neuron, and is not a gait or balance controller.

## Native fixture and causal ownership

`MuscleHingeBridge` runs alone in an empty Unity scene. It has the previously
checked fixed root and one revolute child (2 kg, central isotropic inertia
0.15 kg m², local centre of mass y=0.3 m). Gravity, friction, damping and joint
drives are disabled for this diagnostic. Two opposing straight muscle paths
attach at child y=0.4 m and root y=0.3 m. Root z offsets are chosen to match the
qualified muscle reference's initial total path length, then represented as
native floats. The actual rounded coordinates are retained and fingerprinted.
This is synthetic geometry, not anatomical attachment calibration.

The existing `AvatarCausalMotorPlant`, `AvatarDynamicMuscle`, motor transport and
calcium model remain unchanged. Each physical interval applies the tendon
tensions already available at its start. Unity returns the resulting measured
path lengths and point velocities. The plant advances muscle state with its
existing causal partition and exact event boundaries, then observes that endpoint.
No future body samples, target-angle servo, duplicated fatigue stage or additional
artificial activation process are introduced.

Unity owns every `Physics.Simulate` call. Initial registration uses a frame yield
with scripted physics, zero physical steps, zero angle and zero speed. There is
no hidden warm-up. The physical interval is the actual promoted Unity float
`0.0001250000059371814` seconds, represented in milliseconds in the shared clock.
The plant uses two muscle substeps because even this nominal 0.125 ms step is
slightly greater than the muscle solver's maximum when represented exactly.
Each case has 1,024 steps, ending at approximately 128 ms; integer steps own time.

The file bridge preserves every immutable request and reply. Both sides check
session, sequence, body geometry identity, plant binding, current physical step,
exact start/end times and state hash. Each effort names both muscles exactly
once with finite nonnegative tendon tensions and a fresh effort ID. Complete
native geometry and the applied effort receipt accompany every endpoint.
Read-only captures re-inspect native state; they do not just replay a cached
measurement. No physics runs while waiting for a command. Any uncertain outcome
faults the epoch; physical commands are never retried. All force conversions are
validated before forces are queued. The native failure record distinguishes a
rejection before physics from an attempted physical advance.

The shared wire source is linked into the .NET probe. This is an isolated
diagnostic transport, not production distributed transport or a speed benchmark.
It does not authenticate an external actor, provide checkpoint recovery or
qualify arbitrary remote JSON. Its strict host parser, native field checks and
retained evidence serve this controlled local experiment.

## Prospective experiment

The visible PowerShell runner first executes the existing 75 geometry/motor
regressions, including solution coverage. It builds a separate Unity player and
the .NET `NativeMotorBridgeProbe` from frozen source. The native cases are:

- Quiet: both declared motor sources explicitly complete without events.
- Left: two fixture events arrive at 10.03125 and 60.03125 ms in muscle m0.
- Right: the same events arrive in muscle m1.
- Balanced: both muscles receive the same events.

Every case proves initial pause and waiting for neuronal completeness before
movement. All 4,096 native steps, eight fixture arrivals, muscle states, force
commands and endpoint observations must be retained. Final pause must preserve
state. Quiet and balanced angles must remain within 1e-5 rad; left and right
responses must have the expected signs and exceed 1e-7 rad; their mirror sum
must be within 1e-5 rad. These are qualitative response controls, not coupled
trajectory accuracy criteria.

Eight additional fresh native sessions intentionally send wrong session,
sequence, time, state hash, plant binding, duplicate muscle identity, negative
tension or force attached to an inspect command. Host prevalidation is bypassed
only for these declared controls. The actual native boundary must reject each
for the declared reason, exit with failure, preserve state and execute zero
physical steps. Such expected rejection does not count as a failed experiment.

`analyse-native-motor-bridge.py` independently reads the saved files. It checks
all commands, receipts, physical clocks, complete event identity and time, and
native path geometry. It recomputes individual native torque impulses and angle
envelopes using the previously fixed 2e-6 rad/s and 2e-6 rad plus truncation
bounds. It checks point/linear velocity and root pose with the prior unit-specific
2e-6 bounds. Matching recorded path arithmetic has a separate 1e-12 bound.
It does not integrate a model or certify long-trajectory error.

## Evidence and next requirement

Preparation requires clean pushed source, the verified hinge and analytic
motor/muscle reviews and their D: copies, unchanged qualified cores, pinned
Unity/Python executables and valid asset metadata. It freezes the qualified
muscle parameter input. Source, imported build inputs, test/probe/player binaries,
commands, native states, muscle traces, logs and analysis receive a verified D:
backup. The PowerShell window stays visible; Orion does not monitor it.

Authoring compilation of both the native bridge and .NET driver succeeds with
zero warnings/errors. Python/PowerShell syntax and asset metadata checks pass.
No new test or simulation result is implied by these static checks. Consult the
launch record for pending work.

After command/event/force integration passes, compare the complete native coupled
trajectory with an independent reference using the actual nonlinear attachment
geometry and physical step refinement. Numerical accuracy, measured sensory
feedback, anatomical muscle paths, afferent physiology and whole-body recovery
remain open. This fixture is not eligible for live Avatar promotion on its own.

## Launch record

`native-motor-bridge-20261116-223098`, frozen revision
`3d87882a8dc2b4d83fbac76350e2f0e4cf3d20a4`, was launched in independent visible
PowerShell task `DNNE-Native-Motor-Bridge-c5b38826821e`. All 1,617 initial files
plus manifest were verified on D: and rechecked by the launcher. The preparer
verified both prerequisite reviews, unchanged qualified cores and all 48 asset
metadata files / 35 scripts. This is the only pending completion report.
No test, Unity build, native run or analysis outcome has been inspected after
launch; do not monitor. Wait for the operator to report completion.

## First bridge failure: exact clock rejected after nine quiet steps

The operator reported `223098` FAILED. All 75 regressions and the native Unity
build passed. The driver completed nine stationary quiet steps, then native
request 33 (the tenth physical step) was rejected by the combined effort-ID /
endpoint-time check. The failure record says physics was not attempted for that
request, and its observed native state exactly matches the preceding reply.
No one-sided response case, malformed-command case or completed bridge analysis
was reached. The .NET driver's unhandled exception produced `-532462766`.

`review-native-motor-bridge-failure-20261116.py` verifies all 2,449 C:/D: files
(192,112,136 bytes), all 1,610 frozen Git files, test/probe/player binaries,
all 35 compiled Unity C# inputs, metadata and exact 75-case regression coverage.
The capture manifest is
`38E1488428D9C89820B917DD772459CEF6329856359C9311CD28C9D742891568`.
The one-file derived review `artifacts/native-motor-bridge-failure-review-20261116`
has its own verified D: copy. The failed package remains untouched.

The saved request has a fresh valid effort GUID and the exactly correct endpoint
`1.2500000593718141` ms, binary64 bits `3FF400000FF00000`. The original failure did
not record Unity's decoded request bits, so decimal decoder disagreement is a
specific hypothesis, not yet an established cause. No physical or clock bound
is changed to accommodate it. The old package is no longer pending.

## Version 2 wire and native diagnosis before retry

The shared wire now represents each mechanical scalar as a canonical 16-digit
uppercase IEEE-754 hexadecimal string inside a `bits` field. This includes
start/end times, step duration, tendon tensions and measured lengths/velocities
in both directions. The native rest-length argument uses the same exact format.
The wire version changes from 1 to 2; muscle dynamics, event timing, integer
physical steps, geometry, forces, anatomical assumptions and acceptance bounds
are unchanged. Decoding rejects malformed encodings, infinities and NaNs. Signed
zero and finite subnormals retain their original bits. Ordinary muscle traces
still contain readable numeric values. Driver failures now exit 1 with the error
and evidence path rather than embedding entire body observations in an unhandled
exception.

Before any physical retry, the native player runs without physical bodies:

- Replays the exact original failed request through Unity's decimal parser and
  records the parsed start/end bits, expected bits and both effort-ID predicates.
  It must reproduce the endpoint decoding mismatch while retaining the correct
  start time and valid fresh ID. Otherwise it stops and preserves the diagnosis.
- Checks all 1,025 represented clock endpoints plus 15 scalar cases, including
  the original tension, initial path length, adjacent representable values,
  signed zeros, subnormals and finite extremes. Every bit pattern must survive
  host-to-native decoding and native-to-host return: 1,040 exact round trips.
- Rejects ten malformed/nonfinite scalar encodings on both sides. Extreme values
  exercise only the codec; they are never physical force inputs. Decimal parser
  diagnostics are separate and do not require decimal handling of subnormals or
  double extrema.

Only after those checks pass does the original four-case muscle experiment and
eight native command-rejection controls run. The independent Python audit checks
the encoded forms, exact original request, complete round-trip matrix and decoded
physical evidence. This is a candidate transport repair, not a new tolerance.
Both authoring builds pass with zero warnings/errors; syntax and metadata checks
pass. Native diagnosis, codec acceptance and physical retry await execution in
the independent visible PowerShell window. Consult the next launch record.

Unity's [FromJson documentation](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/jsonutility/fromjson)
identifies its serializer, but does not prove this numerical diagnosis. The
retained original request and native replay determine the result.

The fresh retry `native-motor-bridge-20261116-bd7079`, frozen revision
`46eaa518c56764bd0ac8491ac9d5c4209162f534`, is launched in independent visible
PowerShell task `DNNE-Native-Motor-Bridge-769bbf16dc61`. All 1,622 initial files
plus manifest are verified on D: and rechecked at launch, including the original
failed request/reply and verified failure review. This is the only pending
operator report. No test, native preflight or physical outcome has been inspected
after launch. Do not monitor or infer that the candidate repair has passed.
