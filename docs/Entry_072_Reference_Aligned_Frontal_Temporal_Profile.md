# Entry 072: Reference-Aligned Frontal-Temporal Profile

## Intent

Correct the frontal-temporal relationship using the bundled anatomical references. The anterior frontal cortex must project away from the cerebellum, form a visible shelf over the temporal lobe, and preserve the rising medial contour of the temporal lobe near the longitudinal fissure.

## Reference Rules

- Positive longitudinal displacement is anterior: toward the frontal pole and away from the cerebellum.
- The ventral frontal surface forms an anterior orbitofrontal shelf rather than continuing directly into the temporal contour.
- A frontotemporal notch separates the shelf from the temporal pole in lateral and anterior views.
- The temporal lobe remains visible below and lateral to the frontal shelf.
- The medial temporal wall rises toward the longitudinal fissure instead of remaining flat across its width.
- Cortical opacity remains unchanged; only internal reference and guide surfaces are attenuated.

## Implementation

- Moved the shelf envelope into the ventral frontal band and increased its anterior projection.
- Added a localized frontotemporal notch to create a genuine re-entrant boundary below the frontal shelf.
- Lowered and broadened the temporal tongue and pole so they remain visible beneath the shelf.
- Added a medial temporal rise concentrated near the longitudinal fissure.
- Deepened the Sylvian indentation to improve the visual separation between frontal and temporal cortex.
- Reduced opacity and specular intensity for deep-anatomy guide shells and tubes without changing cortical parcel rendering.
- Repositioned the orbitofrontal landmark to follow the corrected shelf boundary.

## Verification

- Release editor build: succeeded with zero warnings and zero errors.
- Automated regression suite: 365 of 365 tests passed.
- Live anterior, right-lateral, and inferior preset views were captured and inspected against the bundled references.
- The temporal lobe is visible beneath the frontal shelf, and its medial wall rises toward the longitudinal fissure.
- Render anatomy validation remained `OK`: 92 of 3,208 cortical samples off-shell (2.9%) and zero non-cortical envelope failures.
- The running brain stack remained intact; only the editor was rebuilt and relaunched.

## Scope

This pass changes editor geometry and reference-surface presentation only. It does not change circuit placement, neuron counts, synaptic behavior, simulation authority, or the brain-avatar-world data path.
