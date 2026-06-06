using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace NeuralResonanceEngine.Shared.Contracts;

public sealed class VisualInputDispatchClient
{
    public const string VisualInputPath = "/api/v1/admin/input/visual";

    private readonly HttpClient _httpClient;

    public VisualInputDispatchClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<VisualInputDispatchResponse> DispatchAsync(
        Uri baseUri,
        VisualInputRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            new Uri(baseUri, VisualInputPath),
            request,
            cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new VisualInputHttpException((int)response.StatusCode, payload);
        }

        return ParseResponse(payload);
    }

    public async Task<VisualInputDispatchOutcome> DispatchWithHemisphereFallbackAsync(
        Uri baseUri,
        VisualInputRequest request,
        Func<VisualInputHttpException, bool> shouldRetryFallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(shouldRetryFallback);

        try
        {
            var response = await DispatchAsync(baseUri, request, cancellationToken);
            return new VisualInputDispatchOutcome(
                response,
                FallbackAttempted: false,
                InitialError: null,
                LeftResponse: null,
                RightResponse: null,
                LeftError: null,
                RightError: null);
        }
        catch (VisualInputHttpException initialError) when (request.Hemisphere is null && shouldRetryFallback(initialError))
        {
            VisualInputDispatchResponse? leftResponse = null;
            VisualInputDispatchResponse? rightResponse = null;
            VisualInputHttpException? leftError = null;
            VisualInputHttpException? rightError = null;

            try
            {
                leftResponse = await DispatchAsync(baseUri, request with { Hemisphere = "L" }, cancellationToken);
            }
            catch (VisualInputHttpException ex)
            {
                leftError = ex;
            }

            try
            {
                rightResponse = await DispatchAsync(baseUri, request with { Hemisphere = "R" }, cancellationToken);
            }
            catch (VisualInputHttpException ex)
            {
                rightError = ex;
            }

            if (leftResponse is not null && rightResponse is not null)
            {
                return new VisualInputDispatchOutcome(
                    MergeResponses(leftResponse, rightResponse),
                    FallbackAttempted: true,
                    InitialError: initialError,
                    LeftResponse: leftResponse,
                    RightResponse: rightResponse,
                    LeftError: leftError,
                    RightError: rightError);
            }

            if (leftResponse is not null)
            {
                return new VisualInputDispatchOutcome(
                    leftResponse,
                    FallbackAttempted: true,
                    InitialError: initialError,
                    LeftResponse: leftResponse,
                    RightResponse: null,
                    LeftError: leftError,
                    RightError: rightError);
            }

            if (rightResponse is not null)
            {
                return new VisualInputDispatchOutcome(
                    rightResponse,
                    FallbackAttempted: true,
                    InitialError: initialError,
                    LeftResponse: null,
                    RightResponse: rightResponse,
                    LeftError: leftError,
                    RightError: rightError);
            }

            throw new InvalidOperationException(
                $"Visual input dispatch failed after L/R retry: {FormatHttpError(initialError, 220)} | L retry: {FormatHttpError(leftError, 120)} | R retry: {FormatHttpError(rightError, 120)}");
        }
    }

    public static bool ShouldRetryHemisphereFallback(VisualInputHttpException ex, string targetStructure = "V1")
    {
        if (ex.StatusCode != 404)
        {
            return false;
        }

        var structure = string.IsNullOrWhiteSpace(targetStructure) ? "V1" : targetStructure.Trim();
        return ex.Payload.Contains($"No active service instances found for {structure}", StringComparison.OrdinalIgnoreCase) &&
               ex.Payload.Contains("(both)", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatHttpError(VisualInputHttpException? ex, int maxPayloadChars = 120)
    {
        if (ex is null)
        {
            return "-";
        }

        return $"HTTP {ex.StatusCode}: {TrimPayload(ex.Payload, maxPayloadChars)}";
    }

    private static VisualInputDispatchResponse ParseResponse(string payload)
    {
        var generated = 0;
        var delivered = 0;
        var targetCount = 0;
        var recoveryAttempted = false;
        var recoveryRestarted = 0;
        var recoveryHealthy = 0;
        var recoveryRetriedInstances = 0;
        var pausedDueToSleep = false;
        string? focusField = null;
        string? focusHemisphere = null;
        var focusConfidence = 0f;
        var blockedByInputGate = false;
        string? inputSource = null;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                generated = GetInt(doc.RootElement, "generatedSpikes");
                delivered = GetInt(doc.RootElement, "deliveredSpikes");
                targetCount = GetInt(doc.RootElement, "targetInstances");
                recoveryAttempted = GetBool(doc.RootElement, "recoveryAttempted");
                recoveryRestarted = GetInt(doc.RootElement, "recoveryRestarted");
                recoveryHealthy = GetInt(doc.RootElement, "recoveryHealthy");
                recoveryRetriedInstances = GetInt(doc.RootElement, "recoveryRetriedInstances");
                pausedDueToSleep = GetBool(doc.RootElement, "pausedDueToSleep");
                focusField = GetString(doc.RootElement, "attentionFocusField");
                focusHemisphere = GetString(doc.RootElement, "attentionFocusHemisphere");
                focusConfidence = GetSingle(doc.RootElement, "attentionFocusConfidence");
                blockedByInputGate = GetBool(doc.RootElement, "blockedByInputGate");
                inputSource = GetString(doc.RootElement, "inputSource");
            }
        }
        catch
        {
            // Best-effort response parsing only.
        }

        return new VisualInputDispatchResponse(
            generated,
            delivered,
            targetCount,
            recoveryAttempted,
            recoveryRestarted,
            recoveryHealthy,
            recoveryRetriedInstances,
            pausedDueToSleep,
            focusField,
            focusHemisphere,
            focusConfidence,
            blockedByInputGate,
            inputSource);
    }

    private static VisualInputDispatchResponse MergeResponses(VisualInputDispatchResponse left, VisualInputDispatchResponse right)
    {
        var focus = left;
        if (right.DeliveredSpikes > left.DeliveredSpikes)
        {
            focus = right;
        }
        else if (right.DeliveredSpikes == left.DeliveredSpikes &&
                 right.AttentionFocusConfidence > left.AttentionFocusConfidence)
        {
            focus = right;
        }

        return new VisualInputDispatchResponse(
            GeneratedSpikes: left.GeneratedSpikes + right.GeneratedSpikes,
            DeliveredSpikes: left.DeliveredSpikes + right.DeliveredSpikes,
            TargetInstances: left.TargetInstances + right.TargetInstances,
            RecoveryAttempted: left.RecoveryAttempted || right.RecoveryAttempted,
            RecoveryRestarted: left.RecoveryRestarted + right.RecoveryRestarted,
            RecoveryHealthy: left.RecoveryHealthy + right.RecoveryHealthy,
            RecoveryRetriedInstances: left.RecoveryRetriedInstances + right.RecoveryRetriedInstances,
            PausedDueToSleep: left.PausedDueToSleep && right.PausedDueToSleep,
            AttentionFocusField: focus.AttentionFocusField,
            AttentionFocusHemisphere: focus.AttentionFocusHemisphere,
            AttentionFocusConfidence: Math.Max(left.AttentionFocusConfidence, right.AttentionFocusConfidence),
            BlockedByInputGate: left.BlockedByInputGate || right.BlockedByInputGate,
            InputSource: string.IsNullOrWhiteSpace(focus.InputSource) ? left.InputSource ?? right.InputSource : focus.InputSource);
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static float GetSingle(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return 0f;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            float.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0f;
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

        if (value.ValueKind == JsonValueKind.String &&
            bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
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

    private static string TrimPayload(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (maxLength <= 0 || cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        return $"{cleaned[..maxLength]}...";
    }
}

public sealed record VisualInputDispatchResponse(
    int GeneratedSpikes,
    int DeliveredSpikes,
    int TargetInstances,
    bool RecoveryAttempted,
    int RecoveryRestarted,
    int RecoveryHealthy,
    int RecoveryRetriedInstances,
    bool PausedDueToSleep,
    string? AttentionFocusField,
    string? AttentionFocusHemisphere,
    float AttentionFocusConfidence,
    bool BlockedByInputGate,
    string? InputSource);

public sealed record VisualInputDispatchOutcome(
    VisualInputDispatchResponse Response,
    bool FallbackAttempted,
    VisualInputHttpException? InitialError,
    VisualInputDispatchResponse? LeftResponse,
    VisualInputDispatchResponse? RightResponse,
    VisualInputHttpException? LeftError,
    VisualInputHttpException? RightError);

public sealed class VisualInputHttpException : Exception
{
    public VisualInputHttpException(int statusCode, string? payload)
        : base($"HTTP {statusCode}")
    {
        StatusCode = statusCode;
        Payload = payload ?? string.Empty;
    }

    public int StatusCode { get; }
    public string Payload { get; }
}
