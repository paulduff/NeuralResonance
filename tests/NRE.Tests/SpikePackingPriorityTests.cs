using System.Reflection;
using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

public class SpikePackingPriorityTests
{
    [Fact]
    public void PackSpikes_Preserves_Callosum_And_Cerebellum_When_Downsampling()
    {
        var engine = new NreEngine(new NreEngineOptions(), seed: 12345);

        var cortex = new List<(byte hemi, int idx)>(4096);
        var cc = new List<(byte hemi, int idx)>(512);
        var cereb = new List<(byte hemi, int idx)>(512);

        CollectByRegion(engine.Left, hemi: 0, cortex, cc, cereb);
        CollectByRegion(engine.Right, hemi: 1, cortex, cc, cereb);

        Assert.NotEmpty(cortex);
        Assert.NotEmpty(cc);
        Assert.NotEmpty(cereb);

        var spikes = new List<(byte hemi, int idx)>(24000);
        for (int i = 0; i < 22000; i++)
            spikes.Add(cortex[i % cortex.Count]);
        for (int i = 0; i < 1200; i++)
            spikes.Add(cc[i % cc.Count]);
        for (int i = 0; i < 1200; i++)
            spikes.Add(cereb[i % cereb.Count]);

        var mi = typeof(NreEngine).GetMethod("PackSpikes", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);

        var packed = (PackedPoints)mi!.Invoke(engine, new object[] { spikes, 500 })!;
        Assert.True(packed.Count > 0);
        Assert.NotNull(packed.Data);
        Assert.True(packed.Data.Length >= packed.Count * 6);

        int ccCount = 0, cerebCount = 0;
        for (int i = 0; i < packed.Count; i++)
        {
            byte region = packed.Data[i * 6 + 5];
            if (region == RegionIds.CorpusCallosum) ccCount++;
            else if (region == RegionIds.Cerebellum) cerebCount++;
        }

        Assert.True(ccCount >= 10, $"Expected >=10 callosal spikes in packed frame, got {ccCount}.");
        Assert.True(cerebCount >= 14, $"Expected >=14 cerebellar spikes in packed frame, got {cerebCount}.");
    }

    private static void CollectByRegion(
        NeuralVolume vol,
        byte hemi,
        List<(byte hemi, int idx)> cortex,
        List<(byte hemi, int idx)> cc,
        List<(byte hemi, int idx)> cereb)
    {
        for (int idx = 0; idx < vol.Total; idx++)
        {
            if (!vol.Active[idx]) continue;
            byte r = vol.RegionId[idx];
            if (RegionIds.IsCortex(r)) cortex.Add((hemi, idx));
            else if (r == RegionIds.CorpusCallosum) cc.Add((hemi, idx));
            else if (r == RegionIds.Cerebellum) cereb.Add((hemi, idx));
        }
    }
}
