namespace NRE.Core.Engine;

/// <summary>
/// Anatomical region layout — places brain regions into a NeuralVolume.
/// Extracted from NreEngine.ApplyRegionLayout for reuse and testability.
/// 
/// Implements a simplified voxel neuroanatomy:
/// - Cortex as a thin outer mantle partitioned into gyri
/// - Subcortical nuclei as ovoid deep structures
/// - Sizes scaled by relative human volumes
/// </summary>
public static class RegionLayout
{
    /// <summary>
    /// Apply anatomical region assignments to a neural volume.
    /// This is the brain atlas — it defines where each region lives in the voxel grid.
    /// </summary>
    public static void Apply(NeuralVolume vol, NreEngineOptions opt, bool isLeftHemisphere)
    {
        int w = opt.W, h = opt.H, d = opt.D;
        
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f, cz = (d - 1) * 0.5f;
        float rx = w * 0.48f, ry = h * 0.44f, rz = d * 0.46f;

        // Cortex mantle thickness
        float cortexInner = 1.0f - (2.2f / MathF.Max(8f, (rx + ry + rz) / 3f));

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        for (int z = 0; z < d; z++)
        {
            float nx = (x - cx) / (rx + 1e-6f);
            float ny = (y - cy) / (ry + 1e-6f);
            float nz = (z - cz) / (rz + 1e-6f);
            float r2 = nx * nx + ny * ny + nz * nz;

            if (r2 > 1.0f)
            {
                vol.Active[vol.IndexOf(x, y, z)] = false;
                vol.RegionId[vol.IndexOf(x, y, z)] = 255;
                continue;
            }

            vol.Active[vol.IndexOf(x, y, z)] = true;
            float r = MathF.Sqrt(r2);

            if (r >= cortexInner)
            {
                vol.RegionId[vol.IndexOf(x, y, z)] = AssignCorticalRegion(nx, ny, nz, opt);
            }
            else
            {
                vol.RegionId[vol.IndexOf(x, y, z)] = AssignSubcorticalRegion(nx, ny, nz, r, opt);
            }
        }
        
        // Paint corpus callosum voxels — superior midline arch
        PaintCorpusCallosum(vol, opt);
    }
    
    /// <summary>
    /// Paint CC voxels as a midline arch band for callosal relay.
    /// </summary>
    private static void PaintCorpusCallosum(NeuralVolume vol, NreEngineOptions opt)
    {
        int w = opt.W, h = opt.H, d = opt.D;
        int xMid = w / 2;
        int ccHalfWidth = Math.Max(1, w / 16);
        
        int zMin = (int)(d * 0.14f);
        int zMax = (int)(d * 0.86f);
        if (zMax <= zMin) { zMin = 0; zMax = d - 1; }
        
        int yEndPosterior = (int)(h * 0.26f);
        int yEndAnterior  = (int)(h * 0.20f); // less downward curve at anterior
        int yMid = (int)(h * 0.14f);
        int yHalf = Math.Max(3, (int)(h * 0.08f));
        
        int zMidPt = (zMin + zMax) / 2;
        float zSpan = Math.Max(1.0f, (zMax - zMin) * 0.5f);
        
        for (int z = zMin; z <= zMax; z++)
        {
            float t = (z - zMidPt) / zSpan;
            t = Math.Clamp(t, -1f, 1f);
            
            int yEnd = (t >= 0f) ? yEndAnterior : yEndPosterior;
            float yf = yMid + (yEnd - yMid) * (t * t);
            int yCenter = (int)MathF.Round(yf);
            
            int y0 = Math.Clamp(yCenter - yHalf, 0, h - 1);
            int y1 = Math.Clamp(yCenter + yHalf, 0, h - 1);
            
            for (int y = y0; y <= y1; y++)
            for (int x = Math.Max(0, xMid - ccHalfWidth); x <= Math.Min(w - 1, xMid + ccHalfWidth); x++)
            {
                byte existing = vol.RegionId[vol.IndexOf(x, y, z)];
                if (existing != 255 && !RegionIds.IsCortex(existing)) continue;
                vol.Active[vol.IndexOf(x, y, z)] = true;
                vol.RegionId[vol.IndexOf(x, y, z)] = RegionIds.CorpusCallosum;
            }
        }
    }

    private static byte AssignCorticalRegion(float nx, float ny, float nz, NreEngineOptions opt)
    {
        // Frontal lobe: anterior (nz < -0.1)
        if (nz < -0.10f)
        {
            if (nz > -0.35f)
            {
                // Precentral gyrus (primary motor) — just anterior to central sulcus
                return RegionIds.PrecentralGyrus;
            }

            // Frontal gyri by height
            if (ny < -0.15f) return RegionIds.SuperiorFrontalGyrus;
            if (ny < 0.20f)  return RegionIds.MiddleFrontalGyrus;
            return RegionIds.InferiorFrontalGyrus;
        }

        // Inferior parietal bridge: explicit ventral-lateral strip so region 19
        // remains represented in the canonical atlas.
        if (nz >= 0.10f && nz < 0.34f && ny > -0.04f && ny < 0.22f && MathF.Abs(nx) > 0.16f)
            return RegionIds.InferiorParietal;

        // Parietal lobe: posterior-superior (nz >= -0.1, nz < 0.35, ny < 0.1)
        if (nz < 0.35f && ny < 0.10f)
        {
            if (nz < 0.05f) return RegionIds.PostcentralGyrus; // just posterior to central sulcus

            if (ny < -0.20f) return RegionIds.SuperiorParietal;
            // Inferior parietal sits ventral/lateral to superior parietal and bridges
            // toward temporal cortex. Keep a dedicated band so region 19 is always present.
            if (ny > -0.03f || MathF.Abs(nx) > 0.52f) return RegionIds.InferiorParietal;
            if (nz < 0.20f)  return RegionIds.SupramarginalGyrus;
            return RegionIds.AngularGyrus;
        }

        // Temporal lobe: inferior-lateral (ny > 0.1)
        if (ny > 0.10f)
        {
            if (ny < 0.35f) return RegionIds.SuperiorTemporalGyrus;
            if (ny < 0.60f) return RegionIds.MiddleTemporalGyrus;
            return RegionIds.InferiorTemporalGyrus;
        }

        // Occipital lobe: posterior (nz >= 0.35)
        if (ny < 0.0f) return RegionIds.SuperiorOccipital;
        return RegionIds.InferiorOccipital;
    }

    private static byte AssignSubcorticalRegion(float nx, float ny, float nz, float r, NreEngineOptions opt)
    {
        // Subcortical structures positioned by relative anatomy.
        // Deepest/most medial structures first.

        // Brainstem: inferior-posterior-medial (stem shape)
        if (ny > 0.15f && nz > 0.10f && MathF.Abs(nx) < 0.25f && r < 0.55f)
            return RegionIds.Brainstem;

        // Pons: just anterior to brainstem
        if (ny > 0.10f && nz > -0.05f && nz < 0.25f && MathF.Abs(nx) < 0.25f && r < 0.50f)
            return RegionIds.Pons;

        // Cerebellum: posterior-inferior, wider than brainstem
        if (ny > 0.25f && nz > 0.20f && r < 0.65f)
            return RegionIds.Cerebellum;

        // Thalamus: central ovoid
        if (MathF.Abs(nx) < 0.22f && MathF.Abs(ny) < 0.18f && MathF.Abs(nz) < 0.18f && r < 0.35f)
            return RegionIds.Thalamus;

        // Hypothalamus: below thalamus
        if (MathF.Abs(nx) < 0.18f && ny > 0.0f && ny < 0.25f && MathF.Abs(nz) < 0.15f && r < 0.35f)
            return RegionIds.Hypothalamus;

        // Basal ganglia: lateral to thalamus
        if (MathF.Abs(nx) > 0.15f && MathF.Abs(nx) < 0.40f && MathF.Abs(ny) < 0.20f && MathF.Abs(nz) < 0.25f && r < 0.50f)
            return RegionIds.BasalGanglia;

        // Amygdala: anterior temporal pole, deep
        if (ny > 0.10f && nz < -0.15f && MathF.Abs(nx) > 0.15f && r < 0.50f)
            return RegionIds.Amygdala;

        // Hippocampus: medial temporal, curved
        if (ny > 0.0f && MathF.Abs(nx) < 0.25f && nz > -0.10f && nz < 0.25f && r < 0.45f)
            return RegionIds.Hippocampus;

        // Default deep tissue: assign to nearest plausible region
        // If deep and anterior: frontal white matter → treat as thalamus relay
        if (r < 0.40f)
            return RegionIds.Thalamus;

        // Otherwise: deep white matter, assign to nearest cortical region
        return AssignCorticalRegion(nx, ny, nz, opt);
    }
}
