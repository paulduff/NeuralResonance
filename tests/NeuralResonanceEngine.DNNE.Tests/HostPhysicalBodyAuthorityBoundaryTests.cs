using System.Reflection;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostPhysicalBodyAuthorityBoundaryTests
{
    [Fact]
    public void PhysicalBodyContractCannotPrescribeNeuralActivity()
    {
        var properties = typeof(PhysicalBodyFrameRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] forbidden =
        [
            "SourceStructure", "TargetStructure", "Hemisphere", "Intensity",
            "BurstCount", "Pattern", "IsFeedback", "IncludeVestibular", "IncludeCerebellar",
            "Hunger", "Health", "PainLevel", "ContactLevel", "LeftMotorDrive", "RightMotorDrive"
        ];
        Assert.All(forbidden, name => Assert.DoesNotContain(name, properties));
    }

    [Theory]
    [InlineData(StructureId.ProprioceptiveAfferents, "ProprioceptiveAfferents")]
    [InlineData(StructureId.VestibularAfferents, "VestibularAfferents")]
    [InlineData(StructureId.VisceralAfferents, "VisceralAfferents")]
    public void PhysicalAfferentsAreRealNeuronalServices(StructureId structure, string projectDirectory)
    {
        var root = ResolveRepositoryRoot();
        Assert.True(File.Exists(Path.Combine(
            root,
            "Structures",
            projectDirectory,
            $"NeuralResonanceEngine.Structures.{projectDirectory}.csproj")));

        var program = File.ReadAllText(Path.Combine(root, "Structures", projectDirectory, "Program.cs"));
        Assert.Contains($"StructureId.{structure}", program, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyBodyAuthoritySymbolsAreDeleted()
    {
        var root = ResolveRepositoryRoot();
        var control = File.ReadAllText(Path.Combine(root, "ControlProgram", "Program.cs"));
        var avatar = File.ReadAllText(Path.Combine(root, "src", "NRE.SimAvatar", "AvatarControlApi.cs"));

        Assert.DoesNotContain("/api/v1/admin/input/body-state", control, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildBodyStateStimulusSpikes", control, StringComparison.Ordinal);
        Assert.DoesNotContain("BodyStateRuntime", control, StringComparison.Ordinal);
        Assert.DoesNotContain("PostBodyStateAsync", avatar, StringComparison.Ordinal);
        Assert.Contains("/api/v1/admin/input/body-frame", control, StringComparison.Ordinal);
        Assert.Contains("PostPhysicalBodyFrameAsync", avatar, StringComparison.Ordinal);
    }

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
