# Folded Archive Entry 115: Laptop WorldSim Physiology and Homeostasis

Date: 2026-08-08

## Purpose

Apply the corrective rung identified by the extended WorldSim run while
preserving DNNE's neuronal-only architecture. These changes are suitable for
the present laptop and do not require the Tartarus cluster or RTX workstation.

## Neuronal Authority Boundary

No machine-learning library, policy network, reinforcement-learning agent,
classifier, scripted navigation routine, semantic action chooser, or host goal
system was added.

The division of responsibility remains:

- the brain selects movement and emits a general manipulator signal through
  neuronal circuits;
- the avatar transduces neuronal output into peripheral motor drive;
- WorldSim applies geometry, collision, body capacity, object contact, and
  physiological consequences;
- sensory and visceral consequences return as neuronal spikes;
- learning remains local Hebbian plasticity, eligibility traces, synaptic tags,
  and neuronal circuit dynamics.

The simulator can reduce movement when the physical body is failing, just as a
damaged or exhausted biological body cannot fully express intact motor intent.
It does not alter the direction, target, or meaning of the neural output.

## Implemented Changes

### Physical lifecycle

WorldSim now distinguishes viable, incapacitated, and dead physical states.
Exhausted energy or critically low tissue suppresses the body's motor and
manipulator capacity. Terminal tissue loss schedules a deterministic physical
respawn after eight seconds, restores bounded energy and hydration, resets the
physical pose, and preserves the running neural substrate.

The metabolic rate was reduced from 33,600 J/sec to 3,360 J/sec. A fresh
6,000,000 J body therefore has approximately 29.8 minutes of awake reserve
before food, activity scaling, or sleep effects, instead of approximately 179
seconds. This remains a deliberately compressed experimental physiology.

### Motor expression and physical interaction

The body translates the same bilateral neuronal drive into twice the forward
speed and retains peripheral drive longer between sparse neural bursts. It
still cannot steer, choose a destination, or recover through a host policy.

A clear food affordance is placed several metres ahead of the initial physical
pose. This changes only the environment's opportunity distribution. The brain
must still perceive it, approach it, face it, and emit the manipulator signal.
No movement or interaction is performed automatically.

Interaction telemetry now distinguishes out-of-reach, outside-cone, occluded,
and unavailable-body failures. Pickups also require an unobstructed physical
segment. This turns the previous zero-success result into evidence that can
separate perception, locomotion, orientation, contact geometry, and effector
timing.

### Runtime logging and interrupted uploads

Routine ASP.NET request and HTTP-client information logs are suppressed to
warning level by default in ControlProgram and structure hosts. DNNE's bounded
aggregate request profiler and transport telemetry remain available. Set
`NRE_VERBOSE_FRAMEWORK_LOGS=true` only for a short diagnostic session when
individual framework request records are necessary.

Interrupted or cancelled visual-frame bodies now produce controlled HTTP
responses instead of escaping as noisy application exceptions.

### Neuronal synaptic homeostasis

Stable receptor GUID identity already existed. The missing mechanism was
bounded retention of historical inbound synapses. Persistence now applies a
deterministic homeostatic ceiling:

- high-volume sensory structures default to 65,536 inbound synapses per
  bilateral instance;
- other structures default to 262,144 inbound synapses per instance;
- `NRE_SENSORY_SYNAPSE_MAX_INBOUND` changes the sensory ceiling;
- `NRE_SYNAPSE_MAX_INBOUND` overrides the ceiling for every structure.

When a population exceeds its ceiling, the substrate removes the least
neurally supported connections first. Retention is ordered by repeated use,
then Hebbian weight divergence, eligibility trace, synaptic tag, pre/post
traces, and recency. Stable GUID ordering resolves exact ties. This is synaptic
homeostasis inside the neuronal engine, not ML model selection or a behavior
policy.

Existing oversized files are pruned when their structure next loads and saves.
No manual deletion is required.

## Verification

- full solution Release build: zero warnings and zero errors;
- full regression suite: 419 passed, zero failed, zero skipped;
- neuronal-only causal and authority preflight: 100 passed;
- circuit audit: every listed structure reported `OK`;
- cortical functional benchmark: `PASS`, with 100% stream separation,
  learning, persistence, and adaptive output gating;
- source guard rejects ML/policy symbols in WorldSim and its physical dynamics;
- production dependency guard rejects common ML runtimes and policy engines;
- homeostasis test proves deterministic retention of repeated and tagged
  synapses over weak unused connections.

The qualification status is `PREFLIGHT_PASS_LIVE_REQUIRED`. The next visible
embodied run is therefore the remaining gate for this rung.

## Next Measurement

Run the complete brain and visible WorldSim with seed 317. Compare against Entry
114 using interaction success rate, miss categories, distance per locomotor
dispatch, mapped terrain, vital-state transitions, log growth per minute, and
persisted sensory synapse counts. Improvement must be attributed to measured
neuronal output and physical consequences, never to a host action policy.
