using Nre.UnityWorld;
using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityJointCoordinateTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void TwoActiveAxesUsePackedSlotsSeparatelyFromPhysicalAxes(bool head, int physicalAxis)
    {
        var coordinates = new JointCoordinates(2, head);
        coordinates.ValidateNativeCount(3);
        Assert.Throws<InvalidOperationException>(() => coordinates.ValidateNativeCount(2));
        Assert.Equal(0, coordinates.NativeIndex(0));
        Assert.Equal(1, coordinates.NativeIndex(1));
        Assert.Equal(physicalAxis, coordinates.SecondPhysicalAxis);
        Assert.Equal(2, coordinates.UnusedIndex);
        var force = coordinates.Pack(.25f, -.75f);
        Assert.Equal(3, force.Length);
        Assert.Equal(.25f, force[0]);
        Assert.Equal(-.75f, force[1]);
        Assert.Equal(0, force[2]);
        var measured = new[] { .1f, -.2f, .3f };
        Assert.Equal(-.2f, measured[coordinates.NativeIndex(1)]);
    }

    [Theory]
    [InlineData(true, "XY")]
    [InlineData(false, "XZ")]
    public void AdapterSlotsAgreeWithIndependentlyCapturedEngineRotations(bool head, string axes)
    {
        string repository = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(repository, "UnityWorld", "Qualification")))
            repository = Directory.GetParent(repository)?.FullName ?? throw new InvalidOperationException("Repository not found.");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "UnityWorld", "Qualification", "native-coordinate-probe.json")));
        Assert.Equal("6000.3.19f1", document.RootElement.GetProperty("unity").GetString());
        Assert.True(document.RootElement.GetProperty("passed").GetBoolean());
        var coordinates = new JointCoordinates(2, head);
        var samples = document.RootElement.GetProperty("samples").EnumerateArray()
            .Where(s => s.GetProperty("axes").GetString() == axes).ToArray();
        Assert.Equal(20, samples.Length);
        foreach (var sample in samples)
        {
            coordinates.ValidateNativeCount(sample.GetProperty("nativeCount").GetInt32());
            int slot = sample.GetProperty("slot").GetInt32();
            int direction = sample.GetProperty("direction").GetInt32();
            var rotation = sample.GetProperty("rotationRadians");
            float[] physical = { rotation.GetProperty("x").GetSingle(), rotation.GetProperty("y").GetSingle(), rotation.GetProperty("z").GetSingle() };
            if (slot == coordinates.UnusedIndex)
            {
                Assert.All(physical, angle => Assert.InRange(angle, -.002f, .002f));
                continue;
            }
            int axis = slot == 0 ? 0 : 1;
            int physicalAxis = axis == 0 ? 0 : coordinates.SecondPhysicalAxis;
            int native = coordinates.NativeIndex(axis);
            string operation = sample.GetProperty("operation").GetString()!;
            float minimum = operation == "velocity" ? .001f : operation == "position" ? .15f : .05f;
            Assert.True(direction * physical[physicalAxis] > minimum, $"{axes} {operation} slot {slot} has wrong physical response.");
            float measured = sample.GetProperty("position")[native].GetSingle();
            Assert.InRange(measured - physical[physicalAxis], -.002f, .002f);
            for (int other = 0; other < 3; other++)
                if (other != physicalAxis) Assert.InRange(physical[other], -.002f, .002f);
        }
    }

    [Fact]
    public void HingeAndFixedJointLayoutsDoNotAddSpuriousAxes()
    {
        var hinge = new JointCoordinates(1, false);
        hinge.ValidateNativeCount(1);
        Assert.Equal(new[] { .4f }, hinge.Pack(.4f, .7f));
        Assert.Throws<ArgumentOutOfRangeException>(() => hinge.NativeIndex(1));
        var fixedJoint = new JointCoordinates(0, false);
        fixedJoint.ValidateNativeCount(0);
        Assert.Empty(fixedJoint.Pack(.4f, .7f));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixedJoint.NativeIndex(0));
    }
}
