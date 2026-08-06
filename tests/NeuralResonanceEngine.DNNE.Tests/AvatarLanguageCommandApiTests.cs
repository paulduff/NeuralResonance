using System.Text.Json;
using System.Net;
using System.Net.Http;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarLanguageCommandApiTests
{
    [Fact]
    public void Frame_Path_Uses_The_Control_Program_Incremental_Dispatch_Contract()
    {
        var path = AvatarControlApi.GetFramePath(1234, includeConnectome: false);

        Assert.Equal("/api/v1/frame?dispatch_since_ms=1234&include_connectome=false", path);
    }

    [Fact]
    public void Sleep_State_Uses_Only_The_Neuronal_Decoder()
    {
        using var neuronal = JsonDocument.Parse(
            """{"neuronalSleepConsolidation":{"available":true,"stateActive":true,"state":1}}""");
        using var neuronalString = JsonDocument.Parse(
            """{"neuronalSleepConsolidation":{"available":true,"stateActive":true,"state":"Rem"}}""");
        using var legacy = JsonDocument.Parse(
            """{"sleepMemory":{"isSleeping":true},"sleepState":"sleeping"}""");

        Assert.True(AvatarJson.IsSleepingState(neuronal.RootElement));
        Assert.True(AvatarJson.IsSleepingState(neuronalString.RootElement));
        Assert.False(AvatarJson.IsSleepingState(legacy.RootElement));
    }

    [Fact]
    public void Control_Plane_Secret_Uses_A_Constant_Time_Comparison()
    {
        Assert.True(NreControlPlaneSecurity.IsAuthorized("correct-secret", "correct-secret"));
        Assert.False(NreControlPlaneSecurity.IsAuthorized("wrong-secret", "correct-secret"));
        Assert.False(NreControlPlaneSecurity.IsAuthorized(null, "correct-secret"));
    }

    [Fact]
    public async Task Outcome_Post_Throws_When_Control_Program_Rejects_The_Request()
    {
        using var client = new HttpClient(new StaticResponseHandler(HttpStatusCode.BadRequest, "invalid outcome"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AvatarControlApi.PostOutcomeAsync(client, new Uri("http://localhost:5080"), new AvatarOutcomeTelemetry()));

        Assert.Contains("HTTP 400", error.Message);
        Assert.Contains("invalid outcome", error.Message);
    }

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

    private sealed class StaticResponseHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
    }
}
