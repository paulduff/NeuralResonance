# Golgi preflight failure and explicit runtime transition

The operator reported the first finer Golgi window failed. The failure was in
saved-analysis preflight, after all 43 focused tests passed and before any of
the 14 new simulations. The original package remains unchanged with FAILED
status: `golgi-sdirk3-fine-20261109-4b5c0f`, revision
`69bc40f1593ec5cab71883be705cdbe2a8cfbec4`.

The completed evidence review is
`artifacts/golgi-fine-environment-review-20261113/review.json`. It verifies all
3,481 files (2,014,093,973 bytes per copy) on C: and D:, frozen inputs and executed
test binaries. Capture manifest SHA256:
`22D4AFC35DAC8BEDA279E5BD51F4372553F989B5BC329038A2CA2120D9A0295B`.
No new study directory, successful preflight report or candidate executable
capture exists. No model or ODE was executed during review.

## Identified mismatch

Both environment records contain 2,436 dependency files. Version strings are
unchanged: Python 3.12.14, NumPy 2.2.6, SciPy 1.16.3. Exactly three file hashes
changed in the base Python runtime under the app-managed cache:

* `python.exe`
* `python3.dll`
* `python312.dll`

All other 2,433 recorded files, including NumPy/SciPy and their native libraries,
have unchanged identities and hashes. Full before/after hashes are in the review;
`current-environment.json` preserves the new inventory. The virtual environment's
`pyvenv.cfg` points to that base runtime. This explains why retaining the virtual
environment directory alone did not freeze the Python interpreter. The evidence
does not identify which updater replaced the binaries.

The strict environment check correctly refused to call the current runtime the
same one used by the older study. Matching version strings are insufficient.

## Retry design

The retry explicitly declares the reviewed interpreter transition. Preparation
requires the verified D: review, re-verifies the failed capture, and copies the
review and full expected environment into a new immutable package. It accepts
only these three colocated runtime-file changes, unchanged version metadata,
unchanged paths/file set and exact reviewed old/new hashes. Any additional library
change or later update fails the gate.

The retained analyzer has exactly two source changes: an optional explicit
`replay_environment` argument and selection of that expected environment for its
final check. With no argument its original strict behavior remains. Preparation
and preflight compare its entire source with the preserved analyzer after these
two declared substitutions; numerical equations, comparisons, bounds and all
other analyzer code must remain identical.

Preflight runs nine acceptance/rejection controls for the transition gate, verifies
the actual environment against the frozen declaration, and then reproduces the
entire 38-case saved analysis exactly, including its original failures. Only
that exact match permits the 14 finer cases. Reference solving remains prohibited.
Post-analysis requires the same declared environment again. The report labels
this as saved-analysis compatibility with the new interpreter, never reproduction
of the old interpreter binaries. All original numerical limits remain unchanged.

This is a candidate repair to experiment reproducibility, not a Golgi physiology
change. Static syntax/source checks precede launch; the nine transition controls,
full saved-analysis replay and new simulations run only in the independent visible
PowerShell window. No result for the corrected preflight is claimed before the
operator reports that window complete. Runtime capture beyond the existing
interpreter/NumPy/SciPy inventory remains a future hardening task.

## Launched retry

The source repair and review were committed and pushed as
`ad68cc7a3c5e1f854d1c0bfe489602a9caad37f4`. Python syntax and PowerShell parsing
passed, and static comparison verifies the retained analyzer has only the two
declared environment-check edits. The two-file environment review has a verified
D: copy. Preparation reverified both original captures and their source/binary
evidence before freezing `golgi-sdirk3-fine-20261113-ca813f` at that revision.

All 3,094 initial package files plus manifest were verified on D:, then rechecked
by the launcher. Independent visible PowerShell task
`DNNE-Golgi-Sdirk3-Fine-2db991578748` has started. It is the only pending experiment.
Its nine transition controls, exact old-analysis replay, 43 focused tests and
14 finer cases are not yet reviewed. Await the operator's completion report with
no live monitoring. Host CUDA timing remains held until this window finishes.
