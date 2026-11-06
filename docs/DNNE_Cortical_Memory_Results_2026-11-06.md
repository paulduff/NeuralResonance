# Cortical diagnostic result and the missing local modulator input

The operator reported `cortical-memory-20261106-4d4edf` complete. Frozen revision
`2b89c98a5d5e37ea710a618bc9d3a8149cd7503d` completed capture/analysis at
2026-11-06T12:36:39Z. The solution-manifest case passed; the unchanged original
consolidation assertion failed: **0.000189 -> 0.000113**, strength 0.011191.
DIAGNOSTICS_COMPLETE means evidence collection completed, not a repaired rule.

`review-cortical-memory.py` independently checked raw contact accounting,
modulator targets, observer invariance, the original TRX outcome and all source/
binary/package hashes. C: and D: match for **1,754 files, 249,464,443 bytes per
copy**, plus their manifest; 1,291 pre-launch packaged files were checked.
The review is `artifacts/cortical-memory-review-20261106`. No simulation was rerun.

## Measured findings

In every modulated diagnostic case, the cue targets **cell 166**, while all
**126 modulatory contacts target cell 100**. Every recorded cue learning event
has zero dopamine and acetylcholine; it has no continuous modulation updates.
Its weight changes come from actual pre/post spike pairing only. The observer
does record modulation elsewhere, so this is not a globally absent observer.

The owning source explains the separation. The test helper matches source indices
through the circuit kernel, but `StructureEngine.ResolveInboundNeuronIndex` then
applies an additional cortical population mapping. `ResolveAfferentPopulation`
sends modulatory sources to L1 and the cortical cue to L2/3. In this capture those
are different cells. Local GLU output from the L1-labelled cell does not transmit
that cell's acetylcholine/dopamine state to the cue cell.

| Diagnostic | Cue weight, first -> last cue | Ensemble score, first -> last | Integrated time | Maximum late delivery |
| --- | --- | --- | --- | --- |
| Original timing, fixed IDs | 1.0020001 -> 1.0043107 | 0.00004724 -> 0.00011974 | 180 ms | 19.99 ms |
| Continuous, simultaneous | 1.0033692 -> 1.0032777 | 0.00008784 -> 0.00040499 | 495 ms | 0 ms |
| Continuous, spaced | 1.0021387 -> 1.0040487 | 0.00007400 -> 0.00026665 | 495 ms | 0 ms |
| Continuous, no modulators | 0.99975926 -> 1.0007635 | 0.00005426 -> 0.00068725 | 495 ms | 0 ms |

The observed/unobserved original-timing cases match exactly in contacts,
acknowledgements and outbound payloads apart from fresh message IDs. Reconstructing
the score from raw contacts agrees within 1.43e-9. Its arithmetic is consistent,
but it is an aggregate of positive weight changes and update counts, not a
measured molecular consolidation process. A rising score can accompany a small
decline of the cue contact itself, and the largest final score occurs without
injected modulation in these particular controls. Neither result establishes
that modulation inhibits real biological consolidation.

The fixed-ID original-timing diagnostic increases its score while the original
random-ID test fails. Projection IDs participate in sampled conduction delays;
these are not identical arrival histories. The earlier failing IDs were not
captured, so the exact causal contribution to that assertion remains unresolved.
The wiring defect and incomplete time coverage are directly established for the
diagnostic and source, rather than inferred from the scalar assertion alone.

## Required repair and limits

Declare actual source/contact/recipient-cell connections and preserve those
targets through cortical routing. First compare identical input/timing with
modulation at the cue cell, at a different cell and absent. Require measured local
receptor exposure and contact-specific changes; do not require weight or score
to rise merely because input was repeated. Keep the original assertion visible.

Treating every modulatory projection as a separate L1 cell is not a sufficient
anatomical model of a pyramidal cell's apical dendrite. Human ChAT microscopy
found cholinergic axons throughout cortical layers, with region/layer-dependent
density ([Mesulam et al., 1992](https://pubmed.ncbi.nlm.nih.gov/1374768/)). The
proposed explicit-contact check is an engineering isolation of recipient identity,
not a claim that all neurons receive identical receptors or that ACh always
potentiates synapses. Cell-specific receptor kinetics, modulatory arbors/volume
transmission, biochemical capture and long-term consolidation still need models
and calibration. Existing STDP/tag rules and plasticity budgets remain reduced.

No live brain, Avatar, Unity or editor was changed; no whole-suite, physiology,
motor authority or recovery qualification follows. The pending motor-cell and
Golgi windows were not inspected. Motor-source integration remains prepared work
for after its source qualification; this concrete learning-path defect takes
priority over adding an unqualified source bridge.
