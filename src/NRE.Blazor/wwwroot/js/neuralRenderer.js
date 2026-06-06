window.NeuralRenderer = (function () {
  // ==========================================================================
  // UNIFIED NEUROANATOMICAL COORDINATE SYSTEM
  // See: docs/Coordinate_System_Specification.md
  //
  // Standard Axes:
  //   X: Lateral     -1 = Left,     +1 = Right,    0 = Midline (midsagittal)
  //   Y: Vertical    +1 = Superior, -1 = Inferior  (dorsal/ventral)
  //   Z: Depth       -1 = Anterior, +1 = Posterior (rostral/caudal)
  //
  // Anatomical Directions:
  //   ANTERIOR (-Z): Toward face/frontal lobe
  //   POSTERIOR (+Z): Toward back of head/occipital
  //   SUPERIOR (+Y): Toward top of skull
  //   INFERIOR (-Y): Toward chin/base of skull
  //   LEFT (-X): Left hemisphere
  //   RIGHT (+X): Right hemisphere
  //
  // Anatomical Planes:
  //   Sagittal: Y-Z plane (divides left/right)
  //   Coronal: X-Y plane (divides front/back)
  //   Horizontal/Axial: X-Z plane (divides top/bottom)
  // ==========================================================================
  
  const ANTERIOR = -1;
  const POSTERIOR = +1;
  const SUPERIOR = +1;
  const INFERIOR = -1;
  const LEFT_LATERAL = -1;
  const RIGHT_LATERAL = +1;

  let scene, camera, renderer, controls;
  let _pendingViewPreset = null;

  // Visual uplift controls
  let renderMode = 'activity';
  let shellVisible = true;
  let fibrePulseEnabled = true;
  let depthFogEnabled = true;
  let shellLeft = null, shellRight = null;
  let connPulsePhase = null;

  // Point clouds
  let pointsL, pointsR, geomL, geomR, matL, matR;
  let basePointsL, basePointsR, baseGeomL, baseGeomR, baseMatL, baseMatR;
  let midlinePoints, midlineGeom, midlineMat;
  let ccPoints, ccGeom, ccMat;
  
  let layoutPosMap = new Map();
  
  // Packed numeric key: avoids template-string allocation on every lookup.
  // hemi is 0/1, x/y/z are 0..255 (byte coords). Pack into a single 32-bit int.
  function _posKey(hemi, x, y, z) {
    return (hemi << 24) | (x << 16) | (y << 8) | z;
  }
  const CC_GHOST_KEY_OFFSET = 100000;
  const CC_GHOST_DIM_FACTOR = 0.30;
  function _ccGhostKey(hemi, x, y, z) {
    return _posKey(hemi, x, y, z) + CC_GHOST_KEY_OFFSET;
  }
  
  // Dedicated CC voxel position map � keyed same as layoutPosMap but never overwritten
  // by cortex/subcortical neurons that share the same voxel coordinates.
  let ccPosMap = new Map();
  // Sorted array of CC positions for nearest-neighbor connection anchoring.
  // Each entry: { z: voxelZ, pos: {x,y,z} }
  let ccPosArray = [];
  
  // Density clouds
  let densL, densR, densGeomL, densGeomR, densMatL, densMatR;

  // Spike buffers (reuse to avoid per-frame allocations / GC hitches)
  let spikeCap = 2500;
  let spikePosLArr, spikeColLArr, spikePosRArr, spikeColRArr;
  let spikeAttrPosL, spikeAttrColL, spikeAttrPosR, spikeAttrColR;

  // Connections
  let connLines, connGeom, connMat;
  let connectionsLoaded = false;
  // connKeyToSeg: maps packed connection endpoint pair to segment index.
  // Key = two packed 32-bit ints combined into a string "A|B" (still far cheaper than 10-field template).
  let connKeyToSeg = new Map();
  function _connKey(h1, x1, y1, z1, r1, h2, x2, y2, z2, r2) {
    const a = (h1 << 24) | (x1 << 16) | (y1 << 8) | z1;
    const b = (h2 << 24) | (x2 << 16) | (y2 << 8) | z2;
    // Include region in a compact way (5 bits each, plenty for region IDs < 32)
    return ((a * 31 + r1) * 2147483647 + b) * 31 + r2;
  }
  let connGlow = null; // Float32Array per segment
  let connBaseCol = null; // Float32Array copy
  let connActive = []; // active segment indices
  let connActiveSet = new Set();
  let lastAnimT = 0;

  // Brain outline meshes
  let brainMeshes = [];
  let outlineRefs = {
    leftSag: null,
    rightSag: null,
    coronal: [],
    axial: null,
    fissure: null,
    _segs: { sag: 48, cor: 36, axial: 90, fiss: 34 }
  };

  // Outline rendering is optional in Live view. Keep it OFF by default.
  // (This variable existed in earlier iterations; some code paths still
  // reference it. If undefined, the renderer can crash during init.)
  let showOutline = false;

  let w = 20, h = 20, d = 20;
  let xOffset = 20;
  // Midline fissure gap (world units, pre-axis scaling).
  // This is the *empty* space between the two hemispheres.
  let midlineGap = 1.60 * 600.0;
  let spacing = 2.4;
  // Mirror the right hemisphere across the sagittal plane.
  // This is a DISPLAY-SPACE mirror: the backend volumes use a per-hemisphere voxel
  // convention where the right hemisphere's medial wall is toward x=0.
  // Mirroring here ensures the right hemisphere is a true reflection of the left
  // (not just a translated copy) without changing simulation state.
  let mirrorRight = true;

  // --- Functional area overlay (reference anatomy map; display-only) ---
  // 1..14 match the user's reference diagram (cortical functional areas + cerebellum).
  // Subcortical nuclei keep their biological region colors.
  // Functional overlay is a *reference* view. Default it OFF so the canonical
  // structural colours (modules / regions) remain readable unless the user
  // explicitly enables the overlay.
  let functionalOverlayEnabled = false;
  let functionalLabelsEnabled = false;
  let functionalSprites = new Map(); // id -> THREE.Sprite

  // --- Sagittal split (Option A) + hemisphere fit (Option B) ---
  // Option A: clamp/flatten the medial wall so each hemisphere reads as a half-volume
  //           along the sagittal plane, with a visible longitudinal fissure.
  let medialFlattenEnabled = false;   // Not used in clean renderer
  let medialFlattenStrength = 0.18; // 0..0.35 (fraction of hemi x-radius)
  let superiorFissureBoost = 0.10;  // extra widening at the dorsal midline

  // Option B: after all warps (gyrification + silhouette warp), project points back inside
  //           a super-ellipsoid hemisphere envelope so the neural cloud doesn't "leak".
  let fitToHemisphereEnabled = false;  // Not used in clean renderer
  let fitExponent = 2.35; // 2=ellipsoid, higher=boxier; 2.2..2.6 reads well at 32^3

  // --- Brain silhouette tuning (visual mapping only) ---
  // Match *human* gross proportions by ratio:
  //   length (antero-posterior, Z-axis) � 167mm
  //   width  (left-right, X-axis)       � 140mm
  //   height (superior-inferior, Y-axis) � 93mm
  //
  // Coordinate mapping:
  //   Z: Anterior (-) to Posterior (+) = length
  //   X: Left (-) to Right (+) = width  
  //   Y: Inferior (-) to Superior (+) = height
  // Keep the existing Z elongation as the overall reference scale,
  // then derive X/Y from the real-world ratios.
  let scaleZ = 1.75; // length baseline
  let scaleX = scaleZ * (140.0 / 167.0);
  let scaleY = scaleZ * (93.0 / 167.0);

  // Small deterministic spatial jitter (world units) to break up the visible voxel grid.
  // Biology is irregular; this keeps the overall anatomy shape while avoiding "stacked-sheet" visuals.
  // Set to 0 to disable.
  let jitter = 0;  // LOCKED: biological view � no jitter

  // Deterministic hash -> [0,1)
  function hash01(a, b, c, d) {
    let x = (a * 73856093) ^ (b * 19349663) ^ (c * 83492791) ^ (d * 2654435761);
    x = (x ^ (x >>> 13)) >>> 0;
    x = (x * 1274126177) >>> 0;
    return (x >>> 0) / 4294967296.0;
  }

  // Mirror helper: converts per-hemisphere voxel X to the effective X used for mapping/procedural fields.
  // When mirrorRight is enabled, the right hemisphere uses x' = (w-1-x) so it becomes a true mirror of the left.
  function hemiVoxelX(hemi, x) {
    return (mirrorRight && hemi === 1) ? ((w - 1) - x) : x;
  }

  function jitterVec(hemi, x, y, z) {
    const amp = jitter * spacing;
    if (amp <= 0) return { jx: 0, jy: 0, jz: 0 };
    const r1 = hash01(hemi + 1, x + 11, y + 37, z + 53) - 0.5;
    const r2 = hash01(hemi + 7, x + 19, y + 41, z + 59) - 0.5;
    const r3 = hash01(hemi + 13, x + 23, y + 43, z + 61) - 0.5;
    return { jx: r1 * amp, jy: r2 * amp, jz: r3 * amp };
  }

    // --- Procedural gyrification (cortical folds) ---
  // We approximate gyrification by displacing cortical mantle points along their outward normal
  // using a cheap, continuous multi-sine field. This is purely visual (render-space) and keeps
  // the simulation topology untouched.
  // At 32^3, folds need slight exaggeration to "read" as brain.
  // Canon (Paul): cortex is constructed piece-by-piece as gyri; render should not
  // apply any additional silhouette warping/gyrification.
  let gyrifyEnabled = false;  // LOCKED: biological view only
  let gyrifyAmpVox = 0.85;   // amplitude in voxel units (multiplied by spacing)
  let gyrifyFreq1 = 0.55;    // radians per voxel
  let gyrifyFreq2 = 1.05;
  let gyrifyFreq3 = 1.85;

  // --- Aggressive anatomical warp (Option B) ---
  // This is a purely visual, deterministic warp that sculpts the point cloud
  // into something that reads as a brain at 32^3: frontal bulge, occipital taper,
  // temporal flare, and a flatter inferior surface.
  let brainWarpEnabled = true;   // LOCKED: biological view only
  let warpStrength = 1.00; // 0..1.5
  
  // === ANATOMICAL MODE ===
  // Canon (Paul): "drawn without warping" means: no artificial fold/gyrify noise.
  // However, we DO want the *anatomical silhouette* (frontal bulge / occipital taper / temporal flare)
  // so the cortex isn't rendered as a sphere. Therefore anatomicalMode keeps gyrify OFF but keeps
  // the outline-fit warp ON.
  let anatomicalMode = true;  // LOCKED: biological view only


// === ANATOMICAL CIRCUIT PLACEMENT ===
// When enabled, the Live view places neurons inside coarse anatomical circuit volumes
// (matching anatomical-viewer.html), while still rendering individual neurons/edges.
// DISPLAY-ONLY remap: simulation is unchanged.
let anatomicalCircuitPlacementEnabled = true;  // LOCKED: biological view only

  // --------------------------------------------------------------------------
  // Packed byte[] interop helpers (Blazor Server -> JS)
  // .NET serializes byte[] as base64 strings in JSON. Many API endpoints return
  // packed binary payloads (points/lines/traffic) as byte[]; when passed through
  // JS interop they arrive as base64 strings. If we treat those as numeric
  // arrays we get NaN coordinates, which can result in "nothing renders".
  // --------------------------------------------------------------------------

  // Fast base64 -> Uint8Array using a pre-built decode table.
  // Avoids per-byte charCodeAt overhead from the atob+loop pattern.
  const _B64 = new Uint8Array(128);
  'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'.split('').forEach((c, i) => _B64[c.charCodeAt(0)] = i);

  function _b64ToU8(b64) {
    // Strip padding
    let len = b64.length;
    while (len > 0 && b64.charCodeAt(len - 1) === 61) len--;
    const outLen = (len * 3) >> 2;
    const out = new Uint8Array(outLen);
    let j = 0;
    for (let i = 0; i < len; i += 4) {
      const a = _B64[b64.charCodeAt(i)];
      const b = (i + 1 < len) ? _B64[b64.charCodeAt(i + 1)] : 0;
      const c = (i + 2 < len) ? _B64[b64.charCodeAt(i + 2)] : 0;
      const d = (i + 3 < len) ? _B64[b64.charCodeAt(i + 3)] : 0;
      out[j++] = (a << 2) | (b >> 4);
      if (j < outLen) out[j++] = ((b & 0xF) << 4) | (c >> 2);
      if (j < outLen) out[j++] = ((c & 0x3) << 6) | d;
    }
    return out;
  }

  function _ensureU8(data) {
    if (!data) return null;
    if (data instanceof Uint8Array) return data;
    // Some environments may hand us a Uint8ClampedArray or similar.
    if (data.buffer && typeof data.byteLength === 'number') return new Uint8Array(data.buffer);
    if (Array.isArray(data)) return Uint8Array.from(data);
    if (typeof data === 'string') return _b64ToU8(data);
    return data;
  }

  function _normalizePacked(p) {
    if (p && p.data) p.data = _ensureU8(p.data);
    return p;
  }

// Target circuit volumes (approx mm; Live view world units are ~mm-scale).
// Unified coordinate system (NRE canon):
//   +X right, -X left
//   +Y superior, -Y inferior
//   +Z posterior, -Z anterior
//
// BIOLOGICAL HEMISPHERE SHAPE:
// The cerebral hemisphere viewed from front: wider than tall.
// Real brain: ~140mm wide (both hemispheres), ~93mm tall, ~167mm long
// Per hemisphere: ~70mm lateral � ~55mm tall � ~80mm AP
const HEMI_RX = 68;   // lateral extent from midline (mm)
const HEMI_RY = 55;   // superior extent from center (mm)  
const HEMI_RZ = 78;   // anterior-posterior extent from center (mm)

const ANATOM = {
  // -- midline structures --
  // Slightly enlarged from anatomy so they still read clearly inside the cortex.
  thalamus:     { type: 'ellipsoid', c: {x:0, y:1.2,  z:0.6},  r: {x:10.6, y:8.6, z:10.8} },
  hypothalamus: { type: 'ellipsoid', c: {x:0, y:-4.2, z:-3.8}, r: {x:4.6, y:3.6, z:5.0} },
  // Additional shift pass: +15% inferior (+|Y|) and +10% posterior (+Z).
  brainstem:    { type: 'ellipsoid', c: {x:0, y:-27.0,z:8.1}, r: {x:7.8, y:16.2, z:8.0} },
  // Additional shift pass: +15% inferior (+|Y|) and +10% posterior (+Z).
  pons:         { type: 'ellipsoid', c: {x:0, y:-19.8, z:8.5}, r: {x:9.0, y:7.0, z:8.0} },
  // Additional shift pass: +15% inferior (+|Y|) and +10% posterior (+Z).
  cerebellum:   { type: 'ellipsoid', c: {x:0, y:-30.9,z:25.5},r: {x:18.8, y:11.4, z:14.6} },

  // -- bilateral subcortical (x sign applied per hemisphere) --
  hippocampus:  { type: 'tube',      c: {x:8.3, y:-6.3, z:-0.2}, len: 26.0, rad: 3.9 },
  amygdala:     { type: 'ellipsoid', c: {x:12.0,y:-7.0, z:-10}, r: {x:3.5, y:2.9, z:3.5} },
  basalGanglia: { type: 'ellipsoid', c: {x:8.8, y:-0.2, z:-0.5},r: {x:4.6, y:3.8, z:5.2} },
  // -- cortex: per-gyrus target volumes (per hemisphere, x sign applied) --
  // BIOLOGICAL POSITIONS: These match actual cortical surface anatomy.
  // The cortex forms a continuous mantle wrapping the hemisphere.
  //
  // Key biological constraints:
  //   - Central sulcus runs from superior midline (~y=60) down to Sylvian fissure (~y=10)
  //     at about z=-4 to z=8 (slightly posterior to center)
  //   - Frontal lobe: anterior to central sulcus, occupies ~40% of cortex
  //   - Parietal lobe: posterior-superior to central sulcus
  //   - Temporal lobe: inferior to Sylvian fissure, lateral surface
  //   - Occipital lobe: posterior pole
  //   - Superior gyri (frontal, parietal) extend to MEDIAL wall (x near 8-12mm)
  //   - Temporal gyri run along the LATERAL-INFERIOR surface
  //   - Precentral/Postcentral run as vertical strips flanking central sulcus
  gyri: {
    // === PRIMARY MOTOR/SENSORY STRIPS (central sulcus belt) ===
    // Run from medial wall (leg area) to lateral surface (face area)
    // as wide strips flanking the central sulcus
    11: { c: {x:30, y:35, z:-4},  r: {x:28, y:32, z:14} },  // Precentral (M1)
    12: { c: {x:30, y:35, z:10},  r: {x:28, y:32, z:14} },  // Postcentral (S1)

    // === FRONTAL LOBE (anterior to central sulcus) ===
    // Large volumes � frontal lobe is ~40% of cortex
    15: { c: {x:14, y:44, z:-42}, r: {x:14, y:24, z:28} },  // Superior frontal - medial+dorsal
    16: { c: {x:36, y:32, z:-38}, r: {x:22, y:22, z:28} },  // Middle frontal - lateral
    17: { c: {x:48, y:12, z:-32}, r: {x:18, y:18, z:24} },  // Inferior frontal - ventrolateral

    // === PARIETAL LOBE (posterior-superior to central sulcus) ===
    18: { c: {x:14, y:50, z:24},  r: {x:14, y:20, z:26} },  // Superior parietal - dorsomedial
    19: { c: {x:42, y:32, z:26},  r: {x:20, y:18, z:22} },  // Inferior parietal - lateral
    20: { c: {x:50, y:20, z:18},  r: {x:14, y:16, z:14} },  // Supramarginal
    21: { c: {x:46, y:26, z:34},  r: {x:14, y:16, z:14} },  // Angular

    // === TEMPORAL LOBE (inferior to Sylvian fissure) ===
    // Horizontal strips on lateral-inferior surface
    22: { c: {x:52, y:1,  z:-4},  r: {x:15, y:12, z:30} },  // Superior temporal
    23: { c: {x:49, y:-10, z:4},  r: {x:16, y:12, z:30} },  // Middle temporal
    24: { c: {x:43, y:-21, z:10}, r: {x:17, y:11, z:28} },  // Inferior temporal

    // === OCCIPITAL LOBE (posterior pole) ===
    25: { c: {x:12, y:34, z:58},  r: {x:12, y:22, z:20} },  // Superior occipital - dorsomedial
    26: { c: {x:28, y:8,  z:56},  r: {x:22, y:20, z:18} },  // Inferior occipital - ventrolateral
  }
};

function _clamp01(x) { return x < 0 ? 0 : (x > 1 ? 1 : x); }

function createRoundPointTexture(THREE, size = 64) {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');
  const r = size * 0.5;
  const grad = ctx.createRadialGradient(r, r, size * 0.08, r, r, r);
  grad.addColorStop(0.0, 'rgba(255,255,255,1.0)');
  grad.addColorStop(0.72, 'rgba(255,255,255,0.98)');
  grad.addColorStop(1.0, 'rgba(255,255,255,0.0)');
  ctx.clearRect(0, 0, size, size);
  ctx.fillStyle = grad;
  ctx.beginPath();
  ctx.arc(r, r, r * 0.94, 0, Math.PI * 2);
  ctx.fill();
  const tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  return tex;
}

function _mapToEllipsoid(u, v, w, center, radii) {
  // Map [0,1]� cube to a filled ellipsoid.
  let px = (u * 2 - 1);
  let py = (v * 2 - 1);
  let pz = (w * 2 - 1);
  const dist = Math.sqrt(px*px + py*py + pz*pz) || 1e-6;
  if (dist > 1.0) { px /= dist; py /= dist; pz /= dist; }
  return {
    x: center.x + px * radii.x,
    y: center.y + py * radii.y,
    z: center.z + pz * radii.z
  };
}

// === HEMISPHERE VOLUME MAPPING ===
// Maps cortical neurons into a continuous filled hemisphere volume.
// u,v,w are [0,1] normalized across the ENTIRE cortex hemisphere.
//
// NO per-gyrus spatial separation � gyri are distinguished by COLOUR only.
// This eliminates all gaps/lines between adjacent gyri.
//
// The hemisphere is a half-ellipsoid with a flat vertical medial wall.
function _mapToHemisphereMantle(u, v, w, gyrusCenter, gyrusRadii, hemi, region) {
  const s = (hemi === 0) ? -1 : +1;

  // Direct mapping: full-hemisphere [0,1] -> hemisphere volume coordinates.
  // We keep the cloud continuous, then apply only a light regional bias so lobes
  // can occupy their canonical zones without reopening visible seams.
  let gx = (u * 2 - 1) * HEMI_RX * s;
  let gy = (v * 2 - 1) * HEMI_RY + 10;
  let gz = (w * 2 - 1) * HEMI_RZ;

  const hCenterY = 10;

  function clampToHemisphere(scaleTo = 0.98) {
    const nx = Math.abs(gx) / (HEMI_RX + 1e-6);
    const ny = (gy - hCenterY) / (HEMI_RY + 1e-6);
    const nz = gz / (HEMI_RZ + 1e-6);
    const ellDist = Math.sqrt(nx * nx + ny * ny + nz * nz);
    if (ellDist > 1.0) {
      const scale = scaleTo / ellDist;
      gx = (nx * scale) * HEMI_RX * s;
      gy = (ny * scale) * HEMI_RY + hCenterY;
      gz = (nz * scale) * HEMI_RZ;
    }
  }

  clampToHemisphere(0.98);

  const targetX = s * Math.max(8.0, Math.min(HEMI_RX * 0.92, Math.abs(gyrusCenter.x)));
  const targetY = Math.max(hCenterY - HEMI_RY * 0.88, Math.min(hCenterY + HEMI_RY * 0.92, gyrusCenter.y));
  const targetZ = Math.max(-HEMI_RZ * 0.95, Math.min(HEMI_RZ * 0.95, gyrusCenter.z));
  let zoneBlend = 0.18;
  if (region >= 15 && region <= 17) zoneBlend = 0.22;
  else if (region >= 18 && region <= 21) zoneBlend = 0.20;
  else if (region >= 22 && region <= 24) zoneBlend = 0.34;
  else if (region >= 25 && region <= 26) zoneBlend = 0.30;

  gx += (targetX - gx) * zoneBlend;
  gy += (targetY - gy) * zoneBlend;
  gz += (targetZ - gz) * zoneBlend;

  if (region >= 22 && region <= 24) {
    gx += (targetX - gx) * 0.10;
    gy += (targetY - gy) * 0.16;
  }

  clampToHemisphere(0.97);

  const medialMin = 3.0;
  if (hemi === 0 && gx > -medialMin) gx = -medialMin;
  if (hemi === 1 && gx < medialMin) gx = medialMin;

  return { x: gx, y: gy, z: gz };
}

function _tubeCenterline(t) {
  const z = (t * 2 - 1) * 20;
  const y = -3.0 * Math.cos(t * Math.PI) + 0.5;
  const x = -8.0 * Math.sin(t * Math.PI) * 0.45;
  return { x, y, z };
}

function _mapToTube(u, v, w, center, sideSign, lenMm, radMm) {
  const t = _clamp01(u);
  const ang = (v * 2.0 * Math.PI);
  const rr = Math.sqrt(_clamp01(w)) * radMm;
  const cl = _tubeCenterline(t);
  const dt = 0.002;
  const cl2 = _tubeCenterline(_clamp01(t + dt));
  let tx = cl2.x - cl.x, ty = cl2.y - cl.y, tz = cl2.z - cl.z;
  let tl = Math.sqrt(tx*tx + ty*ty + tz*tz) || 1.0;
  tx/=tl; ty/=tl; tz/=tl;
  let ux=0, uy=1, uz=0;
  if (Math.abs(ty) > 0.92) { ux=0; uy=0; uz=1; }
  let nx = ty*uz - tz*uy;
  let ny = tz*ux - tx*uz;
  let nz = tx*uy - ty*ux;
  let nl = Math.sqrt(nx*nx + ny*ny + nz*nz) || 1.0;
  nx/=nl; ny/=nl; nz/=nl;
  let bx = ty*nz - tz*ny;
  let by = tz*nx - tx*nz;
  let bz = tx*ny - ty*nx;
  const ox = rr * (Math.cos(ang) * nx + Math.sin(ang) * bx);
  const oy = rr * (Math.cos(ang) * ny + Math.sin(ang) * by);
  const oz = rr * (Math.cos(ang) * nz + Math.sin(ang) * bz);
  const zscale = lenMm / 45.0;
  const lx = cl.x * sideSign;
  const ly = cl.y;
  const lz = cl.z * zscale;
  return { x: center.x + lx + ox, y: center.y + ly + oy, z: center.z + lz + oz };
}


function _mapToCorpusCallosum(u, v, w) {
  // Corpus callosum: biologically accurate flat midline white-matter sheet.
  //
  // Real anatomy (midsagittal):
  //   - Rostrum: thin anterior-inferior hook curving down toward anterior commissure
  //   - Genu: thick rounded anterior bend
  //   - Body (trunk): long thin horizontal section, highest point
  //   - Isthmus: slight narrowing posterior to body
  //   - Splenium: thick bulbous posterior end
  //
  // The CC is a FLAT SHEET that fans out laterally. In axial view it looks like
  // a butterfly � fibers radiate from the midline toward each hemisphere's cortex.
  //
  // u => position along CC from anterior (0) to posterior (1) � the sagittal profile
  // v => medio-lateral fan spread (0 = left edge, 1 = right edge)
  // w => dorso-ventral thickness within the sheet
  const t = _clamp01(u);

  // === SAGITTAL PROFILE (Y-Z plane at midline) ===
  // Z: anterior (-) to posterior (+), total span ~80mm
  const zSpan = 18.5;
  const z = (t * 2.0 - 1.0) * zSpan - 2.0; // slightly anterior, kept central
  const tn = z / zSpan; // normalized -1..+1

  // Y: arch shape with anatomical features
  // Body is at ~Y=12mm above origin (deep inside brain, not at top), genu/splenium curve down
  const archBase = 1.0;
  const archHeight = 2.9;
  const archY = archBase + archHeight * (1.0 - tn * tn); // parabolic arch

  // Rostrum: thin sharp hook curving inferiorly at anterior extreme
  const rostrumT = Math.max(0, -tn - 0.75) * 4.0;
  const rostrumDrop = rostrumT * rostrumT * 12.0;

  // Genu: thick rounded anterior curve, moderate drop
  const genuT = Math.max(0, -tn - 0.45) * 2.2;
  const genuDrop = genuT * genuT * 3.0;

  // Splenium: bulbous posterior end, moderate drop
  const spleniumT = Math.max(0, tn - 0.6) * 2.5;
  const spleniumDrop = spleniumT * spleniumT * 4.5;

  const baseY = archY - rostrumDrop - genuDrop - spleniumDrop;

  // === THICKNESS (dorso-ventral) ===
  // Varies by region: genu thick (~8mm), body thin (~5mm), isthmus thinner,
  // splenium thick (~10mm), rostrum very thin (~2mm)
  const bodyT = 0.84;
  const genuThick = 1.55 * Math.max(0, 1.0 - (tn + 1.0) * 1.2);
  const isthmusNarrow = -0.42 * Math.max(0, 1.0 - Math.abs(tn - 0.4) * 3.0);
  const spleniumThick = 1.85 * Math.max(0, 1.0 - (1.0 - tn) * 1.3);
  const rostrumThin = -0.65 * Math.max(0, 1.0 - (tn + 1.0) * 2.5);
  const thickness = bodyT + genuThick + isthmusNarrow + spleniumThick + rostrumThin;

  // w maps through thickness perpendicular to the arch
  const y = baseY + (w * 2.0 - 1.0) * Math.max(1.0, thickness);

  // === LATERAL FAN (medio-lateral spread) ===
  // The CC is NOT a narrow midline stripe � it's a wide flat sheet.
  // Fibers fan out ~30-35mm from midline. Wider at splenium and genu,
  // narrower at body/isthmus.
  const fanBase = 4.6; // mm half-width at body
  const genuFan = 1.45 * Math.max(0, 1.0 - (tn + 1.0) * 1.5);
  const spleniumFan = 2.05 * Math.max(0, 1.0 - (1.0 - tn) * 1.2);
  const isthmusShrink = -0.95 * Math.max(0, 1.0 - Math.abs(tn - 0.4) * 3.0);
  const fanWidth = fanBase + genuFan + spleniumFan + isthmusShrink;

  const x = (v * 2.0 - 1.0) * fanWidth;

  // User-tuned pass: midline structures scaled up by 100%.
  const midlineScale = 2.0;
  return {
    x: x * midlineScale,
    y: y * midlineScale,
    z: z * midlineScale
  };
}

function _mapToCerebellarLobe(u, v, w, hemi) {
  const s = (hemi === 0) ? -1 : +1;
  const sharedCenter = {
    x: ANATOM.cerebellum.c.x,
    y: ANATOM.cerebellum.c.y - 0.3,
    z: ANATOM.cerebellum.c.z + 0.1
  };
  const hemiCenter = {
    x: sharedCenter.x + s * 5.1,
    y: sharedCenter.y,
    z: sharedCenter.z
  };
  const radii = {
    x: ANATOM.cerebellum.r.x * 0.92,
    y: ANATOM.cerebellum.r.y * 0.82,
    z: ANATOM.cerebellum.r.z * 0.96
  };

  const medialToLateral = _clamp01(u);
  const p = _mapToEllipsoid(0.06 + 0.88 * medialToLateral, v, w, hemiCenter, radii);

  const nx = (s * (p.x - hemiCenter.x)) / (radii.x + 1e-6);
  const ny = (p.y - hemiCenter.y) / (radii.y + 1e-6);
  const nz = (p.z - hemiCenter.z) / (radii.z + 1e-6);

  const medial = 1.0 - _clamp01((nx + 1.0) * 0.5);
  const lateral = _clamp01((nx + 1.0) * 0.5);
  const superior = _clamp01((ny + 1.0) * 0.5);
  const inferior = 1.0 - superior;
  const posterior = _clamp01((nz + 1.0) * 0.5);
  const anterior = 1.0 - posterior;

  // Shared vermis ridge: keep a true midline bridge, but carve a deeper posterior-superior groove.
  const vermisBridge = medial * medial * (0.40 + 0.60 * superior) * (0.35 + 0.65 * posterior);
  p.x -= s * 2.9 * vermisBridge;
  p.y += 0.74 * vermisBridge;
  p.z += 0.26 * vermisBridge;
  const vermisCleft = medial * (0.45 + 0.55 * superior) * (0.35 + 0.65 * posterior);
  p.y -= 0.42 * vermisCleft;
  p.z += 0.08 * vermisCleft;

  // Posterior hemispheres should read as broad, rounded masses rather than narrow fans.
  const posteriorRound = posterior * (0.25 + 0.75 * lateral) * (0.55 + 0.45 * superior);
  p.z += 1.10 * posteriorRound;
  p.y -= 0.16 * posteriorRound;
  p.x += s * 0.10 * posteriorRound;

  // Inferior semilunar / tonsillar fullness for the lower lobe contour.
  const inferiorRound = inferior * (0.25 + 0.75 * lateral) * (0.20 + 0.80 * posterior);
  p.y -= 0.42 * inferiorRound;
  p.z += 0.16 * inferiorRound;

  // Give the lateral lower lobes more of the classic posterior apron.
  const lateralApron = lateral * posterior * (0.35 + 0.65 * inferior);
  p.x += s * 0.48 * lateralApron;
  p.y -= 0.10 * lateralApron;

  // Soften the roof and keep the peduncular side slimmer.
  const superiorTrim = superior * (0.20 + 0.80 * lateral) * anterior;
  p.y -= 0.18 * superiorTrim;
  const peduncleTaper = medial * anterior * (0.25 + 0.75 * superior);
  p.z -= 0.28 * peduncleTaper;
  p.y += 0.06 * peduncleTaper;

  // Scale the finished cerebellar envelope uniformly so we keep the shape but gain volume.
  const cerebellumScale = 1.50;
  p.x = sharedCenter.x + (p.x - sharedCenter.x) * cerebellumScale;
  p.y = sharedCenter.y + (p.y - sharedCenter.y) * cerebellumScale;
  p.z = sharedCenter.z + (p.z - sharedCenter.z) * cerebellumScale;

  // Meet at the vermis without crossing through each other.
  const midlineInset = 0.18;
  if (s < 0 && p.x > -midlineInset) p.x = -midlineInset;
  if (s > 0 && p.x < midlineInset) p.x = midlineInset;

  return p;
}
function anatomicalPlacePoint(pBase, bounds, region, hemi) {
  if (!bounds) return pBase;
  const dx = (bounds.maxX - bounds.minX) || 1e-6;
  const dy = (bounds.maxY - bounds.minY) || 1e-6;
  const dz = (bounds.maxZ - bounds.minZ) || 1e-6;
  const u = _clamp01((pBase.x - bounds.minX) / dx);
  const v = _clamp01((pBase.y - bounds.minY) / dy);
  const ww = _clamp01((pBase.z - bounds.minZ) / dz);

  // Midline structures (no hemisphere sign flip)
  if (region === 1) {
    const h1 = Math.abs(Math.sin(pBase.x * 22.671 + pBase.y * 56.339 + pBase.z * 81.772)) % 1.0;
    const h2 = Math.abs(Math.sin(pBase.x * 68.213 + pBase.y * 33.891 + pBase.z * 47.556)) % 1.0;
    const h3 = Math.abs(Math.sin(pBase.x * 51.447 + pBase.y * 14.668 + pBase.z * 92.113)) % 1.0;
    return _mapToEllipsoid(h1, h2, h3, ANATOM.thalamus.c, ANATOM.thalamus.r);
  }
  if (region === 2) return _mapToEllipsoid(u, v, ww, ANATOM.hypothalamus.c, ANATOM.hypothalamus.r);
  if (region === 14) return _mapToCorpusCallosum(u, v, ww);
  if (region === 7) {
    const h1 = Math.abs(Math.sin(pBase.x * 31.892 + pBase.y * 74.553 + pBase.z * 18.446)) % 1.0;
    const h2 = Math.abs(Math.sin(pBase.x * 59.117 + pBase.y * 42.773 + pBase.z * 86.331)) % 1.0;
    const h3 = Math.abs(Math.sin(pBase.x * 44.556 + pBase.y * 27.118 + pBase.z * 63.889)) % 1.0;
    return _mapToEllipsoid(h1, h2, h3, ANATOM.brainstem.c, ANATOM.brainstem.r);
  }
  if (region === 8) {
    const h1 = Math.abs(Math.sin(pBase.x * 26.334 + pBase.y * 83.112 + pBase.z * 54.778)) % 1.0;
    const h2 = Math.abs(Math.sin(pBase.x * 71.889 + pBase.y * 17.443 + pBase.z * 39.661)) % 1.0;
    const h3 = Math.abs(Math.sin(pBase.x * 58.221 + pBase.y * 45.667 + pBase.z * 72.113)) % 1.0;
    return _mapToEllipsoid(h1, h2, h3, ANATOM.pons.c, ANATOM.pons.r);
  }
  
  // Cerebellum: bilateral lobes with a superior vermis cleft and fuller posterior contour.
  if (region === 27) {
    return _mapToCerebellarLobe(u, v, ww, hemi);
  }
  
  // Bilateral subcortical structures: flip X by hemisphere
  if (region === 5) {
    const s = (hemi === 0) ? -1 : +1;
    const c = { x: ANATOM.hippocampus.c.x * s, y: ANATOM.hippocampus.c.y, z: ANATOM.hippocampus.c.z };
    // Use pBase coordinates to generate well-spread u,v,w
    // Hash the base position to distribute voxels evenly through the tube volume
    const hash1 = Math.abs(Math.sin(pBase.x * 12.9898 + pBase.y * 78.233 + pBase.z * 45.164)) % 1.0;
    const hash2 = Math.abs(Math.sin(pBase.x * 63.7376 + pBase.y * 15.059 + pBase.z * 91.334)) % 1.0;
    const hash3 = Math.abs(Math.sin(pBase.x * 36.173 + pBase.y * 49.921 + pBase.z * 27.816)) % 1.0;
    return _mapToTube(hash1, hash2, hash3, c, s, ANATOM.hippocampus.len, ANATOM.hippocampus.rad);
  }
  if (region === 4) {
    const s = (hemi === 0) ? -1 : +1;
    const c = { x: ANATOM.amygdala.c.x * s, y: ANATOM.amygdala.c.y, z: ANATOM.amygdala.c.z };
    const h1 = Math.abs(Math.sin(pBase.x * 17.231 + pBase.y * 82.119 + pBase.z * 39.771)) % 1.0;
    const h2 = Math.abs(Math.sin(pBase.x * 55.432 + pBase.y * 23.897 + pBase.z * 67.142)) % 1.0;
    const h3 = Math.abs(Math.sin(pBase.x * 41.889 + pBase.y * 71.336 + pBase.z * 13.557)) % 1.0;
    return _mapToEllipsoid(h1, h2, h3, c, ANATOM.amygdala.r);
  }
  if (region === 3) {
    const s = (hemi === 0) ? -1 : +1;
    const c = { x: ANATOM.basalGanglia.c.x * s, y: ANATOM.basalGanglia.c.y, z: ANATOM.basalGanglia.c.z };
    const h1 = Math.abs(Math.sin(pBase.x * 29.443 + pBase.y * 61.778 + pBase.z * 84.216)) % 1.0;
    const h2 = Math.abs(Math.sin(pBase.x * 73.156 + pBase.y * 38.442 + pBase.z * 52.891)) % 1.0;
    const h3 = Math.abs(Math.sin(pBase.x * 47.623 + pBase.y * 19.887 + pBase.z * 96.334)) % 1.0;
    return _mapToEllipsoid(h1, h2, h3, c, ANATOM.basalGanglia.r);
  }
  
  // === CORTEX: map onto continuous hemisphere mantle surface ===
  // All cortical gyri map onto the same continuous hemisphere surface.
  // The medial wall is flat/vertical, lateral surface is convex.
  // Each gyrus occupies its biological zone on the surface.
  if (isCorticalRegion(region) && ANATOM.gyri[region]) {
    const gyrus = ANATOM.gyri[region];
    return _mapToHemisphereMantle(u, v, ww, gyrus.c, gyrus.r, hemi, region);
  }
  
  // Fallback: unknown cortical region ? lateral cortex
  if (isCorticalRegion(region)) {
    const gc = { x: 40, y: 25, z: 0 };
    const gr = { x: 20, y: 20, z: 30 };
    return _mapToHemisphereMantle(u, v, ww, gc, gr, hemi, region);
  }
  
  return pBase;
}

  

  function createAnatomyShellGeometry(THREE, sideSign) {
    const latSteps = 28;
    const apSteps = 44;
    const positions = [];
    const indices = [];

    for (let i = 0; i <= latSteps; i++) {
      const u = i / latSteps;
      const theta = (Math.PI * 0.5) * u;
      const lateral = Math.sin(theta);
      for (let j = 0; j <= apSteps; j++) {
        const v = j / apSteps;
        const phi = (v - 0.5) * Math.PI;
        const cp = Math.cos(phi);
        const sp = Math.sin(phi);

        const frontalLift = 1.0 + 0.06 * Math.max(0, -sp);
        const occipitalRound = 1.0 + 0.05 * Math.max(0, sp);
        const occipitalTaper = 1.0 - 0.08 * Math.pow(Math.max(0, sp), 1.2);
        const temporalBulge = 1.0 + 0.20 * Math.pow(Math.max(0, -cp), 1.8);
        const dorsalFlatten = 1.0 - 0.12 * Math.pow(Math.max(0, cp), 4.0);
        const inferiorDrop = 1.0 + 0.15 * Math.pow(Math.max(0, -cp), 1.5);
        const superiorRound = 1.0 - 0.05 * Math.pow(Math.max(0, cp), 1.4);

        const xMedialBias = 0.10 + 0.90 * Math.pow(lateral, 0.86);
        const x = sideSign * xMedialBias;
        const y = (cp >= 0 ? cp * superiorRound * dorsalFlatten : cp * inferiorDrop * temporalBulge) * (0.88 + 0.12 * lateral);
        const z = sp * frontalLift * occipitalRound * occipitalTaper * (0.94 + 0.10 * lateral);

        positions.push(x, y, z);
      }
    }

    for (let i = 0; i < latSteps; i++) {
      for (let j = 0; j < apSteps; j++) {
        const a = i * (apSteps + 1) + j;
        const b = a + apSteps + 1;
        const c = b + 1;
        const d = a + 1;
        indices.push(a, b, d);
        indices.push(b, c, d);
      }
    }

    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geo.setIndex(indices);
    geo.computeVertexNormals();
    return geo;
  }

  function createShellMeshes(THREE) {
    if (!scene || shellLeft || shellRight) return;
    const geoLeft = createAnatomyShellGeometry(THREE, -1);
    const geoRight = createAnatomyShellGeometry(THREE, 1);
    const mkMat = (hex) => new THREE.MeshBasicMaterial({
      color: hex,
      transparent: true,
      opacity: 0.07,
      depthWrite: false,
      side: THREE.DoubleSide
    });
    shellLeft = new THREE.Mesh(geoLeft, mkMat(0x6f89a6));
    shellRight = new THREE.Mesh(geoRight, mkMat(0x6f89a6));
    shellLeft.visible = shellVisible;
    shellRight.visible = shellVisible;
    scene.add(shellLeft);
    scene.add(shellRight);
  }

  function _computeGeomBounds1(geom) {
    if (!geom || !geom.getAttribute) return null;
    const attr = geom.getAttribute('position');
    if (!attr || !attr.array || attr.array.length < 3) return null;
    const arr = attr.array;
    let minX = Infinity, minY = Infinity, minZ = Infinity;
    let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;
    for (let i = 0; i < arr.length; i += 3) {
      const x = arr[i], y = arr[i+1], z = arr[i+2];
      if (x < minX) minX = x; if (x > maxX) maxX = x;
      if (y < minY) minY = y; if (y > maxY) maxY = y;
      if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
    }
    return { minX, maxX, minY, maxY, minZ, maxZ };
  }

  function updateShellMeshes() {
    if (!shellLeft || !shellRight) return;
    const lb = _computeGeomBounds1(baseGeomL);
    const rb = _computeGeomBounds1(baseGeomR);
    const updateOne = (mesh, b, isRight) => {
      if (!mesh || !b) return;
      const width = Math.max(20, b.maxX - b.minX);
      const height = Math.max(20, b.maxY - b.minY);
      const depth = Math.max(20, b.maxZ - b.minZ);
      const sx = width * 0.61;
      const sy = height * 0.59;
      const sz = depth * 0.62;
      const cx = (b.minX + b.maxX) * 0.5 + (isRight ? 1 : -1) * Math.max(1.4, width * 0.025);
      const cy = (b.minY + b.maxY) * 0.5 + height * 0.018;
      const cz = (b.minZ + b.maxZ) * 0.5 - depth * 0.015;
      mesh.position.set(cx, cy, cz);
      mesh.scale.set(sx, sy, sz);
      mesh.visible = shellVisible;
    };
    updateOne(shellLeft, lb, false);
    updateOne(shellRight, rb, true);
  }

  function applyVisualStyle() {
    const THREE = window.THREE;
    const mode = (renderMode || 'activity').toLowerCase();
    let connOpacity = 0.16, baseOpacity = 0.40, spikeSize = 1.55, spikeOpacity = 0.98, shellOpacity = 0.07;
    let baseSize = 1.8, ccOpacity = 0.30, midOpacity = 0.48, densOpacity = 0.10, densSize = 2.9;
    let fogDensity = 0.00026;
    let outlineWanted = false;

    if (mode === 'anatomy') {
      connOpacity = 0.10; baseOpacity = 0.64; baseSize = 1.95; spikeSize = 1.20; spikeOpacity = 0.90; shellOpacity = 0.09; densOpacity = 0.11; densSize = 3.2; fogDensity = 0.00018;
    } else if (mode === 'connectivity') {
      connOpacity = 0.28; baseOpacity = 0.20; baseSize = 1.85; spikeSize = 1.35; spikeOpacity = 0.95; shellOpacity = 0.04; ccOpacity = 0.38; densOpacity = 0.08; densSize = 3.0; fogDensity = 0.00015;
    } else if (mode === 'validation') {
      connOpacity = 0.22; baseOpacity = 0.38; baseSize = 1.90; spikeSize = 1.40; spikeOpacity = 0.95; shellOpacity = 0.12; ccOpacity = 0.42; midOpacity = 0.58; densOpacity = 0.12; densSize = 3.1; outlineWanted = true; fogDensity = 0.00016;
    } else {
      connOpacity = 0.18; baseOpacity = 0.34; baseSize = 1.90; spikeSize = 1.85; spikeOpacity = 0.98; shellOpacity = 0.065; ccOpacity = 0.34; densOpacity = 0.11; densSize = 3.0; fogDensity = 0.00024;
    }

    if (connMat) { connMat.opacity = connOpacity; }
    if (baseMatL) { baseMatL.opacity = baseOpacity; baseMatL.size = baseSize; }
    if (baseMatR) { baseMatR.opacity = baseOpacity; baseMatR.size = baseSize; }
    if (midlineMat) { midlineMat.opacity = midOpacity; midlineMat.size = 1.65; }
    if (ccMat) { ccMat.opacity = ccOpacity; ccMat.size = (mode === 'validation') ? 1.4 : 1.2; }
    if (densMatL) { densMatL.opacity = densOpacity; densMatL.size = densSize; }
    if (densMatR) { densMatR.opacity = densOpacity; densMatR.size = densSize; }

    if (matL) {
      matL.size = spikeSize; matL.opacity = spikeOpacity; matL.depthWrite = false;
      if (THREE) matL.blending = THREE.AdditiveBlending;
    }
    if (matR) {
      matR.size = spikeSize; matR.opacity = spikeOpacity; matR.depthWrite = false;
      if (THREE) matR.blending = THREE.AdditiveBlending;
    }

    if (shellLeft && shellLeft.material) { shellLeft.material.opacity = shellOpacity; shellLeft.visible = shellVisible; }
    if (shellRight && shellRight.material) { shellRight.material.opacity = shellOpacity; shellRight.visible = shellVisible; }

    if (scene && THREE) {
      scene.fog = depthFogEnabled ? new THREE.FogExp2(0x0b1020, fogDensity) : null;
    }

    if (outlineWanted && !showOutline && THREE) {
      showOutline = true;
      createBrainOutline(THREE);
      if (cachedLayoutData) setLayout(cachedLayoutData);
    } else if (!outlineWanted && showOutline) {
      showOutline = false;
      for (const m of brainMeshes) { if (m && m.parent) m.parent.remove(m); }
      brainMeshes = [];
      outlineRefs = { leftSag: null, rightSag: null, coronal: [], axial: null, fissure: null, _segs: { sag: 48, cor: 36, axial: 90, fiss: 34 } };
    }
  }

  function setRenderMode(mode) {
    renderMode = (mode || 'activity').toLowerCase();
    applyVisualStyle();
  }

  function setShellVisible(v) {
    shellVisible = !!v;
    if (shellLeft) shellLeft.visible = shellVisible;
    if (shellRight) shellRight.visible = shellVisible;
  }

  function setFibrePulseEnabled(v) {
    fibrePulseEnabled = !!v;
  }

  // Cache the last layout data so we can refresh after mode changes
  let cachedLayoutData = null;
  let cachedConnectionData = null;

  function refreshCachedScene(forceConnections = true) {
    if (cachedLayoutData) setLayout(cachedLayoutData);
    if (cachedConnectionData) setConnections(cachedConnectionData, forceConnections);
  }
  
  // Toggle anatomical mode
  function setAnatomicalMode(enabled) {
    anatomicalMode = enabled;
    if (enabled) {
      // Anatomical silhouette ON, artificial folds OFF
      brainWarpEnabled = true;
      warpStrength = 1.00;
      gyrifyEnabled = false;
      medialFlattenEnabled = false;  // Not used in clean mapPoint
      fitToHemisphereEnabled = false;  // Not used in clean mapPoint
      jitter = 0;
      // Keep physical proportions close to biological (mm ratios captured in canon).
      scaleX = 1.75 * (140.0 / 167.0);
      scaleY = 1.75 * (93.0 / 167.0);
      scaleZ = 1.75;

    } else {
      // Non-anatomical: some visual enhancements
      brainWarpEnabled = true;
      gyrifyEnabled = false;
      medialFlattenEnabled = false;
      fitToHemisphereEnabled = false;
      jitter = 0;
      scaleX = 1.75 * (140.0 / 167.0);
      scaleY = 1.75 * (93.0 / 167.0);
      scaleZ = 1.75;
    }
    // Refresh display if we have cached data
    refreshCachedScene(true);
  }
  
  // Manual refresh function
  function refresh() {
    refreshCachedScene(true);
  }

  function smoothstep(a, b, x) {
    const t = clamp01((x - a) / (b - a));
    return t * t * (3 - 2 * t);
  }

  function brainWarp(hemi, wx, wy, wz, baseXWorld, region) {
    if (!brainWarpEnabled || warpStrength <= 0) return { x: wx, y: wy, z: wz };

    // BIOLOGICAL BRAIN SHAPE � Lateral profile:
    //
    // Coordinate system:  nz < 0 = anterior,  nz > 0 = posterior
    //                     ny > 0 = superior,  ny < 0 = inferior
    //
    // LATERAL VIEW (key anatomical features):
    //   - PFC: flat inferior shelf with wide V notch (orbital fossa)
    //   - Temporal lobe: drops BELOW the frontal shelf by half temporal height,
    //     continues at this lower level toward posterior
    //   - Posterior (occipital): rounder than frontal, no sharp shelf
    //   - Superior: domed, narrowing in X toward vertex
    //
    // SUPERIOR VIEW: Egg-shaped, widest at parietal, frontal slightly narrower,
    //   occipital tapers gently.

    const rx = (w * 0.5) * spacing * 0.5;
    const ry = (h * 0.5) * spacing;
    const rz = (d * 0.5) * spacing;

    let lx = (wx - baseXWorld) / scaleX;
    let ly = wy / scaleY;
    let lz = wz / scaleZ;

    const nx = lx / (rx + 1e-6);
    const ny = ly / (ry + 1e-6);
    const nz = lz / (rz + 1e-6);
    const lat = clamp01(Math.abs(nx));
    const isTemporal = (region >= 22 && region <= 24);
    const isOccipital = (region >= 25 && region <= 26);
    const isFrontal = (region >= 15 && region <= 17);

    // (1) FRONTAL narrowing: frontal pole slightly narrower than parietal
    const anterior = smoothstep(0.0, 0.9, -nz);
    lx *= (1.0 - 0.10 * anterior * (isFrontal ? 1.0 : 0.6) * warpStrength);

    // (2) OCCIPITAL: keep the posterior broad and rounded instead of pinched.
    const posterior = smoothstep(0.30, 0.98, nz);
    lx *= (1.0 - 0.05 * posterior * warpStrength);

    // (3) PFC SHELF: flat inferior base in the frontal region only
    const frontalInferior = smoothstep(-0.15, -0.55, ny) * smoothstep(-0.1, -0.7, nz) * (isFrontal ? 1.0 : 0.55);
    const shelfTarget = -0.42 * ry;
    ly = ly * (1.0 - 0.5 * frontalInferior * warpStrength)
       + shelfTarget * (0.5 * frontalInferior * warpStrength);

    // (4) PFC V-NOTCH: V-shaped indentation spanning the full width of the frontal shelf
    //     Reaches from left to right (no lateral cutoff), deepest at midline
    const vNotchAP = smoothstep(-0.1, -0.7, nz);
    const vNotchInf = smoothstep(-0.20, -0.55, ny);
    // V shape: deepest at midline, linearly shallower toward lateral edges
    // Reaches all the way to left and right (no hard cutoff)
    const vNotchV = 1.0 - 0.6 * lat;  // 1.0 at midline, 0.4 at full lateral
    const vNotchDepth = vNotchAP * vNotchInf * Math.max(0, vNotchV) * 0.22 * ry * warpStrength;
    ly -= vNotchDepth;

    // (5) TEMPORAL LOBE: drops below the PFC shelf, rounded outer-inferior edge.
    //     The temporal lobe is LOWER than the frontal shelf.
    //     The outer (lateral) inferior edge is rounded � X narrows as Y drops,
    //     creating a smooth curve instead of a sharp corner.
    const temporalAP = smoothstep(-0.42, 0.08, nz) * (1.0 - smoothstep(0.26, 0.48, nz));
    const temporalInf = smoothstep(-0.08, -0.58, ny);
    const temporalDrop = temporalAP * temporalInf * (isTemporal ? 1.0 : 0.12);
    ly -= 0.42 * ry * temporalDrop * warpStrength;

    const y01 = clamp01((ny + 1.0) * 0.5);  // 0=inferior, 1=superior
    const outerEdge = smoothstep(0.24, 0.94, lat);
    const lowerTemporalBand = smoothstep(0.00, 0.48, y01) * (1.0 - smoothstep(0.56, 0.74, y01));
    const temporalMass = temporalAP * outerEdge * lowerTemporalBand * (isTemporal ? 1.0 : 0.10);
    const outerTemporalDrop = outerEdge * smoothstep(0.00, 0.50, y01) * (1.0 - smoothstep(0.54, 0.76, y01)) * (isTemporal ? 1.0 : 0.0);
    const innerTemporalEdge = (1.0 - smoothstep(0.18, 0.56, lat)) * lowerTemporalBand * (isTemporal ? 1.0 : 0.0);
    const temporalUCurve = (1.0 - smoothstep(0.16, 0.44, lat)) * smoothstep(0.00, 0.36, y01) * (1.0 - smoothstep(0.38, 0.58, y01)) * (isTemporal ? 1.0 : 0.0);
    ly -= 0.26 * ry * temporalMass * warpStrength;
    ly -= 0.30 * ry * outerTemporalDrop * warpStrength;
    ly += 0.36 * ry * innerTemporalEdge * warpStrength;
    ly += 0.30 * ry * temporalUCurve * warpStrength;
    lx *= (1.0 + 0.10 * temporalMass * warpStrength);
    lx *= (1.0 - 0.10 * innerTemporalEdge * warpStrength);

    // Rounded outer-inferior edge: the more inferior, the more X is compressed.
    const infAmount = clamp01(-ny);  // 0 at equator, 1 at max inferior
    const roundingCurve = infAmount * infAmount;
    const temporalRounding = temporalAP * roundingCurve * (isTemporal ? 0.22 : 0.05) * warpStrength;
    lx *= (1.0 - temporalRounding);

    // (6) POSTERIOR ROUNDING: occipital region is rounder, with a subtle posterior bulge.
    ly *= (1.0 - 0.06 * posterior * (isOccipital ? 1.0 : 0.6) * warpStrength);
    lx *= (1.0 + 0.08 * posterior * (isOccipital ? 1.0 : 0.45) * warpStrength);
    const occipitalTail = posterior
      * smoothstep(0.18, 0.86, lat)
      * smoothstep(0.04, 0.72, y01)
      * (1.0 - smoothstep(0.78, 0.98, y01))
      * (isOccipital ? 1.0 : 0.0);
    ly -= 0.22 * ry * occipitalTail * warpStrength;
    lx *= (1.0 - 0.05 * occipitalTail * warpStrength);

    // (7) FLAT BASE: general flat inferior (posterior cranial fossa)
    //     Only for non-frontal regions (frontal has its own shelf)
    const nonFrontal = 1.0 - smoothstep(0.0, -0.5, nz);
    if (ny < -0.35) {
      const t = smoothstep(-0.35, -0.85, ny) * nonFrontal;
      const baseTarget = -0.75 * ry;
      ly = ly * (1.0 - 0.40 * t * warpStrength) + baseTarget * (0.40 * t * warpStrength);
      if (isOccipital) ly -= 0.08 * ry * t * warpStrength;
    }

    // (8) SUPERIOR DOME: rounded X narrowing for dome silhouette
    //     Quadratic curve: gentle near equator, steeper near vertex.
    //     From anterior view the brain looks like a smooth dome, not a triangle.
    const superior01 = clamp01(ny);  // 0 at equator, 1 at vertex
    const domeRound = superior01 * superior01;  // quadratic = rounded dome
    lx *= (1.0 - 0.58 * domeRound * warpStrength);

    // (9) MIDLINE: subtle X compression at superior midline
    const midline = 1.0 - smoothstep(0.08, 0.28, lat);
    lx *= (1.0 - 0.04 * midline * domeRound * warpStrength);

    return { x: baseXWorld + (lx * scaleX), y: (ly * scaleY), z: (lz * scaleZ) };
  }

  // Option B: project points back inside a hemisphere envelope after warps.
  // This prevents gyrification/warp from pushing points outside the intended shape.
  function fitToHemisphere(hemi, wx, wy, wz, baseXWorld) {
    if (!fitToHemisphereEnabled) return { x: wx, y: wy, z: wz };

    // Unscale so fitExponent behaves consistently regardless of axis scaling.
    let lx = (wx - baseXWorld) / scaleX;
    let ly = wy / scaleY;
    let lz = wz / scaleZ;

    // Radii in unscaled lattice-world units.
    // X is half-extent because of sagittal split.
    const rx = (w * 0.5) * spacing * 0.5;
    const ry = (h * 0.5) * spacing;
    const rz = (d * 0.5) * spacing;

    const p = Math.max(1.6, Math.min(4.0, fitExponent || 2.35));

    const ax = Math.abs(lx) / (rx + 1e-6);
    const ay = Math.abs(ly) / (ry + 1e-6);
    const az = Math.abs(lz) / (rz + 1e-6);

    const m = Math.pow(Math.pow(ax, p) + Math.pow(ay, p) + Math.pow(az, p), 1.0 / p);
    if (m > 1.0) {
      const s = 1.0 / (m + 1e-6);
      lx *= s; ly *= s; lz *= s;
    }

    // MIDLINE HANDLING (Folded Archive):
    // - Keep the cortical "cut" surface at +/- midlineGap so each hemisphere reads as a half-volume.
    // - Allow deep nuclei to approach the midline (so they don't look like flattened walls).
    // - Never clamp the corpus callosum (region 14), so the tract can occupy true midline space.
    const xWorldUnscaled = (baseXWorld / scaleX) + lx;

    const isCortex = (region === 11 || region === 12 || (region >= 15 && region <= 26));
    const isMidlineTract = (region === 14);
    const isSubcortex = ((region >= 1 && region <= 8) || region === 27);

    if (!isMidlineTract) {
      const gapMul = isCortex ? 1.0 : (isSubcortex ? 0.35 : 1.0);
      const medialPlane = (hemi === 0) ? (-midlineGap * gapMul) : (+midlineGap * gapMul);

      if (hemi === 0 && xWorldUnscaled > medialPlane) lx -= (xWorldUnscaled - medialPlane);
      if (hemi === 1 && xWorldUnscaled < medialPlane) lx += (medialPlane - xWorldUnscaled);
    }


    return { x: baseXWorld + (lx * scaleX), y: (ly * scaleY), z: (lz * scaleZ) };
  }

  function isCorticalRegion(region) {
    // Canon (Paul): cortex is parcellated into gyri circuits.
    // 11-12: pre/postcentral. 15-26: frontal/parietal/temporal/occipital gyri.
    return region === 11 || region === 12 || (region >= 15 && region <= 26);
  }
  
  function isSubcorticalRegion(region) {
    // Regions 1-8: Thalamus, Hypothalamus, Basal Ganglia, Amygdala, Hippocampus, Cerebellum, Brainstem, Pons
    return (region >= 1 && region <= 8) || region === 27;
  }
  
  // === CONNECTION FILTERING ===
  // Region IDs: 1=Thalamus, 2=Hypothalamus, 3=BasalGanglia, 4=Amygdala, 5=Hippocampus,
  //             27=Cerebellum, 7=Brainstem, 8=Pons, 11-12,15-26=Cortical gyri, 14=CorpusCallosum
  
  // Filter modes
  let connectionFilterMode = 'all';  // 'all', 'none', 'regions', 'local', 'callosal', 'deep'
  let connectionFilterRegions = new Set();  // Set of region IDs to show connections for
  
  // Set connection filter mode
  function setConnectionFilter(mode, regions = []) {
    connectionFilterMode = mode;
    connectionFilterRegions = new Set(regions);
    // Refresh connections
    if (cachedConnectionData) {
      connectionsLoaded = false;  // Force rebuild
      setConnections(cachedConnectionData, true);
    }
  }
  
  // Preset filters
  function showAllConnections() {
    setConnectionFilter('all');
  }
  
  function hideAllConnections() {
    setConnectionFilter('none');
  }
  
  function showKeyPathwayConnections() {
    // Show connections to/from: Corpus Callosum (14), Thalamus (1), Hypothalamus (2), Hippocampus (5), Pons (8), Cerebellum (27).
    // Corpus callosum is detected as inter-hemispheric cortical?cortical edges.
    setConnectionFilter('regions', [14, 1, 2, 5, 8, 27]);
  }
  
  function showThalamicConnections() {
    setConnectionFilter('regions', [1]);
  }
  
  function showCallosalConnections() {
    setConnectionFilter('regions', [14]);
  }
  
  function showCerebellarConnections() {
    setConnectionFilter('regions', [27]);
  }
  
  function showPontineConnections() {
    setConnectionFilter('regions', [8]);
  }
  
  // Check if a connection should be displayed based on filtering rules
  // Paul: In Live view we only display connections for:
  //   Thalamus(1), Hypothalamus(2), Hippocampus(5), Cerebellum(27), Pons(8), and Corpus Callosum pathway (inter-hemispheric cortical?cortical).
  // The CC is not a "region node" in the lattice, so we detect callosal edges by hemisphere mismatch.
  const forcedConnRegions = new Set([1, 2, 5, 8, 27, 14]);
  function isCallosalEdge(h1, h2, r1, r2) {
    if (r1 === 14 || r2 === 14) return true;
    return (h1 !== h2) && isCorticalRegion(r1) && isCorticalRegion(r2);
  }
  
  function shouldShowConnection(h1, h2, region1, region2) {
    const r1 = (region1 || 0);
    const r2 = (region2 || 0);
    const callosal = isCallosalEdge(h1, h2, r1, r2);

    // Modes:
    //  - 'none'    : show nothing
    //  - 'all'     : show key biological pathways (callosal + deep nuclei links)
    //  - 'regions' : show only connections touching selected regions (CC via region 14)
    //  - 'local'   : show intra-hemisphere cortico-cortical (no deep, no callosal)
    //  - 'callosal': show only cross-hemisphere cortico-cortical
    //  - 'deep'    : show only cortex<->deep (and deep<->deep) key pathways, no callosal

    if (connectionFilterMode === 'none') return false;

    if (connectionFilterMode === 'regions') {
      if (callosal) return connectionFilterRegions.has(14);
      return connectionFilterRegions.has(r1) || connectionFilterRegions.has(r2);
    }

    if (connectionFilterMode === 'local') {
      if (callosal) return false;
      if (h1 !== h2) return false;
      return isCorticalRegion(r1) && isCorticalRegion(r2);
    }

    if (connectionFilterMode === 'callosal') {
      return callosal;
    }

    if (connectionFilterMode === 'deep') {
      if (callosal) return false;
      // any connection touching a key deep nucleus, optionally cortex<->deep
      if (forcedConnRegions.has(r1) || forcedConnRegions.has(r2)) return true;
      return false;
    }

    // Default ('all') � constrained to key pathways to avoid overdraw.
    if (callosal) return true;
    if (forcedConnRegions.has(r1) || forcedConnRegions.has(r2)) return true;
    return false;
  }

function clamp01(v) {
    return v < 0 ? 0 : (v > 1 ? 1 : v);
  }

  function foldField(hemi, x, y, z) {
    // When mirrorRight is enabled we pass seedH=0 for BOTH hemispheres, so fold phases match.
    // Use x centered around the per-hemisphere voxel center so mirroring around the sagittal plane
    // produces a true mirrored fold pattern (rather than a translated copy).
    const cx = (w - 1) * 0.5;
    const ux = x - cx;

    // fixed phases per hemisphere (or seed) for determinism + continuity
    const p1 = (hemi === 0 ? 0.7 : 1.9);
    const p2 = (hemi === 0 ? 2.1 : 0.4);
    const p3 = (hemi === 0 ? 1.3 : 2.7);

    const s1 = Math.sin((ux * gyrifyFreq1) + (y * gyrifyFreq1 * 0.83) + (z * gyrifyFreq1 * 1.11) + p1);
    const s2 = Math.sin((ux * gyrifyFreq2 * 0.91) + (y * gyrifyFreq2 * 1.07) + (z * gyrifyFreq2 * 0.76) + p2);
    const s3 = Math.sin((ux * gyrifyFreq3 * 1.03) + (y * gyrifyFreq3 * 0.72) + (z * gyrifyFreq3 * 1.18) + p3);

    // weighted sum -> [-~1.75, ~1.75]
    let n = (1.0 * s1) + (0.6 * s2) + (0.35 * s3);
    // normalize roughly to [-1,1]
    n = n / 1.95;

    // ridge-like shaping (folds, not pure noise)
    const ridge = Math.sin(n * Math.PI); // [-1,1]
    return ridge;
  }

  function gyrifyDisplacement(hemi, x, y, z, region, wx, wy, wz, baseXWorld) {
    if (!gyrifyEnabled || !isCorticalRegion(region)) return { gx: 0, gy: 0, gz: 0 };

    const xm = hemiVoxelX(hemi, x);
    const seedH = mirrorRight ? 0 : hemi;

    // Estimate "surface-ness" (cortical mantle near outer envelope) using normalized radius.
    // Use voxel-space distance from hemisphere center.
    const cx = (w - 1) * 0.5;
    const cy = (h - 1) * 0.5;
    const cz = (d - 1) * 0.5;

    // hemi radii estimate (visual only)
    const rx = w * 0.46;
    const ry = h * 0.47;
    const rz = d * 0.50;

    const dx = (xm - cx) / rx;
    const dy = (y - cy) / ry;
    const dz = (z - cz) / rz;
    const r = Math.sqrt(dx*dx + dy*dy + dz*dz);

    // Strongest at the outer 25% of the mantle
    const surf = clamp01((r - 0.72) / 0.28);

    const amp = gyrifyAmpVox * spacing * (0.25 + 0.75 * surf);

    // outward normal in world-space relative to hemisphere center
    const vx = wx - baseXWorld;
    const vy = wy - 0.0;
    const vz = wz - 0.0;
    const len = Math.sqrt(vx*vx + vy*vy + vz*vz) + 1e-6;

    const nx = vx / len;
    const ny = vy / len;
    const nz = vz / len;

    const f = foldField(seedH, xm, y, z);

    return { gx: nx * amp * f, gy: ny * amp * f, gz: nz * amp * f };
  }

  let regionOffsets = {};

  // Midline (single-instance) regions.
  // Region 14 (Corpus Callosum) is the major midline white matter tract.
  // NOTE: Hippocampus (5) is placed bilaterally per hemisphere by the engine,
  // so it is NOT midline for rendering purposes (even though it's deep/medial).
  function isMidlineRegion(region) {
    return region === 1 || region === 2 || region === 7 || region === 8 || region === 14;
  }

  // Saturated region colors (deep nuclei + cortical gyri).
  // PRE-ALLOCATED: each color array is created once; regionColor returns a reference.
  // Callers must NOT mutate the returned array.
  const _RC = {
    1:  [0.95, 0.75, 0.25], // thalamus - gold
    2:  [0.85, 0.55, 0.30], // hypothalamus - brown-orange
    3:  [0.50, 0.50, 0.85], // basal ganglia - blue-violet
    4:  [0.95, 0.25, 0.25], // amygdala - red
    5:  [0.25, 0.90, 0.45], // hippocampus - green
    27: [0.70, 0.45, 0.90], // cerebellum - purple
    7:  [0.45, 0.65, 0.90], // brainstem - sky blue
    8:  [0.50, 0.85, 0.85], // pons - cyan
    14: [0.35, 0.35, 0.40], // corpus callosum - dim grey (lights up white on spikes)
    11: [0.92, 0.45, 0.18], // precentral (M1) - orange
    12: [0.88, 0.82, 0.26], // postcentral (S1) - yellow
    15: [0.95, 0.68, 0.30], // superior frontal
    16: [0.92, 0.58, 0.24], // middle frontal
    17: [0.88, 0.48, 0.20], // inferior frontal
    18: [0.30, 0.85, 0.90], // superior parietal
    19: [0.22, 0.75, 0.86], // inferior parietal
    20: [0.70, 0.35, 0.90], // supramarginal
    21: [0.88, 0.30, 0.78], // angular
    22: [0.25, 0.82, 0.45], // superior temporal
    23: [0.18, 0.72, 0.40], // middle temporal
    24: [0.12, 0.62, 0.35], // inferior temporal
    25: [0.25, 0.55, 0.92], // superior occipital
    26: [0.15, 0.42, 0.85], // inferior occipital
  };
  const _RC_DEFAULT = [0.40, 0.40, 0.40];
  function regionColor(region) {
    return _RC[region] || _RC_DEFAULT;
  }
  
  // Apply hemisphere-specific color tinting
  // Left (hemi=0): Logical - cooler, blue-shifted tones
  // Right (hemi=1): Creative - warmer, orange-shifted tones
  function applyHemisphereTint(color, hemi, region) {
    const [r, g, b] = color;
    
    if (isMidlineRegion(region)) {
      // Midline structures: no tint
      return [r, g, b];
    }
    
    if (hemi === 0) {
      // LEFT (Logical): Cool blue tint, slightly desaturated
      return [
        r * 0.85 + 0.05,           // reduce red
        g * 0.95 + 0.03,           // keep green
        Math.min(1.0, b * 1.1 + 0.08)  // boost blue
      ];
    } else {
      // RIGHT (Creative): Warm orange tint, more vibrant
      return [
        Math.min(1.0, r * 1.15 + 0.08),  // boost red
        g * 0.95,                   // keep green
        b * 0.85                    // reduce blue
      ];
    }
  }

  

  // --- Functional areas (display-only mapping) ---
// Returns 0 if not mapped. Uses voxel coordinates (per-hemisphere).
//
// This is an *anatomy-shaped* heuristic map (not just banded z-slices):
// - Occipital cap for V1
// - Curved central sulcus for M1/S1 strips
// - Temporal lobe is truly lateral+inferior (not bilateral inside a hemi volume)
// - Medial strip for cingulate/limbic
// - Left-dominant Broca/Wernicke overlays

function functionalAreaId(hemi, x, y, z, region) {
  // Only map cortical mantle (server regions 9..13) to reference functional areas (1..13).
  // Non-cortex (thalamus, hippocampus, etc) use their own labels/colors elsewhere.
  if (region < 9 || region > 13) return 0;

  // hemi voxel already has medial wall facing midline (x=0 is medial, x=1 is lateral)
  const x01 = clamp01(x);
  const y01 = clamp01(y);
  const z01 = clamp01(z);

  // hemi can arrive as either a string ("L"/"R") or a numeric side (0/1).
  const isLeft = (hemi === 0 || hemi === 'L' || hemi === 'left');

  const medial = 1.0 - x01;   // 0 lateral, 1 medial
  const lateral = x01;        // 0 medial, 1 lateral

  // Soft, biology-inspired "patch" classifier:
  // 1 V1: posterior + mostly medial (calcarine / occipital pole)
  // 5 A1: lateral superior temporal patch
  // 3 M1 / 9 S1: narrow band around central sulcus, mostly dorsolateral
  // 12 FEF: small dorsal frontal patch
  // 4 Broca & 11 Wernicke: left-dominant language patches (kept small)
  // 8 Olfactory: ventral-anterior/orbital patch
  // 6 Limbic/Cingulate: medial strip
  // 10 S2/Parietal + 7 Posterior Parietal: dorsal posterior association
  // 13 PFC: anterior association
  // 2 Multimodal: catch-all

  // Central sulcus "curve" (varies slightly with dorsal/ventral & medial/lateral)
  const zCs = 0.33 + 0.03 * Math.cos((y01 - 0.35) * Math.PI) + 0.02 * (0.5 - medial);

  // 8 Olfactory / orbitofrontal: ventral + anterior + lateral-ish
  if (z01 < 0.18 && y01 > 0.74 && lateral > 0.25) return 8;

  // 6 Limbic / cingulate: medial wall band (avoid extreme ventral)
  if (medial > 0.82 && y01 < 0.68 && z01 > 0.16 && z01 < 0.84) return 6;

  // 1 V1: posterior & mostly medial; allow small occipital pole cap laterally
  if (z01 > 0.82 && y01 < 0.92 && (medial > 0.58 || z01 > 0.92)) return 1;

  // Language (left-dominant): keep patches tight and in plausible gyri.
  // Broca: left IFG (lateral, ventrolateral frontal)
  if (isLeft && lateral > 0.70 && y01 > 0.58 && y01 < 0.82 && z01 > 0.16 && z01 < 0.40) return 4;
  // Wernicke: left posterior STG/TPJ
  if (isLeft && lateral > 0.60 && y01 > 0.48 && y01 < 0.78 && z01 > 0.60 && z01 < 0.84) return 11;

  // Helper: anisotropic squared distance in (z,y,lateral) space.
  function d2(zc, yc, lc, sz, sy, sl) {
    const dz = (z01 - zc) / sz;
    const dy = (y01 - yc) / sy;
    const dl = (lateral - lc) / sl;
    return dz * dz + dy * dy + dl * dl;
  }

  // Candidate distances (lower = better). Add penalties to keep regions in plausible territory.
  let bestId = 2;
  let best = 1e9;

  // 5 A1: lateral superior temporal patch (tight)
  if (lateral > 0.45 && y01 > 0.46 && y01 < 0.82 && z01 > 0.34 && z01 < 0.70) {
    const da = Math.min(
      d2(0.52, 0.62, 0.78, 0.14, 0.12, 0.18),
      d2(0.60, 0.62, 0.74, 0.16, 0.12, 0.20)
    );
    if (da < best) { best = da; bestId = 5; }
  }

  // 3 M1: precentral band around (zCs - small offset), dorsolateral
  if (y01 < 0.78 && lateral > 0.25) {
    const dm = Math.min(
      d2(zCs - 0.018, 0.38, 0.62, 0.07, 0.18, 0.30),
      d2(zCs - 0.018, 0.58, 0.58, 0.07, 0.22, 0.30)
    );
    // discourage medial wall
    const dm2 = dm + (medial > 0.75 ? 0.8 : 0.0);
    if (dm2 < best) { best = dm2; bestId = 3; }
  }

  // 9 S1: postcentral band around (zCs + small offset), dorsolateral
  if (y01 < 0.80 && lateral > 0.22) {
    const ds = Math.min(
      d2(zCs + 0.020, 0.38, 0.60, 0.07, 0.18, 0.32),
      d2(zCs + 0.020, 0.58, 0.56, 0.07, 0.22, 0.32)
    );
    const ds2 = ds + (medial > 0.78 ? 0.9 : 0.0);
    if (ds2 < best) { best = ds2; bestId = 9; }
  }

  // 12 FEF: dorsal frontal patch, near anterior M1
  if (y01 < 0.52 && z01 > 0.14 && z01 < 0.34 && lateral > 0.25) {
    const df = d2(0.22, 0.28, 0.55, 0.10, 0.14, 0.32);
    if (df < best) { best = df; bestId = 12; }
  }

  // 10 S2 / parietal association: dorsal posterior, slightly lateral
  if (y01 < 0.62 && z01 > 0.44) {
    const d10 = Math.min(
      d2(0.58, 0.36, 0.55, 0.18, 0.18, 0.35),
      d2(0.64, 0.46, 0.50, 0.18, 0.20, 0.35)
    );
    const d10p = d10 + (y01 > 0.70 ? 1.5 : 0.0);
    if (d10p < best) { best = d10p; bestId = 10; }
  }

  // 7 Posterior parietal: dorsal posterior cap (attention / spatial)
  if (y01 < 0.50 && z01 > 0.62) {
    const d7 = d2(0.74, 0.26, 0.58, 0.20, 0.16, 0.34);
    if (d7 < best) { best = d7; bestId = 7; }
  }

  // 13 PFC: anterior association (exclude orbitofrontal already handled)
  if (z01 < 0.40 && y01 < 0.70) {
    const d13 = Math.min(
      d2(0.16, 0.42, 0.55, 0.18, 0.22, 0.40),
      d2(0.22, 0.58, 0.48, 0.20, 0.24, 0.42)
    );
    const d13p = d13 + (z01 > 0.48 ? 0.8 : 0.0);
    if (d13p < best) { best = d13p; bestId = 13; }
  }

  // 2 Multimodal association as default, but bias temporal ventral remainder into 2.
  // (No-op; bestId already 2)

  return bestId;
}


  const _FC = {
    1:  [0.25, 0.55, 0.90], 2:  [0.20, 0.72, 0.36], 3:  [0.93, 0.74, 0.15],
    4:  [0.90, 0.20, 0.20], 5:  [0.90, 0.55, 0.20], 6:  [0.10, 0.55, 0.28],
    7:  [0.80, 0.45, 0.10], 8:  [0.80, 0.25, 0.60], 9:  [0.30, 0.70, 0.95],
    10: [0.15, 0.55, 0.90], 11: [0.95, 0.55, 0.55], 12: [0.60, 0.35, 0.80],
    13: [0.85, 0.20, 0.20], 14: [0.60, 0.42, 0.25],
  };
  function functionalColor(fid) {
    return _FC[fid] || _RC_DEFAULT;
  }

  function displayColor(hemi, x, y, z, region) {
    if (functionalOverlayEnabled) {
      const fid = functionalAreaId(hemi, x, y, z, region);
      // Keep structural (region/module) colours for the broad "association" catch-all (2),
      // otherwise the entire cortex collapses into a single green tint.
      // Only specialised functional patches override the structural palette.
      if (fid > 0 && fid !== 2) return functionalColor(fid);
    }
    return regionColor(region);
  }

  function ensureFunctionalLabelSprites(THREE, centroids) {
    if (!functionalLabelsEnabled || !centroids) {
      for (const sp of functionalSprites.values()) sp.visible = false;
      return;
    }

    // Simple numbered text sprite.
    function makeSprite(text) {
      const canvas = document.createElement('canvas');
      const size = 96;
      canvas.width = size;
      canvas.height = size;
      const ctx = canvas.getContext('2d');

      // Background disc
      ctx.clearRect(0, 0, size, size);
      ctx.beginPath();
      ctx.arc(size/2, size/2, size*0.42, 0, Math.PI*2);
      ctx.fillStyle = 'rgba(0,0,0,0.55)';
      ctx.fill();

      // Text
      ctx.font = 'bold 44px Arial';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.lineWidth = 6;
      ctx.strokeStyle = 'rgba(255,255,255,0.85)';
      ctx.strokeText(text, size/2, size/2);
      ctx.fillStyle = 'rgba(0,0,0,0.9)';
      ctx.fillText(text, size/2, size/2);

      const tex = new THREE.CanvasTexture(canvas);
      tex.needsUpdate = true;

      const mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, depthWrite: false });
      const sp = new THREE.Sprite(mat);
      sp.scale.set(spacing * 2.1, spacing * 2.1, 1);
      return sp;
    }

    for (const [fid, c] of Object.entries(centroids)) {
      const id = parseInt(fid, 10);
      if (!(id >= 1 && id <= 14)) continue;

      let sp = functionalSprites.get(id);
      if (!sp) {
        sp = makeSprite(String(id));
        functionalSprites.set(id, sp);
        if (scene) scene.add(sp);
      }
      sp.position.set(c.x, c.y, c.z);
      sp.visible = true;
    }
  }

function init(canvasId, volumeW, volumeH, volumeD, options) {
    if (!window.THREE) throw new Error("THREE is not loaded.");
    const THREE = window.THREE;

    w = volumeW; h = volumeH; d = volumeD;
    options = options || {};
    spacing = options.spacing || 2.4;
    mirrorRight = (options.mirrorRight !== undefined) ? options.mirrorRight : true;
    spikeCap = (options.spikeCap !== undefined) ? options.spikeCap : 2500;
    jitter = (options.jitter !== undefined) ? options.jitter : jitter;

    // Hemisphere separation / sagittal split.
    // We treat each hemisphere as a half-volume along the sagittal plane.
    // That means: per-hemisphere X span is halved, and the medial wall sits near the midline.
    // midlineGap is the empty fissure between hemispheres.
    midlineGap = (options.midlineGap !== undefined)
      ? options.midlineGap
      : ((options.midlineGapVox !== undefined) ? (options.midlineGapVox * spacing) : (50.0 * spacing));

    // Feature flags (Option A + B)
    medialFlattenEnabled = (options.medialFlattenEnabled !== undefined) ? options.medialFlattenEnabled : medialFlattenEnabled;
    medialFlattenStrength = (options.medialFlattenStrength !== undefined) ? options.medialFlattenStrength : medialFlattenStrength;
    superiorFissureBoost = (options.superiorFissureBoost !== undefined) ? options.superiorFissureBoost : superiorFissureBoost;
    fitToHemisphereEnabled = (options.fitToHemisphereEnabled !== undefined) ? options.fitToHemisphereEnabled : fitToHemisphereEnabled;
    fitExponent = (options.fitExponent !== undefined) ? options.fitExponent : fitExponent;

    // Old renderer treated each hemisphere as a full sphere/ellipsoid and then "overlapped" them.
    // Here: X half-extent is half of the per-hemisphere voxel radius.
    const hemiRX_full = (w * 0.5) * spacing;
    const hemiRX_half = hemiRX_full * 0.5;
    xOffset = midlineGap + hemiRX_half;

    // Anatomical coordinate system (render/world):
    //   X = left(-) / right(+)
    //   Y = inferior(-) / superior(+)
    //   Z = anterior(-) / posterior(+)
    // NOTE: do NOT introduce local axis scale factors here; the renderer maintains
    // global scaleX/scaleY/scaleZ that are controlled by Anatomical Mode.

    // Region offsets were used when the server-side anatomy was "blob-per-region".
    // The engine now generates a biologically positioned cortex mantle + deep nuclei.
    // Applying additional region offsets double-translates structures and can create
    // visible "protrusions". Keep offsets disabled (zero) so geometry matches the
    // engine's voxel anatomy.
    regionOffsets = {};

    const canvas = document.getElementById(canvasId);
    if (!canvas) throw new Error("Canvas not found: " + canvasId);

    scene = new THREE.Scene();
    createShellMeshes(THREE);
    camera = new THREE.PerspectiveCamera(50, (canvas.clientWidth || 800) / (canvas.clientHeight || 600), 0.1, 9000);
    
    // Position camera to view from front-right, slightly above.
    // Our mapping centres Y/Z around the origin; target the origin so the outline
    // and neural volume align.
    camera.position.set(xOffset * 2.5 * scaleX, +h * spacing * 0.5 * scaleY, d * spacing * 2.5 * scaleZ);
    camera.lookAt(new THREE.Vector3(0, 0, 0));

    renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, alpha: true });
    renderer.setSize(canvas.clientWidth || 800, canvas.clientHeight || 600, false);

    controls = createOrbitControls(canvas, camera);
    voxelRoundTex = createRoundPointTexture(THREE, 64);

    // Canon (Paul): render without warping/gyrification; show true placed anatomy.
    // Enforce regardless of any caller-supplied options.
    setAnatomicalMode(true);

    // Brain outline is optional; default off for neuron-only Live view.
    if (showOutline) createBrainOutline(THREE);

    // Connections with vertex colors
    // Keep these relatively faint so the node cloud silhouette reads as "brain".
    // Traffic pulses will temporarily brighten.
    connGeom = new THREE.BufferGeometry();
    connMat = new THREE.LineBasicMaterial({ 
      vertexColors: true, 
      transparent: true, 
      opacity: 0.12
    });
    connLines = new THREE.LineSegments(connGeom, connMat);
    scene.add(connLines);

    // Layout points - LEFT hemisphere
    baseGeomL = new THREE.BufferGeometry();
    baseMatL = new THREE.PointsMaterial({ size: 1.45, vertexColors: true, transparent: true, opacity: 0.55, map: voxelRoundTex, alphaTest: 0.2 });
    baseGeomL.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
    baseGeomL.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
    basePointsL = new THREE.Points(baseGeomL, baseMatL);
    scene.add(basePointsL);

    // Layout points - RIGHT hemisphere
    baseGeomR = new THREE.BufferGeometry();
    baseMatR = new THREE.PointsMaterial({ size: 1.45, vertexColors: true, transparent: true, opacity: 0.55, map: voxelRoundTex, alphaTest: 0.2 });
    baseGeomR.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
    baseGeomR.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
    basePointsR = new THREE.Points(baseGeomR, baseMatR);
    scene.add(basePointsR);

    // Layout points - MIDLINE
    midlineGeom = new THREE.BufferGeometry();
    midlineMat = new THREE.PointsMaterial({ size: 1.18, vertexColors: true, transparent: true, opacity: 0.45, map: voxelRoundTex, alphaTest: 0.2 });
    midlineGeom.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
    midlineGeom.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
    midlinePoints = new THREE.Points(midlineGeom, midlineMat);
    scene.add(midlinePoints);

    // Corpus callosum points � very dim base; lights up white via spike overlay.
    ccGeom = new THREE.BufferGeometry();
    ccMat = new THREE.PointsMaterial({ size: 0.95, vertexColors: true, transparent: true, opacity: 0.30, map: voxelRoundTex, alphaTest: 0.2 });
    ccGeom.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
    ccGeom.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
    ccPoints = new THREE.Points(ccGeom, ccMat);
    scene.add(ccPoints);

    // Spike points - LEFT
    geomL = new THREE.BufferGeometry();
    matL = new THREE.PointsMaterial({ size: 1.125, vertexColors: true, transparent: true, opacity: 0.95, map: voxelRoundTex, alphaTest: 0.2 });
    spikePosLArr = new Float32Array(spikeCap * 3);
    spikeColLArr = new Float32Array(spikeCap * 3);
    spikeAttrPosL = new THREE.BufferAttribute(spikePosLArr, 3);
    spikeAttrColL = new THREE.BufferAttribute(spikeColLArr, 3);
    geomL.setAttribute("position", spikeAttrPosL);
    geomL.setAttribute("color", spikeAttrColL);
    geomL.setDrawRange(0, 0);
    pointsL = new THREE.Points(geomL, matL);
    scene.add(pointsL);

    // Spike points - RIGHT
    geomR = new THREE.BufferGeometry();
    matR = new THREE.PointsMaterial({ size: 1.125, vertexColors: true, transparent: true, opacity: 0.95, map: voxelRoundTex, alphaTest: 0.2 });
    spikePosRArr = new Float32Array(spikeCap * 3);
    spikeColRArr = new Float32Array(spikeCap * 3);
    spikeAttrPosR = new THREE.BufferAttribute(spikePosRArr, 3);
    spikeAttrColR = new THREE.BufferAttribute(spikeColRArr, 3);
    geomR.setAttribute("position", spikeAttrPosR);
    geomR.setAttribute("color", spikeAttrColR);
    geomR.setDrawRange(0, 0);
    pointsR = new THREE.Points(geomR, matR);
    scene.add(pointsR);

    // Density clouds
    densGeomL = new THREE.BufferGeometry();
    densGeomR = new THREE.BufferGeometry();
    densMatL = new THREE.PointsMaterial({ size: 2.9, vertexColors: true, transparent: true, opacity: 0.10, depthWrite: false, map: voxelRoundTex, alphaTest: 0.2 });
    densMatR = new THREE.PointsMaterial({ size: 2.9, vertexColors: true, transparent: true, opacity: 0.10, depthWrite: false, map: voxelRoundTex, alphaTest: 0.2 });
    densGeomL.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
    densGeomL.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
    densGeomR.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
    densGeomR.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
    densL = new THREE.Points(densGeomL, densMatL);
    densR = new THREE.Points(densGeomR, densMatR);
    scene.add(densL);
    scene.add(densR);

    applyVisualStyle();
    animate();
  }

  function createBrainOutline(THREE) {
    // Simple outline that matches the renderer's anatomical coordinate mapping.
    // IMPORTANT: This must share the SAME centering as mapPoint(), otherwise the
    // outline will drift relative to the neural volume.
    const outlineMat = new THREE.LineBasicMaterial({ 
      color: 0x556677, 
      transparent: true, 
      opacity: 0.25 
    });

    // MapPoint() centers Y and Z around origin:
    //   yc = (y - h/2) * spacing
    //   zc = (z - d/2) * spacing
    // And hemispheres are offset on X by +/- xOffset.
    const centerY = 0;
    const centerZ = 0;
    // Must match mapPoint() axis scaling so outline hugs the neural volume.
    // Sagittal split: X half-extent is half the old radius.
    const hemiRX = (w * 0.5) * spacing * scaleX * 0.5;
    const hemiRY = (h * 0.5) * spacing * scaleY;
    const hemiRZ = (d * 0.5) * spacing * scaleZ;
    const hemiXC = xOffset * scaleX;
    // Used for midline/corpus callosum arcs that span both hemispheres.
    const outerRX = hemiXC + hemiRX;

    // Create ellipse points for one hemisphere outline
    function createHemisphereEllipse(xCenter, segments = outlineRefs._segs.sag) {
      const points = [];
      for (let i = 0; i <= segments; i++) {
        const angle = (i / segments) * Math.PI * 2;
        const x = xCenter;
        const y = centerY + Math.sin(angle) * hemiRY;
        const z = centerZ + Math.cos(angle) * hemiRZ;
        
        points.push(new THREE.Vector3(x, y, z));
      }
      return points;
    }

    // LEFT HEMISPHERE - sagittal profile
    const leftSagittal = createHemisphereEllipse(-hemiXC);
    const leftSagGeom = new THREE.BufferGeometry().setFromPoints(leftSagittal);
    const leftSagLine = new THREE.Line(leftSagGeom, outlineMat);
    outlineRefs.leftSag = leftSagLine;
    brainMeshes.push(leftSagLine);

    // RIGHT HEMISPHERE - sagittal profile
    const rightSagittal = createHemisphereEllipse(hemiXC);
    const rightSagGeom = new THREE.BufferGeometry().setFromPoints(rightSagittal);
    const rightSagLine = new THREE.Line(rightSagGeom, outlineMat);
    outlineRefs.rightSag = rightSagLine;
    brainMeshes.push(rightSagLine);

    // CORONAL ARCS (front-to-back slices)
    const coronalMat = new THREE.LineBasicMaterial({ color: 0x445566, transparent: true, opacity: 0.15 });
    
    function createCoronalArc(zPos, xCenter) {
      // A coronal arc for a single hemisphere (shows two-lobe overlap rather than one big hull).
      const points = [];
      const segments = outlineRefs._segs.cor;
      for (let i = 0; i <= segments; i++) {
        const t = i / segments;
        const angle = t * Math.PI;

        const x = xCenter + Math.cos(angle) * hemiRX;
        const y = centerY + Math.sin(angle) * hemiRY;

        points.push(new THREE.Vector3(x, y, zPos));
      }
      const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(points), coronalMat);
      // Coronal guide arcs can read as "holes" on the hemisphere surface when
      // viewed through the cortical point cloud. Keep them available (toggleable
      // via code later), but hide by default.
      line.visible = false;
      outlineRefs.coronal.push(line);
      return line;
    }
    
    // Z positions: negative = anterior (front), positive = posterior (back)
    // LEFT + RIGHT arcs at three z-slices
    const z1 = -hemiRZ * 0.9;
    const z2 = 0;
    const z3 = hemiRZ * 0.85;
    brainMeshes.push(createCoronalArc(z1, -hemiXC));
    brainMeshes.push(createCoronalArc(z1, +hemiXC));
    brainMeshes.push(createCoronalArc(z2, -hemiXC));
    brainMeshes.push(createCoronalArc(z2, +hemiXC));
    brainMeshes.push(createCoronalArc(z3, -hemiXC));
    brainMeshes.push(createCoronalArc(z3, +hemiXC));

    // AXIAL (top view) outline
    // Draw a single brain-like boundary (frontal bulge + occipital taper) rather than two spheres.
    const axialMat = new THREE.LineBasicMaterial({ color: 0x445566, transparent: true, opacity: 0.14 });
    const axialPts = [];
    const axialSegs = outlineRefs._segs.axial;
    for (let i = 0; i <= axialSegs; i++) {
      const a = (i / axialSegs) * Math.PI * 2;
      const s = Math.sin(a);
      const c = Math.cos(a); // z-axis component

      // Base ellipse in XZ.
      let xr = hemiRX * 0.96;
      let zr = hemiRZ * 0.92;

      // Frontal bulge (anterior = negative z): make it a little wider and rounder.
      const anterior = Math.max(0, -c);
      xr *= 1.0 + anterior * 0.14;
      zr *= 1.0 + anterior * 0.08;

      // Occipital taper (posterior = positive z): slightly narrower.
      const posterior = Math.max(0, c);
      xr *= 1.0 - posterior * 0.08;
      zr *= 1.0 - posterior * 0.04;

      // Temporal bulge: widest laterally around mid-z.
      const temporal = Math.abs(s) * (1.0 - Math.abs(c));
      xr *= 1.0 + temporal * 0.10;

      const x = s * xr;
      const z = c * zr;
      // Top roof of the brain (not the inferior base).
      axialPts.push(new THREE.Vector3(x, centerY + hemiRY * 0.95, z));
    }
    const axialLine = new THREE.Line(new THREE.BufferGeometry().setFromPoints(axialPts), axialMat);
    outlineRefs.axial = axialLine;
    brainMeshes.push(axialLine);

    // INTERHEMISPHERIC FISSURE (top): a subtle midline line to read as "brain".
    const fissMat = new THREE.LineBasicMaterial({ color: 0x445566, transparent: true, opacity: 0.10 });
    const fissPts = [];
    const fissSegs = outlineRefs._segs.fiss;
    for (let i = 0; i <= fissSegs; i++) {
      const t = i / fissSegs;
      const z = (t - 0.5) * (hemiRZ * 1.6);
      // Slight dip at the centre to suggest depth.
      const y = (centerY + hemiRY * 0.90) - Math.sin(t * Math.PI) * (hemiRY * 0.06);
      fissPts.push(new THREE.Vector3(0, y, z));
    }
    const fissLine = new THREE.Line(new THREE.BufferGeometry().setFromPoints(fissPts), fissMat);
    outlineRefs.fissure = fissLine;
    brainMeshes.push(fissLine);

    // MIDLINE - vertical plane separating hemispheres
    const midlineMat = new THREE.LineBasicMaterial({ color: 0x665555, transparent: true, opacity: 0.18 });
    const midlinePoints = [
      new THREE.Vector3(0, centerY - hemiRY, -hemiRZ),
      new THREE.Vector3(0, centerY - hemiRY, hemiRZ),
      new THREE.Vector3(0, centerY + hemiRY, hemiRZ),
      new THREE.Vector3(0, centerY + hemiRY, -hemiRZ),
      new THREE.Vector3(0, centerY - hemiRY, -hemiRZ),
    ];
    const midlineGeom = new THREE.BufferGeometry().setFromPoints(midlinePoints);
    brainMeshes.push(new THREE.Line(midlineGeom, midlineMat));

    // CORPUS CALLOSUM - arc connecting hemispheres (at top of brain)
    const ccMat = new THREE.LineBasicMaterial({ color: 0x556655, transparent: true, opacity: 0.15 });
    const ccPoints = [];
    for (let i = 0; i <= 16; i++) {
      const t = i / 16;
      const x = (t - 0.5) * (outerRX * 1.1);
      // Arc just below the cortical roof
    // Arc near the superior midline.
    const y = (centerY + hemiRY * 0.30) + Math.sin(t * Math.PI) * (hemiRY * 0.08);
      ccPoints.push(new THREE.Vector3(x, y, 0));
    }
    const ccGeom = new THREE.BufferGeometry().setFromPoints(ccPoints);
    brainMeshes.push(new THREE.Line(ccGeom, ccMat));

    // Add all meshes to scene
    brainMeshes.forEach(m => scene.add(m));
  }

  // Refit the outline to the actual (warped) neural volume so it doesn't drift.
  // Called from setLayout() after positions are computed.
  function updateOutlineFromBounds(THREE, bounds) {
    if (!bounds || !outlineRefs.leftSag || !outlineRefs.rightSag || !outlineRefs.leftCoronal || !outlineRefs.rightCoronal || !outlineRefs.axial || !outlineRefs.fissure) return;

    const minX = bounds.min.x, maxX = bounds.max.x;
    const minY = bounds.min.y, maxY = bounds.max.y;
    const minZ = bounds.min.z, maxZ = bounds.max.z;

    const cx = (minX + maxX) * 0.5;
    const cy = (minY + maxY) * 0.5;
    const cz = (minZ + maxZ) * 0.5;

    const totalW = Math.max(1e-3, (maxX - minX));
    const totalH = Math.max(1e-3, (maxY - minY));
    const totalD = Math.max(1e-3, (maxZ - minZ));

    // Slightly "brain" proportions (not a perfect ellipsoid), derived from the live neural volume bounds.
    const hemiXC = totalW * 0.10;                         // midline gap / inter-hemispheric fissure space
    const hemiRX = Math.max(1e-3, totalW * 0.50 - hemiXC);
    const hemiRY = totalH * 0.52;
    const hemiRZ = totalD * 0.52;

    // Helper: hemisphere side silhouette in the YZ plane at constant X.
    // We warp an ellipse to get: frontal bulge, occipital taper, slight inferior flattening, and a small posterior-inferior bump.
    const hemiSide = (xCenter, segs = 96) => {
      const pts = [];
      for (let i = 0; i <= segs; i++) {
        const a = (i / segs) * Math.PI * 2.0;
        const s = Math.sin(a); // maps to Y
        const c = Math.cos(a); // maps to Z

        const anterior = Math.max(0.0, -c); // z negative side
        const posterior = Math.max(0.0,  c); // z positive side
        const inferior = Math.max(0.0,  s);  // y positive side (inferior)
        const superior = Math.max(0.0, -s);  // y negative side (superior)

        // Z radius: more frontal, a bit less posterior.
        let rz = hemiRZ * (1.0 + 0.18 * anterior - 0.10 * posterior);

        // Y radius: slightly domed superiorly, flatter inferiorly.
        let ry = hemiRY * (1.0 + 0.08 * superior - 0.16 * inferior);

        // Cerebellar-ish bump: posterior + inferior quadrant.
        const bump = posterior * inferior;
        rz *= (1.0 + 0.10 * bump);
        ry *= (1.0 + 0.06 * bump);

        const y = cy + s * ry;
        const z = cz + c * rz;

        pts.push(new THREE.Vector3(xCenter, y, z));
      }
      return pts;
    };

    // Update sagittal profiles
    outlineRefs.leftSag.geometry.setFromPoints(hemiSide(cx - hemiXC));
    outlineRefs.rightSag.geometry.setFromPoints(hemiSide(cx + hemiXC));

    // Coronal arcs (XY plane) at a few Z slices: anterior, mid, posterior.
    // These give the "breadth" and the temporal/lateral bulge impression.
    const coronalArc = (zPos, xCenter, segs = 80) => {
      const pts = [];
      // Slice factor: -1 anterior .. +1 posterior
      const t = (zPos - cz) / Math.max(1e-3, hemiRZ);

      // Width varies: broader mid, slightly broader anterior, tapered posterior.
      const wMid = 1.08;
      const wAnt = 1.12;
      const wPos = 0.92;

      const w = t < -0.15 ? (wAnt + (wMid - wAnt) * ((t + 1.0) / 0.85))
              : t >  0.15 ? (wMid + (wPos - wMid) * ((t - 0.15) / 0.85))
              : wMid;

      // Height varies: a touch taller mid/anterior, flatter posterior.
      const hMid = 1.02;
      const hAnt = 1.04;
      const hPos = 0.95;

      const h = t < -0.15 ? (hAnt + (hMid - hAnt) * ((t + 1.0) / 0.85))
              : t >  0.15 ? (hMid + (hPos - hMid) * ((t - 0.15) / 0.85))
              : hMid;

      const rx = hemiRX * w;
      const ry = hemiRY * h;

      for (let i = 0; i <= segs; i++) {
        const a = (i / segs) * Math.PI * 2.0;
        const sa = Math.sin(a);
        const ca = Math.cos(a);

        // Inferior flattening in the coronal slice as well (sa > 0).
        const flatten = sa > 0 ? 0.84 : 1.0;

        const x = xCenter + ca * rx;
        const y = cy + sa * ry * flatten;
        pts.push(new THREE.Vector3(x, y, zPos));
      }
      return pts;
    };

    const zA = cz - hemiRZ * 0.85;
    const zM = cz;
    const zP = cz + hemiRZ * 0.75;

    // Stitch multiple loops into a single polyline so we can render them as one LineLoop.
    const stitchLoops = (loops) => {
      const pts = [];
      for (let k = 0; k < loops.length; k++) {
        const loop = loops[k];
        for (let i = 0; i < loop.length; i++) pts.push(loop[i]);
        if (k !== loops.length - 1) pts.push(loop[loop.length - 1].clone().add(new THREE.Vector3(0, 0, 0.001)));
      }
      return pts;
    };

    outlineRefs.leftCoronal.geometry.setFromPoints(stitchLoops([
      coronalArc(zA, cx - hemiXC),
      coronalArc(zM, cx - hemiXC),
      coronalArc(zP, cx - hemiXC),
    ]));

    outlineRefs.rightCoronal.geometry.setFromPoints(stitchLoops([
      coronalArc(zA, cx + hemiXC),
      coronalArc(zM, cx + hemiXC),
      coronalArc(zP, cx + hemiXC),
    ]));

    // Axial "top" outline (XZ plane) just above the cortical roof (superior = -Y).
    const axialY = cy - hemiRY * 0.96;
    const axialPts = [];
    const segs = 120;
    for (let i = 0; i <= segs; i++) {
      const a = (i / segs) * Math.PI * 2.0;
      const ca = Math.cos(a);
      const sa = Math.sin(a);

      // Similar Z warp as sagittal: frontal bulge, posterior taper.
      const anterior = Math.max(0.0, -sa);
      const posterior = Math.max(0.0,  sa);
      const rz = hemiRZ * (1.0 + 0.16 * anterior - 0.10 * posterior);

      // Mild lateral temporal bulge.
      const temporal = Math.pow(Math.abs(ca), 1.6);
      const rx = hemiRX * (1.0 + 0.08 * (1.0 - temporal));

      const x = cx + ca * rx;
      const z = cz + sa * rz;
      axialPts.push(new THREE.Vector3(x, axialY, z));
    }
    outlineRefs.axial.geometry.setFromPoints(axialPts);

    // Inter-hemispheric fissure line (top midline)
    outlineRefs.fissure.geometry.setFromPoints([
      new THREE.Vector3(cx, axialY, cz - hemiRZ * 0.95),
      new THREE.Vector3(cx, axialY, cz + hemiRZ * 0.85),
    ]);
  }

  function createOrbitControls(canvas, camera) {
    const state = {
      dragging: false,
      panning: false,
      lastX: 0,
      lastY: 0,
      // Canon view: LEFT LATERAL (matches Neuroanatomy Navigation reference)
      // Camera sits on -X looking toward the midline (target at origin), so:
      //   screen +X ~ +Z (posterior to the right)
      //   screen +Y ~ +Y (superior up)
      // This prevents "posterior drifting" into screen-down/right diagonals.
      yaw: -Math.PI / 2,
      pitch: 0.0,
      distance: Math.max(100, d * spacing * 3),
      // Our point mapping centers Y/Z around 0, so aim the camera at the origin.
      // This keeps the outline and volume aligned visually.
      target: new THREE.Vector3(0, 0, 0),
      rotateSpeed: 0.007,
      panSpeed: 0.003,
      zoomSpeed: 0.0012
    };

    function applyCamera() {
      const cp = Math.cos(state.pitch);
      const sp = Math.sin(state.pitch);
      const cy = Math.cos(state.yaw);
      const sy = Math.sin(state.yaw);

      camera.position.set(
        state.target.x + state.distance * cp * sy,
        state.target.y + state.distance * sp,
        state.target.z + state.distance * cp * cy
      );
      camera.lookAt(state.target);
    }

    canvas.addEventListener("contextmenu", e => e.preventDefault());
    canvas.addEventListener("mousedown", e => {
      state.dragging = (e.button === 0);
      state.panning = (e.button === 2);
      state.lastX = e.clientX;
      state.lastY = e.clientY;
    });
    window.addEventListener("mousemove", e => {
      if (!state.dragging && !state.panning) return;
      const dx = e.clientX - state.lastX;
      const dy = e.clientY - state.lastY;
      state.lastX = e.clientX;
      state.lastY = e.clientY;

      if (state.dragging) {
        state.yaw -= dx * state.rotateSpeed;
        state.pitch -= dy * state.rotateSpeed;
        state.pitch = Math.max(-Math.PI * 0.48, Math.min(Math.PI * 0.48, state.pitch));
      } else if (state.panning) {
        const dir = new THREE.Vector3();
        camera.getWorldDirection(dir);
        const right = new THREE.Vector3().crossVectors(dir, camera.up).normalize();
        const up = camera.up.clone().normalize();
        const panScale = state.distance * state.panSpeed;
        state.target.addScaledVector(right, -dx * panScale);
        state.target.addScaledVector(up, dy * panScale);
      }
      applyCamera();
    });
    window.addEventListener("mouseup", () => { state.dragging = false; state.panning = false; });
    canvas.addEventListener("wheel", e => {
      e.preventDefault();
      state.distance *= (1.0 + e.deltaY * state.zoomSpeed);
      state.distance = Math.max(30, Math.min(3000, state.distance));
      applyCamera();
    }, { passive: false });

    applyCamera();
    // Expose minimal API compatible with our view preset helpers.
    // NOTE: This is NOT THREE.OrbitControls; it's a lightweight internal control.
    return {
      target: state.target,
      update: () => applyCamera(),
      setView: (dir, dist, target, up) => {
        if (up) camera.up.set(up.x, up.y, up.z);
        if (target) state.target.set(target.x, target.y, target.z);
        const dy = Math.max(-1, Math.min(1, dir.y));
        state.pitch = Math.asin(dy);
        state.yaw = Math.atan2(dir.x, dir.z);
        state.distance = Math.max(30, Math.min(9000, dist));
        applyCamera();
      }
    };
  }

  function initSafe(canvasId, volumeW, volumeH, volumeD, options) {
    try { init(canvasId, volumeW, volumeH, volumeD, options); return null; }
    catch (e) { console.error("NeuralRenderer.init failed:", e); return e.message || "init failed"; }
  }

  function resizeIfNeeded() {
    if (!renderer) return;
    const canvas = renderer.domElement;
    const width = canvas.clientWidth, height = canvas.clientHeight;
    if (width && height && (canvas.width !== width || canvas.height !== height)) {
      renderer.setSize(width, height, false);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
    }
  }

  function regionOffset(region) {
    return regionOffsets[region] || { x: 0, y: 0, z: 0 };
  }

  // Map voxel coordinates to 3D world position
  function mapPoint(hemi, x, y, z, region) {
    // CLEAN BIOLOGICAL RENDERING
    // The engine's ApplyRegionLayout already places structures anatomically with
    // brain-shaped envelope, volumetric scaling, capsule hippocampus, etc.
    // mapPoint just converts voxel coords -> world coords with:
    //   1. Center around origin
    //   2. Scale to human brain proportions
    //   3. Mirror right hemisphere
    //   4. Separate hemispheres with midline fissure gap
    // Anatomical circuit remapping (subcortical shapes, gyrus targets) happens
    // in setLayout pass 2 via anatomicalPlacePoint, NOT here.

    const xm = hemiVoxelX(hemi, x);
    
    // Center around origin. Y flipped: voxel y=0 = superior, render +Y = superior
    const yc = (h / 2 - y) * spacing;
    const zc = (z - d / 2) * spacing;

    // Midline regions (thalamus, hypothalamus, brainstem, pons): center X, direct Y/Z
    if (isMidlineRegion(region)) {
      const xCentered = (xm - (w - 1) / 2) * spacing;
      return {
        x: (xCentered * 0.35) * scaleX,
        y: yc * scaleY,
        z: zc * scaleZ
      };
    }

    // HEMISPHERIC REGIONS: midline-style direct voxel mapping with bilateral mirror.
    // Same (voxel - center) * spacing as midline, but offset outward per hemisphere.
    //
    // Left hemi (hemi=0): voxels 0..~15, xCentered goes from -37 (lateral) to ~0 (medial)
    //   We want final X negative, so: offset - |xCentered| ? more negative = more lateral
    // Right hemi (hemi=1): xm already mirrored by hemiVoxelX, same xCentered range
    //   We want final X positive, so: offset + |xCentered| flipped
    
    const xCentered = (xm - (w - 1) / 2) * spacing;  // negative for lateral voxels
    const s = (hemi === 0) ? -1 : 1;
    
    // Hemisphere offset from midline
    const hemiOffset = midlineGap * 0.1 * s;
    
    // For left: wx = hemiOffset + xCentered = negative (offset is neg, xCentered is neg for lateral)
    // For right: wx = hemiOffset - xCentered = positive (offset is pos, xCentered is neg so -neg = pos for lateral)
    const wx = (hemiOffset + xCentered * s * -1) * scaleX;
    const wy = yc * scaleY;
    const wz = zc * scaleZ;
    
    // brainWarp for biological shape
    const baseXWorld = hemiOffset * scaleX;
    return brainWarp(hemi, wx, wy, wz, baseXWorld, region);
  }

  
function setLayout(packedPoints) {
  packedPoints = _normalizePacked(packedPoints);
  if (!packedPoints || !baseGeomL || !baseGeomR || !midlineGeom) return;
  const count = packedPoints.count;
  const data = packedPoints.data;
  if (!data || count <= 0) return;

  // Cache the layout data for refresh capability
  cachedLayoutData = packedPoints;

  layoutPosMap.clear();
  ccPosMap.clear();
  ccPosArray = [];

  const areaSum = {};
  for (let a = 1; a <= 14; a++) areaSum[a] = { sx: 0, sy: 0, sz: 0, c: 0 };

  let countL = 0, countR = 0, countMid = 0, countCC = 0;
  let ccGhostL = 0, ccGhostR = 0;
  const ccXMidCount = w >> 1;
  for (let i = 0; i < count; i++) {
    const hemi = data[i * 6 + 0];
    const x_c = data[i * 6 + 1];
    const region = data[i * 6 + 5];
    if (region === 255) continue;

    if (region === 14) {
      countCC++;
      if (x_c < ccXMidCount) { ccGhostL++; } else { ccGhostR++; }
      continue;
    }

    if (isMidlineRegion(region)) {
      if (hemi === 0) countMid++;
    } else {
      if (hemi === 0) countL++; else countR++;
    }
  }
  const posL = new Float32Array((countL + ccGhostL) * 3);
  const colL = new Float32Array((countL + ccGhostL) * 3);
  const posR = new Float32Array((countR + ccGhostR) * 3);
  const colR = new Float32Array((countR + ccGhostR) * 3);
  const posMid = new Float32Array(countMid * 3);
  const colMid = new Float32Array(countMid * 3);
  const posCC = new Float32Array(countCC * 3);
  const colCC = new Float32Array(countCC * 3);

  // === PASS 1: compute base positions + per-region bounds (for anatomical placement) ===
  const basePos = new Float32Array(count * 3);
  const boundsByKey = new Map();

  function getKey(region, hemi) {
    // CORTEX: use WHOLE-HEMISPHERE bounds so u,v,w span the entire cortex.
    // This eliminates gaps between gyri � all cortical neurons share one
    // continuous coordinate space, with gyrus identity only controlling
    // where within the hemisphere each neuron lands.
    if (isCorticalRegion(region)) return `CTX|${hemi}`;
    if (isMidlineRegion(region)) return `${region}|M`;
    // Bilateral subcortical (hippocampus 5, amygdala 4, basal ganglia 3, cerebellum 27)
    return `${region}|${hemi}`;
  }
  function pushBounds(key, x, y, z) {
    let b = boundsByKey.get(key);
    if (!b) {
      b = { minX: x, minY: y, minZ: z, maxX: x, maxY: y, maxZ: z };
      boundsByKey.set(key, b);
      return;
    }
    if (x < b.minX) b.minX = x; if (x > b.maxX) b.maxX = x;
    if (y < b.minY) b.minY = y; if (y > b.maxY) b.maxY = y;
    if (z < b.minZ) b.minZ = z; if (z > b.maxZ) b.maxZ = z;
  }

  // Track cortex surface height per z-slice per hemisphere for CC ghost clamping
  const cortexMaxY = [new Float32Array(d).fill(-Infinity), new Float32Array(d).fill(-Infinity)];
  // Track cortex region in voxel space for CC ghost color matching (full x,y,z neighbourhood)
  // 0 means unknown/non-cortex. Stored as Uint16 for compactness.
  const wh = w * h;
  const cortexRegion3D = [new Uint16Array(wh * d), new Uint16Array(wh * d)];

  // CC ghost -> matched cortex region (for spike colour matching)
  const ccGhostRegionByKey = new Map();

  function _idxOf(x, y, z) { return x + y * w + z * wh; }

  function _nearestCortexRegion(h0, x, y, z) {
    // Exact match first.
    const idx0 = _idxOf(x, y, z);
    const r0 = cortexRegion3D[h0][idx0];
    if (r0) return r0;

    // Search local neighbourhood (small radius; CC voxels are sparse, so this is cheap).
    let bestR = 0;
    let bestD2 = 1e9;
    const maxR = 3;
    for (let rr = 1; rr <= maxR; rr++) {
      const x0 = Math.max(0, x - rr), x1 = Math.min(w - 1, x + rr);
      const y0 = Math.max(0, y - rr), y1 = Math.min(h - 1, y + rr);
      const z0 = Math.max(0, z - rr), z1 = Math.min(d - 1, z + rr);
      for (let zz = z0; zz <= z1; zz++) {
        for (let yy = y0; yy <= y1; yy++) {
          const row = yy * w + zz * wh;
          for (let xx = x0; xx <= x1; xx++) {
            const r = cortexRegion3D[h0][row + xx];
            if (!r) continue;
            const dx = xx - x, dy = yy - y, dz = zz - z;
            const d2 = dx * dx + dy * dy + dz * dz;
            if (d2 < bestD2) { bestD2 = d2; bestR = r; }
          }
        }
      }
      if (bestR) return bestR;
    }
    return 16; // fallback: middle frontal
  }

  for (let i = 0; i < count; i++) {
    const hemi = data[i * 6 + 0];
    const x = data[i * 6 + 1], y = data[i * 6 + 2], z = data[i * 6 + 3];
    const region = data[i * 6 + 5];
    if (region === 255) continue;

    const p = layoutPosMap.get(_posKey(hemi, x, y, z)) || mapPoint(hemi, x, y, z, region);
    const bi = i * 3;
    basePos[bi] = p.x; basePos[bi + 1] = p.y; basePos[bi + 2] = p.z;

    if (anatomicalCircuitPlacementEnabled) {
      pushBounds(getKey(region, hemi), p.x, p.y, p.z);
      if (isCorticalRegion(region) && z >= 0 && z < d) {
        const h0 = (hemi === 0) ? 0 : 1;
        if (p.y > cortexMaxY[h0][z]) cortexMaxY[h0][z] = p.y;
        // Store cortex region in full voxel space for neighbourhood matching.
        cortexRegion3D[h0][_idxOf(x, y, z)] = region;
      }
    }
  }

  // === PASS 2: write final layout (optionally anatomically placed) ===
  let iL = 0, iR = 0, iMid = 0, iCC = 0;

  // Bounds for refitting outline to the actual rendered volume (after placement).
  let minX = Infinity, minY = Infinity, minZ = Infinity;
  let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;

  for (let i = 0; i < count; i++) {
    const hemi = data[i * 6 + 0];
    const x = data[i * 6 + 1], y = data[i * 6 + 2], z = data[i * 6 + 3];
    const region = data[i * 6 + 5];
    if (region === 255) continue;

    const base = i * 3;
    const pBase = { x: basePos[base], y: basePos[base + 1], z: basePos[base + 2] };

    let p;
    
    if (region === 14 && anatomicalCircuitPlacementEnabled) {
      // Corpus callosum: map directly from VOXEL coordinates to the CC arch.
      const ccU = _clamp01(z / Math.max(1, d - 1));
      const ccV = _clamp01(x / Math.max(1, w - 1));
      const ccW = _clamp01(y / Math.max(1, h - 1));
      p = _mapToCorpusCallosum(ccU, ccV, ccW);
    } else if (anatomicalCircuitPlacementEnabled && isCorticalRegion(region)) {
      // CORTEX: base positions include brainWarp shape deformation.
      // No medial wall clamp needed - the engine's fissure exclusion zone
      // and brainWarp's midline compression already create the longitudinal fissure.
      p = pBase;
    } else if (anatomicalCircuitPlacementEnabled) {
      p = anatomicalPlacePoint(pBase, boundsByKey.get(getKey(region, hemi)), region, hemi);
    } else {
      p = pBase;
    }

    if (region === 14 && !anatomicalCircuitPlacementEnabled) {
      // Keep CC near the midline in non-anatomical mode, but don't collapse it to a line.
      const dx = x - (w - 1) * 0.5;
      const halfWidthMm = 7.0;
      const xMm = dx * spacing;
      p = { x: Math.max(-halfWidthMm, Math.min(halfWidthMm, xMm)), y: p.y, z: p.z };
    }

    const baseColor = displayColor(hemi, x, y, z, region);
    const tinted = applyHemisphereTint(baseColor, hemi, region);
    // Dim the base neuron colors so resting state is clearly subdued but still visible.
    // Spike overlay at full brightness (opacity 0.95) will pop against this.
    // CC (region 14) is exempt: it already has a dim base colour [0.35,0.35,0.40]
    // and its own low-opacity material (0.25), so further dimming would make it invisible.
    const dimFactor = (region === 14) ? 1.0 : ((region === 1) ? 0.22 : 0.38);
    const c = [tinted[0] * dimFactor, tinted[1] * dimFactor, tinted[2] * dimFactor];

    const fid = functionalAreaId(hemi, x, y, z, region);
    if (fid > 0 && areaSum[fid]) {
      areaSum[fid].sx += p.x; areaSum[fid].sy += p.y; areaSum[fid].sz += p.z; areaSum[fid].c += 1;
    }

    if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
    if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
    if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;

    layoutPosMap.set(_posKey(hemi, x, y, z), p);
    if (isMidlineRegion(region)) {
      layoutPosMap.set(_posKey(0, x, y, z), p);
      layoutPosMap.set(_posKey(1, x, y, z), p);
    }

    if (region === 14) {
      const out = iCC * 3;
      posCC[out] = p.x; posCC[out + 1] = p.y; posCC[out + 2] = p.z;
      colCC[out] = c[0]; colCC[out + 1] = c[1]; colCC[out + 2] = c[2];
      iCC++;
      // Store in dedicated CC map so connections resolve to arch, not overwritten cortex pos
      ccPosMap.set(_posKey(0, x, y, z), p);
      ccPosMap.set(_posKey(1, x, y, z), p);
      ccPosArray.push({ z: z, y: y, pos: p });
      
      // === CC GHOST FILL ===
      // Bilateral like hippocampus: hemisphere mapPoint for cortex shape,
      // Y clamped to cortex surface, X clamped to respect midline fissure.
      {
        const ccXMid = w >> 1;
        const ccHemi = (x < ccXMid) ? 0 : 1;
        const h0 = ccHemi === 0 ? 0 : 1;

        // Match surrounding cortex region first so posterior CC ghosts follow
        // the hosting lobe rather than a hard-coded frontal projection.
        const matchedRegion = _nearestCortexRegion(h0, x, y, z);
        const ghostPos = mapPoint(ccHemi, x, y, z, matchedRegion);

        // Clamp Y to cortex surface at this z-slice
        const surfaceY = (z >= 0 && z < d) ? cortexMaxY[h0][z] : ghostPos.y;
        if (surfaceY > -Infinity && ghostPos.y > surfaceY) {
          ghostPos.y = surfaceY;
        }

        // Enforce hemisphere containment in X and Z so ghosts cannot leak off
        // the posterior tail or cross the interhemispheric fissure.
        const ctxBounds = boundsByKey.get(`CTX|${ccHemi}`);
        if (ctxBounds) {
          if (ccHemi === 0 && ghostPos.x > ctxBounds.maxX) ghostPos.x = ctxBounds.maxX;
          if (ccHemi === 1 && ghostPos.x < ctxBounds.minX) ghostPos.x = ctxBounds.minX;
          if (ghostPos.z < ctxBounds.minZ) ghostPos.z = ctxBounds.minZ;
          if (ghostPos.z > ctxBounds.maxZ) ghostPos.z = ctxBounds.maxZ;
        }

        const ghostColor = displayColor(ccHemi, x, y, z, matchedRegion);
        const ghostTinted = applyHemisphereTint(ghostColor, ccHemi, matchedRegion);
        const gc = [ghostTinted[0]*CC_GHOST_DIM_FACTOR, ghostTinted[1]*CC_GHOST_DIM_FACTOR, ghostTinted[2]*CC_GHOST_DIM_FACTOR];
        
        const ghostKey = _ccGhostKey(ccHemi, x, y, z);
        layoutPosMap.set(ghostKey, ghostPos);
        ccGhostRegionByKey.set(ghostKey, matchedRegion);
        
        if (ccHemi === 0) {
          const out2 = iL * 3;
          posL[out2] = ghostPos.x; posL[out2+1] = ghostPos.y; posL[out2+2] = ghostPos.z;
          colL[out2] = gc[0]; colL[out2+1] = gc[1]; colL[out2+2] = gc[2];
          iL++;
        } else {
          const out2 = iR * 3;
          posR[out2] = ghostPos.x; posR[out2+1] = ghostPos.y; posR[out2+2] = ghostPos.z;
          colR[out2] = gc[0]; colR[out2+1] = gc[1]; colR[out2+2] = gc[2];
          iR++;
        }
      }
    } else if (isMidlineRegion(region)) {
      if (hemi === 0) {
        const out = iMid * 3;
        posMid[out] = p.x; posMid[out + 1] = p.y; posMid[out + 2] = p.z;
        colMid[out] = c[0]; colMid[out + 1] = c[1]; colMid[out + 2] = c[2];
        iMid++;
      }
    } else if (hemi === 0) {
      const out = iL * 3;
      posL[out] = p.x; posL[out + 1] = p.y; posL[out + 2] = p.z;
      colL[out] = c[0]; colL[out + 1] = c[1]; colL[out + 2] = c[2];
      iL++;
    } else {
      const out = iR * 3;
      posR[out] = p.x; posR[out + 1] = p.y; posR[out + 2] = p.z;
      colR[out] = c[0]; colR[out + 1] = c[1]; colR[out + 2] = c[2];
      iR++;
    }
  }

  baseGeomL.setAttribute("position", new THREE.BufferAttribute(posL, 3));
  baseGeomL.setAttribute("color", new THREE.BufferAttribute(colL, 3));
  baseGeomL.attributes.position.needsUpdate = true;
  baseGeomL.attributes.color.needsUpdate = true;

  baseGeomR.setAttribute("position", new THREE.BufferAttribute(posR, 3));
  baseGeomR.setAttribute("color", new THREE.BufferAttribute(colR, 3));
  baseGeomR.attributes.position.needsUpdate = true;
  baseGeomR.attributes.color.needsUpdate = true;

  midlineGeom.setAttribute("position", new THREE.BufferAttribute(posMid, 3));
  midlineGeom.setAttribute("color", new THREE.BufferAttribute(colMid, 3));
  midlineGeom.attributes.position.needsUpdate = true;
  midlineGeom.attributes.color.needsUpdate = true;


  if (ccGeom) {
    ccGeom.setAttribute("position", new THREE.BufferAttribute(posCC, 3));
    ccGeom.setAttribute("color", new THREE.BufferAttribute(colCC, 3));
    ccGeom.attributes.position.needsUpdate = true;
    ccGeom.attributes.color.needsUpdate = true;
    if (ccPoints) ccPoints.visible = (countCC > 0);
  }

  // Sort CC positions by Z for fast nearest-neighbor connection anchoring
  ccPosArray.sort((a, b) => a.z - b.z);

  // Refit brain outline to match the current volume (only if enabled).
  if (showOutline && isFinite(minX) && isFinite(maxX) && isFinite(minY) && isFinite(maxY) && isFinite(minZ) && isFinite(maxZ)) {
    updateOutlineFromBounds(THREE, { minX, maxX, minY, maxY, minZ, maxZ });
  }

  // Functional area labels (centroids computed from the rendered layout)
  const centroids = {};
  for (let a = 1; a <= 14; a++) {
    const s = areaSum[a];
    if (s && s.c > 0) centroids[a] = { x: s.sx / s.c, y: s.sy / s.c, z: s.sz / s.c };
  }
  ensureFunctionalLabelSprites(window.THREE, centroids);
  updateShellMeshes();
  applyVisualStyle();
}

function setConnections(packedLines, forceRefresh = false) {
    packedLines = _normalizePacked(packedLines);
    if (!packedLines || !connGeom) return;
    if (connectionsLoaded && !forceRefresh) return;
    
    // Cache connection data for refresh
    cachedConnectionData = packedLines;
    
    const count = packedLines.count || 0;
    const data = packedLines.data;
    if (!data || count <= 0) {
      connGeom.setAttribute("position", new THREE.BufferAttribute(new Float32Array(0), 3));
      connGeom.setAttribute("color", new THREE.BufferAttribute(new Float32Array(0), 3));
      return;
    }

    const stride = Math.floor(data.length / Math.max(1, count));
    const use12 = (stride >= 12);

    // Clear old key mappings on refresh
    if (forceRefresh) {
      connKeyToSeg.clear();
    }

    
    // === CONNECTION SAMPLING (STRATIFIED) ===
    // The raw connectome can be extremely dense; if we sample na�vely, a small set of
    // hubs dominates the line budget and you get a "funnel" / "trunk" artifact.
    // We instead stratify by (source hemisphere, source region) so every cortical
    // territory contributes visible fibres.
    //
    // Budget: cap total displayed *edges* (not segments). Callosal routing may emit
    // up to 2 segments per edge (via CC arch), so segments are capped later.
    const MAX_DISPLAY_EDGES = 22000;

    function _hashStr(s) {
      // FNV-1a 32-bit
      let h = 2166136261 >>> 0;
      for (let i = 0; i < s.length; i++) {
        h ^= s.charCodeAt(i);
        h = Math.imul(h, 16777619) >>> 0;
      }
      return h >>> 0;
    }

    function hashSample(seed) {
      let x = (seed * 2654435761) >>> 0;
      x = ((x ^ (x >> 16)) * 2246822507) >>> 0;
      x = ((x ^ (x >> 13)) * 3266489909) >>> 0;
      return (x ^ (x >> 16)) >>> 0;
    }

    // Pass 1: bucket edges by (source hemi, source region)
    const connsByBucket = new Map(); // bucketKey -> [edgeIndex]
    let eligibleCount = 0;

    for (let i = 0; i < count; i++) {
      const b = i * (use12 ? 12 : 10);
      const h1 = data[b + 0] || 0;
      const x1 = data[b + 1] || 0, y1 = data[b + 2] || 0, z1 = data[b + 3] || 0;
      const r1 = use12 ? (data[b + 4] || 0) : 0;
      const h2 = use12 ? (data[b + 5] || 0) : (data[b + 4] || 0);
      const r2 = use12 ? (data[b + 9] || 0) : 0;

      if (!shouldShowConnection(h1, h2, r1, r2)) continue;

      // Stratify by source region when cortical; for deep sources keep as-is.
      const bucketKey = `${h1}:${r1}`;
      let arr = connsByBucket.get(bucketKey);
      if (!arr) { arr = []; connsByBucket.set(bucketKey, arr); }
      arr.push(i);
      eligibleCount++;
    }

    const bucketCount = Math.max(1, connsByBucket.size);
    const globalBudget = Math.min(MAX_DISPLAY_EDGES, eligibleCount);
    const perBucket = Math.max(1, Math.floor(globalBudget / bucketCount));

    // Pass 2: sample deterministically within each bucket
    const selectedIndices = new Set();

    for (const [bucketKey, indices] of connsByBucket) {
      const total = indices.length;
      const toShow = Math.min(total, perBucket);

      if (toShow >= total) {
        for (const idx of indices) selectedIndices.add(idx);
        continue;
      }

      const seed0 = _hashStr(bucketKey);
      // Partial Fisher-Yates
      const shuffled = indices.slice();
      for (let j = 0; j < toShow; j++) {
        const swapIdx = j + (hashSample(seed0 + j * 31) % (shuffled.length - j));
        const tmp = shuffled[j];
        shuffled[j] = shuffled[swapIdx];
        shuffled[swapIdx] = tmp;
        selectedIndices.add(shuffled[j]);
      }
    }
// Allocate extra: callosal connections may produce 2 segments each (routed through CC arch)
    const maxSegs = selectedIndices.size * 2 + 16;
    const pos = new Float32Array(maxSegs * 6);
    const col = new Float32Array(maxSegs * 6);
    let k = 0;

    for (let i = 0; i < count; i++) {
      if (!selectedIndices.has(i)) continue;
      
      const b = i * (use12 ? 12 : 10);

      const h1 = data[b + 0] || 0;
      const x1 = data[b + 1] || 0, y1 = data[b + 2] || 0, z1 = data[b + 3] || 0;
      const r1 = use12 ? (data[b + 4] || 0) : 0;

      const h2 = use12 ? (data[b + 5] || 0) : (data[b + 4] || 0);
      const x2 = use12 ? (data[b + 6] || 0) : (data[b + 5] || 0);
      const y2 = use12 ? (data[b + 7] || 0) : (data[b + 6] || 0);
      const z2 = use12 ? (data[b + 8] || 0) : (data[b + 7] || 0);
      const r2 = use12 ? (data[b + 9] || 0) : 0;

      let p1 = (r1 === 14)
        ? (ccPosMap.get(_posKey(h1, x1, y1, z1)) || layoutPosMap.get(_posKey(h1, x1, y1, z1)) || mapPoint(h1, x1, y1, z1, r1))
        : (layoutPosMap.get(_posKey(h1, x1, y1, z1)) || mapPoint(h1, x1, y1, z1, r1));
      let p2 = (r2 === 14)
        ? (ccPosMap.get(_posKey(h2, x2, y2, z2)) || layoutPosMap.get(_posKey(h2, x2, y2, z2)) || mapPoint(h2, x2, y2, z2, r2))
        : (layoutPosMap.get(_posKey(h2, x2, y2, z2)) || mapPoint(h2, x2, y2, z2, r2));

      if (!isFinite(p1.x) || !isFinite(p2.x)) continue;

      let c = displayColor(h1, x1, y1, z1, (r1 || 0));
      const isCallosal = isCallosalEdge(h1, h2, (r1 || 0), (r2 || 0));

      if (isCallosal) {
        c = [0.38, 0.38, 0.46];
        const ccSide = (r1 === 14) ? 1 : (r2 === 14) ? 2 : 0;

        if (ccSide !== 0) {
          const ccP  = (ccSide === 1) ? p1 : p2;
          const ctxP = (ccSide === 1) ? p2 : p1;
          const b1 = k * 6;
          pos[b1]   = ccP.x;   pos[b1+1] = ccP.y;   pos[b1+2] = ccP.z;
          pos[b1+3] = ctxP.x;  pos[b1+4] = ctxP.y;  pos[b1+5] = ctxP.z;
          col[b1]   = c[0];    col[b1+1] = c[1];     col[b1+2] = c[2];
          col[b1+3] = c[0]*0.5;col[b1+4] = c[1]*0.5; col[b1+5] = c[2]*0.5;
          connKeyToSeg.set(_connKey(h1, x1, y1, z1, r1, h2, x2, y2, z2, r2), k);
          connKeyToSeg.set(_connKey(h2, x2, y2, z2, r2, h1, x1, y1, z1, r1), k);
          k++;
        } else {
          const avgZ = ((z1 + z2) * 0.5);
          let ccArch;
          if (ccPosArray.length > 0) {
            let lo = 0, hi = ccPosArray.length - 1;
            while (lo < hi) { const mid = (lo + hi) >> 1; if (ccPosArray[mid].z < avgZ) lo = mid + 1; else hi = mid; }
            ccArch = ccPosArray[lo].pos;
          } else {
            ccArch = _mapToCorpusCallosum(_clamp01(avgZ / Math.max(1, d - 1)), 0.5, 0.5);
          }

          const b1 = k * 6;
          pos[b1]   = p1.x;      pos[b1+1] = p1.y;      pos[b1+2] = p1.z;
          pos[b1+3] = ccArch.x;  pos[b1+4] = ccArch.y;  pos[b1+5] = ccArch.z;
          col[b1]   = c[0];      col[b1+1] = c[1];       col[b1+2] = c[2];
          col[b1+3] = c[0]*0.7;  col[b1+4] = c[1]*0.7;   col[b1+5] = c[2]*0.7;
          connKeyToSeg.set(_connKey(h1, x1, y1, z1, r1, h2, x2, y2, z2, r2), k);
          k++;

          const b2 = k * 6;
          pos[b2]   = ccArch.x;  pos[b2+1] = ccArch.y;  pos[b2+2] = ccArch.z;
          pos[b2+3] = p2.x;     pos[b2+4] = p2.y;     pos[b2+5] = p2.z;
          col[b2]   = c[0]*0.7; col[b2+1] = c[1]*0.7;  col[b2+2] = c[2]*0.7;
          col[b2+3] = c[0]*0.4; col[b2+4] = c[1]*0.4;  col[b2+5] = c[2]*0.4;
          connKeyToSeg.set(_connKey(h2, x2, y2, z2, r2, h1, x1, y1, z1, r1), k);
          k++;
        }
        continue;
      }

      const base = k * 6;
      pos[base] = p1.x; pos[base + 1] = p1.y; pos[base + 2] = p1.z;
      pos[base + 3] = p2.x; pos[base + 4] = p2.y; pos[base + 5] = p2.z;
      
      col[base] = c[0]; col[base + 1] = c[1]; col[base + 2] = c[2];
      col[base + 3] = c[0] * 0.6; col[base + 4] = c[1] * 0.6; col[base + 5] = c[2] * 0.6;
      connKeyToSeg.set(_connKey(h1, x1, y1, z1, r1, h2, x2, y2, z2, r2), k);
      connKeyToSeg.set(_connKey(h2, x2, y2, z2, r2, h1, x1, y1, z1, r1), k);

      k++;
    }

    const trimPos = pos.slice(0, k * 6);
    const trimCol = col.slice(0, k * 6);
    
    connGeom.setAttribute("position", new THREE.BufferAttribute(trimPos, 3));
    connGeom.setAttribute("color", new THREE.BufferAttribute(trimCol, 3));
    connGeom.attributes.position.needsUpdate = true;
    connGeom.attributes.color.needsUpdate = true;
    
    if (trimPos.length >= 6) connGeom.computeBoundingSphere();
    connectionsLoaded = true;

    // Init glow buffers
    connGlow = new Float32Array(k);
    connPulsePhase = new Float32Array(k);
    connBaseCol = new Float32Array(trimCol.length);
    connBaseCol.set(trimCol);
    connActive = [];
    connActiveSet = new Set();
    applyVisualStyle();
  }

  function updateTraffic(packedTraffic) {
    packedTraffic = _normalizePacked(packedTraffic);
    if (!packedTraffic || !packedTraffic.data || !connectionsLoaded) return;
    const cnt = packedTraffic.count || 0;
    const data = packedTraffic.data;
    if (cnt <= 0 || !data) return;
    const stride = 11;

    for (let i = 0; i < cnt; i++) {
      const b = i * stride;
      const h1 = data[b + 0] || 0;
      const x1 = data[b + 1] || 0;
      const y1 = data[b + 2] || 0;
      const z1 = data[b + 3] || 0;
      const r1 = data[b + 4] || 0;
      const h2 = data[b + 5] || 0;
      const x2 = data[b + 6] || 0;
      const y2 = data[b + 7] || 0;
      const z2 = data[b + 8] || 0;
      const r2 = data[b + 9] || 0;
      const sB = data[b + 10] || 0;
      const g = Math.min(1.0, (sB / 255.0) * 1.25);

      const key = _connKey(h1, x1, y1, z1, r1, h2, x2, y2, z2, r2);
      const seg = connKeyToSeg.get(key);
      if (seg === undefined) continue;

      const prev = connGlow[seg];
      const next = g > prev ? g : prev;
      connGlow[seg] = next;
      if (connPulsePhase) connPulsePhase[seg] = 0;
      if (!connActiveSet.has(seg)) {
        connActiveSet.add(seg);
        connActive.push(seg);
      }
    }
  }

  // updateFrame overloads:
  //  - updateFrame(points, callosalTraffic01)
  //  - updateFrame(points, densLeft, densRight, callosalTraffic01)  (legacy)
  function updateFrame(packedPoints, arg2, arg3, arg4) {
    packedPoints = _normalizePacked(packedPoints);
    if (!packedPoints || !geomL || !geomR) return;

    let densLeft = null;
    let densRight = null;
    let callosalTraffic01 = 0;

    if (typeof arg2 === 'number') {
      callosalTraffic01 = arg2;
    } else {
      densLeft = arg2;
      densRight = arg3;
      callosalTraffic01 = (typeof arg4 === 'number') ? arg4 : 0;
    }

    const count = packedPoints.count;
    const data = packedPoints.data;

    // Pass 1: count (for stride) without allocating.
    // Midline structures (thalamus, brainstem, CC) can have spikes that resolve
    // to either side of x=0, so we count them for BOTH buffers conservatively.
    let countL = 0, countR = 0;
    let preserveL = 0, preserveR = 0;

    for (let i = 0; i < count; i++) {
      const region = data[i * 6 + 5];
      if (region === 255) continue;

      const hemi = data[i * 6 + 0];
      const preserveSparse = (region === 14 || region === 27);

      if (isMidlineRegion(region)) {
        // Midline spikes go to whichever side they resolve to.
        countL++; countR++;
        if (preserveSparse) { preserveL++; preserveR++; }
      } else if (hemi === 0) {
        countL++;
        if (preserveSparse) preserveL++;
      } else {
        countR++;
        if (preserveSparse) preserveR++;
      }
    }

    const reserveL = Math.min(spikeCap, preserveL);
    const reserveR = Math.min(spikeCap, preserveR);
    const normalCapL = Math.max(0, spikeCap - reserveL);
    const normalCapR = Math.max(0, spikeCap - reserveR);

    const strideL = Math.max(1, Math.ceil(countL / spikeCap));
    const strideR = Math.max(1, Math.ceil(countR / spikeCap));

    let seenL = 0, seenR = 0;
    let iL = 0, iR = 0;

    for (let i = 0; i < count; i++) {
      const hemi = data[i * 6 + 0];
      const x = data[i * 6 + 1];
      const y = data[i * 6 + 2];
      const z = data[i * 6 + 3];
      const energy = data[i * 6 + 4] / 255.0;
      const region = data[i * 6 + 5];

      if (region === 255) continue;

      const p = (region === 14)
        ? (ccPosMap.get(_posKey(hemi, x, y, z)) || layoutPosMap.get(_posKey(hemi, x, y, z)) || mapPoint(hemi, x, y, z, region))
        : (layoutPosMap.get(_posKey(hemi, x, y, z)) || mapPoint(hemi, x, y, z, region));
      const baseColor = displayColor(hemi, x, y, z, region);
      const tintedColor = applyHemisphereTint(baseColor, hemi, region);
      
      const brightness = 0.5 + energy * 0.5;
      let rr, gg, bb;
      if (region === 14) {
        // Corpus callosum: spike as bright white (energy-modulated)
        rr = gg = bb = 0.6 + energy * 0.4;
      } else if (region === 27) {
        // Cerebellum spikes need to read through dense overdraw.
        const b = 0.78 + energy * 0.85;
        rr = Math.min(1.0, tintedColor[0] * b + 0.08);
        gg = Math.min(1.0, tintedColor[1] * b + 0.08);
        bb = Math.min(1.0, tintedColor[2] * b + 0.08);
      } else {
        rr = tintedColor[0] * brightness;
        gg = tintedColor[1] * brightness;
        bb = tintedColor[2] * brightness;
      }

      function _brighten(c, e) {
        // Bright version of base colour; keep within [0,1].
        const b = 0.65 + e * 0.70;
        return Math.min(1.0, c * b);
      }

      // For midline structures: route to LEFT or RIGHT buffer based on resolved X position.
      // This ensures CC and other midline circuits show activity on BOTH sides.
      if (isMidlineRegion(region)) {
        const preserveSparse = (region === 14 || region === 27);
        if (p.x <= 0) {
          if (!preserveSparse) {
            if ((seenL++ % strideL) !== 0) continue;
            if (iL >= normalCapL) continue;
          } else {
            seenL++;
            if (iL >= spikeCap) continue;
          }
          const base = iL * 3;
          spikePosLArr[base] = p.x; spikePosLArr[base + 1] = p.y; spikePosLArr[base + 2] = p.z;
          spikeColLArr[base] = rr; spikeColLArr[base + 1] = gg; spikeColLArr[base + 2] = bb;
          iL++;
        } else {
          if (!preserveSparse) {
            if ((seenR++ % strideR) !== 0) continue;
            if (iR >= normalCapR) continue;
          } else {
            seenR++;
            if (iR >= spikeCap) continue;
          }
          const base = iR * 3;
          spikePosRArr[base] = p.x; spikePosRArr[base + 1] = p.y; spikePosRArr[base + 2] = p.z;
          spikeColRArr[base] = rr; spikeColRArr[base + 1] = gg; spikeColRArr[base + 2] = bb;
          iR++;
        }
        
        // CC ghost spike: also fire at the cortex surface position
        if (region === 14) {
          const ccXMid = w >> 1;
          const ccHemi = (x < ccXMid) ? 0 : 1;
          const ghostKey = _ccGhostKey(ccHemi, x, y, z);
          const gp = layoutPosMap.get(ghostKey);
          if (gp) {
            // Use the ghost's matched cortex region colour, not CC white.
            const mr = ccGhostRegionByKey.get(ghostKey) || 16;
            const gc0 = applyHemisphereTint(displayColor(ccHemi, x, y, z, mr), ccHemi, mr);
            const gr = _brighten(gc0[0], energy);
            const gg2 = _brighten(gc0[1], energy);
            const gb = _brighten(gc0[2], energy);
            if (ccHemi === 0 && iL < spikeCap) {
              const base = iL * 3;
              spikePosLArr[base] = gp.x; spikePosLArr[base+1] = gp.y; spikePosLArr[base+2] = gp.z;
              spikeColLArr[base] = gr; spikeColLArr[base+1] = gg2; spikeColLArr[base+2] = gb;
              iL++;
            } else if (ccHemi === 1 && iR < spikeCap) {
              const base = iR * 3;
              spikePosRArr[base] = gp.x; spikePosRArr[base+1] = gp.y; spikePosRArr[base+2] = gp.z;
              spikeColRArr[base] = gr; spikeColRArr[base+1] = gg2; spikeColRArr[base+2] = gb;
              iR++;
            }
          }
        }
      } else if (hemi === 0) {
        const preserveSparse = (region === 14 || region === 27);
        if (!preserveSparse) {
          if ((seenL++ % strideL) !== 0) continue;
          if (iL >= normalCapL) continue;
        } else {
          seenL++;
          if (iL >= spikeCap) continue;
        }
        const base = iL * 3;
        spikePosLArr[base] = p.x; spikePosLArr[base + 1] = p.y; spikePosLArr[base + 2] = p.z;
        spikeColLArr[base] = rr; spikeColLArr[base + 1] = gg; spikeColLArr[base + 2] = bb;
        iL++;
      } else {
        const preserveSparse = (region === 14 || region === 27);
        if (!preserveSparse) {
          if ((seenR++ % strideR) !== 0) continue;
          if (iR >= normalCapR) continue;
        } else {
          seenR++;
          if (iR >= spikeCap) continue;
        }
        const base = iR * 3;
        spikePosRArr[base] = p.x; spikePosRArr[base + 1] = p.y; spikePosRArr[base + 2] = p.z;
        spikeColRArr[base] = rr; spikeColRArr[base + 1] = gg; spikeColRArr[base + 2] = bb;
        iR++;
      }
    }

    // Update existing buffers in-place (prevents allocation spikes / GC hitching)
    geomL.setDrawRange(0, iL);
    geomR.setDrawRange(0, iR);
    spikeAttrPosL.needsUpdate = true;
    spikeAttrColL.needsUpdate = true;
    spikeAttrPosR.needsUpdate = true;
    spikeAttrColR.needsUpdate = true;

    if (densLeft) updateDensityCloud(densGeomL, densLeft, 0);
    if (densRight) updateDensityCloud(densGeomR, densRight, 1);
    
    if (connMat) {
      connMat.opacity = 0.12 + callosalTraffic01 * 0.25;
    }

    // Apply any pending view preset once geometry exists and controls are ready.
    if (_pendingViewPreset && camera && controls && camera.position && camera.up && (controls.setView || controls.target)) {
      const p = _pendingViewPreset;
      // do not clear here; setViewPreset will clear once applied.
      setViewPreset(p);
    }
  }

  function updateDensities(densLeft, densRight) {
    if (densLeft) updateDensityCloud(densGeomL, densLeft, 0);
    if (densRight) updateDensityCloud(densGeomR, densRight, 1);
  }

  function updateDensityCloud(geom, densData, hemi) {
    if (!densData || !densData.data) return;
    
    const dw = densData.w, dh = densData.h, dd = densData.d;
    const arr = densData.data;
    
    const dsX = (w / dw) * spacing;
    const dsY = (h / dh) * spacing;
    const dsZ = (d / dd) * spacing;
    
    let activeCount = 0;
    for (let i = 0; i < arr.length; i++) {
      if (arr[i] > 25) activeCount++;
    }
    
    const pos = new Float32Array(activeCount * 3);
    const col = new Float32Array(activeCount * 3);
    let k = 0;
    
    for (let iz = 0; iz < dd; iz++) {
      for (let iy = 0; iy < dh; iy++) {
        for (let ix = 0; ix < dw; ix++) {
          const idx = (iz * dh + iy) * dw + ix;
          const val = arr[idx];
          if (val <= 25) continue;
          
          const t = val / 255.0;
          const xm = (hemi === 1 && mirrorRight) ? (dw - 1 - ix) : ix;
          
          const base = k * 3;
          pos[base] = (hemi === 0 ? -xOffset : +xOffset) + (xm - dw/2) * dsX;
          pos[base + 1] = iy * dsY;
          pos[base + 2] = (iz - dd/2) * dsZ;
          
          col[base] = 0.35 + t * 0.55;
          col[base + 1] = 0.45 + t * 0.45;
          col[base + 2] = 0.65 + t * 0.35;
          
          k++;
        }
      }
    }
    
    geom.setAttribute("position", new THREE.BufferAttribute(pos, 3));
    geom.setAttribute("color", new THREE.BufferAttribute(col, 3));
    geom.attributes.position.needsUpdate = true;
    geom.attributes.color.needsUpdate = true;
  }

  function animate() {
    requestAnimationFrame(animate);
    resizeIfNeeded();
    if (!renderer) return;

    // Edge glow decay + incremental color updates (fast, bounded by active segments)
    if (connectionsLoaded && connGlow && connBaseCol && connGeom && connGeom.attributes && connGeom.attributes.color) {
      const t = (performance && performance.now) ? performance.now() : Date.now();
      const dt = (lastAnimT > 0) ? Math.min(0.1, (t - lastAnimT) / 1000.0) : 0.016;
      lastAnimT = t;
      const decay = Math.exp(-dt * 6.5); // ~150ms half-life

      const colAttr = connGeom.attributes.color;
      const colArr = colAttr.array;

      let any = false;
      // Iterate active segments; remove when cooled.
      for (let i = connActive.length - 1; i >= 0; i--) {
        const seg = connActive[i];
        let g = connGlow[seg] * decay;
        if (g < 0.02) {
          connGlow[seg] = 0;
          connActiveSet.delete(seg);
          // Swap-remove (O(1) removal from unordered list)
          connActive[i] = connActive[connActive.length - 1];
          connActive.pop();

          // Restore base color (6 floats per segment: 2 endpoints � RGB)
          const base = seg * 6;
          for (let j = 0; j < 6; j++) colArr[base + j] = connBaseCol[base + j];
          any = true;
          continue;
        }

        connGlow[seg] = g;
        const base = seg * 6;
        const ig = 1.0 - g;
        let lead = 0.78;
        let tail = 0.42;
        if (fibrePulseEnabled) {
          const phase = (((t * 0.0035) + seg * 0.173) % 1.0 + 1.0) % 1.0;
          lead = 0.35 + 0.90 * phase;
          tail = 1.05 - lead * 0.55;
        }
        for (let ch = 0; ch < 3; ch++) {
          colArr[base + ch] = connBaseCol[base + ch] * ig + Math.min(1.0, g * lead);
          colArr[base + 3 + ch] = connBaseCol[base + 3 + ch] * ig + Math.min(1.0, g * tail);
        }
        any = true;
      }

      if (any) colAttr.needsUpdate = true;
    }

    if (controls) controls.update();
    renderer.render(scene, camera);
  }

  // Combined update: single interop call for frame + traffic + avatar
  function updateFrameCombined(packedPoints, callosalTraffic01, packedTraffic, bodyData) {
    updateFrame(packedPoints, callosalTraffic01);
    if (packedTraffic && packedTraffic.count > 0) updateTraffic(packedTraffic);
    if (bodyData && window.AvatarRenderer && window.AvatarRenderer.render) {
      window.AvatarRenderer.render(bodyData);
    }
  }

  
  // --------------------------------------------------------------------------
  // View presets (for tuning). Uses current geometry bounds to frame the brain.
  // Presets obey the unified coordinate system:
  //   X: Left(-) / Right(+)
  //   Y: Inferior(-) / Superior(+)
  //   Z: Anterior(-) / Posterior(+)
  // --------------------------------------------------------------------------
  function _computeBoundsFromGeoms() {
    const mins = { x: +1e9, y: +1e9, z: +1e9 };
    const maxs = { x: -1e9, y: -1e9, z: -1e9 };

    function consumeGeom(g) {
      if (!g) return;
      const a = g.getAttribute && g.getAttribute("position");
      if (!a || !a.array || a.array.length < 3) return;
      const arr = a.array;
      for (let i = 0; i < arr.length; i += 3) {
        const x = arr[i], y = arr[i + 1], z = arr[i + 2];
        if (x < mins.x) mins.x = x; if (x > maxs.x) maxs.x = x;
        if (y < mins.y) mins.y = y; if (y > maxs.y) maxs.y = y;
        if (z < mins.z) mins.z = z; if (z > maxs.z) maxs.z = z;
      }
    }

    consumeGeom(baseGeomL);
    consumeGeom(baseGeomR);

    if (mins.x > maxs.x) return null;
    return { mins, maxs };
  }

  function _fitDistance(bounds) {
    const cx = (bounds.mins.x + bounds.maxs.x) * 0.5;
    const cy = (bounds.mins.y + bounds.maxs.y) * 0.5;
    const cz = (bounds.mins.z + bounds.maxs.z) * 0.5;
    const dx = bounds.maxs.x - bounds.mins.x;
    const dy = bounds.maxs.y - bounds.mins.y;
    const dz = bounds.maxs.z - bounds.mins.z;
    const radius = Math.max(1e-3, Math.sqrt(dx*dx + dy*dy + dz*dz) * 0.5);
    return { cx, cy, cz, radius };
  }

  function setViewPreset(preset) {
    _pendingViewPreset = preset;
    if (!camera || !controls || !camera.position || !camera.up) return;
    if (!controls.setView && !controls.target) return;
    preset = _pendingViewPreset;
    _pendingViewPreset = null;
    const bounds = _computeBoundsFromGeoms();
    if (!bounds) return;

    const { cx, cy, cz, radius } = _fitDistance(bounds);
    const dist = radius * 2.25;

    let dir = { x: 0, y: 0, z: 1 };
    let up = { x: 0, y: 1, z: 0 };

    switch ((preset || "").toLowerCase()) {
      case "left": dir = { x: -1, y: 0, z: 0 }; up = { x: 0, y: 1, z: 0 }; break;
      case "right": dir = { x: 1, y: 0, z: 0 }; up = { x: 0, y: 1, z: 0 }; break;
      case "anterior": dir = { x: 0, y: 0, z: -1 }; up = { x: 0, y: 1, z: 0 }; break;
      case "posterior": dir = { x: 0, y: 0, z: 1 }; up = { x: 0, y: 1, z: 0 }; break;
      case "superior": dir = { x: 0, y: 1, z: 0 }; up = { x: 0, y: 0, z: -1 }; break;
      case "inferior": dir = { x: 0, y: -1, z: 0 }; up = { x: 0, y: 0, z: 1 }; break;
      default: dir = { x: 0, y: 0, z: 1 }; up = { x: 0, y: 1, z: 0 }; break;
    }

    if (controls.setView) {
      controls.setView(dir, dist, { x: cx, y: cy, z: cz }, up);
    } else {
      camera.up.set(up.x, up.y, up.z);
      camera.position.set(cx + dir.x * dist, cy + dir.y * dist, cz + dir.z * dist);
      if (controls.target && controls.target.set) controls.target.set(cx, cy, cz);
      else if (controls.target) { controls.target.x = cx; controls.target.y = cy; controls.target.z = cz; }
      if (controls.update) controls.update();
      camera.lookAt(cx, cy, cz);
    }
  }

return { 
    initSafe,
    setViewPreset,
    setLayout, 
    updateFrame, 
    updateFrameCombined,
    updateDensities, 
    setConnections, 
    updateTraffic,
    setAnatomicalMode,
    refresh,
    setConnectionFilter,
    setGyrificationEnabled: (v) => { gyrifyEnabled = v; refresh(); },
    setBrainWarpEnabled: (v) => { brainWarpEnabled = v; refresh(); },
    setJitter: (v) => { jitter = v; refresh(); },
    setRenderMode,
    setShellVisible,
    setFibrePulseEnabled,
    isAnatomicalMode: () => anatomicalMode,
    showAllConnections,
    hideAllConnections,
    showKeyPathwayConnections,
    showThalamicConnections,
    showCallosalConnections,
    showCerebellarConnections,
    showPontineConnections
  };
})();






























