# Entry 131: Neuronal Righting Reflex

## Observation

The first full physical-balance run exposed an absorbing mechanical state. The
avatar could fall correctly, vestibular and somatic receptors continued to
report the event, and locomotion was suppressed while horizontal, but the
balance plant had no physically permitted transition out of `fallen`.

The live run remained healthy at 119 services with no tick failures. The body
stayed at approximately 86 degrees pitch and roll with zero forward speed while
descending motor activity continued to move the limbs. Four somatic ingress
rejections occurred during startup and did not continue increasing.

## Correction

The body now exposes a righting transition governed by physical and neuronal
conditions:

- the avatar must be grounded;
- a sustained descending stand command must be present;
- measured hip, knee, axial, and shoulder extensor force must be sufficient;
- a brief commitment interval rejects isolated motor spikes;
- loss of the command or muscle support returns the body to falling;
- completing the rotation returns the body to marginal support, where ordinary
  balance dynamics immediately resume.

The host does not decide to recover. It supplies only skeleton mechanics and
muscle-force consequences for neuronal output. No machine-learning policy,
scripted get-up decision, or host-authored behavioural controller was added.

## Additional finding

A posture label previously counted as broad support before the pelvis, knees,
chest, or head actually contacted the ground. Broad support is now derived only
from measured body contact. A supported neuronal sit or lie request may produce
a controlled descent while the feet still bear weight, but the label cannot
convert an already falling or fallen body into a supported state.

## Verification

Focused balance tests cover all three boundaries:

1. a fallen body remains fallen without descending righting drive;
2. sustained stand drive plus extensor force can restore upright posture;
3. a sitting label without body contact does not invent broad support.

## Neuronal closure

The subsequent live run proved that mechanics were no longer the limiting
factor. After an uncommanded fall, both posture selection and stand output fell
to zero while the neuronal clock and spike dispatch continued normally. The
vestibular and proprioceptive receptors were reporting the event, but ordinary
topographic projection scattered those populations before they reached the
spinal posture lanes.

The reflex is now closed through neuronal populations:

- otolith pitch, otolith roll, dynamic support-margin loss, center-of-mass
  displacement, and support narrowing enter a dedicated righting ensemble;
- that ensemble preserves its lane through the existing vestibular nuclei,
  vermis, fastigial, reticular, vestibulospinal, and proprioceptive-reflex
  connectome routes;
- left and right spinal cord instances expose the firing rate of the descending
  stand lane;
- only bilateral spinal activity receives reflex motor authority;
- an actively selected neuronal sit or lie lane inhibits the reflex, allowing
  intentional floor postures without consulting a host posture label;
- the body still requires sustained stand drive and measured extensor muscle
  force before its physical righting transition can succeed.

No body-state flag, machine-learning policy, scripted get-up sequence, or
host-authored behavioural decision participates in the reflex. The host maps
receptor populations, neural lanes, muscles, and physical constraints; the
spiking network supplies the righting drive.

## Extended verification

Focused coverage now includes receptor specificity, subthreshold rejection,
vestibulospinal lane preservation, bilateral authority, unilateral rejection,
voluntary floor-posture inhibition, motor-effector translation, and physical
recovery. The complete DNNE suite passes 595 of 595 tests, and the Release build
completes with zero warnings and zero errors.

## Live closure finding

The first end-to-end run after neuronal closure showed bilateral spinal righting
activity reaching the body and lifting it from approximately 86 degrees of tilt.
It also exposed an unstable handoff: locomotor pattern activity continued while
the body was rising, competing posture populations could interrupt recovery, and
the balance plant would not accept another righting command until the interrupted
motion had completed a second fall.

The correction remains neuronal in authority. A sustained descending stand lane
now recruits spinal reciprocal inhibition of incompatible gait and floor-posture
patterns while the measured body is falling, fallen, or righting. Righting may
catch an active fall, and ordinary balance resumes only after the feet support
the body and lateral center-of-mass momentum has settled. No stand command is
created by the host: without bilateral descending activity and extensor force,
the body still falls and remains down.

The release side is also neuronal. Motor authority now requires coincident
bilateral firing in both the primary vestibular/proprioceptive righting ensemble
and the descending spinal stand ensemble. The primary afferents have no recurrent
return from the motor loop, so upright receptor input naturally removes that
coincidence and releases the reflex even while downstream activity finishes
decaying. This prevents a transient fall from becoming a latched stand command
without consulting a host posture flag or adding a timeout policy.

## Tract identity and distributed timing

A controlled live stimulus exposed two subtler faults hidden by aggregate
action-lane diagnostics:

1. ordinary vestibular and proprioceptive neurons can share the numerical stand
   lane with fall-sensitive neurons;
2. a structure processed once per scheduler rotation was decaying its reflex
   trace by only its local one-millisecond work slice, ignoring the simulation
   time elapsed while other structures ran.

Righting evidence is therefore no longer inferred from a lane firing rate.
Named fall receptor input must make its postsynaptic primary-afferent neuron
fire, producing a spike-confirmed `ReflexDrive`. The resulting spike carries a
backward-compatible righting-tract identity bit through declared vestibular,
cerebellar, reticular, and spinal projections. Untagged traffic in the same lane
cannot enter the reflex arc. This bit identifies the axonal circuit that carried
the spike; it contains no posture decision, motor command, or body-state flag.

The primary and spinal traces now decay by elapsed brain simulation time between
structure visits. Continuous fall-sensitive input renews them; removal of that
input releases them even when the distributed scheduler is rotating across the
full network.

The diagnostic run demonstrated the complete bilateral path before the timing
correction: primary fall evidence reached `L=0.432/R=0.428`, tagged descending
spinal output reached `L=0.790/R=0.800`, and the motor controller emitted a
righting drive of `0.428`. The run also proved that untagged spinal stand-lane
traffic is rejected and that one-sided evidence cannot acquire bilateral motor
authority.

The final rebuilt live proof used a balanced primary-afferent volley with the
world paused. Bilateral primary evidence reached `L=0.275/R=0.289`; tagged
spinal output reached `L=0.677/R=0.536`; and righting authority reached `0.275`.
With no further fall input, righting evidence declined to `0.029` at tick 521
and `0.009` at tick 740, with the stand output declining to `0.030` and `0.009`
respectively. This demonstrates activation, bilateral neural closure, and
unlatched release on the full 119-service network.

## Articulated-plant closure

Visual observation after the first closure proof found the avatar still lying
flat even though telemetry reported `righting` and the descending stand drive
was near one. Repeated live samples showed the fall pitch and roll fixed at
approximately `+/-1.357` radians. The isolated balance test had supplied an
artificial `0.80` force fraction, while the real articulated plant produced only
about `0.05` from its resting muscle tone. The neural command reached the body,
but its motor units never generated enough measured force to rotate it.

The articulated plant now converts an active descending righting signal into
temporary motor-unit recruitment across hip and knee extensors, directional
axial muscles, obliques, and shoulder support muscles. Recovery speed remains a
function of the force those muscles actually produce. Removing the neuronal
signal removes the recruitment and a fallen body remains down. The measured
righting-force fraction is included in physical balance telemetry for future
end-to-end diagnosis.

A new plant-boundary regression test applies real external torque until the
articulated body is below `30%` upright, then supplies only descending neuronal
stand drive. It verifies gluteal and quadriceps recruitment, restoration of
standing height, and return to stable upright posture. The complete suite now
passes 595 of 595 tests; the Release build remains at zero warnings and errors.
