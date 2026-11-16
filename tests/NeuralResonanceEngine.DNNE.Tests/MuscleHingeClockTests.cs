using Nre.UnityWorld;
using static Nre.UnityWorld.MuscleHingeWire;

namespace NeuralResonanceEngine.DNNE.Tests;

public class MuscleHingeClockTests
{
    private static (Request Request, Reply Current) Step(int resolution)
    {
        double h = StepMsAt(resolution);
        var current = new Reply { version = MuscleHingeWire.Version, sessionId = Guid.NewGuid().ToString("N"), sequence = 25, physicsStep = 7,
            physicsStepMs = h, physicalTimeMs = 7 * h, bodyFingerprint = new string('A', 64),
            plantFingerprint = new string('B', 64), stateHash = new string('C', 64), lastAppliedEffortId = Guid.NewGuid().ToString("N") };
        return (new Request { version = MuscleHingeWire.Version, sessionId = current.sessionId, sequence = 26, expectedStep = 7,
            operation = "step", bodyFingerprint = current.bodyFingerprint, plantFingerprint = current.plantFingerprint,
            stateHash = current.stateHash, effortId = Guid.NewGuid().ToString("N"), startTimeMs = 7 * h, endTimeMs = 8 * h,
            tensions = [new Tension { muscleId = "m0", axialNewtons = 1 }, new Tension { muscleId = "m1", axialNewtons = 0 }] }, current);
    }

    [Theory]
    [InlineData(-1, 512)]
    [InlineData(0, 1024)]
    [InlineData(1, 2048)]
    public void Exact_Clock_Preserves_Common_End_And_Accepts_One_Step(int resolution, int steps)
    {
        var (request, current) = Step(resolution);
        Assert.Equal(1024 * StepMs, steps * StepMsAt(resolution));
        Assert.Equal((double)StepSecondsAt(resolution) * 1000, (double)current.physicsStepMs);
        Validate(request, current);
        Assert.Equal(7, current.physicsStep); // Validation cannot advance the body.
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Rejects_Nominal_Decimal_Instead_Of_Actual_Native_Endpoint(int resolution)
    {
        var (request, current) = Step(resolution);
        request.endTimeMs = 8 * .125 / Math.Pow(2, resolution);
        Assert.Throws<InvalidOperationException>(() => Validate(request, current));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Nonmoving_Read_Cannot_Advance_Any_Resolution(int resolution)
    {
        var (request, current) = Step(resolution);
        request.operation = "inspect"; request.effortId = ""; request.tensions = [];
        Assert.Throws<InvalidOperationException>(() => Validate(request, current));
        request.endTimeMs = request.startTimeMs;
        Validate(request, current);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(2)]
    public void Rejects_Unsupported_Resolution(int resolution) =>
        Assert.Throws<InvalidOperationException>(() => StepSecondsAt(resolution));

    [Fact]
    public void Rejects_Unrecognized_Current_Physical_Clock()
    {
        var (request, current) = Step(0); current.physicsStepMs = .125;
        Assert.Throws<InvalidOperationException>(() => Validate(request, current));
    }
}
