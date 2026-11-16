# Vestibular neuronal source candidates and loop timing

This bounded source inspection was performed while the receptor and spindle
windows awaited operator reports. No downloaded code was executed, dependency
installed, pending result inspected or live sensory model replaced.

## Existing loop boundary

`AvatarPhysicalObservationProducer` preserves an initial capture and complete
step/contact observations; receiver acceptance and consumption are separately
counted. `AvatarCausalMotorPlant` requires motor completeness through each body
endpoint. `AvatarMotorAxonReceiver` conservatively maps source completeness to
body time; its frontier does not exploit an additional motor conduction delay.
`ReceptorDrivenMotoneuron` separately checks each contact's declared completeness
plus that contact's delay before advancing the membrane.

The checked `ObservedMotorBodyStudy` intentionally advances fixed-current source
neurons before their publication group and then advances the body. Its 1/10/50 ms
packet lookahead is valid for that declared open-loop input. It is not a closed
sensory-loop scheduler. Such a scheduler must use processed sensory-neuron
completeness, not merely receipt of a body packet, and flush publication before
it exhausts the loop's actual causal lookahead. Missing inputs cannot be declared
silent to break a scheduling deadlock. No arbitrary physiological delay should
be added to make batching convenient.

`ControlProgram/VestibularLabyrinthTransducer.cs` already preserves bilateral
canal directions, cupula/adaptation state and otolith-related input. It outputs
normalized activations; downstream population generation is still a legacy
stimulus boundary. Its initial/reset call substitutes a 1/30-second update and
its frame interval is clamped. These mechanisms are preserved but cannot be
renamed individual sensory membrane firings or a qualified physical clock.

## Pinned sources

[ModelDB 244202](https://modeldb.science/244202) supplies a single-compartment
vestibular ganglion model with synthetic synaptic drive, associated with Hight,
Kalluri and Ventura. Its public mirror is pinned at
`23afe1b4efaa87b92f2cf9505b545467c507a5df`. All archive files were checked against
their Git blob IDs, lengths and SHA-256 hashes. It is a somatic ionic model, not
a complete hair-cell/mechanical transducer or anatomically distributed afferent.

Direct inspection found several reproduction choices that must remain explicit:

- The main script defaults to a custom cell, not its named sustained/transient
  presets. Its comments distinguish sodium conductance 13 in the earlier work
  from 20 in the later extension; the current custom HCN conductance is 0.91.
- `inf_tau_r_rm.m` assigns `Ih_shift = 7` inside the function. Several branches
  have different parenthesis structures in their time-constant expressions.
  Preserve and reconcile these with the corresponding publication before a port;
  do not silently label the current download the unchanged 2016 model.
- `I_ltk_rm.m` selects the Kv1-only branch internally. Merely allocating the
  extra gate arrays does not activate its optional mixed-current branch.
- Gate helpers mutate global two-step histories. The top-level solver starts with
  two equal voltage values and contains plotting/file-writing branches. A clean
  harness must make initialization, stimulus units, clock and recording explicit.

The archive and repository metadata do not identify a license. It is preserved
for source review; no repository code has been copied into a DNNE implementation
or represented as having a known reuse license. The published equations and
the scope of a future independent implementation still need a full review.

The [Govindaraju hair-cell/calyx repository](https://github.com/acgsci/vestibularHairCellCalyxNQT)
is pinned at `47d499449df66c6676ee43cbb08ea1995469a817`. Its README describes a
COMSOL model of nonquantal transmission, linked to the
[2023 PNAS paper](https://www.pnas.org/doi/10.1073/pnas.2207466120). The 14-entry
tree, README and GPL-3.0 license were retained. Its `.mph` files and supplementary
PDF were not downloaded or inspected here; no portable implementation or local
COMSOL availability is claimed. This is a separate, more detailed source lead,
not evidence that a complete neuronal vestibular transducer is already available.

## Preservation and next decision

`artifacts/vestibular-source-20261116` contains 67 files plus manifest, D: verified.
It retains API responses, commit/tree identities, the complete ganglion archive,
per-file verification and request hashes. The GitHub connector was unavailable;
public HTTPS APIs supplied the source. The original Ia source gap remains open.

Before adopting either candidate, establish the chosen version's complete
parameters/equations, lawful source handling and missing mechanics-to-synapse
interface. Then qualify channel arithmetic and the source membrane separately
from hair-cell input, timing, physiological calibration and whole-body balance.
The current receptor and spindle results take priority over a new vestibular
experiment; no third experimental window was launched for this inspection.
