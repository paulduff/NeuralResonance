# Entry 151 - Homeostatic Salience, Gait Handoff, and Tissue Consequence

Date: 2026-08-22

## Status

Implemented and verified offline. The DNNE stack remained stopped and the
fresh network state was not advanced. A new live acceptance run is required.

## Evidence from Entry 150

The ordinary-mode acceptance run remained technically stable for 3.44 hours
and 373,874 world ticks with 119 of 119 services healthy and no tick failures.
It travelled 127.59 metres, but visited only 33 of 13,462 terrain cells. All
692 interaction attempts ended out of reach, so no food or water was consumed
and ten metabolic deaths followed.

The objective gait report also showed an incomplete reciprocal pattern:
1,285 alternating swing transitions, 1,804 repeated same-side transitions,
and 72.3% double support. Local nociceptive source expiry worked, but a physical
contact could remain for 76 seconds without a corresponding physical tissue
consequence.

## Homeostatic salience and earned reward

- Energy deficit now recruits a distinct arcuate AgRP/NPY-style hunger
  population; osmotic deficit recruits a distinct lamina-terminalis-style
  thirst population.
- The nucleus tractus solitarius now has an excitatory homeostatic projection
  to the lateral hypothalamic area in addition to its arcuate, parabrachial,
  autonomic, and thalamic relays.
- Actual increases in stored energy and hydration emit separate, need-weighted
  phasic VTA populations. Restoration received while deeply depleted produces
  stronger teaching evidence than the same restoration near satiation.
- Respawn restoration remains explicitly excluded. Death and reset cannot be
  learned as an appetitive outcome.

The host reports body chemistry and its change only. It does not label food,
water, destinations, or desired actions. VTA, accumbens, basal-ganglia, and
cortical plasticity remain responsible for learning which preceding neuronal
activity predicts restoration.

## Reciprocal gait handoff

The spinal gait oscillator now uses plantar recontact as phase evidence. Once
a swing has passed its apex, recontact closes that active half-centre and
releases the contralateral half-cycle. Loss of the contralateral stance foot
still slows the oscillator rather than manufacturing support.

This is sensory entrainment of a physical central-pattern generator. It does
not select a direction, destination, or movement; without neuronal locomotor
recruitment the oscillator remains inactive.

## Physical tissue consequence

- A new contact exposure model converts genuinely severe first-contact impulse
  into bounded immediate tissue loss.
- Sustained non-foot pressure begins accumulating slow tissue damage only after
  an eight-second grace period, scaled by force, contact area, duration, and
  frame time.
- Ordinary plantar support is exempt; severe foot impact is not.
- The world snapshot and persistent v6 run report record total contact tissue
  damage, impact events, sustained-pressure episodes, and the most damaged
  body region.
- The same force, impulse, pressure, duration, position, and surface normal
  continue through the existing local Merkel, Meissner, Pacinian, Ruffini,
  free-nerve-ending, and spinal-withdrawal afferents. Tissue loss also returns
  through the visceral body frame.

No classifier, machine-learning policy, scripted goal, or host-authored action
was introduced.

## Acceptance gates

- Hunger and thirst populations must rise with their own physical deficit and
  fall after earned restoration.
- Food and water contact must produce distinct phasic VTA evidence without any
  reward on respawn.
- Alternating swing transitions must exceed repeated same-side transitions in
  a sustained locomotor run, with both soles showing measurable clearance.
- Ordinary standing must produce zero contact tissue damage.
- Severe impacts and prolonged non-foot loading must produce bounded local
  nociception and graded tissue loss without tick failures.
- Interaction success, food consumed, water interactions, physical deaths,
  and the new damage diagnostics must all be retained in the final report.

## Offline verification

- Homeostatic and hypothalamic focused suite: 30 passed, 0 failed.
- Articulated gait focused suite: 38 passed, 0 failed.
- Physical consequence, report, and editor-contract focused suite: 39 passed,
  0 failed.
- Full suite: 779 passed, 0 failed, 0 skipped.
