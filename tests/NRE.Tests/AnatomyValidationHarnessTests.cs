using System.Numerics;
using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

public sealed class AnatomyValidationHarnessTests
{
    [Fact]
    public void Default_Layout_Passes_Biology_Validation_Harness()
    {
        var opt = new NreEngineOptions();
        var report = AnatomyValidationHarness.ValidateCurrentCanon(opt);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Invariants.Where(x => !x.Passed).Select(x => $"{x.Id}: {x.Details}")));
    }

    [Fact]
    public void Default_Layout_Contains_All_Cortical_Regions_In_Both_Hemispheres()
    {
        var opt = new NreEngineOptions();
        var report = AnatomyValidationHarness.ValidateCurrentCanon(opt);

        foreach (var region in RegionIds.AllCorticalRegions)
        {
            Assert.Contains(report.Regions, r => r.Hemisphere == "Left" && r.RegionId == region && r.VoxelCount > 0);
            Assert.Contains(report.Regions, r => r.Hemisphere == "Right" && r.RegionId == region && r.VoxelCount > 0);
        }
    }

    [Fact]
    public void NeuroCoord_Roundtrip_Stays_Close_To_Original_Voxel()
    {
        const int w = 20, h = 20, d = 20;

        for (int x = 0; x < w; x += 3)
        for (int y = 0; y < h; y += 4)
        for (int z = 0; z < d; z += 5)
        {
            Vector3 norm = NeuroCoord.VoxelToNorm(x, y, z, w, h, d);
            var (vx, vy, vz) = NeuroCoord.NormToVoxel(norm.X, norm.Y, norm.Z, w, h, d);

            Assert.InRange(Math.Abs(vx - x), 0, 1);
            Assert.InRange(Math.Abs(vy - y), 0, 1);
            Assert.InRange(Math.Abs(vz - z), 0, 1);
        }
    }
}
