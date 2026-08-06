# Entry 068 - Embodied Neuronal Motor Qualification

## Purpose

Rungs 1 through 8 made neuronal circuits authoritative and removed symbolic cognition writes. Rung 9 establishes the evidence required before neuronal motor output may advance from `Shadow` to `Assist`, and later from `Assist` to a guarded `Primary` canary.

Qualification is an evaluation system, not a mode switch. No benchmark, report, or script in this rung calls the motor-mode administration endpoint. A human operator must review a complete campaign and explicitly request a mode change.

## Evidence Classes

The harness keeps three claims separate:

1. **Offline causal preflight** proves expected decoder perturbations: bilateral drive, lateralized steering, basal-ganglia inhibition, hemisphere ablation, sleep suppression, and independence from the symbolic comparison reference.
2. **Live scenario capture** samples `/api/v1/neuronal-motor` and `/api/v1/state` while the real structure services, avatar, and rendered simulator exchange data.
3. **Multi-seed campaign** requires three training and three held-out scenarios with distinct seeds and verified layout fingerprints.

Offline or synthetic evidence can never qualify authority. A single live scenario can only enter a campaign. A passing `Shadow` campaign recommends `Assist`; only a separate passing `Assist` campaign reports readiness for a guarded `Primary` canary.

## Default Gate

The live evaluator reads the active `NeuronalMotorControl` settings from the API. The current production defaults require:

- at least 1,200 new active evaluation samples during each capture;
- final bilateral motor coverage of at least 0.80;
- final confidence EMA of at least 0.62;
- final migration agreement EMA of at least 0.72;
- a qualified streak of at least 600 ticks;
- action-selection circuit evidence on at least 80% of active observations;
- motor and embodied-state API observations aligned within a bounded tick-skew window;
- advancing body and outcome feedback from the simulator;
- observed embodied movement;
- zero active motor output while sleeping;
- symbolic scaffold, semantic motor injection, and world-goal steering authority all disabled;
- all offline causal perturbations passing.

The capture starts its own counter baseline. A process that was already promotion-ready cannot pass from one final sample.

## Seeded Rendered Maze

The rendered maze now reads `NRE_MAZE_SEED`. `tools/start-maze-sim.ps1` exposes this as `-Seed`. On startup the visible maze log prints both the seed and a SHA-256 layout fingerprint. The fingerprint includes the seed, dimensions, walls, entities, start, and goal cells generated for that run.

Recommended campaign seeds:

- training: `317`, `911`, `2027`;
- held-out: `4049`, `5051`, `6067`.

The brain must begin each scenario from the declared snapshot and the simulator must be restarted with the requested seed. Reusing a seed or layout causes campaign failure.

## Commands

Offline preflight:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-motor-qualification.ps1" -Mode Preflight
```

Start a seeded rendered maze after the DNNE stack is healthy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\start-maze-sim.ps1" -Configuration Release -Seed 317
```

Copy the layout fingerprint printed in the maze log, then capture the live `Shadow` scenario:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-motor-qualification.ps1" -Mode Capture -ApiBaseUrl "http://localhost:5080" -ScenarioId "maze-training-317" -Split training -Seed 317 -ExpectedMode Shadow -LayoutFingerprint "sha256:REPLACE_WITH_MAZE_LOG_VALUE" -MaxSeconds 1800
```

After all six scenario reports exist, evaluate the `Shadow` campaign:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-motor-qualification.ps1" -Mode Campaign -ExpectedMode Shadow
```

Repeat the six scenarios in `Assist` only after the Shadow campaign passes. Then evaluate the Assist campaign:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-neuronal-motor-qualification.ps1" -Mode Campaign -ExpectedMode Assist
```

## Artifacts

The default output directory is `artifacts/neuronal-motor-qualification` and contains:

- immutable live capture JSON with all distinct sampled runtime sequences;
- scenario JSON and Markdown reports;
- campaign JSON and Markdown reports;
- offline preflight JSON and Markdown reports.

Each scenario report includes a SHA-256 capture fingerprint. Campaign reports list every contributing scenario fingerprint so the decision can be traced back to raw observations.

## Circuit Sizing Rule

Neuron counts are not changed merely because a gate fails.

- Low coverage points first to missing services, hemisphere loss, stale snapshots, or transport failure.
- Healthy coverage with weak confidence points to firing-rate separation, gain, saturation, synaptic tuning, or potentially insufficient population capacity.
- Weak action-lane coverage points to lane allocation or collision pressure in the action-selection populations.

Population resizing is justified only after this telemetry identifies a specific starved, saturated, or colliding circuit. The report records the applicable recommendation for each scenario.

## Scientific Boundary

A passing campaign demonstrates that the implemented neuronal pathway behaves consistently under the declared perturbations and embodied scenarios. It does not establish consciousness, biological equivalence, or unrestricted general intelligence. Failed trials remain evidence and must be retained alongside successful ones.
