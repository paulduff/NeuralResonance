using System.Net;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class InputGatesAndVisualDispatchTests
{
    [Fact]
    public void Input_Gates_Update_Runtime_And_State()
    {
        var state = CreateState();

        var applied = state.TrySetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: false, SpontaneousSpikingEnabled: false),
            out var runtime,
            out var error);

        Assert.True(applied);
        Assert.Null(error);
        Assert.False(runtime.AvatarVisionEnabled);
        Assert.False(runtime.SpontaneousSpikingEnabled);
        Assert.False(state.IsAvatarVisionEnabled());
        Assert.False(state.IsSpontaneousSpikingEnabled());
    }

    [Fact]
    public void Input_Gates_Reject_NoOp_Request()
    {
        var state = CreateState();

        var applied = state.TrySetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: null, SpontaneousSpikingEnabled: null),
            out _,
            out var error);

        Assert.False(applied);
        Assert.Contains("At least one setting is required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Avatar_Input_Source_Classification_Is_Stable()
    {
        Assert.True(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("avatar_vision")));
        Assert.True(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("avatar-object")));
        Assert.True(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("editor_webcam")));
        Assert.False(AdminInputSource.IsAvatarSource(AdminInputSource.Normalize("world_map_editor")));
    }

    [Fact]
    public async Task Visual_Dispatch_Fallback_Merges_Left_And_Right()
    {
        var attemptedHemispheres = new List<string?>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async request =>
        {
            var hemisphere = await ReadHemisphereAsync(request);
            attemptedHemispheres.Add(hemisphere);

            return hemisphere switch
            {
                null => JsonResponse(HttpStatusCode.NotFound, """{"error":"No active service instances found for V1 (both)." }"""),
                "L" => JsonResponse(HttpStatusCode.OK, """{"generatedSpikes":8,"deliveredSpikes":3,"targetInstances":1,"attentionFocusField":"left","attentionFocusHemisphere":"L","attentionFocusConfidence":0.62}"""),
                "R" => JsonResponse(HttpStatusCode.OK, """{"generatedSpikes":9,"deliveredSpikes":6,"targetInstances":1,"attentionFocusField":"right","attentionFocusHemisphere":"R","attentionFocusConfidence":0.71}"""),
                _ => JsonResponse(HttpStatusCode.BadRequest, """{"error":"unexpected hemisphere"}""")
            };
        }));
        var client = new VisualInputDispatchClient(httpClient);

        var outcome = await client.DispatchWithHemisphereFallbackAsync(
            new Uri("http://localhost:5080"),
            new VisualInputRequest(
                Pattern: "VideoFrame",
                Intensity: 0.8f,
                BurstCount: 24,
                TargetStructure: "V1",
                SourceStructure: "Retina",
                Hemisphere: null,
                LeftFieldSaliency: 0.4f,
                RightFieldSaliency: 0.6f,
                UseAttentionRouting: true,
                InputSource: "avatar_vision"),
            ex => VisualInputDispatchClient.ShouldRetryHemisphereFallback(ex, "V1"));

        Assert.True(outcome.FallbackAttempted);
        Assert.Equal(new string?[] { null, "L", "R" }, attemptedHemispheres);
        Assert.Equal(17, outcome.Response.GeneratedSpikes);
        Assert.Equal(9, outcome.Response.DeliveredSpikes);
        Assert.Equal(2, outcome.Response.TargetInstances);
        Assert.Equal("right", outcome.Response.AttentionFocusField);
        Assert.Equal("R", outcome.Response.AttentionFocusHemisphere);
    }

    [Fact]
    public async Task Visual_Dispatch_Fallback_Uses_Partial_Recovery_When_One_Side_Fails()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async request =>
        {
            var hemisphere = await ReadHemisphereAsync(request);
            return hemisphere switch
            {
                null => JsonResponse(HttpStatusCode.NotFound, """{"error":"No active service instances found for V1 (both)." }"""),
                "L" => JsonResponse(HttpStatusCode.OK, """{"generatedSpikes":5,"deliveredSpikes":4,"targetInstances":1,"pausedDueToSleep":false}"""),
                "R" => JsonResponse(HttpStatusCode.ServiceUnavailable, """{"error":"instance unhealthy"}"""),
                _ => JsonResponse(HttpStatusCode.BadRequest, """{"error":"unexpected hemisphere"}""")
            };
        }));
        var client = new VisualInputDispatchClient(httpClient);

        var outcome = await client.DispatchWithHemisphereFallbackAsync(
            new Uri("http://localhost:5080"),
            new VisualInputRequest(
                Pattern: "VideoFrame",
                Intensity: 0.8f,
                BurstCount: 24,
                TargetStructure: "V1",
                SourceStructure: "Retina",
                Hemisphere: null,
                LeftFieldSaliency: 0.4f,
                RightFieldSaliency: 0.6f,
                UseAttentionRouting: true,
                InputSource: "avatar_vision"),
            ex => VisualInputDispatchClient.ShouldRetryHemisphereFallback(ex, "V1"));

        Assert.True(outcome.FallbackAttempted);
        Assert.NotNull(outcome.LeftResponse);
        Assert.Null(outcome.RightResponse);
        Assert.NotNull(outcome.RightError);
        Assert.Equal(4, outcome.Response.DeliveredSpikes);
        Assert.Equal(1, outcome.Response.TargetInstances);
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        return state;
    }

    private static async Task<string?> ReadHemisphereAsync(HttpRequestMessage request)
    {
        if (request.Content is null)
        {
            return null;
        }

        var body = await request.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("hemisphere", out var hemisphere) ||
            hemisphere.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return hemisphere.ValueKind == JsonValueKind.String
            ? hemisphere.GetString()
            : null;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
