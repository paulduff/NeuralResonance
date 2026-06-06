using NRE.Blazor.Shared.OperatorConsole;
using NRE.Contracts.Voice;

namespace NRE.Blazor.Services;

public interface IEngineApiClient
{
    Task<EngineStatusDto?> GetStatusAsync(CancellationToken ct = default);
    Task<byte[]> GetFastFrameBinaryAsync(CancellationToken ct = default);
    Task<VoiceUtteranceDto[]?> GetVoiceMessagesAsync(int max = 6, CancellationToken ct = default);
    Task PostVoiceReafferentAsync(VoiceReafferenceRequest request, CancellationToken ct = default);
}

public interface IRendererInteropService
{
    ValueTask SpeakAsync(VoiceUtteranceDto utterance);
}
