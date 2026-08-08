using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostSurvivalAuthorityBoundaryTests
{
    [Fact]
    public void AvatarMotorProjectionHasNoUrgencyScaleInput()
    {
        var parameterNames = typeof(AvatarKinematics)
            .GetMethod(nameof(AvatarKinematics.ComputeBrainMotorOutput))!
            .GetParameters()
            .Select(static parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("forwardScale", parameterNames);
        Assert.DoesNotContain("urgency", parameterNames);
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    public void RenderedWorldsHaveNoHostUrgencyMotorMultiplier(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        AssertSourceOmits(
            source,
            "ComputeUrgentRunScale",
            "forwardScale",
            "RunSpeedMultiplier");
    }

    [Fact]
    public void WorldHasNoHostAffectOrAutomaticWeaponAuthority()
    {
        var source = ReadSource("src", "NRE.WpfWorldSim", "MainWindow.xaml.cs");

        AssertSourceOmits(
            source,
            "_environmentAnxiety",
            "_environmentShelterNeed",
            "_environmentSleepPressure",
            "_flightPressure",
            "_threatDecayRate",
            "GetWeaponNeedPressure",
            "RegisterFlightEpisode",
            "ConsumeWeaponCharge",
            "WeaponAttackCooldown");
        Assert.Contains("distance <= PredatorStrikeRadius", source, StringComparison.Ordinal);
        Assert.Contains("_sleepState && IsInShelter()", source, StringComparison.Ordinal);
        AssertSourceOmits(source, "_hunger", "_health");
        Assert.Contains("_storedEnergyJoules", source, StringComparison.Ordinal);
        Assert.Contains("_tissueIntegrity", source, StringComparison.Ordinal);
        Assert.Contains("ApplyManipulatorOutput", source, StringComparison.Ordinal);
        Assert.Contains("AvatarPhysicalInteraction.IsWithinEffectorCone", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsumeNearbyPickups", source, StringComparison.Ordinal);
    }

    private static void AssertSourceOmits(string source, params string[] forbiddenSymbols)
    {
        foreach (var symbol in forbiddenSymbols)
        {
            Assert.DoesNotContain(symbol, source, StringComparison.Ordinal);
        }
    }

    private static string ReadSource(params string[] pathParts)
        => File.ReadAllText(Path.Combine([ResolveRepositoryRoot(), .. pathParts]));

    private static string ResolveRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not resolve the DNNE repository root.");
    }
}
