# Entry 070: Complete Editor Activity Visibility

## Observation

The live Control Program was producing full recent dispatch batches while the editor showed little neural activity. Runtime frames contained activity from many structures, but each aggregate snapshot contained only a small subset of structure states under laptop load.

The anatomy display also hid neural structure geometry and the closed cortical reference surface obscured deep structures despite using translucent materials.

## Cause

- Neuron highlights were prepared only for structures present in the current aggregate snapshot.
- Direct source and target neuron IDs from dispatch traces were ignored when their structure was absent from that partial snapshot.
- Frames with dispatch activity but no aggregate structure states returned before applying highlights.
- Anatomy mode and the peripheral sensory filter suppressed otherwise valid structure geometry.
- The cortical shell was drawn before internal structures, allowing its depth buffer to mask them.
- Shell diffuse, emissive, and specular layers were too strong for a closed two-sided surface.

## Change

- Direct dispatch traces now illuminate rendered source and target structures independently of aggregate snapshot completeness.
- Dispatch-only frames remain renderable while the editor waits for structure snapshots.
- All 86 explicitly defined runtime structure types remain rendered in Anatomy and Circuit modes, including Retina and Cochlea.
- The 162 bilateral and midline structure instances remain visible together with 611 pathways.
- Anatomical reference shells are drawn after structure geometry and before pathways.
- Cortical, cerebellar, and brainstem shell alpha and specular strength were reduced to a faint contextual overlay.

## Live Verification

- Editor process remained responsive beside the complete DNNE brain stack.
- Live scene reported 162 structures and 611 pathways.
- Runtime definition coverage was 86 of 86 registered structure types.
- The shell became visibly transparent and internal structures remained legible through the full cerebral envelope.
- Partial snapshots continued to arrive with direct dispatch batches including 257 and 39 spikes.
- The maze was closed gracefully at the user's request; the brain stack remained running.

## Authority

This change affects observation only. It does not add symbolic authority, alter neuronal computation, resize circuits, or change the brain-avatar-world control flow.
