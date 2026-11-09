# Complete the interrupted Golgi reference comparison

The reported `motor-axon-20261106-42c421` capture stopped after eleven of twelve
adaptive solves. Its terminal observation, verified original inputs and sealed
partial evidence are recorded in `DNNE_Compute_And_Cell_Results_2026-11-09.md`.
The original RUNNING status is stale and remains preserved. The missing solve is
the no-SK control using Radau. It is an isolated channel-ablation experiment,
not a proposed removal of SK from the brain.

The continuation creates a fresh package from clean, pushed source. It checks
the sealed evidence against both copies of the review backup, then copies the
78 original input files, eleven completed NPZ arrays and their metadata. It also
copies the original three Python reference/analysis modules, verifying their
hashes against the old frozen source manifest. The equations, geometry, initial
states, channel parameters, six-second challenges and comparison rules are
unchanged. The old capture and both existing backups are read-only inputs.

## Stages in the visible PowerShell window

1. Verify frozen files, exact protocol/tolerance settings and the eleven unique
   cached solves. Validate their full arrays, gates, calcium, SK mass and time
   grids. Recompute all ten previously completed reference contrasts and thirty
   recorded candidate contrasts; every saved metric must reproduce exactly.
2. Execute only `no-sk / radau-strict` using the original reference module:
   rtol `1e-10`, voltage/gate/calcium absolute tolerances `1e-11 mV`, `1e-13`,
   `1e-12 micromolar`, and maximum internal step `0.125 ms`. Store the full
   24,001-by-159 state array, crossing times, solver statistics and clamp error.
3. In a separate read-only calculation process, reload all twelve reference
   arrays and recompute twelve reference contrasts and 36 candidate contrasts.
   Retain the complete report even if its original reference gate fails.

The original reference gate still requires equal crossing counts, ordinal timing
differences strictly below `0.01 ms` and soma voltage differences strictly below
`0.05 mV`. Full cable, gate, SK and calcium differences remain in the report.
Candidate traces are compared without spike alignment or rate averaging. Their
known first-order splitting/timing errors are not repaired by completing this
reference. No candidate, physiology or Avatar recovery pass is inferred.

The environment must match the original NumPy `2.2.6` and SciPy `1.16.3` versions.
The continuation records hashes of their installed distribution files, Python
executables and core Python DLLs, and rejects changes between stages. It retains
the pre-existing environment rather than installing or upgrading dependencies.
Single-thread BLAS settings match the original reference window. This environment
record strengthens continuation reproducibility; the old capture did not contain
an equivalent dependency-file inventory, so it does not prove the old dependency
binaries were identical. Exact reproduction of the recorded metrics is required.

New output uses exclusive creation. An interruption does not trigger an automatic
restart, overwrite a partial trace or rerun the eleven completed solves. Review
any failed/partial continuation separately before preparing another package.
The PowerShell launcher verifies the initial C:/D: package, writes separate stage
logs/status, and saves a final verified D: backup. No live monitoring is used.

## Development and scope

`prepare-golgi-resume.py`, `golgi-reference-resume.py` and
`start-golgi-resume-window.ps1` implement packaging, staged execution and the
independent window. Static Python/PowerShell syntax checks precede packaging;
runtime preflight and numerical results remain pending until the operator reports
completion. No unit-suite or build result is implied by this Python-only run.

This work can run separately from the pending olive CPU-batch study: neither is
a timing benchmark, and both use isolated frozen inputs. No running study is
monitored or changed. Production DNNE, Avatar, Unity, editor controls and old
brain saves are unchanged. The next Golgi implementation decision depends on the
complete reference evidence, not on whether the process reaches its footer.
