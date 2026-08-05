namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class EmbodiedClosedLoopBenchmarkTests
{
    [Fact]
    public void ProductionAvatarRoundTripChangesTheBrainsNextChoice()
    {
        EmbodiedClosedLoopResult result = EmbodiedClosedLoopBenchmark.Run();

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Criteria));
        Assert.Equal(1f, result.LoopIntegrityScore);
        Assert.Contains("food", result.InitialIntent.GoalKey, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            result.AdaptedIntent.GoalKey.Contains("shelter", StringComparison.OrdinalIgnoreCase) ||
            result.AdaptedIntent.GoalKey.Contains("threat", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(result.InitialIntent.GoalKey, result.AdaptedIntent.GoalKey);
        Assert.True(result.Boundaries.BrainMotorSpikes > 0);
        Assert.True(result.Boundaries.AvatarMotorEvents > 0);
        Assert.True(result.Boundaries.BodyFeedbackReachedBrain);
        Assert.True(result.Boundaries.OutcomeFeedbackReachedBrain);
        Assert.True(result.ActionMemoryCount > 0);
        Assert.True(result.DopamineLearningCount > 0);
    }
}
