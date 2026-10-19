# Learning and structure mechanism audit

This executable characterizes the existing runtime. A completed capture is **not**
a biological qualification. It never contacts running brain services or reads the
saved brain weights. Every engine gets a new, isolated persistence directory.
Diagnostic stimulation and seeded eligibility are labelled in the outputs.

From the repository root, build outside production service output:

```powershell
dotnet build tools/LearningStructureAudit/LearningStructureAudit.csproj --configuration Release --artifacts-path artifacts/learning-audit-build --verbosity minimal -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
dotnet artifacts/learning-audit-build/bin/LearningStructureAudit/release/NeuralResonanceEngine.DNNE.Tests.dll (Get-Location).Path artifacts/learning-audit-first
dotnet artifacts/learning-audit-build/bin/LearningStructureAudit/release/NeuralResonanceEngine.DNNE.Tests.dll (Get-Location).Path artifacts/learning-audit-second
```

Both output directories must be new. Separate process starts are necessary to
compare `projectionFingerprint` in the two `connection-identity.json` files. The
assembly name uses the runtime's existing test-visibility grant; this is a console
diagnostic, not another test project. Reflection intentionally exercises the
current private coordinator/runtime methods without replacing their behaviour.

Outputs include source/profile hashes, structure inventory, routed contact
identity, actual post-spike handling, delayed modulation, sender-only learning,
integration-cadence effects and a conditional restored-clock budget check.
`completion.json` explicitly records that biological equivalence and physical
recovery have not been qualified. Review individual observations and scope fields;
exit zero means only that captures completed.

For the repaired runtime, verify two captures against the current source:

```powershell
python tools/LearningStructureAudit/verify_repair.py . artifacts/learning-audit-first artifacts/learning-audit-second artifacts/learning-repair-verification.json
```

The output file must be new. Assertions cover sampled mapping stability, actual
post updates, delayed modulation, unchanged production sender release, physical-
time trace agreement and warm-start budget rebasing. The verdict explicitly leaves
physiological equivalence and physical recovery unqualified. Sender probes invoke
the production outbound-spike builder with diagnostic activity/strength; there is
no test-only replacement learning function in the runtime.

Implementation and saved-state handling are documented in the
[foundation repair report](../../docs/DNNE_Biological_Foundation_Repair_2026-10-19.md).

The dated findings and repair acceptance criteria are in
[`DNNE_Learning_And_Structure_Review_2026-10-19.md`](../../docs/DNNE_Learning_And_Structure_Review_2026-10-19.md).
