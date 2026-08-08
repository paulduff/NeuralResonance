using System.Text.Json;
using System.Net;
using System.Net.Http;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarTextSightApiTests
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
    public async Task Physical_Body_Frame_Post_Throws_When_Control_Program_Rejects_The_Request()
    {
        using var client = new HttpClient(new StaticResponseHandler(HttpStatusCode.BadRequest, "invalid physical body frame"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AvatarControlApi.PostPhysicalBodyFrameAsync(
                client,
                new Uri("http://localhost:5080"),
                new PhysicalBodyFrameRequest(
                    1, 1, 0f, 0f, 0f, 0f, 0f, 0f,
                    8_000_000f, 1f, 37f, 0.98f, 0.75f, "test")));

        Assert.Contains("HTTP 400", error.Message);
        Assert.Contains("invalid physical body frame", error.Message);
    }

    [Fact]
    public void Text_Renderer_Produces_Valid_Deterministic_Retinal_Frames()
    {
        var lowerCase = AvatarTextSightRenderer.Render("hello", generation: 7, captureTimestampMs: 11);
        var upperCase = AvatarTextSightRenderer.Render("HELLO", generation: 7, captureTimestampMs: 11);
        var different = AvatarTextSightRenderer.Render("WORLD", generation: 8, captureTimestampMs: 12);

        lowerCase.Validate();
        Assert.Equal(AvatarTextSightRenderer.FrameWidth, lowerCase.Width);
        Assert.Equal(AvatarTextSightRenderer.FrameHeight, lowerCase.Height);
        Assert.Equal("Bgra32", lowerCase.PixelFormat);
        Assert.Equal(lowerCase.Pixels, upperCase.Pixels);
        Assert.False(lowerCase.Pixels.SequenceEqual(different.Pixels));
    }

    [Fact]
    public void Retinal_Frame_Has_No_Structured_Language_Metadata()
    {
        var propertyNames = typeof(AvatarSightFrame)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Text", propertyNames);
        Assert.DoesNotContain("Tokens", propertyNames);
        Assert.DoesNotContain("Mode", propertyNames);
        Assert.DoesNotContain("Hemisphere", propertyNames);
        Assert.DoesNotContain("TargetStructure", propertyNames);
        Assert.DoesNotContain("MotorDirective", propertyNames);
    }

    [Fact]
    public async Task Visible_Text_Is_Posted_As_Octet_Stream_To_The_Retinal_Route()
    {
        const string responseBody =
            """{"accepted":true,"dispatchDeferred":true,"blockedByInputGate":false,"generatedSpikes":18,"deliveredSpikes":0,"targetInstances":2,"sampleColumns":24,"sampleRows":12,"onChannelSpikes":10,"offChannelSpikes":8,"meanLuminance":0.8,"meanTemporalChange":0.2}""";
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, responseBody);
        using var client = new HttpClient(handler);
        var frame = AvatarTextSightRenderer.Render("hello", generation: 1, captureTimestampMs: 2);

        var result = await AvatarControlApi.PostRetinalFrameAsync(
            client,
            new Uri("http://localhost:5080"),
            frame,
            AvatarRuntimeDefaults.TypedTextVisualInputSource);

        Assert.True(result.Accepted);
        Assert.Equal(18, result.GeneratedSpikes);
        Assert.NotNull(handler.RequestUri);
        Assert.Equal("/api/v1/admin/input/visual-frame", handler.RequestUri!.AbsolutePath);
        Assert.Contains("inputSource=avatar_text_display", handler.RequestUri.Query, StringComparison.Ordinal);
        Assert.Equal("application/octet-stream", handler.ContentType);
        Assert.Equal(frame.Stride * frame.Height, handler.PayloadLength);
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

    private sealed class RecordingResponseHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ContentType { get; private set; }
        public int PayloadLength { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            PayloadLength = request.Content is null
                ? 0
                : (await request.Content.ReadAsByteArrayAsync(cancellationToken)).Length;
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            };
        }
    }
}
