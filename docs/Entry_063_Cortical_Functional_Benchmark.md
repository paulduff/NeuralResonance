# Entry 063 - Cortical Functional Benchmark

Date: 2026-08-05

## Purpose

This rung moves cortical specialization from structural tests to an executable benchmark over DNNE's real structure runtime. It checks population separation, synaptic learning, persistence after restart, and neuromodulator-sensitive output modes without requiring the distributed stack.

## Learning Correction

Benchmark tracing uncovered a consequential defect: inbound synaptic plasticity was updated and persisted, but the learned strength was not applied when later spikes were integrated. The runtime now combines presynaptic release strength and learned postsynaptic strength using a bounded geometric mean. This preserves contributions from both sides of the distributed synapse while avoiding multiplicative runaway excitation.

The first end-to-end run also exposed non-finite values reaching synapse persistence. Spike ingress now rejects non-finite biological values, neuromodulator and plasticity paths contain non-finite state, and synapses are stabilized before use and serialization. Persistence remains strict JSON; invalid numerical state is corrected at the biological boundary instead of being hidden by the serializer.

## Protocol

The benchmark measures:

- separation of visual, auditory, body, self-context, and executive input streams;
- repeated coincident activation across five representative cortical services;
- change in inbound synaptic strength under each circuit's configured learning rule;
- exact recovery of learned strength after the structure service is disposed and recreated;
- attention-driven FEF bursts, prediction-error-driven midcingulate bursts, dopamine-gated vmPFC bursts, and quiet tonic FEF output.

Run it with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-cortical-functional-benchmark.ps1
```

The runner writes timestamped JSON and Markdown artifacts under `artifacts/cortical-functional-benchmark` and returns a failing exit code when the benchmark thresholds are not met.

## Initial Baseline

The 24-epoch baseline completed on 2026-08-05 with:

- overall score: 99.0%;
- stream separation: 96.0%;
- learning: 100.0%;
- persistence: 100.0%;
- adaptive output gating: 100.0%.

All five representative cortical circuits changed their learned inbound strength, and every learned value was recovered exactly after engine disposal and recreation. All four gated-output scenarios produced the expected burst or tonic spike type. The remaining separation loss is explicit: two of five executive-control inputs currently converge on the same dorsomedial prefrontal population. That collision is the next population-coding refinement target.

Baseline report: `artifacts/cortical-functional-benchmark/cortical-functional-benchmark-20260805-200932.md`.

## Executive Population Refinement

The initial benchmark showed that mixing a control-lane offset into a hash did not guarantee functional separation: the action-selection and attention streams could collide. Non-FEF executive association regions now reserve disjoint population partitions for planning, conflict/error, value, action selection, attention, and uncategorized control input. Local identity remains distributed within each partition. Frontal eye fields retain their spatial grid because gaze control is topographic rather than categorical.

The unchanged 24-epoch benchmark was repeated on 2026-08-05 after this correction:

- overall score: 100.0%;
- stream separation: 100.0%;
- learning: 100.0%;
- persistence: 100.0%;
- adaptive output gating: 100.0%.

All five executive-control inputs now activate five distinct dmPFC populations. Updated report: `artifacts/cortical-functional-benchmark/cortical-functional-benchmark-20260805-201338.md`.

## Boundary

Passing this benchmark demonstrates that specialized circuits process distinct streams, alter future responses through learning, survive restart, and adapt output modes to control signals. It does not establish object invariance, face expertise, social inference, or a stable self-model. Those require closed-loop avatar and simulation trials with withheld generalization cases.
