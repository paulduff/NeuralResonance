# Entry 157 - Tonic Posture and Emergency Righting Release

Date: 2026-08-23

## Status

Implemented and verified. The complete DNNE stack remains stopped pending the
next observation run.

## Triggering evidence

The Entry 156 observation ran for 4,131.55 seconds. The brain remained healthy
at tick 19,469 with 119 services, no non-OK services, and live sensory input.
The world advanced to tick 124,817 and received 192,721 neuronal motor
dispatches. The avatar nevertheless travelled zero metres, visited one terrain
cell, and produced no locomotor or manipulator dispatches.

Physical telemetry showed a stable upright body for 4,129.21 seconds, full
double-foot support for the complete run, no swing phase, no collisions, and a
final balance prediction error of zero. The only continuously active motor
lanes were standing and head orientation. Three energy-depletion deaths and
automatic respawns occurred because the stationary avatar could not forage.

The final motor frame showed that the emergency righting latch entered at brain
tick 35 and never recovered. Tonic bilateral righting evidence remained at
approximately 0.227 from proprioceptive and vestibular afferents and 0.507 from
descending spinal support even after physical balance recovered.

## Root cause

The emergency righting latch correctly required large balance error and
bilateral righting recruitment to enter. Its release incorrectly required both
low balance error and righting drive below 0.04.

Ordinary standing continuously excites the same stand populations used by the
righting pathway. The righting drive therefore has a physiological tonic floor
well above 0.04. Once entered, the latch could never satisfy its release
condition. It suppressed every voluntary action selected by the neuronal basal
ganglia circuit, while allowing tonic standing and orienting to remain active.

The cumulative action-authority telemetry compounded the diagnosis by counting
candidate channel grants inside the action-selection circuit as body-output
authority even when the righting latch blocked the selected channel. WorldSim
also read `actionAuthorityHistory` case-sensitively while the ControlProgram
state serialized it as `ActionAuthorityHistory`, causing final run reports to
lose the history entirely.

## Repair

1. Preserve the dual neuronal requirement for entering emergency righting:
   bilateral righting recruitment plus a large physical balance prediction
   error.
2. Treat sustained low balance prediction error as the recovery signal.
   Tonic proprioceptive, vestibular, and spinal standing activity remains
   available but can no longer hold the emergency latch indefinitely.
3. Require four consecutive stable neural updates before release. Renewed
   instability resets the recovery counter and retriggers righting.
4. Keep the one-update fresh-selection boundary after recovery so a stale
   pre-fall action cannot resume automatically.
5. Report the emergency latch as the final motor-authority reason while it is
   blocking output.
6. Count cumulative authority only when the selected neuronal channel reaches
   the motor-output boundary. Candidate action traces remain available for
   circuit diagnosis but are not mislabelled as body authority.
7. Read ControlProgram object properties case-insensitively at the WorldSim
   frame boundary so cumulative authority survives into rolling and final run
   reports.

No scripted movement, host-selected action, ML model, semantic steering, or
non-neuronal behavioural authority is introduced.

## Acceptance

- A large physical imbalance with bilateral righting evidence enters the
  emergency latch and suppresses unrelated voluntary movement.
- Stable balance releases the latch after four neural updates even while tonic
  postural drive remains above 0.04.
- A fresh neuronal locomotor winner reaches the avatar after recovery.
- Renewed instability before recovery resets the stable counter.
- Candidate basal-ganglia grants blocked by righting are not counted as motor
  output authority.
- Pascal-cased ControlProgram authority history is retained in WorldSim run
  telemetry.
- The focused tests, complete suite, and Release solution build pass.

## Verification

- Focused motor, authority, and WorldSim tests: 104 passed, 0 failed.
- Complete DNNE test suite: 830 passed, 0 failed.
- Release solution build: succeeded with 0 warnings and 0 errors.
- Live observation acceptance remains to be recorded after the next run.

## Next observation

Start from a fresh generation-three network state. The run succeeds when the
righting latch enters and recovers, locomotor dispatch becomes non-zero, a foot
enters swing phase, distance and visited terrain increase, and the world report
contains non-null brain action-authority history. Balance protection must still
re-enter during a genuine disturbance.
