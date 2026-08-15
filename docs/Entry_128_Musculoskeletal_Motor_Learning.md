# Entry 128: Musculoskeletal Motor Learning

## Decision

Avatar now has a physical musculoskeletal plant rather than a display-only articulated rig. The brain retains behavioural authority. The body supplies biological constraints and spinal mechanics: antagonistic muscles, reciprocal inhibition, resting tone, joint stops, passive stiffness, fatigue, contact, support, and central-pattern coordination. These mechanisms do not choose goals or actions.

This rung intentionally starts a fresh neural history. The action topology has expanded from five to nine channels and compatibility with old weights is not a design constraint until a network demonstrates value worth preserving.

## Physical plant

The initial plant has 24 major muscles:

- Bilateral iliopsoas and gluteus maximus at the hips.
- Bilateral hamstrings and quadriceps at the knees.
- Bilateral tibialis anterior and gastrocnemius-soleus groups at the ankles.
- Bilateral anterior deltoid and latissimus dorsi at the shoulders.
- Bilateral biceps and triceps at the elbows.
- Rectus abdominis, erector spinae, and bilateral obliques for axial posture.

Muscle activation has finite rise and fall times. Force depends on activation, fatigue, muscle length, and contraction velocity. Unequal antagonists receive force-balanced resting tone so a neutral limb does not acquire an artificial bias.

The plant produces forward and turning force from grounded limb recruitment. The world no longer translates neuronal drive directly into root motion.

## Neural boundary

The nine action populations are left locomotion, right locomotion, inhibition, idle, manipulation, stand, crouch, sit, and lie. Selected populations are transported through the spinal motor boundary as excitatory events. There is no symbolic fallback, scripted action chooser, or machine-learning policy outside DNNE.

Every body frame returns muscle length, shortening velocity, tendon force, activation, fatigue, support, posture, upright fraction, and balance error. Muscle spindle and Golgi tendon populations cross contralaterally into the proprioceptive afferent structures. Somatic contact remains spatial and local.

## Contact model

Collision and support are sampled at both feet, shins, knees, pelvis, chest, head, and hands. Terrain height is interpolated rather than snapped to a single cell. Static objects and shelters resolve against anatomical probes, allowing sitting, lying, reaching, and narrow contacts to produce distinguishable sensory consequences.

## Learning curriculum

Predators are suspended by default. They may only be restored with the explicit `PredatorsEnabled` world option after basic motor competence is stable.

1. Quiet standing: sustain support and minimise balance error without joint-limit strikes.
2. Weight shift and crouch: vary height while preserving bilateral support.
3. Sit and stand: learn controlled support transfer and recovery to upright posture.
4. Lie and recover: tolerate broad contact, then regain support.
5. Alternating steps: learn useful displacement without collision or excessive fatigue.
6. Reach and hold: recruit shoulder and elbow muscles against local hand loads.
7. Navigation: combine binocular input, proprioception, balance, and locomotion.
8. Predators: restore only after the body can evade without destroying tissue through immature movement.

## Acceptance signals

- Posture populations do not leak into locomotor or manipulator output.
- All joints remain inside anatomical hard stops.
- Opposing muscles recruit and report force without neutral drift.
- Right-side muscle receptors reach left proprioceptive afferents and vice versa.
- Movement is caused by the musculoskeletal plant, not direct avatar translation.
- Predator suspension is visible in telemetry and is the default training state.
- Editor telemetry exposes posture, balance, support, activation, force, and fatigue.

## Next observation

The first live curriculum should begin with predators suspended and only quiet standing enabled. Record balance error, support fraction, joint-limit contacts, muscle fatigue, displacement, and action-channel selection before advancing each stage. The full brain is expected to learn recruitment and timing; the skeleton and spinal plant merely enforce what a biological body can physically do.
