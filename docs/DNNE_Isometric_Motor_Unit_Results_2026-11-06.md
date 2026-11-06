# Isometric motor-unit integration: reviewed fixture result

The operator reported completion of `artifacts/motor-unit-plant-20261106-5836ab`.
It finished at 2026-11-06 12:04:01 UTC, exit 0, using pushed source
`33f3914780057d7c2df3794f75c47dc91822bece`.

All **61 required cases passed**: 32 motor-axon transport, 13 calcium component,
15 isometric integration and one solution-manifest case. The full DNNE suite was
not run here. Its separately recorded cortical-consolidation failure remains open.

The fifteen new cases establish the asserted fixture behavior: original identity
and fractional arrival timing, no second conduction delay/activation filter,
exact equality of complete unit state across frame and packet partitioning,
isolation from another unit's traffic, source-order invariance, replay protection,
source completeness, invalid-time/packet rejection, explicit parameter coverage,
zero response to proven silence, snapshot isolation, fail-stop on a numerical
fault and model fingerprints. The numerical-fault case deliberately supplies an
extreme release rate and requires an exception; this is a diagnostic fixture,
not a physiological parameter set.

`tools/review-isometric-plant.py` checked every test against the frozen class/case
inventory, all packaged and binary hashes, both complete file sets and manifests.
The C: and D: captures match for **1,672 files and 88,254,359 bytes per copy**, plus
their manifest. The package contains 1,281 pre-launch hashed files. Review evidence
is in `artifacts/isometric-plant-review-20261106`, with its own verified D: copy.
No experiment or test was rerun during this review.

The operator subsequently recalled an overflow message. No overflow message is
present in this capture's transcript, stage logs or TRX; its build reports zero
warnings/errors. The originating window/error text has been requested. Do not
attribute an unidentified overflow to the intentional fault fixture, or conclude
that it is harmless from this passing report. The separate active-cell result
remains pending operator report and review.

These events are fixtures, not actual qualified motor-neuron output. The outcome
supports the isolated event-to-unit connector, not endplate physiology, human
muscle calibration, dynamic body coupling, neuronal authority or Avatar recovery.
No live Avatar, Unity world or editor was changed or restarted. Source-cell
qualification and coupled causal tests remain the next motor-path requirements.
