using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using NAudio.Wave;

namespace NRE.WpfEditor;

// Microphone input pipeline: WaveIn capture, RMS/ZCR feature extraction,
// auditory + language stimulus dispatch, level meter UI. Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private async void ToggleMicrophoneInputButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(ToggleMicrophoneInputAsync, "Toggle microphone input");

    private async Task ToggleMicrophoneInputAsync()
    {
        if (_microphoneInputInFlight)
        {
            AddOutputLog("Microphone input toggle already in progress.");
            return;
        }

        _microphoneInputInFlight = true;
        try
        {
            if (_microphoneRunning)
            {
                await StopMicrophoneInputAsync();
                return;
            }

            var deviceIndex = 0;
            var rawIndex = MicrophoneIndexTextBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(rawIndex) && !int.TryParse(rawIndex, out deviceIndex))
            {
                AddOutputLog($"Microphone input invalid index '{rawIndex}'.");
                return;
            }

            // Defensive: route a stale CTS through StopMicrophoneInputAsync so the
            // worker task is awaited before disposal, avoiding an ObjectDisposedException
            // on the token observed by any in-flight loop.
            if (_microphoneCts is not null)
            {
                if (!await StopMicrophoneInputAsync())
                {
                    return;
                }
            }
            _microphoneCts = CancellationTokenSource.CreateLinkedTokenSource(_workerCts.Token);
            var token = _microphoneCts.Token;

            _microphoneRunning = true;
            _microphoneLanguageRouteAvailable = true;
            _audioRmsEwma = 0;
            _audioZcrEwma = 0;
            _audioLevelEwma = 0;
            _lastMicrophoneDataUtc = DateTime.MinValue;
            _lastMicrophoneRecoveryUtc = DateTime.MinValue;
            if (ToggleMicrophoneInputButton is not null)
            {
                ToggleMicrophoneInputButton.Content = "Stop Microphone Input";
            }

            MicrophoneStatusText.Text = $"Microphone: starting device {deviceIndex}";
            SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Warning, "Microphone pipeline: starting");
            UpdateMicrophoneLevelMeterUi(0, isActive: true);
            AddOutputLog($"Microphone input starting on device {deviceIndex}.");
            _microphoneTask = Task.Run(() => MicrophoneInputLoopAsync(deviceIndex, token), token);
        }
        finally
        {
            _microphoneInputInFlight = false;
        }
    }

    private async Task<bool> StopMicrophoneInputAsync()
    {
        var cts = _microphoneCts;
        var task = _microphoneTask;
        cts?.Cancel();

        try
        {
            if (task is not null)
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (TimeoutException)
        {
            MicrophoneStatusText.Text = "Microphone: stopping (capture worker is still releasing)";
            AddOutputLog("Microphone stop is waiting for the capture worker to release; resources remain owned until it exits.");
            return false;
        }
        catch (Exception ex)
        {
            // A faulted task is complete, so its source can now be released.
            AddOutputLog($"Microphone stopped after worker error: {ex.Message}");
        }

        _microphoneTask = null;
        if (ReferenceEquals(_microphoneCts, cts))
        {
            _microphoneCts = null;
            cts?.Dispose();
        }
        _microphoneRunning = false;
        _audioRmsEwma = 0;
        _audioZcrEwma = 0;
        _audioLevelEwma = 0;
        _lastMicrophoneDataUtc = DateTime.MinValue;
        _lastMicrophoneRecoveryUtc = DateTime.MinValue;
        if (ToggleMicrophoneInputButton is not null)
        {
            ToggleMicrophoneInputButton.Content = "Start Microphone Input";
        }

        MicrophoneStatusText.Text = "Microphone: idle";
        SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Idle, "Microphone pipeline: inactive");
        UpdateMicrophoneLevelMeterUi(0, isActive: false);
        AddOutputLog("Microphone input stopped.");
        return true;
    }

    private async Task MicrophoneInputLoopAsync(int deviceIndex, CancellationToken token)
    {
        PostUi(() => MicrophoneStatusText.Text = $"Microphone: opening device {deviceIndex}");

        WaveInEvent? waveIn = null;
        try
        {
            waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };
        }
        catch (Exception ex)
        {
            PostUi(() =>
            {
                _microphoneRunning = false;
                if (ToggleMicrophoneInputButton is not null)
                {
                    ToggleMicrophoneInputButton.Content = "Start Microphone Input";
                }

                MicrophoneStatusText.Text = $"Microphone: failed to open device {deviceIndex}";
                UpdateMicrophoneLevelMeterUi(0, isActive: false);
                AddOutputLog($"Microphone input failed: {ex.Message}");
            });
            return;
        }

        using (waveIn)
        {
            // Written from the UI thread via PostUi inside RecordingStopped; read
            // on the capture-loop thread. Closure capture promotes the local to a
            // shared field, but the runtime still needs a memory barrier to make
            // the write visible across threads. Use an int with Volatile.Read/Write.
            var recordingStoppedFlag = 0;
            waveIn.DataAvailable += (_, args) =>
            {
                var sampleCount = args.BytesRecorded / 2;
                if (sampleCount <= 0)
                {
                    return;
                }

                double sumSquares = 0;
                var zeroCrossings = 0;
                var previous = 0.0;
                for (var i = 0; i < args.BytesRecorded; i += 2)
                {
                    var sample = BitConverter.ToInt16(args.Buffer, i) / 32768.0;
                    sumSquares += sample * sample;
                    if (i > 0 && ((sample >= 0 && previous < 0) || (sample < 0 && previous >= 0)))
                    {
                        zeroCrossings++;
                    }

                    previous = sample;
                }

                var rms = Math.Sqrt(sumSquares / sampleCount);
                var zcr = Math.Clamp(zeroCrossings / (double)sampleCount, 0.0, 1.0);
                lock (_audioMetricsGate)
                {
                    _audioRmsEwma = (_audioRmsEwma * 0.82) + (rms * 0.18);
                    _audioZcrEwma = (_audioZcrEwma * 0.82) + (zcr * 0.18);
                    _audioLevelEwma = (_audioLevelEwma * 0.75) + (rms * 0.25);
                    _lastMicrophoneDataUtc = DateTime.UtcNow;
                }
            };

            waveIn.RecordingStopped += (_, args) =>
            {
                PostUi(() =>
                {
                    if (!_microphoneRunning)
                    {
                        return;
                    }

                    Volatile.Write(ref recordingStoppedFlag, token.IsCancellationRequested ? 0 : 1);
                    var detail = args?.Exception?.Message;
                    MicrophoneStatusText.Text = string.IsNullOrWhiteSpace(detail)
                        ? "Microphone: recording stopped"
                        : $"Microphone: recording stopped ({detail})";
                });
            };

            waveIn.StartRecording();
            PostUi(() => MicrophoneStatusText.Text = $"Microphone: running (device {deviceIndex})");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    if (Volatile.Read(ref recordingStoppedFlag) != 0)
                    {
                        Volatile.Write(ref recordingStoppedFlag, 0);
                        try
                        {
                            waveIn.StopRecording();
                        }
                        catch
                        {
                            // Ignore transient stop races.
                        }

                        await Task.Delay(90, token);
                        waveIn.StartRecording();
                        PostUi(() =>
                        {
                            MicrophoneStatusText.Text = $"Microphone: recovered stream (device {deviceIndex})";
                            AddOutputLog("Microphone input recovered after recording stop.");
                        });
                    }

                    if ((now - _lastMicrophoneStimulusUtc) >= MicrophoneStimulusInterval)
                    {
                        _lastMicrophoneStimulusUtc = now;
                        double rms;
                        double zcr;
                        double level;
                        DateTime lastData;
                        lock (_audioMetricsGate)
                        {
                            rms = _audioRmsEwma;
                            zcr = _audioZcrEwma;
                            level = _audioLevelEwma;
                            lastData = _lastMicrophoneDataUtc;
                        }

                        if ((now - _lastMicrophoneMeterUiUtc) >= TimeSpan.FromMilliseconds(60))
                        {
                            _lastMicrophoneMeterUiUtc = now;
                            var levelPercent = ComputeMicrophoneLevelPercent(level);
                            PostUi(() => UpdateMicrophoneLevelMeterUi(levelPercent, isActive: _microphoneRunning));
                        }

                        if (lastData != DateTime.MinValue && (now - lastData) > MicrophoneSignalStallTimeout)
                        {
                            PostUi(() => MicrophoneStatusText.Text = $"Microphone: no signal detected on device {deviceIndex}");
                            if ((now - _lastMicrophoneRecoveryUtc) >= MicrophoneRecoveryCooldown)
                            {
                                _lastMicrophoneRecoveryUtc = now;
                                try
                                {
                                    waveIn.StopRecording();
                                }
                                catch
                                {
                                    // Ignore transient stop races.
                                }

                                await Task.Delay(80, token);
                                waveIn.StartRecording();
                                PostUi(() => AddOutputLog("Microphone input auto-recovered after stalled signal."));
                            }
                        }

                        await PushMicrophoneStimulusAsync(rms, zcr, token);
                    }

                    await Task.Delay(40, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    PostUi(() => AddOutputLog($"Microphone input warning: {ex.Message}"));
                    await Task.Delay(120, token);
                }
            }

            try
            {
                waveIn.StopRecording();
            }
            catch
            {
                // Ignore shutdown race.
            }
        }

        PostUi(() =>
        {
            MicrophoneStatusText.Text = "Microphone: idle";
            SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Idle, "Microphone pipeline: inactive");
            UpdateMicrophoneLevelMeterUi(0, isActive: false);
        });
    }

    private async Task PushMicrophoneStimulusAsync(double rmsSignal, double zcrSignal, CancellationToken token)
    {
        if (_isSimulationSleeping)
        {
            PostUi(() =>
            {
                var levelPercent = ComputeMicrophoneLevelPercent(rmsSignal);
                MicrophoneStatusText.Text = $"Microphone: paused during sleep | lvl={levelPercent:0}%";
                UpdateMicrophoneLevelMeterUi(levelPercent, isActive: _microphoneRunning);
            });
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(1900));
        var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
        if (baseUri is null)
        {
            return;
        }

        var pattern = ResolveMicrophonePattern(rmsSignal, zcrSignal);
        var intensity = (float)Math.Clamp(0.2 + (rmsSignal * 14.0), 0.2, 3.0);
        var burstCount = Math.Clamp((int)Math.Round(6.0 + (rmsSignal * 420.0) + (zcrSignal * 42.0)), 4, 64);
        var auditoryTask = SendAuditoryStimulusAsync(baseUri, pattern, intensity, burstCount, cts.Token);

        LanguageStimulusDispatchResult? languageResult = null;
        Exception? languageError = null;
        string? languageUtterance = null;
        if (_microphoneLanguageRouteAvailable)
        {
            var languageMode = ResolveMicrophoneLanguageMode(rmsSignal, zcrSignal);
            var languageText = BuildMicrophoneUtterance(rmsSignal, zcrSignal);
            languageUtterance = languageText;
            var languageIntensity = (float)Math.Clamp(0.55 + (rmsSignal * 9.0) + (zcrSignal * 0.7), 0.2, 3.0);
            var burstPerToken = Math.Clamp((int)Math.Round(4.0 + (rmsSignal * 110.0) + (zcrSignal * 10.0)), 3, 22);
            try
            {
                languageResult = await SendLanguageStimulusAsync(
                    baseUri,
                    languageText,
                    languageMode,
                    "L",
                    languageIntensity,
                    burstPerToken,
                    cts.Token);
            }
            catch (Exception ex)
            {
                languageError = ex;
                if (ex.Message.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase))
                {
                    _microphoneLanguageRouteAvailable = false;
                }
            }
        }

        var result = await auditoryTask;
        PostUi(() =>
        {
            if (result.PausedDueToSleep || (languageResult?.PausedDueToSleep ?? false))
            {
                var pausedLevel = ComputeMicrophoneLevelPercent(rmsSignal);
                MicrophoneStatusText.Text = $"Microphone: paused during sleep | lvl={pausedLevel:0}%";
                UpdateMicrophoneLevelMeterUi(pausedLevel, isActive: _microphoneRunning);
                return;
            }

            var languageDelivered = languageResult?.Delivered ?? 0;
            var levelPercent = ComputeMicrophoneLevelPercent(rmsSignal);
            var recoveryLabel = result.RecoveryAttempted
                ? $" rec={result.RecoveryHealthy}/{Math.Max(result.RecoveryRestarted, result.RecoveryRetriedInstances)}"
                : string.Empty;
            MicrophoneStatusText.Text = $"Microphone: {result.Pattern} i={intensity:0.00} b={burstCount} del={result.Delivered}{recoveryLabel} | lang={languageDelivered} | lvl={levelPercent:0}%";
            UpdateMicrophoneLevelMeterUi(levelPercent, isActive: _microphoneRunning);
            if (result.Delivered > 0)
            {
                AddSpikeLog($"Microphone {result.Pattern}: delivered {result.Delivered} spikes");
            }

            if (result.RecoveryAttempted)
            {
                AddOutputLog(
                    $"Microphone recovery: restarted={result.RecoveryRestarted}, healthy={result.RecoveryHealthy}, retried={result.RecoveryRetriedInstances}, delivered={result.Delivered}/{result.Generated}.");
            }

            if (languageResult is not null)
            {
                if (languageResult.Delivered > 0)
                {
                    var utteranceToRemember = !string.IsNullOrWhiteSpace(languageResult.Utterance)
                        ? languageResult.Utterance
                        : languageUtterance;
                    if (!string.IsNullOrWhiteSpace(utteranceToRemember) &&
                        ShouldPromoteMicrophoneUtteranceToSpeech(rmsSignal, zcrSignal))
                    {
                        RememberLanguageUtterance(utteranceToRemember, force: false);
                    }

                    AddSpikeLog(
                        $"Microphone language {languageResult.Mode}: delivered {languageResult.Delivered} spikes ({languageResult.TokenCount} tokens)");
                }
            }
            else if (languageError is not null)
            {
                AddOutputLog($"Microphone language routing warning: {languageError.Message}");
                if (!_microphoneLanguageRouteAvailable)
                {
                    AddOutputLog("Microphone language routing disabled (language endpoint unavailable).");
                }
            }
        });
    }

    private async Task<AuditoryStimulusDispatchResult> SendAuditoryStimulusAsync(
        Uri baseUri,
        string pattern,
        float intensity,
        int burstCount,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            Pattern = pattern,
            Intensity = intensity,
            BurstCount = burstCount,
            TargetStructure = "A1",
            SourceStructure = "Thalamus",
            Hemisphere = (string?)null
        };

        using var response = await _httpClient.PostAsJsonAsync(new Uri(baseUri, "/api/v1/admin/input/auditory"), request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}. {payload}");
        }

        var generated = 0;
        var delivered = 0;
        var targetCount = 0;
        var recoveryAttempted = false;
        var recoveryRestarted = 0;
        var recoveryHealthy = 0;
        var recoveryRetriedInstances = 0;
        var pausedDueToSleep = false;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                generated = GetInt(doc.RootElement, "generatedSpikes");
                delivered = GetInt(doc.RootElement, "deliveredSpikes");
                targetCount = GetInt(doc.RootElement, "targetInstances");
                recoveryAttempted = GetBool(doc.RootElement, "recoveryAttempted");
                recoveryRestarted = GetInt(doc.RootElement, "recoveryRestarted");
                recoveryHealthy = GetInt(doc.RootElement, "recoveryHealthy");
                recoveryRetriedInstances = GetInt(doc.RootElement, "recoveryRetriedInstances");
                pausedDueToSleep = GetBool(doc.RootElement, "pausedDueToSleep");
            }
        }
        catch
        {
            // Best effort details parsing only.
        }

        return new AuditoryStimulusDispatchResult(
            pattern,
            generated,
            delivered,
            targetCount,
            recoveryAttempted,
            recoveryRestarted,
            recoveryHealthy,
            recoveryRetriedInstances,
            pausedDueToSleep);
    }

    private static string ResolveMicrophonePattern(double rmsSignal, double zcrSignal)
    {
        if (rmsSignal >= 0.085)
        {
            return "ToneBurst";
        }

        if (zcrSignal >= 0.16)
        {
            return "BroadbandNoise";
        }

        return "SustainedTone";
    }

    private static double ComputeMicrophoneLevelPercent(double rmsSignal)
    {
        var clamped = Math.Clamp(rmsSignal, 0.0, 1.0);
        var compressed = Math.Log10(1.0 + (clamped * 1400.0)) / Math.Log10(1401.0);
        return Math.Clamp(compressed * 100.0, 0.0, 100.0);
    }

    private void UpdateMicrophoneLevelMeterUi(double levelPercent, bool isActive)
    {
        if (MicrophoneLevelBar is not null)
        {
            MicrophoneLevelBar.Value = Math.Clamp(levelPercent, 0.0, 100.0);
        }

        if (MicrophoneLevelText is not null)
        {
            var suffix = isActive ? string.Empty : " (idle)";
            MicrophoneLevelText.Text = $"{Math.Clamp(levelPercent, 0.0, 100.0):0}%{suffix}";
        }
    }

    private static string ResolveMicrophoneLanguageMode(double rmsSignal, double zcrSignal)
    {
        if (rmsSignal >= 0.08 || zcrSignal >= 0.19)
        {
            return "repetition";
        }

        if (zcrSignal <= 0.06 && rmsSignal >= 0.03)
        {
            return "prosody";
        }

        if (rmsSignal >= 0.045)
        {
            return "comprehension";
        }

        return "production";
    }

    private static bool ShouldPromoteMicrophoneUtteranceToSpeech(double rmsSignal, double zcrSignal)
    {
        return rmsSignal >= MicrophoneUtterancePromoteRmsThreshold ||
               zcrSignal >= MicrophoneUtterancePromoteZcrThreshold;
    }

    private static string BuildMicrophoneUtterance(double rmsSignal, double zcrSignal)
    {
        var tokenCount = Math.Clamp((int)Math.Round(2.0 + (rmsSignal * 30.0) + (zcrSignal * 8.0)), 2, 7);
        var syllables = zcrSignal >= 0.16
            ? new[] { "ka", "ta", "sa", "sha", "cha", "fa", "za" }
            : rmsSignal >= 0.075
                ? new[] { "ba", "da", "ga", "ma", "na", "la", "ra" }
                : new[] { "ah", "oh", "uh", "eh", "mm", "nn", "la" };

        var tokens = new string[tokenCount];
        var stride = Math.Max(1, (int)Math.Round(1 + (rmsSignal * 7.0)));
        var start = Math.Max(0, Math.Min(syllables.Length - 1, (int)Math.Round(zcrSignal * 10.0)));
        for (var i = 0; i < tokenCount; i++)
        {
            tokens[i] = syllables[(start + (i * stride)) % syllables.Length];
        }

        return string.Join(' ', tokens);
    }
}
