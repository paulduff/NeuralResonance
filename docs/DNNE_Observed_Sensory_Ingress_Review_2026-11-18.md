# Actual sensory events: remaining DNNE ingress boundary

Static source inspection while sensory precision `ddef1a` was pending found
the following integration requirements. No pending output/process was inspected,
and no model, test, live transducer or service was run or changed.

`PhysicalBodyTransducerRuntime.AddPopulation` creates frame-timestamped messages
from activation. It addresses VestibularAfferents as both source and recipient,
shares a receptor-level source label across fibres, derives release parameters
from activation and can choose BURST from activation/tick parity. It remains a
legacy stimulus generator, not the owner of the new actual sensory axon model.

The isolated `AvatarAfferentEventObserver` draft instead follows an actual cable
node and returns its accepted crossing time, original cell/session, source
instance/neuron, observation fingerprint and ordinal. Its twenty prospective
cases remain UNRUN. Do not connect that unqualified draft to live control.

## Required source-to-recipient semantics

1. One physical afferent owns its observed events. Central routing must not
   simulate another copy of that afferent in the VestibularAfferents service.
   The first declared target is a real receiving vestibular nucleus cell.
2. A projection's anatomical identity and recipient are declared before the
   epoch. A new delivery ID may distinguish branches of one source event, but
   the original event ID, crossing time and source cell remain recoverable.
   `StableNeuralIdentity.Contact` already separates projection from receiving
   synapse and includes the actual target instance/cell.
3. Delay begins at the observed axonal site. Do not add the already simulated
   cable's travel time again. Declare any remaining tract/synaptic delay and its
   uncalibrated assumptions. Do not replace the source time with a frame boundary.
4. Receiving biology cannot advance past the source's known causal horizon.
   Quiet intervals must carry progress as well as intervals containing events;
   packet retries must not release transmitter twice or advance the source again.
5. Preserve the actual source observation, accepted delivery and receiving
   contact/membrane response separately. Transport success does not establish
   afferent firing, calibrated nucleus response, balance or recovery.

## Existing receiving code to reuse or extend explicitly

`StructureEngine.EnqueueSpikeBatchAsync` validates targets, applies capacity and
message-ID deduplication, then computes a generic integer conduction delay.
`ProcessTickAsync` aligns integration to queued delivery boundaries and keeps
late scheduled/processed times separate. The new isolated ingress should retain
those queue/causality protections while using explicitly declared sensory delay.
It must not route the same event through both the old and new paths.

`ResolveInboundNeuronIndex` honours `IsolatedSynapticTargets` first, otherwise
uses righting/withdrawal/topographic rules; a named TargetNeuronId alone is not
an explicit recipient guarantee. That isolated map currently permits fresh
cortical/RedNucleus populations, not vestibular nuclei. Extend the isolated
recipient boundary deliberately, with source/epoch/target checks and fingerprints;
do not globally bypass normal routing merely because a packet names a cell.

The runtime assembly does not reference NRE.SimAvatar. Put any shared immutable
event/route contract at the protocol boundary rather than introducing a circular
brain/body project dependency. Exact retry and overload rejection require
bounded, explicit ownership; a successful enqueue must not masquerade as an
acknowledged physiological response.

The operator's completed motor-review failure interrupted implementation at this
source-audit stage. No new projection or receiving ingress has been implemented
in this checkpoint. Resume after the review workflow repair, without treating
these design requirements or the draft observer as tested integration.
