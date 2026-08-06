# Entry 069 - Directional Wall Withdrawal

## Observation

The first live `Shadow` capture of rendered maze seed `317` exposed persistent wall hugging. The avatar accumulated more than 5,290 wall impacts while collision orienting alternated between hemispheres and failed to establish an escape trajectory.

The immutable scenario report was written as `neuronal-motor-scenario-maze-training-317-20260806-094726.md`. It rejected the scenario with these measurements:

- 2,513 distinct runtime samples;
- 3,074 new active evaluation samples;
- 1.000 mean and minimum bilateral motor coverage;
- 0.772 final confidence EMA;
- 0.939 final agreement EMA;
- 212 maximum qualified ticks against a required 600;
- no neuron-population resize justified.

All criteria passed except sustained qualification and final promotion readiness. The failure was therefore behavioral stability, not missing services, inadequate coverage, or population capacity.

## Causal Defects

Two independent defects reinforced the wall-hugging loop.

1. The rendered maze declared front, left, and right wall-proximity fields but never populated them. Body-state input therefore reported no directional tactile geometry even during continuous contact.
2. Collision orienting chose its superior-colliculus hemisphere by alternating recovery parity. This repeatedly reversed the escape cue instead of sustaining one side through a contact episode.

A second live comparison found a downstream issue after those defects were repaired. The avatar peripheral reflex evaluated generalized pain before directional contact. A painful wall impact therefore selected `flinch`, which reduced forward speed but supplied zero turn bias; the directional contact reflex was unreachable.

## Correction

The maze now ray-probes the front and both forward sides on every avatar update. Ray clearance is converted to normalized tactile proximity after accounting for avatar radius. These values drive the existing body-state input and wall-proximity channels.

Collision orienting now selects an escape hemisphere from actual left/right proximity and latches it until collision-free movement resumes. Repeated impacts in one contact episode no longer alternate hemispheres.

The avatar peripheral layer now prioritizes directional contact withdrawal over generalized pain:

- contact on the left produces a rightward withdrawal turn;
- contact on the right produces a leftward withdrawal turn;
- a symmetric head-on contact continues an existing turn, or deterministically breaks symmetry when no turn exists;
- painful contact strengthens withdrawal and suppresses forward speed rather than erasing the turn response.

This is a spinal/peripheral neuronal reflex. It changes motor output through the avatar nervous system and does not directly translate, rotate, teleport, or pathfind for the avatar.

## Validation

Focused geometry and reflex tests pass, including contact-distance normalization, side selection, contact-episode latching, painful directional withdrawal, and head-on turn continuation.

In the visible same-seed comparison, the avatar changed from near-zero movement at the wall to an active withdrawal turn, left the original contact, travelled several metres, reached `wall=0.00`, and resumed forward movement. Wall hugging was therefore broken.

The recent collision rate remained high while exploring subsequent corridors. This rung does not claim efficient maze navigation. The next navigation rung should address anticipatory wall avoidance and action persistence using the now-valid directional sensory stream, while retaining collision rate and path efficiency as measured outcomes.

## Boundary

The rejected baseline remains evidence and must not be replaced by the corrected run. A new seed-317 qualification capture is required after the code is committed and the complete brain stack is restarted from a declared snapshot. No motor mode may advance on the basis of this comparison alone.
