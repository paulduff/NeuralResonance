# Cortical precision through actual cells and learned-state storage

The completed isolated contact comparison showed that small updates can be lost
to float rounding and per-update numerical cutoffs. This candidate carries the
reviewed double contact state through the actual recurrent StructureEngine,
receiving ModelNeuron receptors and an explicitly isolated save format. It does
not substitute a new learning curve or increase biological learning multipliers.

## Implementation

An internal constructor option supports full isolated cortical populations using
STDP, synaptic tagging/capture, or their existing combination. It requires an
explicit absolute state directory and an isolated instance identity. No live host
or production configuration selects it. All nonsynthetic receiving glutamate
contacts, including recurrent contacts, acquire authoritative double state.
Existing float fields are one-way diagnostic mirrors. GABA, modulatory and
synthetic contacts retain their existing rules.

Presynaptic arrivals and actual ModelNeuron firing drive contact updates with
their biological timestamps. Modulation comes from the connected receiving cell.
A separate read-only firing observer records fractional substeps; tick-end
`IsActive` alone cannot count every firing. Recurrence is retained in the study.

The learned weight multiplies presynaptic release in double precision at the
receiver. AMPA, NMDA, metabotropic, dendritic and existing intracellular inputs
receive that efficacy without an intermediate float conversion. Source messages
remain unchanged. Shared legacy receptor arithmetic is preserved for float input.

Schema 5 is restricted to this candidate. It stores exact contact state and checks
identity, model fingerprint, rule, recipient map, membership and diagnostic
mirrors on load. Schema 4 remains the production default. Neither automatic
upgrade nor fallback to rounded state is permitted. An explicit compatible warm
start preserves learned weights and remaining budget but resets transient traces,
receptor/membrane dynamics and event queues. It is not an exact brain checkpoint.

## Prospective PowerShell study

`prepare-cortical-precision.py` requires clean pushed source and verifies the
completed numerical candidate and seven-scenario legacy capture against their
D: copies. It archives source and copies only the required reference evidence.
`start-cortical-precision-window.ps1` requires a verified initial backup, opens an
independent visible PowerShell window and keeps it open after completion.
There is no live monitoring and no running brain or old brain save is accessed.

The frozen window builds and runs 68 focused cases: precision integration (13),
declared recipients (8), biological contact learning (27), inbound efficacy (9),
determinism (10), and solution completeness (1). It repeats all seven old cortical
scenarios and compares every recorded numerical field, identity and event order;
only generated per-run message UUIDs are excluded from that legacy comparison.

Seven new scenarios use the full TemporalAssociation population for 550 ms with
120 cue arrivals: legacy paired, precise paired, neighboring modulation, absent
modulation, STDP alone, capture alone and synthetic cue. Every actual firing,
precise contact update and receptor delivery is saved. Each precise case also
performs an explicit learned warm start at a new biological time origin.

The offline analyzer independently evaluates recorded contact transitions, checks
post events against actual cell firings, matches each presynaptic arrival to its
contact delivery, checks receptor gain and exact persisted double state, and
verifies locality and warm-start semantics. Prospective absolute limits are
2e-12 for state, 2e-13 for budget and 1e-11 for measured receptor gain. At least
one paired update must change the double weight while leaving its float mirror
unchanged. Failure preserves evidence; bounds are not adjusted automatically.

Both source and executed binaries are hashed. Final logs, results and failures
receive a verified D: backup. The operator reports completion before review.

## Limits

Build success is not a passed study. This is a numerical/integration repair of
the existing reduced contact rule. Its tagging/capture approximation, plasticity
budget, receptor gains and cortical cell physiology remain uncalibrated. A passed
study would not establish biochemical consolidation, the intermittent original
aggregate consolidation assertion, the full unit suite, or Avatar recovery.
The new warm-start tests load only saves made within this isolated experiment.
Live Avatar, Unity and editor behavior is unchanged; the diagnostic status labels
the candidate when used. Promotion requires the saved outcome and further
biological review. The separate Golgi integration failure remains unresolved.

## Launch record

The window was launched on 9 November as
`cortical-precision-20261109-67008c`, frozen revision
`4fefd12c27c027fb86b048d37b25481f9fc00d1b`, task
`DNNE-Cortical-Precision-f3c50e797d62`. Its 1,450 initial files plus manifest have
a verified D: copy. Probe and test builds completed with zero warnings/errors;
Python and PowerShell syntax checks passed before freezing. No tests were run
inline. The numerical outcome is pending the operator's completion report.

## First outcome and harness corrections

The operator reported that window failed. The completed capture verifies on C:
and D: (1,840 files, 367,353,970 bytes per copy), including frozen source and test
binaries. `tools/review-cortical-precision-first-20261109.py` records the audit in
`artifacts/cortical-precision-first-review-20261109/review.json`. All 68 required
tests ran: 67 passed and the inhibitory/synthetic contact test failed. Neither
seven-scenario study started, so the complete integration result is unavailable.

The failing test incorrectly expected delivered contacts after 1 ms. Its input
originates in Entorhinal cortex, whose existing tract window is 3..12 ms with a
bounded timing adjustment and a maximum 24 ms delivery time. Declaring a recipient
does not remove that conduction delay. The corrected test requires no arrivals
after 1 ms, then advances through 24 ms and requires exactly the three distinct
input events at their declared cells within the existing delivery bounds. It also
checks synthetic nonlearning, inhibitory learning and precision save membership.
No conduction or learning code changes for this retry.

Review of the saved test names also exposed a later runner issue: the existing
`StructureEngineDeterminismTests` class has no namespace. The new configuration
records each fully qualified class name as it actually appears, rather than
prefixing that class with a nonexistent namespace. The same 68 cases remain
required. This check had not yet been reached by the failed window.

The corrected test assembly builds with zero warnings/errors. Runtime confirmation
requires a new frozen PowerShell package. The original failed package remains
unchanged; reference data, model equations and numerical acceptance limits are
preserved. These harness corrections do not qualify the candidate.
