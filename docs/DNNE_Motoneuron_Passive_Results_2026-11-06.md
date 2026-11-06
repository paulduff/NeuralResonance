# Motor-cell channels and passive coupling: reviewed outcome

The operator-reported `artifacts/motoneuron-reference-20261106-f949c7` completed
its scientific checks at 2026-11-06 11:36:41 UTC using source
`1ff49a1603599b3581b3557c7aab193774173c96`. All nine motor-cell unit cases and
the solution-manifest case passed. The full DNNE suite was not selected; its
separately recorded cortical-consolidation failure remains unresolved.

All 96 voltage/calcium clamps and nine passive cable traces pass the criteria
declared before launch. Maximum clamped gate error is 4.44e-16, equilibrium error
5.55e-17 and time-constant error 1.14e-13 ms. Maximum passive voltage discrepancy
from the independent matrix-exponential reference is 6.99e-10 mV, against the
1e-7 mV bound. Maximum charge-ledger residual, including intermediate candidate
steps, is 3.30e-9 fC, against the 1e-5 fC bound. Calcium remains at its declared
baseline in every passive trace; no threshold crossings occur.

These discrepancies establish agreement of these calculations under the declared
geometry and reference conventions. They are not experimental physiological
precision or a native NEURON comparison. No active current contributes to these
passive traces, and no free-firing test has run yet.

## Backup failure and recovery

The window subsequently reported FAILED when Windows PowerShell 5.1 `Copy-Item`
rejected a generated editor-config file whose absolute source path contains
260 characters. The file was present, and extended-path .NET I/O read all 920
bytes. This was a backup failure after the checks, not a cell-model failure.

`tools/backup-unity-run.ps1` now consistently uses extended absolute paths for
enumeration, copy, hashing and size verification. It retains bounded snapshots
for open logs, rejects source links/junctions and overlapping source/destination
trees, and includes nested manifests as ordinary evidence. Only the root manifest
being generated is excluded from its own file list. No Windows setting, antivirus
configuration or long-path system policy was changed.

The updated routine was used in Windows PowerShell 5.1 to repair this completed
capture's D: backup. No experiment or test was rerun. The frozen source and original
`window-status.json` still record FAILED and its original error. The preceding
manifest and status were preserved separately before regenerating the manifest.

`tools/review-motoneuron-passive.py` independently rehashed the complete local and
D: file sets: **1,738 files and 92,909,207 bytes per copy**, plus matching manifests.
It verified all 1,281 packaged file hashes, both binary inventories, the ten TRX
outcomes and exact agreement between saved discrepancies and retained traces.
Clamped analytic relaxation was recalculated from frozen source-derived rate
functions; no cell simulation was executed during review.

Review evidence is in `artifacts/motoneuron-passive-review-20261106`, including
the original manifest, original status and `review.json`, with its own D: copy.
The recovered capture is at
`D:/DNNE-desktop-runs/UnityWorld/motoneuron-reference-20261106-f949c7`.

## Next gate

Proceed to independent active-cell numerical comparison, retaining complete
state and all threshold crossings. Require reference agreement and step-size
refinement before interpreting recruitment, afterhyperpolarization, persistent
excitation or inhibitory recovery. The candidate is still isolated: no neuronal
motor source, live brain, Avatar response or recovery has been qualified.
