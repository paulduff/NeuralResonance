# Complete motor accounting through archives — 18 November 2026

The reviewed compact recording study preserved 241,906 original file byte
sequences and all 39,997 decoded physical samples while reducing storage by
65.8407%. Before using that format for new motor captures, the complete causal
auditor must consume it: source neuronal frames, contact admissions, motor
packets, conduction arrivals, applied muscle efforts and every native reply.
The endpoint-array check alone did not establish that integration.

## Explicit read-only mounts

`tools/native_evidence_view.py` composes hash-declared capture headers with
explicit immutable archive mounts. It exposes only original bytes and a small
read-only path interface. Unknown records cannot fall back to the filesystem.
Overlapping mounts, inconsistent file/directory names, case-ambiguous paths,
changed headers, traversal, closed archives and writes are rejected.

`analyse-rubral-native-body.py` now opens each compressed JSON-lines record as
a binary stream before passing it to gzip. It continues to work on ordinary
paths, and can also consume the checked archive stream. Static AST comparison
against the reviewed original requires that exact stream-opening change and
proves the rest of the auditor, including every assertion, is unchanged.
The already qualified archive implementation is pinned separately. No neuronal,
muscle, Unity, event-clock or learning code changes.

## Prospective visible qualification

The new `native_accounting_gate.py` / `start-native-accounting-window.ps1`
workflow freezes the reviewed archive results and original motor source. It
contains 12 mount/stream controls followed by two completed-capture comparisons:

- `repeat`: 7,999 physical steps at the original body resolution.
- `body-finer`: 31,996 physical steps at the selected h/4 body setting.

For each capture, the original frozen auditor reads the raw D: recording and
must reproduce the reviewed source-accounting report. The candidate auditor
then reads mounted archives and copied headers. Both complete reports and
derived model specifications must be exactly equal. Every decoded array must
match dtype, shape and bytes; its digest must also match the earlier recording
review. All archive member hashes are checked against the original manifest.
The comparison includes 24,000 source neuronal frames and 39,995 physical steps,
plus both initial body samples. No neurons, Unity player or reference ODE rerun.

Original reports retain the fact that models executed during their historical
capture. The new workflow's execution flags remain false: reading those reports
does not execute those models. Twelve new controls execute only in the separate
visible PowerShell window. No result is inferred from syntax or AST preservation.

The new package and verified backup use D: to retain C: headroom. These two
copies share one physical drive; original raw motor captures retain their
earlier C:/D: copies. No original file is removed, recompressed in place or
overwritten. New-capture staging cleanup is also not enabled by this change.

## What follows

After the reported result is reviewed, use the qualified accounting path in a
new per-condition recording workflow with explicit storage admission and
verification before any removal of its own temporary raw staging. Preserve all
old captures and failed prefixes. Then run the full original quiet, left, right,
disconnected, packet-cadence and retry matrix at h/4 body and h/4 muscle, retaining
the existing numerical limits. Strong-left convergence alone does not qualify
that full matrix, live brain authority, physiological calibration, balance or
fall recovery. This recording work is an enabling step, not motor qualification.
