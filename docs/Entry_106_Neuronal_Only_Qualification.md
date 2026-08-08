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

Protocol v2 replaces the maze gate with the visible rendered WorldSim. `-Mode
Live` requires a healthy DNNE runtime, starts WorldSim when it is not already
running, and monitors WorldSim's atomic physical-state stream. Qualification
requires:

- one uninterrupted WorldSim session with fresh Control Program telemetry;
- numeric neuronal motor dispatches to reach the avatar;
- actual displacement or newly visited terrain;
- fresh raw retinal, cochlear, physical-body, and somatic frames;
- at least one neuronal manipulator attempt;
- no new WorldSim tick failures.

Only a run passing both preflight and those live checks sets
`embodiedQualified=true`. The harness reads observations and writes evidence; it
does not inject a named action, goal coordinate, motor directive, or fallback
policy.

## Commands

Laptop preflight:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-only-qualification.ps1" -Mode Preflight
```

With the DNNE stack running (WorldSim is started visibly if necessary):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-only-qualification.ps1" -Mode Live -LiveDurationSec 300
```

Artifacts are written beneath `artifacts/neuronal-only-qualification/<UTC
stamp>/`. WorldSim remains visible after the gate so the run can be observed.
Short laptop runs are development evidence. Longer multi-seed runs on the RTX
workstation remain necessary for promotion-quality claims. MazeSim remains a
focused navigation diagnostic and is no longer evidence for the complete
embodied qualification gate.
