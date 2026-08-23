# Entry 152 - Recumbent Truth, Plantar Semantics, and Metabolic Fatigue

Date: 2026-08-22

## Status

Implemented and verified offline. The DNNE stack remained stopped and its
graceful checkpoint was not advanced. A fresh-network observation run is the
next acceptance step.

## Evidence from the Entry 151 run

The acceptance run survived 3.28 hours and 356,366 world ticks at 30.17 Hz.
All 119 services were healthy, no tick failed, and the only transport fault was
one recovered six-second body-input timeout among 216,176 accepted frames.

The biological report nevertheless exposed three mechanical inconsistencies:

- fine-grained heel and forefoot regions accumulated damage during ordinary
  plantar support;
- simultaneous head, chest, pelvis, thigh, and shin contacts persisted for up
  to 879.5 seconds while balance reported only 5.1 seconds fallen;
- bilateral iliopsoas, gluteus medius, and hamstrings reached complete fatigue
  and zero force, but root locomotion retained a hidden minimum reserve.

These were model defects rather than neuronal learning failures.

## Plantar tissue semantics

All anatomical regions beginning `left_foot` or `right_foot` now receive the
same plantar semantics as the parent foot. Ordinary load and sustained stance
pressure do not damage tissue. The higher plantar impact threshold remains in
force, so a genuinely severe heel or forefoot strike still produces immediate,
graded tissue loss.

## Recumbent contact truth

Balance now distinguishes internally distributed nominal support from actual
upward collision contacts. A multi-region axial collision pattern involving
the chest plus head, pelvis, or leg surfaces is direct evidence of recumbence.
It arrests motion as a fallen body; it can no longer be relabelled as upright
`broad_support` or pass passive-recovery gates while those contacts persist.

Neuronal righting remains possible. It must supply sufficient descending drive
and muscle force, and completion is accepted only after the recumbent collision
pattern has physically cleared. Controlled lying and supported sitting retain
their existing semantics.

## Fatigue closes the mechanical loop

The load-bearing hip, coronal hip, knee, ankle, and ankle-roll antagonist pairs
now contribute their measured fatigue capacity to root locomotion. The existing
force model remains authoritative, but exhausted prime movers can no longer
leave a host-provided 22% translation reserve behind.

Muscle fatigue also emits a bilateral metabolic interoceptive population into
the visceral afferents. Local Group III/IV somatic distress remains anatomically
sided, while whole-body metabolic fatigue contributes to homeostatic deviation
and strengthens aversive evidence when force is expended without motion.

The host does not command rest, choose an action, or label a desired behaviour.
It reports muscle mechanics and chemistry; the neuronal network must learn
whether reducing recruitment, changing posture, or resting improves that state.

## Acceptance gates

- all fine-grained plantar regions must remain damage-free under ordinary
  standing pressure;
- a severe fine-grained heel impact must still damage tissue;
- actual multi-region recumbent collisions must report `fallen`, low upright
  fraction, and maximum balance error;
- sitting, crouching, lying, and independent ankle mechanics must remain valid;
- sustained load-bearing exhaustion must reduce root locomotion toward zero;
- fatigue must reach both local somatic nociceptors and bilateral visceral
  metabolic afferents;
- the next long run should show agreement between continuous whole-body contact
  duration and fallen/righting phases, with lower false plantar tissue loss.

## Verification

- Focused tissue, balance, articulation, and transducer suite: 93 passed,
  0 failed, 0 skipped.
- Full suite: 788 passed, 0 failed, 0 skipped.
