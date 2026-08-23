# Entry 132: Quarter-Metre Terrain and Physical Ascent

## Observation

The original world encoded each terrain height level as one metre while the
browser only split its surface into half-metre visual tiles. A single visible
step was therefore too high for ordinary gait, and the articulated collision
rig correctly stopped the body at its vertical face.

## World correction

Terrain is now built and simulated in quarter-metre height increments. The
Blazor world uses 0.25 metre surface voxels, while the authoritative world,
water level, shelters, obstacles, vision samples, body support, and Bepu
terrain faces share the same 0.25 metre vertical unit. The terrain generator
retains its original metre-scale relief by encoding every metre as four height
units; the sea remains at 3 metres rather than being compressed to 0.75 metre.

The four 0.25 metre surface blocks within each metre-wide source cell sample a
shared quantized grade instead of repeating one flat height. Adjacent changes
below one metre therefore form traversable quarter-metre slopes. Changes of one
metre or more remain discontinuous cliffs and receive matching Bepu collision
faces. C# and JavaScript use the same interpolation, quantization, cliff
threshold, and water datum, so the visible and physical surfaces agree.

## Body capability

The articulated body can negotiate terrain through two physical modes:

- a 0.25 metre rise is a leg-driven step requiring forward neuronal output,
  measured leg activation, upright posture, and support;
- a higher ledge up to one metre is a supported mantle requiring forward
  neuronal output, measured arm and leg activation, manipulator recruitment,
  and actual hand contact.

Motion is incremental. Every candidate body pose passes through the complete
18-collider Bepu sweep. Water, insufficient force, a lost command, failed hand
support, or another solid obstruction stops or aborts the ascent and gravity
resumes. The host neither chooses a destination nor creates an ascent command.

## Visibility and verification

The Blazor editor reports `step` or `mantle` progress beside the avatar state.
Focused tests cover restored vertical relief, quarter-metre terrain arithmetic,
graded slopes, physical cliffs, rise probing, step motion, hand-gated mantling,
effort withdrawal, and collision veto. The full DNNE suite passes 632 of 632
tests, JavaScript syntax validation passes, and the 131-project Release build
completes with zero warnings and zero errors.
