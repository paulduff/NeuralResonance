# Entry 130: Physical Balance and Vestibulospinal Feedback

## Decision

The avatar's balance is a physical property of the articulated body, not a host-authored movement command. DNNE now derives whole-body centre of mass from the mass-bearing collider rig, builds the current support polygon from measured contacts, computes centre of pressure, and evaluates both static and extrapolated centre-of-mass margins. Sustained loss of dynamic support produces angular momentum and a physical fall.

No ML model, scripted recovery action, or symbolic posture controller decides how to regain balance. The body reports receptor facts; the neuronal network must learn what muscular activity reduces the error.

## Mechanical State

The body now exposes:

- centre of mass and planar velocity;
- extrapolated centre of mass for dynamic stability;
- centre of pressure and support area;
- signed support margin;
- pitch and roll orientation and angular velocity;
- stable, marginal, unstable, falling, fallen, airborne, and broad-support phases.

External collision forces are applied at their body-local contact points. Their moment about the centre of mass changes pitch and roll angular momentum. A short instability interval avoids treating an ordinary gait transfer as a fall; continued loss of support commits the body to falling. Sitting, kneeling, and lying use their wider measured contact support instead of an upright-only rule.

## Neuronal Loop

Physical state is converted to topographic receptor spikes:

- proprioceptive afferents encode COM-to-pressure displacement, support narrowing, negative margin, muscle spindle state, and tendon load;
- vestibular afferents encode linear acceleration, pitch/roll/yaw angular velocity, otolith tilt, and dynamic-margin loss;
- somatic and nociceptive afferents continue to encode local contact and force-duration-dependent pain.

The connectome now closes both postural paths:

1. Vestibular afferents -> vestibular nuclei.
2. Vestibular nuclei -> spinal motor pools through a direct lateral vestibulospinal projection.
3. Vestibular nuclei -> reticular formation -> spinal motor pools through the reticulospinal projection.
4. Vestibular nuclei -> cerebellar vermis -> fastigial nucleus -> vestibular nuclei/reticular formation for adaptive correction.
5. Proprioceptive afferents -> spinal reflex, spinocerebellar, thalamic, and cortical body-schema paths.

This gives immediate tone correction, distributed postural coordination, and a plastic cerebellar error loop while preserving neuronal authority over movement.

## Evidence Base

- Dynamic balance requires centre-of-mass position and velocity relative to the base of support, not only a static projection: https://pmc.ncbi.nlm.nih.gov/articles/PMC4184911/
- Extrapolated centre of mass and margin of stability provide a useful dynamic boundary measure: https://pmc.ncbi.nlm.nih.gov/articles/PMC6715793/
- COM, centre of pressure, and base-of-support measurements are established balance variables: https://pmc.ncbi.nlm.nih.gov/articles/PMC7826642/
- Segment masses already carried by the avatar collider rig provide the first anthropometric approximation: https://holmeslab.ca/csb-scb/Archives/Zatsiorsky-deLeva.pdf

## Next Physics Rung

The present model adds coherent whole-body balance to the existing continuous-collision plant. The next major rung is a fully dynamic articulated BEPU body: rigid segments with mass, inertia tensors, frictional contacts, constrained joints, and muscle-generated joint torques. That work should replace remaining kinematic root integration incrementally, preserving the same neuronal sensory and motor boundary.

