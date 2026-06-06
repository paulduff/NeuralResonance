namespace NRE.Core.Engine;

/// <summary>
/// Builds biologically-structured synaptic connectivity.
/// Extracted from NreEngine.BuildInitialConnectivityBiological.
/// Contains all anatomical tract wiring rules.
/// </summary>
public sealed class ConnectomeBuilder
{
    private readonly NreEngineOptions _opt;
    private readonly Random _rng;

    public ConnectomeBuilder(NreEngineOptions opt, Random rng) { _opt = opt; _rng = rng; }

    /// <summary>
    /// Build all intra-hemispheric connectivity for a single hemisphere.
    /// Uses its own Random for thread safety — can be called in parallel for L/R.
    /// </summary>
    public void BuildHemisphere(SynapseMap map, NeuralVolume vol, int seed)
    {
        var rng = new Random(seed);
        int n = _opt.W * _opt.H * _opt.D;
        static bool IsCortex(byte r) => RegionIds.IsCortex(r);

        var regionPool = new List<int>[RegionIds.MaxRegionIdUsed + 1];
        for (int r = 0; r < regionPool.Length; r++) regionPool[r] = new List<int>(512);
        for (int idx = 0; idx < n; idx++)
        {
            var (x, y, z) = vol.FromIndex(idx);
            if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
            byte r = vol.RegionId[vol.IndexOf(x, y, z)];
            if (r < regionPool.Length) regionPool[r].Add(idx);
        }

        int jitter = Math.Max(0, _opt.ProjectionTopographicJitterVoxels);
        int maxAttempts = Math.Max(1, _opt.ProjectionMaxPlacementAttempts);

        // Helper closures capture rng, vol, regionPool, _opt
        int PickLocal(int x, int y, int z, byte region, int radius)
        {
            for (int a = 0; a < maxAttempts; a++)
            {
                int dx = rng.Next(-radius, radius + 1), dy = rng.Next(-radius, radius + 1), dz = rng.Next(-radius, radius + 1);
                if (dx == 0 && dy == 0 && dz == 0) continue;
                int xx = x + dx, yy = y + dy, zz = z + dz;
                if (xx < 0 || yy < 0 || zz < 0 || xx >= _opt.W || yy >= _opt.H || zz >= _opt.D) continue;
                if (!vol.Active[vol.IndexOf(xx, yy, zz)] || vol.RegionId[vol.IndexOf(xx, yy, zz)] != region) continue;
                return vol.IndexOf(xx, yy, zz);
            }
            var pool = region < regionPool.Length ? regionPool[region] : null;
            return pool is { Count: > 0 } ? pool[rng.Next(pool.Count)] : -1;
        }

        int PickTopo(int x, int y, int z, byte targetRegion)
        {
            for (int a = 0; a < maxAttempts; a++)
            {
                int dx = jitter == 0 ? 0 : rng.Next(-jitter, jitter + 1);
                int dy = jitter == 0 ? 0 : rng.Next(-jitter, jitter + 1);
                int dz = jitter == 0 ? 0 : rng.Next(-jitter, jitter + 1);
                int xx = Math.Clamp(x + dx, 0, _opt.W - 1), yy = Math.Clamp(y + dy, 0, _opt.H - 1), zz = Math.Clamp(z + dz, 0, _opt.D - 1);
                if (!vol.Active[vol.IndexOf(xx, yy, zz)] || vol.RegionId[vol.IndexOf(xx, yy, zz)] != targetRegion) continue;
                return vol.IndexOf(xx, yy, zz);
            }
            var pool = targetRegion < regionPool.Length ? regionPool[targetRegion] : null;
            return pool is { Count: > 0 } ? pool[rng.Next(pool.Count)] : -1;
        }

        float Wt(bool inh) { float w = _opt.InitialWeightMean + ((float)rng.NextDouble() - 0.5f) * 2f * _opt.InitialWeightJitter; return inh ? -Math.Abs(w) : w; }
        int Delay(int min, int max) => rng.Next(min, max + 1);
        int D2() => Math.Max(2, _opt.MinDelayTicks);
        int D3() => Math.Max(3, _opt.MinDelayTicks);

        byte SampleNeighbor(byte cr)
        {
            var neigh = RegionIds.Neighbors(cr);
            if (neigh.Length == 0) return cr;
            return rng.NextDouble() < 0.15 ? cr : neigh[rng.Next(neigh.Length)];
        }

        void ProjectAll(byte srcRegion, byte dstRegion, float prob, bool inh, int fanout = 1)
        {
            if (regionPool[srcRegion].Count == 0 || regionPool[dstRegion].Count == 0) return;
            foreach (int pre in regionPool[srcRegion])
            {
                if (prob < 1.0f && rng.NextDouble() > prob) continue;
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < fanout; i++)
                { int post = PickTopo(x, y, z, dstRegion); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(inh)); }
            }
        }

        void ProjectFrom(byte[] srcRegions, byte dstRegion, float prob, bool inh)
        {
            if (regionPool[dstRegion].Count == 0) return;
            foreach (byte cr in srcRegions)
            { var pres = regionPool[cr]; if (pres.Count == 0) continue; foreach (int pre in pres) { if (rng.NextDouble() > prob) continue; var (x, y, z) = vol.FromIndex(pre); int post = PickTopo(x, y, z, dstRegion); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 3), Wt(inh)); } }
        }

        // ===================================================================
        // 1) Local microcircuits
        // ===================================================================
        for (int idx = 0; idx < n; idx++)
        {
            var (x, y, z) = vol.FromIndex(idx);
            if (!vol.Active[vol.IndexOf(x, y, z)]) continue;
            byte r = vol.RegionId[vol.IndexOf(x, y, z)];
            int fan = IsCortex(r) ? _opt.LocalFanoutPerNeuron : _opt.SubcorticalLocalFanoutPerNeuron;
            for (int i = 0; i < fan; i++)
            {
                int post = PickLocal(x, y, z, r, _opt.LocalRadiusVoxels);
                if (post < 0) continue;
                map.Add(idx, post, Delay(_opt.MinDelayTicks, _opt.MaxDelayTicks), Wt(rng.NextDouble() < _opt.LocalInhibitoryFraction));
            }
        }

        // ===================================================================
        // 2) Thalamo-cortical relay
        // ===================================================================
        if (regionPool[1].Count > 0)
        {
            byte[] targets = { RegionIds.InferiorOccipital, RegionIds.SuperiorTemporalGyrus, RegionIds.PostcentralGyrus, RegionIds.PrecentralGyrus, RegionIds.MiddleFrontalGyrus };
            float[] probs = { 0.28f, 0.18f, 0.18f, 0.18f, 0.18f };
            foreach (int pre in regionPool[1])
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.ThalamoCorticalFanout; i++)
                {
                    float u = (float)rng.NextDouble(); float acc = 0; byte tr = targets[^1];
                    for (int j = 0; j < targets.Length; j++) { acc += probs[j]; if (u <= acc) { tr = targets[j]; break; } }
                    int post = PickTopo(x, y, z, tr); if (post < 0) continue;
                    map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(false));
                }
            }
        }

        // 3) Cortico-thalamic feedback
        if (regionPool[1].Count > 0) foreach (byte cr in RegionIds.AllCorticalRegions)
        { var pres = regionPool[cr]; if (pres.Count == 0) continue; foreach (int pre in pres) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < _opt.CorticoThalamicFanout; i++) { int post = PickTopo(x, y, z, 1); if (post < 0) continue; map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(false)); } } }

        // 4) Cortico-cortical long-range
        foreach (byte cr in RegionIds.AllCorticalRegions)
        {
            var pres = regionPool[cr]; if (pres.Count == 0) continue;
            foreach (int pre in pres)
            {
                var (x, y, z) = vol.FromIndex(pre);
                for (int i = 0; i < _opt.CorticoCorticalLongRangeFanout; i++)
                {
                    byte tr;
                    bool isFrontal = cr is RegionIds.SuperiorFrontalGyrus or RegionIds.MiddleFrontalGyrus or RegionIds.InferiorFrontalGyrus;
                    if (isFrontal) tr = rng.NextDouble() < 0.60 ? SampleNeighbor(cr) : RegionIds.AllCorticalRegions[rng.Next(RegionIds.AllCorticalRegions.Length)];
                    else tr = rng.NextDouble() < 0.75 ? SampleNeighbor(cr) : RegionIds.FrontalAssociation[rng.Next(RegionIds.FrontalAssociation.Length)];
                    int post = PickTopo(x, y, z, tr); if (post < 0) continue;
                    map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(false));
                }
            }
        }

        // 5) Hippocampus ↔ cortex
        if (regionPool[5].Count > 0)
        {
            byte[] ht = { RegionIds.MiddleFrontalGyrus, RegionIds.InferiorFrontalGyrus, RegionIds.AngularGyrus, RegionIds.SuperiorTemporalGyrus, RegionIds.InferiorOccipital };
            foreach (int pre in regionPool[5]) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < _opt.HippocampoCorticalFanout; i++) { int post = PickTopo(x, y, z, ht[rng.Next(ht.Length)]); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 3), Wt(false)); } }
            foreach (byte cr in ht) { var pres = regionPool[cr]; if (pres.Count == 0) continue; foreach (int pre in pres) { var (x, y, z) = vol.FromIndex(pre); int post = PickTopo(x, y, z, 5); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 3), Wt(false)); } }
        }

        // 6) Amygdala → cortex
        if (regionPool[4].Count > 0)
        {
            byte[] at = { RegionIds.InferiorFrontalGyrus, RegionIds.MiddleFrontalGyrus, RegionIds.SuperiorTemporalGyrus, RegionIds.PostcentralGyrus, RegionIds.PrecentralGyrus, RegionIds.AngularGyrus };
            foreach (int pre in regionPool[4]) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < _opt.AmygdaloCorticalFanout; i++) { int post = PickTopo(x, y, z, at[rng.Next(at.Length)]); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 3), Wt(false)); } }
        }

        // 7) BG loop: cortex→BG(exc), BG→thalamus(inh)
        if (regionPool[3].Count > 0)
        {
            foreach (byte cr in new byte[] { RegionIds.SuperiorFrontalGyrus, RegionIds.MiddleFrontalGyrus, RegionIds.InferiorFrontalGyrus, RegionIds.PrecentralGyrus, RegionIds.PostcentralGyrus })
            { var pres = regionPool[cr]; if (pres.Count == 0) continue; foreach (int pre in pres) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < _opt.BasalGangliaFanout; i++) { int post = PickTopo(x, y, z, 3); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(false)); } } }
            if (regionPool[1].Count > 0) foreach (int pre in regionPool[3]) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < _opt.BasalGangliaFanout; i++) { int post = PickTopo(x, y, z, 1); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(true)); } }
        }

        // 8) Cerebellum → M1
        if (regionPool[27].Count > 0 && regionPool[11].Count > 0)
            foreach (int pre in regionPool[27]) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < _opt.CerebelloMotorFanout; i++) { int post = PickTopo(x, y, z, 11); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 3), Wt(false)); } }

        // 9) Corticopontocerebellar
        if (regionPool[8].Count > 0)
        {
            foreach (byte cr in new byte[] { RegionIds.PrecentralGyrus, RegionIds.PostcentralGyrus, RegionIds.SuperiorFrontalGyrus, RegionIds.MiddleFrontalGyrus, RegionIds.SuperiorParietal, RegionIds.InferiorOccipital })
            { var pres = regionPool[cr]; if (pres.Count == 0) continue; foreach (int pre in pres) { var (x, y, z) = vol.FromIndex(pre); int post = PickTopo(x, y, z, 8); if (post >= 0) map.Add(pre, post, Delay(D2(), _opt.MaxDelayTicks + 2), Wt(false)); } }
            if (regionPool[27].Count > 0) foreach (int pre in regionPool[8]) { var (x, y, z) = vol.FromIndex(pre); for (int i = 0; i < 2; i++) { int post = PickTopo(x, y, z, 27); if (post >= 0) map.Add(pre, post, Delay(Math.Max(1, _opt.MinDelayTicks), _opt.MaxDelayTicks + 1), Wt(false)); } }
        }

        // 10) Brainstem
        if (regionPool[7].Count > 0)
        {
            ProjectAll(7, 1, 1.0f, false); // BS→Thal
            if (regionPool[8].Count > 0) { ProjectAll(7, 8, 1.0f, false); ProjectAll(8, 7, 1.0f, false); } // BS↔Pons
            ProjectAll(7, 27, 1.0f, false); // BS→Cereb (climbing fibers)
        }

        // 11) Hypothalamus
        if (regionPool[2].Count > 0)
        {
            ProjectAll(4, 2, 1.0f, false); // Amy→Hyp
            ProjectAll(5, 2, 0.3f, false); // Hipp→Hyp
            ProjectAll(2, 7, 1.0f, false); // Hyp→BS
            ProjectAll(2, 8, 1.0f, false); // Hyp→Pons
        }

        // 12) Amygdala bidirectional
        if (regionPool[4].Count > 0)
        {
            ProjectFrom(new byte[] { RegionIds.InferiorFrontalGyrus, RegionIds.MiddleFrontalGyrus, RegionIds.SuperiorTemporalGyrus, RegionIds.InferiorOccipital, RegionIds.InferiorTemporalGyrus }, 4, 0.25f, false);
            ProjectAll(5, 4, 0.3f, false); // Hipp→Amy
            ProjectAll(4, 7, 1.0f, false); // Amy→BS
        }

        // === WIRING AUDIT FIXES ===
        ProjectAll(11, 7, 1.0f, false, fanout: 2); // P1.1 M1→BS (corticospinal)
        ProjectAll(27, 7, 1.0f, false);              // P1.2 Cereb→BS
        ProjectAll(3, 7, 0.5f, true);                // P1.3 BG→BS (pedunculopontine, inh)
        ProjectAll(4, 5, 0.3f, false);                // P2.1 Amy→Hipp
        ProjectFrom(new byte[] { RegionIds.SuperiorFrontalGyrus, RegionIds.MiddleFrontalGyrus, RegionIds.InferiorFrontalGyrus }, 7, 0.3f, false); // P2.2 PFC→BS
        ProjectFrom(new byte[] { RegionIds.PostcentralGyrus, RegionIds.SuperiorTemporalGyrus, RegionIds.InferiorOccipital }, 7, 0.25f, false); // P2.3 Sensory→BS
        ProjectFrom(new byte[] { RegionIds.MiddleFrontalGyrus, RegionIds.InferiorFrontalGyrus }, 2, 0.2f, true); // P2.4 PFC→Hyp (inh)
        ProjectAll(7, 2, 0.3f, false);                // P2.5 BS→Hyp
        ProjectAll(1, 3, 0.3f, false);                // P3.1 Thal→BG

        // P3.2 BS→Cortex (ascending reticular)
        if (regionPool[7].Count > 0)
        {
            byte[] asc = { RegionIds.PrecentralGyrus, RegionIds.SuperiorFrontalGyrus, RegionIds.MiddleFrontalGyrus };
            foreach (int pre in regionPool[7])
            { var (x, y, z) = vol.FromIndex(pre); if (rng.NextDouble() > 0.2) continue; int post = PickTopo(x, y, z, asc[rng.Next(asc.Length)]); if (post >= 0) map.Add(pre, post, Delay(D3(), _opt.MaxDelayTicks + 3), Wt(false)); }
        }
    }
}
