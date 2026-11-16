# Ia sensory membrane: source gap and causal boundary

The independent spindle mechanics comparison is running separately. Before a
spindle can supply biological sensory events to DNNE, its mechanical state must
drive a sourced sensory membrane whose actual firings retain cell/contact identity
and causal timestamps. A model that directly applies a prescribed conductance to
the motor neuron does not supply that missing sensory neuron.

This review changes no live transducer, neuronal model, Avatar, Unity world,
editor selection or save. Neither pending experiment was inspected. No test,
model, downloaded code or benchmark was executed.

## New evidence

The [Housley et al. paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC10988694/)
describes a spatially distributed rat Ia model and says its source would be
deposited in ModelDB. Its mechanical-to-electrical conversion is partly
phenomenological. Publication of a description does not supply the complete
reproducible morphology, channel implementations and input files.

Unlike the earlier timed-out requests, this attempt successfully retrieved the
public ModelDB name and ID catalogues: **1,931 entries**. Four names contain
“spindle”: Moraud 2016, Vannucci 2017, Kim 2020, and an unrelated thalamic spindle
model. Individually fetched metadata confirms the relevant candidate IDs; the
parallel catalogue ordering is used only to locate them. No Housley Ia entry was
located by this bounded search. This does not prove the source does not exist.

Four complete public repository trees belonging to the author were also pinned:

| Repository under `nickh89` | Revision | Tree entries |
| --- | --- | ---: |
| `GT_2018_CancerChemoInteraction_Paper` | `6571faf83e180eaac1c6d34d59ecc3c72caf0255` | 41 |
| `Mechanosensory-encoding-dysfunction-emerges-from-cancer-chemotherapy-interaction` | `78a3779bdd8fe0a8f524f8248c17825824240409` | 4 |
| `Neural_circuit_mechanisms_of_sensorimotor_disability_in_cancer_treatment` | `4cfa63ba7a8438b40f86eb6b33e266a24907d5ec` | 38 |
| `Stephen-N.-Housley-Public-Repository-for-Publication-Code-and-Data` | `32ba5797879b8f94189f0e36e2f5c82a0daa2535` | 7 |

The author's [Georgia Tech thesis](https://repository.gatech.edu/server/api/core/bitstreams/b33c6137-1306-4480-b7c5-eab68293e798/content)
links the last repository. These tree inventories expose experimental tables and
R analysis filenames, but no `.hoc`, `.mod`, `.slx` or `.mdl` file. This is a
filename/tree inspection, not an assertion that every file's contents have been
read or that no implementation exists in another language, branch or repository.
The recording tables may inform later calibration; their exact correspondence
to the 2024 model's input/output protocols is not established here.

The public download link for the older
[Blum et al. Dryad dataset](https://datadryad.org/dataset/doi:10.5061/dryad.vdncjsxsw)
returned HTTP 403. The earlier API download returned 401. No data archive was
retrieved, no access restriction was bypassed, and its contents remain unknown.
The GitHub connector's search tools were unavailable, so public HTTPS APIs were
used for source discovery. Numerical dependencies were not changed.

## Why the Kim alternative is not this missing mechanism

[ModelDB 266732](https://modeldb.science/266732) is a published motor-unit study.
Its mirror was pinned at `9cf940b9ce2885cd5b2207a72c1252483f31cf19`.
Six selected text files were downloaded and checked against their Git blob IDs,
lengths and SHA-256 hashes; none was executed or adopted into DNNE.

The inspected `motor_unit.hoc` loads `group_Ia.hoc`. That file inserts `IaSyn`
on the soma and selected dendrites and selects preset conductances for muscle
length conditions. `syn_Ia.mod` computes current directly from that conductance
and membrane voltage. The figure-9 variant modulates conductance with a prescribed
sine function. These particular inputs do not model a spindle receptor membrane
or observed Ia axonal crossings. That is a scope distinction, not a criticism of
the study's motor-neuron question.

Using those inputs as our sensory replacement would skip the mechanism demanded
by DNNE's biological objective. It is therefore not selected as an Ia membrane
source. Do not treat a filename containing “Ia” or “closed-loop” as qualification.

## Consequences for implementation

Preserve `AvatarPhysicalObservationReceiver` and the fixed physical clock. They
retain sample identity, complete step order and exact packet retry receipts.
Acceptance is transport evidence; consuming a sample does not prove receptor
integration or a neural firing. The legacy `AddMusclePopulations` remains
frame-driven and cannot be relabelled as a mechanistic afferent.

The following requirements remain explicit for the receptor owner:

1. Retain a true initial state and advance on the mapped physical/neural clock.
   Missing samples stop dependent advancement. Packet boundaries and retries
   must not create repeated mechanical integration or repeated neural events.
2. Distinguish whole musculotendon path geometry from extrafusal fibre state and
   intrafusal mechanics. Body force is not an intrafusal stress measurement.
   Keep reference lengths, geometry and static/dynamic gamma drive explicit.
3. Input known at a completed endpoint cannot be retroactively applied to an
   earlier emitted firing. A completed-interval derivative has an endpoint time;
   forward/backward filtering of a finished trace is not a causal live filter.
   Test prefix invariance and numerical refinement of the declared coupling.
4. Integrate a sourced receptor terminal/axon membrane and preserve its actual
   crossings through declared contacts and delays. Do not fill a missing model
   with a firing-rate sampler, generic HH cell or arbitrary current conversion
   under a physiological-equivalence claim.
5. The source package must identify morphology/discretization, membrane and axial
   constants, channel files/parameters, temperature, initialization, mechanical
   input units/scaling, original stimuli and expected recordings, and its license.
   Channel checks precede whole-cell and mechanical-input qualification. CPU
   accuracy precedes any CUDA port; biological calibration remains separate.

The current motor/native study is explicitly open loop and permits bounded
neuronal lookahead. That permission cannot be carried into this sensory loop.
Any eventual loop owner must respect measured-input availability and declared
conduction delays, rather than advance the motor source into an unknown future.

Do not repeat these same repository/catalogue searches without a new lead or
changed access. Continue qualifying the existing mechanics and motor connection
while seeking a genuinely reproducible Ia source. Successful pending results do
not remove this source gap or qualify standing and recovery.

## Preserved review

`tools/review-afferent-source-discovery-20261116.py` verifies saved response hashes,
catalogue identities, pinned author trees and all six selected Kim source files,
and records exact source anchors. Evidence is in
`artifacts/afferent-source-discovery-20261116-29b5c8`: **30 files plus manifest**,
with a verified D: copy. Public requests and failed access responses are recorded.
This is source inspection, not an executed membrane or sensory-path test.

The only pending operator reports remain:

* `observed-native-body-20261116-bb10dc`
* `spindle-fixed-20261116-18ec5e`

No third experiment was launched. Wait for the operator's report before reading
either result; no live monitoring is permitted.
