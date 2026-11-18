# Sensory rest and nerve component results

The operator reported nerve components `d3fbc9` and active sensory rest
`760702` complete, and explicitly confirmed motor refinement `3d8835` is still
running. Only the two completed packages were inspected. The motor package
remains untouched after launch.

## Nerve component review

`artifacts/afferent-components-review-20261118/review.json` records verification
of all 2,435 files on both C: and D:, exact frozen Git source, test/probe binaries,
managed runtime, source timing data and all 166 focused cases. Saved component
analysis reproduced exactly without a model or test rerun. The new one-file
review also has a verified D: copy.

This passes the component prerequisite for the independent derivative study in
`DNNE_Afferent_Numerics_2026-11-18.md`. It does not yet qualify coupled numerical
accuracy, propagated neuronal firing or a live afferent route into DNNE.

## Active sensory resting state

`artifacts/cleft-rest-review-20261118` records all 2,525 C:/D: files, frozen
revision `e4e3cf2243d108a49a49162b3ef5173605989c25`, test/probe binaries,
unchanged managed/numerical dependencies, 132 focused cases and exact
reproduction of the saved stationary analysis. Its two files have a verified
D: copy. Review used saved rates, roots and matrices only: no model evaluation,
root search, time integration, test or eigensolve reran.

All eighteen native roots independently satisfy the original physical residual
criteria. All eighteen independent searches also meet those criteria. The two
methods agree within the declared state distances. Three distinct starting
guesses reach the same sampled solution for each of six geometries; this does
not prove mathematical uniqueness.

All eighteen recorded full-state spectra classify as `locally-decaying estimate`.
The largest real eigenvalue ranges from -0.0033874494 to -0.0007457839 per ms;
the empirical condition-weighted uncertainty radius ranges from 2.53e-11 to
9.92e-9 per ms. Thus the recorded local classifications are separated from zero
under the declared uncertainty estimate. This concerns sufficiently small
perturbations in the reduced equations; it is not recovery after a finite
disturbance or physiological calibration.

Review independently reproduced the saved Richardson matrices, their
successive-difference norms, exact free-gate diagonal checks and classification
arithmetic. Recorded eigenvalues also satisfy the saved matrix trace check.
Eigenvectors were not retained by the original study, so their residual and
condition number remain frozen-study evidence rather than independently
rechecked vector witnesses. Preserve that limitation in any stronger stability
claim; future full-circuit spectral captures should retain the vectors.

For the single-patch geometry, relative to the held bath, the solution has
hair-cell voltage -74.3376 mV, calyx voltage -62.5145 mV, cleft voltage +1.9918 mV,
cleft potassium 7.7168 mM and sodium 130.3252 mM. Larger synthetic geometries
produce spatial gradients. The eight-patch uniform case reaches cleft voltages
up to +12.0370 mV and sodium down to 74.1893 mM. Those are model outputs for
uncalibrated geometry, not validated anatomical concentrations or a reason to
clip the state.

The tested terminal is explicitly sealed with zero axonal current. The new
active nerve changes that boundary condition, so its loaded resting state must
be established separately. No live initialization, Avatar parameters or old
saves were changed by these results.

## Next execution

Freeze, initially back up and visibly launch the already-authored 1,686-case
nerve derivative comparison. Motor refinement continues independently; there
is no new timing benchmark competing with it. Review the new derivative result
only after the operator reports completion. Joint dynamics components, full
trajectory accuracy, loaded resting state, actual sensory events and DNNE
delivery remain subsequent requirements before balance/recovery qualification.
