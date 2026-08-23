# Entry 147 - Biophysical Vestibular Labyrinth

Date: 2026-08-20

## Status

Implemented and verified in automated tests. Embodied requalification remains
required before the balance improvement can be accepted.

## Decision

Balance must be informed by a simulated vestibular organ in the skull, not by a
host-computed balance conclusion. DNNE therefore replaces its coarse duplicated
angular-velocity encoding with a bilateral, stateful vestibular labyrinth.

The organ supplies physical receptor evidence only. It cannot select a posture,
prevent a fall, prescribe righting, or command a muscle.

## Previous limitation

The former transducer:

- copied the same canal activation into both ears;
- discarded rotational direction by taking angular-speed magnitude;
- treated fall angle as a direct otolith shortcut; and
- injected host-computed dynamic support-margin loss into the vestibular nerve.

That was sufficient to prove a wire path but not to model the inner ear. In
particular, it could not provide the opponent signal needed to distinguish the
direction of rotation.

## Implemented labyrinth

### Semicircular canals

Each ear now owns independent horizontal, anterior, and posterior canal state.
Skull angular velocity combines physical body rotation with measured trunk and
neck articulation derivatives. The six canal-plane inputs form the bilateral
horizontal, LARP, and RALP push-pull pairs.

Each canal has:

- tonic resting afferent activity;
- direction-dependent excitation and inhibition relative to that baseline;
- a fast cupula response;
- a five-second adaptation state for sustained rotation; and
- an opposite after-response when rotation stops.

The implementation deliberately keeps each canal independent so later lesion,
asymmetry, compensation, and plasticity experiments do not require another
architectural rewrite.

### Utricle and saccule

The utricular and saccular populations receive a low-pass gravito-inertial
vector in skull coordinates. It combines:

- gravity transformed by physical fall, trunk, and neck orientation; and
- linear acceleration derived temporally from body-local velocity.

Directional populations represent left/right and fore/aft utricular evidence,
plus up/down saccular evidence. Static tilt therefore recruits otoliths without
inventing canal rotation. As in biology, this signal alone contains a
gravity-versus-translation ambiguity that downstream vestibular, visual, and
cerebellar circuits must resolve.

### Boundary correction

The headless world now honours the physical-frame contract and publishes
body-local rather than world-axis linear velocity. Dynamic support margin was
removed from vestibular afference. Support geometry remains available through
proprioceptive and physical balance pathways; the inner ear cannot directly
sense a support polygon.

## Existing neuronal route

The new receptor populations enter the already implemented route:

1. Vestibular afferents -> vestibular nuclei.
2. Vestibular nuclei -> cerebellar vermis and fastigial nucleus.
3. Vestibular nuclei -> reticular formation and spinal motor populations.
4. Vestibular nuclei -> parietal spatial circuits.
5. Oculomotor and vestibular pathways exchange activity for later
   vestibulo-ocular stabilisation.

All downstream interpretation, adaptation, and motor competition remain
neuronal. No ML policy or scripted balance action was introduced.

## Verification

Automated tests establish that:

- the resting labyrinth has symmetric canal tone and a gravity-bearing saccule;
- reversing yaw reverses left/right horizontal canal dominance;
- sustained yaw adapts and stopping produces an opponent after-response;
- neck pitch recruits the correct anterior/posterior canal pairs;
- static roll recruits directional otolith populations without canal motion;
- host-computed support margin cannot enter the labyrinth; and
- sequence resets cannot generate a false head-acceleration burst; and
- the complete Release suite passes 752 tests with no failures.

## Evidence basis

- Semicircular canal cupula and afferent dynamics:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC6966309/
- Directional and temporal coding by canal afferents:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC3000935/
- Otolith gravito-inertial mechanics:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC6451982/
- Utricular and saccular acceleration transduction:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC5980960/

This is a biologically motivated simulation, not a clinically validated human
labyrinth model.

## Live acceptance gates

- Ordinary head turns produce clear bilateral canal opposition without a
  righting event.
- Sustained rotation adapts without becoming a permanent motor command.
- Static lean produces persistent otolith evidence while canal activity settles.
- Falling/righting transition counts fall substantially from the Entry 146
  baseline without suppressing a real unsupported fall.
- All neural services, body frames, and sensory dispatch loops remain healthy
  throughout a predator-suspended observation.
