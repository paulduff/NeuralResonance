# Muscle geometry and initial-state preparation

This work is independent of the pending visible `184323` sensory run. No run
output or process was inspected, and no test or model was executed here.

## Existing Avatar mechanisms inspected

`AvatarArticulatedBody.cs` already names the functional muscle groups and their
separate strength capacities. For example, its ankle pair names tibialis anterior
and gastrocnemius/soleus. Preserve that design and its sensory/mechanical work.
The legacy `AvatarMuscle` uses normalized length and scalar activation; it is
not the detailed calcium-driven muscle candidate.

`AvatarDynamicMuscle` retains the Nagamori tibialis-anterior parameterization:
optimal fiber length 0.068 m, reference tendon length 0.28875 m, fiber mass
0.15 kg and reference force approximately 661.765 N. Its first explicit slow
motor unit has peak tetanic force approximately 0.462 N. The detailed full-body
component fixtures reuse that reference geometry and a small identified unit
subset for delivery checks. These fixtures are not a calibrated realization
of Avatar's existing whole-muscle capacities.

The native full-body force qualification intentionally uses synthetic opposing
paths straddling each joint by 0.1 m. It does not establish anatomical origins,
insertions, wrapping or compatible fiber/tendon resting lengths. Prescribed
40 N pulls demonstrated force delivery and exposed engine friction; they did
not qualify force generation by the detailed motor-unit population.

## Pinned anatomical source

The [OpenSim repository's Rajagopal2016 model](https://github.com/opensim-org/opensim-models/blob/e0cee6cbcf56f74731e841668ef668556d140ce9/Models/Rajagopal/Rajagopal2016.osim)
provides a concrete anatomical reference. Its [accompanying description](https://github.com/opensim-org/opensim-models/blob/e0cee6cbcf56f74731e841668ef668556d140ce9/Models/Rajagopal/README.txt)
records changes from the original paper distribution, including the newer XML
format and fiber damping. The primary paper is
[Rajagopal et al., 2016](https://doi.org/10.1109/TBME.2016.2586891).

Pinned repository revision: `e0cee6cbcf56f74731e841668ef668556d140ce9`.
Model Git blob: `f0bbab84ebbd652fece7c36928e916618f0b9fff`.
Model SHA256: `3F5C5F23E486073F2AD2AA4A4967FFE2FCDD582B1E355512BC54F70C36376BF4`.
The 874,820-byte model and original README are saved locally with retrieval
provenance. `tools/capture_muscle_anatomy_source.py` checks both source identities
and decodes data without invoking OpenSim, Unity, a solver or a neuron.

The decoded catalogue retains **80 muscles, 288 path points, 40 wrapping
surfaces, 22 source bodies and 39 serialized coordinates**. Two coordinate
couplers govern the patella coordinates. **42 muscles use wrapping**; dropping
their surfaces would change their paths and moments. Original decimal text,
frame identities, complete explicit muscle definitions, constraints and defaults
remain available. Missing library defaults are not invented.

The catalogue also records the 17 source coordinate actuators separately. They
are not muscle replacements for Avatar. The reference model's activation floors,
controllers and constitutive equations are not selected by the DNNE runtime.
The local complete source and derived catalogue are evidence artifacts, not
redistributed runtime assets or a replacement for the brain.

Twenty left/right name associations identify reference muscles corresponding
to ten existing Avatar groups. They are review pointers, not actuator mappings:
a biarticular muscle such as a hamstring must not be duplicated independently
at the hip and knee. No group lengths or force capacities are averaged/summed
by this capture.

## Required implementation direction

1. Bind anatomical source frames to Avatar's measured skeleton and explicitly
   account for its reduced joints. Source coordinates are not Unity local
   coordinates; axis swapping alone is insufficient.
2. Retain multi-body attachment routes and required wrapping. The existing
   shared point-force path implementation provides a foundation, but its straight
   segments do not implement wrapping surfaces.
3. Reconcile tendon parameter conventions before constructing material models.
   OpenSim's `tendon_slack_length` is not our candidate's `ReferenceTendonLengthMeters`.
   Empty curve properties also require the appropriate pinned library defaults.
   Direct numerical substitution is not a justified conversion.
4. Add a force-balanced fresh muscle initialization from its actual measured
   path and declared constitutive law. Failure to find a valid balance must
   expose incompatible geometry, rather than rescale anatomy, clip fiber state
   or add neural drive. This is initialization only, never resetting an evolved
   muscle. OpenSim's [muscle interface](https://github.com/opensim-org/opensim-core/blob/main/OpenSim/Simulation/Model/Muscle.h)
   likewise treats initial fiber equilibrium as a separate operation.
5. Populate explicit identified motor units to represent each declared muscle's
   capacity. Preserve neuronal recruitment, conduction and calcium dynamics;
   one diagnostic unit is not a whole-muscle force source. Qualify the resulting
   actual muscle/body loop before a standing claim.

This changes the next action from attaching the tibialis fixture to all 38
synthetic paths to constructing compatible muscle anatomy and initial states.
No anatomical, dynamic, neuronal-authority or standing qualification is inferred
from the catalogue.

Evidence location: `artifacts/muscle-anatomy-source-20261123`, with a same-name
verified D: UnityWorld backup. The capture preserves its decoder and the hashes
of the existing Avatar sources inspected. No new PowerShell test was launched;
`184323` remains the sole pending report.
