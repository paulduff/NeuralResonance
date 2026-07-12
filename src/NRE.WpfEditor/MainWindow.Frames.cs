using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;

namespace NRE.WpfEditor;

// Snapshot polling transport: frame document call, endpoint probing, fallback frame
// document construction, and frame URI builders.
// Payload processing (IngestFrameState, ProcessFramePayload, ApplyNeuronHighlights, etc.)
// remains in MainWindow.xaml.cs because it is tightly woven with UI state.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private void StartFramePolling()
    {
        _framePollTask = Task.Run(() => FramePollLoopAsync(_workerCts.Token));
    }

    private async Task FramePollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await TryFallbackSnapshotPollFromWorkerAsync(token);
                await Task.Delay(FramePollOnlyLoopDelay, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                NoteControlEndpointFailure();
                await Dispatcher.InvokeAsync(
                    () => SetRenderStatus($"Render: unable to fetch snapshot frame (UI overruns: {_uiOverrunCount})", appendToOutput: false),
                    DispatcherPriority.Background,
                    token);
                var reason = ex is OperationCanceledException ? "request timed out" : TrimForStatus(ex.Message, 140);
                EmitFramePollWarning($"Render transport warning: frame poll failed ({reason}).");
                QueueHealthDiagnosticsProbe();
                await Task.Delay(FramePollOnlyLoopDelay, token);
            }
        }
    }

    private async Task TryFallbackSnapshotPollFromWorkerAsync(CancellationToken token, Uri? preferredBaseUri = null)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFrameFallbackPollUtc) < FrameFallbackPollInterval)
        {
            return;
        }

        _lastFrameFallbackPollUtc = now;
        try
        {
            var pollTaskOp = await Dispatcher.InvokeAsync(
                () => PollSnapshotAsync(preferredBaseUri),
                DispatcherPriority.Background,
                token);
            await pollTaskOp;
        }
        catch
        {
            // Best effort fallback path only.
        }
    }

    private void EmitFramePollWarning(string message)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFramePollWarningUtc) < FramePollWarningCooldown)
        {
            return;
        }

        _lastFramePollWarningUtc = now;
        PostUi(() => AddOutputLog(message));
    }

    private async Task PollSnapshotAsync(Uri? preferredBaseUri = null)
    {
        if (_isSnapshotPolling)
        {
            return;
        }

        _isSnapshotPolling = true;
        try
        {
            using var timeout = new CancellationTokenSource(FramePollTimeout);
            var verifiedBaseUri = preferredBaseUri;
            if (verifiedBaseUri is null)
            {
                verifiedBaseUri = await ResolveVerifiedControlBaseUriAsync(timeout.Token);
                if (verifiedBaseUri is null)
                {
                    verifiedBaseUri = await ProbeFrameCapableBaseUriAsync(timeout.Token);
                    if (verifiedBaseUri is not null)
                    {
                        NoteControlEndpointSuccess(verifiedBaseUri);
                    }
                    else
                    {
                        verifiedBaseUri = await ProbeDiagnosticsBaseUriAsync(timeout.Token);
                        if (verifiedBaseUri is not null)
                        {
                            NoteControlEndpointSuccess(verifiedBaseUri);
                        }
                    }
                }
            }
            if (verifiedBaseUri is null)
            {
                SetTransportStatsText("Transport stats unavailable: no verified Control Program endpoint.");
                SetReasoningText("Reasoning telemetry unavailable: no verified Control Program endpoint.");
                SetRenderStatus($"Render: unable to fetch snapshot frame (UI overruns: {_uiOverrunCount})");
                QueueHealthDiagnosticsProbe();
                return;
            }

            var frameUri = BuildFrameUri(verifiedBaseUri);
            using var response = await _httpClient.GetAsync(frameUri, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                if (await TryProcessEndpointFallbackFrameAsync(verifiedBaseUri, timeout.Token))
                {
                    _framePollConsecutiveFailures = 0;
                    _framePollCursorResetApplied = false;
                    return;
                }

                NoteControlEndpointFailure();
                SetTransportStatsText($"Transport stats unavailable: HTTP {(int)response.StatusCode} from {verifiedBaseUri}");
                SetReasoningText($"Reasoning telemetry unavailable: HTTP {(int)response.StatusCode} from {verifiedBaseUri}");
                SetRenderStatus($"Render: unable to fetch snapshot frame (UI overruns: {_uiOverrunCount})");
                QueueHealthDiagnosticsProbe();
                return;
            }

            await using var frameStream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var frameDoc = await JsonDocument.ParseAsync(frameStream, cancellationToken: timeout.Token);
            if (frameDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                if (await TryProcessEndpointFallbackFrameAsync(verifiedBaseUri, timeout.Token))
                {
                    _framePollConsecutiveFailures = 0;
                    _framePollCursorResetApplied = false;
                    return;
                }

                NoteControlEndpointFailure();
                SetTransportStatsText("Transport stats unavailable: /api/v1/frame returned malformed payload.");
                SetReasoningText("Reasoning telemetry unavailable: /api/v1/frame returned malformed payload.");
                SetRenderStatus($"Render: unable to fetch snapshot frame (UI overruns: {_uiOverrunCount})");
                QueueHealthDiagnosticsProbe();
                return;
            }

            ProcessFramePayload(frameDoc.RootElement, verifiedBaseUri);
            _framePollConsecutiveFailures = 0;
            _framePollCursorResetApplied = false;
        }
        catch (Exception ex)
        {
            _framePollConsecutiveFailures++;
            if (_framePollConsecutiveFailures >= FramePollCursorResetThreshold && !_framePollCursorResetApplied)
            {
                _framePollCursorResetApplied = true;
                _lastRemoteOutputLogWallClockMs = 0;
                _lastRemoteSpikeLogWallClockMs = 0;
                _lastRemoteDispatchWallClockMs = 0;
                PostUi(() => AddOutputLog("Render fallback: reset frame cursors after repeated poll failures."));
            }

            var now = DateTime.UtcNow;
            if ((now - _lastFramePollFailureLogUtc) >= FramePollFailureLogCooldown && !_workerCts.IsCancellationRequested)
            {
                _lastFramePollFailureLogUtc = now;
                var reason = ex is OperationCanceledException ? "request timed out" : ex.Message;
                PostUi(() => AddOutputLog($"Render fallback warning: snapshot poll failed ({reason})."));
            }

            NoteControlEndpointFailure();
            SetRenderStatus($"Render: unable to fetch snapshot frame (UI overruns: {_uiOverrunCount})", appendToOutput: false);
            QueueHealthDiagnosticsProbe();
        }
        finally
        {
            _isSnapshotPolling = false;
        }
    }

    private async Task<Uri?> ProbeFrameCapableBaseUriAsync(CancellationToken token)
    {
        foreach (var candidate in EnumerateControlBaseUris())
        {
            try
            {
                using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                probeTimeout.CancelAfter(TimeSpan.FromMilliseconds(1800));
                var uri = new Uri(candidate, "/api/v1/frame?include_connectome=0&max_output_log=1&max_spike_log=1&max_dispatch_spikes=1");
                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, probeTimeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(probeTimeout.Token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: probeTimeout.Token);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetProperty(doc.RootElement, "state", out var stateElement) ||
                    stateElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                return candidate;
            }
            catch
            {
                // Best effort probe only.
            }
        }

        return null;
    }

    private async Task<Uri?> ProbeDiagnosticsBaseUriAsync(CancellationToken token)
    {
        foreach (var candidate in EnumerateControlBaseUris())
        {
            try
            {
                using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                probeTimeout.CancelAfter(TimeSpan.FromMilliseconds(1800));
                using var state = await FetchJsonDocumentAsync(new Uri(candidate, "/api/v1/state"), probeTimeout.Token);
                if (state?.RootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetProperty(state.RootElement, "transportStats", out _) ||
                    TryGetProperty(state.RootElement, "transport_stats", out _) ||
                    TryGetProperty(state.RootElement, "serviceTelemetry", out _) ||
                    TryGetProperty(state.RootElement, "service_telemetry", out _) ||
                    TryGetProperty(state.RootElement, "tick", out _))
                {
                    return candidate;
                }
            }
            catch
            {
                // Best effort probe only.
            }
        }

        return null;
    }

    private async Task<bool> TryProcessEndpointFallbackFrameAsync(Uri baseUri, CancellationToken token)
    {
        try
        {
            using var state = await FetchJsonDocumentAsync(new Uri(baseUri, "/api/v1/state"), token);
            if (state?.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            using var latestSnapshot = await FetchJsonDocumentAsync(new Uri(baseUri, "/api/v1/snapshot/latest"), token);
            using var dispatchSpikes = await FetchJsonDocumentAsync(new Uri(baseUri, $"/api/v1/transport/spikes/recent?limit={FramePollMaxDispatchSpikes}"), token);
            using var doc = BuildFallbackFrameDocument(
                state.RootElement,
                latestSnapshot?.RootElement,
                dispatchSpikes?.RootElement);
            ProcessFramePayload(doc.RootElement, baseUri);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static JsonDocument BuildFallbackFrameDocument(
        JsonElement state,
        JsonElement? latestSnapshot,
        JsonElement? dispatchSpikes)
    {
        var buffer = new ArrayBufferWriter<byte>(64 * 1024);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("state");
            state.WriteTo(writer);
            writer.WriteNull("connectomeReport");
            writer.WritePropertyName("latestSnapshot");
            if (latestSnapshot is { ValueKind: JsonValueKind.Object } snapshot)
            {
                snapshot.WriteTo(writer);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WritePropertyName("outputLog");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("spikeLog");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("dispatchSpikes");
            if (dispatchSpikes is { ValueKind: JsonValueKind.Array } spikes)
            {
                spikes.WriteTo(writer);
            }
            else
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.WrittenMemory);
    }

    private async Task<JsonDocument?> FetchJsonDocumentAsync(Uri uri, CancellationToken token)
    {
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }

    private Uri BuildFrameUri(Uri baseUri)
    {
        var builder = new UriBuilder(new Uri(baseUri, "/api/v1/frame"));
        var outputSince = Math.Max(0, _lastRemoteOutputLogWallClockMs);
        var spikeSince = Math.Max(0, _lastRemoteSpikeLogWallClockMs);
        var dispatchSince = Math.Max(0, _lastRemoteDispatchWallClockMs);
        builder.Query = $"output_since_ms={outputSince}&spike_since_ms={spikeSince}&dispatch_since_ms={dispatchSince}&include_connectome=0&max_output_log={FramePollMaxOutputLog}&max_spike_log={FramePollMaxSpikeLog}&max_dispatch_spikes={FramePollMaxDispatchSpikes}";
        return builder.Uri;
    }

}
