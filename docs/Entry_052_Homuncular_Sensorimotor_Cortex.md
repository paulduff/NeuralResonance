# Entry 052 - Homuncular Sensorimotor Cortex

Date: 2026-06-03

## Purpose

Make avatar motor control and body awareness more biologically grounded by treating the motor/somatosensory homunculus as real cortical circuitry, not a simulator shortcut.

## Biological Rule

- Simulator state may provide sensory/body facts only.
- Movement intent and body awareness must be carried by named neural structures and spikes.
- The correct connectome is mandatory: S1/PPC/SMA/premotor/motor thalamus must converge through M1, and M1 must project to SpinalCordMotor through corticospinal output.
- Visual brain representation should place cortical circuits on cortical surfaces; inner circuits should stay in their biological-equivalent anatomical positions.

## Actual Neural Areas

- Primary motor cortex: M1, precentral gyrus, Brodmann area 4.
- Primary somatosensory cortex: S1, postcentral gyrus, Brodmann areas 3, 1, and 2.
- Premotor cortex: lateral Brodmann area 6.
- Supplementary motor area: medial Brodmann area 6.
- Posterior parietal cortex: body schema and sensorimotor integration, areas 5/7 equivalent.
- Motor thalamus: VA/VL relay into M1.
- Spinal motor pools: final motor output proxy.

## First Slice

Status: DONE for the first slice.

- Added a homuncular sensorimotor circuit kernel for M1/S1.
- M1/S1 spikes now map into body-zone bands: face/head, hand/arm, trunk, leg/foot.
- Updated M1/S1 service profiles and specs to name the actual cortical areas.
- Added editor cortical overlays so M1/S1 show embedded homuncular bands on the cortical sheet.
- Raised the editor's default visible neuron budget from 24^3 to 30^3 per hemisphere, with a slider maximum of 40^3 for denser inspection. Engine density remains unchanged.
- Added connectome tests for the direct biological relay order.

## Next Steps

- DONE: Added a PPC body-schema kernel that binds S1 body zones, vestibular posture, dorsal motion, pulvinar attention, and retrosplenial spatial reference into body-zone/peripersonal-space fields.
- DONE: Added PPC cortical body-schema overlays in the editor so posterior parietal cortex reads as a cortical surface field.
- DONE: Added M1/S1/PPC body-zone diagnostics to telemetry so the editor can show which band and PPC peripersonal-space field is currently active.
- DONE: Extended visual overlays for basal ganglia, thalamus, cerebellum, and brainstem so inner circuits read as their biological equivalents rather than generic nuclei.
- DONE: Added basal ganglia action-selection diagnostics for direct/indirect/hyperdirect pathway balance, GPi/SNr output inhibition, thalamic release, dopamine modulation, and Go/Hold/Stop mode.
- DONE: Added cerebellar correction diagnostics for mossy/parallel-fiber drive, climbing-fiber teaching error, Purkinje inhibition, DCN output, vermis stabilization, correction gain, and Stable/Correcting/Overcorrecting mode.
- DONE: Added vestibulo-reticular posture diagnostics that tie vestibular nuclei, reticular formation, vermis, and spinal motor tone into a live balance/arousal loop.
- DONE: Added superior-colliculus orienting diagnostics for head/eye orienting and salience-driven gaze shifts.
- DONE: Added hippocampal-entorhinal spatial memory diagnostics for place/grid/head-direction alignment during navigation.
- DONE: Added amygdala-insula-ACC salience diagnostics for threat/interoception/conflict-driven state changes.
- DONE: Added prefrontal-working-memory diagnostics for goal maintenance, task set stability, and top-down control.
- DONE: Added thalamic-TRN attention-gating diagnostics for relay selection, sensory gain, and cortical access control.
