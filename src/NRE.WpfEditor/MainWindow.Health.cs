using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NRE.WpfEditor;

// Sensor watchdogs (webcam/microphone), V1 visual route health & auto-recovery,
// service-health diagnostics, input-health indicator UI helper.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private async Task SensoryHealthTimerTickAsync()
    {
        if (_sensoryHealthCheckInFlight)
        {
            return;
        }

        _sensoryHealthCheckInFlight = true;
        try
        {
            await RefreshWebcamHealthAsync();
            await RefreshMicrophoneHealthAsync();
            RefreshVisualRouteHealth();
            UpdateAvatarTransportPanel();
        }
        catch
        {
            // Keep watchdog best-effort and non-fatal.
        }
        finally
        {
            _sensoryHealthCheckInFlight = false;
        }
    }

    private void UpdateAvatarTransportPanel()
    {
        var signal = _avatarService.LatestSignal;
        var movement = _avatarService.LatestActionOutput.Movement;
        AvatarTransportWorkerText.Text = $"Worker: processed {_avatarService.ProcessedCommands:N0}, failed {_avatarService.FailedCommands:N0}";
        AvatarTransportQueueText.Text = $"Queue: {_avatarService.PendingCommandCount}/64, dropped {_avatarService.DroppedCommands:N0}";
        AvatarTransportMotorText.Text = $"Motor spikes: {signal.MotorEvents:N0}, idle ticks {signal.TicksWithoutMotorDispatch:N0}";
        AvatarTransportDriveText.Text = $"Drive L/R: {signal.LeftMotorDrive:0.000} / {signal.RightMotorDrive:0.000}";
        AvatarTransportMotionText.Text = $"Motion: {movement.ForwardSpeed:0.000} m/s, {movement.TurnRateDeg:0.0} deg/s";
        AvatarTransportVisionText.Text = $"Vision queue: {_avatarService.PublishedSightOutputCount:N0}";
    }

    private async Task RefreshWebcamHealthAsync()
    {
        if (!_webcamRunning)
        {
            SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Idle, "Webcam pipeline: inactive");
            return;
        }

        if (_lastWebcamFrameUtc == DateTime.MinValue)
        {
            SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Warning, "Webcam pipeline: warming up");
            return;
        }

        var now = DateTime.UtcNow;
        var age = now - _lastWebcamFrameUtc;
        if (age <= WebcamSignalStallTimeout)
        {
            SetInputHealthIndicator(
                WebcamHealthLight,
                WebcamHealthText,
                InputHealthState.Healthy,
                $"Webcam pipeline: healthy ({age.TotalMilliseconds:0} ms frame age)");
            return;
        }

        if (age <= WebcamHardReconnectTimeout)
        {
            SetInputHealthIndicator(
                WebcamHealthLight,
                WebcamHealthText,
                InputHealthState.Warning,
                $"Webcam pipeline: stalled ({age.TotalSeconds:0.0}s), waiting reconnect");
            return;
        }

        SetInputHealthIndicator(
            WebcamHealthLight,
            WebcamHealthText,
            InputHealthState.Failed,
            $"Webcam pipeline: hard stall ({age.TotalSeconds:0.0}s), auto-recovering");

        if (_webcamInputInFlight || (now - _lastWebcamWatchdogRecoveryUtc) < WebcamWatchdogRecoveryCooldown)
        {
            return;
        }

        _lastWebcamWatchdogRecoveryUtc = now;
        AddOutputLog("Webcam watchdog: stalled capture detected, restarting webcam input.");
        await RestartWebcamInputFromWatchdogAsync();
    }

    private async Task RefreshMicrophoneHealthAsync()
    {
        if (!_microphoneRunning)
        {
            SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Idle, "Microphone pipeline: inactive");
            return;
        }

        if (_lastMicrophoneDataUtc == DateTime.MinValue)
        {
            SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Warning, "Microphone pipeline: warming up");
            return;
        }

        var now = DateTime.UtcNow;
        var age = now - _lastMicrophoneDataUtc;
        if (age <= MicrophoneSignalStallTimeout)
        {
            SetInputHealthIndicator(
                MicrophoneHealthLight,
                MicrophoneHealthText,
                InputHealthState.Healthy,
                $"Microphone pipeline: healthy ({age.TotalMilliseconds:0} ms sample age)");
            return;
        }

        SetInputHealthIndicator(
            MicrophoneHealthLight,
            MicrophoneHealthText,
            InputHealthState.Warning,
            $"Microphone pipeline: stalled ({age.TotalSeconds:0.0}s), auto-recovering");

        if (_microphoneInputInFlight || (now - _lastMicrophoneWatchdogRecoveryUtc) < MicrophoneWatchdogRecoveryCooldown)
        {
            return;
        }

        _lastMicrophoneWatchdogRecoveryUtc = now;
        AddOutputLog("Microphone watchdog: stalled capture detected, restarting microphone input.");
        await RestartMicrophoneInputFromWatchdogAsync();
    }

    private void RefreshVisualRouteHealth()
    {
        if (!_webcamRunning)
        {
            SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Idle, "Retina route: awaiting webcam input");
            return;
        }

        if (_visualRouteRecoveryInFlight)
        {
            SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Warning, "Retina route: recovery in progress");
            return;
        }

        if (_retinaRouteConsecutiveFailures <= 0)
        {
            if (_lastRetinaRouteSuccessUtc == DateTime.MinValue)
            {
                SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Warning, "Retina route: waiting first successful dispatch");
                return;
            }

            var successAge = DateTime.UtcNow - _lastRetinaRouteSuccessUtc;
            if (successAge > RetinaRouteStallWarningTimeout)
            {
                SetInputHealthIndicator(
                    VisualRouteHealthLight,
                    VisualRouteHealthText,
                    InputHealthState.Warning,
                    $"Retina route: no recent deliveries ({successAge.TotalSeconds:0.0}s)");
                return;
            }

            SetInputHealthIndicator(
                VisualRouteHealthLight,
                VisualRouteHealthText,
                InputHealthState.Healthy,
                $"Retina route: healthy ({successAge.TotalMilliseconds:0} ms since delivery)");
            return;
        }

        var failureAge = _lastRetinaRouteFailureUtc == DateTime.MinValue
            ? 0.0
            : (DateTime.UtcNow - _lastRetinaRouteFailureUtc).TotalSeconds;
        var failureState = _retinaRouteConsecutiveFailures >= RetinaRouteRecoveryFailureThreshold
            ? InputHealthState.Failed
            : InputHealthState.Warning;
        SetInputHealthIndicator(
            VisualRouteHealthLight,
            VisualRouteHealthText,
            failureState,
            $"Retina route: failures={_retinaRouteConsecutiveFailures} (last {failureAge:0.0}s)");
    }

    private async Task RestartWebcamInputFromWatchdogAsync()
    {
        try
        {
            if (await StopWebcamInputAsync())
            {
                await ToggleWebcamInputAsync();
            }
        }
        catch (Exception ex)
        {
            AddOutputLog($"Webcam watchdog restart failed: {ex.Message}");
        }
    }

    private async Task RestartMicrophoneInputFromWatchdogAsync()
    {
        try
        {
            if (await StopMicrophoneInputAsync())
            {
                await ToggleMicrophoneInputAsync();
            }
        }
        catch (Exception ex)
        {
            AddOutputLog($"Microphone watchdog restart failed: {ex.Message}");
        }
    }

    private void NoteVisualRouteDispatchSuccess()
    {
        _lastRetinaRouteSuccessUtc = DateTime.UtcNow;
        _retinaRouteConsecutiveFailures = 0;
    }

    private async Task NoteVisualRouteDispatchFailureAsync(string reason, CancellationToken token)
    {
        _lastRetinaRouteFailureUtc = DateTime.UtcNow;
        _retinaRouteConsecutiveFailures++;
        if (_retinaRouteConsecutiveFailures < RetinaRouteRecoveryFailureThreshold)
        {
            return;
        }

        await TryAutoRecoverVisualRouteAsync(reason, token);
    }

    private async Task TryAutoRecoverVisualRouteAsync(string reason, CancellationToken token)
    {
        if (_visualRouteRecoveryInFlight)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastRetinaRouteRecoveryUtc) < RetinaRouteRecoveryCooldown)
        {
            return;
        }

        _visualRouteRecoveryInFlight = true;
        _lastRetinaRouteRecoveryUtc = now;
        try
        {
            PostUi(() => SetInputHealthIndicator(
                VisualRouteHealthLight,
                VisualRouteHealthText,
                InputHealthState.Warning,
                $"Retina route: recovering ({TrimForStatus(reason)})"));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromMilliseconds(4500));
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                PostUi(() => AddOutputLog("Retina route auto-recovery skipped: Control Program endpoint unavailable."));
                return;
            }

            var request = new
            {
                StructureId = "Retina",
                Hemisphere = (string?)null
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/restart-service"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                PostUi(() => AddOutputLog($"Retina route auto-recovery failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 180)}"));
                return;
            }

            var restarted = 0;
            var healthy = 0;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    restarted = GetInt(doc.RootElement, "restarted");
                    healthy = GetInt(doc.RootElement, "healthy");
                }
            }
            catch
            {
                // Best-effort parse.
            }

            PostUi(() => AddOutputLog($"Retina route auto-recovery requested: restarted={restarted}, healthy={healthy}."));
        }
        catch (Exception ex)
        {
            PostUi(() => AddOutputLog($"Retina route auto-recovery warning: {TrimForStatus(ex.Message)}"));
        }
        finally
        {
            _visualRouteRecoveryInFlight = false;
        }
    }

    private static string TrimForStatus(string? text, int maxLen = 96)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "n/a";
        }

        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maxLen ? normalized : $"{normalized[..maxLen]}...";
    }

    // Cached frozen brushes per health state. Replaces the two `new SolidColorBrush`
    // allocations that used to run on every sensory health poll for every indicator.
    private static readonly (SolidColorBrush Fill, SolidColorBrush Stroke) _healthBrushHealthy = BuildHealthBrushPair(Color.FromRgb(34, 197, 94));
    private static readonly (SolidColorBrush Fill, SolidColorBrush Stroke) _healthBrushWarning = BuildHealthBrushPair(Color.FromRgb(245, 158, 11));
    private static readonly (SolidColorBrush Fill, SolidColorBrush Stroke) _healthBrushFailed = BuildHealthBrushPair(Color.FromRgb(239, 68, 68));
    private static readonly (SolidColorBrush Fill, SolidColorBrush Stroke) _healthBrushIdle = BuildHealthBrushPair(Color.FromRgb(100, 116, 139));

    private static (SolidColorBrush Fill, SolidColorBrush Stroke) BuildHealthBrushPair(Color fill)
    {
        var fillBrush = new SolidColorBrush(fill);
        var strokeBrush = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Clamp(fill.R * 0.55, 0, 255),
            (byte)Math.Clamp(fill.G * 0.55, 0, 255),
            (byte)Math.Clamp(fill.B * 0.55, 0, 255)));
        fillBrush.Freeze();
        strokeBrush.Freeze();
        return (fillBrush, strokeBrush);
    }

    private void SetInputHealthIndicator(System.Windows.Shapes.Ellipse? indicator, TextBlock? caption, InputHealthState state, string message)
    {
        if (indicator is not null)
        {
            var pair = state switch
            {
                InputHealthState.Healthy => _healthBrushHealthy,
                InputHealthState.Warning => _healthBrushWarning,
                InputHealthState.Failed => _healthBrushFailed,
                _ => _healthBrushIdle
            };

            indicator.Fill = pair.Fill;
            indicator.Stroke = pair.Stroke;
        }

        if (caption is not null)
        {
            caption.Text = message;
        }
    }

    private void EmitServiceHealthDiagnostics(Dictionary<string, ServiceHealthEntry>? telemetry, string? queryIssue)
    {
        if (telemetry is null)
        {
            ApplyStructureStatusBadges(null, queryIssue);
            var message = $"Service health unavailable: {queryIssue ?? "state endpoint not reachable"}";
            if (!string.Equals(message, _lastServiceHealthSummary, StringComparison.Ordinal))
            {
                _lastServiceHealthSummary = message;
                AddOutputLog(message);
            }

            return;
        }

        ApplyStructureStatusBadges(telemetry, null);
        var summaries = telemetry
            .Where(kvp => !string.Equals(kvp.Value.Status, "OK", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => string.IsNullOrWhiteSpace(kvp.Value.Error)
                ? $"{kvp.Key}={kvp.Value.Status}"
                : $"{kvp.Key}={kvp.Value.Status} ({kvp.Value.Error})")
            .ToList();

        if (summaries.Count == 0)
        {
            _pendingServiceHealthSummary = string.Empty;
            _pendingServiceHealthSummaryCount = 0;
            if (!string.IsNullOrWhiteSpace(_lastServiceHealthSummary))
            {
                _lastServiceHealthSummary = string.Empty;
                AddOutputLog("All structure services currently report OK.");
            }

            return;
        }

        summaries.Sort(StringComparer.OrdinalIgnoreCase);
        var preview = string.Join(", ", summaries.Take(8));
        if (summaries.Count > 8)
        {
            preview += $", ... (+{summaries.Count - 8} more)";
        }

        var summaryMessage = $"Service health: {summaries.Count} non-OK -> {preview}";
        if (!string.Equals(summaryMessage, _pendingServiceHealthSummary, StringComparison.Ordinal))
        {
            _pendingServiceHealthSummary = summaryMessage;
            _pendingServiceHealthSummaryCount = 1;
        }
        else
        {
            _pendingServiceHealthSummaryCount++;
        }

        if (summaries.Count < 4 && _pendingServiceHealthSummaryCount < 3)
        {
            return;
        }

        if (!string.Equals(summaryMessage, _lastServiceHealthSummary, StringComparison.Ordinal))
        {
            _lastServiceHealthSummary = summaryMessage;
            AddOutputLog(summaryMessage);
            if (summaries.Count >= 24)
            {
                AddOutputLog("Likely cause: structure services are not running on configured ports, or they are timing out before TickAck.");
            }
        }
    }

    private async Task EmitServiceHealthDiagnosticsAsync()
    {
        var (telemetry, queryIssue) = await FetchServiceTelemetryAsync();
        EmitServiceHealthDiagnostics(telemetry, queryIssue);
    }

    private async Task EmitServiceHealthDiagnosticsThrottledAsync(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - _lastFallbackHealthProbeUtc) < FallbackHealthProbeInterval)
        {
            return;
        }

        _lastFallbackHealthProbeUtc = now;
        await EmitServiceHealthDiagnosticsAsync();
    }

    private void QueueHealthDiagnosticsProbe(bool force = false)
    {
        if (Dispatcher.CheckAccess())
        {
            _ = EmitServiceHealthDiagnosticsThrottledAsync(force);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => _ = EmitServiceHealthDiagnosticsThrottledAsync(force),
            DispatcherPriority.Background);
    }
}
