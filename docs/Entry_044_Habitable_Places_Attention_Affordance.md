# Entry 044 - Habitable Places and Attention Affordance

Date: 2026-05-30

This entry continues Entry 043 by implementing the next inhabitability slice:

- Home, desk, and workbench model
- Attention affordance

## Biological Rule Applied

The place model and attention affordance are runtime summaries only. They activate from circuit evidence in hippocampus/subiculum, retrosplenial cortex, parahippocampal place area, posterior parietal cortex, PFC, ACC, thalamic/global workspace, basal forebrain, and language circuits. Simulators may provide sensory or body facts, but they do not decide the internal place or attention winner.

## Implementation Notes

- `HabitablePlaceModel` names internal places: desk, workbench, archive, window, listening space, dream space, and shelter room.
- Each place exposes its function, activation, active flag, and biological evidence.
- `AttentionAffordance` exposes the current attention mode, target, why it won, action hint, competing affordances, biological source, and confidence.
- Both summaries are included in room-state snapshots, inhabitance snapshots, diagnostics, and network export/import.

## Remaining List

- Preference and temperament memory
- Self-maintenance loop
- Embodied world atmosphere
- Private working-memory shelf
- Gentler sleep/dream digest
