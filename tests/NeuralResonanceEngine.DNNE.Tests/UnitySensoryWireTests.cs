using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnitySensoryWireTests
{
    [Fact]
    public void MeasuredUnityFallIsAcceptedWithoutLosingFallenOrientation()
    {
        var request = Read<PhysicalBodyFrameRequest>("body-frame-example.json");
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request, out var descriptor, out var error), error);
        Assert.NotNull(descriptor);
        var articulation = descriptor.Articulation;
        Assert.InRange(articulation.TrunkPitchRadians, -2f, 2f);
        Assert.InRange(articulation.TrunkRollRadians, -2f, 2f);
        var measurements = articulation.Musculoskeletal!;
        var balance = measurements.Balance!;
        Assert.Equal("fallen", balance.Phase);
        Assert.True(Math.Abs(balance.FallRollRadians) > 2f);
        Assert.InRange(balance.CenterOfMassYMeters, 0.05f, 0.5f);
        Assert.Equal(0f, measurements.UprightFraction);
        Assert.False(descriptor.MotorTrainingMode);
    }

    [Fact]
    public void MeasuredUnityFloorContactIsAcceptedBySomaticReceptor()
    {
        var request = Read<SomaticContactFrameRequest>("contact-frame-example.json");
        Assert.True(SomaticContactDescriptor.TryCreate(request, out var descriptor, out var error), error);
        Assert.NotNull(descriptor);
        Assert.True(descriptor.ForceNewtons > 50f);
        Assert.InRange(descriptor.PenetrationMillimeters, 0f, 3f);
        Assert.Equal("avatar_unity_left_foot_contact", descriptor.InputSource);
    }

    private static T Read<T>(string filename)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "UnityWorld", "Qualification", filename);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
