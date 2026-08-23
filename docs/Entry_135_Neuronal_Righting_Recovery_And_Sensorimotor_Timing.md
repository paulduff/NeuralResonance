# Entry 135: Neuronal Righting Recovery and Sensorimotor Timing

## Status

Implemented on 17 August 2026. Code-level verification is complete; the
30-minute predators-suspended observation run remains the final behavioural
acceptance step.

## Observation run

The neuronal brain, articulated avatar, and headless world ran for 9,816.6
seconds, or 2 hours 43 minutes 36 seconds. The authoritative world completed
295,795 ticks at 30.13 Hz. The brain remained connected, all 119 services stayed
online, no world tick failed, and shutdown was graceful.

The avatar travelled only 10.93 metres and visited 8 of 13,462 explorable
terrain cells. It made 645 interaction attempts, all of which were out of
reach. No food, water, device, predator, or other successful interaction
provided an outcome signal.

At shutdown the avatar was inside the starting shelter and mechanically
bracing against it:

- posture: `righting`
- balance error: `1.0`
- support margin: `-0.954 m`
- upright fraction: `0.842`
- left hand load: `72.7 N`
- right hand load: `193.2 N`
- forward speed: `0 m/s`

The run report is preserved at:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\world-runs\world-run-353e8dbd22ca47afb4e7aba111fe6fe3-0000295795-stopped-20260816T212032627Z.json`

## Root cause

The body entered a recovery deadlock rather than a collision lock.

Planar propulsion currently accepts only standing and crouching postures. A
body in the righting phase therefore cannot translate or turn. Righting can
rotate the body toward upright, but completion also requires the extrapolated
centre of mass to return inside the support polygon. In this run the centre of
mass was nearly one metre outside that polygon while the hands were loaded
against the shelter. Rotation alone could not move the support base or centre
of mass far enough to complete recovery.

The resulting causal loop was:

1. wall contact displaced the body outside stable support;
2. the balance plant entered righting;
3. righting disabled planar propulsion;
4. the feet and hands could not reposition the support polygon;
5. recovery could not satisfy its completion condition; and
6. the body remained beside the wall performing repeated righting motions.

This is the observed stationary "yoga" behaviour.

## Timing finding

The stable laptop profile load-shed each control pass to 10 of 238 available
hemisphere instances. Near shutdown the control loop completed roughly 7-10
ticks per real second. A particular structure could therefore wait several
seconds between updates.

The new 96-tick action persistence reduced rapid channel chatter, but its real
duration became dependent on machine throughput. On this laptop it could hold
an action for approximately ten seconds. Sampled action-selection margins
averaged `0.00265`, barely above the `0.0025` activation threshold, and the
selected lane continued to move among many limb and posture channels.

Persistence helped continuity but could also preserve an unsuitable action
long after the physical situation changed. Physiological timing must follow a
monotonic sensorimotor timebase rather than raw scheduler tick count.

## Feedback and learning findings

Physical feedback was largely reliable:

- 178,194 physical-body frames were accepted;
- 13 physical-body frames were rejected, a 0.00729% rejection rate;
- 2,116,644 somatic frames were accepted with no somatic rejection;
- binocular retinal input remained balanced at 78,385 frames per eye; and
- no spontaneous spike dispatch error occurred.

Rejected body frames contained muscle velocity derivatives outside the
protocol range of `[-50, 50]`, most often from tibialis posterior. Rejecting a
complete body frame because one derivative saturated creates an avoidable gap
in proprioception.

The curriculum remained in `perceptual_bootstrap`. Sensory discrimination
reached a 99.58% success rate, but feature binding recorded no success and the
action-outcome stage received no samples. The body was producing movement and
pain evidence without a sufficiently active neuronal reinforcement path to
associate actions with displacement, stability, or failed contact.

## Next-rung objective

Close the neuronal recovery loop so that an unstable avatar can use its
physical effectors to restore support, learn from failed force, and resume
exploration without host-authored movement or an ML controller.

## Implemented corrective work

### 1. Recoverable righting mechanics

- Righting now retains 34% of neuronally requested planar propulsion and
  falling/fallen recovery retains 12%. The host exposes bounded mechanical
  capacity but does not choose a direction, route, limb action, or pose.
- The articulated body no longer zeros bilateral neural drive merely because
  it entered righting. Foot repositioning and hand push-off can therefore move
  the support base when the neuronal motor circuit recruits them.
- Righting completion now requires 180 ms of continuous physical stability,
  preventing threshold chatter between falling and recovery.
- Existing substep contact and support-polygon recomputation remains the source
  of grounded support state.

### 2. Fast neuronal sensorimotor lane

- A weighted fast lane now prioritises retinal, somatic, proprioceptive,
  vestibular, cerebellar, basal-ganglia, motor-thalamic, motor-cortical, and
  spinal instances on constrained laptop passes.
- Stable low-pressure operation selects up to three times the configured
  concurrency budget; pressure reduces that budget toward one concurrency
  window. About 82% is reserved for the fast lane while the general round-robin
  lane always advances when general structures are available.
- This corrects the previous scheduler formula, which unintentionally selected
  the same maximum budget regardless of pressure.
- Normal structure ticks and synaptic routing remain unchanged. Scheduling does
  not create a command or bypass a neuronal population.
- `/api/v1/sensorimotor-timing` and `/api/v1/state` now expose physical-body
  input age, mean/max fast-lane cadence, oldest fast-lane age, and per-instance
  cadence and selection counts.

### 3. Time-correct action persistence

- Action persistence is expressed as 350 ms of monotonic physical time rather
  than 96 scheduler ticks.
- A reciprocal antagonist winner, large vestibulo-reticular balance prediction
  error, or habenula pressure releases the retained action immediately.
- Persistence contributes only a weak near-tie bias and cannot override a
  materially stronger neuronal winner.

### 4. Continuous proprioceptive delivery

- Finite muscle-velocity derivatives outside `[-50, 50]` are saturated at the
  physical ingress boundary and counted in telemetry.
- One saturated derivative no longer rejects the complete body frame; all
  remaining truthful articulation, contact, proprioceptive, vestibular, and
  visceral evidence continues into the brain.
- Non-finite or otherwise invalid physical values still reject the frame.

### 5. Neuronal action-outcome plasticity

- Explicit motor-training mode now permits `action_outcome_association` samples
  during perceptual bootstrap.
- Displacement, support-margin improvement, balance improvement, and existing
  positive physical outcomes contribute to dopaminergic teaching evidence.
- Sustained hand/foot force with negligible motion and no support or balance
  improvement contributes aversive habenular evidence.
- Learning still occurs through existing corticostriatal eligibility traces,
  dopamine/SNc/VTA teaching, and habenular suppression. No host-selected
  preferred action, recovery sequence, direction, route, or ML controller was
  added.

## Verification

The implementation passed:

- 154 focused tests covering righting, articulated movement, physical body
  transduction, world physics, action persistence, closed-loop learning, and
  sensorimotor scheduling;
- the complete test suite: 668 passed, 0 failed, 0 skipped; and
- the complete `NeuralResonanceEngine.DNNE.slnx` Release build with 0 warnings
  and 0 errors.

Static and deterministic verification establishes the new timing, scheduling,
feedback, and mechanical contracts. It does not replace the next live
observation run.

## Neuronal-only boundary

This rung must not introduce machine learning, scripted locomotion, navigation
rules, pose sequences, semantic motor commands, or host-selected recovery
actions. The host may enforce anatomy, collision, conservation, force limits,
joint limits, and timing. The neuronal network must select and learn every
movement.

## Acceptance criteria

1. A 30-minute predators-suspended run completes with no service or world tick
   failure.
2. Physical-body frame rejection remains zero, including during collision and
   rapid ankle reversal.
3. Sensorimotor structures update every fast-lane pass and report bounded input
   age.
4. Action persistence has a bounded physical duration independent of machine
   throughput.
5. When support margin becomes negative, the body either falls truthfully or
   returns the centre of mass to stable support; it may not remain indefinitely
   in righting.
6. Sustained hand contact followed by zero displacement produces local pain,
   cerebellar prediction error, and reduced eligibility for the ineffective
   action lane.
7. Action-outcome association receives non-zero samples during motor training.
8. The avatar leaves the starting shelter, visits new terrain, and does not
   spend more than 30 continuous seconds braced against one surface.
9. No host-authored or ML movement authority is present.

Criteria 2, 3, 4, 7, and 9 have direct automated coverage. Criteria 1, 5, 6,
and 8 require the next predators-suspended live run and log analysis before
this rung can be considered behaviourally accepted.

## Expected result

The avatar should no longer freeze in righting beside a wall. It should receive
timely body evidence, recruit a physically possible neuronal recovery, learn
that sustained force without displacement is ineffective, and eventually
discover another action without being told which action to choose.
