# Reported muscle, spindle and older profile outcomes

The operator reported the dynamic-muscle window, then confirmed all remaining
tests complete. The three captures below are now reviewed. No pending capture
was inspected before that report and no test, physics step or ODE solve was
repeated during review.

## Dynamic muscle components passed

`dynamic-muscle-20261113-f9970f`, revision
`23d6a0618968d10c92cf1afaaefa7cb19354e41d`, passed all **93** declared cases:
16 new dynamic components, 13 original calcium, 15 isometric connector, 32 motor
transport, 16 replay and one manifest check. Exact source method/case counts and
distinct test identities verify. Ten saved diagnostic snapshots are retained.

The fixed-optimal control reproduces the original calcium calculation. Under a
single fixture arrival, the free fiber reaches 0.06789970595064637 m at 100 ms,
versus the held fiber's 0.068 m; yield is about 0.98059 versus one. Axial tendon
forces are 1.51513 versus 1.40952 N. The external-stretch control moves the fiber
without recruiting a motor unit. Selective recruitment, dimensional scaling,
receiver delay, identity/time rejection and transactional failure checks pass.
These are controlled component results, not calibrated physiological predictions.

All **1,931 files per C:/D: copy (93,146,420 bytes)**, frozen Git source and executed
binaries verify. Manifest SHA256:
`4EE4D08B41AE5F0AEBBFFF45FDB8E9F3AF7DFE186D47E23D95F9EBDDDFA8EFED`.
Independent adaptive traces and dynamic neuronal/body integration remain required;
the full unit suite was not run and no live muscle is replaced.

## Spindle compatibility succeeded; state concerns are now reproduced

`spindle-reference-20261113-98c085`, revision
`c0ce3e9e7a42a25d19821819b0a47e7933cc2e1d`, completed all **30 trajectories and two
boundary records**. The separate empty-options control reproduces the original
Octave argument rejection, matches three-argument and `odeset()` traces exactly,
and has maximum exponential error 4.13e-13 against the declared 1e-8 bound.
All 59,532 recorded runtime files match before/after/preparation verification;
the 18-file compatibility copy still differs by only the two declared call edits.

The saved analysis reproduces exactly: twenty descriptive step comparisons,
with no numerical acceptance or MATLAB-equivalence claim. The trajectory duration
is only 0.1 seconds and no slack controller or afferent membrane was executed.
The original mechanisms expose specific issues that finite execution cannot fix:

* Under stretch, cached bin populations differ from the authoritative state.
  At the finest step, the maximum difference reaches about 0.02955 for bag and
  0.003795 for chain in the stretch/shorten protocol. The instantaneous boundary
  shift reaches 0.29079 and 0.03198 respectively. Source inspection confirms
  `shift_cbDist` updates `y` without refreshing the cache; force and the ODE use
  `y`, while the reported bound fraction uses cached bins. Do not claim that this
  particular discrepancy alone corrupts the ODE state.
* At the exact overlap boundary, both fiber classes return approximately
  0.9319727891 after a positive-overlap history and zero after a zero history.
  The overlap setter leaves equality uncovered. The kinetics also divide by
  overlap, so simply setting it to zero is not a complete boundary repair.
* The original unevolved force has a nonscalar shape in all thirty trajectories.
  Later forces are scalar. Preserve the original initial observation instead of
  hiding it with an extra unrecorded warmup.
* Imposed shortening can produce negative stress. The below-slack condition is
  an explicit imposed-length diagnostic, not the full unloaded slack solver;
  those values alone do not qualify or condemn physiological behavior.

These findings require an explicit state/overlap repair and separate numerical
qualification before receptor-potential or afferent integration. Do not cure
them with silent cache resets, population clipping or arbitrary zero-division
substitution. Preserve the native-adapted baseline for causal comparisons.

All **1,692 files per C:/D: copy (85,527,715 bytes)**, frozen Git source and every
extracted author file verify. Manifest SHA256:
`06CAB626E4D0FE064D4279DC1C36083C00028D3048A9A5FF7F56F69FD589F5F5`.

## Older body-profile failure is already covered by the correction

`physical-observation-20261113-64ad44`, revision
`d3f148e5e281f3354b4c391f0f2b160768807abb`, has 69 passes and one failure. It is
the same stale current-version rejection fixture diagnosed in `162523`.
The later corrected `7800c5` run already passed all 74 cases and actual complete
Unity body/contact delivery. No new runtime repair or duplicate retry is needed.
Preserve this package's original FAILED status.

All **1,914 files per C:/D: copy (92,511,727 bytes)**, frozen source and test
binaries verify. Manifest SHA256:
`AE9994B81B8FAE0ECC461D5CA32CFC807D24AD69F94D435B33E1D5829233D6CD`.

The reviewer is `tools/review-muscle-spindle-profile-20261113.py`. Its separate
three-file capture, `artifacts/muscle-spindle-profile-review-20261113`, has a
verified D: copy plus matching manifest. All previously outstanding reports are
resolved. Next qualify the coupled muscle traces and repair spindle state
semantics; live sensory/motor authority and Avatar recovery are not established.
