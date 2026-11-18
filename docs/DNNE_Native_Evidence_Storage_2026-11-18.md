# Compact native evidence — 18 November 2026

## Why this is needed for the next motor test

The reviewed strong-left partition comparison passes at h/4 body and h/4 muscle
steps. The remaining motor scope is the full original direction/quiet/cut/retry
matrix at that setting. Its recording volume cannot safely use the former raw
layout on C: with the current headroom. The five-condition original generated
583,562 files. Each native command and reply is a separate file, alongside the
step records. Repeating that layout for seven refined conditions would enlarge
the recording and verification burden further.

The next storage format must retain every original byte and all causal evidence.
It must not decimate physical endpoints, round floats, reconstruct events or
replace actual neuronal recordings with a synthetic stream. No old package,
failed capture, backup or save is deleted or rewritten.

## Implemented isolated format

`tools/native_evidence_archive.py` writes a ZIP64-capable compressed container
with an internal manifest of original relative paths, byte lengths and SHA-256
hashes. Source files are copied as bytes. Embedded timestamps, float bit strings,
BOMs and line endings are not decoded or reserialized by the writer. This
preserves file contents; filesystem ACLs, access/modification times and empty
directories are not a captured model state and are not promised by this format.

Publication requires unchanged source inventory, a second source hash pass,
every archive-member hash, and exact agreement with the declared original
manifest. A failed attempt retains an unpublished `.pending` prefix. Atomic
hard-link publication cannot overwrite an existing archive. Only the new
temporary link is removed; there is no source-removal or old-package migration
API. Unknown versions, unsafe paths, ambiguous names, duplicates, missing or
extra records, unsupported entries, invalid sizes and altered content fail.

The reader presents read-only logical paths, directory globs, original text and
checked binary streams. A closed reader and writes are rejected. Member hashes
are checked before exposing decoded content. Different archives retain distinct
reader identities even when their member names match.

No native bridge, neuronal/muscle equation, Unity step, transport, live service
selection or editor signal has changed. This is preparation for preserving the
broader motor experiment within storage limits, not its physiological result.

## Prospective visible qualification

`native_evidence_gate.py` prepares from the reviewed `e9bed6` outcome and its
structured original full-audit receipt. It freezes the original expected file
hashes and two completed strong-left captures from the verified D: source:

| Capture | Files | Original bytes | Physical endpoints |
| --- | --- | --- | --- |
| repeat, h body / h/2 muscle | 48,962 | 303,394,562 | 8,000 |
| body-finer, h/4 body / h/4 muscle | 192,944 | 896,677,734 | 31,997 |

The independent visible PowerShell window runs 24 recording controls, then
archives and verifies all 241,906 original files. The controls include exact
byte preservation, read-only access, reader lifetime, corruption, duplicate and
unsafe names, truncation, changing sources, unpublished records, wrong manifests
and overwrite refusal. No tests have executed during authoring.

It then runs the unchanged reviewed endpoint decoder on each raw capture and
its archive. Every returned array must have identical shape, dtype and bytes,
including the deliberately unobserved NaN columns. Scalar ledger summaries must
also agree exactly. This checks representation at every saved endpoint; it does
not extend independent trajectory accuracy to the finer intermediate samples.
Actual compression size is recorded, not assumed. This is not a throughput
benchmark, especially while the separate sensory comparison is running.

The new qualification workspace is under `D:/DNNE-build-work`, with its verified
copy under `D:/DNNE-desktop-runs/UnityWorld`. Those two new container copies share
a physical drive and are not independent-drive redundancy. All original raw
evidence retains its existing C: and D: copies; neither is removed. The choice
keeps this qualification from consuming the C: space reserved for sensory work.
The preparation pointer remains in the repository's artifacts directory.

Python AST, PowerShell parsing, static 24-case inventory and the reviewed
numerical environment match at authoring. Tests, conversion and decoded-array
comparisons will execute only in the visible window, without live monitoring.

## Remaining integration

After a reported and verified pass, integrate the format into a newly declared
motor capture workflow with bounded per-condition staging. Qualify complete
archived source/packet/native accounting before any raw new-record cleanup is
enabled. Preserve all old captures. Storage admission must include the maximum
temporary raw condition and verified archive, as well as the reserve. Then run
the full original motor matrix at the refined setting with unchanged biological
parameters and error bounds. This utility alone does not qualify that matrix,
live brain authority, human physiology, balance or recovery.

The sensory retry `501244` remains pending and unmonitored. Full
DNNE/Avatar/World goal remains active and unfinished.


Compact evidence qualification is launched as `native-evidence-20261118-bc0501`,
with workspace `D:/DNNE-build-work/native-evidence-20261118-bc0501`, frozen at
pushed revision `9eff479fb3ceb01dbe429e7941c61335ea248422`. Static frozen
preflight passed; 1,976 initial files plus manifest were verified at
`D:/DNNE-desktop-runs/UnityWorld/native-evidence-20261118-bc0501` before visible
task `DNNE-Native-Evidence-956f6d8a1733` started. These new qualification copies
share D:; the original raw motor captures still retain their C:/D: copies.
Launch returned successfully. No post-launch status, output or process inspected.
The window runs 24 recording controls and the two completed-capture comparisons;
no neural/Unity/ODE model reruns. No success, size reduction or integration is
inferred. Exact file bytes and decoded arrays must pass before adoption.
Exactly TWO completion reports are now pending: sensory `501244` and compact
evidence `bc0501`. All older windows are reviewed. Original records and saves
remain untouched. Full DNNE/Avatar/World goal remains active and unfinished.

Completed compact evidence `bc0501` is now reviewed. Both 2,021-file package
copies and frozen Git source verify. All 24 declared controls passed. Every
archived member matches the original manifest across 241,906 files; decoded
array digests reproduce exactly for all 39,997 physical samples. The archives
use 409,936,598 bytes versus 1,200,072,296 raw bytes (65.8407% reduction).
The four-file review artifact `native-evidence-20261118-bc0501-review` is
D: verified. This was saved-byte/array review only; no models or tests reran.
Original raw C:/D: captures remain; the new archive workspace and verification
copy share D:, so they are not independent-drive redundancy. The completed
format check does not yet qualify a live capture/cleanup workflow. Full motor
source/packet/native accounting through the format is still required before
new-record staging cleanup or the full refined motor matrix. No original
records, limits, saves, or live selectors changed. Only ONE completion report
is now pending: sensory retry `501244`, untouched. The repeated failed `2b5a68`
message is already reviewed and repaired, not a new failed retry.
Independent source-based coupled canal mechanics are being authored; no
physiological calibration or live balance qualification is inferred.
