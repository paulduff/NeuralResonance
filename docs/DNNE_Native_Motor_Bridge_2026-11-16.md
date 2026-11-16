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
