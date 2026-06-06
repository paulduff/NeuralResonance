# Entry 048 - Avatar Service Sensory Inbox

Date: 2026-05-30

## Purpose

The avatar should not be a loose collection of simulator callbacks. It should be a resident service: a body-facing layer with its own thread, its own sensory inbox, and its own outputs.

World, maze, and editor should provide facts about the environment. The avatar should decide what those facts mean to its body and senses before anything is sent to the brain.

## Improvement List

1. Single avatar sensory inbox
   - Route vision, audio, body state, outcome, object observations, and language events through the avatar service.
   - Keep simulator code limited to environment facts and presentation.

2. Avatar-owned body state
   - Move hunger, fatigue, pain, health, threat, effort, shelter, and relief signals behind the avatar service.
   - Let the service publish body-state inputs to the brain.

3. Avatar-owned outcome feedback
   - Queue reward, pain, relief, progress, novelty, and effort outcomes inside the avatar.
   - Keep reinforcement as a bodily consequence, not a direct simulator shortcut.

4. Avatar-owned object perception
   - Let world and maze submit visible object candidates.
   - Let the avatar select recognition salience, confidence, hemisphere, and memory dispatch cadence.

5. Avatar action outputs
   - Let the service emit movement, tool, voice, gesture, attention, arousal, and body sound outputs.
   - Sims consume outputs rather than reaching into nervous-system internals.

6. Avatar clock
   - Add a steady service tick for decay, sensory prioritization, attention, and output generation.
   - Keep WPF timers as presentation/rendering clocks only.

7. Recent sensation memory
   - Track last heard sound, last seen object, last body pain/reward, current attention target, and current bodily mood.
   - Use this to make the avatar continuous rather than frame-by-frame.

## First Implementation Slice

Start by moving body-state and outcome events into the avatar service.

Reason: motor and audio already pass through the service. Body and outcome are the next strongest ownership boundary because they define what happens to the avatar, not what happens to the world.

## Progress

- Body-state and outcome events now pass through the avatar service.
- World and maze audio/object perception now pass through the avatar service.
- Editor webcam frames are input only; the visible preview is an avatar sight output.
- The avatar service now keeps recent sensation memory: last heard sound, last spoken/audio output, last body state, last outcome, last seen object, latest sight frame generation, current attention target, and bodily mood.
- The avatar service now has an optional steady clock. World and maze use it for motor-drive decay instead of driving decay from render/update timers.
- The avatar service now publishes bounded action outputs containing movement and tool intent. World and maze consume avatar action output for brain-driven movement instead of computing motor translation directly.
- The avatar service now publishes bounded attention outputs. Attention is derived from recent sensation memory and included in action output as look/listen/rest intent with target, hemisphere, confidence, and salience.
- The avatar service now completes the first action-output set: voice, gesture, arousal, and body-sound outputs are derived from recent sensation/body memory and included in the bounded avatar action packet.

## Remaining Continuation Points

- Move language events into the avatar sensory inbox.
- Extend the avatar clock beyond motor decay into sensory prioritization, attention, and output generation.

## Biological Rule

Simulators may provide sensory and body facts only. They must not contain brain behavior or decide cognition for the avatar. Teaching, language, reward, memory, motivation, and action selection must be mediated through the brain or through avatar body/peripheral service layers that feed named brain inputs.
