using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

/// <summary>
/// Behavioral guards for the unified per-region cerebellar microzone circuit:
/// climbing-fiber (variance) error drives Marr-Albus conjunction learning, the
/// learned Purkinje weight is the corrective inhibition, and a predictable region
/// habituates.
/// </summary>
public sealed class CerebellumTests
{
    private const int TotalVoxels = 160; // voxelScale = 16 px/voxel -> 10

    private static List<(byte hemi, int idx, byte region)> Spikes(byte region, int count)
    {
        var list = new List<(byte, int, byte)>(count);
        for (int i = 0; i < count; i++)
            list.Add(((byte)0, i, region));
        return list;
    }

    [Fact]
    public void StableRegion_StaysUninhibited()
    {
        var cb = new Cerebellum();
        var steady = Spikes(region: 11, count: 6); // constant activity -> low variance

        for (int i = 0; i < 200; i++)
            cb.Step(dt: 0.02f, steady, TotalVoxels);

        // A perfectly predictable region produces no climbing-fiber error, so no
        // corrective inhibition is learned.
        Assert.True(cb.GetRegionInhibition(11) < 0.02f, $"stable region should stay uninhibited, got {cb.GetRegionInhibition(11)}");
    }

    [Fact]
    public void FluctuatingRegion_LearnsCorrectiveInhibition()
    {
        var cb = new Cerebellum();
        var high = Spikes(region: 11, count: 12);
        var low = Spikes(region: 11, count: 2);

        for (int i = 0; i < 300; i++)
            cb.Step(dt: 0.02f, (i % 2 == 0) ? high : low, TotalVoxels);

        float inhib = cb.GetRegionInhibition(11);
        Assert.InRange(inhib, 0.05f, 0.30f); // learned, and clamped to the engine range
    }

    [Fact]
    public void Region_Habituates_WhenErrorStops()
    {
        var cb = new Cerebellum();
        var high = Spikes(region: 11, count: 12);
        var low = Spikes(region: 11, count: 2);

        // Phase 1: instability -> the cerebellum learns to damp the region.
        for (int i = 0; i < 300; i++)
            cb.Step(dt: 0.02f, (i % 2 == 0) ? high : low, TotalVoxels);
        float trained = cb.GetRegionInhibition(11);
        Assert.True(trained > 0.05f, $"expected learned inhibition before habituation, got {trained}");

        // Phase 2: the region becomes predictable -> the correction habituates away.
        var steady = Spikes(region: 11, count: 6);
        for (int i = 0; i < 400; i++)
            cb.Step(dt: 0.02f, steady, TotalVoxels);
        float habituated = cb.GetRegionInhibition(11);

        Assert.True(habituated < trained * 0.5f, $"inhibition should habituate: trained={trained}, after={habituated}");
    }

    [Fact]
    public void Inhibition_StaysBoundedAndFinite_OverManySteps()
    {
        var cb = new Cerebellum();
        var rng = new Random(7);

        for (int step = 0; step < 300; step++)
        {
            var spikes = new List<(byte, int, byte)>();
            int regions = rng.Next(1, 6);
            for (int r = 0; r < regions; r++)
            {
                byte region = (byte)rng.Next(1, 28);
                int count = rng.Next(0, 20);
                for (int s = 0; s < count; s++) spikes.Add(((byte)0, s, region));
            }

            cb.Step(dt: 0.02f, spikes, TotalVoxels);
        }

        float global = cb.GetGlobalInhibition();
        Assert.False(float.IsNaN(global) || float.IsInfinity(global));
        Assert.InRange(global, 0f, 0.30f);

        for (byte r = 0; r <= 27; r++)
        {
            float inhib = cb.GetRegionInhibition(r);
            Assert.False(float.IsNaN(inhib) || float.IsInfinity(inhib));
            Assert.InRange(inhib, 0f, 0.30f);
        }
    }

    [Fact]
    public void DeepNuclei_AreDisinhibitedByPurkinjeOutput()
    {
        var cb = new Cerebellum();
        var high = Spikes(region: 11, count: 12);
        var low = Spikes(region: 11, count: 2);

        for (int i = 0; i < 300; i++)
            cb.Step(dt: 0.02f, (i % 2 == 0) ? high : low, TotalVoxels);

        var snap = cb.Snapshot();
        Assert.InRange(snap.DeepNucleiOutput, 0f, 0.6f);
        Assert.True(snap.PurkinjeOutput >= 0f);
        // Deep nuclei output is the tonic level minus Purkinje inhibition.
        Assert.True(snap.DeepNucleiOutput <= 0.6f - snap.PurkinjeOutput + 1e-4f);
    }
}
