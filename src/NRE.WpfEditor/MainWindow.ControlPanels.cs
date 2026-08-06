using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NRE.WpfEditor;

// Control-panel handlers: performance profile buttons, auto-profile sliders,
// sleep-pressure / min-wake-ticks controls, input-gates checkboxes, neuronal
// cognition curriculum controls,
// structure restart helpers.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private void UpdateAutoProfileControlLabels()
    {
        if (AutoProfileWarmupTicksText is not null && AutoProfileWarmupTicksSlider is not null)
        {
            AutoProfileWarmupTicksText.Text = ((int)Math.Round(AutoProfileWarmupTicksSlider.Value)).ToString();
        }

        if (AutoProfileManualHoldTicksText is not null && AutoProfileManualHoldTicksSlider is not null)
        {
            AutoProfileManualHoldTicksText.Text = ((int)Math.Round(AutoProfileManualHoldTicksSlider.Value)).ToString();
        }

        if (AutoProfileDegradeNonOkRatioText is not null && AutoProfileDegradeNonOkRatioSlider is not null)
        {
            AutoProfileDegradeNonOkRatioText.Text = AutoProfileDegradeNonOkRatioSlider.Value.ToString("0.000");
        }

        if (AutoProfileDegradeAckLatencyMsText is not null && AutoProfileDegradeAckLatencyMsSlider is not null)
        {
            AutoProfileDegradeAckLatencyMsText.Text = ((int)Math.Round(AutoProfileDegradeAckLatencyMsSlider.Value)).ToString();
        }

        if (AutoProfileDegradeSnapshotAgeTicksText is not null && AutoProfileDegradeSnapshotAgeTicksSlider is not null)
        {
            AutoProfileDegradeSnapshotAgeTicksText.Text = ((int)Math.Round(AutoProfileDegradeSnapshotAgeTicksSlider.Value)).ToString();
        }

        if (AutoProfileDegradeConsecutiveTicksText is not null && AutoProfileDegradeConsecutiveTicksSlider is not null)
        {
            AutoProfileDegradeConsecutiveTicksText.Text = ((int)Math.Round(AutoProfileDegradeConsecutiveTicksSlider.Value)).ToString();
        }

        if (AutoProfileRecoveryNonOkRatioText is not null && AutoProfileRecoveryNonOkRatioSlider is not null)
        {
            AutoProfileRecoveryNonOkRatioText.Text = AutoProfileRecoveryNonOkRatioSlider.Value.ToString("0.000");
        }

        if (AutoProfileRecoveryAckLatencyMsText is not null && AutoProfileRecoveryAckLatencyMsSlider is not null)
        {
            AutoProfileRecoveryAckLatencyMsText.Text = ((int)Math.Round(AutoProfileRecoveryAckLatencyMsSlider.Value)).ToString();
        }

        if (AutoProfileRecoverySnapshotAgeTicksText is not null && AutoProfileRecoverySnapshotAgeTicksSlider is not null)
        {
            AutoProfileRecoverySnapshotAgeTicksText.Text = ((int)Math.Round(AutoProfileRecoverySnapshotAgeTicksSlider.Value)).ToString();
        }

        if (AutoProfileRecoveryConsecutiveTicksText is not null && AutoProfileRecoveryConsecutiveTicksSlider is not null)
        {
            AutoProfileRecoveryConsecutiveTicksText.Text = ((int)Math.Round(AutoProfileRecoveryConsecutiveTicksSlider.Value)).ToString();
        }
    }

    private static double ClampSliderValue(Slider? slider, double value)
    {
        if (slider is null)
        {
            return value;
        }

        return Math.Clamp(value, slider.Minimum, slider.Maximum);
    }

    private void SetSliderValue(Slider? slider, double value)
    {
        if (slider is null)
        {
            return;
        }

        var clamped = ClampSliderValue(slider, value);
        if (Math.Abs(slider.Value - clamped) > 0.0005)
        {
            slider.Value = clamped;
        }
    }

    private void SyncReasoningControlsFromState(JsonElement root)
    {
        if (_reasoningApplyCurriculumInFlight)
        {
            return;
        }

        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        var foundAny = false;
        _suppressReasoningControlEvents = true;
        try
        {
            if (TryGetProperty(root, "curriculum", out var curriculum) && curriculum.ValueKind == JsonValueKind.Object)
            {
                foundAny = true;
                if (ReasoningCurriculumEnabledCheckBox is not null)
                {
                    ReasoningCurriculumEnabledCheckBox.IsChecked = GetBool(curriculum, "enabled", true);
                }

                SetSliderValue(ReasoningCurriculumStageSlider, GetInt(curriculum, "stageIndex"));
            }

        }
        finally
        {
            _suppressReasoningControlEvents = false;
        }

        if (!foundAny)
        {
            return;
        }

        UpdateReasoningSliderLabels();
        if (ReasoningStatusText is not null)
        {
            var tick = GetLong(root, "tick");
            ReasoningStatusText.Text = tick > 0
                ? $"Neuronal cognition: curriculum synced (tick {tick})"
                : "Neuronal cognition: curriculum synced";
        }
    }

    private void UpdateReasoningSliderLabels()
    {
        if (ReasoningCurriculumStageText is not null && ReasoningCurriculumStageSlider is not null)
        {
            ReasoningCurriculumStageText.Text = ((int)Math.Round(ReasoningCurriculumStageSlider.Value)).ToString();
        }

    }

    private async Task ApplyReasoningCurriculumAsync()
    {
        if (_reasoningApplyCurriculumInFlight)
        {
            AddOutputLog("Reasoning curriculum update already in progress.");
            return;
        }

        _reasoningApplyCurriculumInFlight = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = "Reasoning curriculum: control endpoint unavailable";
                }
                AddOutputLog("Reasoning curriculum update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                Enabled = ReasoningCurriculumEnabledCheckBox?.IsChecked ?? true,
                StageIndex = (int)Math.Round(ReasoningCurriculumStageSlider?.Value ?? 0),
                ResetProgress = ReasoningCurriculumResetCheckBox?.IsChecked ?? false
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/reasoning/curriculum"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                NoteControlEndpointFailure();
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = $"Reasoning curriculum: HTTP {(int)response.StatusCode}";
                }
                AddOutputLog($"Reasoning curriculum update failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 220)}");
                return;
            }

            NoteControlEndpointSuccess(baseUri);
            if (ReasoningStatusText is not null)
            {
                ReasoningStatusText.Text = "Reasoning curriculum: applied";
            }
            AddOutputLog("Reasoning curriculum settings updated.");
            if (ReasoningCurriculumResetCheckBox is not null)
            {
                ReasoningCurriculumResetCheckBox.IsChecked = false;
            }

            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    SetReasoningText(FormatReasoningState(doc.RootElement));
                    SyncReasoningControlsFromState(doc.RootElement);
                }
            }
        }
        catch (Exception ex)
        {
            NoteControlEndpointFailure();
            if (ReasoningStatusText is not null)
            {
                ReasoningStatusText.Text = $"Reasoning curriculum: error ({ex.GetType().Name})";
            }
            AddOutputLog($"Reasoning curriculum update failed: {ex.Message}");
        }
        finally
        {
            _reasoningApplyCurriculumInFlight = false;
        }
    }



    private void ReasoningSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressReasoningControlEvents)
        {
            return;
        }

        UpdateReasoningSliderLabels();
    }

    // Async-void button handlers wrap the awaited work in SafeHandlerAsync so an
    // HTTP failure inside the body becomes a log line instead of crashing the
    // WPF dispatcher (which would tear down the whole editor).
    private async void ReasoningApplyCurriculumButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(ApplyReasoningCurriculumAsync, "Apply reasoning curriculum");

    // Webcam UI handlers (resolution combo, preview viewport sizing) moved to MainWindow.Webcam.cs.

    private static string FormatNeuronBudgetLabel(int edge)
    {
        var count = edge * edge * edge;
        return $"display {edge}x{edge}x{edge} ({count})";
    }

    // Camera preset views and transform-lock toggle moved to MainWindow.Camera.cs.

    private async void RestartSimButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(RestartSimulationAsync, "Restart simulation");

    private async void DiagnosticPerfButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(() => ApplyPerformanceProfileAsync("diagnostic"), "Apply diagnostic profile");

    private async void NormalPerfButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(() => ApplyPerformanceProfileAsync("normal"), "Apply normal profile");

    private async void FastPerfButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(() => ApplyPerformanceProfileAsync("fast"), "Apply fast profile");

    private async void HeadlessPerfButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(() => ApplyPerformanceProfileAsync("headless"), "Apply headless profile");

    private async void ApplyAutoProfileButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(async () =>
        {
            _autoProfileDebounceTimer.Stop();
            await ApplyAutoProfileControlsAsync(bypassCooldown: true);
        }, "Apply auto-profile controls");
    private async Task TryRestartSelectedOffStructureAsync()
    {
        if (StructuresTree.SelectedItem is not TreeViewItem item || item.Tag is not StructureTreeNode node)
        {
            return;
        }

        if (!_structureStatusBadges.TryGetValue(node.SnapshotId, out var badge))
        {
            return;
        }

        if (!string.Equals(badge.BadgeText.Text, "OFF", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_structureRestartInFlight.Contains(node.SnapshotId))
        {
            AddOutputLog($"Restart already in progress for {node.DisplayName}.");
            return;
        }

        var now = DateTime.UtcNow;
        if (_lastStructureRestartUtc.TryGetValue(node.SnapshotId, out var lastAttemptUtc))
        {
            var remaining = StructureRestartCooldown - (now - lastAttemptUtc);
            if (remaining > TimeSpan.Zero)
            {
                AddOutputLog($"Restart cooldown active for {node.DisplayName}: {remaining.TotalSeconds:0.0}s remaining.");
                return;
            }
        }

        await RestartStructureAsync(node.DisplayName, node.SnapshotId);
    }

    private async Task RestartStructureAsync(string displayName, string snapshotId)
    {
        if (!_structureRestartInFlight.Add(snapshotId))
        {
            AddOutputLog($"Restart already in progress for {displayName}.");
            return;
        }

        _lastStructureRestartUtc[snapshotId] = DateTime.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog($"Restart skipped for {displayName}: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                StructureId = snapshotId
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/restart-service"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Restart failed for {displayName}: HTTP {(int)response.StatusCode}. {payload}");
                return;
            }

            AddOutputLog($"Restart requested for {displayName} ({snapshotId}).");
            await Task.Delay(200, _workerCts.Token);
            await PollSnapshotAsync();
        }
        catch (Exception ex)
        {
            AddOutputLog($"Restart failed for {displayName}: {ex.Message}");
        }
        finally
        {
            _structureRestartInFlight.Remove(snapshotId);
        }
    }

    private async Task RestartSimulationAsync()
    {
        if (_simRestartInFlight)
        {
            AddOutputLog("Simulation restart already in progress.");
            return;
        }

        var now = DateTime.UtcNow;
        var remaining = SimulationRestartCooldown - (now - _lastSimRestartUtc);
        if (remaining > TimeSpan.Zero)
        {
            AddOutputLog($"Simulation restart cooldown: {remaining.TotalSeconds:0.0}s remaining.");
            return;
        }

        _simRestartInFlight = true;
        _lastSimRestartUtc = now;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Simulation restart skipped: Control Program endpoint not available.");
                return;
            }

            using var response = await _httpClient.PostAsync(new Uri(baseUri, "/api/v1/admin/restart-sim"), null, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Simulation restart failed: HTTP {(int)response.StatusCode}. {payload}");
                return;
            }

            _lastRemoteOutputLogWallClockMs = 0;
            _lastRemoteSpikeLogWallClockMs = 0;
            _lastRemoteDispatchWallClockMs = 0;
            _unmatchedSpikeDiagnostics.Clear();
            AddOutputLog("Simulation restart requested.");
            await Task.Delay(200, _workerCts.Token);
            await PollSnapshotAsync();
        }
        catch (Exception ex)
        {
            AddOutputLog($"Simulation restart failed: {ex.Message}");
        }
        finally
        {
            _simRestartInFlight = false;
        }
    }

    private async Task ApplyPerformanceProfileAsync(string profile)
    {
        if (_perfProfileSwitchInFlight)
        {
            AddOutputLog("Performance profile switch already in progress.");
            return;
        }

        var now = DateTime.UtcNow;
        var remaining = PerfProfileSwitchCooldown - (now - _lastPerfProfileSwitchUtc);
        if (remaining > TimeSpan.Zero)
        {
            AddOutputLog($"Performance profile cooldown: {remaining.TotalSeconds:0.0}s remaining.");
            return;
        }

        _perfProfileSwitchInFlight = true;
        _lastPerfProfileSwitchUtc = now;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Performance profile switch skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                Profile = profile,
                RestartSimulation = true
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/perf-profile"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Performance profile switch failed ({profile}): HTTP {(int)response.StatusCode}. {payload}");
                return;
            }

            _lastRemoteOutputLogWallClockMs = 0;
            _lastRemoteSpikeLogWallClockMs = 0;
            _lastRemoteDispatchWallClockMs = 0;
            _unmatchedSpikeDiagnostics.Clear();
            AddOutputLog($"Performance profile switched to {profile.ToUpperInvariant()}.");
            await Task.Delay(220, _workerCts.Token);
            await PollSnapshotAsync();
            await RefreshAutoProfileControlsFromControlAsync(baseUri);
        }
        catch (Exception ex)
        {
            AddOutputLog($"Performance profile switch failed ({profile}): {ex.Message}");
        }
        finally
        {
            _perfProfileSwitchInFlight = false;
        }
    }

    private async Task ApplyAutoProfileControlsAsync(bool bypassCooldown = false)
    {
        if (_autoProfileUpdateInFlight)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var remaining = AutoProfileUpdateCooldown - (now - _lastAutoProfileUpdateUtc);
        if (!bypassCooldown && remaining > TimeSpan.Zero)
        {
            return;
        }

        _autoProfileUpdateInFlight = true;
        _lastAutoProfileUpdateUtc = now;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Auto profile update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                Enabled = AutoProfileEnabledCheckBox?.IsChecked == true,
                AllowRecovery = AutoProfileAllowRecoveryCheckBox?.IsChecked == true,
                WarmupTicks = AutoProfileWarmupTicksSlider is null ? 80 : (int)Math.Round(AutoProfileWarmupTicksSlider.Value),
                ManualHoldTicks = AutoProfileManualHoldTicksSlider is null ? 500 : (int)Math.Round(AutoProfileManualHoldTicksSlider.Value),
                DegradeNonOkRatio = AutoProfileDegradeNonOkRatioSlider?.Value ?? 0.12,
                DegradeAckLatencyMs = AutoProfileDegradeAckLatencyMsSlider is null ? 900.0 : Math.Round(AutoProfileDegradeAckLatencyMsSlider.Value, 0),
                DegradeSnapshotAgeTicks = AutoProfileDegradeSnapshotAgeTicksSlider is null ? 20 : (long)Math.Round(AutoProfileDegradeSnapshotAgeTicksSlider.Value),
                DegradeConsecutiveTicks = AutoProfileDegradeConsecutiveTicksSlider is null ? 6 : (int)Math.Round(AutoProfileDegradeConsecutiveTicksSlider.Value),
                RecoveryNonOkRatio = AutoProfileRecoveryNonOkRatioSlider?.Value ?? 0.02,
                RecoveryAckLatencyMs = AutoProfileRecoveryAckLatencyMsSlider is null ? 350.0 : Math.Round(AutoProfileRecoveryAckLatencyMsSlider.Value, 0),
                RecoverySnapshotAgeTicks = AutoProfileRecoverySnapshotAgeTicksSlider is null ? 8 : (long)Math.Round(AutoProfileRecoverySnapshotAgeTicksSlider.Value),
                RecoveryConsecutiveTicks = AutoProfileRecoveryConsecutiveTicksSlider is null ? 350 : (int)Math.Round(AutoProfileRecoveryConsecutiveTicksSlider.Value)
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/auto-profile"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Auto profile update failed: HTTP {(int)response.StatusCode}. {payload}");
                AutoProfileStatusText.Text = "Auto profile: update failed";
                return;
            }

            AddOutputLog("Auto profile tuning applied.");
            AutoProfileStatusText.Text = "Auto profile: runtime settings applied";
            await RefreshAutoProfileControlsFromControlAsync(baseUri);
        }
        catch (Exception ex)
        {
            AddOutputLog($"Auto profile update failed: {ex.Message}");
            AutoProfileStatusText.Text = "Auto profile: update failed";
        }
        finally
        {
            _autoProfileUpdateInFlight = false;
        }
    }

    private async Task RefreshAutoProfileControlsFromControlAsync(Uri? preferredBaseUri = null, CancellationToken cancellationToken = default)
    {
        using var localTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        localTimeout.CancelAfter(TimeSpan.FromMilliseconds(2600));
        var token = localTimeout.Token;

        var baseUri = preferredBaseUri ?? await ResolveVerifiedControlBaseUriAsync(token);
        if (baseUri is null)
        {
            AutoProfileStatusText.Text = "Auto profile: endpoint unavailable";
            return;
        }

        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/admin/auto-profile"), token);
            if (!response.IsSuccessStatusCode)
            {
                AutoProfileStatusText.Text = $"Auto profile: HTTP {(int)response.StatusCode}";
                return;
            }

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var parsed = ParseAutoProfileSettings(doc.RootElement);
            if (parsed is null)
            {
                return;
            }

            SetAutoProfileControlsUi(parsed, syncedFromRuntime: true);
            AutoProfileStatusText.Text = $"Auto profile: {(parsed.Enabled ? "enabled" : "disabled")} | degrade {parsed.DegradeNonOkRatio:0.000} / {parsed.DegradeAckLatencyMs:0}ms";
        }
        catch
        {
            // Keep polling resilient; frame path still updates status.
        }
    }
    private void SyncAutoProfileFromState(JsonElement stateElement)
    {
        if (_autoProfileDebounceTimer.IsEnabled || _autoProfileUpdateInFlight || _suppressAutoProfileControlEvents)
        {
            return;
        }

        if (!TryGetProperty(stateElement, "autoProfile", out var autoProfileElement) || autoProfileElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var parsed = ParseAutoProfileSettings(autoProfileElement);
        if (parsed is null)
        {
            return;
        }

        SetAutoProfileControlsUi(parsed, syncedFromRuntime: true);
        AutoProfileStatusText.Text = $"Auto profile: {(parsed.Enabled ? "enabled" : "disabled")} | degrade {parsed.DegradeNonOkRatio:0.000} / {parsed.DegradeAckLatencyMs:0}ms";
    }

    private void SyncInputGatesFromState(JsonElement stateElement)
    {
        if (_inputGatesUpdateInFlight || _suppressInputGatesControlEvents)
        {
            return;
        }

        if (!TryGetProperty(stateElement, "inputGates", out var inputGatesElement) || inputGatesElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var parsed = ParseInputGateSettings(inputGatesElement);
        if (parsed is null)
        {
            return;
        }

        SetInputGatesControlsUi(parsed, syncedFromRuntime: true);
        InputGatesStatusText.Text =
            $"Input gates: avatar {(parsed.AvatarVisionEnabled ? "on" : "off")} | spontaneous {(parsed.SpontaneousSpikingEnabled ? "on" : "off")}";
    }

    private void SetInputGatesControlsUi(InputGateControlSettings settings, bool syncedFromRuntime)
    {
        var avatarChanged = AvatarVisionInputGateCheckBox is not null && AvatarVisionInputGateCheckBox.IsChecked != settings.AvatarVisionEnabled;
        var spontaneousChanged = SpontaneousSpikingInputGateCheckBox is not null && SpontaneousSpikingInputGateCheckBox.IsChecked != settings.SpontaneousSpikingEnabled;

        _suppressInputGatesControlEvents = true;
        try
        {
            if (AvatarVisionInputGateCheckBox is not null)
            {
                AvatarVisionInputGateCheckBox.IsChecked = settings.AvatarVisionEnabled;
            }

            if (SpontaneousSpikingInputGateCheckBox is not null)
            {
                SpontaneousSpikingInputGateCheckBox.IsChecked = settings.SpontaneousSpikingEnabled;
            }
        }
        finally
        {
            _suppressInputGatesControlEvents = false;
        }

        if (syncedFromRuntime && (avatarChanged || spontaneousChanged))
        {
            AddOutputLog(
                $"Input gates synced from runtime: avatarVision={(settings.AvatarVisionEnabled ? "on" : "off")}, spontaneousSpiking={(settings.SpontaneousSpikingEnabled ? "on" : "off")}.");
        }
    }

    private async Task ApplyInputGatesControlsAsync(bool bypassCooldown = false)
    {
        if (_inputGatesUpdateInFlight)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var remaining = InputGatesUpdateCooldown - (now - _lastInputGatesUpdateUtc);
        if (!bypassCooldown && remaining > TimeSpan.Zero)
        {
            return;
        }

        _inputGatesUpdateInFlight = true;
        _lastInputGatesUpdateUtc = now;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                InputGatesStatusText.Text = "Input gates: endpoint unavailable";
                AddOutputLog("Input gates update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new InputGateControlSettings(
                AvatarVisionEnabled: AvatarVisionInputGateCheckBox?.IsChecked == true,
                SpontaneousSpikingEnabled: SpontaneousSpikingInputGateCheckBox?.IsChecked == true);
            InputGatesStatusText.Text = "Input gates: applying runtime update...";

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/input-gates"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                InputGatesStatusText.Text = $"Input gates: HTTP {(int)response.StatusCode}";
                AddOutputLog($"Input gates update failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 220)}");
                return;
            }

            InputGateControlSettings? parsed = null;
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                parsed = ParseInputGateSettings(doc.RootElement);
            }

            var effective = parsed ?? request;
            SetInputGatesControlsUi(effective, syncedFromRuntime: true);
            InputGatesStatusText.Text =
                $"Input gates: avatar {(effective.AvatarVisionEnabled ? "on" : "off")} | spontaneous {(effective.SpontaneousSpikingEnabled ? "on" : "off")}";
        }
        catch (Exception ex)
        {
            InputGatesStatusText.Text = "Input gates: update failed";
            AddOutputLog($"Input gates update failed: {ex.Message}");
        }
        finally
        {
            _inputGatesUpdateInFlight = false;
        }
    }

    private async Task RefreshInputGatesControlsFromControlAsync(Uri? preferredBaseUri = null, CancellationToken cancellationToken = default)
    {
        using var localTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        localTimeout.CancelAfter(TimeSpan.FromMilliseconds(2500));
        var token = localTimeout.Token;

        var baseUri = preferredBaseUri ?? await ResolveVerifiedControlBaseUriAsync(token);
        if (baseUri is null)
        {
            InputGatesStatusText.Text = "Input gates: endpoint unavailable";
            return;
        }

        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/admin/input-gates"), token);
            if (!response.IsSuccessStatusCode)
            {
                InputGatesStatusText.Text = $"Input gates: HTTP {(int)response.StatusCode}";
                return;
            }

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            var parsed = ParseInputGateSettings(doc.RootElement);
            if (parsed is null)
            {
                return;
            }

            SetInputGatesControlsUi(parsed, syncedFromRuntime: true);
            InputGatesStatusText.Text =
                $"Input gates: avatar {(parsed.AvatarVisionEnabled ? "on" : "off")} | spontaneous {(parsed.SpontaneousSpikingEnabled ? "on" : "off")}";
        }
        catch
        {
            // Keep polling resilient; frame path still updates status.
        }
    }

    private static InputGateControlSettings? ParseInputGateSettings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new InputGateControlSettings(
            AvatarVisionEnabled: GetBool(element, "avatarVisionEnabled", true),
            SpontaneousSpikingEnabled: GetBool(element, "spontaneousSpikingEnabled", true));
    }
    private void SetAutoProfileControlsUi(AutoProfileControlSettings settings, bool syncedFromRuntime)
    {
        _suppressAutoProfileControlEvents = true;
        try
        {
            if (AutoProfileEnabledCheckBox is not null)
            {
                AutoProfileEnabledCheckBox.IsChecked = settings.Enabled;
            }

            if (AutoProfileAllowRecoveryCheckBox is not null)
            {
                AutoProfileAllowRecoveryCheckBox.IsChecked = settings.AllowRecovery;
            }

            if (AutoProfileWarmupTicksSlider is not null)
            {
                AutoProfileWarmupTicksSlider.Value = Math.Clamp(settings.WarmupTicks, (int)AutoProfileWarmupTicksSlider.Minimum, (int)AutoProfileWarmupTicksSlider.Maximum);
            }

            if (AutoProfileManualHoldTicksSlider is not null)
            {
                AutoProfileManualHoldTicksSlider.Value = Math.Clamp(settings.ManualHoldTicks, (int)AutoProfileManualHoldTicksSlider.Minimum, (int)AutoProfileManualHoldTicksSlider.Maximum);
            }

            if (AutoProfileDegradeNonOkRatioSlider is not null)
            {
                AutoProfileDegradeNonOkRatioSlider.Value = Math.Clamp(settings.DegradeNonOkRatio, AutoProfileDegradeNonOkRatioSlider.Minimum, AutoProfileDegradeNonOkRatioSlider.Maximum);
            }

            if (AutoProfileDegradeAckLatencyMsSlider is not null)
            {
                AutoProfileDegradeAckLatencyMsSlider.Value = Math.Clamp(settings.DegradeAckLatencyMs, AutoProfileDegradeAckLatencyMsSlider.Minimum, AutoProfileDegradeAckLatencyMsSlider.Maximum);
            }

            if (AutoProfileDegradeSnapshotAgeTicksSlider is not null)
            {
                AutoProfileDegradeSnapshotAgeTicksSlider.Value = Math.Clamp(settings.DegradeSnapshotAgeTicks, (long)AutoProfileDegradeSnapshotAgeTicksSlider.Minimum, (long)AutoProfileDegradeSnapshotAgeTicksSlider.Maximum);
            }

            if (AutoProfileDegradeConsecutiveTicksSlider is not null)
            {
                AutoProfileDegradeConsecutiveTicksSlider.Value = Math.Clamp(settings.DegradeConsecutiveTicks, (int)AutoProfileDegradeConsecutiveTicksSlider.Minimum, (int)AutoProfileDegradeConsecutiveTicksSlider.Maximum);
            }

            if (AutoProfileRecoveryNonOkRatioSlider is not null)
            {
                AutoProfileRecoveryNonOkRatioSlider.Value = Math.Clamp(settings.RecoveryNonOkRatio, AutoProfileRecoveryNonOkRatioSlider.Minimum, AutoProfileRecoveryNonOkRatioSlider.Maximum);
            }

            if (AutoProfileRecoveryAckLatencyMsSlider is not null)
            {
                AutoProfileRecoveryAckLatencyMsSlider.Value = Math.Clamp(settings.RecoveryAckLatencyMs, AutoProfileRecoveryAckLatencyMsSlider.Minimum, AutoProfileRecoveryAckLatencyMsSlider.Maximum);
            }

            if (AutoProfileRecoverySnapshotAgeTicksSlider is not null)
            {
                AutoProfileRecoverySnapshotAgeTicksSlider.Value = Math.Clamp(settings.RecoverySnapshotAgeTicks, (long)AutoProfileRecoverySnapshotAgeTicksSlider.Minimum, (long)AutoProfileRecoverySnapshotAgeTicksSlider.Maximum);
            }

            if (AutoProfileRecoveryConsecutiveTicksSlider is not null)
            {
                AutoProfileRecoveryConsecutiveTicksSlider.Value = Math.Clamp(settings.RecoveryConsecutiveTicks, (int)AutoProfileRecoveryConsecutiveTicksSlider.Minimum, (int)AutoProfileRecoveryConsecutiveTicksSlider.Maximum);
            }
        }
        finally
        {
            _suppressAutoProfileControlEvents = false;
        }

        UpdateAutoProfileControlLabels();
        if (syncedFromRuntime)
        {
            AddOutputLog("Auto profile controls synced from runtime.");
        }
    }

    private static AutoProfileControlSettings? ParseAutoProfileSettings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var enabled = GetBool(element, "enabled", true);
        var allowRecovery = GetBool(element, "allowRecovery", true);
        var warmupTicks = GetInt(element, "warmupTicks");
        var manualHoldTicks = GetInt(element, "manualHoldTicks");
        var degradeNonOkRatio = GetDouble(element, "degradeNonOkRatio");
        var degradeAckLatencyMs = GetDouble(element, "degradeAckLatencyMs");
        var degradeSnapshotAgeTicks = GetLong(element, "degradeSnapshotAgeTicks");
        var degradeConsecutiveTicks = GetInt(element, "degradeConsecutiveTicks");
        var recoveryNonOkRatio = GetDouble(element, "recoveryNonOkRatio");
        var recoveryAckLatencyMs = GetDouble(element, "recoveryAckLatencyMs");
        var recoverySnapshotAgeTicks = GetLong(element, "recoverySnapshotAgeTicks");
        var recoveryConsecutiveTicks = GetInt(element, "recoveryConsecutiveTicks");

        if (warmupTicks < 0 || manualHoldTicks < 0 || degradeAckLatencyMs <= 0 || recoveryAckLatencyMs <= 0)
        {
            return null;
        }

        return new AutoProfileControlSettings(
            Enabled: enabled,
            AllowRecovery: allowRecovery,
            WarmupTicks: warmupTicks,
            ManualHoldTicks: manualHoldTicks,
            DegradeNonOkRatio: degradeNonOkRatio,
            DegradeAckLatencyMs: degradeAckLatencyMs,
            DegradeSnapshotAgeTicks: degradeSnapshotAgeTicks,
            DegradeConsecutiveTicks: degradeConsecutiveTicks,
            RecoveryNonOkRatio: recoveryNonOkRatio,
            RecoveryAckLatencyMs: recoveryAckLatencyMs,
            RecoverySnapshotAgeTicks: recoverySnapshotAgeTicks,
            RecoveryConsecutiveTicks: recoveryConsecutiveTicks);
    }
}
