# Entry 146 - Withdrawal Source Attribution and Phasic Motor Competition

## Status

Implemented on 2026-08-20. Automated verification is complete. A clean live
baseline confirms that false plantar withdrawal has been removed; the new
balance hysteresis and reach gating still require an embodied requalification
run.

## Long-run evidence

The Entry 145 observation ran for 3 hours, 4 minutes, and 47 seconds and
completed 335,116 world ticks. All 119 neural services remained healthy. There
were no tick failures, body-input failures, rejected body or somatic frames,
deaths, or tissue damage. The final state was viable, grounded, upright, and
stable.

The articulated collision repair held. Collision frequency fell from 16.45
events per second in the preceding long run to 0.424 events per second, a 97.4
percent reduction. Directional chest contact was bounded to a maximum episode
of 14.2 seconds. The remaining longest brace was a 133.4-second left-hand
contact against the negative-Z face.

The run nevertheless exposed a deeper sensorimotor problem:

- aggregate spinal withdrawal remained active for 99.24 percent of the run;
- falling and righting states changed 5,126 and 5,118 times respectively;
- stable balance occupied 79.63 percent of the run, while falling and righting
  together occupied 14.21 percent;
- iliopsoas, gluteus medius, hamstrings, and tibialis anterior ended at 100
  percent fatigue with no remaining available force;
- the adductor groups ended near 37.7 percent fatigue, confirming that the new
  adduction fatigue path operates;
- hip-coronal and ankle-sagittal populations were recruited for more than 94
  percent of the run;
- 33,820 locomotor dispatches produced only 65.3 metres of travel and 32 visited
  cells; and
- all 213 interaction attempts failed because the target was out of reach.

The current report records only the peak withdrawal drive. It cannot identify
which body region, side, afferent field, spinal channel, or motor projection
kept that aggregate above threshold. The next repair must therefore begin with
source attribution rather than another global gain change.

## Planned neuronal rung

### 1. Attribute withdrawal at its source

Extend live telemetry and the persisted world-run report with per-channel
withdrawal evidence:

- anatomical body region and side;
- mechanonociceptive afferent field and contact-normal sector;
- spinal withdrawal population and projected motor population;
- peak and mean drive, active duration, episode count, and longest episode;
- recurrent inhibition, refractory state, and fatigue-modulated output; and
- the contact, pressure, penetration, or tissue signal that initiated the
  episode.

Run one clean, predator-suspended observation with these measurements before
changing reflex gains or thresholds.

### 2. Restore phasic spinal competition

Use neuronal lateral inhibition so simultaneous withdrawal populations compete
at shared spinal motor pools. The strongest anatomically causal population may
recruit its antagonist while weaker, stale, or contradictory populations are
suppressed. Per-source recurrent inhibition and refractory recovery must allow
the protective volley to decay after its nociceptive cause disappears.

Muscle fatigue must reduce the force available to the corresponding motor
population and feed proprioceptive fatigue evidence back into the network. It
must not erase pain or contact evidence. A zero motor command remains a true
relaxed state that permits muscular and synaptic recovery.

### 3. Separate real falls from balance-state chatter

Preserve the physical support polygon, capture point, centre of mass, and raw
vestibular and proprioceptive evidence. Add distinct entry and recovery
thresholds, plus a short neuronal evidence-integration interval, to the balance
state populations. Harmless sway must not alternate rapidly between falling
and righting, while actual loss of support must still produce an immediate
mechanical fall.

### 4. Release sustained limb braces neuronally

Route local hand and arm pressure, pain, fatigue, and unsuccessful force into
the same attributed spinal competition. Sustained painful support should weaken
the maintaining motor population and recruit an anatomically valid release or
withdrawal population. Do not add a host-authored release command or escape
sequence.

### 5. Gate reach through peripersonal evidence

Use retinal depth, peripersonal-space, proprioceptive limb position, and
efference-copy populations to inhibit repeated full reach attempts while the
target remains beyond the learned reachable volume. Interaction authority must
remain neuronal; the host may report physical success or failure but must not
choose the action.

## Neuronal boundary

This rung must introduce no ML controller, statistical policy, scripted
locomotion, host-selected escape behaviour, or symbolic action authority.
Physics remains authoritative for contact and falling. Somatic and retinal
transducers provide evidence. Competition, inhibition, fatigue response,
balance integration, reaching, and action selection remain neuronal.

## Acceptance gates

- Every significant withdrawal episode is attributable to a body field, spinal
  population, and motor projection in both live and persisted telemetry.
- A clean unobstructed baseline returns withdrawal drive below threshold after
  the configured neuronal decay and refractory interval.
- Withdrawal is phasic and correlated with nociceptive input rather than active
  throughout almost the entire run.
- Hand or axial bracing releases as local pain and fatigue rise, without a host
  escape command.
- Small recoverable sway does not create rapid falling/righting oscillation;
  genuine support or capture-point loss still causes a fall.
- Fatigued motor populations lose force and recover only while their command is
  near zero.
- Reach attempts fall substantially when targets remain beyond neuronal
  peripersonal reach, while reachable targets can still be attempted.
- A predator-suspended long run completes with all services healthy, no frame
  rejection, no tick failure, and no regression in collision containment.

## Harness follow-up

The laptop startup gate expired just as all 119 services came online; the brain
became fully healthy about 15 seconds later. Increase the laptop startup
allowance or make the gate recognize late but progressing readiness so a slow
warm-up is not reported as a failed launch. This is orchestration only and must
not alter neural behaviour.

## Implemented correction

- Mechanonociceptive withdrawal is now attributed by body side, region,
  contact-normal sector, spinal action channel, and motor projection.
- Live diagnostics and persisted world reports retain source samples, episode
  count, active duration, longest episode, afferent/reflex integrals, peak
  drives, and recurrent inhibition.
- The spinal cord applies source-local recurrent inhibition while preserving an
  acute escape path for a genuinely worsening nociceptive volley.
- Ordinary plantar support is scaled against physiological pressure and impulse
  ranges. It no longer masquerades as injury; excessive pressure, impact,
  penetration, and tissue-threatening contact still recruit withdrawal.
- Righting evidence now has a 0.45-second retention interval and requires a
  restored marginal support reserve before returning to stable. This prevents
  a one-frame evidence gap from restarting the same physical fall.
- PPC near-body and peripersonal populations now gate manipulator recruitment
  against far-space activity. Arm motor populations remain free to move; only
  the interaction pulse is inhibited when neuronal spatial evidence says the
  target is outside reach.
- No host release sequence, escape policy, ML controller, or symbolic action
  selector was added.

## Live baseline after source repair

The clean predator-suspended baseline persisted as
`world-run-4b38c499ba6341aabffb08b6f3617aa5-0000023675-stopped-20260820T103715900Z.json`.
It ran for 785.543 seconds and recorded 23,675 withdrawal samples with zero
active withdrawal seconds, zero peak withdrawal drive, and no attributed
withdrawal sources. Both feet carried ordinary support load without generating
a protective reflex.

That run still entered falling 1,030 times and righting 1,029 times. It was
captured before the evidence-retention repair and Entry 147 vestibular dynamics,
so it is the comparison baseline for the next observation rather than evidence
that balance chatter is already solved.

## Verification

- Focused withdrawal, somatic, balance, and motor tests pass.
- The complete Release suite passes 752 tests with no failures.
- Final acceptance still requires a predator-suspended live run demonstrating
  low falling/righting transition counts without suppressing a real fall.
