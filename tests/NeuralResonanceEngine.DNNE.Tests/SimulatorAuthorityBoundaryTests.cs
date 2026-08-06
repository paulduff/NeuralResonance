using System.Reflection;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SimulatorAuthorityBoundaryTests
{
    [Fact]
    public void MazeSimulatorContainsNoHostNavigationOrEscapeAuthority()
    {
        var source = ReadSource("src", "NRE.WpfMazeSim", "MainWindow.xaml.cs");
        AssertSourceOmits(
            source,
            "SpatialNavigationCheckBox",
            "PollSpatialNavigationAsync",
            "/api/v1/navigation/decision",
            "ApplySpatialNavigationDirective",
            "TriggerOrientingStimulus",
            "ResolveEscapeHemisphere",
            "UpdateProgressAndRecoverIfStuck");
    }

    [Fact]
    public void WorldSimulatorContainsNoHostSteeringOrNoProgressAuthority()
    {
        var source = ReadSource("src", "NRE.WpfWorldSim", "MainWindow.xaml.cs");
        AssertSourceOmits(
            source,
            "ApplyReactiveCollisionAvoidance",
            "TryApplyNavigationFilter",
            "TryCornerSidestep",
            "TryProbeStepAroundObstacle",
            "UpdateProgressAndRecoverIfStuck",
            "HandleStuckDeath",
            "ApplyAboutFaceEscape",
            "ApplyOrientingTargetLock");
    }

    [Fact]
    public void RenderedWorldsProjectNeuronalTurnDirectlyIntoBodyHeading()
    {
        foreach (var project in new[] { "NRE.WpfMazeSim", "NRE.WpfWorldSim" })
        {
            var source = ReadSource("src", project, "MainWindow.xaml.cs");
            Assert.Contains("PublishActionOutput", source, StringComparison.Ordinal);
            Assert.Contains(
                "AvatarKinematics.AdvanceHeading(_avatarHeadingDeg, turnRateDeg, dt)",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain("bodyTurnRateDeg =", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedWallSensorExposesPhysicalTransductionOnly()
    {
        var publicMethods = typeof(AvatarWallSensing)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();

        Assert.Equal([nameof(AvatarWallSensing.ProximityFromRay)], publicMethods);
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
