using System.Net.Http.Json;
using System.Text.Json;

namespace NRE.SimAvatar;

public static class AvatarControlApi
{
    public const string BodyStatePath = "/api/v1/admin/input/body-state";
    public const string OutcomeInputPath = "/api/v1/admin/input/outcome";
    public const string AuditoryInputPath = "/api/v1/admin/input/auditory";
    public const string LanguageInputPath = "/api/v1/admin/input/language";

    public static Uri BuildUri(Uri endpoint, string relativePath) => new(endpoint, relativePath);

    public static Uri BuildUri(string endpoint, string relativePath) => new(new Uri(endpoint), relativePath);

    public static Task<AvatarJsonHttpResponse> GetJsonAsync(HttpClient client, Uri endpoint, string relativePath, CancellationToken cancellationToken = default) =>
        GetJsonAsync(client, BuildUri(endpoint, relativePath), cancellationToken);

    public static Task<AvatarJsonHttpResponse> GetJsonAsync(HttpClient client, string endpoint, string relativePath, CancellationToken cancellationToken = default) =>
        GetJsonAsync(client, BuildUri(endpoint, relativePath), cancellationToken);

    public static async Task<AvatarJsonHttpResponse> GetJsonAsync(HttpClient client, Uri uri, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return await CreateJsonResponseAsync(response, cancellationToken);
    }

    public static Task PostBodyStateAsync(HttpClient client, Uri endpoint, AvatarBodyTelemetry telemetry, AvatarBodyStateProfile profile, CancellationToken cancellationToken = default) =>
        PostBodyStateCoreAsync(client, BuildUri(endpoint, BodyStatePath), telemetry, profile, cancellationToken);

    public static Task PostBodyStateAsync(HttpClient client, string endpoint, AvatarBodyTelemetry telemetry, AvatarBodyStateProfile profile, CancellationToken cancellationToken = default) =>
        PostBodyStateCoreAsync(client, BuildUri(endpoint, BodyStatePath), telemetry, profile, cancellationToken);

    public static Task PostOutcomeAsync(HttpClient client, Uri endpoint, AvatarOutcomeTelemetry telemetry, CancellationToken cancellationToken = default) =>
        PostOutcomeCoreAsync(client, BuildUri(endpoint, OutcomeInputPath), telemetry, cancellationToken);

    public static Task PostOutcomeAsync(HttpClient client, string endpoint, AvatarOutcomeTelemetry telemetry, CancellationToken cancellationToken = default) =>
        PostOutcomeCoreAsync(client, BuildUri(endpoint, OutcomeInputPath), telemetry, cancellationToken);

    public static Task<AvatarAuditoryDispatchResult> PostAuditoryCueAsync(HttpClient client, Uri endpoint, AvatarAuditoryCue cue, CancellationToken cancellationToken = default) =>
        PostAuditoryCueCoreAsync(client, BuildUri(endpoint, AuditoryInputPath), cue, cancellationToken);

    public static Task<AvatarAuditoryDispatchResult> PostAuditoryCueAsync(HttpClient client, string endpoint, AvatarAuditoryCue cue, CancellationToken cancellationToken = default) =>
        PostAuditoryCueCoreAsync(client, BuildUri(endpoint, AuditoryInputPath), cue, cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostEnglishCommandAsync(HttpClient client, Uri endpoint, string text, CancellationToken cancellationToken = default) =>
        PostLanguageCommandAsync(client, endpoint, new AvatarLanguageCommand(text), cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostEnglishCommandAsync(HttpClient client, string endpoint, string text, CancellationToken cancellationToken = default) =>
        PostLanguageCommandAsync(client, new Uri(endpoint), new AvatarLanguageCommand(text), cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostLanguageCommandAsync(HttpClient client, Uri endpoint, AvatarLanguageCommand command, CancellationToken cancellationToken = default) =>
        PostLanguageCommandCoreAsync(client, BuildUri(endpoint, LanguageInputPath), command, cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostLanguageCommandAsync(HttpClient client, string endpoint, AvatarLanguageCommand command, CancellationToken cancellationToken = default) =>
        PostLanguageCommandCoreAsync(client, BuildUri(endpoint, LanguageInputPath), command, cancellationToken);

    private static async Task PostBodyStateCoreAsync(HttpClient client, Uri uri, AvatarBodyTelemetry telemetry, AvatarBodyStateProfile profile, CancellationToken cancellationToken = default)
    {
        var request = AvatarBodyStateInputFactory.CreateRequest(telemetry, profile);
        using var response = await client.PostAsJsonAsync(uri, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Body-state input failed: HTTP {(int)response.StatusCode} {message}");
        }
    }

    private static async Task PostOutcomeCoreAsync(HttpClient client, Uri uri, AvatarOutcomeTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        var request = AvatarOutcomeInputFactory.CreateRequest(telemetry);
        using var _ = await client.PostAsJsonAsync(uri, request, cancellationToken);
    }

    private static async Task<AvatarAuditoryDispatchResult> PostAuditoryCueCoreAsync(
        HttpClient client,
        Uri uri,
        AvatarAuditoryCue cue,
        CancellationToken cancellationToken)
    {
        var pattern = string.IsNullOrWhiteSpace(cue.Pattern) ? "EnvironmentalSound" : cue.Pattern.Trim();
        var request = new
        {
            Pattern = pattern,
            Intensity = Math.Clamp(cue.Intensity, 0.2f, 3.0f),
            BurstCount = Math.Clamp(cue.BurstCount, 4, 64),
            TargetStructure = string.IsNullOrWhiteSpace(cue.TargetStructure)
                ? AvatarRuntimeDefaults.UnifiedAudioTargetStructure
                : cue.TargetStructure,
            SourceStructure = string.IsNullOrWhiteSpace(cue.SourceStructure)
                ? AvatarRuntimeDefaults.UnifiedAudioSourceStructure
                : cue.SourceStructure,
            cue.Hemisphere,
            InputSource = string.IsNullOrWhiteSpace(cue.InputSource)
                ? AvatarRuntimeDefaults.UnifiedAudioInputSource
                : cue.InputSource
        };

        using var response = await client.PostAsJsonAsync(uri, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Auditory cue failed: HTTP {(int)response.StatusCode} {message}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        return new AvatarAuditoryDispatchResult(
            Pattern: AvatarJson.GetString(root, "pattern"),
            GeneratedSpikes: AvatarJson.GetInt(root, "generatedSpikes"),
            DeliveredSpikes: AvatarJson.GetInt(root, "deliveredSpikes"),
            TargetInstances: AvatarJson.GetInt(root, "targetInstances"),
            PausedDueToSleep: AvatarJson.GetBool(root, "pausedDueToSleep"),
            Accepted: AvatarJson.GetBool(root, "accepted"),
            DispatchDeferred: AvatarJson.GetBool(root, "dispatchDeferred"));
    }

    private static async Task<AvatarLanguageCommandResult> PostLanguageCommandCoreAsync(HttpClient client, Uri uri, AvatarLanguageCommand command, CancellationToken cancellationToken)
    {
        var text = command.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("English command text cannot be empty.", nameof(command));
        }

        var tokenCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        var request = new
        {
            Text = text,
            Mode = string.IsNullOrWhiteSpace(command.Mode) ? "english" : command.Mode,
            Hemisphere = command.Hemisphere,
            Intensity = command.Intensity ?? Math.Clamp(0.85f + (tokenCount * 0.04f), 0.20f, 3.0f),
            BurstPerToken = command.BurstPerToken ?? Math.Clamp(6 + tokenCount, 4, 24),
            NoveltyBias = 0.0f
        };

        using var response = await client.PostAsJsonAsync(uri, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Language command failed: HTTP {(int)response.StatusCode} {message}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseLanguageCommandResult(document.RootElement);
    }

    public static AvatarLanguageCommandResult ParseLanguageCommandResult(JsonElement root)
    {
        var grammarIntent = string.Empty;
        var grammarMood = string.Empty;
        if (AvatarJson.TryGetProperty(root, "grammar", out var grammar) && grammar.ValueKind == JsonValueKind.Object)
        {
            grammarIntent = AvatarJson.GetString(grammar, "intent");
            grammarMood = AvatarJson.GetString(grammar, "mood");
        }

        var commandKey = string.Empty;
        var motorDirective = string.Empty;
        var strength = 0.0f;
        if (AvatarJson.TryGetProperty(root, "languageIntent", out var languageIntent) && languageIntent.ValueKind == JsonValueKind.Object)
        {
            commandKey = AvatarJson.GetString(languageIntent, "commandKey");
            motorDirective = AvatarJson.GetString(languageIntent, "motorDirective");
            strength = (float)AvatarJson.GetDouble(languageIntent, "strength");
        }

        var narration = AvatarBrainNarration.Empty;
        if (AvatarJson.TryGetProperty(root, "brainNarration", out var brainNarration) && brainNarration.ValueKind == JsonValueKind.Object)
        {
            narration = ParseBrainNarration(brainNarration);
        }

        return new AvatarLanguageCommandResult(
            Mode: AvatarJson.GetString(root, "mode"),
            TokenCount: AvatarJson.GetInt(root, "tokenCount"),
            BrainTokenCount: AvatarJson.GetInt(root, "brainTokenCount"),
            GeneratedSpikes: AvatarJson.GetInt(root, "generatedSpikes"),
            DeliveredSpikes: AvatarJson.GetInt(root, "deliveredSpikes"),
            TargetInstances: AvatarJson.GetInt(root, "targetInstances"),
            Utterance: AvatarJson.GetString(root, "generatedUtterance", "text"),
            PausedDueToSleep: AvatarJson.GetBool(root, "pausedDueToSleep"),
            GrammarIntent: grammarIntent,
            GrammarMood: grammarMood,
            CommandKey: commandKey,
            MotorDirective: motorDirective,
            Strength: Math.Clamp(strength, 0.0f, 3.0f),
            Narration: narration);
    }

    public static bool TryReadBrainNarration(JsonElement stateElement, out AvatarBrainNarration narration)
    {
        if (AvatarJson.TryGetProperty(stateElement, "brainNarration", out var directNarration) &&
            directNarration.ValueKind == JsonValueKind.Object)
        {
            narration = ParseBrainNarration(directNarration);
            return narration.HasText;
        }

        if (AvatarJson.TryGetProperty(stateElement, "brainBehavior", out var brainBehavior) &&
            brainBehavior.ValueKind == JsonValueKind.Object &&
            AvatarJson.TryGetProperty(brainBehavior, "language", out var language) &&
            language.ValueKind == JsonValueKind.Object)
        {
            narration = ParseBrainNarration(language);
            return narration.HasText;
        }

        narration = AvatarBrainNarration.Empty;
        return false;
    }

    private static AvatarBrainNarration ParseBrainNarration(JsonElement element)
    {
        return new AvatarBrainNarration(
            Utterance: AvatarJson.GetString(element, "utterance"),
            Sequence: AvatarJson.GetLong(element, "sequence"),
            LastUpdatedTick: AvatarJson.GetLong(element, "lastUpdatedTick"),
            Source: AvatarJson.GetString(element, "source"));
    }

    private static async Task<AvatarJsonHttpResponse> CreateJsonResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new AvatarJsonHttpResponse(response.StatusCode, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return new AvatarJsonHttpResponse(response.StatusCode, document);
    }
}
