# Entry 049 - Better Simulation Inhabitability

Date: 2026-05-30

## Purpose

The simulations should feel less like panels and loops, and more like a body living through a world. Improvements should keep the biological rule from Entry 048: simulators provide sensory and body facts, while the avatar service and brain-facing layers mediate bodily meaning, memory, motivation, action, and learning.

## Improvement List

1. Body Event Ledger
   - Track recent bodily events such as hunger, pain, relief, shelter, impact, fear, fatigue, rest, and movement.
   - Keep the ledger avatar-owned so all projects can ask what the body has recently lived through.

2. Needs And Rhythms
   - Add slow biological rhythms for fatigue, sleep pressure, hunger waves, curiosity/restlessness, stress recovery, and rest.
   - Let rhythms influence attention, arousal, movement, voice, and body sound.

3. Reflex Layer
   - Add fast peripheral reflexes inside the avatar service: flinch from pain, brace on collision, orient toward sudden sound, slow when damaged, and seek rest when exhausted.
   - Keep reflexes bodily, not cognitive.

4. Affective Weather
   - Publish a compact felt-state output such as calm, tense, hungry, sheltered, hurt, curious, startled, or tired.
   - Let the felt state modulate action outputs.

5. Persistent Place Memory
   - Track safe places, danger places, food places, blocked places, and interesting places across sessions.
   - Preserve this as place memory rather than simulator shortcuts.

6. Better Audio World
   - Add spatial sound sources in world and maze: water, footfalls, wind, predator, impact, shelter hush.
   - Route all of them through the avatar audio lane.

7. Avatar Self-Diagnostics Panel
   - Add one non-technical avatar panel showing body mood, attention target, current action, last sensation, current need, and recent bodily event.
   - Avoid another telemetry wall.

8. Action Consequence Loop
   - Ensure every meaningful action leaves a bodily consequence: effort, contact, relief, progress, injury, fatigue, curiosity satisfaction, or frustration.
   - This should let the avatar learn from being in the world rather than only moving through it.

## First Implementation Slice

Start with the Body Event Ledger.

Reason: the ledger is the connective tissue for the rest of the list. Needs, rhythms, reflexes, affective weather, place memory, diagnostics, and consequence learning all need a trustworthy body-owned history of what just happened.

## Progress

- Added an avatar-owned Body Event Ledger in the avatar service.
- The ledger derives body events from avatar body-state and outcome inputs, including movement, impact, pain, hunger, fear, shelter, rest, relief, progress, fatigue, and curiosity.
- The ledger is bounded and queryable through the avatar service so any project can ask what the body has recently lived through without putting cognition into a simulator.
- Added avatar-owned needs and rhythms on top of the body ledger. Hunger, fatigue, sleep pressure, stress, curiosity, restlessness, recovery, and rest need are computed inside the avatar service and included in avatar action output.
- Added a peripheral reflex layer inside the avatar service. Flinch, brace, orient-to-sound, slow-when-damaged, and seek-rest reflexes are derived from avatar body memory/needs and included in action output, with conservative movement scaling or turn bias.
- Added avatar-owned affective weather. The avatar service now publishes a compact felt state such as calm, tense, hungry, hurt, tired, curious, sheltered, relieved, or blocked, and includes it in action output to modulate gesture, arousal, and body sound.
- Added avatar-owned persistent place memory. Simulators can submit place facts such as safety, danger, food, blockage, and interest through `AvatarPlaceObservation`; the avatar service merges those facts into remembered places and classifies the dominant bodily meaning itself.
- Tightened the editor webcam preview path so captured webcam frames are posted into the avatar service as sight input and the editor presents the avatar's sight output, with "Avatar Sight Input" wording instead of a direct webcam preview label.
- Improved the spatial audio lane. World and maze audio now use longer optional-input timeouts, slower/adaptive retry backoff, and additional avatar-routed cues for impact, shelter hush, wind/air, goal hum, and checkpoint safety rather than relying only on raw footstep or hazard sounds.
- Added a non-technical Avatar Self panel in the editor showing body mood, attention target, current action, current need, last sensation, and recent body event.
- Tightened the action consequence loop. Meaningful avatar action outputs now leave avatar-owned body events such as effort, tool use, and expression, so action has a bodily trace even before the world reports contact, relief, injury, or progress.

## Remaining Continuation Points

- Entry 049 implementation list complete.

## Biological Rule

Simulators may provide sensory and body facts only. They must not contain brain behavior or decide cognition for the avatar. Teaching, language, reward, memory, motivation, and action selection must be mediated through the brain or through avatar body/peripheral service layers that feed named brain inputs.
