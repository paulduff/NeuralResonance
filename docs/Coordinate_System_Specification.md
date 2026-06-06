# Neural Resonance Engine - Unified Coordinate System

## Standard Neuroanatomical Coordinate System

Based on the standard neuroanatomy navigation reference, the NRE adopts the following **unified coordinate convention** across all components:

```
                    SUPERIOR (+Y)
                        ↑
                        │
                        │
    POSTERIOR (+Z) ←────┼────→ ANTERIOR (-Z)
                        │
                        │
                        ↓
                    INFERIOR (-Y)

                    
        LATERAL ←───────┼───────→ LATERAL
       (Left: -X)       │        (Right: +X)
                     MEDIAL
                    (X = 0)
```

## Axis Definitions

| Axis | Direction | Anatomical Term | Description |
|------|-----------|-----------------|-------------|
| **X** | Positive (+X) | Lateral (Right) | Away from midline toward right ear |
| **X** | Negative (-X) | Lateral (Left) | Away from midline toward left ear |
| **X** | Zero (X=0) | Medial/Midsagittal | Brain midline (corpus callosum) |
| **Y** | Positive (+Y) | Superior/Dorsal | Toward top of skull |
| **Y** | Negative (-Y) | Inferior/Ventral | Toward chin/base of skull |
| **Z** | Positive (+Z) | Posterior/Caudal | Toward back of head (occipital) |
| **Z** | Negative (-Z) | Anterior/Rostral | Toward face (frontal) |

## Anatomical Planes

| Plane | Axes | Description |
|-------|------|-------------|
| **Sagittal** | Y-Z plane | Divides brain into left/right |
| **Coronal** | X-Y plane | Divides brain into front/back |
| **Horizontal/Axial** | X-Z plane | Divides brain into top/bottom |

## Voxel Space Mapping

The simulation operates on a discrete voxel grid with dimensions (W, H, D).

### Voxel to Normalized Coordinates

```csharp
// Voxel indices: (vx, vy, vz) where vx ∈ [0, W-1], etc.
// Normalized coordinates: (nx, ny, nz) where each ∈ [-1, +1]

nx = (vx / (W - 1)) * 2 - 1;  // X: -1 (left lateral) to +1 (right lateral)
ny = 1 - (vy / (H - 1)) * 2;  // Y: +1 (superior) to -1 (inferior)  [FLIPPED]
nz = (vz / (D - 1)) * 2 - 1;  // Z: -1 (anterior) to +1 (posterior)
```

**Note**: Y-axis is flipped because voxel grids typically index from top-down (vy=0 is top row), but anatomically superior is positive.

### Normalized to Voxel Coordinates

```csharp
vx = (int)((nx + 1) * 0.5f * (W - 1));
vy = (int)((1 - ny) * 0.5f * (H - 1));  // FLIPPED
vz = (int)((nz + 1) * 0.5f * (D - 1));
```

## Hemisphere Convention

Each hemisphere has its own voxel volume:

| Hemisphere | X Range (voxel) | X Range (normalized) | Medial Wall |
|------------|-----------------|----------------------|-------------|
| **Left** | vx ∈ [0, W-1] | X ∈ [-1, 0] | vx = W-1 (X = 0) |
| **Right** | vx ∈ [0, W-1] | X ∈ [0, +1] | vx = 0 (X = 0) |

The right hemisphere is stored with **mirrored X** internally so that medial structures align at vx=0 (both hemispheres). The renderer applies a mirror transform for display.

## Key Anatomical Landmarks (Normalized Z)

| Structure | Z Position | Description |
|-----------|------------|-------------|
| Frontal Pole | Z = -0.80 | Most anterior point |
| Prefrontal Cortex | Z = -0.64 | Executive function |
| Premotor Cortex | Z = -0.40 | Movement planning |
| Central Sulcus | Z = -0.20 | Motor/sensory boundary |
| Parietal Cortex | Z = +0.10 | Spatial processing |
| Temporal Pole | Z = -0.30 | Anterior temporal lobe |
| Occipital Cortex | Z = +0.60 | Primary visual |
| Cerebellum | Z = +0.70 | Motor coordination |
| Brainstem | Z = +0.50 | Autonomic functions |

## Key Anatomical Landmarks (Normalized Y)

| Structure | Y Position | Description |
|-----------|------------|-------------|
| Dorsal Cortex | Y = +0.70 | Top of brain |
| Corpus Callosum | Y = +0.20 | Interhemispheric connection |
| Thalamus | Y = 0.00 | Central relay |
| Hypothalamus | Y = -0.15 | Autonomic control |
| Amygdala | Y = -0.30 | Emotion processing |
| Cerebellum | Y = -0.50 | Below main mass |
| Brainstem | Y = -0.60 | Connecting to spinal cord |

## Implementation in NRE Components

### 1. NreEngine (Simulation)

```csharp
/// <summary>
/// Unified coordinate system constants.
/// X: Lateral (-1 left, +1 right), 0 = midline
/// Y: Superior/Inferior (+1 up, -1 down)
/// Z: Anterior/Posterior (-1 front, +1 back)
/// </summary>
public static class NeuroCoord
{
    // Anatomical direction signs
    public const float ANTERIOR = -1f;
    public const float POSTERIOR = +1f;
    public const float SUPERIOR = +1f;
    public const float INFERIOR = -1f;
    public const float LEFT_LATERAL = -1f;
    public const float RIGHT_LATERAL = +1f;
    
    // Convert voxel to normalized coordinates
    public static Vector3 VoxelToNorm(int vx, int vy, int vz, int W, int H, int D)
    {
        float nx = (vx / (float)(W - 1)) * 2f - 1f;
        float ny = 1f - (vy / (float)(H - 1)) * 2f;  // Y flipped
        float nz = (vz / (float)(D - 1)) * 2f - 1f;
        return new Vector3(nx, ny, nz);
    }
    
    // Convert normalized to voxel coordinates
    public static (int vx, int vy, int vz) NormToVoxel(float nx, float ny, float nz, int W, int H, int D)
    {
        int vx = (int)Math.Clamp((nx + 1f) * 0.5f * (W - 1), 0, W - 1);
        int vy = (int)Math.Clamp((1f - ny) * 0.5f * (H - 1), 0, H - 1);  // Y flipped
        int vz = (int)Math.Clamp((nz + 1f) * 0.5f * (D - 1), 0, D - 1);
        return (vx, vy, vz);
    }
}
```

### 2. NeuralRenderer (JavaScript)

```javascript
// Unified coordinate system
// X: Lateral (-1 left, +1 right), 0 = midline
// Y: Superior/Inferior (+1 up, -1 down)
// Z: Anterior/Posterior (-1 front, +1 back)

const ANTERIOR = -1;
const POSTERIOR = +1;
const SUPERIOR = +1;
const INFERIOR = -1;

// Voxel to world coordinates
function voxelToWorld(hemi, vx, vy, vz) {
    // Normalized coordinates
    let nx = (vx / (w - 1)) * 2 - 1;
    let ny = 1 - (vy / (h - 1)) * 2;  // Y flipped for superior = +
    let nz = (vz / (d - 1)) * 2 - 1;
    
    // Apply hemisphere offset
    // Left hemisphere: X in [-1, 0], Right hemisphere: X in [0, +1]
    if (hemi === 0) {
        nx = nx * 0.5 - 0.5;  // Map to [-1, 0]
    } else {
        nx = nx * 0.5 + 0.5;  // Map to [0, +1]
    }
    
    // Scale to world units
    let wx = nx * scaleX * spacing * w * 0.5;
    let wy = ny * scaleY * spacing * h * 0.5;
    let wz = nz * scaleZ * spacing * d * 0.5;
    
    return { x: wx, y: wy, z: wz };
}
```

### 3. Camera Default Position

```javascript
// Default camera: looking at brain from right-anterior-superior
// This gives a standard 3/4 view showing frontal and right hemisphere
camera.position.set(
    +80,   // Right of midline (positive X)
    +60,   // Above brain (positive Y = superior)
    -100   // In front of brain (negative Z = anterior)
);
camera.lookAt(0, 0, 0);  // Look at brain center
```

## Migration from Old Convention

The previous convention had inconsistencies:

| Aspect | Old | New |
|--------|-----|-----|
| Z-axis | Z=0 anterior, Z=1 posterior | Z=-1 anterior, Z=+1 posterior |
| Y-axis | Y=0 dorsal, Y=1 ventral | Y=+1 superior, Y=-1 inferior |
| Normalization | [0, 1] range | [-1, +1] range |

### Migration Function

```csharp
// Convert old [0,1] coordinates to new [-1,+1] coordinates
public static Vector3 MigrateOldToNew(float oldX, float oldY, float oldZ)
{
    float newX = oldX * 2f - 1f;              // [0,1] -> [-1,+1]
    float newY = -(oldY * 2f - 1f);           // [0,1] -> [-1,+1], then flip
    float newZ = oldZ * 2f - 1f;              // [0,1] -> [-1,+1]
    return new Vector3(newX, newY, newZ);
}
```

## Validation

To verify correct coordinate system implementation:

1. **Frontal lobe** should be at negative Z (anterior)
2. **Occipital lobe** should be at positive Z (posterior)
3. **Top of brain** should be at positive Y (superior)
4. **Brainstem** should be at negative Y (inferior)
5. **Left hemisphere** should be at negative X
6. **Right hemisphere** should be at positive X

## References

- Talairach & Tournoux (1988) - Co-planar Stereotaxic Atlas of the Human Brain
- MNI/ICBM coordinate standards
- Standard neuroimaging conventions (RAS+)

---

**Document Version:** 1.0  
**Date:** February 2026  
**Applies to:** NRE v12+
