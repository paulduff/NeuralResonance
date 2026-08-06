# Entry 093 - Delete Host Survival Cognition

Date: 2026-08-06

## Purpose

This rung removes host-authored urgency, anxiety, threat, flight, and automatic
combat decisions from the embodied worlds. The simulator remains responsible
for body and world physics. Neuronal activity remains responsible for affect,
motivation, action selection, and motor magnitude.

## Deleted Authority

The following production mechanisms have been physically deleted:

- the optional motor `forwardScale` and both urgent-run calculators;
- simulator threat decay and abstract threat health damage;
- darkness-derived sleep pressure, shelter need, and anxiety scalars;
- flight episodes, flight pressure, and weapon-seeking pressure;
- automatic weapon firing, charge consumption, range engagement, fight bias,
  and flight suppression;
- threat-decay and weapon-effectiveness tuning controls;
- affect-derived sound intensity.

There is no compatibility switch that can multiply neuronal movement or
restore automatic combat.

## Physical Substrate Retained

The world still owns facts and consequences that exist outside the brain:

- day/night lighting and visible darkness;
- predator movement, pursuit, contact, and strike damage;
- collision, pain, hunger, health, food consumption, and carried objects;
- shelter occupancy;
- lower sleeping metabolism and physical recovery while neuronally asleep in
  shelter;
- physical sound sources and their direction;
- fixed body speed and turn limits.

Sleep state is read from the neuronal sleep decoder. Darkness is presented
through sensory evidence, not converted into host sleep pressure or anxiety.

Weapons may still exist as physical objects and inventory, but they are inert
until a dedicated neuronal action pathway and actuator are implemented. The
world will not infer an attack from proximity.

## Neural Ownership

- amygdala, PAG, hypothalamus, insula, and cingulate circuits derive threat and
  defensive state;
- hypothalamic, NTS, insular, and brainstem circuits derive homeostatic need;
- basal ganglia, motor cortex, cerebellum, thalamus, and spinal pathways select
  and scale movement;
- prefrontal, premotor, basal-ganglia, and brainstem circuits must eventually
  select any discrete manipulation or weapon action;
- auditory and visual circuits learn the significance of predators, shelter,
  darkness, food, and tools from receptor evidence and consequence.

## Authority Tests

`HostSurvivalAuthorityBoundaryTests` verifies that:

- motor projection exposes no urgency multiplier;
- neither rendered world contains urgent-run scaling;
- the world contains no host affect, weapon-seeking, or automatic weapon-use
  system;
- physical predator strikes, hunger, health, shelter, and neuronal sleep
  effects remain.

## Verification

- Control program Release build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Editor Release build: passed with zero warnings.
- Tests: 315 passed, zero failed, zero skipped.
- Host survival-authority source audit: no production references remain.
- Cortical functional benchmark: PASS, 100% overall, stream separation,
  learning, persistence, and adaptive output gating.
