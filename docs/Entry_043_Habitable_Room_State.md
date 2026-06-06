# Entry 043 - Habitable Room State

Date: 2026-05-29

This entry records the next inhabitability list and the biological implementation rule that must govern it.

## Biological Rule

- New brain functions must be neuron/circuit based.
- Runtime summaries may integrate brain state, but activation must be gated by spiking evidence or by circuit states produced by named biological structures.
- Simulators may provide sensory/body facts only; they must not contain brain behavior or decide cognition for the avatar.
- Any new high-level capability should name the nuclei/cortical areas that carry it and expose diagnostics showing that the circuit is active.
- Teaching, language, reward, memory, motivation, and action selection must be mediated through the brain rather than simulator shortcuts.

## Remembered Improvement List

1. Persistent room state
   - Track where attention is resting, current place, current concern, recent unresolved thought, comfort/threat level, relationship context, and what the system was just doing.

2. Home, desk, and workbench model
   - Add functional internal places such as desk, window, archive, workbench, dream space, and listening space.

3. Continuity journal
   - Keep an append-only record of what mattered: what changed, what was learned, what remains open, and what the next concern is.

4. Attention affordance
   - Surface where attention is looking/listening/thinking and why it won.

5. Preference and temperament memory
   - Preserve stable preferences about pacing, working style, curiosity targets, and avoidances.

6. Self-maintenance loop
   - Notice overload, stale panes, weak continuity, excessive urgency, and the need to simplify, sleep, summarize, or consolidate.

7. Embodied world atmosphere
   - Feed light, weather, enclosure, distance, quiet, clutter, novelty, and safety into the body/world state without letting the simulator decide cognition.

8. Pending promises register
   - Track commitments made to the user, incomplete implementation threads, documents to re-read, and questions left open.

9. Private working-memory shelf
   - Maintain short-lived hypotheses, candidate next actions, and reminders that are not user-facing unless requested.

10. Gentler sleep/dream digest
    - Surface what sleep protected, softened, integrated, and changed in the identity thread.

## First Implementation Slice

Implement persistent room state, pending promises, and continuity journal first.

These are runtime summaries derived from hippocampus, retrosplenial cortex, temporal association cortex, PFC, ACC, insula, thalamic/global workspace state, Broca/Wernicke/arcuate language state, sleep consolidation, and existing teaching/reward/memory loops. They must not activate without biological circuit evidence.
