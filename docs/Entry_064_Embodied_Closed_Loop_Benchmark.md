# Entry 064 - Embodied Closed-Loop Benchmark

Date: 2026-08-05

## Purpose

This rung tests Dyad's intended embodied data path as an executable loop:

`brain -> avatar -> simulation -> avatar -> brain`

The benchmark uses the production `SimulationState`, `AvatarService`, motor-spike integration, avatar kinematics, body-state factory, outcome factory, object-observation queue, action memory, and dopamine-learning runtime. A deterministic headless world keeps the challenge repeatable and independent of WPF rendering.

## Challenge

The initial world presents high hunger, visible food, low threat, and visible shelter. The brain must choose food, emit motor spikes, and cause the avatar to move. Food collection then relieves hunger while revealing a nearby threat. Body, object, and outcome feedback return through the avatar. The next brain decision must change to a safety-directed response: seeking shelter or immediately avoiding the threat. That second action must reduce threat exposure and return a second consequence.

The benchmark fails unless every boundary carries data, both actions affect the world, the second choice adapts to the first consequence, and action-memory plus dopamine-learning traces are updated.

Run it with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-embodied-closed-loop-benchmark.ps1"
```

Timestamped JSON and Markdown reports are written under `artifacts/embodied-closed-loop`.

## Initial Baseline

The first complete run on 2026-08-05 passed with 100.0% loop integrity:

- initial intent: `FindFood` / `motor_seek_food`, confidence 0.664;
- first consequence: food collected, threat revealed, RPE +0.680;
- adapted intent: `AvoidThreat` / `motor_about_face_escape`, confidence 0.699;
- second consequence: threat avoided, exposure reduced, RPE +0.540;
- brain motor spikes: 48;
- avatar motor events: 48;
- world-to-avatar traffic: 3 body, 2 outcome, and 5 object messages;
- learned state: 2 action-memory traces and 2 dopamine-learning traces;
- best learned action after the loop: `goal.AvoidThreat`.

The initial specification only accepted shelter seeking. The first execution correctly selected immediate threat avoidance, exposing that overly narrow criterion. The benchmark was corrected to accept either safety strategy only when the resulting world transition measurably lowers threat exposure and returns positive safety feedback.

Baseline report: `artifacts/embodied-closed-loop/embodied-closed-loop-20260805-202422.md`.

## Verification

- full Release solution build: zero warnings and zero errors;
- complete automated suite: 259 passed, 0 failed, 0 skipped;
- environment-sensitive persistence tests: 8 consecutive paired passes after assigning them to a nonparallel test collection;
- circuit audit: every declared cortical and subcortical structure reports `OK`.

The test-collection change prevents process-wide `NRE_SYNAPSE_STATE_DIR` and `SERVICE_INSTANCE` mutations from racing while leaving the rest of the suite parallel.

## Boundary

This benchmark proves deterministic message flow and short-horizon adaptive choice through the production avatar. It does not yet prove continuous navigation, obstacle avoidance, generalization to unseen worlds, or long-horizon autonomous survival in the WPF world simulation. Those are subsequent embodied rungs.
