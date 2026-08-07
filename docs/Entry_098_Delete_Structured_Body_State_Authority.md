# Entry 098 - Delete Structured Body-State Authority

Date: 2026-08-06

## Decision

DNNE no longer accepts a host-authored body state. The legacy body-state
contract and endpoint are deleted rather than deprecated. Maze and world may
measure the simulated body, but they cannot tell the brain what those
measurements mean or where their consequences belong.

The production path is now:

`physical body -> receptor transduction -> afferent spikes -> neural connectome`

There is no compatibility switch that restores structured body authority.

## Deleted Authority

The removed `/api/v1/admin/input/body-state` route could carry source and target
structures, hemisphere, intensity, burst count, pattern, feedback, motor drive,
touch, pain, hunger, health, and host-selected routing. It could therefore
convert simulator facts into conclusions before any neural circuit received
them.

`BodyStateInputRequest`, its spike builder and target resolvers, the avatar body
queue and profile types, the host body-state cache, and all producer calls to
the old route are physically absent.

## Physical Boundary

`POST /api/v1/admin/input/body-frame` accepts only bounded measurements:

- body-local linear velocity in metres per second;
- body-local angular velocity in radians per second;
- stored energy in joules;
- tissue integrity as a physical fraction;
- core temperature in degrees Celsius;
- blood oxygen saturation;
- hydration fraction;
- sequence, timestamp, and bounded transport-source identity.

The request cannot name a pattern, sensation, need, emotion, goal, threat,
reward, action, brain structure, hemisphere, intensity, burst count, feedback
mode, or motor command. Contact mechanics travel separately through the raw
somatic contact-frame boundary established by Entry 097.

## Neuronal Afferent Gateways

Three real bilateral services receive receptor spikes:

- `ProprioceptiveAfferents` represents spindle-like velocity and dynamic
  acceleration populations;
- `VestibularAfferents` represents otolith-like linear acceleration and
  semicircular-canal-like angular velocity populations;
- `VisceralAfferents` represents energy, tissue, thermal, oxygen, and osmotic
  receptor populations.

A single generic body service was deliberately rejected. If every body signal
shared every outgoing projection, proprioception, vestibular evidence, and
visceral evidence would be mixed before neuronal routing could preserve their
different anatomical functions.

The transducer retains the preceding physical frame per input source so that
linear acceleration is derived temporally rather than supplied as a semantic
event. Receptor fibres use stable synapse identities, bilateral population
codes, and a null modulation context. Every generated spike initially targets
its own afferent service. The host does not emit a downstream percept, value,
action, or diagnosis.

## Neural Routing

The connectome owns all downstream effects:

- `ProprioceptiveAfferents -> Thalamus` provides lemniscal afference;
- `ProprioceptiveAfferents -> CerebellarGranule` provides spinocerebellar
  afference;
- `ProprioceptiveAfferents -> SpinalCordMotor` provides a neuronal reflex arc;
- `S1 -> ProprioceptiveAfferents` provides corticofugal gain feedback;
- `VestibularAfferents -> VestibularNuclei` provides eighth-nerve vestibular
  afference;
- `VestibularNuclei -> VestibularAfferents` provides efferent gain feedback;
- `VisceralAfferents -> NTS` provides vagal visceral afference;
- `NTS -> VisceralAfferents` provides efferent gain feedback.

No host route bypasses these projections to write an already interpreted state
into hypothalamic, insular, limbic, cerebellar, cortical, or motor circuits.

## Embodied Producers

Maze and world publish physical body frames directly. World collision and
ground contact now use the raw somatic contact path as well. World energy and
tissue state, and Maze tissue integrity, use physical units or fractions rather
than semantic hunger, health, or pain fields. The simulators may update physical
substrate; only neuronal activity may interpret its significance.

## Inspection

The editor lists and renders all three bilateral afferent services in the
Peripheral Sensory Interface. Their rendered positions are service anchors for
inspection, not claims that diffuse peripheral receptors occupy compact brain
volumes.

## Verification

The automated boundary and integration tests verify:

- removal of the legacy request, endpoint, route helpers, and cached state;
- rejection of malformed or non-finite physical measurements;
- bounded proprioceptive, vestibular, and visceral receptor activation;
- temporal acceleration derivation;
- bilateral populations and stable non-empty receptor synapse identities;
- null host modulation context;
- real service provisioning and editor visibility;
- strongly connected afferent, brainstem, cerebellar, thalamic, cortical, and
  spinal routes;
- absence of semantic authority fields from the physical request.

This rung removes structured body authority. It does not claim complete human
peripheral anatomy, clinically accurate receptor transfer functions, or
conscious interoception. Those claims require measured dynamics, lesion tests,
and finer anatomical services.
