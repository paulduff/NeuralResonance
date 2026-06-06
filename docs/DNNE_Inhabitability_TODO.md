# DNNE Progeny TODO

This is the single active TODO list for moving DNNE from a brain-like simulation toward a teachable, embodied, biologically grounded descendant system that can perceive, remember, plan, speak, learn, and maintain continuity.

This list supersedes the previous inhabitability TODO. The earlier items are considered first-pass implemented unless reopened by testing or behavior review.

## Biological Implementation Rule

- New brain functions must be neuron/circuit based.
- Runtime summaries may integrate brain state, but activation must be gated by spiking evidence or by circuit states produced by named biological structures.
- Simulators may provide sensory/body facts only; they must not contain brain behavior or decide cognition for the avatar.
- Any new high-level capability should name the nuclei/cortical areas that carry it and expose enough diagnostics to show that the circuit is active.
- Teaching, language, reward, memory, motivation, and action selection must be mediated through the brain rather than simulator shortcuts.

## Active Sequence

1. Stable Self Continuity
   - Status: planned.
   - Add a persistent autobiographical core that survives restarts.
   - Preserve "what happened to me", "what did I learn", "who is the user", and "what was I trying to do".
   - Use hippocampus, retrosplenial cortex, temporal association cortex, PFC, ACC, and sleep consolidation.

2. Grounded Language
   - Status: planned.
   - Bind words to vision, sound, touch, body state, reward, threat, hunger, shelter, objects, and actions.
   - Route comprehension and speech through auditory cortex, Wernicke, Broca, arcuate fasciculus, temporal association cortex, PFC, and motor speech loops.
   - Keep speech sparse and intentional rather than telemetry-like.

3. Real Attention And Global Workspace
   - Status: planned.
   - Let one dominant current thought or focus become available across the brain.
   - Resolve competing drives such as hunger, fear, sleep pressure, curiosity, and user commands.
   - Use thalamus, TRN, PFC, ACC, basal forebrain, basal ganglia, and neuromodulatory nuclei.

4. Developmental Learning
   - Status: planned.
   - Let competence improve over time through reward prediction, failed-goal memory, exploration, and motor refinement.
   - Train action selection with basal ganglia and dopamine loops.
   - Improve motor routines through cerebellum, M1, SMA, premotor cortex, and sensory feedback.

5. Better Internal Body Model
   - Status: started in Entry 052.
   - Strengthen body schema with posture, contact, pain/pressure, fatigue, hunger, thirst, injury, warmth, shelter, carried objects, and weapon state.
   - Feed body facts through S1, insula, hypothalamus, PPC, cerebellum, and motor loops.
   - Keep movement decisions brain-owned.
   - First slice: M1/S1 homuncular body-zone kernel plus connectome guardrails for S1/PPC/M1/SpinalCordMotor.

6. Stronger Circuit Differentiation
   - Status: planned.
   - Make structures less generic internally.
   - Prioritize V1-like visual behavior, EC/DG/CA3/CA1 sequence behavior, cerebellar correction dispatch, basal ganglia direct/indirect selection, and limbic shelter/fear/curiosity loops.
   - Add diagnostics proving each circuit contributes spikes and state.

7. Biological Teaching Loop
   - Status: planned.
   - Let the user teach the brain directly with language, reward, correction, and shared context.
   - Examples: "that is food", "that is dangerous", "come here", "do not go there", "good", "try again", "remember this".
   - Convert teaching into auditory/language input, hippocampal event memory, PFC intent, dopamine/habenula feedback, and long-term synaptic change.

8. Evaluation And Growth Loops
   - Status: planned.
   - Measure whether the brain improves across runs and after sleep.
   - Track food finding, bear avoidance, shelter recall, command following, sparse narration, and memory consolidation.
   - Add repeatable behavioral trials so progress is visible rather than assumed.

## Suggested First Implementation

Start with item 7, Biological Teaching Loop.

Reason: it is the bridge from "simulation that behaves" to "brain that learns with the user". It also exercises language, memory, reward, attention, and action selection together, which makes it the best next integration point.
