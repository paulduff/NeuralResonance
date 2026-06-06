using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class InhabitanceSnapshotTests
{
    [Fact]
    public void Inhabitance_Snapshot_Braids_Current_Thought_Self_Body_And_Teaching()
    {
        var state = new global::SimulationState();
        var teaching = new global::BiologicalTeachingEvent(
            Active: true,
            Kind: "remember",
            ConceptKey: "teaching.language.red.door",
            Label: "red door",
            Category: "language",
            Meaning: "The user asked the system to remember the red door.",
            GoalKey: "RememberInstruction",
            ActionKey: "encode_language_teaching",
            Reward: 0.26f,
            Success: 0.62f,
            Threat: 0f,
            Valence: 0.26f,
            Salience: 0.62f,
            Confidence: 0.76f,
            Evidence: "test inhabitance teaching event");

        state.ObserveBiologicalTeachingEvent(teaching, grammar: null, ["remember", "red", "door"], tick: 42);

        var json = JsonSerializer.Serialize(state.GetInhabitanceSnapshot());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("CurrentThought", out var currentThought));
        Assert.False(string.IsNullOrWhiteSpace(currentThought.GetString()));
        Assert.True(root.TryGetProperty("SelfStatement", out var selfStatement));
        Assert.False(string.IsNullOrWhiteSpace(selfStatement.GetString()));
        Assert.True(root.TryGetProperty("IdentityThread", out var identityThread));
        Assert.Contains("red door", identityThread.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(root.TryGetProperty("Teaching", out var teachingElement));
        Assert.Equal("remember", teachingElement.GetProperty("LastKind").GetString());
        Assert.Equal("red door", teachingElement.GetProperty("LastLabel").GetString());
        Assert.True(root.GetProperty("Presence").GetSingle() >= 0f);
        Assert.True(root.GetProperty("Continuity").GetSingle() >= 0f);
        Assert.True(root.GetProperty("Embodiment").GetSingle() >= 0f);
    }
}
