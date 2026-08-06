using System.Buffers.Binary;
using System.Windows;
using NAudio.Wave;
using NRE.SimAvatar;

namespace NRE.WpfEditor;

// Microphone input remains a peripheral sensor: captured pressure samples are
// forwarded unchanged. Frequency decomposition and meaning belong to neurons.
public partial class MainWindow
{
    private const int MicrophoneSampleRate = 16000;
    private const int MicrophoneChannels = 1;

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

            if (_microphoneCts is not null && !await StopMicrophoneInputAsync())
            {
                return;
            }

            _microphoneCts = CancellationTokenSource.CreateLinkedTokenSource(_workerCts.Token);
            var token = _microphoneCts.Token;
            lock (_audioMetricsGate)
            {
                _pendingMicrophonePcm = null;
                _audioRmsEwma = 0;
                _audioLevelEwma = 0;
                _lastMicrophoneDataUtc = DateTime.MinValue;
            }

            _microphoneRunning = true;
            _lastMicrophoneRecoveryUtc = DateTime.MinValue;
            if (ToggleMicrophoneInputButton is not null)
            {
                ToggleMicrophoneInputButton.Content = "Stop Microphone Input";
            }

            MicrophoneStatusText.Text = $"Microphone: starting device {deviceIndex}";
            SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Warning, "Microphone pipeline: starting");
            UpdateMicrophoneLevelMeterUi(0, isActive: true);
            AddOutputLog($"Microphone PCM input starting on device {deviceIndex}.");
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
            AddOutputLog($"Microphone stopped after worker error: {ex.Message}");
        }

        _microphoneTask = null;
        if (ReferenceEquals(_microphoneCts, cts))
        {
            _microphoneCts = null;
            cts?.Dispose();
        }

        lock (_audioMetricsGate)
        {
            _pendingMicrophonePcm = null;
            _audioRmsEwma = 0;
            _audioLevelEwma = 0;
            _lastMicrophoneDataUtc = DateTime.MinValue;
        }

        _microphoneRunning = false;
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
                WaveFormat = new WaveFormat(MicrophoneSampleRate, 16, MicrophoneChannels),
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
            var recordingStoppedFlag = 0;
            waveIn.DataAvailable += (_, args) => CaptureMicrophoneFrame(args.Buffer, args.BytesRecorded);
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
            PostUi(() => MicrophoneStatusText.Text = $"Microphone: raw PCM active (device {deviceIndex})");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    if (Volatile.Read(ref recordingStoppedFlag) != 0)
                    {
                        Volatile.Write(ref recordingStoppedFlag, 0);
                        TryStopRecording(waveIn);
                        await Task.Delay(90, token);
                        waveIn.StartRecording();
                        PostUi(() => AddOutputLog("Microphone input recovered after recording stop."));
                    }

                    byte[]? pcm = null;
                    double level;
                    DateTime lastData;
                    lock (_audioMetricsGate)
                    {
                        level = _audioLevelEwma;
                        lastData = _lastMicrophoneDataUtc;
                        if ((now - _lastMicrophoneStimulusUtc) >= MicrophoneStimulusInterval)
                        {
                            _lastMicrophoneStimulusUtc = now;
                            pcm = _pendingMicrophonePcm;
                            _pendingMicrophonePcm = null;
                        }
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
                            TryStopRecording(waveIn);
                            await Task.Delay(80, token);
                            waveIn.StartRecording();
                            PostUi(() => AddOutputLog("Microphone input auto-recovered after stalled signal."));
                        }
                    }

                    if (pcm is not null)
                    {
                        await PushMicrophoneFrameAsync(pcm, token);
                    }

                    await Task.Delay(25, token);
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

            TryStopRecording(waveIn);
        }

        PostUi(() =>
        {
            MicrophoneStatusText.Text = "Microphone: idle";
            SetInputHealthIndicator(MicrophoneHealthLight, MicrophoneHealthText, InputHealthState.Idle, "Microphone pipeline: inactive");
            UpdateMicrophoneLevelMeterUi(0, isActive: false);
        });
    }

    private void CaptureMicrophoneFrame(byte[] buffer, int bytesRecorded)
    {
        var usableBytes = bytesRecorded - (bytesRecorded % sizeof(short));
        if (usableBytes <= 0)
        {
            return;
        }

        var pcm = new byte[usableBytes];
        Buffer.BlockCopy(buffer, 0, pcm, 0, usableBytes);
        var sampleCount = usableBytes / sizeof(short);
        double sumSquares = 0;
        for (var offset = 0; offset < usableBytes; offset += sizeof(short))
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(offset, sizeof(short))) / 32768.0;
            sumSquares += sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / sampleCount);
        lock (_audioMetricsGate)
        {
            _pendingMicrophonePcm = pcm;
            _audioRmsEwma = (_audioRmsEwma * 0.82) + (rms * 0.18);
            _audioLevelEwma = (_audioLevelEwma * 0.75) + (rms * 0.25);
            _lastMicrophoneDataUtc = DateTime.UtcNow;
        }
    }

    private async Task PushMicrophoneFrameAsync(byte[] pcm, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(1900));
        var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
        if (baseUri is null)
        {
            return;
        }

        var frame = new AvatarAudioFrame(
            Interlocked.Increment(ref _microphoneFrameSequence),
            Environment.TickCount64,
            MicrophoneSampleRate,
            MicrophoneChannels,
            pcm.Length / (MicrophoneChannels * sizeof(short)),
            pcm);
        var result = await AvatarControlApi.PostCochlearFrameAsync(
            _httpClient,
            baseUri,
            frame,
            inputSource: "editor_microphone",
            cts.Token);

        PostUi(() =>
        {
            var levelPercent = ComputeMicrophoneLevelPercent(result.RootMeanSquare);
            MicrophoneStatusText.Text =
                $"Microphone: cochlea L={result.ActiveLeftBands}/{result.FrequencyBands} " +
                $"R={result.ActiveRightBands}/{result.FrequencyBands} rms={result.RootMeanSquare:0.000} " +
                $"peak={result.PeakAmplitude:0.000} spikes={result.GeneratedSpikes}";
            UpdateMicrophoneLevelMeterUi(levelPercent, isActive: _microphoneRunning);
            if (result.GeneratedSpikes > 0)
            {
                AddSpikeLog($"Microphone cochlear frame: generated {result.GeneratedSpikes} receptor spikes");
            }
        });
    }

    private static void TryStopRecording(WaveInEvent waveIn)
    {
        try
        {
            waveIn.StopRecording();
        }
        catch
        {
            // Capture shutdown can race the driver callback.
        }
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
}
