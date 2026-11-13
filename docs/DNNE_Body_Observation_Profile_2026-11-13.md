# Explicit availability of physical body observations

The native Unity source and shared body contract do not expose identical plants.
`SensoryFrames.ArticulationFrame` has no finger aperture, grip-force, hand-fatigue
or hand-slip members. Deserializing into the legacy shared record supplies its
eight defaults, including aperture 1. Those values cannot be treated as measured
hand state. Unity also emits zero placeholders for manipulator extension and two
recovery diagnostics; temperature, blood oxygen and support-plane offset are
declared reference values rather than dynamic plant measurements.

This was found by inspecting Avatar's observation producer/receiver, the native
encoder, shared contracts and migration audit while timing awaited its report.
No benchmark output was read or competing build run before that report.

`PhysicalBodyObservationProfile` snapshots and ordinally sorts an explicit body
encoding, unavailable member paths and reference member paths. Paths are
case-sensitive and relative to the body JSON object. Duplicates, invalid paths,
overlapping unavailable/reference declarations and missing profiles reject.
Caller-owned arrays cannot change the declaration after construction.

The profile participates in the combined layout fingerprint, sample identity
and batch/receipt validation. A changed availability declaration cannot alias
the earlier stream, even if all numeric placeholders are identical. Combined
transport is explicitly **version 2** with a new identity domain; old combined
v1 packets reject. The passing 67-case v1 capture stays frozen and valid for its
tested scope. Body-only protocol v1 and all old saves are unchanged.

Unity declares `unity-muscular-body-v1` with eleven unavailable members: the eight
finger/grip fields, manipulator extension, `rightingForceFraction` and
`dynamicStabilityAllowanceMeters`. It declares three reference members:
`coreTemperatureCelsius`, `bloodOxygenSaturationFraction` and
`articulation/supportPlaneOffsetMeters`. Actual hand load, all contact-site
observations and muscular length/velocity observations remain provided.

Values are preserved for exact audit; unavailable is not measured zero and
reference is not a dynamically simulated organ. A future neuronal consumer must
honor the profile and reject unsupported encodings. This declaration does not
add finger mechanics, oxygen/temperature regulation or sensory neurons, or prove
a source's physiological calibration merely because its profile is explicit.

Three new cases cover immutable declarations/stream separation, malformed and
contradictory profiles, old-packet rejection and Unity's availability mapping.
The focused rig now requires **70 cases** (31 combined, 16 body producer, 22 body
transport, one manifest). Test and Unity .NET adapter builds pass with zero
warnings/errors after timing completion. Python/PowerShell syntax and declared
case identities were checked statically. New tests have not run; they belong in
a fresh frozen PowerShell window with a verified D: copy.

Combined actual-Unity delivery remains the next physical integration study. It
must preserve body plus all fifteen contacts at each original endpoint, retain
this profile, verify paused/backpressured holds, and stop completeness at the
last whole endpoint after an injected missing contact. That physical probe is
not implemented or run yet. Later live editor/neuronal integration must expose
unavailable/reference inputs instead of presenting them as measured activity.

## Launch record

The 70-case package is `physical-observation-20261113-64ad44`, revision
`d3f148e5e281f3354b4c391f0f2b160768807abb`, launched as independent visible
PowerShell task `DNNE-Physical-Observations-50120d406ffc`. Its 1,523 initial files
plus manifest have a verified D: copy, rechecked before launch. The results await
the operator report; no live monitoring or pass is inferred from launch.

The operator later confirmed all windows complete. This capture has 69 passes
and the same stale protocol-v2 rejection fixture failure already diagnosed in
`162523`. Its full C:/D: review is recorded in
`DNNE_Muscle_And_Spindle_Results_2026-11-13.md`; the original FAILED status is
preserved. Corrected `7800c5` already passes all 74 cases, including this profile
scope, and actual native body/contact delivery. No further retry is required for
this older fixture failure, and it is no longer a pending report.
