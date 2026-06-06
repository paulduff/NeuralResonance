using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarLanguageCommandApiTests
{
    [Fact]
    public void Parse_Language_Command_Result_Reads_Intent_And_Narration()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "mode": "english",
              "tokenCount": 2,
              "brainTokenCount": 6,
              "generatedSpikes": 42,
              "deliveredSpikes": 37,
              "targetInstances": 4,
              "generatedUtterance": "find shelter",
              "pausedDueToSleep": false,
              "grammar": {
                "intent": "survival_statement",
                "mood": "imperative"
              },
              "languageIntent": {
                "commandKey": "language.seek_shelter",
                "motorDirective": "motor_seek",
                "strength": 1.14
              },
              "brainNarration": {
                "utterance": "I am looking for shelter.",
                "sequence": 12,
                "lastUpdatedTick": 345,
                "source": "language.seek_shelter"
              }
            }
            """);

        var result = AvatarControlApi.ParseLanguageCommandResult(document.RootElement);

        Assert.Equal("english", result.Mode);
        Assert.Equal(2, result.TokenCount);
        Assert.Equal(6, result.BrainTokenCount);
        Assert.Equal(42, result.GeneratedSpikes);
        Assert.Equal(37, result.DeliveredSpikes);
        Assert.Equal(4, result.TargetInstances);
        Assert.Equal("find shelter", result.Utterance);
        Assert.False(result.PausedDueToSleep);
        Assert.Equal("survival_statement", result.GrammarIntent);
        Assert.Equal("imperative", result.GrammarMood);
        Assert.Equal("language.seek_shelter", result.CommandKey);
        Assert.Equal("motor_seek", result.MotorDirective);
        Assert.Equal(1.14f, result.Strength, precision: 2);
        Assert.Equal("I am looking for shelter.", result.Narration.Utterance);
        Assert.Equal(12, result.Narration.Sequence);
        Assert.Equal(345, result.Narration.LastUpdatedTick);
        Assert.Equal("language.seek_shelter", result.Narration.Source);
    }

    [Fact]
    public void Try_Read_Brain_Narration_Reads_Frame_State_Language_Block()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "brainBehavior": {
                "language": {
                  "utterance": "I am moving forward.",
                  "sequence": 21,
                  "lastUpdatedTick": 900,
                  "source": "language.move_forward"
                }
              }
            }
            """);

        var found = AvatarControlApi.TryReadBrainNarration(document.RootElement, out var narration);

        Assert.True(found);
        Assert.Equal("I am moving forward.", narration.Utterance);
        Assert.Equal(21, narration.Sequence);
        Assert.Equal(900, narration.LastUpdatedTick);
        Assert.Equal("language.move_forward", narration.Source);
    }
}
