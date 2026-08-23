# Entry 149 - Live Gait Recruitment and Contact Release

Date: 2026-08-20

## Status

Implemented and verified in focused tests. A fresh predator-suspended live run
is the acceptance gate.

## Long-run evidence

The Entry 148 stack completed a 6 hour 14 minute observation with 679,252
world ticks, 119 healthy services, no tick failures, no deaths, and intact
tissue. Balance improved materially over the preceding run:

- stable time increased from 27.0% to 52.1%;
- falling time decreased from 48.2% to 15.6%;
- righting time decreased from 23.0% to 4.8%; and
- falling/righting entries decreased from 4.85 to 1.57 per second.

The remaining behavior was inefficient. The avatar travelled 35.1 metres and
visited ten cells despite 42,836 locomotor dispatches. Dynamic balance was
active for only 0.04% of the run and its largest allowance was 7.2 mm. Hand
withdrawal episodes lasted as long as 135 seconds, while old posture and limb
drives remained measurable long after their upstream winners changed.

## Root causes

The control program already used reciprocal inhibition, but the downstream
avatar nervous-system integrator independently accumulated every posture and
signed joint drive. That recreated stale competition at the spinal/body
boundary.

The gait plant also divided live motor drive by the absolute accumulator safety
ceiling of 240. Sustained population output reaches its physiological
recruitment envelope near 48, so the live muscle excursion was approximately
one fifth of the excursion used by the accepted gait tests.

Upper-limb nociception recruited local arm withdrawal but did not recruit the
direction-sensitive axial pools already available to chest and pelvic contact.
An arm could therefore retract while the torso remained pressed into the same
surface.

## Corrections

- A new posture volley inhibits the three losing accumulated posture traces at
  the avatar nervous-system boundary.
- Reversal of a signed limb, ankle, trunk, or orienting population releases the
  accumulated antagonist before recruiting the new agonist.
- Live motor recruitment is calibrated to the sustained neural population
  envelope of 48. The 240 limit remains the hard accumulator safety ceiling.
- Upper-limb nociceptors retain local flexor withdrawal and additionally
  project contact-normal evidence into forward, reverse, turn, or trunk-release
  spinal populations.
- World-run reports now record posture-conflict duration, peak simultaneous
  posture drives, and calibrated locomotor recruitment.

These are neuronal and physiological changes. No route planner, movement
script, target selector, ML policy, or host-authored escape direction was
introduced. The host reports contact geometry; spinal projection determines
the withdrawal population from that somatic evidence.

## Acceptance gates

- Peak simultaneous posture drives should remain at one during ordinary
  operation and posture-conflict time should approach zero.
- Sustained population output should produce visible alternating swing-foot
  clearance rather than a bilateral shuffle.
- Dynamic balance allowance should rise above the previous 7.2 mm ceiling
  during active walking without masking genuine unsupported falls.
- Hand and arm contact-normal withdrawal should shorten continuous wall-contact
  episodes and produce body release as well as local limb retraction.

## Live acceptance result

The predator-suspended run on 2026-08-21 completed 157,805 world ticks over
1.45 hours with no tick failures, no deaths, and intact tissue. Posture
arbitration passed with zero conflict seconds and a peak of one simultaneous
posture drive. Stable balance increased to 71.49%, falling fell to 2.02%, and
the maximum dynamic stability allowance rose from 7.2 mm to 75 mm.

The avatar travelled 22.96 metres and visited 14 terrain cells. Calibrated
locomotor recruitment was active for 68.9% of the run, but the report did not
yet measure swing-foot clearance directly. Physical hand-contact episodes
shortened from 71.7 seconds to 49.4 seconds, while spinal withdrawal traces
remained active for as long as 353.4 seconds and the run ended with left-hand
support. Contact release therefore passed only partially and becomes the next
rung's primary correction.

Three somatic-input requests timed out during the run. All other physical-body
frames were accepted, stderr remained empty, and the simulation stayed live
until graceful shutdown.

## Fresh-network boundary

Because Entry 149 changed downstream recruitment, posture arbitration, and
contact release, its learned synaptic state will not seed the next rung. The
238 active synapse files and graceful network checkpoint were archived at:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\synapses-runs\entry149-final-20260821T182319Z`

The active `synapses-action34-axial-v1` directory was recreated empty. The next
stack start will therefore initialize a new neuronal network while preserving
the Entry 149 state and world report for comparison.
