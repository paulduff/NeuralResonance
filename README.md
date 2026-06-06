# Neural Resonance Engine (NRE) — Canon v2.0 (Next Notch: Regions + Waves)

This solution contains:
- **NRE.Core**: Core neuromorphic engine (pons modulation, hemispheres, corpus callosum, STP, Hebbian-lite, lateral inhibition, delayed axonal signals).
- **NRE.Api**: ASP.NET Core API hosting the engine + a simulation loop.
- **NRE.Blazor**: Blazor Server UI with Three.js renderer (region-tinted spikes + Vm-density clouds + corpus-callosum brightness).

## Run
1. Start **NRE.Api** (binds to `http://localhost:5005`, Swagger at `/swagger`).
2. Start **NRE.Blazor** and open the site (calls the API at `http://localhost:5005`).

## Visualization
- **Spikes**: particle flashes; tint encodes region (Thalamus/Hippocampus/Memory/Cortex) and base color reflects energy.
- **Vm-density clouds**: low-res point clouds showing wave-like activity volumes (downsample controlled by `HeatmapDownsample`).
- **Callosum bridge**: brightness scales with inter-hemispheric spike delivery events.

## Next notch additions
- **Homeostatic Pons**: adapts arousal/stability/reset to keep spike density near a target (Self-Organizing Criticality bias).
- **Thought clustering**: `GET /api/monitor/thought-clusters` clusters resonant voxels into "thought objects" (centroid/size/coherence).


## UI notch
- Sidebar with tabs (Stimulus / Neuromods / Pons / Monitor)
- Dark mode toggle
- Hemispheres + modules are ellipsoid masks (nodes outside are inert)
- Renderer draws ellipsoid wireframes for hemispheres and modules


## Visual notch
- Drag-rotate/zoom/pan via OrbitControls
- Removed auto-floating camera motion
- Expanded neuron spacing (renderer spacing=2.0)
- Connections rendered as line segments from engine connectivity (sampled)
- Right hemisphere rendered as a mirror of left (x mirrored)
- No outline wireframes


## Anatomy validation harness

The API now exposes `GET /api/engine/anatomy/validate` to return a structured report of atlas region summaries and biological spatial invariants for the current canon.

Latest Folded Archive entry: `docs/Entry_032_UI_Decomposition.md`


## Blazor runtime structure

The operator console now splits responsibilities across three Blazor services:

- `EngineApiClient` for NRE API transport
- `RendererInteropService` for browser/renderer interop
- `ConsoleRefreshCoordinator` for fast-frame, status, and voice polling loops


## Recent hardening

- Entry 035 adds focused regression tests around API request formation and fast-frame parsing.


## Recent stabilization notes

- Coordinator behavior tests now cover voice sequencing, fast-frame forwarding, and monitor telemetry cadence.


## Live renderer visual modes
The operator console View tab now exposes four live render modes: Anatomy, Activity, Connectivity, and Validation. These alter display emphasis only; they do not change engine state.


## Anatomy-driven shell refinement
The translucent hemisphere shell is now generated from a hemisphere-specific anatomical surface instead of a scaled sphere, giving better frontal fullness, occipital taper, inferior temporal volume, and a flatter medial wall.
