# Isolated motor-unit calcium and isometric force candidate

The motor-path audit found that the current Avatar muscle receives an excitation
value calculated from joint-angle error. Preserving motor-axon messages repairs
only the transport boundary; it does not supply the missing event-to-contraction
mechanism. This candidate makes an identified arrival release calcium, calcium
bind to myofilament sites, and binding drive activation and isometric force.
It does not introduce a firing-rate decoder or motor policy.

`src/NRE.SimAvatar/AvatarMuscle.cs`, the event receiver and their existing tests
were inspected first. The candidate is a separate internal class,
`AvatarMotorUnitCalcium`; no live Avatar, Unity plant, host or editor selects it.
The old muscle's activation and fatigue must not be applied a second time to
this candidate's force. The live path remains labelled population-derived.

## Reference and declared reduction

The equations are independently expressed from Module 2, equations 10–16 and 20,
of [Nagamori et al. (2021)](https://doi.org/10.1371/journal.pcbi.1008707),
[*Force variability is mostly not motor noise*](https://pmc.ncbi.nlm.nih.gov/articles/PMC7971898/).
The authors' deposited
[model repository](https://github.com/anagamori/MU-Population-Model-Nagamori-2020)
is pinned at `c317be803f01965a1ca8fc4cbad1ba9156433af8`.
Numeric parameters are row 1 of `parameterMatrix` and `PTi[0]` in
`modelParameter.mat`, SHA256
`01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd`.
The independent solver reads those numbers directly; it does not read the C#
parameter constants as its source. The authors' MATLAB implementation is not
copied into the production tree or executed by the rig.

This is one slow unit from a human tibialis-anterior model, at fixed optimal
length and zero shortening velocity. Slow-unit sag is one; yield starts at its
resting equilibrium of one. This differs from the deposited population script's
zero initial yield followed by relaxation. Length adjustments disappear at
optimal length. Neither fast-unit sag nor the authors' rate-based recruitment
and spike generator is imported. The continuous equations are solved together;
the deposited forward-Euler ordering and negative-binding clipping are not used.
Consequently this is not a claim of exact native MATLAB reproduction.

With time in seconds, each arrival at `ti` contributes the causal release drive
`(1-exp(-(t-ti)/tau1))*exp(-(t-ti)/tau2)`. Two decaying accumulators evaluate its
sum without a firing-rate estimate. Calcium moves between the store, the free
pool and bound myofilament sites. Bound calcium determines a saturating Hill
target, followed by the reference activation dynamics. Isometric force is
activation times this unit's declared 0.4620037750784163 N tetanic-force scale.
The calcium variables use the reference's relative site units, not measured
Avatar calcium concentrations. Total calcium is 1.8 and total myofilament
binding-site capacity is one. Flux integrals independently account for release,
pumping and net binding.

## Identity, timing and failure behavior

Each unit has one declared motor-axon contact and an explicit neural/body clock.
An arrival must match its session, source instance/cell, projection, contact and
motor-unit identity, retain original message/source-synapse IDs, have a newer
source ordinal, and arrive exactly at the unit's current biological time.
The existing receiver validates batch completeness/replay and supplies those
arrivals. The unit applies no second conduction delay and never retimes a late
event. Its local ordinal check is not a substitute for the receiver's complete
message-identity/replay checks.

An arrival adds release-waveform state; it does not instantly set activation or
force. Coupled RK4 advances calcium and activation with analytic release values
at its stage times. Steps must be positive and at most 0.125 ms, with event
boundaries honoured by the caller. Invalid input fails before recruitment.
Non-finite or out-of-domain integration states fault the candidate; there is no
clamp, reset or hidden force floor. This isolated object has no save/resume or
live checkpoint compatibility claim. Old brain saves are never loaded.

Successful endplate transmission and excitation–contraction release after an
axonal arrival are assumptions here. Vesicles, acetylcholine receptors, muscle
action potentials, ATP supply/pump energetics, fatigue, tendon and changing
length/velocity remain outside the model. This is a mechanistic reduced muscle
component, not a complete neuromuscular junction or calibrated whole-body plant.

## Prepared verification

The focused suite contains 13 calcium cases, the existing 32 transport cases and
the solution-manifest case: 46 expected cases. It checks silence, causal release
and decay, independent release-waveform summation, calcium bookkeeping,
misaddressed/late/replayed arrival rejection and a publisher→receiver→unit path
with a fractional conduction delay applied exactly once. These are fixture
events, not endogenous neuronal discharges.

`MotorUnitCalciumProbe` records seven six-second protocols at 0.125, 0.0625 and
0.03125 ms: silence, one arrival, a pair, and 5/10/20/40 Hz diagnostic trains.
There are 21 traces with 0.25 ms samples; complete input times are retained.
The trains stop after three seconds of drive, leaving two seconds for decay.
The single/pair events have fractional times between ordinary output samples.

`motor-calcium-reference.py` uses separately expressed continuous equations and
direct sums over the prescribed arrival times. Each actual arrival splits the
integration interval. DOP853 at two tolerances and Radau supply 21 independent
reference traces. Their maximum step is 2 ms, with adaptive error control.
For numerical trial iterates below zero only, the Hill power uses an absolute
value to keep the solver's continuation defined; saved samples remain raw and
must satisfy the physical domain within 1e-12. No accepted sample is clipped.

Criteria are fixed before running:

| Check | Bound |
| --- | --- |
| Both reference contrasts, every calcium/activation sample | 1e-8 absolute |
| Both reference contrasts, cumulative flux integrals | 1e-7 absolute |
| Finest candidate calcium/activation versus tight DOP853 | 1e-6 absolute |
| Finest candidate force versus tight DOP853 | 1e-6 N |
| Calcium bookkeeping | 1e-9 relative-site units |
| Release recurrence versus direct event sum | 1e-10 absolute |
| Finest error versus coarsest error | At most 0.2 times coarsest plus 2e-8 |

Full raw traces, per-state errors, peak force, timing, force integral, late-drive
mean/range and final activation are saved. The intermediate step is reported
even though the predeclared refinement gate compares finest with coarsest.
No physiological firing-rate/force relation or physiological twitch-duration
acceptance band is asserted by these numerical criteria.

The existing committed-source preparer accepts `--muscle-calcium`; this option
is separate from the already-launched Golgi refinement window. It freezes pushed
source and pinned parameter data, builds within `source/artifacts`, checks the
required TRX cases, executes the probe/reference, retains stage logs and hashes,
and backs the finished package up to D:. Cancellation is recorded as interrupted.
Tests run only in the visible PowerShell window, without live monitoring.

Development builds for Avatar, the probe and the test assembly passed with zero
warnings/errors. The first study, `motor-calcium-20261106-7cde4b`, was subsequently
launched in its independent visible PowerShell window after the operator reported
the cell-local timing run complete. It uses frozen revision
`825d17f4890fb99310c5afc48c64d8e44fac2836`; its initial 1,264-file D: backup was
verified before launch. Results await the operator's report; no live monitoring
is used. The separate motor/Golgi window is untouched.

## Promotion gate

Review numerical results before fitting force parameters or connecting this to
spinal neurons. Subsequent work needs independently justified motor-neuron
dynamics, real emitted events, motor-unit anatomy and force distribution, then
dynamic muscle/afferent integration. Update the Avatar/Unity shared source
generator and editor when the live pathway changes. Measured posture and recovery
remain later acceptance tests; a passed isolated contraction test cannot qualify
them.
