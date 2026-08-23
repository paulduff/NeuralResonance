# Entry 136: Dynamic Balance, Axial Rotation, and Truthful Support

## Observation

The stopped motor-training run showed a physically inconsistent recovery state.
The avatar could be lying or heavily tilted while still reporting approximately
360 N through each foot. It also had no independently selectable trunk-yaw
effector, so it could turn the whole body but could not counter-rotate the
thorax over the pelvis. A fixed stability boundary treated the deliberate
centre-of-mass excursion needed for a step as equivalent to an uncontrolled
fall.

The source run report is:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\world-runs\world-run-98fbf93ccb884047ac255f5e1aafb0ad-0000085583-stopped-20260817T122131518Z.json`

## Independent neuronal axial rotation

The action topology now contains 34 lanes. Two reciprocal lanes were appended:

- 32: rotate trunk left
- 33: rotate trunk right

The lanes pass through the existing proposal, basal-ganglia, thalamic, motor,
and spinal populations. At the body boundary they recruit a physical antagonist
pair representing the external and contralateral internal obliques. Joint
mechanics enforce a bounded trunk-yaw range of +/-0.61 radians.

The accepted yaw angle returns through bilateral axial muscle-spindle
populations. It is carried through the avatar, world snapshot, articulated
collider rig, and editor. The World inspector now shows both descending trunk
yaw drive and measured trunk yaw.

## Truthful support

Ground load is now derived from physical posture and balance phase:

- standing and crouching remain foot-supported;
- sitting progressively transfers load to pelvis and knees;
- lying transfers all settled load away from the feet;
- falling transfers load according to measured physical tilt;
- fallen bodies carry all weight through body surfaces;
- unsupported grounded states accumulate instability and fall rather than
  being treated as stable.

This removes the false plantar feedback that could leave the neuronal recovery
circuit believing an impossible support base existed.

## Dynamic stability envelope

The balance plant now distinguishes static support from controlled dynamic
balance. Neuronal locomotor recruitment and commanded physical speed permit a
bounded allowance of at most 0.075 m beyond the static extrapolated-centre-of-
mass margin. This allowance exists only with measured foot support and an
upright body. Broad body support, absent support, passive motion, or excessive
excursion receives no allowance and therefore produces a truthful fall.

The physical body frame reports the dynamic allowance. Proprioceptive and
vestibular emergency righting populations use the effective margin after that
allowance, preventing normal single-support stepping from being mistaken for a
fall. A separate dynamic-stability-reserve population reports the available
reserve to the neuronal network.

The host does not choose a step, stumble response, or righting movement. The
network supplies locomotor, ankle, hip, axial, arm, and stand recruitment. The
plant supplies only anatomy, mass, contact, support, joint limits, and gravity.

## Fresh network boundary

Changing from 32 to 34 action lanes changes neuronal lane indexing. The default
synaptic persistence namespace is therefore now
`synapses-action34-axial-v1`. Older state remains inactive and is not loaded by
the next run. This intentionally starts a fresh network, as agreed while the
weights are still experimental.

## Verification

- Focused action, motor, nervous-system, articulation, balance, transducer, and
  editor tests pass: 148 tests.
- The full DNNE suite passes: 677 tests.
- Release build completes with zero warnings and zero errors.
- JavaScript syntax validation passes.
- A live predators-suspended observation remains the behavioral acceptance
  check.

## Next observation

The next fresh run should verify bilateral axial activity, non-zero trunk yaw,
zero foot load while settled lying, dynamic stepping without constant righting
recruitment, and a real fall whenever support and recovery recruitment are
insufficient.
