# DNNE / Unity standing observation — 12 October 2026

The isolated physical body passed all 19 bidirectional axes and 40 independent
native-coordinate samples on revision `550a4db`. The next rung observes actual
DNNE output with the unanchored Unity body on the same flat floor. This is a
bounded commissioning observation, not a claim of learned standing or a long
training curriculum.

Launch `tools/start-unity-standing-window.ps1` with the newly built player. Its
independent visible PowerShell window first repeats the full isolated mechanical
qualification. It refuses the live trial if that prerequisite or DNNE service
health fails. Before any Unity sensory traffic, it saves read-only motor/timing
telemetry and a coordinator network export, and verifies their D: copies. The
export endpoint briefly quiesces/resumes the coordinator. It does not export
every service's neuron/synapse state and is not described as a complete brain
checkpoint. No restart, network import or brain reset is requested.

The visible Unity player starts **paused**, acquires the exclusive world lease,
and drains legacy WorldSim sensory traffic for ten seconds. Fresh motor ticks and
lease acknowledgments are required before Play is enabled. The operator presses
Play to begin the default 30 simulation seconds. The default preview remains
paused/disconnected unless this explicitly selected trial mode is used. CIV is
independent. DNNE remains the brain, Avatar the body/nervous system, and Unity the
environment.

No host chooses a standing action, injects fixed motor recruitment, anchors the
pelvis, teleports the active body, adds balancing forces, resets an episode or
plays an animation. The physical plant consumes the same confidence-gated DNNE
motor telemetry, canonical spinal population and bounded actuators as the
ordinary preview. Natural physical, retinal and contact feedback reaches the
existing input APIs. Normal neuronal plasticity remains possible; this trial
does not change the motor-training flag or explicitly freeze learning.

At 100 Hz, measurements record upright fraction, pelvis height, foot and other
support, dynamic support margin, joint-limit violation, authorized motor ticks and
actual lower-limb actuation. Five-Hz JSONL traces retain raw observed motor output,
the command actually applied, its age, body-request duration and physical frames.
Admission/generated-spike counters are distinct from deferred neuronal delivery;
`sensoryDeliveryVerified` remains false. Coordinator/timing exports before/after
support later diagnosis without fabricating proof of receptor delivery or learning.

The predeclared quiet-standing sample criteria are:

- Upright fraction at least 0.85 and pelvis height at least 70% of its initial height.
- Each foot supports at least 10% of body weight, with their combined load at least 70%.
- Other support carries at most 5% of body weight.
- Dynamic support margin is nonnegative and joint-limit violation stays below 0.05 rad.

The standing criterion additionally requires ten continuous qualifying seconds
at the end, no larger limit violation during the trial, at least two authorized
motor ticks and some actually applied neural lower-body actuation. Passive support
alone cannot satisfy it. These are engineering acceptance bounds, not anatomical
or biological calibration. Natural weight transfer is reported if supported
standing accumulates at least 0.5 seconds with each foot carrying 60% of foot load.
There is no commanded weight-shift sequence. `learnedStandingAssessed` and
`walkingAssessed` remain false even when these short measured criteria pass.

The integration checks require a completed finite run, fresh motor packets during
the trial, accepted body frames that generated receptor spikes, accepted retinal
frames, and an acknowledged lease release with the final world paused. Confidence
rejection or zero neural actuation may produce a completed integration observation
with a failed standing criterion. Lost motor/lease freshness, non-finite physics,
180 seconds of wall time after starting, or operator closure stops the observation.
Waiting for Play is limited to 20 minutes; the PowerShell window has an independent
25-minute overall bound.

At completion, outstanding sensory requests drain, the world pauses/disconnects,
and its final view stays visible. The script saves reports, traces, coordinator
exports, mechanical prerequisite files, logs and SHA-verified D: backups. It
prints FINISHED for completed integration regardless of standing quality, which
is printed separately. The user reports completion; no live agent monitoring is
needed. The original runtime result was reviewed before committing its checkpoint.

## Canonical muscle and recovery observation

The later `-Muscles` launcher switch explicitly chooses the measured physical
plant with 38 original Avatar muscles; omission retains the earlier simplified
joint-torque baseline. Recruitment, activation/fatigue and strength remain canonical
and nominal. Successful isolated triple-capacity pool diagnostics do not enter
this live baseline. `trial-configuration.json` records the selected mode, player
and assembly SHA-256 hashes, duration and recovery bounds and is verified on D:
before connection. The final muscle capture check requires 38 unique identities
in every saved body trace sample.

The next observation lasts 60 simulation seconds and continues after falling.
Recovery is measured at 100 Hz, independently from the original whole-run standing
criterion. A fall is upright fraction below 0.3 or pelvis height below 40% of its
initial height. Recovery requires ten continuous seconds meeting all per-sample
standing support/height/upright/local-limit bounds, at least two distinct authorized
motor ticks and observed neural leg actuation during that supported interval.
Earlier neural action cannot qualify passive support after a fall. First fall and
first completed recovery times, fall/recovery counts and post-fall actuation/ticks
are saved. The final recovery criterion also requires a valid supported neural
standing interval at completion. Historical limit strikes do not invalidate later
local recovery, while non-finite physics invalidates the result. No observed fall
means recovery was not tested. Nothing in these measurements commands the body.

This is a bounded DNNE/body integration and recovery observation. It does not
promise learned recovery: the original Unity migration still omits four axial
muscles and crouch/sit/lie decoder transitions. Reports retain the distinction
between observed behavior, API admission and verified neuronal sensory delivery.
The Unity view starts paused, waits for operator Play and stays open paused after
completion. No brain restart/reset/import or live agent monitoring is requested.

## Balance feedback and saved motor evidence

The first canonical-muscle live run fell at 1.50 seconds and did not recover.
Its margin-only balance encoding returned zero for most fallen samples supported
by the floor. The repaired encoding uses Avatar's original phase/tilt/margin
function: fallen remains error one regardless of a positive contact support
margin. Raw physical fields are retained; the simpler Unity phase classifier has
no instability timer or control allowance. The unchanged 60-second observation
retests this feedback with the existing brain state and muscle plant.

`motor-telemetry.jsonl` saves full advancing valid responses from the existing
motor polling loop. Each record contains `physicsStep`, `realtimeSeconds`,
`running` and `responseJson`; deserialize that last string to inspect DNNE's
evidence, posture channels and recovery latch. Paused pre-trial responses may
appear, so use `running` and physics step to select the active observation.
No extra polling or agent monitoring is required. Motor evidence and API acceptance
still do not prove causal recovery or complete sensory delivery.
