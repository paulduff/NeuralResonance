# Dynamic motor-unit mechanics: source and integration review

The qualified isolated somatic-cell/transport/unit experiment produces force at
fixed optimal muscle length. To use actual motor-neuron events in a moving
Avatar, the next candidate must couple that unit to changing muscle length and
velocity. This document records source inspection and implementation requirements;
no new dynamic model, simulation or numerical qualification was performed.

## Existing Avatar mechanisms

Inspected `src/NRE.SimAvatar/AvatarMuscle.cs`, `AvatarMotorUnitCalcium.cs` and
`AvatarIsometricMotorUnitPlant.cs`, together with Unity `MuscleCore.cs`,
`MusclePlant.cs`, `MotorUnitPool.cs` and `tools/prepare-unity-muscles.py`.
The generator is the owning route for the shared Unity muscle implementation.

The legacy muscle path uses scalar excitation, a rise/fall activation filter,
reduced normalized length/velocity force and fatigue. Joint displacement maps to
normalized length using a fixed moment-arm/range reduction. This preserves useful
mechanics, but the inspected path has no explicit reference fiber/tendon state.
Its target-error drive and diagnostic motor-unit pool do not become actual
neuronal motor authority merely by renaming their outputs.

The isolated calcium unit instead consumes identified arrivals and integrates
release, calcium binding and activation. It deliberately fixes normalized length
to one, velocity to zero and slow-unit yield to its equilibrium value of one.
The connector retains per-unit clocks and actual fractional arrival times. Its
passing source/unit result is in `DNNE_Somatic_Motor_Unit_Results_2026-11-09.md`.
Do not apply the legacy activation or fatigue filters again to that force.

## Pinned reference evidence

The existing clean Nagamori author repository is pinned at
`c317be803f01965a1ca8fc4cbad1ba9156433af8`:
[author source](https://github.com/anagamori/MU-Population-Model-Nagamori-2020/tree/c317be803f01965a1ca8fc4cbad1ba9156433af8).
The associated primary paper describes length-dependent calcium/activation,
velocity-dependent slow-unit yield, and muscle/tendon mechanics:
[Nagamori et al., 2021](https://journals.plos.org/ploscompbiol/article?id=10.1371/journal.pcbi.1008707).

`tools/capture-motor-mechanics-source-20261113.py` verifies clean revision,
archive/local equality for all eight files, parameter-file identity, exact source
locations and hashes of the seven inspected owning sources. It decodes parameter
data only; no author model is executed. The separate capture is
`artifacts/motor-mechanics-source-20261113`, with two files plus matching manifest
verified on D:. Archive SHA256:
`E89CFA1394BD0137E522E0C656D1CFCB981BC76B25C87021F8EEACACFABC8D6D`.
Parameter MAT SHA256:
`01123AEC76D1ACC18B306AE1BD9DAABFD29FE9287B54212AEF5DB3F15F719EBD`.

The existing first slow unit has tetanic force 0.4620037750784163 N and length
gains 1.9 above optimal and approximately 1.3 below it. The preserved MAT bytes
contain the exact values. Whole-muscle source geometry is tibialis anterior:
optimal fiber length 6.8 cm, tendon reference length 28.875 cm, mass 0.15 kg and
pennation 0.16755160819145562 rad. These are not anatomical calibration for all
38 Avatar muscles, nor a matched physiological pairing with the cat MG neuron.

## Mechanisms and source-ordering issue

In `MU_population_model.m`, normalized length changes calcium-binding and
activation parameters (`k_3`, `N`, `K`, `gamma`), using separate gains above and
below optimal length. Slow-unit yield evolves with absolute normalized velocity.
Active force also depends on length and velocity; passive fiber and series
tendon forces act through explicit mechanical states. A force multiplier applied
after the fixed-length calcium unit would omit the length-dependent activation.

Source lines 204–210 use the same `A` and `L_se` in all four mechanical RK stages,
while advancing intermediate fiber states. Line 216 updates tendon length after
the step. This is a source-ordering finding: it does not establish the accuracy
or stability of that discretization. A fully coupled continuous implementation
needs an independent comparison; it cannot be called an exact port of those
held inputs. Preserve a reproduction of original ordering alongside any repair.
The original calcium update also differs from the already qualified DNNE RK4
unit, so native-output equivalence must be assessed rather than assumed.

## Next implementation and qualification gates

1. Preserve the existing isometric unit and its complete reference evidence.
   Introduce an isolated dynamic slow-unit candidate with explicit physical units,
   geometry and valid parameter domain. Keep actual motor event identity/times.
2. Evaluate calcium, yield and mechanical state at consistent integration stages.
   Distinguish prescribed diagnostic kinematics from causal live physical input;
   future-frame interpolation is not an available live sensory measurement.
3. Require the optimal-length/zero-velocity equilibrium control to recover the
   old calculation. Add length and velocity challenges, independent adaptive
   references, event-boundary/refinement checks and source-ordering controls.
   Declare numerical gates before results; invalid states must remain failures.
4. Qualify coupled fiber/tendon dynamics and physical torque separately from
   intrinsic calcium accuracy. Do not reuse tibialis-anterior geometry for every
   joint or treat the legacy normalized-length mapping as measured anatomy.
5. Only after those reviews, integrate the canonical mechanism into the isolated
   Unity body, preserving source completeness, signed observations and physical
   ownership. Neuronal spinal recruitment, afferent feedback and recovery need
   their own evidence before any live promotion.

All tests and models remain in visible independent PowerShell windows, with no
live monitoring. Source review leaves live brain/Avatar/Unity/editor behavior,
old saves and the outstanding spindle window untouched.
