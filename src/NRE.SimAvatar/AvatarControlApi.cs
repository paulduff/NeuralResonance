using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public static class AvatarControlApi
{
    public static string GetFramePath(long dispatchSinceMs, bool includeConnectome = true)
        => $"/api/v1/frame?dispatch_since_ms={Math.Max(0, dispatchSinceMs)}&include_connectome={(includeConnectome ? "true" : "false")}";

    public const string PhysicalBodyFrameInputPath = "/api/v1/admin/input/body-frame";
    public const string CochlearFrameInputPath = "/api/v1/admin/input/audio-frame";
    public const string LanguageInputPath = "/api/v1/admin/input/language";
    public const string RetinalFrameInputPath = "/api/v1/admin/input/visual-frame";
    public const string SomaticContactFrameInputPath = "/api/v1/admin/input/contact-frame";

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

    public static Task<AvatarPhysicalBodyDispatchResult> PostPhysicalBodyFrameAsync(
        HttpClient client,
        Uri endpoint,
        PhysicalBodyFrameRequest frame,
        CancellationToken cancellationToken = default) =>
        PostPhysicalBodyFrameCoreAsync(client, BuildUri(endpoint, PhysicalBodyFrameInputPath), frame, cancellationToken);

    public static Task<AvatarPhysicalBodyDispatchResult> PostPhysicalBodyFrameAsync(
        HttpClient client,
        string endpoint,
        PhysicalBodyFrameRequest frame,
        CancellationToken cancellationToken = default) =>
        PostPhysicalBodyFrameCoreAsync(client, BuildUri(endpoint, PhysicalBodyFrameInputPath), frame, cancellationToken);

    public static Task<AvatarCochlearFrameDispatchResult> PostCochlearFrameAsync(
        HttpClient client,
        Uri endpoint,
        AvatarAudioFrame frame,
        string inputSource = AvatarRuntimeDefaults.UnifiedAudioInputSource,
        CancellationToken cancellationToken = default) =>
        PostCochlearFrameCoreAsync(client, endpoint, frame, inputSource, cancellationToken);

    public static Task<AvatarCochlearFrameDispatchResult> PostCochlearFrameAsync(
        HttpClient client,
        string endpoint,
        AvatarAudioFrame frame,
        string inputSource = AvatarRuntimeDefaults.UnifiedAudioInputSource,
        CancellationToken cancellationToken = default) =>
        PostCochlearFrameCoreAsync(client, new Uri(endpoint), frame, inputSource, cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostEnglishCommandAsync(HttpClient client, Uri endpoint, string text, CancellationToken cancellationToken = default) =>
        PostLanguageCommandAsync(client, endpoint, new AvatarLanguageCommand(text), cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostEnglishCommandAsync(HttpClient client, string endpoint, string text, CancellationToken cancellationToken = default) =>
        PostLanguageCommandAsync(client, new Uri(endpoint), new AvatarLanguageCommand(text), cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostLanguageCommandAsync(HttpClient client, Uri endpoint, AvatarLanguageCommand command, CancellationToken cancellationToken = default) =>
        PostLanguageCommandCoreAsync(client, BuildUri(endpoint, LanguageInputPath), command, cancellationToken);

    public static Task<AvatarLanguageCommandResult> PostLanguageCommandAsync(HttpClient client, string endpoint, AvatarLanguageCommand command, CancellationToken cancellationToken = default) =>
        PostLanguageCommandCoreAsync(client, BuildUri(endpoint, LanguageInputPath), command, cancellationToken);

    public static Task<AvatarRetinalFrameDispatchResult> PostRetinalFrameAsync(
        HttpClient client,
        Uri endpoint,
        AvatarSightFrame frame,
        string inputSource = AvatarRuntimeDefaults.UnifiedVisualInputSource,
        CancellationToken cancellationToken = default) =>
        PostRetinalFrameCoreAsync(client, endpoint, frame, inputSource, cancellationToken);

    public static Task<AvatarRetinalFrameDispatchResult> PostRetinalFrameAsync(
        HttpClient client,
        string endpoint,
        AvatarSightFrame frame,
        string inputSource = AvatarRuntimeDefaults.UnifiedVisualInputSource,
        CancellationToken cancellationToken = default) =>
        PostRetinalFrameCoreAsync(client, new Uri(endpoint), frame, inputSource, cancellationToken);

    public static Task<AvatarSomaticContactDispatchResult> PostSomaticContactFrameAsync(
        HttpClient client,
        Uri endpoint,
        SomaticContactFrameRequest frame,
        CancellationToken cancellationToken = default) =>
        PostSomaticContactFrameCoreAsync(client, BuildUri(endpoint, SomaticContactFrameInputPath), frame, cancellationToken);

    public static Task<AvatarSomaticContactDispatchResult> PostSomaticContactFrameAsync(
        HttpClient client,
        string endpoint,
        SomaticContactFrameRequest frame,
        CancellationToken cancellationToken = default) =>
        PostSomaticContactFrameCoreAsync(client, BuildUri(endpoint, SomaticContactFrameInputPath), frame, cancellationToken);

    private static async Task<AvatarPhysicalBodyDispatchResult> PostPhysicalBodyFrameCoreAsync(
        HttpClient client,
        Uri uri,
        PhysicalBodyFrameRequest frame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(frame);

        using var response = await client.PostAsJsonAsync(uri, frame, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Physical body frame input failed: HTTP {(int)response.StatusCode} {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        return new AvatarPhysicalBodyDispatchResult(
            Accepted: AvatarJson.GetBool(root, "accepted"),
            DispatchDeferred: AvatarJson.GetBool(root, "dispatchDeferred"),
            GeneratedSpikes: AvatarJson.GetInt(root, "generatedSpikes"),
            DeliveredSpikes: AvatarJson.GetInt(root, "deliveredSpikes"),
            TargetInstances: AvatarJson.GetInt(root, "targetInstances"),
            LinearAccelerationMagnitude: (float)AvatarJson.GetDouble(root, "linearAccelerationMagnitude"),
            AngularSpeedMagnitude: (float)AvatarJson.GetDouble(root, "angularSpeedMagnitude"),
            StoredEnergyReserve: (float)AvatarJson.GetDouble(root, "storedEnergyReserve"),
            TissueIntegrity: (float)AvatarJson.GetDouble(root, "tissueIntegrity"),
            HomeostaticDeviation: (float)AvatarJson.GetDouble(root, "homeostaticDeviation"),
            ActiveProprioceptivePopulations: AvatarJson.GetInt(root, "activeProprioceptivePopulations"),
            ActiveVestibularPopulations: AvatarJson.GetInt(root, "activeVestibularPopulations"),
            ActiveVisceralPopulations: AvatarJson.GetInt(root, "activeVisceralPopulations"));
    }

    private static async Task<AvatarCochlearFrameDispatchResult> PostCochlearFrameCoreAsync(
        HttpClient client,
        Uri endpoint,
        AvatarAudioFrame frame,
        string inputSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        var source = string.IsNullOrWhiteSpace(inputSource)
            ? AvatarRuntimeDefaults.UnifiedAudioInputSource
            : inputSource.Trim();
        var path = $"{CochlearFrameInputPath}?sampleRate={frame.SampleRate}&channels={frame.Channels}" +
                   $"&samplesPerChannel={frame.SamplesPerChannel}&sampleFormat=Pcm16Le" +
                   $"&inputSource={Uri.EscapeDataString(source)}";
        using var content = new ByteArrayContent(frame.Pcm16Le, 0, frame.RequiredBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.PostAsync(BuildUri(endpoint, path), content, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cochlear frame input failed: HTTP {(int)response.StatusCode} {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        return new AvatarCochlearFrameDispatchResult(
            Accepted: AvatarJson.GetBool(root, "accepted"),
            DispatchDeferred: AvatarJson.GetBool(root, "dispatchDeferred"),
            GeneratedSpikes: AvatarJson.GetInt(root, "generatedSpikes"),
            DeliveredSpikes: AvatarJson.GetInt(root, "deliveredSpikes"),
            TargetInstances: AvatarJson.GetInt(root, "targetInstances"),
            FrequencyBands: AvatarJson.GetInt(root, "frequencyBands"),
            ActiveLeftBands: AvatarJson.GetInt(root, "activeLeftBands"),
            ActiveRightBands: AvatarJson.GetInt(root, "activeRightBands"),
            RootMeanSquare: (float)AvatarJson.GetDouble(root, "rootMeanSquare"),
            PeakAmplitude: (float)AvatarJson.GetDouble(root, "peakAmplitude"),
            MeanBandAmplitude: (float)AvatarJson.GetDouble(root, "meanBandAmplitude"),
            MeanOnset: (float)AvatarJson.GetDouble(root, "meanOnset"));
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

    private static async Task<AvatarRetinalFrameDispatchResult> PostRetinalFrameCoreAsync(
        HttpClient client,
        Uri endpoint,
        AvatarSightFrame frame,
        string inputSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();

        var source = string.IsNullOrWhiteSpace(inputSource)
            ? AvatarRuntimeDefaults.UnifiedVisualInputSource
            : inputSource.Trim();
        var path = $"{RetinalFrameInputPath}?width={frame.Width}&height={frame.Height}&stride={frame.Stride}" +
                   $"&pixelFormat={Uri.EscapeDataString(frame.PixelFormat)}&inputSource={Uri.EscapeDataString(source)}";
        using var content = new ByteArrayContent(frame.Pixels, 0, checked(frame.Stride * frame.Height));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.PostAsync(BuildUri(endpoint, path), content, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Retinal frame input failed: HTTP {(int)response.StatusCode} {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        return new AvatarRetinalFrameDispatchResult(
            Accepted: AvatarJson.GetBool(root, "accepted"),
            DispatchDeferred: AvatarJson.GetBool(root, "dispatchDeferred"),
            BlockedByInputGate: AvatarJson.GetBool(root, "blockedByInputGate"),
            GeneratedSpikes: AvatarJson.GetInt(root, "generatedSpikes"),
            DeliveredSpikes: AvatarJson.GetInt(root, "deliveredSpikes"),
            TargetInstances: AvatarJson.GetInt(root, "targetInstances"),
            SampleColumns: AvatarJson.GetInt(root, "sampleColumns"),
            SampleRows: AvatarJson.GetInt(root, "sampleRows"),
            OnChannelSpikes: AvatarJson.GetInt(root, "onChannelSpikes"),
            OffChannelSpikes: AvatarJson.GetInt(root, "offChannelSpikes"),
            MeanLuminance: (float)AvatarJson.GetDouble(root, "meanLuminance"),
            MeanTemporalChange: (float)AvatarJson.GetDouble(root, "meanTemporalChange"));
    }

    private static async Task<AvatarSomaticContactDispatchResult> PostSomaticContactFrameCoreAsync(
        HttpClient client,
        Uri uri,
        SomaticContactFrameRequest frame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(frame);

        using var response = await client.PostAsJsonAsync(uri, frame, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Somatic contact frame input failed: HTTP {(int)response.StatusCode} {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        return new AvatarSomaticContactDispatchResult(
            Accepted: AvatarJson.GetBool(root, "accepted"),
            DispatchDeferred: AvatarJson.GetBool(root, "dispatchDeferred"),
            GeneratedSpikes: AvatarJson.GetInt(root, "generatedSpikes"),
            DeliveredSpikes: AvatarJson.GetInt(root, "deliveredSpikes"),
            TargetInstances: AvatarJson.GetInt(root, "targetInstances"),
            ReceptorSector: AvatarJson.GetInt(root, "receptorSector"),
            ActiveReceptorPopulations: AvatarJson.GetInt(root, "activeReceptorPopulations"),
            PressureActivation: (float)AvatarJson.GetDouble(root, "pressureActivation"),
            OnsetActivation: (float)AvatarJson.GetDouble(root, "onsetActivation"),
            VibrationActivation: (float)AvatarJson.GetDouble(root, "vibrationActivation"),
            StretchActivation: (float)AvatarJson.GetDouble(root, "stretchActivation"),
            HighThresholdActivation: (float)AvatarJson.GetDouble(root, "highThresholdActivation"));
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
