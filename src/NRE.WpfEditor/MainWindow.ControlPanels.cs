using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NRE.WpfEditor;

// Control-panel handlers: performance profile buttons, auto-profile sliders,
// sleep-pressure / min-wake-ticks controls, input-gates checkboxes, reasoning
// sliders + planning/curriculum/consolidation/counterfactual apply actions,
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
        if (_reasoningApplyPlanningInFlight ||
            _reasoningApplyCurriculumInFlight ||
            _reasoningApplyConsolidationInFlight ||
            _reasoningCounterfactualInFlight)
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
            if (TryGetProperty(root, "planningWorkspace", out var planning) && planning.ValueKind == JsonValueKind.Object)
            {
                foundAny = true;
                var goal = GetString(planning, "goal");
                if (!string.IsNullOrWhiteSpace(goal) && ReasoningGoalTextBox is not null && !string.Equals(ReasoningGoalTextBox.Text, goal, StringComparison.Ordinal))
                {
                    ReasoningGoalTextBox.Text = goal;
                }

                if (ReasoningGoalActiveCheckBox is not null)
                {
                    ReasoningGoalActiveCheckBox.IsChecked = GetBool(planning, "goalActive", true);
                }

                SetSliderValue(ReasoningHorizonSlider, GetInt(planning, "horizonSteps"));
                SetSliderValue(ReasoningBranchingSlider, GetInt(planning, "maxBranching"));
                SetSliderValue(ReasoningExplorationSlider, GetDouble(planning, "explorationTemperature"));
                SetSliderValue(ReasoningDopamineSlider, GetDouble(planning, "dopamineBias"));
                SetSliderValue(ReasoningInhibitorySlider, GetDouble(planning, "inhibitoryGate"));
            }

            if (TryGetProperty(root, "curriculum", out var curriculum) && curriculum.ValueKind == JsonValueKind.Object)
            {
                foundAny = true;
                if (ReasoningCurriculumEnabledCheckBox is not null)
                {
                    ReasoningCurriculumEnabledCheckBox.IsChecked = GetBool(curriculum, "enabled", true);
                }

                SetSliderValue(ReasoningCurriculumStageSlider, GetInt(curriculum, "stageIndex"));
            }

            if (TryGetProperty(root, "consolidationControl", out var consolidation) && consolidation.ValueKind == JsonValueKind.Object)
            {
                foundAny = true;
                if (ReasoningConsolidationEnabledCheckBox is not null)
                {
                    ReasoningConsolidationEnabledCheckBox.IsChecked = GetBool(consolidation, "enabled", true);
                }

                SetSliderValue(ReasoningReplayEarlySlider, GetDouble(consolidation, "replayWeightEarlyHippocampal"));
                SetSliderValue(ReasoningReplayLateSlider, GetDouble(consolidation, "replayWeightLateCortical"));
                SetSliderValue(ReasoningAntiForgettingSlider, GetDouble(consolidation, "antiForgettingHomeostasis"));
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
                ? $"Reasoning controls: synced from runtime (tick {tick})"
                : "Reasoning controls: synced from runtime";
        }
    }

    private void UpdateReasoningSliderLabels()
    {
        if (ReasoningHorizonText is not null && ReasoningHorizonSlider is not null)
        {
            ReasoningHorizonText.Text = ((int)Math.Round(ReasoningHorizonSlider.Value)).ToString();
        }

        if (ReasoningBranchingText is not null && ReasoningBranchingSlider is not null)
        {
            ReasoningBranchingText.Text = ((int)Math.Round(ReasoningBranchingSlider.Value)).ToString();
        }

        if (ReasoningExplorationText is not null && ReasoningExplorationSlider is not null)
        {
            ReasoningExplorationText.Text = ReasoningExplorationSlider.Value.ToString("0.00");
        }

        if (ReasoningDopamineText is not null && ReasoningDopamineSlider is not null)
        {
            ReasoningDopamineText.Text = ReasoningDopamineSlider.Value.ToString("0.00");
        }

        if (ReasoningInhibitoryText is not null && ReasoningInhibitorySlider is not null)
        {
            ReasoningInhibitoryText.Text = ReasoningInhibitorySlider.Value.ToString("0.00");
        }

        if (ReasoningCurriculumStageText is not null && ReasoningCurriculumStageSlider is not null)
        {
            ReasoningCurriculumStageText.Text = ((int)Math.Round(ReasoningCurriculumStageSlider.Value)).ToString();
        }

        if (ReasoningReplayEarlyText is not null && ReasoningReplayEarlySlider is not null)
        {
            ReasoningReplayEarlyText.Text = ReasoningReplayEarlySlider.Value.ToString("0.00");
        }

        if (ReasoningReplayLateText is not null && ReasoningReplayLateSlider is not null)
        {
            ReasoningReplayLateText.Text = ReasoningReplayLateSlider.Value.ToString("0.00");
        }

        if (ReasoningAntiForgettingText is not null && ReasoningAntiForgettingSlider is not null)
        {
            ReasoningAntiForgettingText.Text = ReasoningAntiForgettingSlider.Value.ToString("0.00");
        }

        if (ReasoningCounterfactualHorizonText is not null && ReasoningCounterfactualHorizonSlider is not null)
        {
            ReasoningCounterfactualHorizonText.Text = ((int)Math.Round(ReasoningCounterfactualHorizonSlider.Value)).ToString();
        }
    }

    private static string FormatCounterfactualResult(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "Counterfactual returned no payload.";
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return payload;
            }

            var found = GetBool(root, "found");
            var horizon = GetInt(root, "horizonSteps");
            if (!found)
            {
                var reason = GetString(root, "reason");
                var suggestions = new List<string>(8);
                if (TryGetProperty(root, "suggestedActions", out var suggested) && suggested.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in suggested.EnumerateArray().Take(8))
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var suggestedAction = item.GetString();
                            if (!string.IsNullOrWhiteSpace(suggestedAction))
                            {
                                suggestions.Add(suggestedAction);
                            }
                        }
                    }
                }

                return string.Join(Environment.NewLine, new[]
                {
                    $"Found: {found}",
                    $"Horizon: {horizon}",
                    $"Reason: {(string.IsNullOrWhiteSpace(reason) ? "-" : reason)}",
                    $"Suggestions: {(suggestions.Count == 0 ? "-" : string.Join(", ", suggestions))}"
                });
            }

            var actionKey = string.Empty;
            var source = string.Empty;
            var target = string.Empty;
            var nt = string.Empty;
            var isFeedback = false;
            if (TryGetProperty(root, "action", out var action) && action.ValueKind == JsonValueKind.Object)
            {
                actionKey = GetString(action, "actionKey");
                source = GetString(action, "source");
                target = GetString(action, "target");
                nt = GetString(action, "neurotransmitter");
                isFeedback = GetBool(action, "isFeedback");
            }

            var oneStepDispatch = 0.0;
            var oneStepPathway = 0.0;
            var oneStepReward = 0.0;
            var oneStepSleep = 0.0;
            if (TryGetProperty(root, "oneStep", out var oneStep) && oneStep.ValueKind == JsonValueKind.Object)
            {
                oneStepDispatch = GetDouble(oneStep, "expectedDispatchDelta");
                oneStepPathway = GetDouble(oneStep, "expectedPathwayDelta");
                oneStepReward = GetDouble(oneStep, "expectedRewardDelta");
                oneStepSleep = GetDouble(oneStep, "expectedSleepPressureDelta");
            }

            var multiDispatch = 0.0;
            var multiPathway = 0.0;
            var multiReward = 0.0;
            var multiSleep = 0.0;
            if (TryGetProperty(root, "multiStep", out var multiStep) && multiStep.ValueKind == JsonValueKind.Object)
            {
                multiDispatch = GetDouble(multiStep, "dispatchDelta");
                multiPathway = GetDouble(multiStep, "pathwayDelta");
                multiReward = GetDouble(multiStep, "rewardDelta");
                multiSleep = GetDouble(multiStep, "sleepPressureDelta");
            }

            var confidence = GetDouble(root, "confidence");
            var predictionError = GetDouble(root, "predictionError");
            var samples = GetLong(root, "samples");

            var alternatives = new List<string>(6);
            if (TryGetProperty(root, "alternatives", out var alternativesArray) && alternativesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var alternative in alternativesArray.EnumerateArray().Take(6))
                {
                    var altAction = GetString(alternative, "actionKey");
                    var altReward = GetDouble(alternative, "expectedRewardDelta");
                    var altDispatch = GetDouble(alternative, "expectedDispatchDelta");
                    var altSleep = GetDouble(alternative, "expectedSleepPressureDelta");
                    alternatives.Add(
                        $"{altAction} (reward {altReward:+0.000;-0.000;0.000}, dispatch {altDispatch:+0.000;-0.000;0.000}, sleep {altSleep:+0.000;-0.000;0.000})");
                }
            }

            return string.Join(Environment.NewLine, new[]
            {
                $"Found: {found}",
                $"Action: {actionKey} ({source}->{target}, {nt}, feedback={isFeedback})",
                $"Horizon: {horizon}",
                $"One-step delta: dispatch {oneStepDispatch:+0.000;-0.000;0.000}, pathways {oneStepPathway:+0.000;-0.000;0.000}, reward {oneStepReward:+0.000;-0.000;0.000}, sleep {oneStepSleep:+0.000;-0.000;0.000}",
                $"Multi-step delta: dispatch {multiDispatch:+0.000;-0.000;0.000}, pathways {multiPathway:+0.000;-0.000;0.000}, reward {multiReward:+0.000;-0.000;0.000}, sleep {multiSleep:+0.000;-0.000;0.000}",
                $"Confidence: {confidence:0.000} | prediction error: {predictionError:0.000} | samples: {samples}",
                $"Alternatives: {(alternatives.Count == 0 ? "-" : string.Join(" | ", alternatives))}"
            });
        }
        catch
        {
            return payload;
        }
    }

    private async Task ApplyReasoningPlanningAsync()
    {
        if (_reasoningApplyPlanningInFlight)
        {
            AddOutputLog("Reasoning planning update already in progress.");
            return;
        }

        _reasoningApplyPlanningInFlight = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = "Reasoning planning: control endpoint unavailable";
                }
                AddOutputLog("Reasoning planning update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                Goal = ReasoningGoalTextBox?.Text?.Trim(),
                GoalActive = ReasoningGoalActiveCheckBox?.IsChecked ?? true,
                HorizonSteps = (int)Math.Round(ReasoningHorizonSlider?.Value ?? 6),
                MaxBranching = (int)Math.Round(ReasoningBranchingSlider?.Value ?? 8),
                ExplorationTemperature = (float)(ReasoningExplorationSlider?.Value ?? 0.40),
                DopamineBias = (float)(ReasoningDopamineSlider?.Value ?? 1.00),
                InhibitoryGate = (float)(ReasoningInhibitorySlider?.Value ?? 1.00)
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/reasoning/planning"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                NoteControlEndpointFailure();
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = $"Reasoning planning: HTTP {(int)response.StatusCode}";
                }
                AddOutputLog($"Reasoning planning update failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 220)}");
                return;
            }

            NoteControlEndpointSuccess(baseUri);
            if (ReasoningStatusText is not null)
            {
                ReasoningStatusText.Text = "Reasoning planning: applied";
            }
            AddOutputLog("Reasoning planning workspace updated.");

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
                ReasoningStatusText.Text = $"Reasoning planning: error ({ex.GetType().Name})";
            }
            AddOutputLog($"Reasoning planning update failed: {ex.Message}");
        }
        finally
        {
            _reasoningApplyPlanningInFlight = false;
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

    private async Task ApplyReasoningConsolidationAsync()
    {
        if (_reasoningApplyConsolidationInFlight)
        {
            AddOutputLog("Reasoning consolidation update already in progress.");
            return;
        }

        _reasoningApplyConsolidationInFlight = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = "Reasoning consolidation: control endpoint unavailable";
                }
                AddOutputLog("Reasoning consolidation update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                Enabled = ReasoningConsolidationEnabledCheckBox?.IsChecked ?? true,
                ReplayWeightEarlyHippocampal = (float)(ReasoningReplayEarlySlider?.Value ?? 1.8),
                ReplayWeightLateCortical = (float)(ReasoningReplayLateSlider?.Value ?? 1.4),
                AntiForgettingHomeostasis = (float)(ReasoningAntiForgettingSlider?.Value ?? 0.60)
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/reasoning/consolidation"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                NoteControlEndpointFailure();
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = $"Reasoning consolidation: HTTP {(int)response.StatusCode}";
                }
                AddOutputLog($"Reasoning consolidation update failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 220)}");
                return;
            }

            NoteControlEndpointSuccess(baseUri);
            if (ReasoningStatusText is not null)
            {
                ReasoningStatusText.Text = "Reasoning consolidation: applied";
            }
            AddOutputLog("Reasoning consolidation settings updated.");

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
                ReasoningStatusText.Text = $"Reasoning consolidation: error ({ex.GetType().Name})";
            }
            AddOutputLog($"Reasoning consolidation update failed: {ex.Message}");
        }
        finally
        {
            _reasoningApplyConsolidationInFlight = false;
        }
    }

    private async Task EvaluateReasoningCounterfactualAsync()
    {
        if (_reasoningCounterfactualInFlight)
        {
            AddOutputLog("Reasoning counterfactual request already in progress.");
            return;
        }

        _reasoningCounterfactualInFlight = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(6500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = "Reasoning counterfactual: control endpoint unavailable";
                }
                if (ReasoningCounterfactualResultTextBox is not null)
                {
                    ReasoningCounterfactualResultTextBox.Text = "Counterfactual skipped: Control Program endpoint not available.";
                    ReasoningCounterfactualResultTextBox.CaretIndex = 0;
                }
                AddOutputLog("Reasoning counterfactual skipped: Control Program endpoint not available.");
                return;
            }

            var actionKey = ReasoningCounterfactualActionTextBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                actionKey = "idle";
            }

            var request = new
            {
                ActionKey = actionKey,
                HorizonSteps = (int)Math.Round(ReasoningCounterfactualHorizonSlider?.Value ?? 4)
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/reasoning/counterfactual"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                NoteControlEndpointFailure();
                if (ReasoningStatusText is not null)
                {
                    ReasoningStatusText.Text = $"Reasoning counterfactual: HTTP {(int)response.StatusCode}";
                }
                if (ReasoningCounterfactualResultTextBox is not null)
                {
                    ReasoningCounterfactualResultTextBox.Text =
                        $"Counterfactual failed: HTTP {(int)response.StatusCode}.{Environment.NewLine}{TrimForStatus(payload, 400)}";
                    ReasoningCounterfactualResultTextBox.CaretIndex = 0;
                }
                AddOutputLog($"Reasoning counterfactual failed: HTTP {(int)response.StatusCode}. {TrimForStatus(payload, 220)}");
                return;
            }

            NoteControlEndpointSuccess(baseUri);
            var formatted = FormatCounterfactualResult(payload);
            if (ReasoningCounterfactualResultTextBox is not null)
            {
                ReasoningCounterfactualResultTextBox.Text = formatted;
                ReasoningCounterfactualResultTextBox.CaretIndex = 0;
            }

            if (ReasoningStatusText is not null)
            {
                ReasoningStatusText.Text = $"Reasoning counterfactual evaluated: {actionKey}";
            }
            AddOutputLog($"Reasoning counterfactual evaluated for '{actionKey}'.");
        }
        catch (Exception ex)
        {
            NoteControlEndpointFailure();
            if (ReasoningStatusText is not null)
            {
                ReasoningStatusText.Text = $"Reasoning counterfactual: error ({ex.GetType().Name})";
            }

            if (ReasoningCounterfactualResultTextBox is not null)
            {
                ReasoningCounterfactualResultTextBox.Text = $"Counterfactual failed: {ex.Message}";
                ReasoningCounterfactualResultTextBox.CaretIndex = 0;
            }
            AddOutputLog($"Reasoning counterfactual failed: {ex.Message}");
        }
        finally
        {
            _reasoningCounterfactualInFlight = false;
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
    private async void ReasoningApplyPlanningButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(ApplyReasoningPlanningAsync, "Apply reasoning planning");

    private async void ReasoningApplyCurriculumButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(ApplyReasoningCurriculumAsync, "Apply reasoning curriculum");

    private async void ReasoningApplyConsolidationButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(ApplyReasoningConsolidationAsync, "Apply reasoning consolidation");

    private async void ReasoningEvaluateCounterfactualButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(EvaluateReasoningCounterfactualAsync, "Evaluate reasoning counterfactual");

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
            await RefreshSleepMemoryControlsFromControlAsync(baseUri);
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

    private async Task RefreshSleepMemoryControlsFromControlAsync(Uri? preferredBaseUri = null, CancellationToken cancellationToken = default)
    {
        using var localTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        localTimeout.CancelAfter(TimeSpan.FromMilliseconds(2500));
        var token = localTimeout.Token;

        var baseUri = preferredBaseUri ?? await ResolveVerifiedControlBaseUriAsync(token);
        if (baseUri is null)
        {
            return;
        }

        try
        {
            using var response = await _httpClient.GetAsync(new Uri(baseUri, "/api/v1/admin/sleep-memory"), token);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var runtimeMinWakeTicks = GetInt(doc.RootElement, "minWakeTicks");
            var runtimeSleepPressureEnter = (float)GetDouble(doc.RootElement, "sleepPressureEnterThreshold");
            if (runtimeMinWakeTicks <= 0 && runtimeSleepPressureEnter <= 0f)
            {
                return;
            }

            if (runtimeMinWakeTicks > 0)
            {
                SetMinWakeTicksUi(runtimeMinWakeTicks, syncedFromRuntime: true);
            }

            if (runtimeSleepPressureEnter > 0f)
            {
                SetSleepPressureEnterUi(runtimeSleepPressureEnter, syncedFromRuntime: true);
            }
        }
        catch
        {
            // Keep profile switch resilient; stats poll will still reflect current runtime state.
        }
    }

    private void SyncMinWakeTicksFromState(JsonElement stateElement)
    {
        if (_minWakeDebounceTimer.IsEnabled ||
            _sleepPressureDebounceTimer.IsEnabled ||
            _minWakeUpdateInFlight ||
            _sleepPressureUpdateInFlight)
        {
            return;
        }

        if (!TryGetProperty(stateElement, "sleepMemory", out var sleepMemory) || sleepMemory.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var runtimeMinWakeTicks = GetInt(sleepMemory, "minWakeTicks");
        if (runtimeMinWakeTicks > 0 && runtimeMinWakeTicks != _minWakeTicks)
        {
            SetMinWakeTicksUi(runtimeMinWakeTicks, syncedFromRuntime: true);
        }

        var runtimeSleepPressureEnter = (float)GetDouble(sleepMemory, "sleepPressureEnterThreshold");
        if (runtimeSleepPressureEnter > 0f &&
            Math.Abs(runtimeSleepPressureEnter - _sleepPressureEnterThreshold) > 0.0005f)
        {
            SetSleepPressureEnterUi(runtimeSleepPressureEnter, syncedFromRuntime: true);
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

    private void SetMinWakeTicksUi(int value, bool syncedFromRuntime)
    {
        var min = MinWakeTicksSlider is null ? 80 : (int)Math.Round(MinWakeTicksSlider.Minimum);
        var max = MinWakeTicksSlider is null ? 1200 : (int)Math.Round(MinWakeTicksSlider.Maximum);
        var clamped = Math.Clamp(value, min, max);
        var changed = clamped != _minWakeTicks;
        _minWakeTicks = clamped;

        if (MinWakeTicksText is not null)
        {
            MinWakeTicksText.Text = clamped.ToString();
        }

        if (MinWakeTicksSlider is not null && (int)Math.Round(MinWakeTicksSlider.Value) != clamped)
        {
            _suppressMinWakeSliderEvents = true;
            try
            {
                MinWakeTicksSlider.Value = clamped;
            }
            finally
            {
                _suppressMinWakeSliderEvents = false;
            }
        }

        if (syncedFromRuntime && changed)
        {
            AddOutputLog($"Min awake ticks synced from runtime: {clamped}.");
        }
    }

    private async Task ApplyMinWakeTicksAsync(int minWakeTicks)
    {
        if (_minWakeUpdateInFlight)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var remaining = MinWakeUpdateCooldown - (now - _lastMinWakeUpdateUtc);
        if (remaining > TimeSpan.Zero)
        {
            return;
        }

        _minWakeUpdateInFlight = true;
        _lastMinWakeUpdateUtc = now;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Min awake ticks update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                MinWakeTicks = minWakeTicks
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/sleep-memory"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Min awake ticks update failed ({minWakeTicks}): HTTP {(int)response.StatusCode}. {payload}");
                return;
            }

            AddOutputLog($"Min awake ticks set to {minWakeTicks}.");
        }
        catch (Exception ex)
        {
            AddOutputLog($"Min awake ticks update failed ({minWakeTicks}): {ex.Message}");
        }
        finally
        {
            _minWakeUpdateInFlight = false;
        }
    }

    private void SetSleepPressureEnterUi(float value, bool syncedFromRuntime)
    {
        var min = SleepPressureEnterSlider is null ? 0.10f : (float)SleepPressureEnterSlider.Minimum;
        var max = SleepPressureEnterSlider is null ? 1.00f : (float)SleepPressureEnterSlider.Maximum;
        var clamped = Math.Clamp(value, min, max);
        var changed = Math.Abs(clamped - _sleepPressureEnterThreshold) > 0.0005f;
        _sleepPressureEnterThreshold = clamped;

        if (SleepPressureEnterText is not null)
        {
            SleepPressureEnterText.Text = clamped.ToString("0.00");
        }

        if (SleepPressureEnterSlider is not null &&
            Math.Abs(SleepPressureEnterSlider.Value - clamped) > 0.0005)
        {
            _suppressSleepPressureSliderEvents = true;
            try
            {
                SleepPressureEnterSlider.Value = clamped;
            }
            finally
            {
                _suppressSleepPressureSliderEvents = false;
            }
        }

        if (syncedFromRuntime && changed)
        {
            AddOutputLog($"Sleep pressure enter threshold synced from runtime: {clamped:0.00}.");
        }
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

    private async Task ApplySleepPressureEnterThresholdAsync(float threshold)
    {
        if (_sleepPressureUpdateInFlight)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var remaining = SleepPressureUpdateCooldown - (now - _lastSleepPressureUpdateUtc);
        if (remaining > TimeSpan.Zero)
        {
            return;
        }

        _sleepPressureUpdateInFlight = true;
        _lastSleepPressureUpdateUtc = now;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500));
        try
        {
            var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
            if (baseUri is null)
            {
                AddOutputLog("Sleep pressure enter update skipped: Control Program endpoint not available.");
                return;
            }

            var request = new
            {
                SleepPressureEnterThreshold = threshold
            };

            using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/sleep-memory"), request, cts.Token);
            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                AddOutputLog($"Sleep pressure enter update failed ({threshold:0.00}): HTTP {(int)response.StatusCode}. {payload}");
                return;
            }

            AddOutputLog($"Sleep pressure enter threshold set to {threshold:0.00}.");
        }
        catch (Exception ex)
        {
            AddOutputLog($"Sleep pressure enter update failed ({threshold:0.00}): {ex.Message}");
        }
        finally
        {
            _sleepPressureUpdateInFlight = false;
        }
    }

}
