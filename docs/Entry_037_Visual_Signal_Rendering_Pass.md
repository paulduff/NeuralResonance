# Entry 037 — Visual Signal Rendering Pass

This notch improves the live neural renderer without changing simulation semantics.

## Goals
- make activity easier to read
- improve tract visibility
- embed the network in a faint anatomical shell
- add multiple visual modes for anatomy, activity, connectivity, and validation

## Applied changes
- added render modes to the live neural renderer
- added translucent hemisphere shell meshes driven by live layout bounds
- upgraded spike rendering to use additive-style glow
- added animated endpoint-biased fibre pulsing for active connections
- added subtle depth fog to improve volumetric readability
- exposed visual controls in the View tab

## Modes
- Anatomy: structure-first, quieter signals
- Activity: stronger spike glow and balanced tract pulses
- Connectivity: fibres emphasized, shell reduced
- Validation: shell and structural cues emphasized for inspection

## Notes
This pass is display-only. It does not alter the engine, the connectome, or anatomical placement rules.
