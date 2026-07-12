using System.Net.Http;
using System.Text.Json;

namespace NRE.WpfEditor;

// Control-program endpoint resolution: discovers and verifies which configured base URI
// is actually serving the NRE control API. Cached state (_verifiedControlBaseUri,
// _preferredControlBaseUri, _controlEndpointFailureCount, _lastVerifiedControlProbeUtc)
// remains in MainWindow.xaml.cs so other partials can read it without going through here.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private IReadOnlyList<Uri> EnumerateControlBaseUris()
    {
        Uri? verified;
        Uri? preferred;
        lock (_endpointStateGate)
        {
            verified = _verifiedControlBaseUri;
            preferred = _preferredControlBaseUri;
        }

        var candidates = new List<Uri>(_snapshotBaseUris.Length + 2);
        if (verified is not null)
        {
            candidates.Add(verified);
        }

        if (preferred is not null &&
            (verified is null || !string.Equals(preferred.AbsoluteUri, verified.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
        {
            candidates.Add(preferred);
        }

        foreach (var candidate in _snapshotBaseUris)
        {
            if (preferred is not null &&
                string.Equals(candidate.AbsoluteUri, preferred.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (verified is not null &&
                string.Equals(candidate.AbsoluteUri, verified.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    private async Task<Uri?> ResolveVerifiedControlBaseUriAsync(CancellationToken cancellationToken)
    {
        await _endpointResolutionGate.WaitAsync(cancellationToken);
        try
        {
            Uri? verified;
            DateTime lastProbeUtc;
            lock (_endpointStateGate)
            {
                verified = _verifiedControlBaseUri;
                lastProbeUtc = _lastVerifiedControlProbeUtc;
            }

            Uri? staleVerified = null;
            if (verified is not null)
            {
                if ((DateTime.UtcNow - lastProbeUtc) < VerifiedControlProbeInterval)
                {
                    lock (_endpointStateGate)
                    {
                        _preferredControlBaseUri = verified;
                    }
                    return verified;
                }

                if (await IsControlProgramEndpointAsync(verified, cancellationToken))
                {
                    NoteControlEndpointSuccess(verified);
                    return verified;
                }

                staleVerified = verified;
                NoteControlEndpointFailure();
            }

            foreach (var candidate in EnumerateControlBaseUris())
            {
                if (staleVerified is not null &&
                    string.Equals(candidate.AbsoluteUri, staleVerified.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!await IsControlProgramEndpointAsync(candidate, cancellationToken))
                {
                    continue;
                }

                NoteControlEndpointSuccess(candidate);
                return candidate;
            }

            if (staleVerified is not null)
            {
                var freshestDataUtc = _lastSnapshotUtc > _lastFramePayloadUtc ? _lastSnapshotUtc : _lastFramePayloadUtc;
                if (freshestDataUtc != DateTime.MinValue &&
                    (DateTime.UtcNow - freshestDataUtc) <= ControlEndpointGraceFallbackWindow)
                {
                    lock (_endpointStateGate)
                    {
                        _preferredControlBaseUri = staleVerified;
                    }
                    return staleVerified;
                }
            }

            if (_snapshotBaseUris.Length > 0)
            {
                var attempted = string.Join(", ", _snapshotBaseUris.Select(u => u.Authority).Distinct(StringComparer.OrdinalIgnoreCase));
                PostUi(() => AddOutputLog($"Control endpoint probe failed. Attempted: {attempted}"));
            }

            return null;
        }
        finally
        {
            _endpointResolutionGate.Release();
        }
    }

    private async Task<bool> IsControlProgramEndpointAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                using var healthTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                healthTimeout.CancelAfter(TimeSpan.FromMilliseconds(1200));
                using var health = await _httpClient.GetAsync(new Uri(baseUri, "/health"), healthTimeout.Token);
                _ = health.IsSuccessStatusCode;
            }
            catch
            {
                    // /health is best-effort only.
            }

            using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeTimeout.CancelAfter(TimeSpan.FromMilliseconds(2800));
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/startup-health"), probeTimeout.Token);
            var startupLooksValid = false;
            if (!response.IsSuccessStatusCode)
            {
                using var fallbackTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                fallbackTimeout.CancelAfter(TimeSpan.FromMilliseconds(5200));
                using var fallbackResponse = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/state"), fallbackTimeout.Token);
                if (!fallbackResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var fallbackJson = await fallbackResponse.Content.ReadAsStringAsync(fallbackTimeout.Token);
                using var fallbackDoc = JsonDocument.Parse(fallbackJson);
                if (fallbackDoc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                startupLooksValid = (TryGetProperty(fallbackDoc.RootElement, "serviceTelemetry", out var telemetry) && telemetry.ValueKind == JsonValueKind.Object)
                    || TryGetProperty(fallbackDoc.RootElement, "simulationClockMs", out _)
                    || TryGetProperty(fallbackDoc.RootElement, "tick", out _);
            }
            else
            {
                var json = await response.Content.ReadAsStringAsync(probeTimeout.Token);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                startupLooksValid = TryGetProperty(doc.RootElement, "serviceCount", out _)
                    || TryGetProperty(doc.RootElement, "tick", out _)
                    || TryGetProperty(doc.RootElement, "lastSnapshotTick", out _);
            }

            if (!startupLooksValid)
            {
                return false;
            }

            using var frameTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            frameTimeout.CancelAfter(TimeSpan.FromMilliseconds(3600));
            var frameUri = new Uri(baseUri, "/api/v1/frame?include_connectome=0&max_output_log=1&max_spike_log=1&max_dispatch_spikes=1");
            using var frameResponse = await _httpClient.GetAsync(frameUri, HttpCompletionOption.ResponseHeadersRead, frameTimeout.Token);
            if (!frameResponse.IsSuccessStatusCode)
            {
                return false;
            }

            await using var frameStream = await frameResponse.Content.ReadAsStreamAsync(frameTimeout.Token);
            using var frameDoc = await JsonDocument.ParseAsync(frameStream, cancellationToken: frameTimeout.Token);
            if (frameDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var hasState = TryGetProperty(frameDoc.RootElement, "state", out var stateElement) &&
                           stateElement.ValueKind == JsonValueKind.Object;
            var hasLatestSnapshotField = TryGetProperty(frameDoc.RootElement, "latestSnapshot", out _);
            var hasOutputLog = TryGetProperty(frameDoc.RootElement, "outputLog", out var outputLogElement) &&
                               outputLogElement.ValueKind == JsonValueKind.Array;

            return hasState && (hasLatestSnapshotField || hasOutputLog);
        }
        catch
        {
            return false;
        }
    }

    private void NoteControlEndpointSuccess(Uri endpoint)
    {
        lock (_endpointStateGate)
        {
            _verifiedControlBaseUri = endpoint;
            _preferredControlBaseUri = endpoint;
            _lastVerifiedControlProbeUtc = DateTime.UtcNow;
            _controlEndpointFailureCount = 0;
        }
    }

    private void NoteControlEndpointFailure()
    {
        lock (_endpointStateGate)
        {
            _controlEndpointFailureCount++;
            if (_controlEndpointFailureCount < ControlEndpointFailureThreshold)
            {
                return;
            }

            _verifiedControlBaseUri = null;
            _lastVerifiedControlProbeUtc = DateTime.MinValue;
            _controlEndpointFailureCount = 0;
        }
    }
}
