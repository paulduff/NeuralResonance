using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;
using CV = OpenCvSharp;
using Cv2 = OpenCvSharp.Cv2;

namespace NRE.WpfEditor;

// Webcam input pipeline: capture, raw retinal dispatch, preview, and neural-attention reticle.
public partial class MainWindow
{
    private void WebcamResolutionCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var previous = _webcamFrameEdgePx;
        _webcamFrameEdgePx = ResolveSelectedWebcamFrameEdgePx();
        UpdateWebcamPreviewViewportSize(_webcamFrameEdgePx);
        if (!IsLoaded || previous == _webcamFrameEdgePx)
        {
            return;
        }

        if (_webcamRunning)
        {
            WebcamStatusText.Text = $"Webcam: running (restart to apply {_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
            AddOutputLog($"Avatar sight preview resized to {_webcamFrameEdgePx}x{_webcamFrameEdgePx}. Stop/start webcam input to apply capture resolution.");
            return;
        }

        WebcamStatusText.Text = $"Webcam: idle ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
        AddOutputLog($"Webcam resolution set to {_webcamFrameEdgePx}x{_webcamFrameEdgePx}.");
    }

    private int ResolveSelectedWebcamFrameEdgePx()
    {
        if (WebcamResolutionCombo?.SelectedItem is ComboBoxItem item)
        {
            if (item.Tag is not null && int.TryParse(item.Tag.ToString(), out var taggedEdge) && taggedEdge is >= 64 and <= 1024)
            {
                return taggedEdge;
            }

            var content = item.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(content))
            {
                var parts = content.Split(new[] { 'x', 'X', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out var contentEdge) && contentEdge is >= 64 and <= 1024)
                {
                    return contentEdge;
                }
            }
        }

        return DefaultWebcamFrameEdgePx;
    }

    private void UpdateWebcamPreviewViewportSize(int edgePx)
    {
        if (WebcamPreviewBorder is null)
        {
            return;
        }

        var clamped = Math.Clamp(edgePx, 64, 1024);
        WebcamPreviewBorder.Width = clamped;
        WebcamPreviewBorder.Height = clamped;
        UpdateWebcamAttentionReticle();
    }

    private async void ToggleWebcamInputButton_OnClick(object sender, RoutedEventArgs e)
        => await SafeHandlerAsync(ToggleWebcamInputAsync, "Toggle webcam input");

    private async Task ToggleWebcamInputAsync()
    {
        if (_webcamInputInFlight)
        {
            AddOutputLog("Webcam input toggle already in progress.");
            return;
        }

        _webcamInputInFlight = true;
        try
        {
            if (_webcamRunning)
            {
                await StopWebcamInputAsync();
                return;
            }

            var cameraIndex = 0;
            var rawIndex = WebcamIndexTextBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(rawIndex) && !int.TryParse(rawIndex, out cameraIndex))
            {
                AddOutputLog($"Webcam input invalid camera index '{rawIndex}'.");
                return;
            }

            // Defensive: if a previous start partially failed without calling Stop,
            // the existing CTS may still be observed by a worker task. Route through
            // StopWebcamInputAsync so disposal happens only after that task awaits.
            if (_webcamCts is not null)
            {
                if (!await StopWebcamInputAsync())
                {
                    return;
                }
            }
            _webcamCts = CancellationTokenSource.CreateLinkedTokenSource(_workerCts.Token);
            var token = _webcamCts.Token;
            _webcamFrameEdgePx = ResolveSelectedWebcamFrameEdgePx();
            var frameEdgePx = _webcamFrameEdgePx;
            UpdateWebcamPreviewViewportSize(frameEdgePx);

            _webcamRunning = true;
            if (ToggleWebcamInputButton is not null)
            {
                ToggleWebcamInputButton.Content = "Stop Webcam Input";
            }

            _webcamFrameCount = 0;
            _webcamStimulusDroppedCount = 0;
            _webcamStimulusSentCount = 0;
            _webcamStimulusInFlight = false;
            _webcamStimulusPending = false;
            _pendingWebcamSightFrame = null;
            _lastWebcamFrameUtc = DateTime.MinValue;
            _lastWebcamPreviewUiUtc = DateTime.MinValue;
            _lastRetinaRouteSuccessUtc = DateTime.MinValue;
            _lastRetinaRouteFailureUtc = DateTime.MinValue;
            _retinaRouteConsecutiveFailures = 0;
            WebcamStatusText.Text = $"Webcam: starting camera {cameraIndex} ({frameEdgePx}x{frameEdgePx})";
            SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Warning, "Webcam pipeline: starting");
            SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Warning, "Retina route: waiting first dispatch");
            SetWebcamPreviewUnavailable("Avatar sight starting...");
            AddOutputLog($"Webcam input starting on camera index {cameraIndex} at {frameEdgePx}x{frameEdgePx}.");
            _webcamTask = Task.Run(() => WebcamInputLoopAsync(cameraIndex, frameEdgePx, token), token);
        }
        finally
        {
            _webcamInputInFlight = false;
        }
    }

    private async Task<bool> StopWebcamInputAsync()
    {
        var cts = _webcamCts;
        var task = _webcamTask;
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
            WebcamStatusText.Text = "Webcam: stopping (capture worker is still releasing)";
            AddOutputLog("Webcam stop is waiting for the capture worker to release; resources remain owned until it exits.");
            return false;
        }
        catch (Exception ex)
        {
            // A faulted task is complete, so its source can now be released.
            AddOutputLog($"Webcam stopped after worker error: {ex.Message}");
        }

        _webcamTask = null;
        if (ReferenceEquals(_webcamCts, cts))
        {
            _webcamCts = null;
            cts?.Dispose();
        }
        _webcamRunning = false;
        _webcamFrameCount = 0;
        _webcamStimulusDroppedCount = 0;
        _webcamStimulusSentCount = 0;
        _webcamStimulusInFlight = false;
        _webcamStimulusPending = false;
        _pendingWebcamSightFrame = null;
        _lastWebcamFrameUtc = DateTime.MinValue;
        _lastWebcamPreviewUiUtc = DateTime.MinValue;
        if (ToggleWebcamInputButton is not null)
        {
            ToggleWebcamInputButton.Content = "Start Webcam Input";
        }

        WebcamStatusText.Text = $"Webcam: idle ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
        SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Idle, "Webcam pipeline: inactive");
        SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Idle, "Retina route: awaiting webcam input");
        SetWebcamPreviewUnavailable("Avatar sight stopped");
        AddOutputLog("Webcam input stopped.");
        return true;
    }

    private async Task WebcamInputLoopAsync(int cameraIndex, int frameEdgePx, CancellationToken token)
    {
        PostUi(() => WebcamStatusText.Text = $"Webcam: opening camera {cameraIndex}");

        CV.VideoCapture? capture = null;
        using var frame = new CV.Mat();
        var readFailureCount = 0;
        var lastStatusRefreshUtc = DateTime.MinValue;

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (capture is null || !capture.IsOpened())
                    {
                        capture?.Dispose();
                        capture = new CV.VideoCapture(cameraIndex, CV.VideoCaptureAPIs.DSHOW);
                        if (!capture.IsOpened())
                        {
                            PostUi(() =>
                            {
                                WebcamStatusText.Text = $"Webcam: camera {cameraIndex} unavailable, retrying...";
                                SetWebcamPreviewUnavailable($"Camera {cameraIndex} unavailable");
                            });
                            await Task.Delay(650, token);
                            continue;
                        }

                        capture.Fps = 15;
                        capture.FrameWidth = frameEdgePx;
                        capture.FrameHeight = frameEdgePx;
                        readFailureCount = 0;
                        PostUi(() => WebcamStatusText.Text = $"Webcam: running (camera {cameraIndex}, {frameEdgePx}x{frameEdgePx})");
                    }

                    if (!capture.Read(frame) || frame.Empty())
                    {
                        readFailureCount++;
                        if (readFailureCount == WebcamReadFailureWarnThreshold)
                        {
                            PostUi(() => WebcamStatusText.Text = $"Webcam: signal degraded on camera {cameraIndex}, holding stream...");
                        }

                        var frameStallAge = _lastWebcamFrameUtc == DateTime.MinValue
                            ? TimeSpan.MaxValue
                            : DateTime.UtcNow - _lastWebcamFrameUtc;
                        if (readFailureCount >= WebcamReadFailureReconnectThreshold &&
                            frameStallAge >= WebcamHardReconnectTimeout)
                        {
                            readFailureCount = 0;
                            capture.Release();
                            capture.Dispose();
                            capture = null;
                            PostUi(() => WebcamStatusText.Text = $"Webcam: reconnecting camera {cameraIndex} after prolonged frame stall...");
                            await Task.Delay(250, token);
                            continue;
                        }

                        if (_lastWebcamFrameUtc != DateTime.MinValue &&
                            (DateTime.UtcNow - _lastWebcamFrameUtc) > WebcamSignalStallTimeout)
                        {
                            PostUi(() => WebcamStatusText.Text = $"Webcam: signal stalled on camera {cameraIndex}, retrying...");
                        }

                        await Task.Delay(40, token);
                        continue;
                    }

                    readFailureCount = 0;
                    var now = DateTime.UtcNow;
                    _lastWebcamFrameUtc = now;
                    _webcamFrameCount++;
                    var previewDue = (now - _lastWebcamPreviewUiUtc) >= WebcamPreviewUiInterval;
                    var retinalDispatchDue = (now - _lastWebcamStimulusUtc) >= WebcamStimulusInterval;
                    if (previewDue || retinalDispatchDue)
                    {
                        var sightFrame = CreateWebcamSightFrame(frame, now);
                        if (sightFrame is not null)
                        {
                            _avatarService.PostSightInputFrame(sightFrame);
                            if (previewDue)
                            {
                                _lastWebcamPreviewUiUtc = now;
                                _ = PresentWebcamAvatarSightOutputWhenReadyAsync(sightFrame.Generation);
                            }

                            if (retinalDispatchDue)
                            {
                                _lastWebcamStimulusUtc = now;
                                QueueWebcamStimulusDispatch(sightFrame, token);
                            }
                        }
                    }

                    if ((now - lastStatusRefreshUtc) >= TimeSpan.FromMilliseconds(420))
                    {
                        lastStatusRefreshUtc = now;
                        PostUi(() =>
                        {
                            WebcamStatusText.Text = $"Webcam: live f={_webcamFrameCount} q={(_webcamStimulusInFlight ? "busy" : "idle")} sent={_webcamStimulusSentCount} drop={_webcamStimulusDroppedCount} ({frameEdgePx}x{frameEdgePx})";
                        });
                    }

                    await Task.Delay(24, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    PostUi(() => AddOutputLog($"Webcam input warning: {ex.Message}"));
                    capture?.Release();
                    capture?.Dispose();
                    capture = null;
                    await Task.Delay(220, token);
                }
            }
        }
        finally
        {
            capture?.Release();
            capture?.Dispose();
        }

        PostUi(() =>
        {
            WebcamStatusText.Text = $"Webcam: idle ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
            SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Idle, "Webcam pipeline: inactive");
            SetWebcamPreviewUnavailable("Avatar sight idle");
        });
    }

    private async Task PushWebcamStimulusAsync(AvatarSightFrame frame, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(1500));
        var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
        if (baseUri is null)
        {
            _lastRetinaRouteFailureUtc = DateTime.UtcNow;
            _retinaRouteConsecutiveFailures = Math.Min(_retinaRouteConsecutiveFailures + 1, 1000);
            PostUi(() =>
            {
                WebcamStatusText.Text = $"Webcam: live f={_webcamFrameCount} (awaiting control)";
                SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Failed, "Retina route: control endpoint unavailable");
            });
            return;
        }

        var result = await AvatarControlApi.PostRetinalFrameAsync(
            _httpClient,
            baseUri,
            frame,
            "editor_webcam",
            cts.Token);
        if (result.Accepted && !result.BlockedByInputGate && result.TargetInstances > 0)
        {
            NoteVisualRouteDispatchSuccess();
        }
        else if (result.BlockedByInputGate)
        {
            NoteVisualRouteDispatchSuccess();
        }
        else
        {
            await NoteVisualRouteDispatchFailureAsync(
                $"accepted={result.Accepted} generated={result.GeneratedSpikes} targets={result.TargetInstances}",
                token);
        }

        PostUi(() =>
        {
            WebcamStatusText.Text =
                result.BlockedByInputGate
                    ? $"Webcam: Retina input gate closed f={_webcamFrameCount}"
                    : $"Webcam: Retina on={result.OnChannelSpikes} off={result.OffChannelSpikes} gen={result.GeneratedSpikes} " +
                      $"y={result.MeanLuminance:0.00} dt={result.MeanTemporalChange:0.00} f={_webcamFrameCount}";
            if (result.GeneratedSpikes > 0)
            {
                AddSpikeLog($"Webcam Retina: generated {result.GeneratedSpikes} receptor spikes");
            }
        });
    }

    private void QueueWebcamStimulusDispatch(AvatarSightFrame frame, CancellationToken token)
    {
        var shouldStartWorker = false;
        lock (_webcamStimulusGate)
        {
            if (_webcamStimulusInFlight)
            {
                _webcamStimulusPending = true;
                _pendingWebcamSightFrame = frame;
                _webcamStimulusDroppedCount++;
            }
            else
            {
                _webcamStimulusInFlight = true;
                _pendingWebcamSightFrame = frame;
                shouldStartWorker = true;
            }
        }

        if (shouldStartWorker)
        {
            _ = Task.Run(() => WebcamStimulusWorkerAsync(token), token);
        }
    }

    private async Task WebcamStimulusWorkerAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                AvatarSightFrame? frame;
                lock (_webcamStimulusGate)
                {
                    frame = _pendingWebcamSightFrame;
                    _pendingWebcamSightFrame = null;
                    _webcamStimulusPending = false;
                }

                try
                {
                    if (frame is not null)
                    {
                        await PushWebcamStimulusAsync(frame, token);
                    }
                    _webcamStimulusSentCount++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await NoteVisualRouteDispatchFailureAsync(ex.Message, token);
                    PostUi(() => AddOutputLog($"Webcam stimulus dispatch warning: {ex.Message}"));
                }

                var hasPending = false;
                lock (_webcamStimulusGate)
                {
                    hasPending = _webcamStimulusPending;
                    if (!hasPending)
                    {
                        _webcamStimulusInFlight = false;
                    }
                }

                if (!hasPending)
                {
                    break;
                }
            }
        }
        finally
        {
            lock (_webcamStimulusGate)
            {
                _webcamStimulusInFlight = false;
            }
        }
    }

    private AvatarSightFrame? CreateWebcamSightFrame(CV.Mat frame, DateTime nowUtc)
    {
        if (frame.Empty())
        {
            return null;
        }

        using var rgb = new CV.Mat();
        Cv2.CvtColor(frame, rgb, CV.ColorConversionCodes.BGR2RGB);
        var width = rgb.Width;
        var height = rgb.Height;
        var stride = (int)rgb.Step();
        var byteCount = stride * height;
        if (byteCount <= 0)
        {
            return null;
        }

        var pixels = new byte[byteCount];
        Marshal.Copy(rgb.Data, pixels, 0, byteCount);
        return new AvatarSightFrame(
            Generation: unchecked((int)Math.Min(_webcamFrameCount, int.MaxValue)),
            CaptureTimestampMs: new DateTimeOffset(nowUtc).ToUnixTimeMilliseconds(),
            Width: width,
            Height: height,
            Stride: stride,
            Pixels: pixels,
            PreviewHeadingDeg: 0.0,
            PixelFormat: "Rgb24");
    }

    private async Task PresentWebcamAvatarSightOutputWhenReadyAsync(int minGeneration)
    {
        if (PresentLatestWebcamAvatarSightOutput(minGeneration))
        {
            return;
        }

        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                await Task.Delay(12, _workerCts.Token).ConfigureAwait(false);
                if (PresentLatestWebcamAvatarSightOutput(minGeneration))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool PresentLatestWebcamAvatarSightOutput(int minGeneration = int.MinValue)
    {
        AvatarSightFrame? latestFrame = null;
        while (_avatarService.TryDequeueSightOutput(out var frame))
        {
            if (frame.Generation >= minGeneration)
            {
                latestFrame = frame;
            }
        }

        if (latestFrame is null && _avatarService.LatestSightOutput is { } latestOutput && latestOutput.Generation >= minGeneration)
        {
            latestFrame = latestOutput;
        }

        if (latestFrame is not null)
        {
            PresentWebcamAvatarSightFrame(latestFrame);
            return true;
        }

        return false;
    }

    private void PresentWebcamAvatarSightFrame(AvatarSightFrame sightFrame)
    {
        var source = BitmapSource.Create(
            sightFrame.Width,
            sightFrame.Height,
            96,
            96,
            PixelFormats.Rgb24,
            null,
            sightFrame.Pixels,
            sightFrame.Stride);
        source.Freeze();

        PostUi(() =>
        {
            if (WebcamPreviewImage is not null)
            {
                WebcamPreviewImage.Source = source;
            }

            if (WebcamPreviewOverlayText is not null)
            {
                WebcamPreviewOverlayText.Visibility = Visibility.Collapsed;
            }

            UpdateWebcamAttentionReticle();
        });
    }

    private void SetWebcamPreviewUnavailable(string message)
    {
        if (WebcamPreviewImage is not null)
        {
            WebcamPreviewImage.Source = null;
        }

        if (WebcamPreviewOverlayText is not null)
        {
            WebcamPreviewOverlayText.Visibility = Visibility.Visible;
            WebcamPreviewOverlayText.Text = string.IsNullOrWhiteSpace(message)
                ? "Avatar sight unavailable"
                : message;
        }

        if (WebcamAttentionReticle is not null)
        {
            WebcamAttentionReticle.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateWebcamAttentionReticle()
    {
        if (WebcamAttentionReticle is null || WebcamPreviewBorder is null)
        {
            return;
        }

        if (!_webcamRunning)
        {
            WebcamAttentionReticle.Visibility = Visibility.Collapsed;
            return;
        }

        var focusField = _visualAttentionFocusField;
        var confidence = Math.Clamp(_visualAttentionFocusConfidence, 0.0, 1.0);
        var show = (focusField is "left" or "right") && confidence >= 0.08;
        if (!show)
        {
            WebcamAttentionReticle.Visibility = Visibility.Collapsed;
            return;
        }

        var width = WebcamPreviewBorder.ActualWidth > 1.0 ? WebcamPreviewBorder.ActualWidth : WebcamPreviewBorder.Width;
        var height = WebcamPreviewBorder.ActualHeight > 1.0 ? WebcamPreviewBorder.ActualHeight : WebcamPreviewBorder.Height;
        if (width <= 1.0 || height <= 1.0)
        {
            WebcamAttentionReticle.Visibility = Visibility.Collapsed;
            return;
        }

        var baseBoxWidth = width * 0.42;
        var baseBoxHeight = height * 0.68;
        var confidenceScale = 0.86 + (0.28 * confidence);
        var boxWidth = Math.Clamp(baseBoxWidth * confidenceScale, width * 0.24, width * 0.56);
        var boxHeight = Math.Clamp(baseBoxHeight * confidenceScale, height * 0.32, height * 0.82);
        var centerX = focusField == "left" ? width * 0.28 : width * 0.72;
        var centerY = height * 0.50;

        WebcamAttentionReticle.Width = boxWidth;
        WebcamAttentionReticle.Height = boxHeight;
        WebcamAttentionReticle.StrokeThickness = 1.8 + (1.8 * confidence);

        // Reuse a single Stroke/Fill brush instance per Shape; mutating Color avoids
        // the per-call SolidColorBrush allocation that ran on every reticle update.
        var alpha = (byte)Math.Clamp(145 + (confidence * 95.0), 125.0, 255.0);
        var fillAlpha = (byte)Math.Clamp(16 + (confidence * 36.0), 10.0, 88.0);
        if (WebcamAttentionReticle.Stroke is not SolidColorBrush strokeBrush)
        {
            strokeBrush = new SolidColorBrush();
            WebcamAttentionReticle.Stroke = strokeBrush;
        }
        if (WebcamAttentionReticle.Fill is not SolidColorBrush fillBrush)
        {
            fillBrush = new SolidColorBrush();
            WebcamAttentionReticle.Fill = fillBrush;
        }
        strokeBrush.Color = Color.FromArgb(alpha, 145, 231, 255);
        fillBrush.Color = Color.FromArgb(fillAlpha, 145, 231, 255);

        Canvas.SetLeft(WebcamAttentionReticle, centerX - (boxWidth / 2.0));
        Canvas.SetTop(WebcamAttentionReticle, centerY - (boxHeight / 2.0));
        WebcamAttentionReticle.Visibility = Visibility.Visible;
    }

}
