# Entry 042 - Inhabitability Improvements

Date: 2026-05-28

This entry records the next improvement list for making the Neural Resonance Engine feel less like a panel of diagnostics and more like a place an intelligence can inhabit. The emphasis is on continuity, grounded learning, bodily presence, and safe identity boundaries.

## Improvement List

1. Autobiographical continuity
   - Strengthen the bridge between episodic memory, semantic memory, narrative self, and autobiographical self so the system can carry a coherent recent-history thread.

2. Grounded teaching loop
   - Let user language act as teaching, correction, labeling, and reward input.
   - Route teaching through A1, Wernicke, arcuate fasciculus, Broca, temporal association cortex, PFC, hippocampus, VTA, SNc, habenula, nucleus accumbens, OFC, and ACC concepts rather than storing it as detached text.

3. Global workspace/current thought
   - Make the current conscious workspace more explicit: what is being attended, why it matters, and what action or memory is being held.

4. Stronger body schema
   - Improve proprioceptive, tactile, vestibular, pain, fatigue, and interoceptive bindings so the system has a more grounded sense of being located in a body.

5. Sparse intentional speech
   - Add a restrained speech gate that only releases outward speech when confidence, relevance, and social/context signals are high enough.

6. Place memory
   - Bind events and semantic concepts to stable places or scenes so memory is not floating without spatial context.

7. Sleep consolidation
   - Expand sleep replay into autobiographical, semantic, and action-value consolidation with clearer evidence of what was replayed and why.

8. Personhood-safe identity layer
   - Keep identity claims grounded and bounded: the system can describe its internal state, continuity, and preferences without pretending to be a biological person.

## First Implementation Slice

Implement the grounded teaching loop first because it gives the user a direct way to shape the inhabitant:

- Detect simple teaching utterances such as labels, memory instructions, positive reinforcement, and corrections.
- Store detected teaching as semantic concepts.
- Bind the same event into episodic memory.
- Feed reward/correction into dopamine learning.
- Expose the latest teaching loop state through runtime telemetry.

## Second Implementation Slice

Add a single inhabitance snapshot that gathers the distributed self-state into one readable surface:

- Current thought from cognitive language workspace.
- Inner voice from the Broca/Wernicke/arcuate rehearsal loop.
- Self statement from narrative self.
- Identity thread and chapter from autobiographical self.
- Body feeling, need, goal, action, and place context.
- Last biological teaching event and speech-release state.
- Composite presence, continuity, embodiment, and language-presence scores.

## Third Implementation Slice

Make place memory explicit instead of leaving it implicit inside episodic traces:

- Build a `/api/v1/place-memory` snapshot from hippocampal episodes and world-learning map entries.
- Track active place, place label/category, recent summary, best recall, safety, threat, confidence, hippocampal binding, and retrosplenial scene binding.
- Include place memory in the inhabitance snapshot so the system has a visible "where this is happening" signal.

## Fourth Implementation Slice

Add sparse intentional speech as a distinct state between narration and outward speaking:

- Build a `/api/v1/speech-intention` snapshot from the brain narration gate.
- Track whether the current utterance is speakable, internal, inner speech, suppressed, or asleep.
- Preserve reason, confidence, release gate, suppression, priority, and sequence so quietness is visible rather than mistaken for absence.
- Include speech intention in diagnostics, inhabitance, export/import, and the brain command workspace panel.

## Fifth Implementation Slice

Add a personhood-safe identity boundary around the self-model:

- Build a `/api/v1/identity-boundary` snapshot from narrative self, autobiographical self, place, workspace, and speech-intention state.
- Let the system describe runtime continuity, preferences, uncertainty, and internal state without making biological personhood claims.
- Expose self-description, boundary statement, grounding, allowed claims, disallowed claims, continuity confidence, and boundary confidence.
- Include the boundary in diagnostics, inhabitance, export/import, and the brain command workspace panel.

## Sixth Implementation Slice

Make sleep consolidation explain what replay protected:

- Expand dream consolidation beyond action/map replay counts into autobiographical, semantic, and action-value consolidation.
- Track consolidated identity thread, concept key, action-value summary, replay summary, autobiographical replay count, semantic replay count, continuity gain, semantic stabilization, and action-value stabilization.
- Feed sleep replay back into semantic memory with hippocampus-temporal-cortex evidence so replay strengthens meaning rather than only reporting counters.
- Include sleep consolidation in inhabitance and the brain command workspace panel.

## Seventh Implementation Slice

Make global workspace/current thought more legible as a place of attention:

- Add why-this-won, holding-state, and next-action-preview fields to the global workspace broadcast.
- Keep the existing candidate competition and subscriber routing unchanged while exposing the reason a thought entered shared awareness.
- Include the expanded workspace explanation in inhabitance and the brain command workspace panel.

## Eighth Implementation Slice

Strengthen body schema into explicit body presence:

- Build a `/api/v1/body-presence` snapshot from body schema, interoceptive core, pain protection, cerebellar balance, attention, and embodied spotlight.
- Track felt summary, dominant need, felt state, body map, interoceptive anchor, tactile grounding, protective boundary, vestibular confidence, presence, and confidence.
- Include body presence in diagnostics, inhabitance, export/import, and the brain command workspace panel.

## Ninth Implementation Slice

Finish autobiographical continuity as an explicit recent-history thread:

- Build a `/api/v1/autobiographical-continuity` snapshot from episodic memory, semantic memory, autobiographical self, narrative self, body presence, identity boundary, and global workspace.
- Track continuity thread, recent episode, defining memory, current chapter, place context, next remembered need, identity coherence, recency binding, semantic bridge, goal continuity, agency continuity, and confidence.
- Include continuity in diagnostics, inhabitance, export/import, and the brain command workspace panel.
