# Connecting actual DNNE firing to the qualified motor receiver path

The completed cubic receptor comparison qualifies isolated postsynaptic
receptor/membrane/event calculations and delivery into the actual Avatar motor
receiver. Its presynaptic events are supplied fixtures. A motor cell responding
to those fixtures does not demonstrate authority from an upstream DNNE nucleus.

Source inspection after the operator's completion report found:

- `StructureEngine.ProcessStepAsync` retains one state-lock transaction around
  a tick and outbound drain, returning `StructureStepResult`. This is an existing
  ownership boundary to investigate, rather than scraping activity diagnostics.
- `StructureEngine` emits outbound messages only when `ModelNeuron.Step` reports
  firing. `BuildOutboundSpike` preserves a source instance, neuron, projection,
  synapse and target. It currently stamps that message with the completed
  integration-substep time and creates a new message GUID. That is not the
  isolated motor cell's cubic crossing-localization contract. Do not relabel
  these presynaptic times as precisely localized crossings.
- The live inbound queue checks identity/capacity and suppresses recently seen
  message IDs, but is not the per-contact completeness protocol of
  `ReceptorDrivenMotoneuron`. Empty spike batches are rejected. A silent interval
  needs an explicit accounted boundary; it cannot be inferred from an empty
  queue or lack of a message.
- `ReceptorMotorSource` and `ReceptorDrivenMotoneuron` are explicitly isolated;
  the live `StructureEngine` does not select them. Their input admission has
  contact-local sequence, watermark, replay and causal-arrival checks. Those
  guarantees must survive any new ownership adapter.
- The real engine's message contains additional release, receptor/modulation
  and routing information. The isolated two-state receptor has fixed reference
  contact parameters and a unit transmitter pulse. Preserve original messages
  and explicitly account for the treatment of each field; an identity match
  alone is not proof that live synaptic efficacy or learning has been transferred.

An isolated connection should own the source's advancement and publication,
carry original events without inventing spike times, account for every declared
input lane, and stop advancement under unresolved input or downstream capacity.
It must distinguish a genuinely silent completed source interval from missing
data. Use fresh isolated state, preserve the existing membrane/reference models,
and declare any diagnostic stimulation at the presynaptic cell rather than
installing a desired motor firing schedule.

Evidence should cover quiet and active source behavior, loss of downstream
response when the relevant source/contact is disabled, exact original-event
provenance, packet-cadence/retry invariance, missing-input rejection and preserved
neuronal state during backpressure. Measure source timing resolution separately
from postsynaptic localization. Connection to the previously reviewed muscle and
native body path then needs its own causal/accuracy accounting; it does not
follow automatically from receiver delivery.

This is source evidence and an integration requirement, not a newly executed
experiment or implemented live adapter. The existing code paths remain unchanged.
In parallel, the spindle requires an independent moving reference before its
mechanistic sensory path can replace Avatar's reduced length/velocity stimuli.
