# Entry 088 - Delete Host Sleep Authority

Date: 2026-08-06

## Purpose

This rung removes the remaining central sleep controller and every transport,
avatar, editor, and simulator shortcut that could overrule the neuronal brain.
Sleep and consolidation are now circuit states, not host modes.

## Authority Boundary

The host may measure ATP reserve, accumulated homeostatic pressure, elapsed
time, and neural workload. These are physical substrate observations. They
enter the relevant structures as physiological drive but cannot select wake,
NREM, REM, replay content, language availability, or motor atonia.

Those decisions belong to the distributed hypothalamic, brainstem, thalamic,
TRN, hippocampal, and cortical populations exposed by
`/api/v1/neuronal-sleep-consolidation`.

## Deleted Runtime Authority

- `SleepMemoryRuntime`, threshold transitions, wake inertia, adaptive duty
  cycle rules, alerts, and central replay mirrors were deleted.
- Global sleep neurotransmitter scaling and host-wide excitatory/inhibitory
  traffic multipliers were deleted.
- Spontaneous, sensory, language, replay, and motor traffic are no longer
  paused by a central sleep boolean.
- The motor population decoder no longer accepts a host sleep argument.
- The avatar nervous system and both worlds no longer reset or zero motor
  drive because a sleep flag was observed.
- `PausedDueToSleep` was removed from the HTTP response and shared client
  contracts. Sensors continue reporting the world; neuronal circuits decide
  how that evidence propagates.
- The editor's sleep thresholds and wake-duration controls were deleted.

## Preserved Physiology

`MetabolicPhysiologyRuntime` retains ATP reserve, homeostatic pressure, their
bounded rates, and wake/sleep duration telemetry. Its sleep observation is
copied only from an available, active neuronal decision. High pressure or
exhausted ATP cannot initiate sleep. Missing or incomplete neuronal evidence
cannot hold a previous host sleep state.

The read-only `/api/v1/admin/metabolic-physiology` endpoint explicitly reports
that it cannot authorize sleep or gate neural traffic.

## Persistence

The network checkpoint schema advances to version 3. Importing a version 2
checkpoint discards the old sleep-memory overlay and starts with default
metabolic physiology. Unknown legacy JSON remains harmless because no runtime
type or restore destination exists for it.

## Simulator Boundary

The world may display the neuronally decoded sleep state and apply genuine
physical consequences such as sheltered recovery. It may not suppress motor
population output, stop sensory delivery, or create a second sleep decision.
Motor stillness must emerge from the neuronal sleep, basal-ganglia,
brainstem, and spinal pathways.

## Causal Invariants

- High metabolic load without a neuronal decision never enters sleep.
- A complete neuronal NREM decision changes the observed physiological state.
- An incomplete circuit immediately loses sleep authority and cannot fall
  back to host thresholds.
- The motor decoder and avatar body contract contain no sleep veto parameter.
- Legacy sleep JSON cannot reactivate simulator sleep state.
- Central replay selectors and sleep traffic-scaling types are physically
  absent.

## Population Size

No neuron-count change was justified. This rung removes authority leaks; it
does not address measured population starvation or representational capacity.

## Verification

- Control Program Release build: passed with zero warnings.
- WPF editor Release build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Tests: 320 passed, zero failed.
- Cortical functional benchmark: PASS, 100% overall and in every category.
