# Published canal trajectory comparison — 18 November 2026

The completed `coupled-canal-20261118-0fe70b` review qualifies 76 component
checks, including 23 canal controls. The next comparison asks whether the
unchanged six-volume mechanical candidate accurately integrates the coupled
published specimen. No skull transformation, bundle deformation map, neuronal
firing, live Avatar selection or physiological calibration is inferred.

## Fixed source and independent calculation

The model remains the Ifediba 2007 right labyrinth-1 specimen at printed
precision, documented in `DNNE_Coupled_Canal_Mechanics_2026-11-18.md` and its
source audit. The two production mechanical files, source catalog, SimAvatar
project, shared finite-value helper and four component test classes must match
the completed component capture. Its 76 cases are reused, not rerun. No old
capture, failed result, model default, learning equation or saved state changes.

The native calculation uses the existing two-stage Radau IIA integrator.
`NativeMotorBridgeProbe --coupled-canal-trajectories INPUT OUTPUT` is an isolated
entry point. Each accepted step retains all twelve state components, both full
stages, physical and numerical dissipation, work, energy and backward error.
Clocks use integer ticks at 2^26 ticks per second, with exact dyadic timesteps.
Failed prefixes are retained and cannot overwrite an existing output directory.

The independent reference constructs the augmented first-order linear system
directly from the catalog's original CGS decimal strings and exact SI conversion,
using a separate mass-matrix inverse. Python Decimal at 60 and 90 digits computes
the matrix exponential by Taylor scaling/squaring. Its stated remainder bound
is local to the reduced Taylor series, not a certified bound on all accumulated
roundoff. Precision agreement is therefore checked on every operator and every
reference point. A third calculation uses SciPy `expm` with a separate binary64
mass solve. None uses native stages or trajectories as integration inputs.

Six analytic reference controls precede the model: zero, diagonal, nilpotent,
damped Jordan, stiff decay and constant forcing. Their 60/90-digit answers must
match closed-form values within 1e-45. No dependency is installed: standard-library
Decimal and the existing pinned NumPy 2.2.6/SciPy 1.16.3 environment are used.
Decimal implementation files, versions and managed runtime are also retained.

## Conditions and resolution

Fifteen conditions combine three time regimes with quiet, HC/AC/PC prime-axis
drive, and undriven free decay. Axis drives use four held intervals with
amplitudes +1, 0, -1, 0 rad/s² in the declared source-prime coordinates. Free
decay has explicitly prescribed small volume/flow perturbations. These are
diagnostic inputs, not a claim about normal head motion or human thresholds.

Each condition runs at h, h/2 and h/4: 45 native captures and 1,020,600 retained
native steps. The independent calculations retain every finest-grid point.
Inputs switch only at exact interval boundaries. The coarse schedule for each
held interval is:

| Regime | Total case duration | Segments per held interval: duration ticks / step ticks |
| --- | ---: | --- |
| Fast | 30.517578125 microseconds | 512 / 4 |
| Middle | 0.125 seconds | 2,048 / 4; (2^21 − 2,048) / 2,048 |
| Slow | 128 seconds | 2,048 / 4; (2^23 − 2,048) / 2,048; (32×2^26 − 2^23) / 2^21 |

Every input change starts with resolved fast steps. Larger steps apply only
after the declared transient intervals. Stability alone must not hide an
unresolved fast mechanical response. All intermediate endpoints are compared;
there is no endpoint thinning or selection of successful phases.

## Declared acceptance limits

For every state component the normalized difference is
`abs(candidate-reference)/(absolute + relative*max(abs(candidate),abs(reference)))`.
Native relative tolerance is 1e-5. Volume/flow absolute tolerances are respectively
1e-25 m³ / 1e-21 m³/s for fast, 1e-18 / 1e-17 for middle, and 1e-16 / 1e-17 for
slow. Independent SciPy-versus-Decimal bounds are 100 times tighter in both
absolute and relative terms. Decimal 60-versus-90 agreement uses 1e-40 absolute
and relative for every operator entry and trajectory component.

Each finest native trace must pass its absolute/relative bound. Each refinement
must reduce the maximum normalized error to at most 0.3 of the previous value,
plus a 1e-5 normalized floor for roundoff. These are diagnostic numerical limits,
not physiological uncertainty estimates. A failure is retained with these limits.

Energy and stage accounting are independently reconstructed from saved states,
stages and source matrices. The energy allowance is 1e-32 J plus 5e-10 times
the sum of the magnitudes of stored energy, work and both losses. Undriven
intervals cannot gain energy beyond that allowance. Reported solve backward
error stays at the existing 1e-12 bound; reconstructed physical stage equations
must have componentwise backward error at most 5e-10. Physical viscosity and
Radau's numerical damping remain separate quantities.

## Execution and remaining work

`canal_trajectory_gate.py` freezes clean pushed source with the reviewed
component evidence. `start-canal-trajectory-window.ps1` runs all controls,
native mechanics, references and comparison in a separate visible PowerShell
window, without live monitoring. The authoring probe build passed with zero
warnings/errors; no model, analytic control or test executed during authoring.

Work uses `D:/DNNE-build-work` and a verified copy under
`D:/DNNE-desktop-runs/UnityWorld`; these new copies share D:, while old C:/D:
evidence remains untouched. Preparation requires 20 GiB free and later model
phases require a 10 GiB reserve. Complete original binary64 native records,
Decimal textual traces, binary64 reference arrays and operators are retained.

After review, source-to-skull orientation, anatomical placement, local bundle
mechanics and physical-clock ownership still require explicit integration.
Sensory resting states, sustained neuronal afferent firing, physiological
calibration and connected Avatar balance/recovery remain separate open work.
The motor branch separately needs new-capture archive staging and the full
refined condition matrix; archive accounting alone does not qualify that path.


Initial trajectory package `canal-trajectories-20261118-e01e93` stopped at static
preflight before launch: the original capture's analysis SHA-256 was mistakenly
compared with the reformatted review JSON. Their decoded contents agree; their
bytes differ. No model/test executed. The unlaunched package and explicit failure
receipt are preserved. The freezer now separately copies/hash-verifies the
original analysis and compares its decoded contents to the reviewed reconstruction.
A new frozen package is required; no frozen source or acceptance bound is edited.
