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
