# Finer Golgi numerical comparison: original criteria passed

The operator reported `golgi-sdirk3-fine-20261113-ca813f` complete. Its fourteen
new third-order traces meet the original numerical criteria across all eight
protocols. All 43 focused tests, nine environment-transition controls, retained
refinement checks and nonlinear sensitivity checks passed. The 38 older cases
and their failed absolute-accuracy result remain preserved. No acceptance limit,
cell equation, input or biological parameter was changed to obtain this result.

The new timestep is 0.0009765625 ms (approximately 0.977 microseconds). This is
half the preceding 0.001953125 ms step. The six active protocols each have ordinary
and eightfold tighter nonlinear tolerances, and the two passive protocols have
one case each. Both active tolerances pass comparison with both retained BDF and
Radau references. Recorded somatic crossing counts match; no phase alignment or
spike removal was used. Full sampled state, charge/calcium accounting, SK mass,
domain checks and the original 0.25 ms output grid remain part of the assessment.

| Quantity | Worst active error across both references/tolerances | Original limit |
| --- | ---: | ---: |
| Voltage | 0.032027303 mV | 0.05 mV |
| Channel gate | 0.000335676 | 0.0005 |
| SK occupancy | 0.000009808 | 0.0005 |
| Calcium | 2.3601342e-7 mM | 5e-7 mM |
| Corresponding crossing time | 0.000107273 ms | 0.01 ms |

This establishes the tested solver's numerical agreement at this resolution for
these conditions. It does not establish matched physiological calibration,
active axonal propagation, connected granular-layer function, real-time cost or
Avatar recovery. The small required timestep is an engineering constraint to
measure, not permission to loosen the biological/reference criteria. No live
host, solver default, editor qualification label or saved brain is changed.

## Verified evidence

The frozen source revision is `ad68cc7a3c5e1f854d1c0bfe489602a9caad37f4`.
`tools/review-golgi-and-contacts-20261113.py` verified all 3,638 files in each
C:/D: copy (2,475,593,958 bytes per copy), all 1,476 frozen Git files and executed
test/probe binaries. All 43 test identities, including theory arguments, match
their frozen source. The manifest SHA-256 is
`A85350D59AC2762C1409A90D9DC121798754BFD2895264B202E6523ABD6FA137`.

The frozen fine analyzer reproduced `fine-analysis.json` exactly from saved
arrays, with new reference ODE solves explicitly prohibited. The reviewed
three-binary Python transition remains declared; exact old-analysis reproduction
does not claim the old interpreter was recreated. No test or model was rerun in
this review. The separate `artifacts/golgi-contact-review-20261113` capture has
three files plus manifest verified on D:.

The body/contact retry also passed its review (67 cases). Both windows are now
resolved. The prepared matched CUDA host timing study can proceed exclusively;
the spindle reference and new physical integration experiments must wait until
that timing window is reported complete and reviewed. Subsequent Golgi work
should evaluate reference physiology and connected behavior explicitly, preserving
this numerical baseline and source identity.
