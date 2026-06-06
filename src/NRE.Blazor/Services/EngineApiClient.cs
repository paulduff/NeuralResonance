using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using NRE.Blazor.Shared.OperatorConsole;
using NRE.Contracts.Voice;

namespace NRE.Blazor.Services;

public sealed class EngineApiClient : IEngineApiClient
{
    private readonly IHttpClientFactory _httpFactory;
    private HttpClient? _client;

    public EngineApiClient(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public HttpClient Client => _client ??= _httpFactory.CreateClient("nre");

    public string BaseAddress => Client.BaseAddress?.ToString() ?? "http://localhost:5005/";

    public Task<EngineStatusDto?> GetStatusAsync(CancellationToken ct = default)
        => Client.GetFromJsonAsync<EngineStatusDto>("api/engine/status", ct);

    public Task<PackedPoints?> GetLayoutAsync(CancellationToken ct = default)
        => Client.GetFromJsonAsync<PackedPoints>("api/engine/layout", ct);

    public Task<PackedLinesDto?> GetConnectionsAsync(int maxEdges = 12000, CancellationToken ct = default)
        => Client.GetFromJsonAsync<PackedLinesDto>($"api/engine/connections?maxEdges={maxEdges}", ct);

    public Task<byte[]> GetFastFrameBinaryAsync(CancellationToken ct = default)
        => Client.GetByteArrayAsync("api/engine/framefast.bin", ct);

    public Task<VoiceUtteranceDto[]?> GetVoiceMessagesAsync(int max = 6, CancellationToken ct = default)
        => Client.GetFromJsonAsync<VoiceUtteranceDto[]>($"api/engine/voice?max={max}", ct);

    public async Task PostVoiceReafferentAsync(VoiceReafferenceRequest request, CancellationToken ct = default)
    {
        using var response = await Client.PostAsJsonAsync("api/engine/voice/reafferent", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        using var response = await Client.PostAsync("api/engine/start", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        using var response = await Client.PostAsync("api/engine/stop", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetNeuromodulatorAsync(string type, float value, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/neuromodulator?type={Uri.EscapeDataString(type)}&value={value}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPonsAsync(float arousal01, float stability01, float resetPressure01, float thetaHz, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/pons?arousal01={arousal01}&stability01={stability01}&resetPressure01={resetPressure01}&thetaHz={thetaHz}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetVoiceSettingsAsync(float bgConfidence, int cooldownSteps, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/voice/settings?bgConfidence={bgConfidence}&cooldownSteps={cooldownSteps}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetMotorGainAsync(float gain, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/motor/gain?gain={gain}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPeerNameAsync(string name, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/peer/name?name={Uri.EscapeDataString(name)}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ConnectAllPeersAsync(CancellationToken ct = default)
    {
        using var response = await Client.PostAsync("api/engine/peer/connect-all", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string[]> GetPeerHubInstancesAsync(CancellationToken ct = default)
        => await Client.GetFromJsonAsync<string[]>("api/engine/peer/hub", ct) ?? Array.Empty<string>();

    public Task<PeerBridgeStatusDto?> GetPeerInfoAsync(CancellationToken ct = default)
        => Client.GetFromJsonAsync<PeerBridgeStatusDto>("api/engine/peer/info", ct);

    public async Task PeerSayAsync(string text, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/peer/say?text={Uri.EscapeDataString(text)}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPeerPitchAsync(float hz, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/vocaltract/pitch?hz={hz}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task InjectAsync(InjectRequestDto request, CancellationToken ct = default)
    {
        using var response = await Client.PostAsJsonAsync("api/engine/inject", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyVisualAsync(float intensity01, float speedHz, float spatialFreq, CancellationToken ct = default)
    {
        using var response = await Client.PostAsJsonAsync("api/engine/visual", new VisualStimulusRequest(intensity01, speedHz, spatialFreq), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyAudioAsync(float intensity01, float toneHz, CancellationToken ct = default)
    {
        using var response = await Client.PostAsJsonAsync("api/engine/auditory", new AuditoryStimulusRequest(intensity01, toneHz), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyThalamusAsync(float frequencyHz, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/thalamus?frequencyHz={frequencyHz}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ForceSleepAsync(string phase, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/sleep/force?phase={Uri.EscapeDataString(phase)}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task BuildSleepPressureAsync(float amount, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/sleep/buildpressure?amount={amount}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplySalienceAsync(int regionId, float salience, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync($"api/engine/amygdala/salience?regionId={regionId}&salience={salience}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<ResonantClustersDto?> GetResonantClustersAsync(CancellationToken ct = default)
        => Client.GetFromJsonAsync<ResonantClustersDto>("api/monitor/resonant-clusters", ct);

    public Task<ThoughtClustersDto?> GetThoughtClustersAsync(CancellationToken ct = default)
        => Client.GetFromJsonAsync<ThoughtClustersDto>("api/monitor/thought-clusters", ct);

    public Task<TelemetrySnapshotDto?> GetTelemetryAsync(CancellationToken ct = default)
        => Client.GetFromJsonAsync<TelemetrySnapshotDto>("api/monitor/telemetry", ct);

    public Task<HttpResponseMessage> SaveBrainAsync(CancellationToken ct = default)
        => Client.GetAsync("api/engine/save", ct);

    public Task<HttpResponseMessage> LoadBrainAsync(byte[] bytes, CancellationToken ct = default)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        return Client.PostAsync("api/engine/load", content, ct);
    }

    public async Task PostAbsoluteAsync(string relativeUri, CancellationToken ct = default)
    {
        using var response = await Client.PostAsync(relativeUri, null, ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record VisualStimulusRequest(float Intensity01, float SpeedHz, float SpatialFreq, bool Enabled = true);
    private sealed record AuditoryStimulusRequest(float Intensity01, float ToneHz, bool Enabled = true);
}
