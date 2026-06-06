using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class BiologicalTeachingLoopTests
{
    [Fact]
    public void Teaching_Label_Binds_To_Semantic_Episodic_And_Dopamine_Memory()
    {
        var state = new global::SimulationState();
        var teaching = new global::BiologicalTeachingEvent(
            Active: true,
            Kind: "label",
            ConceptKey: "teaching.food.safe.berry",
            Label: "safe berry",
            Category: "food",
            Meaning: "The user labeled safe berry as food.",
            GoalKey: "FindFood",
            ActionKey: "approach_food",
            Reward: 0.18f,
            Success: 0.62f,
            Threat: 0f,
            Valence: 0.18f,
            Salience: 0.62f,
            Confidence: 0.76f,
            Evidence: "test teaching event");

        var runtime = state.ObserveBiologicalTeachingEvent(teaching, grammar: null, ["that", "is", "food"], tick: 12);

        Assert.True(runtime.Active);
        Assert.Equal("label", runtime.LastKind);
        Assert.Equal("teaching.food.safe.berry", runtime.LastConceptKey);
        Assert.Equal("food", runtime.LastCategory);
        Assert.True(runtime.SemanticConceptCount > 0);
        Assert.True(runtime.EpisodicEventCount > 0);
        Assert.True(runtime.DopamineTraceCount > 0);

        var semanticJson = JsonSerializer.Serialize(state.GetSemanticMemorySnapshot(8));
        var episodicJson = JsonSerializer.Serialize(state.GetEpisodicMemorySnapshot(8));

        Assert.Contains("safe berry", semanticJson);
        Assert.Contains("teaching", episodicJson);
    }

    [Fact]
    public void Corrective_Teaching_Produces_Negative_Reward_Signal()
    {
        var state = new global::SimulationState();
        var teaching = new global::BiologicalTeachingEvent(
            Active: true,
            Kind: "correction",
            ConceptKey: "teaching.feedback.recent.action",
            Label: "recent action",
            Category: "feedback",
            Meaning: "Corrective teaching feedback was associated with recent action.",
            GoalKey: "FollowFeedback",
            ActionKey: "adjust_recent_action",
            Reward: -0.48f,
            Success: 0.18f,
            Threat: 0.30f,
            Valence: -0.78f,
            Salience: 0.72f,
            Confidence: 0.66f,
            Evidence: "test correction event");

        var runtime = state.ObserveBiologicalTeachingEvent(teaching, grammar: null, ["no", "wrong"], tick: 20);

        Assert.Equal("correction", runtime.LastKind);
        Assert.True(runtime.LastReward < 0f);
        Assert.True(runtime.LastValence < 0f);

        var dopamineJson = JsonSerializer.Serialize(state.GetDopamineLearningSnapshot(8));
        Assert.Contains("adjust_recent_action", dopamineJson);
    }
}
