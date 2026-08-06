# Entry 097 - Delete Structured Collision Authority

## Decision

DNNE no longer accepts a collision label or a host-authored neural response.
The old collision route is deleted. Physical contact now enters a bilateral
neuronal somatic-afferent service, and only the connectome may carry the result
into perception, reflex, learning, or action circuits.

The runtime path is:

`world contact -> physical contact frame -> receptor transduction -> SomaticAfferents spikes -> neural connectome`

## Deleted Authority

The removed `/api/v1/admin/input/collision` route allowed a producer to send:

- a semantic `Pattern`, including `wall_contact`;
- a source and target brain structure;
- a selected hemisphere;
- intensity and burst count;
- feedback status.

The maze used that authority to inject an already interpreted impact directly
from S1 into Superior Colliculus orienting cells. The host therefore decided
that contact was an orienting event before the neural system received it.

`CollisionInputRequest`, `BuildCollisionStimulusSpikes`, the semantic collision
literal, random collision synapses, and all producer calls to the old route are
physically absent.

## Raw Physical Boundary

`POST /api/v1/admin/input/contact-frame` accepts only measured substrate:

- monotonically identified frame and timestamp;
- body-local contact position;
- surface-normal vector;
- force in newtons;
- impulse in newton-seconds;
- penetration in millimetres;
- tangential speed in metres per second;
- contact area in square millimetres;
- duration in milliseconds;
- bounded transport-source identity.

The request cannot name a pattern, brain structure, hemisphere, value, threat,
attention target, action, intensity, burst count, or feedback mode. Measurements
must be finite and bounded. A non-zero contact requires a physical surface
normal.

## Neuronal Somatic Gateway

`SomaticAfferents` is a real bilateral structure service with Izhikevich
neurons, STDP, and a 1-6 ms conduction-delay profile. It models the primary
afferent boundary rather than a host classifier.

`SomaticContactTransducerRuntime` derives five receptor populations from the
physical frame:

- Merkel/SA1 sustained pressure;
- Meissner/RA1 pressure onset;
- Pacinian/RA2 impulse and vibration;
- Ruffini/SA2 stretch and slip;
- high-threshold mechanical afferents.

Pressure history is retained per input source and receptor sector, allowing
rapidly adapting responses to fall on repeated steady contact while sustained
pressure remains represented. Receptor sectors are calculated from body-local
geometry. Right-body input reaches the left afferent service, left-body input
reaches the right service, and midline input is bilateral.

Every generated spike has `SomaticAfferents` as both source and target, a stable
receptor-fibre synapse identity, and no modulation context. The transducer does
not emit S1, thalamic, collicular, motor, value, or action conclusions.

## Neural Routing

The connectome now owns all downstream consequences:

- `SomaticAfferents -> Thalamus` carries somatothalamic afference;
- `Thalamus -> S1` remains the cortical relay;
- `SomaticAfferents -> SpinalCordMotor` permits a neuronal cutaneous-reflex
  branch;
- `S1 -> SomaticAfferents` provides corticofugal gain feedback.

This is an explicit anatomical abstraction. A later refinement may split the
afferent service into dorsal-root ganglia, dorsal-column nuclei, dorsal horn,
and named thalamic somatosensory nuclei. The current route is still neuronal:
the abstraction is represented by services and synaptic projections, not by a
host decision.

## Embodied Producers

The maze now sends physical frames for:

- wall impact and sliding contact;
- penetrating hazard contact;
- continuous ground pressure and slip.

Wall laterality is encoded as body-local geometry rather than a hemisphere.
Approximate force and impulse are derived from avatar mass, speed, and contact
duration. The old body-state packet no longer duplicates maze touch or pain.

World-simulator contact remains on the body-state conversion list and is the
next migration target. Until that rung is complete, the world does not yet
share the new contact boundary.

## Inspection

The editor lists Somatic Afferents in the Peripheral Sensory Interface and
renders both service instances beside the spinal gateway. This model is a
service anchor, not a claim that the body's diffuse peripheral nerves occupy a
single anatomical volume.

## Verification

The test suite verifies:

- physical-only request fields;
- removal of the structured endpoint and spike builder;
- contralateral and midline receptor mapping;
- rapid receptor adaptation;
- high-threshold mechanical activation;
- stable non-empty receptor synapse IDs;
- absence of host modulation context;
- real service provisioning and editor visibility;
- strongly connected somatic, thalamic, cortical, and spinal pathways;
- malformed physical-frame rejection.

This rung removes an authority leak. It does not claim that the somatosensory
anatomy is complete or that every simulated force estimate matches a measured
human contact.
