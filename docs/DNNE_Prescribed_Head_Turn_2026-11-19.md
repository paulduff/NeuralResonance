# Prescribed head motion for opposing sensory controls

The original completed head clip drove the declared bundle mostly toward its
inhibitory side. A quiet output cannot establish that an excitatory stimulus
would fail. `PrescribedHeadTurn` provides explicit diagnostic head inputs for
later tests in both directions, while continuous comparison `2a402c` remains
pending. It does not execute sensory cells, apply neural current, move a native
Unity body or claim brain authority over movement.

The fixture retains the supplied stationary head geometry, physical clock,
sample identities, fixed origin and world gravity. Pitch, yaw and roll refer
to the initial head's right/up/forward axes. Rodrigues rotation produces the
orientation; the sensing point follows that rotation. Point velocity is
omega cross radius, and its analytic acceleration includes both angular
acceleration cross radius and centripetal acceleration. No normalization,
reflected-frame repair or substitute pelvis orientation is used.

For turn phase u in [0,1], angle is A(10u^3 - 15u^4 + 6u^5), angular speed is
(A/T)30u^2(1-u)^2 and angular acceleration is
(A/T^2)60u(1-u)(1-2u). Units are radians and seconds. Integer sample indices
determine phase without accumulating floating-point time. At least two initial
quiet samples allow the existing skull/mechanical owner to attach before motion.
The completed turn holds its final orientation at zero velocity/acceleration;
it does not reset the head to its initial pose. A later recovery interval can
therefore retain all organ/cell state rather than resettling it.

Positive/negative excursion labels describe kinematic direction only. The
coupled anatomical projection and measured bundle/cell response must establish
which phase excites or inhibits a particular afferent. Do not assume positive
yaw always means excitation or retrospectively invert the original capture.
Actual experiments must declare distinct physical sessions and motion parameters.
The fixture fingerprint retains axis, signed excursion, timing and original head.
Existing observation ownership rejects conflicting movements under one sample ID.

These observations are prescribed test inputs, not measured native motion.
Analytic acceleration is retained for diagnostics; the existing skull adapter
still obtains its causal interval-average acceleration from completed endpoint
velocities. No future endpoint derivative is inserted into an earlier interval.
No live Avatar/Unity/editor selector or prior biological equation is changed.

## Input checks

Twenty new cases cover exact quiet samples, zero excursion, midpoint derivatives,
all three rotation axes, signed direction, independent position/velocity finite
differences, centripetal acceleration, held final pose, actual skull-adapter
consumption, random-access identities, conflicting input, rotated initial frames,
invalid declarations and parameter fingerprints. Analytic checks use a declared
0.4-radian, one-second example; this is not a selected or calibrated physiological
stimulus protocol. The finite-difference allowance is 1e-8 in the tested SI
derivatives; direct scalar/basis comparisons use 1e-12.

`head_turn_gate.py` / `start-head-turn-window.ps1` prepare 32 visible input cases:
20 new producer cases plus 12 existing skull-adapter controls. The completed,
reviewed `add4d7` source/runtime evidence is preserved; pending `2a402c` output
is not accessed. The original sensory/receiving equations remain unchanged.
Both projects build with zero warnings/errors at
`D:/DNNE-build-work/head-turn-authoring-20261119`; Python/PowerShell syntax and
the 32-case prospective inventory pass. Tests remain unexecuted until the
separate visible window runs. Record its actual package/task separately.

An input-control pass will not establish active organ/cell accuracy, physiological
firing, anatomical calibration or Avatar balance. Select and freeze the next
active sensory protocol only after the pending independent trajectory review.
