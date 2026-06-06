using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

public sealed class SensoryRelayAndMotorTests
{
    [Fact]
    public void Motor_Output_Uses_Arms_And_Legs_Not_Only_Shoulders()
    {
        var opt = new NreEngineOptions
        {
            UiPublishEveryNSteps = 1
        };

        var engine = new NreEngine(opt, seed: 98765);
        engine.Start();
        engine.SetVisualStimulus(0.85f, 5.5f, 1.10f, enabled: true);
        engine.SetAuditoryStimulus(0.68f, 180.0f, enabled: true);

        float maxShoulder = 0f;
        float maxElbow = 0f;
        float maxHip = 0f;
        float maxKnee = 0f;

        for (int step = 0; step < 360; step++)
        {
            engine.Step(1f / 60f);
            var b = engine.GetBodyState();
            maxShoulder = MathF.Max(maxShoulder, MathF.Max(MathF.Abs(b.ShoulderL), MathF.Abs(b.ShoulderR)));
            maxElbow = MathF.Max(maxElbow, MathF.Max(MathF.Abs(b.ElbowL), MathF.Abs(b.ElbowR)));
            maxHip = MathF.Max(maxHip, MathF.Max(MathF.Abs(b.HipL), MathF.Abs(b.HipR)));
            maxKnee = MathF.Max(maxKnee, MathF.Max(MathF.Abs(b.KneeL), MathF.Abs(b.KneeR)));
        }

        Assert.True(maxShoulder > 0.01f, $"Expected shoulder movement, got {maxShoulder:0.000}.");
        Assert.True(maxElbow > 0.01f, $"Expected elbow movement, got {maxElbow:0.000}.");
        Assert.True(maxHip > 0.005f || maxKnee > 0.005f, $"Expected lower-body movement, got hip={maxHip:0.000}, knee={maxKnee:0.000}.");
    }
}
