# Entry 091 - Delete Host Valuation Ingress

Date: 2026-08-06

## Purpose

This rung removes the final public path by which a simulator could tell DNNE
what an event meant. Worlds may expose physical receptor facts and apply
physical consequences. They may not label those consequences as reward,
progress, novelty, safety, anxiety, threat, comfort, effort, social approval,
or appetitive/aversive value.

Those meanings must emerge from neuronal activity and learned connectivity.

## Deleted Semantic Outcome System

The following production components have been physically deleted:

- `OutcomeInputRequest`;
- `AvatarOutcomeTelemetry` and `AvatarOutcomeInputFactory`;
- the avatar outcome queue and command;
- `AvatarControlApi.PostOutcomeAsync`;
- `/api/v1/admin/input/outcome`;
- `EnvironmentalStateRuntime` and `OutcomeStateRuntime`;
- host appetitive, aversive, salience, and target-selection arithmetic;
- maze and world outcome publication for goals, food, shelter, checkpoints,
  weapons, predators, hazards, and wall impacts.

There is no compatibility switch or dormant endpoint that can restore this
path.

## Raw Receptor Boundary

Body-state ingress now accepts only quantities that a simulated body can
measure directly:

- forward velocity and turn rate;
- bilateral motor drive;
- contact and directional touch;
- ground load;
- pain;
- hunger;
- health.

The public contract no longer accepts darkness, shelter need, anxiety,
predator threat, shelter occupancy, shelter safety, or urgency as body facts.
Light remains available through visual receptors. Objects and hazards remain
available through physical vision and sound. Their significance is not
precomputed by the world.

Nearby danger is not reported as pain. Pain and reduced health are reported
only after physical contact or damage.

## Neural Ownership

Raw interoceptive state is transduced into glutamatergic receptor-like spike
trains. The first relay is medulla to nucleus tractus solitarius, followed by
the existing neuronal pathways involving hypothalamus and insula. Somatic,
vestibular, proprioceptive, and interoceptive packets use separate channel
identities. Sensory channel hashing is deterministic across process restarts,
so a learned receptive identity does not move merely because DNNE restarted.

Interpretation belongs to distributed circuits:

- NTS, hypothalamus, and insula derive homeostatic and bodily state;
- amygdala, PAG, hypothalamus, and cingulate circuits learn defensive value;
- hippocampal and entorhinal circuits derive novelty and contextual change;
- orbitofrontal, cingulate, basal-ganglia, VTA, and SNc circuits derive value,
  prediction error, and action consequence;
- hippocampal, prefrontal, and basal-ganglia circuits derive progress and goal
  relevance;
- temporal, temporoparietal, medial-prefrontal, and cingulate circuits derive
  social meaning.

The host routes receptor channels according to anatomy. It does not choose the
resulting neural state, action, or meaning.

## Editor And Diagnostics

The editor inhabitance pane now displays raw movement, touch, hunger, health,
and pain beside the measured neuronal affect and motor decoders. The deleted
environmental scalar object is not presented as brain state.

## Authority Tests

`HostValuationIngressBoundaryTests` verifies that:

- semantic outcome types are absent from the compiled assemblies;
- the body contract exposes raw receptor quantities but none of the deleted
  host judgements;
- the control program contains no outcome endpoint or scalar valuation state;
- both rendered worlds contain no semantic outcome publisher;
- interoceptive input is routed to NTS, hypothalamus, and insula.

## Population Size

No neuronal population was resized. This rung repairs causal ownership. The
next population decision must be based on measured firing, saturation,
capacity, lesion, and embodied learning results rather than compensating for a
host-authored reward channel.

## Verification

- Control program and test dependency build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Editor Release build: passed with zero warnings.
- Tests: 309 passed, zero failed, zero skipped.
- Semantic outcome source audit: no production references remain.
