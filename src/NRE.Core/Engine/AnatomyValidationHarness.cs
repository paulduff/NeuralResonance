using System.Numerics;

namespace NRE.Core.Engine;

/// <summary>
/// Biology validation harness for the voxel atlas.
/// It turns the current anatomical layout into measurable region summaries
/// and checks a stable set of spatial invariants against the current canon.
/// </summary>
public static class AnatomyValidationHarness
{
    public static AnatomyValidationReportDto ValidateCurrentCanon(NreEngineOptions opt)
    {
        var left = new NeuralVolume(opt.W, opt.H, opt.D, baseHz: 1f, oscGain: 0f, baseThreshold: 1f, energyMax: 1f);
        var right = new NeuralVolume(opt.W, opt.H, opt.D, baseHz: 1f, oscGain: 0f, baseThreshold: 1f, energyMax: 1f);
        RegionLayout.Apply(left, opt, isLeftHemisphere: true);
        RegionLayout.Apply(right, opt, isLeftHemisphere: false);
        return Validate(left, right, opt);
    }

    public static AnatomyValidationReportDto Validate(NeuralVolume left, NeuralVolume right, NreEngineOptions opt)
    {
        var leftSummaries = BuildRegionSummaries(left, "Left");
        var rightSummaries = BuildRegionSummaries(right, "Right");

        var allSummaries = leftSummaries.Values
            .Concat(rightSummaries.Values)
            .OrderBy(r => r.Hemisphere, StringComparer.Ordinal)
            .ThenBy(r => r.RegionId)
            .ToArray();

        var invariants = new List<AnatomyInvariantResultDto>();
        void Check(string id, string name, bool passed, string details)
            => invariants.Add(new AnatomyInvariantResultDto(id, name, passed, details));

        bool Has(Dictionary<byte, AnatomyRegionSummaryDto> map, byte region) => map.ContainsKey(region);
        AnatomyRegionSummaryDto L(byte region) => leftSummaries[region];

        // Presence checks: these are the minimum biological scaffold.
        foreach (var region in RegionIds.AllCorticalRegions)
        {
            Check($"presence.left.{region}", $"Left {RegionIds.NameOf(region)} present",
                Has(leftSummaries, region),
                Has(leftSummaries, region) ? "Present in left cortical mantle." : "Missing from left hemisphere layout.");

            Check($"presence.right.{region}", $"Right {RegionIds.NameOf(region)} present",
                Has(rightSummaries, region),
                Has(rightSummaries, region) ? "Present in right cortical mantle." : "Missing from right hemisphere layout.");
        }

        foreach (var region in new byte[]
        {
            RegionIds.CorpusCallosum, RegionIds.Thalamus, RegionIds.Hypothalamus,
            RegionIds.Hippocampus, RegionIds.Amygdala, RegionIds.BasalGanglia,
            RegionIds.Cerebellum, RegionIds.Brainstem, RegionIds.Pons
        })
        {
            Check($"presence.left.{region}", $"Left {RegionIds.NameOf(region)} present",
                Has(leftSummaries, region),
                Has(leftSummaries, region) ? "Present in left hemi volume." : "Missing from left hemi volume.");

            Check($"presence.right.{region}", $"Right {RegionIds.NameOf(region)} present",
                Has(rightSummaries, region),
                Has(rightSummaries, region) ? "Present in right hemi volume." : "Missing from right hemi volume.");
        }

        if (Has(leftSummaries, RegionIds.CorpusCallosum) && Has(leftSummaries, RegionIds.Thalamus))
        {
            var cc = L(RegionIds.CorpusCallosum);
            var th = L(RegionIds.Thalamus);
            Check("cc.midline", "Corpus callosum stays near midline",
                MathF.Abs(cc.CentroidNorm.X) <= 0.15f,
                $"CC X={cc.CentroidNorm.X:0.00}; expected close to 0 midline.");
            Check("cc.superior_to_thalamus", "Corpus callosum lies superior to thalamus",
                cc.CentroidNorm.Y > th.CentroidNorm.Y + 0.10f,
                $"CC Y={cc.CentroidNorm.Y:0.00}, thalamus Y={th.CentroidNorm.Y:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.Thalamus) && Has(leftSummaries, RegionIds.Hypothalamus))
        {
            var th = L(RegionIds.Thalamus);
            var hy = L(RegionIds.Hypothalamus);
            Check("thalamus.midline", "Thalamus stays near midline",
                MathF.Abs(th.CentroidNorm.X) <= 0.15f,
                $"Thalamus X={th.CentroidNorm.X:0.00}; expected near 0.");
            Check("hypo.below_thalamus", "Hypothalamus lies inferior to thalamus",
                hy.CentroidNorm.Y < th.CentroidNorm.Y - 0.02f,
                $"Hypothalamus Y={hy.CentroidNorm.Y:0.00}, thalamus Y={th.CentroidNorm.Y:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.Amygdala) && Has(leftSummaries, RegionIds.Hippocampus))
        {
            var am = L(RegionIds.Amygdala);
            var hi = L(RegionIds.Hippocampus);
            Check("hippo.posterior_to_amygdala", "Hippocampus lies posterior to amygdala",
                hi.CentroidNorm.Z > am.CentroidNorm.Z + 0.05f,
                $"Hippocampus Z={hi.CentroidNorm.Z:0.00}, amygdala Z={am.CentroidNorm.Z:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.Thalamus) && Has(leftSummaries, RegionIds.Amygdala))
        {
            var th = L(RegionIds.Thalamus);
            var am = L(RegionIds.Amygdala);
            Check("amygdala.inferior_to_thalamus", "Amygdala lies inferior to thalamus",
                am.CentroidNorm.Y < th.CentroidNorm.Y - 0.05f,
                $"Amygdala Y={am.CentroidNorm.Y:0.00}, thalamus Y={th.CentroidNorm.Y:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.Cerebellum) && Has(leftSummaries, RegionIds.Thalamus))
        {
            var ce = L(RegionIds.Cerebellum);
            var th = L(RegionIds.Thalamus);
            Check("cerebellum.posterior_to_thalamus", "Cerebellum lies posterior to thalamus",
                ce.CentroidNorm.Z > th.CentroidNorm.Z + 0.20f,
                $"Cerebellum Z={ce.CentroidNorm.Z:0.00}, thalamus Z={th.CentroidNorm.Z:0.00}.");
            Check("cerebellum.inferior_to_thalamus", "Cerebellum lies inferior to thalamus",
                ce.CentroidNorm.Y < th.CentroidNorm.Y - 0.20f,
                $"Cerebellum Y={ce.CentroidNorm.Y:0.00}, thalamus Y={th.CentroidNorm.Y:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.Brainstem) && Has(leftSummaries, RegionIds.Thalamus))
        {
            var bs = L(RegionIds.Brainstem);
            var th = L(RegionIds.Thalamus);
            Check("brainstem.inferior_to_thalamus", "Brainstem lies inferior to thalamus",
                bs.CentroidNorm.Y < th.CentroidNorm.Y - 0.20f,
                $"Brainstem Y={bs.CentroidNorm.Y:0.00}, thalamus Y={th.CentroidNorm.Y:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.Pons) && Has(leftSummaries, RegionIds.Brainstem))
        {
            var po = L(RegionIds.Pons);
            var bs = L(RegionIds.Brainstem);
            Check("pons.anterior_to_brainstem", "Pons lies anterior to brainstem",
                po.CentroidNorm.Z < bs.CentroidNorm.Z - 0.02f,
                $"Pons Z={po.CentroidNorm.Z:0.00}, brainstem Z={bs.CentroidNorm.Z:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.PrecentralGyrus) && Has(leftSummaries, RegionIds.PostcentralGyrus))
        {
            var pre = L(RegionIds.PrecentralGyrus);
            var post = L(RegionIds.PostcentralGyrus);
            Check("precentral.anterior_to.postcentral", "Precentral gyrus lies anterior to postcentral gyrus",
                pre.CentroidNorm.Z < post.CentroidNorm.Z - 0.03f,
                $"Precentral Z={pre.CentroidNorm.Z:0.00}, postcentral Z={post.CentroidNorm.Z:0.00}.");
        }

        if (Has(leftSummaries, RegionIds.SuperiorFrontalGyrus) && Has(leftSummaries, RegionIds.SuperiorOccipital))
        {
            var frontal = L(RegionIds.SuperiorFrontalGyrus);
            var occ = L(RegionIds.SuperiorOccipital);
            Check("frontal.anterior_to.occipital", "Frontal cortex lies anterior to occipital cortex",
                frontal.CentroidNorm.Z < occ.CentroidNorm.Z - 0.30f,
                $"Frontal Z={frontal.CentroidNorm.Z:0.00}, occipital Z={occ.CentroidNorm.Z:0.00}.");
        }

        // Stable bilateral parity check: both hemi volumes should currently carry the same atlas counts.
        foreach (var region in leftSummaries.Keys.Union(rightSummaries.Keys).OrderBy(x => x))
        {
            leftSummaries.TryGetValue(region, out var ls);
            rightSummaries.TryGetValue(region, out var rs);
            bool passed = ls is not null && rs is not null && ls.VoxelCount == rs.VoxelCount;
            Check($"parity.{region}", $"Left/right voxel parity for {RegionIds.NameOf(region)}", passed,
                ls is null || rs is null
                    ? "Region missing from one hemisphere summary."
                    : $"Left voxels={ls.VoxelCount}, right voxels={rs.VoxelCount}.");
        }

        int failed = invariants.Count(x => !x.Passed);
        int passedCount = invariants.Count - failed;

        return new AnatomyValidationReportDto(
            opt.W, opt.H, opt.D, failed == 0, passedCount, failed,
            invariants.ToArray(), allSummaries);
    }

    private static Dictionary<byte, AnatomyRegionSummaryDto> BuildRegionSummaries(NeuralVolume vol, string hemisphere)
    {
        var builders = new Dictionary<byte, RegionBuilder>();

        for (int i = 0; i < vol.Total; i++)
        {
            if (!vol.Active[i]) continue;
            byte region = vol.RegionId[i];
            if (region == RegionIds.Inert) continue;

            if (!builders.TryGetValue(region, out var b))
            {
                b = new RegionBuilder();
                builders[region] = b;
            }

            var (x, y, z) = vol.FromIndex(i);
            var norm = NeuroCoord.VoxelToNorm(x, y, z, vol.W, vol.H, vol.D);
            b.Add(norm);
        }

        return builders.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToSummary(hemisphere, kvp.Key));
    }

    private sealed class RegionBuilder
    {
        private int _count;
        private Vector3 _sum;
        private Vector3 _min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        private Vector3 _max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public void Add(Vector3 v)
        {
            _count++;
            _sum += v;
            _min = Vector3.Min(_min, v);
            _max = Vector3.Max(_max, v);
        }

        public AnatomyRegionSummaryDto ToSummary(string hemisphere, byte regionId)
        {
            var centroid = _count > 0 ? _sum / _count : Vector3.Zero;
            var min = _count > 0 ? _min : Vector3.Zero;
            var max = _count > 0 ? _max : Vector3.Zero;
            return new AnatomyRegionSummaryDto(hemisphere, regionId, RegionIds.NameOf(regionId), _count, centroid, min, max);
        }
    }
}
