# Shared arrival boundaries influence unrelated receiving cells

The full boundary-timing study completed at 09:13:02 UTC on 6 November 2026.
Capture: `artifacts/boundary-timing-20261106-2c82a3`; source revision:
`084c6e2ba525dcb286a77a0d56b79ef7d7f8a22c`. Its 24 conditions use fresh isolated
Purkinje cells, fixed incoming messages and unchanged biological parameters.

The result supports the proposed numerical mechanism. An arrival aimed at one
cell partitions every cell's integration interval. Removing those arrivals
changes another cell's dynamics even though it receives no climbing input.
Restoring just the removed boundaries, without transmitter release, reproduces
the connected case's other-cell states and spikes exactly in all eight offset /
resolution comparisons for the complete minute.

## Recorded evidence

| Offset | Step (ms) | First differing sample without boundaries (s) | Maximum ordinal spike-time difference (ms) | Maximum sampled voltage difference (mV) |
|---|---:|---:|---:|---:|
| 0 ms | 0.125 | 2.028 | 0.125 | 1.71653 |
| 0 ms | 0.0625 | 2.032 | 0.0625 | 0.168866 |
| 60 ms | 0.125 | 2.088 | 0.125 | 0.0314922 |
| 60 ms | 0.0625 | 2.292 | 0 | 0.00675491 |

At both finer resolutions the marker times already lie on the regular grid;
the untouched cell remains identical with or without them. This is alignment
in this protocol, not a general solver-convergence result. A zero ordinal spike
difference can coexist with changed voltage/support states.

The diagnostic also changes output in the disconnected taught cell. At offset 0
and 0.125 ms, inserting no-op boundaries changes its total from 3,658 to 3,659
spikes. Both cases have identical biological inputs. This is numerical influence,
not communication or learning between unconnected cells.

All 16 connected/disconnected archived baselines reproduce their spike trains
and voltage/energy and contact CSV bytes exactly. The capture contains 720,000
voltage/energy rows, 3,593,568 contact rows, 1,440,000 selected cell-state rows
and 204,591 event records. Maximum energy ledger error is 4.364e-12; maximum
calcium mass error is 1.6945e-11. Event identities, targets, scheduled/processed
times, no-op counts and absence of fake releases pass the saved accounting.

`tools/review-boundary-timing.py` reproduced the complete saved analysis without
changing it. All 650 manifest-listed files in each C: and D: copy were rehashed:
2,213,296,132 bytes per copy. All 298 packaged-file hashes verify. The run recorded
all 40,076 protected older/package paths unchanged at completion; this review
does not claim a new whole-drive audit of those older files. Derived evidence is
in `artifacts/boundary-timing-review-20261106`, outside the original capture.

## Consequence

Remove unrelated-arrival partitions from each cell's numerical integration,
while preserving actual receptor arrival times and target ownership. Retaining
meaningless boundaries is not a biological repair. The next isolated candidate
and its acceptance gates are described in
[Cell-local timing candidate](DNNE_Cell_Local_Timing_2026-11-06.md).

This finding does not resolve fixed-input spike refinement, independent olive
accuracy, imposed Purkinje drives, calcium learning magnitude/selectivity,
maximum-history induction or missing LTP. The selected diagnostic states are
not a full continuation snapshot. Physiological calibration, live circuit
function and Avatar recovery remain unqualified. The separate previously
reproduced cortical-consolidation unit-test failure remains open.
