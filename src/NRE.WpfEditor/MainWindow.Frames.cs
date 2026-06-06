using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;

namespace NRE.WpfEditor;

// Frame streaming and snapshot polling transport: NDJSON stream loop, fallback poll,
// endpoint probing, fallback frame document construction, and frame URI builders.
// Payload processing (IngestFrameState, ProcessFramePayload, ApplyNeuronHighlights, etc.)
// remains in MainWindow.xaml.cs because it is tightly woven with UI state.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private void StartFrameStreaming()
    {
        _frameStreamTask = Task.Run(() => FrameStreamLoopAsync(_workerCts.Token));
    }

    private async Task FrameStreamLoopAsync(CancellationToken token)
    {
        var reconnectDelayMs = 300;
        while (!token.IsCancellationRequested)
        {
            Uri? activeStreamEndpoint = null;
            if (_streamDisabledForSession)
            {
                await TryFallbackSnapshotPollFromWorkerAsync(token, _verifiedControlBaseUri ?? _preferredControlBaseUri);
                await Task.Delay(FramePollOnlyLoopDelay, token);
                continue;
            }

            if (DateTime.UtcNow < _frameStreamBackoffUntilUtc)
            {
                await TryFallbackSnapshotPollFromWorkerAsync(token);
                await Task.Delay(FramePollOnlyLoopDelay, token);
                continue;
            }

            try
            {
                using var resolveTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                resolveTimeout.CancelAfter(TimeSpan.FromMilliseconds(5000));
                var verifiedBaseUri = await ResolveVerifiedControlBaseUriAsync(resolveTimeout.Token);
                if (verifiedBaseUri is null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        SetTransportStatsText("Transport stats unavailable: no verified Control Program endpoint.");
                        SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})", appendToOutput: false);
                    }, DispatcherPriority.Background, token);
                    EmitFrameStreamWarning("Render transport warning: no verified Control Program endpoint; attempting fallback polling.");
                    await TryFallbackSnapshotPollFromWorkerAsync(token);
                    QueueHealthDiagnosticsProbe();
                    await Task.Delay(reconnectDelayMs, token);
                    reconnectDelayMs = Math.Min(reconnectDelayMs * 2, 3000);
                    continue;
                }

                activeStreamEndpoint = verifiedBaseUri;
                await StreamFramesFromEndpointAsync(verifiedBaseUri, token);
                reconnectDelayMs = 300;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _frameStreamConsecutiveFailures++;
                var now = DateTime.UtcNow;
                var recentlyReceivedPayload = _lastFrameStreamPayloadUtc != DateTime.MinValue &&
                                              (now - _lastFrameStreamPayloadUtc) <= TimeSpan.FromSeconds(4);
                var transientStreamDisconnect = recentlyReceivedPayload && ex is IOException;
                if (!transientStreamDisconnect)
                {
                    NoteControlEndpointFailure();
                }

                await Dispatcher.InvokeAsync(
                    () => SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})", appendToOutput: false),
                    DispatcherPriority.Background,
                    token);
                var reason = ex is OperationCanceledException
                    ? "request timed out"
                    : TrimForStatus(ex.Message, 140);
                if (_frameStreamConsecutiveFailures >= 3 || !recentlyReceivedPayload)
                {
                    var endpointLabel = activeStreamEndpoint is null ? string.Empty : $" @ {activeStreamEndpoint.Authority}";
                    EmitFrameStreamWarning($"Render transport warning: frame stream read failed{endpointLabel} ({reason}); using fallback polling.");
                }
                if (_frameStreamConsecutiveFailures >= FrameStreamBackoffFailureThreshold)
                {
                    _frameStreamBackoffUntilUtc = now + FrameStreamBackoffWindow;
                    EmitFrameStreamWarning("Render transport warning: stream unstable; temporarily using polling-only mode.");
                }

                var isPrematureEnd = ex.ToString().Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase) ||
                                     reason.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase);
                if (isPrematureEnd && _frameStreamConsecutiveFailures >= FrameStreamDisableThreshold && !_streamDisabledForSession)
                {
                    _streamDisabledForSession = true;
                    EmitFrameStreamWarning("Render transport warning: frame stream disabled for this session after repeated premature-end failures; using polling mode.");
                }

                await TryFallbackSnapshotPollFromWorkerAsync(token, activeStreamEndpoint);
                QueueHealthDiagnosticsProbe();
                await Task.Delay(reconnectDelayMs, token);
                reconnectDelayMs = Math.Min(reconnectDelayMs * 2, 3000);
            }
        }
    }

    private async Task StreamFramesFromEndpointAsync(Uri verifiedBaseUri, CancellationToken token)
    {
        var streamUri = BuildFrameStreamUri(verifiedBaseUri);
        using var request = new HttpRequestMessage(HttpMethod.Get, streamUri);
        request.Headers.Accept.ParseAdd("application/x-ndjson");

        // Apply timeout only to connection/header acquisition; do not keep this CTS alive
        // for the full stream lifetime or it can abort healthy long-lived reads.
        HttpResponseMessage response;
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            connectTimeout.CancelAfter(TimeSpan.FromMilliseconds(8000));
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectTimeout.Token);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                NoteControlEndpointFailure();
                await Dispatcher.InvokeAsync(() =>
                {
                    SetTransportStatsText($"Transport stats unavailable: HTTP {(int)response.StatusCode} from {verifiedBaseUri}");
                    SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})", appendToOutput: false);
                }, DispatcherPriority.Background, token);
                EmitFrameStreamWarning($"Render transport warning: frame stream endpoint returned HTTP {(int)response.StatusCode}.");
                QueueHealthDiagnosticsProbe();
                throw new HttpRequestException($"Frame stream endpoint returned HTTP {(int)response.StatusCode}.");
            }

            NoteControlEndpointSuccess(verifiedBaseUri);
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);

            while (!token.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }

                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonElement frame;
                try
                {
                    using var frameDoc = JsonDocument.Parse(line);
                    if (frameDoc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    frame = frameDoc.RootElement.Clone();
                }
                catch
                {
                    continue;
                }

                _frameStreamConsecutiveFailures = 0;
                _lastFrameStreamPayloadUtc = DateTime.UtcNow;
                _frameStreamBackoffUntilUtc = DateTime.MinValue;
                QueueStreamFrameForUi(frame, verifiedBaseUri, token);
            }

            if (!token.IsCancellationRequested)
            {
                throw new IOException("Frame stream disconnected.");
            }
        }
    }

    private void QueueStreamFrameForUi(JsonElement frame, Uri verifiedBaseUri, CancellationToken token)
    {
        lock (_pendingStreamFrameGate)
        {
            _pendingStreamFrame = frame;
            _pendingStreamFrameBaseUri = verifiedBaseUri;
        }

        SchedulePendingStreamFrameApply(token);
    }

    private void SchedulePendingStreamFrameApply(CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _streamFrameUiApplyScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // Once the UI delegate runs, ProcessPendingStreamFramePayload owns the
            // flag (it resets and may reschedule from its finally). We only reset
            // here if the dispatch never happens, so a delay/dispatch failure
            // can't permanently leave the flag stuck at 1.
            var dispatched = false;
            try
            {
                var delay = StreamFrameUiApplyMinInterval - (DateTime.UtcNow - _lastStreamFrameUiApplyUtc);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }

                await Dispatcher.InvokeAsync(ProcessPendingStreamFramePayload, DispatcherPriority.Background, token);
                dispatched = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Swallow non-cancellation errors so the scheduled flag is reset
                // in finally rather than freezing future updates forever.
            }
            finally
            {
                if (!dispatched)
                {
                    Interlocked.Exchange(ref _streamFrameUiApplyScheduled, 0);
                }
            }
        }, CancellationToken.None);
    }

    private void ProcessPendingStreamFramePayload()
    {
        JsonElement? frame;
        Uri? verifiedBaseUri;
        lock (_pendingStreamFrameGate)
        {
            frame = _pendingStreamFrame;
            verifiedBaseUri = _pendingStreamFrameBaseUri;
            _pendingStreamFrame = null;
            _pendingStreamFrameBaseUri = null;
        }

        try
        {
            if (frame.HasValue && verifiedBaseUri is not null)
            {
                _lastStreamFrameUiApplyUtc = DateTime.UtcNow;
                ProcessFramePayload(frame.Value, verifiedBaseUri);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _streamFrameUiApplyScheduled, 0);
            lock (_pendingStreamFrameGate)
            {
                if (_pendingStreamFrame.HasValue)
                {
                    SchedulePendingStreamFrameApply(_workerCts.Token);
                }
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

    private void EmitFrameStreamWarning(string message)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFrameStreamWarningUtc) < FrameStreamWarningCooldown)
        {
            return;
        }

        _lastFrameStreamWarningUtc = now;
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
                SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})");
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
                SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})");
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
                SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})");
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
            SetRenderStatus($"Render: unable to fetch snapshot stream (UI overruns: {_uiOverrunCount})", appendToOutput: false);
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

    private Uri BuildFrameStreamUri(Uri baseUri)
    {
        var builder = new UriBuilder(new Uri(baseUri, "/api/v1/frame/stream"));
        var outputSince = Math.Max(0, _lastRemoteOutputLogWallClockMs);
        var spikeSince = Math.Max(0, _lastRemoteSpikeLogWallClockMs);
        var dispatchSince = Math.Max(0, _lastRemoteDispatchWallClockMs);
        builder.Query = $"output_since_ms={outputSince}&spike_since_ms={spikeSince}&dispatch_since_ms={dispatchSince}&include_connectome=0&max_output_log={FrameStreamMaxOutputLog}&max_spike_log={FrameStreamMaxSpikeLog}&max_dispatch_spikes={FrameStreamMaxDispatchSpikes}&stream_interval_ms={FrameStreamRequestedIntervalMs}";
        return builder.Uri;
    }
}
