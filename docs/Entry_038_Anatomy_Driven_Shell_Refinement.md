# Entry 038 - Anatomy Driven Shell Refinement

## Intent

Refine the translucent hemisphere shell so it reads more like cerebral anatomy and less like a scaled sphere wrapped around the neural point cloud.

## Problem

Entry 037 introduced shell overlays using a simple sphere scaled to fit the live left and right hemisphere bounds. That gave useful embedding, but it still had several visual shortcomings:

- too uniformly round in lateral and superior views
- insufficient frontal/occipital differentiation
- insufficient inferior temporal fullness
- medial wall did not read as a flattened hemispheric cut

## Change

The shell is now generated from a hemisphere-specific anatomical surface rather than a sphere primitive.

The new shell geometry adds:

- frontal fullness
- occipital taper
- inferior temporal bulge
- flatter medial wall
- slightly superior dorsal flattening
- small live-fit offsets so the shell sits more naturally around the neural cloud

## Implementation

Primary change:

- `src/NRE.Blazor/wwwroot/js/neuralRenderer.js`

New shell path:

- `createAnatomyShellGeometry(THREE, sideSign)` generates a per-hemisphere surface
- `createShellMeshes()` now uses the anatomy shell geometry for left and right hemispheres
- `updateShellMeshes()` still fits from live base geometry bounds, but now applies anatomy-aware scale and centering adjustments

## Expected visual result

Compared with Entry 037, the shell should now:

- look less spherical in lateral view
- read longer anteroposteriorly
- feel fuller through the frontal lobe
- taper more naturally toward the occipital pole
- show a flatter hemispheric cut toward the sagittal plane

## Scope

This is a renderer-shell refinement only.

It does **not** alter:

- engine atlas positions
- anatomical validation rules
- neural connectivity
- monitor/status contracts

## Why this notch now

The transport, coordination, and test seams are now substantially more stable than earlier canons. That makes the renderer a safer place to improve, and the shell is one of the highest-impact visual elements because it frames the entire neural display.
