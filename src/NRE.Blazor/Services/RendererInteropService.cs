using Microsoft.JSInterop;
using NRE.Blazor.Shared.OperatorConsole;
using NRE.Contracts.Voice;

namespace NRE.Blazor.Services;

public sealed class RendererInteropService : IRendererInteropService
{
    private readonly IJSRuntime _js;

    public RendererInteropService(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask SetDarkThemeAsync(bool dark) => _js.InvokeVoidAsync("Theme.setDark", dark);
    public ValueTask<string?> InitVoiceAsync() => _js.InvokeAsync<string?>("VoiceOut.initSafe");
    public ValueTask<string?> InitNeuralRendererSafeAsync(string canvasId, int w, int h, int d, object options)
        => _js.InvokeAsync<string?>("NreInterop.initNeuralRendererSafe", canvasId, w, h, d, options);
    public ValueTask SetLayoutAsync(PackedPoints layout) => _js.InvokeVoidAsync("NeuralRenderer.setLayout", new { count = layout.Count, data = layout.Data });
    public ValueTask SetConnectionsAsync(PackedLinesDto lines) => _js.InvokeVoidAsync("NeuralRenderer.setConnections", new { count = lines.Count, data = lines.Data });
    public ValueTask ShowKeyPathwayConnectionsAsync() => _js.InvokeVoidAsync("NeuralRenderer.showKeyPathwayConnections");
    public ValueTask UpdateFrameCombinedAsync(RenderFrameFastDto frame, bool showAvatar)
        => _js.InvokeVoidAsync("NeuralRenderer.updateFrameCombined",
            new { count = frame.Spikes.Count, data = frame.Spikes.Data },
            frame.CallosalTraffic01,
            new { count = frame.CrossModuleTraffic.Count, data = frame.CrossModuleTraffic.Data },
            showAvatar && frame.Body is { Length: 21 } ? frame.Body : null);
    public ValueTask SpeakAsync(VoiceUtteranceDto utterance) => _js.InvokeVoidAsync("VoiceOut.speak", utterance);
    public ValueTask InitAvatarAsync() => _js.InvokeVoidAsync("AvatarRenderer.init");
    public ValueTask DestroyAvatarAsync() => _js.InvokeVoidAsync("AvatarRenderer.destroy");
    public ValueTask StartWebcamAsync(string apiBase, object options) => _js.InvokeVoidAsync("SensoryCapture.startWebcam", apiBase, options);
    public ValueTask StopWebcamAsync() => _js.InvokeVoidAsync("SensoryCapture.stopWebcam");
    public ValueTask StartMicAsync(string apiBase, object options) => _js.InvokeVoidAsync("SensoryCapture.startMic", apiBase, options);
    public ValueTask StopMicAsync() => _js.InvokeVoidAsync("SensoryCapture.stopMic");
    public ValueTask StartInternetStreamAsync(string apiBase, string streamUrl, object options)
        => _js.InvokeVoidAsync("SensoryCapture.startInternetStream", apiBase, streamUrl, options);
    public ValueTask StopInternetStreamAsync() => _js.InvokeVoidAsync("SensoryCapture.stopInternetStream");
    public ValueTask DownloadBlobAsync(string base64, string fileName) => _js.InvokeVoidAsync("NreSaveLoad.downloadBlob", base64, fileName);
    public ValueTask FocusBrainFileInputAsync() => _js.InvokeVoidAsync("eval", "document.getElementById('brainFileInput').querySelector('input[type=file]')?.click() || document.querySelector('#brainFileInput input')?.click()");
    public ValueTask RefreshAsync() => _js.InvokeVoidAsync("NeuralRenderer.refresh");
    public ValueTask SetAnatomicalModeAsync(bool value) => _js.InvokeVoidAsync("NeuralRenderer.setAnatomicalMode", value);
    public ValueTask SetBrainWarpEnabledAsync(bool value) => _js.InvokeVoidAsync("NeuralRenderer.setBrainWarpEnabled", value);
    public ValueTask SetGyrificationEnabledAsync(bool value) => _js.InvokeVoidAsync("NeuralRenderer.setGyrificationEnabled", value);
    public ValueTask SetJitterAsync(float value) => _js.InvokeVoidAsync("NeuralRenderer.setJitter", value);
    public ValueTask SetRenderModeAsync(string mode) => _js.InvokeVoidAsync("NeuralRenderer.setRenderMode", mode);
    public ValueTask SetShellVisibleAsync(bool value) => _js.InvokeVoidAsync("NeuralRenderer.setShellVisible", value);
    public ValueTask SetFibrePulseEnabledAsync(bool value) => _js.InvokeVoidAsync("NeuralRenderer.setFibrePulseEnabled", value);
    public ValueTask OpenWindowAsync(string url, string target) => _js.InvokeVoidAsync("window.open", url, target);
    public ValueTask SetViewPresetAsync(string preset) => _js.InvokeVoidAsync("NeuralRenderer.setViewPreset", preset);
    public ValueTask ShowAllConnectionsAsync() => _js.InvokeVoidAsync("NeuralRenderer.showAllConnections");
    public ValueTask HideAllConnectionsAsync() => _js.InvokeVoidAsync("NeuralRenderer.hideAllConnections");
    public ValueTask SetConnectionFilterAsync(string filter) => _js.InvokeVoidAsync("NeuralRenderer.setConnectionFilter", filter);
    public ValueTask SetConnectionFilterAsync(string filter, int[] regions) => _js.InvokeVoidAsync("NeuralRenderer.setConnectionFilter", filter, regions);
}

