# Desktop brain–body–world results — 9 October 2026

The measurement completed, but runtime qualification failed because three samples contained stale brain-frame data. The experiment confirms neuronal output can move Avatar and receive physical and retinal feedback. It does not qualify reliable autonomous behaviour or language comprehension.

Source run: `artifacts/desktop-brain-world-20261009-105018-060701/`, recorded revision `af50b91`. Measurement completed at 12:20:32 British Summer Time. The independently running measurement window collected 109 samples over approximately 30 minutes. The initial world was already safety-paused; the operator pressed Play near the end. World time advanced by 524.58 seconds, approximately 8 minutes 45 seconds, so this was not a complete 30-minute active-world qualification. The later `c5bb976` launcher waits for operator Play before starting the measurement timer.

| Measurement | Result |
| --- | --- |
| Neuronal services | 119 healthy in every sample |
| Brain advancement | 24.95 ticks per wall-clock second |
| World advancement | 6,679 → 22,533 ticks |
| Distance travelled | 0 → 4.192 metres |
| Visited terrain cells | 1 → 5 |
| Retinal frames accepted | 370 → 8,674 |
| Physical body frames accepted | 125 → 10,055 |
| Somatic contact frames accepted | 1,004 → 111,425 |
| Motor dispatch count | 27,537 → 209,728 |
| New world tick failures | 0 |
| New body-input failures | 0; five failures predated this measurement |
| Additional safety pauses | 0; one pause predated this measurement |
| Stale brain-frame samples | 3 of 109 |
| Food consumed / physical deaths during measurement | 0 / 0 |
| Mean DNNE/world CPU share of the machine | 4.68% |
| Peak summed process working sets | 18.22 GiB; shared pages may be counted more than once |

Two stale-frame samples occurred while physical time was paused, and one occurred after Play. Each was followed by a connected sample. All services remained healthy, but service health alone does not establish timely brain-to-body communication. Saved ControlProgram logs also contain successful frame requests that took 7.7–12.9 seconds. Those logs do not contain enough timing evidence to attribute every sampled gap to a particular request.

The body currently polls the general `/api/v1/frame` endpoint. This includes diagnostics, logs and the latest brain snapshot as well as motor and sleep state. An earlier diagnostic response with logs disabled was approximately 1.9 MB after formatting. That suggests investigating payload size, serialization, shared-state contention and garbage collection before assuming the neural circuits need alteration. It does not establish the cause of the stalls.

## Observation after measurement

DNNE and WorldSim intentionally remained running. A separately saved rolling report at 12:36:02 BST records 10.054 metres travelled, zero food consumption and one physical death followed by a viable respawned body. The death record attributes most accumulated tissue damage to right-foot contact impact, with additional energy-depletion damage. This report extends beyond the measurement and must not be folded into its final totals.

The later gait record contains 61 alternating swing transitions and 105 repeated same-side transitions. The body spent substantial time in the falling balance phase. These observations make balance, coordinated gait and contact damage specific investigation targets; movement counters alone are insufficient evidence of useful exploration. The saved report is `post-measurement-world-report.json` in the run directory.

## Next qualification

First isolate the intermittent frame delays and compare a bounded brain-to-body payload against the diagnostic frame, preserving the same neuronal motor, sleep and action-authority evidence. Then repeat a full 30-minute active-world run using the corrected operator-Play timer. Evaluate walking, balance, contact damage and environmental interaction separately from transport health. DNNE retains action authority throughout; Entity language training is not a remedy for these runtime and body-control findings.

The original summary, completion marker, samples and final snapshot were independently hash-verified against their D: backups. The assessment and later rolling-report snapshot are also preserved with the run.
