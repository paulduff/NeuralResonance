# Sensory nerve derivative comparison

Status: authored and statically checked; **ready for preparation and launch**.
The operator reported `afferent-components-20261118-d3fbc9` complete. Its full
saved-data review passed: 2,435 files per C:/D: copy and all 166 focused cases;
the new review also has a verified D: copy. See
`DNNE_Sensory_Rest_And_Nerve_Results_2026-11-18.md`. The full DNNE, Avatar and
World goal remains active.

The new nerve membrane and cable must calculate the intended electrical
equations correctly before a coupled trajectory is interpreted as sensory
firing. This comparison preserves the three new Avatar kernels, all earlier
sensory equations and the declared May14 source choices from
`DNNE_Vestibular_Afferent_2026-11-18.md`. It changes no live selection or editor
observation and does not initialize a living brain or body.

## Prescribed coverage

| Group | Cases | Coverage |
| --- | ---: | --- |
| Membrane derivatives | 1,536 | Eight channel/ion profiles, 64 voltages, actual zero/one/mixed gate states |
| Held-voltage analytic gates | 84 | Seven voltages, four durations, three initial gate states |
| Cable derivatives | 26 | Three subdivisions, two bath potentials, four voltage/terminal patterns, two analytic graphs |
| Joint calyx/cable derivatives | 40 | One/two/eight cleft patches, uniform/graded state, two subdivisions, both current directions, severed and multiple attachments |

There are 1,686 native cases and 3,372 independent reference evaluations at
60/90 decimal digits. The existing 166 focused component cases must also pass
in the new frozen package. These are prescribed derivative and held-voltage
checks, not a free membrane time evolution or a spatial convergence claim.

`tools/NativeMotorBridgeProbe/AfferentNumericalStudy.cs` saves each typed input,
kernel fingerprint, complete evaluation and the actual timing tables. Output
is streamed to four new JSONL files. Failed prefixes remain on disk.

`tools/afferent_numerical_reference.py` separately implements the six channel
rates, Nernst potentials, actual-gate currents and analytic clamp relaxation.
It reads the audited source table values, not candidate rates or currents.
Cable currents use a dense conductance Laplacian with explicit boundaries.
The joint reference feeds the opposite terminal current into the unchanged
independent sensory reference and full capacitance-matrix solve. Binary64 input
constants, including the explicitly declared Math.PI value, are converted
exactly into Decimal; subsequent reference arithmetic uses the chosen precision.

Both complete precision witnesses are retained for every case. A reference
exception records the input and traceback; remaining cases continue, and an
incomplete reference cannot pass the final comparison.

## Prospective criteria

For ordinary values the bound is `absolute + relative * abs(reference)`:

| Quantity | Absolute | Relative |
| --- | ---: | ---: |
| Reference 60/90-digit agreement | 1e-40 | 1e-40 |
| Membrane evaluation | 2e-10 | 2e-12 |
| Analytic clamp gates | 2e-13 | 2e-12 |
| Cable evaluation/transfers | 2e-8 | 2e-12 |
| Earlier sensory evaluation, unchanged | 2e-10 | 2e-12 |

Cable charge/power residuals and joint interface residuals are assessed
separately because they subtract large opposing flows. They are not omitted:
both the reported residual and a new sum of saved components must satisfy
`absolute + 256 * binary64_epsilon * sum(abs(terms))`, with absolute floors
1e-10 pA and 1e-8 fW. Their mutual discrepancy must satisfy the same declared
rule. The analysis retains each residual, scale, bound and result.

Accounting also checks individual node incidence, `C*dV/dt`, membrane
area/current multiplication, total current and power sums, nonnegative
dissipation, current direction, sealed terminal silence and opposite signed
calyx/cable transfers. Each sensory patch's saved axon port must equal its
attached cable transfers. All values in the old sensory result retain the
original sensory comparison bound, including its residual fields.

Two explicit passive graphs provide independent analytic controls: a 3 nS
edge between 0 and 10 mV carries -30 pA and dissipates 300 fW. Adding a 2 nS
boundary at 20 mV supplies 40 pA, adds 800 fW dissipation and changes the first
node charge rate from 30 to 70 pA. These controls execute only in the visible
study. Full schema/count/input identity, source tables and stable fingerprints
are also required. Analysis is saved before a failed numerical verdict raises.

## Execution and preservation

First the operator must report `d3fbc9` complete. The new
`tools/review-afferent-components-20261118.py --operator-reported-complete`
then audits both full C:/D: copies, frozen source/binaries/runtime, all 166 TRX
cases and exact saved component analysis. It executes no model or test. Its
fresh review folder must be backed up and verified before preparation.

`tools/afferent_numerical_gate.py prepare REVIEW_FOLDER` requires that passing
review, a clean pushed revision, unchanged candidate/earlier source and the
reviewed numerical environment from completed sensory refinement `884934`.
It freezes a new Git archive, source evidence, runtime provenance, protocol and
bounds. Initial D: backup must verify before
`tools/start-afferent-numerics-window.ps1 -PackageDirectory NEW_PACKAGE` can
start an independent visible PowerShell window. No live monitoring; review
only after the operator reports its final outcome. Old packages are untouched.

Authoring validation: five new Python files parsed, fixture identities/counts
and 166-case source inventory checked, PowerShell launcher parsed, and the
probe built with zero warnings/errors at
`D:/DNNE-build-work/afferent-numerical-authoring-20261118`. The general Python
runtime lacks SciPy; static imports used the existing numerical environment
without changing it. No equations, controls, references or tests ran inline.

Even a passing result establishes only these sampled derivative and clamp
calculations. Loaded stationary state, free joint trajectories, propagated
neuronal events, physiological calibration, live DNNE afferent delivery and
Avatar balance/recovery remain required. A sealed calyx stationary solution
cannot silently initialize the newly attached active nerve.
