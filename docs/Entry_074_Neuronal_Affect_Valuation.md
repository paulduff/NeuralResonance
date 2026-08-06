# Entry 074 - Neuronal Affect And Valuation

Date: 2026-08-06

## Purpose

This rung removes central scalar emotion, motivation, reward, and named-goal records from live affect authority. DNNE now carries affect through spikes, local receptor currents, and plastic synapses. The controller may observe the distributed state but cannot turn that observation into an action bias.

## Circuit Evidence

The read-only decoder consumes:

- amygdala, insula, ACC, hypothalamus, locus coeruleus, basal forebrain, accumbens, PFC, and PAG salience/affect activity;
- nucleus tractus solitarius, hypothalamic, insular, limbic, autonomic, arousal, and comfort-deficit activity;
- amygdala-hypothalamus-PAG-raphe-medulla-reticular-spinal defense activity;
- VTA, SNc, accumbens, striatal, habenular, orbitofrontal, and PFC reward activity.

All firing-derived values are normalized against a bounded saturation rate. Human-readable diagnostic mode strings are ignored. The observer does not read the legacy scalar `RewardPredictionError`; positive and negative valuation are inferred from measured reward and aversion populations.

## Numeric Valuation Lanes

The decoder reports four anonymous lanes:

- lane `0`: approach pressure from positive reward, homeostasis, and interoception;
- lane `1`: exploratory control from PFC control, attention, and learning readiness;
- lane `2`: recovery pressure from setpoint error, comfort deficit, and interoception;
- lane `3`: defense pressure from threat, aversion, and descending protection circuits.

These labels are documentation at the monitoring boundary. Renaming or contradicting a diagnostic label cannot change the result, and the four monitor lanes are never written back into an action population.

## Causal Neural Path

The causal path is neuronal:

- amygdala, insula, hypothalamus, ACC, OFC, VTA, SNc, habenula, and accumbens emit along explicit connectome routes;
- routed neurotransmitter spikes change only the receptors of the neurons that receive them;
- each neuron derives its own dopamine, serotonin, acetylcholine, and norepinephrine state from decaying receptor currents;
- local receptor state controls firing gain, inhibition, burst type, metabolic recovery, intracellular support, and synaptic plasticity;
- corticostriatal and memory learning receive local D1/D2 appetitive and 5-HT aversive teaching signs rather than a controller reward scalar;
- basal-ganglia direct, indirect, and hyperdirect competition remains the only action-selection path.

`TickSignal.GlobalNeuromodState`, `TickSignal.RewardPredictionError`, and per-spike `ModulationContext` remain compatibility fields, but the structure engine no longer reads them as causal inputs. The controller sends neutral values on the tick wire and neuronal outbound spikes carry no scalar modulation context.

## API Boundary

`/api/v1/neuronal-affect-valuation` exposes a read-only observation and explicitly reports `CanBiasAction=false`. Limbic, emotion, interoceptive-core, motivation-arbitration, goal-intent, and dopamine-learning endpoints are legacy telemetry. They can describe old checkpoint state but cannot authorize valuation or movement.

The global cognition-authority snapshot now includes `affect-valuation` as its eighth domain.

## Causal Tests

Tests pin:

- reward plus homeostatic populations selecting numeric lane `0`;
- threat plus defense populations selecting numeric lane `3`;
- loss of authority after valuation-circuit ablation;
- invariance under diagnostic label changes;
- no valuation without neuronal diagnostics;
- local D1/D2 receptor teaching reversing corticostriatal preference when contingencies reverse;
- routed raphe serotonin extinguishing a memory association and routed VTA dopamine relearning it;
- finite membrane, intracellular, and synaptic state under sustained local neuromodulation;
- identical plasticity when only legacy global neuromodulator and reward fields differ.

No population resize was needed. Future measurements may justify dedicated finer-grained hunger, thirst, thermal, attachment, pain, and social valuation ensembles.
