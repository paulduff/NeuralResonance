# Entry 148 - Alternating Spinal Gait and Dynamic Balance

Date: 2026-08-20

## Status

Implemented and verified in the complete automated suite. A
predator-suspended live observation remains the acceptance gate for natural
walking in the world.

## Observation

The previous long run survived mechanically, but the avatar shuffled rather
than walked. Both feet remained load-bearing through much of the cycle, root
translation followed averaged bilateral drive, and the knee and ankle motion
did not create a reliable swing-foot clearance phase.

The run also exposed excessive balance-state competition:

- harmless single-support gait intervals repeatedly entered falling/righting;
- smoothed losing posture populations briefly coexisted with the winner; and
- rotated non-foot ground probes reported the correct height but not the actual
  lowest point of the articulated collider.

## Decision

Locomotion now uses a physiological spinal pattern-generator boundary. Neural
motor populations continue to select direction, effort, turning, posture, and
voluntary joint drives. The body plant converts that descending excitation into
alternating muscle and joint mechanics using measured sole contact.

No ML policy, scripted route, target selector, or host-authored behavioural
decision was introduced.

## Implemented gait

- A continuous bilateral phase oscillator alternates left and right swing.
- Cadence scales with neural effort and is entrained by actual stance and swing
  sole contact.
- Swing uses asymmetric hip flexion, substantial knee flexion, and ankle
  dorsiflexion so the foot clears the floor rather than dragging its toe.
- Stance receives the reciprocal load transfer and gates propulsion.
- Contralateral arm swing accompanies the stepping cycle.
- Crouching retains a shorter stride and lower mobility.
- Loss of measured sole support removes propulsion; a moving limb cannot pull
  the avatar through empty space.

## Dynamic balance

Ordinary walking includes intervals in which the static centre of mass is not
inside one foot's support patch. DNNE now distinguishes that recoverable
single-support state from a committed fall.

Active gait may use a bounded dynamic step-recovery allowance. It cannot hide:

- complete loss of support;
- severe static or capture-point displacement;
- substantial body tilt;
- stationary one-foot imbalance; or
- crossed legs, which remove the available capture-step reserve.

Fall commitment also uses longer evidence accumulation and faster decay when
physical fall evidence disappears. This reduces phase chatter without making
the body artificially stable.

## Posture arbitration

The selected basal-ganglia posture population now immediately inhibits the
three losing posture outputs. Temporal smoothing remains on the winner, but a
stale stand, crouch, sit, or lie trace cannot continue commanding the body after
another posture wins.

## Contact correction

Ground probes for rotated boxes, capsules, and spheres now use the true lowest
world-space support point. Foot pressure probes remain distributed across heel
and forefoot receptor fields on the articulated sole.

## Verification

Focused automated tests establish that:

- both legs enter distinct swing phases;
- each swing foot clears its reciprocal stance foot by at least 15 mm;
- sole load transfers in both directions;
- forward propulsion persists through the alternating cycle;
- the gait does not enter falling or fallen states during the 300-step test;
- a new posture winner immediately silences stale competing posture drives; and
- resolved ground contacts remain attached to the true articulated surfaces.

The complete Release suite passes 754 tests with no failures.

## Live acceptance gates

- The avatar takes visible steps instead of sliding both feet along the ground.
- Left and right sole load alternate in the editor telemetry.
- Knees flex during swing and feet clear 0.25 m terrain increments where the
  available step height permits it.
- Balance may report bounded dynamic instability without falling/righting
  chatter on every step.
- Real unsupported, crossed-leg, collision, and excessive-tilt states still
  produce physically truthful falls.
