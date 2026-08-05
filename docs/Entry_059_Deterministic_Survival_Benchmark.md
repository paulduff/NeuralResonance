# Entry 059 - Deterministic Survival Benchmark

Date: 2026-07-19

## Purpose

Dyad needs a stable way to ask whether DNNE's embodied state, learning pathways, and action interface are improving. This is the first such instrument. It is a deliberately small headless grid world, not a claim that DNNE has solved general survival.

The benchmark runs on a private imported `SimulationState`. It does not pause, mutate, or take control of the live DNNE control state. A run starts from either an explicitly supplied network-state document or a snapshot exported from the current control state, then creates a fresh isolated state for each policy.

## What It Exercises

Every benchmark step advances the real DNNE clock and applies:

- visible food, shelter, and threat object observations;
- environmental feedback for hunger, threat, shelter, health, and darkness;
- body and motor feedback;
- appetitive and aversive outcome feedback;
- cognitive runtime observation and the intentional-action loop.

`control-state-intent` is not a live distributed-DNNE run or an external planner. It imports a control-state snapshot into a private `SimulationState`, applies benchmark observations through the control-state learning routines, and translates the resulting bounded motor directive into one discrete grid movement. When no actionable directive exists, the adapter records a deterministic orienting fallback. That distinction is present in every episode record. The legacy input alias `current-dnne-intent` is accepted but normalized to this more accurate name.

The other named policies are explicit baselines:

- `rule-safety`: a hand-authored safety/food rule;
- `deterministic-random`: a fixed pseudo-random walk;
- `no-learning-stationary`: no movement and no benchmark observation or outcome learning; only the isolated simulation clock advances.

## Reproducibility

The world layout, random baseline, step count, policy list, and initial brain snapshot are all recorded in the JSON output. Episode records include the ordered sequence of world observations, DNNE intention fields, selected action, motor output, outcome feedback, and terminal condition. Wall-clock time is deliberately not part of the benchmark result, so two runs from the same snapshot and request can be compared directly.

## Running It

Start the DNNE Control Program, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-survival-benchmark-dnne.ps1"
```

The runner writes a complete JSON artifact and a concise Markdown report under `tools\artifacts\survival-benchmark` by default. To replay from a saved network state rather than taking a fresh snapshot of the live control state:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-survival-benchmark-dnne.ps1" `
  -InitialBrainStatePath "C:\path\to\network-state.json" `
  -Seed 317 `
  -Steps 240
```

## Next Use

The benchmark gives Dyad a regression gate before larger cognitive additions. Entry 060 adds replay comparison through the existing review-only Dyad language boundary. Future ablations must preserve the same episode format and no-live-state-mutation rule.
