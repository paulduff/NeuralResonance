using NRE.Blazor.Shared.OperatorConsole;
using NRE.Contracts.Voice;

namespace NRE.Blazor.Services;

public sealed class ConsoleRefreshCoordinator
{
    private readonly IEngineApiClient _api;
    private readonly IRendererInteropService _renderer;

    public int VoiceLoopDelayMs { get; set; } = 200;
    public int FrameLoopDelayMs { get; set; } = 50;
    public int StatusLoopDelayMs { get; set; } = 200;

    public ConsoleRefreshCoordinator(IEngineApiClient api, IRendererInteropService renderer)
    {
        _api = api;
        _renderer = renderer;
    }

    public async Task RunVoiceLoopAsync(Func<VoiceUtteranceDto, Task> onVoiceAsync, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msgs = await _api.GetVoiceMessagesAsync(6, ct);
                if (msgs is { Length: > 0 })
                {
                    foreach (var msg in msgs)
                    {
                        if (string.IsNullOrWhiteSpace(msg.Text) && (msg.Phonemes == null || msg.Phonemes.Length == 0))
                            continue;

                        await _renderer.SpeakAsync(msg);
                        await _api.PostVoiceReafferentAsync(new VoiceReafferenceRequest(msg.Text, msg.Rate, msg.Pitch, msg.Volume, HoldSeconds: 0.45f, Phonemes: msg.Phonemes, Gloss: msg.Gloss), ct);
                        await onVoiceAsync(msg);
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(VoiceLoopDelayMs, ct);
        }
    }

    public async Task RunFrameLoopAsync(Func<RenderFrameFastDto, Task> onFrameAsync, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var bytes = await _api.GetFastFrameBinaryAsync(ct);
                var frame = FastFrameParser.Parse(bytes);
                if (frame is not null)
                    await onFrameAsync(frame);
            }
            catch
            {
            }

            await Task.Delay(FrameLoopDelayMs, ct);
        }
    }

    public async Task RunStatusLoopAsync(Func<EngineStatusDto, Task> onStatusAsync, Func<int, Task> onTelemetryTickAsync, Func<Task> invalidateAsync, Func<string> getTab, CancellationToken ct)
    {
        var telemetryCounter = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await _api.GetStatusAsync(ct);
                if (status is not null)
                {
                    await onStatusAsync(status);
                }

                if (getTab() == "Monitor" && ++telemetryCounter >= 12)
                {
                    telemetryCounter = 0;
                    await onTelemetryTickAsync(telemetryCounter);
                }

                await invalidateAsync();
            }
            catch
            {
            }

            await Task.Delay(StatusLoopDelayMs, ct);
        }
    }
}


