# Entry 071: Continuous Cortical Ribbon

## Intent

Represent the cerebral cortex as one shared folded sheet instead of a collection of unrelated display volumes. The rendered mantle must preserve anatomical placement, expose the implemented neuronal circuits, and remain extensible as more cortical territories become functional.

## Anatomical Rules

- Pial shape, cortical parcels, neuron samples, activity markers, and sulcal landmarks derive from the same parameterized manifold.
- Functional cortical structures retain their atlas anchors; reshaping the mantle must not relocate a circuit into a different lobe.
- The cortex is represented by paired pial and white surfaces separated by a 4 mm ribbon thickness.
- Subcortical structures remain independent volumetric anatomy inside the cortical ribbon.
- The ventral frontal lobe includes an anteriorly projecting orbitofrontal shelf rather than only a flattened underside.
- The temporal lobe has a lower, lateral, elongated tongue that tapers into the temporal pole beneath the Sylvian fissure.
- Both hemispheres are generated from the same geometry and remain mirror symmetric before the established medial roll is applied.

## Implementation

- Reworked the shared cortical surface equation to distinguish temporal root, temporal tongue, temporal pole, and orbitofrontal shelf envelopes.
- Added genuine anterior displacement and controlled flattening to the orbitofrontal shelf.
- Added longitudinal extension, inferior displacement, lateral breadth, and anterior taper to the temporal tongue.
- Added an orbitofrontal boundary landmark so the frontal shelf remains legible beside the temporal lobe.
- Added a white-surface mesh offset inward from the folded pial surface by the cortical ribbon thickness.
- Kept all cortical territory meshes and neuronal sampling bound to `BuildCorticalSurfacePoint` and `BuildFoldedCorticalReferencePoint`.

## Verification

- Release editor build: succeeded with zero warnings and zero errors.
- Automated regression suite: 365 of 365 tests passed.
- Live anterior, right-lateral, and inferior preset views were captured and inspected.
- The anterior shelf is visible as a distinct ventral frontal projection.
- The temporal contour extends beneath the shelf as a longer tapered lobe.
- Render anatomy validation remained `OK`: 62 of 3,208 cortical samples off-shell (1.9%), zero non-cortical envelope failures, and no reported atlas-centre or extent failures.
- The running brain stack was left intact; only the editor was rebuilt and relaunched.

## Scope

This is an atlas-constrained procedural cortical manifold, not a subject-specific MRI reconstruction. It improves anatomical continuity and visual truthfulness without changing neuron counts, circuit dynamics, synaptic authority, or the brain-avatar-world data path.
