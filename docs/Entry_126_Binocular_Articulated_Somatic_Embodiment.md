# Folded Archive Entry 126: Binocular, Articulated, Somatic Embodiment

## Purpose

Increase the physical information available to the neuronal DNNE without
introducing a host policy, semantic shortcut, reward label, learned ML model,
or scripted behavioral choice. The avatar remains a physical transducer and
actuator plant. Perception, pain, learning, valuation, and action selection
remain neural processes inside the DNNE.

## Binocular vision

The avatar now has two independently rendered eyes separated by 64 mm. Both
eyes have parallel resting visual axes and produce separate raw retinal frames:

- `avatar_retina_left`
- `avatar_retina_right`

The retinal transducer preserves eye identity in receptor neuron IDs, target
neuron IDs, temporal history, and stable synapse IDs. It does not fuse the
frames, calculate a depth map, match objects, or select a fixation target.
Those capabilities must emerge through the retina, LGN, V1, extrastriate,
superior-collicular, and oculomotor pathways.

A fixed 30-degree offset was rejected as anatomically inappropriate. Human
visual axes are nearly parallel for distant fixation and converge by an amount
that varies with fixation distance. Dynamic vergence can be added when it is
driven by neuronal oculomotor output.

References:

- NCBI Bookshelf, *The Actions and Innervation of Extraocular Muscles*:
  https://www.ncbi.nlm.nih.gov/books/NBK217/
- Read et al., *The binocular geometry of distance perception*:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC4058134/

## Articulated body

`AvatarArticulatedBody` is a deterministic musculoskeletal plant. Neuronal
motor drive moves the plant; the plant returns only physical measurements:

- bilateral hip, knee, and ankle angle
- bilateral foot load
- bilateral shoulder and elbow angle
- bilateral hand load
- manipulator extension
- trunk pitch and roll

Limb proprioceptors project contralaterally. Axial and reach measurements are
bilateral. The receptor populations include static and dynamic muscle spindle
signals and Golgi-like foot and hand loading. Requested movement against an
obstacle produces mechanical load, not a host-selected avoidance response.

## Somatic receptor fields

Somatic transduction now uses deterministic body-local receptor fields. Hands,
lips, and face have smaller receptive fields and denser discriminative-touch
fibers than general skin. Feet and distal limbs occupy intermediate fields.
Free nerve ending mechanonociceptors are present in every field.

Contact and damage are sampled at 20 Hz by default. Each contact retains its
body-local coordinates, hemisphere, spatial sector, surface normal, pressure,
impulse, penetration, slip, area, and duration. Two damaging contacts at
different locations therefore activate different stable nociceptor fibers.
Global tissue integrity remains a slower visceral measurement; it does not
replace local pain.

## Visual embodiment

The browser avatar was rebuilt around adult proportions with a continuous
shoulder-to-waist trunk, a smaller cranium, defined jaw and neck, ears, hands,
feet, and a short tapered hairstyle. The predator display now uses a heavy
forequarter, barrel, rump, broad head, muzzle, round ears, planted limbs, and
substantial paws so it reads clearly as a bear. These meshes change only the
display and carry no behavioral authority.

## Guardrails

- Eye frames remain independent until neuronal processing combines them.
- The host cannot inject depth, salience, fear, pain meaning, reward, target
  identity, desired movement, or action success into sensory spikes.
- The body may calculate mechanics and expose physical receptor measurements.
- The world may calculate contact, force, damage, light, and sound.
- The DNNE alone interprets those measurements and selects behavior.

## Verification

Focused regression coverage verifies:

- 64 mm eye separation and parallel resting axes
- disjoint left-eye and right-eye receptor and synapse identities
- articulated movement, supported body weight, manipulator extension, and
  lateralized physical hand load
- contralateral limb proprioception
- denser hand receptors than general skin
- spatially distinct local nociceptor fibers
- absence of host binocular fusion
- valid browser avatar and predator construction

Verification result:

- 47 focused embodiment tests passed
- 501 tests passed in the full suite; two endpoint tests reached their exact
  30-second timeout while the laptop was under combined test load
- the two timed-out endpoint groups passed in isolation (31 tests)
- the Blazor editor built with no warnings or errors
- desktop and 390-pixel mobile canvas captures were nonblank, with no
  horizontal document overflow

## Next live criterion

Run the full bilateral brain and authoritative Blazor WorldSim with seed 317.
Observe independent retinal traffic, limb proprioception, local contact and
damage activity, grounded gait, target approach, and predator interaction.
Judge behavior from the neuronal and physical logs; do not add a scripted
correction if the first behavior is poor.
