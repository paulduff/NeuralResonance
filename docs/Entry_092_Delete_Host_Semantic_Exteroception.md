# Entry 092 - Delete Host Semantic Exteroception

Date: 2026-08-06

## Purpose

This rung removes the remaining path by which a rendered world could tell DNNE
what a visible thing was or what it meant. A world may render geometry, light,
colour, motion, occlusion, sound, contact, damage, and body chemistry. It may
not inject `food`, `danger`, `shelter`, `tool`, `goal`, or any other semantic
object class into the brain.

## Deleted Semantic Path

The following production components have been physically deleted:

- `ObjectInputRequest` and `/api/v1/admin/input/object`;
- the object-specific ingress counter and configuration;
- `AvatarObjectObservation`, its queue, and its command;
- the maze goal-observation helper;
- maze classifiers for goals, food, hazards, and checkpoints;
- the world survival-cue and recognized-object dispatchers;
- world classifiers for food, predators, shelter, darkness, weapons,
  obstacles, and water;
- simulator-authored salience, confidence, hemisphere, and memory-encoding
  hints for those classes;
- recognized-object tuning controls and host-classification UI.

There is no legacy switch, annotation-only endpoint, or dormant transport type
that can restore this path.

## Remaining Sensory Boundary

The rendered worlds retain physical exteroception:

- raw rendered sight frames;
- low-level visual receptor traffic pending its later consolidation into the
  raw retinal route;
- auditory receptor traffic caused by physical events;
- directional touch, ground load, collision, pain, and body state.

The editor may display neuronal object-recognition and object-memory
diagnostics. Those displays read measured neural state; they do not label
world objects or feed a semantic answer back into the brain.

## Neural Ownership

Object identity, attention, familiarity, context, and value must arise across
the visual and association hierarchy:

- retina, thalamus, V1, V2, V4, and MT derive visual features and motion;
- inferotemporal, fusiform, temporal-association, and perirhinal circuits form
  object ensembles;
- hippocampal and entorhinal circuits bind place, sequence, and context;
- amygdala, insula, hypothalamus, orbitofrontal cortex, cingulate cortex, VTA,
  SNc, and basal ganglia learn consequence and value;
- prefrontal and language circuits may later bind learned ensembles to words,
  but a word is not permitted to create the percept it names.

## Authority Tests

`HostSemanticExteroceptionBoundaryTests` verifies that:

- semantic object transport types are absent from compiled assemblies;
- the control program has no object-label endpoint;
- neither rendered world can publish preclassified object observations;
- neuronal object-recognition diagnostics remain available;
- rendered vision continues to reach the avatar sensory boundary.

## Population Size

No population was resized. This rung removes privileged information and makes
the existing visual, temporal, memory, and valuation circuits responsible for
the work they represent. Population changes will follow measured capacity,
firing, lesion, and embodied-learning evidence.

## Verification

- Control program Release build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Editor Release build: passed with zero warnings.
- Tests: 311 passed, zero failed, zero skipped.
- Semantic exteroception source audit: no production references remain.
- Cortical functional benchmark: PASS, 100% overall, stream separation,
  learning, persistence, and adaptive output gating.
