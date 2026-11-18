# Connected sensory numerical review and precision study

The operator reported both `afferent-rest-20261118-62949f` and
`afferent-trajectories-20261118-cf2f38` finished with FAILED footers. Both
captures were then reviewed, including full C:/D: file hashes, Git archives,
test/probe binaries, numerical dependencies and managed runtime. No model,
test, new root or eigen solve was executed during saved-data review.

## Completed evidence

The trajectory capture has 2,964 verified files per copy and 181 passing
focused cases. Its original saved analysis reproduces exactly. All 21 native
captures and 21 references completed. All seven finest native comparisons and
all charge/ion accounting checks meet their original bounds. However:

| Case | Standard/tight reference error / limit | Finest native error / limit | Step refinement passes |
| --- | ---: | ---: | --- |
| quiet-one | 3.38045 | 0.18530 | No |
| bundle-one | 2.34178 | 0.17970 | No |
| loaded-one | 1.25466 | 0.04735 | Yes |
| reverse-one | 1.36715 | 0.08121 | No |
| cut-one | 0.96603 | 0.05469 | No |
| graded-two | 2.61225 | 0.22497 | No |
| bundle-eight | 3.01014 | 0.05218 | No |

The tighter Radau/BDF pair agrees within the reference limit in all seven
cases (maximum fraction 0.28524). The standard Radau calculation fails in six.
Many native maximum errors occur in accumulated membrane charge at the end of
the run; the loaded interface transient shows the expected approximately
fourfold reduction. This suggests nonlinear solve error can dominate time-step
error. It is a hypothesis to test, not an established repair or permission to
relax the refinement criterion.

The rest capture has 2,601 verified files per copy and 196 passing focused
cases. The original audit stopped on bit-exact eigen-residual reproduction.
NumPy 2.2.6 returns real eig witnesses as strided real views of complex storage.
NPZ retains values/dtype, but packs away that stride. This changes BLAS summation
order. Restoring the original layout exactly reproduces the recorded residuals
and condition numbers for all eighteen cases; no residual tolerance changed.
New references canonicalize C-contiguous witnesses before computing metrics
and declare that layout. Unknown layouts and changed witness values are rejected.

The corrected saved audit also verifies the five retained spectral controls,
all free-gate Jacobian checks, all eighteen independent roots and their recorded
locally-decaying estimates. Twelve native searches qualify. Six report
`LineSearchFailed` with native scaled residuals 1.01057–1.28483, above their
unchanged stopping limit of 1. Their independently checked residuals meet the
separate, looser root-comparison limit, but that does not convert the failed
native searches to success. Native root search remains unresolved; local linear
estimates do not establish physiological rest or nonlinear stability.

Reviews are preserved and D: verified:

- `afferent-trajectories-20261118-cf2f38-review`: one result file plus manifest.
- `afferent-rest-20261118-62949f-review`: initial saved-matrix diagnosis, one file.
- `afferent-rest-20261118-62949f-audit-review`: five files, including exact corrected
  audit sources and complete saved analysis. Original failed capture untouched.

## Next isolated experiment

`afferent_precision_gate.py` freezes the same seven fixtures, inputs, durations,
4/2/1 tick steps and all acceptance bounds. It changes only configured solve
tolerances:

- Native voltage/ion/gate absolute and relative nonlinear tolerances become
  1e-12 / 1e-14 / 1e-15 / 1e-14, each 1,000 times tighter.
- Reference Radau relative tolerances become 2e-11 and 2e-13; BDF becomes 2e-13.
  Their absolute tolerance factors tighten correspondingly by 100.
- Same biological kernels, clock, actual free gates, compartments, sources,
  nonlinear iteration limits, reference equations and conserved ledgers.

The window runs eight eigen-witness regression controls, the same 181 focused
cases, 21 native trajectories and 21 new references. It reuses only the exact
reviewed initial 60/90-digit derivative witnesses, whose inputs are unchanged;
their comparisons to every new native initial record are re-audited.

Every original failed capture and limit remains intact. An unsuccessful finer
solve is retained explicitly. No live selection, stationary initialization,
DNNE sensory publishing, afferent firing, physiological or Avatar qualification
is implied. Visible independent PowerShell only; no live monitoring.

Motor saved comparison `b224a6` remains pending and untouched. The voltage-event
observer described in `DNNE_Afferent_Event_Observer_Draft_2026-11-18.md` is held
at unrun source stage until the numerical issues are understood. Full goal active.
