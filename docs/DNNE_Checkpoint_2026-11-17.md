# DNNE / Avatar / World checkpoint — 17 November 2026

The operator requested a natural stopping point, notes for tomorrow and a pause.
No new experiment was launched after that request. Existing windows are left
alone; no pending outputs, status or processes were inspected. Resume development
only when the operator asks. The full biological brain/body/world goal remains
unfinished, with LM/Entity/Dyad parked and CIV separate.

## Reviewed evidence at this checkpoint

- Corrected closed cleft `cleft-coupling-20261117-3076da`: all 40 focused cases
  and 468 edge, 60 local capacitance and 96 spatial cases pass the original
  60/90-digit reference criteria. Maximum error uses about 1.524% of its allowed
  bound. Full review verifies 2,384 files in each C:/D: copy, 1,813 Git files,
  frozen binaries and unchanged runtimes. The saved analysis was reproduced
  without executing model equations or tests. The new
  `artifacts/cleft-coupling-review-20261117/review.json` has its own verified D:
  copy. Repeated `ca4412` FAILED text belongs to the earlier serialization-input
  failure, preserved unchanged; it is not the corrected result.
- Calyx membrane `920328`: reviewed 23 tests, 15,984 current/gate cases and 224
  analytic voltage clamps within their original bounds. Full C:/D:/source
  verification and a separately backed-up review are recorded in
  `DNNE_Calyx_Membranes_2026-11-17.md`.
- Hair cell `959475`: reviewed isolated fixed-bath trajectories, 47 tests,
  channel arithmetic and independent references; see
  `DNNE_Vestibular_Hair_Cell_Results_2026-11-17.md`. These results do not establish
  the combined hair/cleft/calyx/afferent receptor.

Earlier spindle, motor and vestibular work remains in its individual result
documents. In particular, retain `DNNE_Spindle_Continuous_Results_2026-11-17.md`:
completion of the continuous reference did not establish its spatial accuracy.
Do not infer whole-brain or Avatar recovery from the component results above.

## Still awaiting explicit completion reports

| Results folder | Visible task | Purpose |
|---|---|---|
| `rubral-comparison-20261117-30755e` | `DNNE-Rubral-Comparison-4a71b0b7b7c4` | Audit the preserved native rubral/muscle capture and complete independent comparisons. |
| `unity-skull-20261117-1e2f21` | `DNNE-Unity-Skull-3bad563dbfa3` | Corrected native skull sampling checks. |

The operator said “the last one is still doing something,” without identifying
which of these windows. Do not infer either finished. Ask for completed folder
names/footers on resumption; inspect only those confirmed complete. Long backup
activity is not permission to terminate a window or weaken preservation checks.

The original rubral/native `4cdba5` and failed comparison `a6b77a` are preserved.
The corrected continuation uses a pinned Git reference archive so its scheduled
window does not depend on Git being on PATH. See
`DNNE_Rubral_Native_Muscles_2026-11-17.md` for the original runtime-loader fault,
packaging repair and remaining numerical gate. Saved native movement is not
yet a reviewed numerical or physiological success.

## Prepared for tomorrow, NOT launched

Package: `artifacts/cleft-open-20261117-db4794`.
Frozen revision: `04c4fd61f4df36ba438dfff26b41db28d278d625`.
D: backup: `D:\DNNE-desktop-runs\UnityWorld\cleft-open-20261117-db4794`.
All 1,848 initial files plus manifest were verified before stopping.
There is no launch task for this package yet.

This study owns 51 focused tests, exact replay of all 624 reviewed closed cases,
and 192 open-boundary cases against independent 60/90-digit references. It checks
exchange through the cleft aperture into held perilymph, including ion/current
and holding-current accounting. It does not integrate channels over time or
generate sensory spikes. No numerical acceptance bounds were relaxed.

Preparation was first attempted with the general-purpose Python interpreter and
stopped before creating a package because SciPy was absent. It succeeded using
the preserved `artifacts/numerical-reference-env-20261102/Scripts/python.exe`.
No dependency was installed or changed. The prepared configuration pins the
correct interpreter. Do not recreate or modify this frozen package merely to
update its notes.

After resumption, and once any reported outcomes have been reviewed, launch with:

```powershell
$package = 'C:\Users\duff_\source\repos\NeuralResonance\artifacts\cleft-open-20261117-db4794'
& (Join-Path $package 'source\tools\start-cleft-open-window.ps1') -PackageDirectory $package
```

The launcher verifies frozen files and both initial backup copies before opening
an independent visible PowerShell window. Do not run its tests/reference inline
or monitor the window afterward; wait for the operator's final footer.

## Authored code beyond that study

Pushed revision `04c4fd6` adds `AvatarVestibularCleftMembranes`: local hair/calyx
currents and seven channel-gate derivatives feed the same open spatial voltage/
ion derivative. Source inspection confirmed active hair channels belong on the
cleft-facing surface, while uncovered membrane contributes capacitance. MET is
counted once. Actual gates, local ions, separate inner/outer calyx membranes,
neutral KCC transport and explicit axon terminal current remain visible.

Its 12 new cases compile with zero warnings/errors but have not run. They are
not included in the prepared 51-case open study. No new live Avatar, editor or
Unity selection exists. The implementation is documented in
`DNNE_Cleft_Membrane_Coupling_2026-11-17.md` and the migration/equation audits.

Next qualify complete coupled derivatives independently, preserving the original
fixed-bath controls. Then establish stationary initialization and simultaneous
voltage/ion/gate integration with refinement and charge/species accounting.
Actual afferent dynamics, calibrated sensory transfer, neuronal balance and
recovery remain necessary. Avoid turning a prescribed terminal current into an
invented neuronal event source or using separate clamped steps as coupled biology.

Keep all tests, comparisons and benchmarks in visible PowerShell without live
monitoring. Preserve old saves and frozen evidence, verify D: backups, commit and
push coherent changes, and keep any World startup paused. No service shutdown
was requested or performed at this checkpoint.
