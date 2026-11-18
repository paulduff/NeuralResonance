# Actual sensory events: remaining DNNE ingress boundary

An isolated implementation now follows this audit; see the final section below.
Live service integration and physiological qualification remain incomplete.

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

## Subsequent isolated implementation

The following code is authored and builds with zero warnings/errors. Its
prospective controls are being prepared for a visible independent window;
no test or model has run inline.

`ObservedAfferentPacket` carries a declared source epoch/cell/site/fingerprint,
an integer completed-step interval, and the original ordered event IDs and
crossing times. An empty event list explicitly conveys a quiet completed
interval. Construction defensively copies events, validates each crossing's
completed step and binds the entire packet to a content fingerprint.

`AvatarAfferentPacketSource` owns the existing actual-event observer, retains
events in a bounded outbox and seals an immutable packet. It stops source
advancement on backpressure until the exact packet is acknowledged. Retries
do not advance the membrane or append events again. A failed later observation
does not erase an earlier completed, publishable prefix. The new source identity
property does not change the observer's original identity algorithm or equations.

`IsolatedAfferentIngress` attaches only to a fresh full-size vestibular nucleus
with explicit receiving-instance identity and empty isolated save directory.
The source and route fingerprints enter the engine's isolated fingerprint.
Existing service constructors do not select it. No second afferent is simulated
in the VestibularAfferents service.

Each declared branch retains the original source event and crossing time while
obtaining its own delivery ID and receiving contact ID. Its delay is explicitly
the remaining path beyond the measured axonal site. The queue uses this exact
double arrival time; it does not add the existing generic integer tract delay.
Source/target identity is checked at admission and recipient selection, so an
explicit receiving contact is not topographically remapped.

Packet admission validates the whole interval, event sequence, all branches and
capacity before committing queue entries and source progress together. Exact
last-packet retries return the same receipt even after target advancement;
changed or older packets are rejected. An overload leaves both queue and source
watermark unchanged. Legacy spike-batch admission is disabled only for this
explicitly isolated owner, preventing duplicate parallel input paths.

Receiving ticks must be contiguous and cannot exceed the completed source time
plus the shortest declared remaining delay. Arrival at the current boundary is
permitted; an arrival in already elapsed receiving biology is rejected. This is
a one-source coupling control, not a complete multiple-source cluster scheduler.
The packet contract is currently used in process; no live RPC is added here.

The actual receiving neuron and learning equations are preserved. Its existing
LIF/receptor approximations and explicitly supplied release/delay parameters
remain uncalibrated; successful contact delivery is not proof of physiological
vestibular response. Mechanical head sampling through sensory firing, loaded
rest, multi-source modulation and the complete World feedback owner remain open.
Avatar carries the new outbox, but no Unity/editor live mode is promoted by
these component changes. Receipt fields retain source/arrival/contact evidence
for subsequent integration and observation.

## Prospective component window

`afferent_ingress_gate.py` and `start-afferent-ingress-window.ps1` freeze and run
264 cases: the earlier 181 sensory components, 20 observation cases, 20 new
packet/receiving cases, 8 declared-target, 15 connected-timing and 20 neuronal
righting regressions. The twenty observation cases are now included for their
component semantics; this does not depend on declaring the pending active
sensory precision run successful.

New cases include immutable/JSON packet round trips, source epochs, routing,
quiet progress, causal horizon rejection before mutation, exact and changed
retries, missing events, atomic overload, actual fractional-time synaptic
delivery, branch identity, cancellation, isolation, packet partition invariance,
outbox backpressure and an observed passive voltage crossing reaching an actual
receiving contact. That passive control is explicitly not afferent firing.

The authoring build is at
`D:/DNNE-build-work/afferent-ingress-authoring-20261118`. Static checks verify
the test inventory and unchanged sensory, receiving-cell and learning sources
against the completed component evidence. All test execution occurs visibly,
with complete binary/runtime evidence and D: backup. The existing motor `e9bed6`
and sensory precision `ddef1a` windows remain pending and untouched.


Observed sensory ingress controls are now launched as
`afferent-ingress-20261118-d519cf`, frozen at pushed revision
`3afd4e7623c524bedde78c1d304ac3abbfc92ac5`. Frozen preflight passed; 1,953
initial files plus manifest were D: verified before visible task
`DNNE-Afferent-Ingress-fa7811807879` started. Launch returned successfully;
no post-launch output, status or process inspected. The window includes all
264 declared cases, including the previously unrun observation controls.
Exactly THREE pending reports: sensory ingress `d519cf`, sensory precision
`ddef1a`, and saved motor comparison `e9bed6`. No completion or test success
is inferred. Live sensory firing, physiological calibration, closed-loop
World control and Avatar recovery remain unqualified. Full goal active.

## Completed ingress review

The operator subsequently reported `d519cf` complete. Its review verified all
2,497 files in each C:/D: copy, 1,944 archived Git source files, the frozen
binaries/runtime and all 264 declared test cases. Saved analysis reproduces
exactly. The separate one-file review is D: verified at
`artifacts/afferent-ingress-20261118-d519cf-review/review.json`.

The twenty observer controls and twenty receiving-ingress controls pass,
including actual fractional-time receptor delivery, target isolation, original
event/contact identity, exact retries, atomic rejection and bounded causal
advancement. These are isolated component results; the passive crossing fixture
is not physiological afferent firing. Full-unit-suite, live-world and balance
qualification remain false. No model or test was rerun during review.

Exactly TWO older reports remain pending: precision `ddef1a` and motor `e9bed6`.
The operator's repeated `b224a6` footer was the already reviewed Git/PATH failure,
not a result from its corrected retry. Neither pending capture was inspected.
The physical conversion gap and next component are documented in
`DNNE_Vestibular_Bundle_Input_2026-11-18.md`.
