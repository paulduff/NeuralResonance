using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AdminRouteHandlerUnitTests
{
    [Fact]
    public async Task InputGate_Handler_Applies_Requested_Changes()
    {
        var state = CreateState();

        var result = AdminInputControlRoutes.SetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: false, SpontaneousSpikingEnabled: false),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.True(GetBool(body.RootElement, "applied"));
        Assert.False(state.IsAvatarVisionEnabled());
        Assert.False(state.IsSpontaneousSpikingEnabled());
    }

    [Fact]
    public async Task InputGate_Handler_Rejects_Empty_Request()
    {
        var state = CreateState();

        var result = AdminInputControlRoutes.SetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: null, SpontaneousSpikingEnabled: null),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.NotNull(body);
        Assert.Contains("at least one setting", GetString(body.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VisualAttention_Handler_Rejects_Empty_Saliency()
    {
        var state = CreateState();

        var result = AdminInputControlRoutes.PostVisualAttention(
            new VisualAttentionInputRequest(LeftFieldSaliency: null, RightFieldSaliency: null),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.NotNull(body);
        Assert.Contains("leftFieldSaliency", GetString(body.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VisualAttention_Handler_Accepts_Left_Right_Signals()
    {
        var state = CreateState();

        var result = AdminInputControlRoutes.PostVisualAttention(
            new VisualAttentionInputRequest(LeftFieldSaliency: 0.90f, RightFieldSaliency: 0.10f),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.NotEqual(string.Empty, GetString(body.RootElement, "focusedField"));
    }

    [Fact]
    public async Task Reasoning_And_Telemetry_Handlers_Return_Ok()
    {
        var state = CreateState();

        var schemas = await ExecuteJsonResultAsync(AdminReasoningRoutes.GetSchemas(state, 4));
        Assert.Equal(StatusCodes.Status200OK, schemas.StatusCode);

        var startup = await ExecuteJsonResultAsync(AdminTelemetryRoutes.GetStartupHealth(state, 4));
        Assert.Equal(StatusCodes.Status200OK, startup.StatusCode);

        var ingress = new InputIngressRuntime(new ConfigurationBuilder().Build());
        var transport = await ExecuteJsonResultAsync(AdminTelemetryRoutes.GetTransportStats(state, ingress));
        Assert.Equal(StatusCodes.Status200OK, transport.StatusCode);
        Assert.NotNull(transport.Body);
        Assert.True(TryGetProperty(transport.Body.RootElement, "inputIngress", out var inputIngress));
        Assert.True(TryGetProperty(inputIngress, "object", out _));
    }

    [Fact]
    public async Task Counterfactual_Handler_Rejects_Null_Request()
    {
        var state = CreateState();

        var result = AdminReasoningRoutes.PostCounterfactual(null!, state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.NotNull(body);
        Assert.Contains("payload", GetString(body.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = 0.5f,
                SerotoninLevel = 0.5f,
                AcetylcholineLevel = 0.5f,
                NorepinephrineLevel = 0.5f
            },
            rewardPredictionError: 0.0f,
            attention: new AttentionVector(0.25f, 0.25f, 0.25f, 0.25f));
        return state;
    }

    private static Task<(int StatusCode, JsonDocument? Body)> ExecuteJsonResultAsync(IResult result)
    {
        var status = result is IStatusCodeHttpResult statusResult
            ? statusResult.StatusCode.GetValueOrDefault(StatusCodes.Status200OK)
            : StatusCodes.Status200OK;

        var value = result is IValueHttpResult valueResult
            ? valueResult.Value
            : null;

        if (value is null)
        {
            return Task.FromResult((status, (JsonDocument?)null));
        }

        var payload = JsonSerializer.Serialize(value, value.GetType());
        return Task.FromResult<(int StatusCode, JsonDocument? Body)>((status, JsonDocument.Parse(payload)));
    }

    private static bool GetBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
