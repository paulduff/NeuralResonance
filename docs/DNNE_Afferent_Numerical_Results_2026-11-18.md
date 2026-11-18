# Sensory nerve derivative results

The operator reported `afferent-numerics-20261118-4ab20e` complete. Its saved
evidence passes review: 2,473 files per C:/D: copy, exact frozen Git source,
built binaries, managed/numerical dependencies and all 166 focused cases.
The frozen revision is `3f49cb952d90b124ab84cb7a727a2a7d4f196593`. Saved analysis
reproduces exactly without executing an equation, reference solver or test.
The new `artifacts/afferent-numerics-20261118-4ab20e-review` also has a verified
D: copy.

All 1,686 native cases agree with the independent 60/90-digit references under
the original bounds. All separate current/power accounting checks pass.

| Group | Passed | Largest fraction of the candidate comparison allowance |
| --- | ---: | ---: |
| Membrane derivatives | 1,536 / 1,536 | 0.00240714 |
| Analytic voltage clamps | 84 / 84 | 0.00042321 |
| Cable derivatives | 26 / 26 | 0.00011446 |
| Joint sensory/cable derivatives | 40 / 40 | 0.01588438 |

A fraction of one is the original acceptance boundary. These figures concern
the sampled derivative/clamp comparisons; they are not errors measured over a
free neuronal trajectory. Physiology, axonal propagation and live DNNE input
remain unqualified. No parameter, bound, live selection or old save changed.

This opens the existing joint-dynamics component gate: 181 cases including the
fifteen new clock, passive analytic evolution, coupling, conservation, severed
contact and failure-preservation checks. The runner remains independent and
visible, with frozen source, initial/final D: verification and no live monitoring.

The subsequent fast-transient comparison is now fully authored; see
`DNNE_Afferent_Fast_Transient_2026-11-18.md`. Its preparation requires a reported,
reviewed and D:-verified passing dynamics component result first. Longer joint
trajectories, a loaded resting state, emitted sensory events and delivery to
identified DNNE contacts remain necessary before physical balance/recovery.

Motor refinement `3d8835` has not completed its final preservation. At the
operator's request to investigate an apparent stall, one bounded process check
confirmed active final-backup I/O. Its calculation finished but the analysis
stopped at the event-identity comparison. Full outcome review awaits the final
verified backup/footer; no simulation restart or frozen-package edit occurred.
