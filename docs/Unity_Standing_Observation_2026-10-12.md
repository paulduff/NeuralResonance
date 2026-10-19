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

## Completed corrected-feedback observation

Run `unity-standing-20261012-160513-8ed073` completed 60.01 simulation seconds
with finite physics and passed integration checks. It fell at 1.41 seconds,
accumulated 0.77 seconds of standing, and did not recover. All 279 captured fallen
samples now report physical balance error one. All 286 saved body frames passed
offline validation/transduction without projection or missing muscle identities.
After falling, neural leg actuation continued for 5,277 physics steps, using 404
distinct authorized motor ticks. Correct encoding alone did not restore recovery.

The saved motor journal contains 455 advancing responses marked `running=true`.
None reports an active righting latch. Rounded afferent righting drive reached
0.804, but descending drive reached only 0.030, below the 0.040 trigger floor.
The logged population balance-error estimate remained 0.000. That estimate is
computed from mean vestibular, vermis and spinal firing; it is a different
quantity from Avatar's measured physical balance error. These observations
identify weak descending righting output, without proving its cause.

The post-trial transport snapshot reports limits of six source spikes per service
and 160 total per tick, with 4,641 budget drops among 4,719 drained spikes on that
single tick. Fanout means dispatched counts cannot be subtracted as source counts.
The bounded routing trace covers ticks 6,994,398–6,994,459; the active motor journal
covers 6,992,869–6,994,354. They do not overlap. Low-index routes in that export
are evidence about the post-trial window, not the actual falling interval.
Code inspection confirms that FIFO prefix selection favors low-index neurons
under saturation. Whether those omissions caused this fall remains unverified.
`tools/analyse-unity-recovery.py` records these ranges and evidence limitations
in `trace-analysis.json`, preserving the original captures.

## Prepared higher transport allowance

This records the 12 October preparation. The 19 October startup attempt below
showed that `headless` is unsuitable for this live-body comparison. Use the new
`-BrainProfile embodied` setting for the higher spike allowance with stable timing.

The operator requested a higher allowance for this desktop, then explicitly
requested code preparation without another run. The optional launcher argument
`-BrainProfile headless` selects the existing profile with 24 source spikes per
service and 1,536 total per tick. The Unity player still opens visibly and paused;
the profile name refers to DNNE runtime settings. Omitting the argument preserves
the current profile. No default application settings are changed.

When this future window runs, it records the original profile and automatic policy,
temporarily disables automatic profile selection, and applies the requested
profile with `RestartSimulation=false`. It saves the acknowledged settings and
restores the original automatic-policy enabled flag in `finally`, including caught
failures. Forced window termination cannot guarantee that restoration. The selected
performance profile itself remains applied. This profile also changes concurrency,
timeouts and snapshot cadence, so the comparison is not a transport-only ablation.

`transport-telemetry.jsonl` samples before/after the trial and approximately every
five simulation seconds from the existing independent PowerShell progress loop.
It records configured caps and effective caps, backpressure, queue errors and
delivery latency from coordinator diagnostics. Counters describe a single tick,
not cumulative trial totals; profile and state reads are adjacent, not atomic.
Admission still does not prove end-to-end sensory delivery. No live agent
monitoring is required. No new trial or profile change was made on 12 October
after this preparation. DNNE and the editor were gracefully shut down at the
operator's request; the checkpoint and final persisted synapses were verified on D:.

## 19 October restart and transport-profile correction

Before restart, all 239 local persisted synapse files matched the verified shutdown
backup. The previous Unity player and managed assembly hashes also matched. DNNE
started with 119 healthy service types and the editor paused. The initial attempt,
`unity-standing-20261019-064354-e2e4a3`, passed all 19 mechanical axes and 40 native
samples, but stopped before operator Play: zero physics steps and body frames.
Its report states that motor telemetry or lease readiness was not established.
This is a commissioning failure, not a standing/recovery result or evidence that
muscles were absent. The launcher now surfaces that report error before checking
muscle captures from an incomplete observation.

The prepared `headless` profile changes more than spike capacity. Its RPC
concurrency of 72 also controls the tick scheduler's participant budget: up to
144 instances during warmup and 216 afterward at zero pressure, versus stable's
20/30. A saved post-failure sample has 144 active instances, a 2.05-second tick,
2.04-second median and 4.16-second 95th percentile. Unity's existing motor-freshness
limit is 1.25 seconds; the journal contains sparse advancing motor packets while
paused. The broader scheduling change makes this an unsuitable way to isolate
the intended transport comparison. The saved timing is not a proof that spike
capacity alone caused the delay.

The new explicit `embodied` profile derives from `stable`, changing only its name
and source-spike limits to 24 per service and 1,536 total per tick. RPC concurrency
remains 10, dispatch concurrency 32, snapshot cadence 12 ticks, and all timeouts,
query cadence and direct-step settings remain stable's values. The default profile
is unchanged. Existing live switching uses `RestartSimulation=false`; automatic
profile selection is temporarily pinned by the observation window as before.
No body, neural decoder, sensory encoding or freshness threshold is changed.

All 53 focused profile, actual scheduler and standing-measurement tests pass.
Regression coverage checks the complete non-spike settings and actual selected
instances during both warmup and normal operation. Current synapses and coordinator
state were gracefully saved and SHA-verified on D: before restarting the rebuilt
coordinator. The earlier shutdown backup remains intact. Transient neuron state
is not fully checkpointed; this is a fresh process startup using persisted synapses.
The next 60-second comparison uses the same Unity body, starts paused, and runs
in an independent visible PowerShell window. Recovery quality remains unassessed.
