# Entry 137: Run Truth and Nociceptive Withdrawal

## Observation

The previous observation runs exposed two gaps. A final world snapshot could
show the avatar in a difficult posture, but it could not reveal whether a hand
or foot had been braced against a surface for seconds or minutes. It also could
not show how long the network occupied each balance phase or which motor lanes
dominated the run. Separately, sustained local pressure reached the somatic
afferents and pain pathways but did not yet recruit a direct spinal withdrawal
response.

This made a long run difficult to diagnose and left the fastest biological
protective route incomplete.

## Persistent run telemetry

World run reports now use `dnne.world-run.v2` and retain statistics across the
whole observation rather than only the final instant. Each report records:

- observed run duration;
- time and entry count for every balance phase, including falling, fallen, and
  righting;
- minimum support margin, maximum dynamic stability allowance, and peak body
  pitch and roll;
- peak combined hand and foot loads;
- per-contact sample count, total duration, longest continuous dwell, peak
  force, and peak impulse;
- per-motor-channel active, positive, and negative duration, integrated
  absolute drive, and peak absolute drive.

Fallback hand contacts now retain their own continuous duration and anatomical
laterality. The report accumulator is reset only with a full world reset, so a
death and respawn cannot erase the evidence from the preceding episode.

## Neuronal withdrawal route

High-threshold or sustained mechanonociceptive activity now emits
anatomically-tagged primary-afferent collaterals from `SomaticAfferents`
directly into the ipsilateral body-side `SpinalCordMotor` service. The spinal
engine maps those collaterals onto bounded withdrawal pools and lets its actual
neurons fire before any motor effect exists.

The initial withdrawal topology is deliberately conservative:

- hand and forearm pressure recruit shoulder extension and elbow flexion;
- upper-arm pressure recruits shoulder extension and shoulder abduction;
- foot, shin, knee, and thigh pressure recruit ankle dorsiflexion and hip
  abduction.

The spinal output appears through each affected action channel's measured
reflex drive. Motor decoding uses that neuronal drive to excite the withdrawal
agonist and release its reciprocal voluntary lane. Sustained nociceptive drive
also suppresses action persistence so an old voluntary command cannot keep a
limb pressed against a solid surface.

Ordinary touch does not obtain this authority. Head and torso pain continues
to ascend without inventing a host-selected movement where no unambiguous
withdrawal vector exists.

## Authority boundary

The world supplies contact position, duration, force, and impulse. The
transducer supplies receptor physiology. Spinal neurons integrate the
collateral, fire, and recruit anatomical action pools. The body plant applies
only joint mechanics and collision constraints.

No ML model, scripted behavior selector, or host-authored movement command was
introduced.

## Verification

- Focused somatic, spinal-withdrawal, motor-control, closed-loop, and world
  runtime tests pass: 113 tests.
- The dedicated neuronal-withdrawal suite passes: 11 tests, including an
  integration test through the real Izhikevich spinal engine.
- A live-runtime report test verifies elapsed time, balance history, and samples
  for all 22 exposed motor channels after real simulation ticks.
- The full DNNE suite passes: 689 tests.
- The complete solution Release build succeeds with zero warnings and zero
  errors.

## Next observation

The next predators-suspended run should test a limb held against a shelter
surface. Acceptance requires a local mechanonociceptive response, spinal
withdrawal activity on the correct body side, reciprocal release of the
opposing motor lane, and decreasing contact dwell. The resulting v2 report
must make any persistent bracing, balance-phase trapping, or motor-channel
imbalance visible even if the final snapshot appears normal.
