# Neural Activity Audit

Generated: 2026-06-07

## Summary

The DNNE connectome and service map are structurally complete: 74 structure services are present and all registered structures have explicit inbound/outbound routes. The runtime problem observed on 2026-06-07 was not missing anatomy; it was neural starvation.

Live `/api/v1/circuit-health` showed:

| Metric | Value |
| --- | ---: |
| Structures | 74 |
| True warnings | 0 |
| Quiet notices | 66 |
| Generated / routed / delivered spikes | 0 / 0 / 0 |
| Active pathways | 0 |
| Mean function support | 0.259 |
| Spontaneous spiking gate | Off |

## Circuit Family Activity

| Family | Circuits | Recently active | Recent in | Recent out | Lifetime in | Lifetime out |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Arousal/Limbic | 8 | 0 | 0 | 0 | 2585 | 404 |
| Association/Control | 7 | 2 | 841 | 711 | 15315 | 6218 |
| Auditory | 5 | 0 | 0 | 0 | 4364 | 4739 |
| BasalGanglia/Reward | 11 | 0 | 0 | 0 | 8399 | 637 |
| Cerebellar | 7 | 0 | 0 | 0 | 49922 | 2842 |
| Hippocampal/Nav | 10 | 5 | 1550 | 1550 | 18527 | 12739 |
| Language | 4 | 0 | 0 | 0 | 73 | 109 |
| Sensorimotor | 8 | 0 | 0 | 0 | 25442 | 88605 |
| Thalamic/GW | 4 | 1 | 0 | 130 | 92 | 6468 |
| Visual/Object | 8 | 0 | 0 | 0 | 19214 | 21171 |

## Findings

1. All major CNS circuit families have routes and lifetime participation.
2. The recent activity window collapsed to zero active pathways because spontaneous spiking was disabled at runtime.
3. With spontaneous spiking disabled, the brain depends on external visual/audio/body/object dispatches; when those calls are sparse or timing out, most circuits go alive-idle.
4. The cerebellar and sensorimotor systems have very high lifetime throughput but no recent activity, indicating the simulation can activate them but lacks sustained awake tonic drive.
5. Basal ganglia/reward and arousal/limbic families are particularly underactive in the recent window, which weakens action selection, motivation, and global activation.

## Fix Applied

- Enabled forced sparse spontaneous fallback in `ControlProgram/appsettings.json`.
- Added an awake neural-starvation guard in `ControlProgram`: if the brain is awake, transport is fully silent, spontaneous spiking is off, and live sources exist for a sustained window, spontaneous spiking is restored and logged.
- Relaxed spontaneous fallback so it can generate one real routed spontaneous event during complete transport silence, not only benchmark mode.
- Re-enabled the live runtime spontaneous spiking gate via `/api/v1/admin/input-gates`.

## Biological Rule

The fix does not invent display spikes. It restores endogenous tonic activity through existing biological structures and explicit connectome routes. Editor 3D activity should still represent actual brain dispatch activity only.
