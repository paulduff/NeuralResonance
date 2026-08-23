# Entry 138: Contact Impulse and Balance State Repair

## Observation

The 108-minute Entry 137 observation completed without a tick failure or
death, but its report exposed two false long-run states:

- fallback hand impulse was calculated from force multiplied by total contact
  age, eventually exceeding the input contract's 1,000 Ns limit and causing
  84,260 rejected somatic frames;
- the physical body ended upright, slow, grounded, and well inside its support
  polygon while the balance phase remained `falling`.

The report also omitted fallback hand contacts and the decoded spinal
withdrawal signal, so those parts of the loop could not be audited directly.

## Repair

Fallback contact duration remains cumulative, but impulse is now force
multiplied by the current body-frame interval only. The value is finite,
non-negative, and bounded by the somatic input contract. Fallback contacts are
included in persistent run statistics rather than existing only in outbound
HTTP frames.

Balance dynamics now reconcile a stale fall classification only after the
measured body remains upright, slow, and positively supported for 350 ms. This
does not move the root, set a pose, or activate a muscle. A genuinely fallen
body still requires descending neuronal righting drive and measured extensor
force.

Upward hand contact now carries part of the body's weight. Its measured
vertical reaction is subtracted from the plantar and axial ground-load budget,
while horizontal wall pressure remains outside that vertical budget. Bilateral
blocked-locomotion load is distributed once across the feet instead of being
counted once per leg.

## Run truth

World reports now use `dnne.world-run.v3` and add:

- fallback hand contact dwell, force, and per-frame impulse;
- peak vertical-support force for each contact and for the run;
- spinal-withdrawal sample count, active duration, and peak decoded drive.

This makes the next observation capable of proving both contact acceptance and
neuronal withdrawal without inferring either from the avatar's final pose.

## Authority boundary

All new decisions remain mechanical or neuronal. The host validates physical
measurements and reconciles physical state; it does not choose an action,
author a recovery movement, or provide an ML controller.

## Verification

- Fallback impulse tests use a 450 N hand load at 25 ms and verify 11.25 Ns.
- Balance tests verify passive state reconciliation without changing the
  measured fall angles.
- Support tests verify that a 240 N upward hand reaction leaves approximately
  480 N in the body's ground-contact budget.
- Run telemetry tests verify persisted fallback contact and spinal-withdrawal
  evidence.
- The complete Release suite passes 693 of 693 tests.
- The complete Release solution builds with zero warnings and zero errors.

## Next observation

Run the full predators-suspended stack against a shelter surface. Acceptance
requires zero somatic range rejections, a non-zero spinal-withdrawal peak when
nociception is evoked, decreasing hand-contact dwell after withdrawal, and no
long-lived `falling` phase once the measured body is upright and stably
supported.
