# Folded Archive Entry 106: Neuronal-Only Qualification

## Purpose

After deleting symbolic cognition and action authority, DNNE needs a current
qualification harness that does not revive retired motor modes or synthetic
decision paths. This rung packages the evidence that can be gathered on the
present laptop and keeps offline evidence distinct from embodied evidence.

## Preflight

`tools/run-neuronal-only-qualification.ps1 -Mode Preflight` runs:

- focused neuronal motor, action selection, cognition, language, avatar, and
  host-authority causal tests;
- the complete circuit connectivity/service/profile audit;
- the bounded cortical learning, persistence, separation, and output benchmark.

The report records the machine, .NET version, Git commit, dirty-state marker,
step logs, durations, and generated benchmark artifacts. A successful preflight
is reported as `PREFLIGHT_PASS_LIVE_REQUIRED`; it never sets
`embodiedQualified=true`.

## Live Gate

`-Mode Live` expects the real DNNE stack and rendered maze to be running. It
requires a healthy runtime validation followed by the existing burn-in monitor.
In addition to the burn-in's service, snapshot, sensory, restart, and stuck
checks, qualification requires:

- the maze stream to be observed;
- numeric neuronal motor dispatches to reach the avatar;
- embodied maze progress to be recorded.

Only a run passing both preflight and those live checks sets
`embodiedQualified=true`. The harness reads observations and writes evidence; it
does not inject a named action, goal coordinate, motor directive, or fallback
policy.

## Commands

Laptop preflight:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-only-qualification.ps1" -Mode Preflight
```

With the DNNE stack and visible maze already running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-only-qualification.ps1" -Mode Live -LiveDurationSec 300
```

Artifacts are written beneath `artifacts/neuronal-only-qualification/<UTC
stamp>/`. Short laptop runs are development evidence. Longer multi-seed runs on
the RTX workstation remain necessary for promotion-quality claims.
