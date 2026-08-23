using NRE.WorldSim;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class WorldTerrainResolutionTests
{
    [Fact]
    public void EveryGeneratedHeightLevelRepresentsQuarterOfAMetre()
    {
        var terrain = new WorldTerrain(317);

        for (var x = 0; x < WorldTerrain.Size; x += 11)
        {
            for (var z = 0; z < WorldTerrain.Size; z += 13)
            {
                var expected = (terrain.HeightAtCell(x, z) * 0.25) - 0.125;
                Assert.Equal(expected, terrain.SurfaceHeightAtCell(x, z), 8);
            }
        }
    }

    [Fact]
    public void GeneratedTerrainRetainsMetreScaleVerticalRelief()
    {
        var terrain = new WorldTerrain(317);
        var minimum = int.MaxValue;
        var maximum = int.MinValue;

        for (var x = 0; x < WorldTerrain.Size; x++)
        {
            for (var z = 0; z < WorldTerrain.Size; z++)
            {
                minimum = Math.Min(minimum, terrain.HeightAtCell(x, z));
                maximum = Math.Max(maximum, terrain.HeightAtCell(x, z));
            }
        }

        Assert.True(minimum < WorldTerrain.SeaLevelHeightUnits);
        Assert.True(
            maximum >= 10 * WorldTerrain.HeightUnitsPerMeter,
            $"Expected a 10 metre summit, generated {maximum * WorldTerrain.HeightUnitMeters:F2} metres.");
        Assert.True(
            (maximum - minimum) * WorldTerrain.HeightUnitMeters >= 10.0,
            $"Expected at least 10 metres of relief, generated {(maximum - minimum) * WorldTerrain.HeightUnitMeters:F2} metres.");
    }

    [Fact]
    public void ForwardProbeReportsRiseInQuarterMetreIncrements()
    {
        var terrain = new WorldTerrain(317);
        var half = (WorldTerrain.Size - 1) * 0.5;

        for (var worldX = -half + 1.0; worldX < half - 1.0; worldX += 0.25)
        {
            for (var worldZ = -half + 1.0; worldZ < half - 1.0; worldZ += 0.25)
            {
                if (terrain.IsWater(worldX, worldZ) ||
                    !terrain.TryProbeRise(worldX, worldZ, 1.0, 0.0, 0.85, out var rise) ||
                    rise.RiseMeters > 1.0)
                {
                    continue;
                }

                Assert.True(rise.RiseMeters > 0.0);
                Assert.Equal(
                    Math.Round(rise.RiseMeters / WorldTerrain.HeightUnitMeters),
                    rise.RiseMeters / WorldTerrain.HeightUnitMeters,
                    8);
                Assert.Equal(rise.CurrentSurfaceY + rise.RiseMeters, rise.TargetSurfaceY, 8);
                return;
            }
        }

        Assert.Fail("Generated terrain contained no suitable forward rise to probe.");
    }

    [Fact]
    public void OrdinarySlopesAreExpressedAsQuarterMetreGrades()
    {
        var terrain = new WorldTerrain(317);
        var half = (WorldTerrain.Size - 1) * 0.5;

        for (var x = 1; x < WorldTerrain.Size - 1; x++)
        {
            for (var z = 1; z < WorldTerrain.Size - 1; z++)
            {
                if (terrain.IsCliffBetweenCells(x, z, x + 1, z))
                {
                    continue;
                }

                var worldX = x - half;
                var worldZ = z - half;
                var samples = Enumerable.Range(0, 5)
                    .Select(index => terrain.SurfaceAt(worldX + (index * 0.25), worldZ))
                    .ToArray();
                var distinct = samples.Distinct().Count();
                var largestStep = samples.Zip(samples.Skip(1), static (left, right) => Math.Abs(right - left)).Max();
                if (distinct < 3 || largestStep > WorldTerrain.HeightUnitMeters + 0.000001)
                {
                    continue;
                }

                Assert.All(samples, surface => Assert.Equal(
                    Math.Round((surface + WorldTerrain.HalfHeightUnitMeters) / WorldTerrain.HeightUnitMeters),
                    (surface + WorldTerrain.HalfHeightUnitMeters) / WorldTerrain.HeightUnitMeters,
                    8));
                return;
            }
        }

        Assert.Fail("Generated terrain contained no quarter-metre graded slope.");
    }

    [Fact]
    public void AbruptHeightChangesRemainPhysicalCliffs()
    {
        var terrain = new WorldTerrain(317);
        var half = (WorldTerrain.Size - 1) * 0.5;

        for (var x = 1; x < WorldTerrain.Size - 1; x++)
        {
            for (var z = 1; z < WorldTerrain.Size - 1; z++)
            {
                if (!terrain.IsCliffBetweenCells(x, z, x + 1, z))
                {
                    continue;
                }

                var boundaryX = (x - half) + 0.5;
                var worldZ = z - half;
                var left = terrain.SurfaceAt(boundaryX - 0.000001, worldZ);
                var right = terrain.SurfaceAt(boundaryX + 0.000001, worldZ);
                Assert.True(Math.Abs(right - left) >=
                    WorldTerrain.CliffThresholdHeightUnits * WorldTerrain.HeightUnitMeters);
                return;
            }
        }

        Assert.Fail("Generated terrain contained no cliff transition.");
    }
}
