# Entry 140: Wall-Support Sensorimotor Attractor

## Observation

The Entry 139 observation showed a repeatable behaviour in which the avatar
advanced toward the central shelter, contacted a wall with a hand, remained
upright against it, and continued cycling individual limb populations for a
long period.

This was not evidence of an intentional wall-seeking or grasping policy. The
headless world currently resets the avatar at `(0, 6)` with a heading of 180
degrees, directly toward the central shelter entrance. The Blazor world also
runs with motor-training mode enabled, predators suspended, and no symbolic or
host-authored world-goal steering. An initially selected locomotor population
therefore carries the body toward the shelter without a learned navigation
objective.

Once contact occurs, the wall becomes a stable sensorimotor attractor:

- hand contact enlarges the measured support polygon and can reduce balance
  error;
- improved support margin or balance produces a small positive neuronal
  teaching signal;
- continued force with little motion produces ineffective-force and
  nociceptive evidence;
- mechanonociceptive input activates spinal withdrawal channels for the arms,
  legs, and ankles;
- the resulting limb movement repeatedly renews contact instead of reliably
  producing release, rotation, or locomotor escape.

The avatar is physically bracing against the wall. It is not successfully
holding a graspable object.

## Run evidence

The stopped run report recorded:

- 7,811.54 seconds observed;
- 14.29 metres travelled across only 10 terrain cells;
- 2,897 collision events;
- 196 interaction attempts, all rejected as `target out of reach`;
- 7,452.19 seconds of active spinal-withdrawal drive;
- peak spinal-withdrawal drive of 0.980;
- 5,658.80 seconds of measured left-hand wall contact;
- one uninterrupted left-hand contact lasting 1,187.26 seconds;
- 5,755.57 seconds of reported right-hand load;
- peak combined hand load of 528.22 N;
- 7,204.58 seconds in the stable balance phase.

Together these figures distinguish purposeful exploration from a contact loop:
locomotion and environmental coverage became small while contact, stability,
and withdrawal activity dominated the run.

## Logic defect

World collision resolution identifies the contacting region as `left_hand` or
`right_hand`, but `ApplyManipulatorContact` receives only force and a lateral
coordinate. It then divides that load between both hands. A unilateral wall
contact can therefore create false contralateral hand-load feedback.

That false bilateral signal inflates applied-force evidence, can recruit the
wrong withdrawal population, and makes both arms appear to participate in a
contact made by one hand. Exact anatomical contact identity must survive the
entire world-to-body-to-brain path.

## Next repair

1. Replace lateral load redistribution with an explicitly sided hand-contact
   API. A left-hand collision may update only the left hand; a right-hand
   collision may update only the right hand.
2. Preserve the contacting region, normal, force, impulse, duration, and
   support component through physical-body and somatic feedback.
3. Confirm that unilateral contact cannot create contralateral hand pressure,
   nociception, withdrawal, or support.
4. Add neuronal action-release evidence for sustained force without useful
   displacement. Habenular negative prediction and spinal withdrawal must be
   able to release the perseverating action lane and permit a competing
   reverse, turn, trunk-rotation, or limb-withdrawal lane to win.
5. Reward hand support only for measured improvement during falling,
   righting, or genuinely unstable balance. Static wall contact must not
   continue to earn support-improvement teaching after stability has settled.
6. Retain wall support as real physics. Do not add scripted detachment,
   automatic turning, host-authored escape movement, an ML controller, or a
   hidden navigation policy.

## Regression requirements

- A left-hand wall collision produces no right-hand load or right-arm
  withdrawal activity, and vice versa.
- Transient bracing can improve a falling body's support margin.
- Stable, motionless bracing does not repeatedly generate positive teaching.
- Sustained ineffective force generates anatomically local pressure,
  nociception, and negative prediction.
- The neuronal action selector can release the blocked lane and select a
  physically available alternative.
- Contact release removes the hand from the support polygon immediately.
- No host-authored movement, symbolic goal steering, or ML authority is added.

## Implemented repair

- Physical wall contacts now enter the body through an anatomically explicit
  `left_hand` or `right_hand` path. Target interactions retain their separate
  lateral load-distribution path.
- Positive support teaching is restricted to falling, fallen, righting, or
  genuinely unstable states. A settled static brace earns no continuing
  positive prediction signal.
- Sustained ineffective force and spinal withdrawal continue to drive
  habenular negative prediction. This releases action persistence but does not
  prescribe an escape direction; competing neuronal action populations still
  determine the next movement.
- Muscle fatigue now reduces available contractile force continuously and can
  reach complete mechanical exhaustion. Active, highly fatigued muscles emit
  anatomically sided group III/IV somatic distress and negative teaching so a
  costly hold can become painful and lose neuronal persistence.
- Zero descending excitation is a true relaxed state: activation and active
  force settle exactly to zero, fatigue recovers, and no hidden arm or elbow
  holding tone remains. Passive joint mechanics remain physical properties of
  the body rather than motor commands.
- Neural righting may arrest momentum while the measured centre of mass is
  still over the physical support polygon. Once the mass itself crosses beyond
  that base, righting can no longer suppress roll or pitch: the body returns to
  the falling state and gravity advances the fall.
- Regression coverage now verifies exact hand laterality, immediate support
  removal after contact release, transient recovery teaching, no reward for a
  stable brace, fatigue pain laterality, exhaustion and recovery, and
  habenular release of a perseverating action.

## Next observation

Run the predators-suspended full stack from a clean neural state. Observe the
first shelter encounter until either the avatar disengages or 10 minutes have
elapsed. Acceptance requires truthful unilateral hand telemetry, a bounded
withdrawal episode, release of a non-productive contact lane, and renewed
terrain exploration without an automatic escape behaviour.
