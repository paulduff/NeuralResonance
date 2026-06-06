using System.Numerics;

namespace NRE.Core.Engine;

/// <summary>
/// Unified Neuroanatomical Coordinate System
/// 
/// Standard Axes (following neuroimaging conventions):
///   X: Lateral     -1 = Left,     +1 = Right,    0 = Midline (midsagittal plane)
///   Y: Vertical    +1 = Superior, -1 = Inferior  (dorsal/ventral)
///   Z: Depth       -1 = Anterior, +1 = Posterior (rostral/caudal)
/// 
/// Anatomical Planes:
///   Sagittal:   Y-Z plane (divides left/right)
///   Coronal:    X-Y plane (divides front/back)
///   Horizontal: X-Z plane (divides top/bottom, also called "axial")
/// 
/// This matches standard RAS+ neuroimaging orientation with Y and Z adjusted
/// for intuitive brain visualization (Y-up, Z-forward-is-anterior).
/// </summary>
public static class NeuroCoord
{
    // ==================== ANATOMICAL DIRECTION CONSTANTS ====================
    
    /// <summary>Anterior direction (toward face/frontal lobe). Negative Z.</summary>
    public const float ANTERIOR = -1f;
    
    /// <summary>Posterior direction (toward back of head/occipital). Positive Z.</summary>
    public const float POSTERIOR = +1f;
    
    /// <summary>Rostral = Anterior (toward nose/beak). Negative Z.</summary>
    public const float ROSTRAL = -1f;
    
    /// <summary>Caudal = Posterior (toward tail/spinal cord). Positive Z.</summary>
    public const float CAUDAL = +1f;
    
    /// <summary>Superior direction (toward top of skull). Positive Y.</summary>
    public const float SUPERIOR = +1f;
    
    /// <summary>Inferior direction (toward chin/base of skull). Negative Y.</summary>
    public const float INFERIOR = -1f;
    
    /// <summary>Dorsal = Superior (toward back/top). Positive Y.</summary>
    public const float DORSAL = +1f;
    
    /// <summary>Ventral = Inferior (toward belly/bottom). Negative Y.</summary>
    public const float VENTRAL = -1f;
    
    /// <summary>Left lateral direction. Negative X.</summary>
    public const float LEFT = -1f;
    
    /// <summary>Right lateral direction. Positive X.</summary>
    public const float RIGHT = +1f;
    
    /// <summary>Medial (toward midline). X approaches 0.</summary>
    public const float MEDIAL = 0f;
    
    // ==================== KEY ANATOMICAL Z-POSITIONS (Normalized) ====================
    // These define standard anterior-posterior positions for brain structures
    
    /// <summary>Frontal pole - most anterior point of brain.</summary>
    public const float Z_FRONTAL_POLE = -0.80f;
    
    /// <summary>Prefrontal cortex - executive functions.</summary>
    public const float Z_PREFRONTAL = -0.64f;
    
    /// <summary>Premotor cortex - movement planning.</summary>
    public const float Z_PREMOTOR = -0.40f;
    
    /// <summary>Central sulcus - motor/sensory boundary.</summary>
    public const float Z_CENTRAL_SULCUS = -0.20f;
    
    /// <summary>Parietal cortex - spatial processing.</summary>
    public const float Z_PARIETAL = +0.10f;
    
    /// <summary>Temporal pole - anterior temporal lobe.</summary>
    public const float Z_TEMPORAL_POLE = -0.30f;
    
    /// <summary>Temporal mid - auditory cortex area.</summary>
    public const float Z_TEMPORAL_MID = 0.00f;
    
    /// <summary>Temporal posterior - language areas (Wernicke's).</summary>
    public const float Z_TEMPORAL_POST = +0.30f;
    
    /// <summary>Occipital cortex - primary visual.</summary>
    public const float Z_OCCIPITAL = +0.60f;
    
    /// <summary>Cerebellum center.</summary>
    public const float Z_CEREBELLUM = +0.70f;
    
    /// <summary>Brainstem (pons, medulla).</summary>
    public const float Z_BRAINSTEM = +0.50f;
    
    // ==================== KEY ANATOMICAL Y-POSITIONS (Normalized) ====================
    // These define standard superior-inferior positions for brain structures
    
    /// <summary>Dorsal cortex - top of brain.</summary>
    public const float Y_DORSAL_CORTEX = +0.70f;
    
    /// <summary>Corpus callosum - interhemispheric connection.</summary>
    public const float Y_CORPUS_CALLOSUM = +0.20f;
    
    /// <summary>Thalamus - central relay station.</summary>
    public const float Y_THALAMUS = 0.00f;
    
    /// <summary>Hypothalamus - autonomic control.</summary>
    public const float Y_HYPOTHALAMUS = -0.15f;
    
    /// <summary>Amygdala - emotion processing.</summary>
    public const float Y_AMYGDALA = -0.30f;
    
    /// <summary>Hippocampus - memory formation.</summary>
    public const float Y_HIPPOCAMPUS = -0.20f;
    
    /// <summary>Cerebellum - below main mass.</summary>
    public const float Y_CEREBELLUM = -0.50f;
    
    /// <summary>Brainstem - connecting to spinal cord.</summary>
    public const float Y_BRAINSTEM = -0.60f;
    
    // ==================== COORDINATE CONVERSION ====================
    
    /// <summary>
    /// Convert voxel coordinates to normalized anatomical coordinates.
    /// </summary>
    /// <param name="vx">Voxel X index [0, W-1]</param>
    /// <param name="vy">Voxel Y index [0, H-1]</param>
    /// <param name="vz">Voxel Z index [0, D-1]</param>
    /// <param name="W">Volume width</param>
    /// <param name="H">Volume height</param>
    /// <param name="D">Volume depth</param>
    /// <returns>Normalized coordinates in range [-1, +1]</returns>
    public static Vector3 VoxelToNorm(int vx, int vy, int vz, int W, int H, int D)
    {
        // X: 0 -> -1 (left), W-1 -> +1 (right)
        float nx = W > 1 ? (vx / (float)(W - 1)) * 2f - 1f : 0f;
        
        // Y: 0 -> +1 (superior), H-1 -> -1 (inferior)
        // Note: Y is FLIPPED because voxel grids index top-down
        float ny = H > 1 ? 1f - (vy / (float)(H - 1)) * 2f : 0f;
        
        // Z: 0 -> -1 (anterior), D-1 -> +1 (posterior)
        float nz = D > 1 ? (vz / (float)(D - 1)) * 2f - 1f : 0f;
        
        return new Vector3(nx, ny, nz);
    }
    
    /// <summary>
    /// Convert normalized anatomical coordinates to voxel indices.
    /// </summary>
    /// <param name="nx">Normalized X [-1, +1]</param>
    /// <param name="ny">Normalized Y [-1, +1]</param>
    /// <param name="nz">Normalized Z [-1, +1]</param>
    /// <param name="W">Volume width</param>
    /// <param name="H">Volume height</param>
    /// <param name="D">Volume depth</param>
    /// <returns>Clamped voxel indices</returns>
    public static (int vx, int vy, int vz) NormToVoxel(float nx, float ny, float nz, int W, int H, int D)
    {
        // X: -1 -> 0, +1 -> W-1
        int vx = (int)Math.Clamp((nx + 1f) * 0.5f * (W - 1), 0, W - 1);
        
        // Y: +1 -> 0, -1 -> H-1 (FLIPPED)
        int vy = (int)Math.Clamp((1f - ny) * 0.5f * (H - 1), 0, H - 1);
        
        // Z: -1 -> 0, +1 -> D-1
        int vz = (int)Math.Clamp((nz + 1f) * 0.5f * (D - 1), 0, D - 1);
        
        return (vx, vy, vz);
    }
    
    /// <summary>
    /// Convert from old [0,1] normalized coordinates to new [-1,+1] coordinates.
    /// Used for migration from legacy code.
    /// </summary>
    /// <param name="oldX">Old X in [0, 1]</param>
    /// <param name="oldY">Old Y in [0, 1] where 0=dorsal, 1=ventral</param>
    /// <param name="oldZ">Old Z in [0, 1] where 0=anterior, 1=posterior</param>
    /// <returns>New normalized coordinates</returns>
    public static Vector3 MigrateOld01ToNew(float oldX, float oldY, float oldZ)
    {
        // X: [0,1] -> [-1,+1]
        float newX = oldX * 2f - 1f;
        
        // Y: [0,1] -> [-1,+1], but old 0=dorsal(top), new +1=superior(top)
        // So we need to flip: old 0 -> new +1, old 1 -> new -1
        float newY = -(oldY * 2f - 1f);
        
        // Z: [0,1] -> [-1,+1], old 0=anterior, new -1=anterior
        // So we flip: old 0 -> new -1, old 1 -> new +1
        float newZ = oldZ * 2f - 1f;
        
        return new Vector3(newX, newY, newZ);
    }
    
    /// <summary>
    /// Convert new [-1,+1] coordinates back to old [0,1] format.
    /// Used for backwards compatibility.
    /// </summary>
    public static (float oldX, float oldY, float oldZ) NewToOld01(Vector3 newCoord)
    {
        float oldX = (newCoord.X + 1f) * 0.5f;
        float oldY = (-newCoord.Y + 1f) * 0.5f;  // Flip Y
        float oldZ = (newCoord.Z + 1f) * 0.5f;
        return (oldX, oldY, oldZ);
    }
    
    // ==================== HEMISPHERE UTILITIES ====================
    
    /// <summary>
    /// Determine which hemisphere a normalized X coordinate belongs to.
    /// </summary>
    /// <param name="nx">Normalized X coordinate</param>
    /// <returns>0 for left hemisphere, 1 for right hemisphere</returns>
    public static int GetHemisphere(float nx)
    {
        return nx < 0 ? 0 : 1;
    }
    
    /// <summary>
    /// Get the medial direction for a hemisphere.
    /// Left hemisphere: medial is toward +X
    /// Right hemisphere: medial is toward -X
    /// </summary>
    public static float MedialDirection(int hemisphere)
    {
        return hemisphere == 0 ? +1f : -1f;
    }
    
    /// <summary>
    /// Get the lateral direction for a hemisphere (opposite of medial).
    /// </summary>
    public static float LateralDirection(int hemisphere)
    {
        return hemisphere == 0 ? -1f : +1f;
    }
    
    /// <summary>
    /// Mirror an X coordinate across the midsagittal plane.
    /// </summary>
    public static float MirrorX(float nx)
    {
        return -nx;
    }
    
    // ==================== ANATOMICAL QUERIES ====================
    
    /// <summary>
    /// Check if a position is in the frontal lobe region (anterior).
    /// </summary>
    public static bool IsFrontal(float nz) => nz < Z_CENTRAL_SULCUS;
    
    /// <summary>
    /// Check if a position is in the parietal region.
    /// </summary>
    public static bool IsParietal(float ny, float nz) => 
        ny > Y_THALAMUS && nz >= Z_CENTRAL_SULCUS && nz < Z_OCCIPITAL;
    
    /// <summary>
    /// Check if a position is in the temporal region.
    /// </summary>
    public static bool IsTemporal(float ny, float nz) => 
        ny < Y_THALAMUS && ny > Y_CEREBELLUM && nz < Z_OCCIPITAL;
    
    /// <summary>
    /// Check if a position is in the occipital region (posterior).
    /// </summary>
    public static bool IsOccipital(float nz) => nz >= Z_OCCIPITAL;
    
    /// <summary>
    /// Check if a position is subcortical (deep structures).
    /// </summary>
    public static bool IsSubcortical(float nx, float ny, float nz)
    {
        float laterality = MathF.Abs(nx);
        return laterality < 0.4f && ny < Y_CORPUS_CALLOSUM && ny > Y_CEREBELLUM;
    }
    
    /// <summary>
    /// Check if a position is in the cerebellum.
    /// </summary>
    public static bool IsCerebellum(float ny, float nz) => 
        ny < Y_HIPPOCAMPUS && nz > Z_TEMPORAL_POST;
    
    /// <summary>
    /// Check if a position is in the brainstem.
    /// </summary>
    public static bool IsBrainstem(float nx, float ny, float nz) =>
        MathF.Abs(nx) < 0.15f && ny < Y_HYPOTHALAMUS && nz > Z_TEMPORAL_MID;
    
    // ==================== DISTANCE UTILITIES ====================
    
    /// <summary>
    /// Compute anatomical distance between two normalized positions.
    /// </summary>
    public static float Distance(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }
    
    /// <summary>
    /// Compute distance from midline (midsagittal plane).
    /// </summary>
    public static float DistanceFromMidline(float nx)
    {
        return MathF.Abs(nx);
    }
    
    /// <summary>
    /// Compute distance from brain center (origin).
    /// </summary>
    public static float DistanceFromCenter(Vector3 pos)
    {
        return pos.Length();
    }
}
