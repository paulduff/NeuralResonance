using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class PlaceMemoryTests
{
    [Fact]
    public void Teaching_Event_Produces_Place_Memory_Context()
    {
        var state = new global::SimulationState();
        var teaching = new global::BiologicalTeachingEvent(
            Active: true,
            Kind: "remember",
            ConceptKey: "teaching.shelter.warm.cave",
            Label: "warm cave",
            Category: "shelter",
            Meaning: "The user asked the system to remember the warm cave as shelter.",
            GoalKey: "FindShelter",
            ActionKey: "approach_shelter",
            Reward: 0.26f,
            Success: 0.70f,
            Threat: 0f,
            Valence: 0.26f,
            Salience: 0.68f,
            Confidence: 0.80f,
            Evidence: "test place teaching event");

        state.ObserveBiologicalTeachingEvent(teaching, grammar: null, ["remember", "warm", "cave", "shelter"], tick: 60);

        var json = JsonSerializer.Serialize(state.GetPlaceMemorySnapshot(8));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.GetProperty("Count").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("ActivePlaceKey").GetString()));
        Assert.Equal("shelter", root.GetProperty("ActiveCategory").GetString());
        Assert.True(root.GetProperty("HippocampalPlaceBinding").GetSingle() >= 0f);
        Assert.True(root.GetProperty("RetrosplenialSceneBinding").GetSingle() >= 0f);

        var top = root.GetProperty("Top").EnumerateArray().ToArray();
        Assert.NotEmpty(top);
        Assert.Contains("warm cave", top[0].GetProperty("RecentSummary").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inhabitance_Includes_Active_Place_Memory()
    {
        var state = new global::SimulationState();
        var teaching = new global::BiologicalTeachingEvent(
            Active: true,
            Kind: "remember",
            ConceptKey: "teaching.threat.dark.hall",
            Label: "dark hall",
            Category: "threat",
            Meaning: "The user asked the system to remember the dark hall as dangerous.",
            GoalKey: "AvoidThreat",
            ActionKey: "avoid_threat",
            Reward: -0.20f,
            Success: 0.40f,
            Threat: 0.55f,
            Valence: -0.75f,
            Salience: 0.74f,
            Confidence: 0.78f,
            Evidence: "test threat place teaching event");

        state.ObserveBiologicalTeachingEvent(teaching, grammar: null, ["remember", "dark", "hall", "danger"], tick: 90);

        var json = JsonSerializer.Serialize(state.GetInhabitanceSnapshot());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var placeMemory = root.GetProperty("PlaceMemory");

        Assert.Equal("threat", placeMemory.GetProperty("ActiveCategory").GetString());
        Assert.True(placeMemory.GetProperty("Threat").GetSingle() > 0f);
        Assert.Contains("dark hall", placeMemory.GetProperty("RecentSummary").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}
