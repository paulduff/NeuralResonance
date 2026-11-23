# Evening checkpoint — 23 November 2026

Paul asked to stop and note up for tomorrow while the next native study was
being prepared. **No experiment is pending. No new study was launched.**
Resume the goal only when requested; keep every experiment in an independent
visible PowerShell window, without live monitoring. Paul reports completion.

## Completed outcomes

Both studies below were explicitly reported complete, then reviewed against
their entire original and D: backup inventories, frozen Git sources, execution
records and saved analyses. Later duplicate population messages add no new run.

- `D:/DNNE-build-work/native-population-reanalysis-20261123-c6dcd3`:
  PASS. 2,356 files per copy verified. Eight receipt controls and all eight
  original native conditions reconcile. Two 147-unit muscles, 94,080 packets,
  336 body endpoints, 1,008 head receipts, 2,560 point forces and 320 exact
  step/inspect receipt pairs are accounted for. Maximum force discrepancy is
  2.842170943040401e-14 N. Cutting prescribed arrivals reproduces quiet exactly;
  duplicate arrivals reproduce the original driven outcome exactly. Maximum
  joint response across the body relative to quiet is 0.029783129692077637 rad
  (about 1.7 degrees), with 0.029783546924591064 rad in the finer muscle case.
  These are prescribed-input results, not firing-brain control or a body-step
  convergence qualification. Original failed `b3cf65` and both protected copies
  remain exact; no model or player was rerun by this reanalysis.
- `D:/DNNE-build-work/routed-muscle-path-20261123-61820b`: PASS. 2,947 files
  per copy verified. All 169 managed cases pass, including 18 new complete
  routed-path controls. The assembly retains ordered attachments, cylinder
  reactions, virtual patellar load transfer and force/work accounting. Native
  constrained force application and human anatomical fit remain unqualified.

Reviews and their verified D: copies:

- `artifacts/native-population-reanalysis-20261123-c6dcd3-review` — five files;
  `D:/DNNE-desktop-runs/UnityWorld/native-population-reanalysis-20261123-c6dcd3-review`.
- `artifacts/routed-muscle-path-20261123-61820b-review` — four files;
  `D:/DNNE-desktop-runs/UnityWorld/routed-muscle-path-20261123-61820b-review`.

## Authored candidate, not yet executed

`NativeRoutedMuscleBatch.cs` prepares all native point forces and constraint
moments before queuing any load. It retains original double values, actual
float-boundary values, receiving body identities, original state/accumulators,
mechanical identity and body power. A batch has one main-thread application
attempt; changed state or a repeated attempt is rejected. Existing body and
biological equations are unchanged. This is only selected by the new isolated
`--native-routed-case` mode, with an explicitly paused fresh body.

`RoutedMuscleNativeQualification.cs` authors seven full-Avatar cases:
quiet, left, right, both, both-cut, invalid-last, both-replay. Each has eight
physical steps and nine endpoints, using the reviewed no-joint-friction
diagnostic setting and 0.005f-second steps. Two analytic knee routes use the
existing knee profile's mechanical scale, with prescribed 40 N pulses.
The expected inventory is 56 physical steps, 490 point forces and 196 paired
constraint moments. The late-invalid case prepares nonzero earlier loads before
an invalid final recipient, so the absence of partial application is meaningful.

This is **analytic test geometry**, not a source-mapped human quadriceps. The
massless patella reduction is not an anatomical mass/inertia qualification.
Source OpenSim hybrid wrap selection, source-to-Avatar anatomy, range mapping,
physiological force/recruitment and meaningful standing remain separate work.
Existing live modes and old wire owner are unchanged.

`tools/native_routed_study.py` and `tools/start-native-routed-window.ps1` are
authored, but final harness review and execution are outstanding. They propose:

1. Freeze a clean pushed revision and preserve both new reviews and the completed
   `998900` original quiet capture; preserve attributed FBX/texture importer inputs.
2. Reuse the verified 169 managed cases because their source is unchanged; run
   the existing eight asset-input controls, then build a separate Unity player.
3. Run the seven fresh isolated cases and preserve all native observations.
4. Analyse every load increment on all 15 bodies, accumulated forces/moments,
   reset after each step, binary conversions, body work, analytic path length,
   length rate and generalized knee load. Compare original quiet, cut/quiet,
   invalid/quiet and replay/driven endpoints exactly (excluding cross-process
   Unity instance identifiers only). Motion magnitude is diagnostic.

Prospective accumulator bounds retain the previous study's 2e-4 N,
2e-5 N m and 2e-5 relative allowance. New native mapping allowances are 2e-6 m,
2e-6 m/s, 1e-4 N m generalized load and 1e-4 W power. These are declared before
execution and must not be widened to turn a failure into a pass.

## Checks and tomorrow's first actions

All Unity runtime C# sources compile in a source-only author check against the
installed 6000.3.19f1 engine references with language version 9: zero errors,
seven existing JSON-deserialization field warnings. This does not replace the
actual Unity player build. Initial use of the newer compiler language default
selected a different existing `Reverse` overload; the C# 9 check leaves that
source unchanged. Both compile logs are retained under
`D:/DNNE-build-work/native-routed-author-compile-20261123`; final log is
`build-final.log`. Python AST and PowerShell parser checks pass. No models,
tests, native builds or native players were executed during close-out.

Before launching tomorrow, review the analyser's expected schemas, analytic
geometry/power signs, original quiet comparison and exact native case counts.
Review the launcher and frozen-input declaration (baseline `caa07de`, exactly
five Unity file changes), then prepare with the existing isolated Python:

`D:/DNNE-build-work/cylinder-reference-env-20261123/Scripts/python.exe -I -B tools/native_routed_study.py prepare`

After successful data-only preflights and verified initial D: backup, launch
`tools/start-native-routed-window.ps1` from the frozen source with its package
directory. Record the launch and stop inspecting the run until Paul reports
its final footer. Do not infer that an unlaunched test is pending or restart any
of today's completed studies.

The overall DNNE–Avatar–World goal remains incomplete. The next result can
qualify native constrained load delivery; it cannot demonstrate neuronal motor
authority, physiological calibration, balance or standing.
