# Entry 154 - Fresh Striatal Bootstrap and Death Truth

Date: 2026-08-22

## Status

Implemented and deterministically verified. The DNNE stack remains stopped while
the repair is audited. A fresh-network live acceptance run is still required;
the old experimental weights are not evidence for or against the new dynamics.

## Triggering evidence

The primary report was:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\world-runs\world-run-e5f19e29825a4bf6a4b0c6a8604b4dea-0000200603-stopped-20260822T122950307Z.json`

The HandSpace run lasted about 1 hour 50 minutes. Transport remained healthy,
119 structure services were present, and cortical proposal and homeostatic
activity reached the action circuit. The body nevertheless produced no useful
movement, reach, grasp, or intake. Striatum received 193,692 spikes but emitted
only six; persisted D1 and D2 activation remained zero, so strict neuronal
authority correctly denied voluntary movement.

Five physical deaths and resets occurred. The old report counted them but did
not preserve their immediate cause. Static plantar support also contributed to
`ineffective_force`, falsely teaching that standing body weight was a failed
action on essentially every physical frame.

## Root causes

1. A single corticostriatal spike reached one medium spiny neuron. That did not
   represent the convergent axonal arbor and persistent up-state needed to
   recruit sparse D1/D2 populations in a fresh network.
2. Normal bilateral foot loading was included in attempted-force evidence.
   Passive support therefore generated continuous aversive teaching even when
   no muscular action was being attempted.
3. Death telemetry retained only a count. Metabolic, dehydration, impact,
   sustained-pressure, and predator causes could not be separated after a run.

## Neuronal repair

The repair preserves the neuronal-only authority boundary:

- eligible corticostriatal and intralaminar glutamatergic axons arborize over a
  small population in the same action lane and the same D1/D2 receptor class;
- each striatal neuron accumulates a membrane-level up-state from convergent
  excitation, inhibition, local dopamine receptor state, and biological decay;
- D1 and D2 membrane voltage, synaptic current, up-state, active-neuron count,
  and population output are carried into the per-channel authority trace;
- no host code selects an action, grants a command, fabricates a motor drive,
  or bypasses the strict basal-ganglia-thalamic gate.

The fresh-striatum assay requires both D1 and D2 populations to show positive
synaptic recruitment and up-state formation and to emit non-zero activity. A
telemetry-only implementation cannot satisfy the assay.

## Teaching repair

Passive bilateral support now has a 900 N body-weight envelope. Hand loading
remains direct applied-force evidence. Foot loading contributes only above the
passive envelope, retaining evidence for forceful leg exertion without teaching
that ordinary standing is failure.

This is a physical measurement correction, not a behavioral reward rule.

## Death truth

World-run schema `dnne.world-run.v7` now records each fatal episode with:

- world tick and elapsed time;
- final stored energy, hydration, and tissue integrity;
- primary cause and its accumulated tissue-damage fraction;
- last physical interaction outcome;
- the complete damage contribution map since the previous respawn.

Damage causes are accumulated separately for energy depletion, dehydration,
combined metabolic failure, regional impact, regional sustained pressure, and
predator contact. Respawn clears the body-local accumulator only after the
fatal event has been copied into run telemetry.

## Deterministic acceptance

The focused suite passed 53/53 tests, including:

- same-lane and same-receptor corticostriatal arbor preservation;
- fresh D1 and D2 MSN recruitment and emitted activity;
- passive standing produces zero ineffective-force evidence;
- deliberate static hand force still produces failure evidence;
- death cause and contribution maps survive report capture and clear on a new
  run;
- silent Striatum and absent Motor Thalamus still deny voluntary authority.

The complete solution also builds in Release with zero warnings and errors.

## Live acceptance ladder

The next run begins from fresh weights and remains predators-suspended in
HandSpace:

1. **Circuit observation:** verify non-zero D1 and D2 membrane recruitment,
   sparse striatal output, GPi/SNr modulation, and Motor Thalamus relay.
2. **Quiescence observation:** verify that a weak or unresolved proposal grants
   no voluntary command and that muscle recruitment can settle near zero.
3. **Reach observation:** only after the complete action loop is visible, allow
   the reachable food assay to test reach, grasp, contact, intake, and earned
   biological teaching.
4. **Death audit:** if a fatal reset occurs, require a non-unknown v7 cause and
   reconcile its contribution map with physiology and contact telemetry.

No long-run locomotion, foraging, or survival claim is made by this entry. The
repair establishes a biologically plausible path out of fresh-network silence
and makes failure causally inspectable; live behavior must still earn the next
rung.
