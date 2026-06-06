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

// Webcam input pipeline: capture loop, frame preview, motion/luminance/saliency
// extraction, V1 stimulus dispatch, attention reticle. Extracted from MainWindow.xaml.cs.
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
                await StopWebcamInputAsync();
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
            _pendingWebcamMotionSignal = 0;
            _pendingWebcamLuminanceSignal = 0;
            _pendingWebcamLeftSaliencySignal = 0.5;
            _pendingWebcamRightSaliencySignal = 0.5;
            _lastWebcamFrameUtc = DateTime.MinValue;
            _lastWebcamPreviewUiUtc = DateTime.MinValue;
            _lastV1RouteSuccessUtc = DateTime.MinValue;
            _lastV1RouteFailureUtc = DateTime.MinValue;
            _v1RouteConsecutiveFailures = 0;
            WebcamStatusText.Text = $"Webcam: starting camera {cameraIndex} ({frameEdgePx}x{frameEdgePx})";
            SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Warning, "Webcam pipeline: starting");
            SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Warning, "V1 route: waiting first dispatch");
            SetWebcamPreviewUnavailable("Avatar sight starting...");
            AddOutputLog($"Webcam input starting on camera index {cameraIndex} at {frameEdgePx}x{frameEdgePx}.");
            _webcamTask = Task.Run(() => WebcamInputLoopAsync(cameraIndex, frameEdgePx, token), token);
        }
        finally
        {
            _webcamInputInFlight = false;
        }
    }

    private async Task StopWebcamInputAsync()
    {
        _webcamCts?.Cancel();

        try
        {
            if (_webcamTask is not null)
            {
                await _webcamTask.WaitAsync(TimeSpan.FromSeconds(1));
            }
        }
        catch
        {
            // Best effort stop.
        }
        finally
        {
            _webcamTask = null;
            _webcamCts?.Dispose();
            _webcamCts = null;
            _webcamRunning = false;
            _webcamFrameCount = 0;
            _webcamStimulusDroppedCount = 0;
            _webcamStimulusSentCount = 0;
            _webcamStimulusInFlight = false;
            _webcamStimulusPending = false;
            _pendingWebcamMotionSignal = 0;
            _pendingWebcamLuminanceSignal = 0;
            _pendingWebcamLeftSaliencySignal = 0.5;
            _pendingWebcamRightSaliencySignal = 0.5;
            _lastWebcamFrameUtc = DateTime.MinValue;
            _lastWebcamPreviewUiUtc = DateTime.MinValue;
            if (ToggleWebcamInputButton is not null)
            {
                ToggleWebcamInputButton.Content = "Start Webcam Input";
            }

            WebcamStatusText.Text = $"Webcam: idle ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
            SetInputHealthIndicator(WebcamHealthLight, WebcamHealthText, InputHealthState.Idle, "Webcam pipeline: inactive");
            SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Idle, "V1 route: awaiting webcam input");
            SetWebcamPreviewUnavailable("Avatar sight stopped");
            AddOutputLog("Webcam input stopped.");
        }
    }

    private async Task WebcamInputLoopAsync(int cameraIndex, int frameEdgePx, CancellationToken token)
    {
        PostUi(() => WebcamStatusText.Text = $"Webcam: opening camera {cameraIndex}");

        CV.VideoCapture? capture = null;
        using var frame = new CV.Mat();
        using var gray = new CV.Mat();
        using var previousGray = new CV.Mat();
        using var diff = new CV.Mat();
        var motionEwma = 0.0;
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
                            previousGray.Release();
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
                    Cv2.CvtColor(frame, gray, CV.ColorConversionCodes.BGR2GRAY);
                    if (previousGray.Empty())
                    {
                        gray.CopyTo(previousGray);
                        await Task.Delay(33, token);
                        continue;
                    }

                    Cv2.Absdiff(gray, previousGray, diff);
                    var motion = Cv2.Mean(diff).Val0;
                    var luminance = Cv2.Mean(gray).Val0;
                    motionEwma = (motionEwma * 0.82) + (motion * 0.18);
                    var (leftSaliency, rightSaliency) = ComputeHemifieldSaliency(gray, diff);

                    var now = DateTime.UtcNow;
                    _lastWebcamFrameUtc = now;
                    _webcamFrameCount++;
                    QueueWebcamPreviewFrame(frame, now);
                    if ((now - _lastWebcamStimulusUtc) >= WebcamStimulusInterval)
                    {
                        _lastWebcamStimulusUtc = now;
                        QueueWebcamStimulusDispatch(motionEwma, luminance, leftSaliency, rightSaliency, token);
                    }

                    if ((now - lastStatusRefreshUtc) >= TimeSpan.FromMilliseconds(420))
                    {
                        lastStatusRefreshUtc = now;
                        var motionLabel = motionEwma / 255.0;
                        var lumaLabel = luminance / 255.0;
                        PostUi(() =>
                        {
                            WebcamStatusText.Text = $"Webcam: live m={motionLabel:0.00} y={lumaLabel:0.00} f={_webcamFrameCount} q={(_webcamStimulusInFlight ? "busy" : "idle")} sent={_webcamStimulusSentCount} drop={_webcamStimulusDroppedCount} ({frameEdgePx}x{frameEdgePx})";
                        });
                    }

                    gray.CopyTo(previousGray);
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

    private async Task PushWebcamStimulusAsync(
        double motionSignal,
        double luminanceSignal,
        double leftSaliency,
        double rightSaliency,
        CancellationToken token)
    {
        if (_isSimulationSleeping)
        {
            PostUi(() =>
            {
                WebcamStatusText.Text = $"Webcam: paused during sleep ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
            });
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(1500));
        var baseUri = await ResolveVerifiedControlBaseUriAsync(cts.Token);
        if (baseUri is null)
        {
            _lastV1RouteFailureUtc = DateTime.UtcNow;
            _v1RouteConsecutiveFailures = Math.Min(_v1RouteConsecutiveFailures + 1, 1000);
            PostUi(() =>
            {
                WebcamStatusText.Text = $"Webcam: live m={motionSignal / 255.0:0.00} y={luminanceSignal / 255.0:0.00} f={_webcamFrameCount} (awaiting control)";
                SetInputHealthIndicator(VisualRouteHealthLight, VisualRouteHealthText, InputHealthState.Failed, "V1 route: control endpoint unavailable");
            });
            return;
        }

        var visualSignal = AvatarVisualSignalFactory.FromWebcam(motionSignal, luminanceSignal, leftSaliency, rightSaliency);
        var result = await SendVisualStimulusAsync(baseUri, visualSignal, cts.Token);
        if (!result.PausedDueToSleep)
        {
            if (result.Delivered > 0)
            {
                NoteVisualRouteDispatchSuccess();
            }
            else
            {
                await NoteVisualRouteDispatchFailureAsync($"delivered=0 generated={result.Generated} targets={result.TargetCount}", token);
            }
        }

        PostUi(() =>
        {
            if (result.PausedDueToSleep)
            {
                WebcamStatusText.Text = $"Webcam: paused during sleep ({_webcamFrameEdgePx}x{_webcamFrameEdgePx})";
                return;
            }

            var recoveryLabel = result.RecoveryAttempted
                ? $" rec={result.RecoveryHealthy}/{Math.Max(result.RecoveryRestarted, result.RecoveryRetriedInstances)}"
                : string.Empty;
            var focusLabel = string.IsNullOrWhiteSpace(result.FocusField)
                ? "neutral"
                : result.FocusField!;
            _visualAttentionFocusField = focusLabel.Trim().ToLowerInvariant();
            _visualAttentionFocusHemisphere = string.IsNullOrWhiteSpace(result.FocusHemisphere)
                ? "M"
                : result.FocusHemisphere!.Trim().ToUpperInvariant();
            _visualAttentionFocusConfidence = Math.Clamp(result.FocusConfidence, 0f, 1f);
            UpdateWebcamAttentionReticle();
            WebcamStatusText.Text =
                $"Webcam: {result.Pattern} i={visualSignal.Intensity:0.00} b={visualSignal.BurstCount} del={result.Delivered} m={visualSignal.MotionSignal:0.00} " +
                $"attn={focusLabel}/{result.FocusHemisphere}({result.FocusConfidence:0.00}) f={_webcamFrameCount}{recoveryLabel}";
            if (result.Delivered > 0)
            {
                AddSpikeLog($"Webcam {result.Pattern}: delivered {result.Delivered} spikes");
            }

            if (result.RecoveryAttempted)
            {
                AddOutputLog(
                    $"Webcam recovery: restarted={result.RecoveryRestarted}, healthy={result.RecoveryHealthy}, retried={result.RecoveryRetriedInstances}, delivered={result.Delivered}/{result.Generated}.");
            }
        });
    }

    private void QueueWebcamStimulusDispatch(
        double motionSignal,
        double luminanceSignal,
        double leftSaliency,
        double rightSaliency,
        CancellationToken token)
    {
        var shouldStartWorker = false;
        lock (_webcamStimulusGate)
        {
            if (_webcamStimulusInFlight)
            {
                _webcamStimulusPending = true;
                _pendingWebcamMotionSignal = motionSignal;
                _pendingWebcamLuminanceSignal = luminanceSignal;
                _pendingWebcamLeftSaliencySignal = leftSaliency;
                _pendingWebcamRightSaliencySignal = rightSaliency;
                _webcamStimulusDroppedCount++;
            }
            else
            {
                _webcamStimulusInFlight = true;
                _pendingWebcamMotionSignal = motionSignal;
                _pendingWebcamLuminanceSignal = luminanceSignal;
                _pendingWebcamLeftSaliencySignal = leftSaliency;
                _pendingWebcamRightSaliencySignal = rightSaliency;
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
                double motionSignal;
                double luminanceSignal;
                double leftSaliency;
                double rightSaliency;
                lock (_webcamStimulusGate)
                {
                    motionSignal = _pendingWebcamMotionSignal;
                    luminanceSignal = _pendingWebcamLuminanceSignal;
                    leftSaliency = _pendingWebcamLeftSaliencySignal;
                    rightSaliency = _pendingWebcamRightSaliencySignal;
                    _webcamStimulusPending = false;
                }

                try
                {
                    await PushWebcamStimulusAsync(motionSignal, luminanceSignal, leftSaliency, rightSaliency, token);
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

    private void QueueWebcamPreviewFrame(CV.Mat frame, DateTime nowUtc)
    {
        if (WebcamPreviewImage is null || frame.Empty())
        {
            return;
        }

        if ((nowUtc - _lastWebcamPreviewUiUtc) < WebcamPreviewUiInterval)
        {
            return;
        }

        _lastWebcamPreviewUiUtc = nowUtc;

        using var rgb = new CV.Mat();
        Cv2.CvtColor(frame, rgb, CV.ColorConversionCodes.BGR2RGB);
        var width = rgb.Width;
        var height = rgb.Height;
        var stride = (int)rgb.Step();
        var byteCount = stride * height;
        if (byteCount <= 0)
        {
            return;
        }

        var pixels = new byte[byteCount];
        Marshal.Copy(rgb.Data, pixels, 0, byteCount);
        var sightFrame = new AvatarSightFrame(
            Generation: unchecked((int)Math.Min(_webcamFrameCount, int.MaxValue)),
            CaptureTimestampMs: new DateTimeOffset(nowUtc).ToUnixTimeMilliseconds(),
            Width: width,
            Height: height,
            Stride: stride,
            Pixels: pixels,
            PreviewHeadingDeg: 0.0,
            PixelFormat: "Rgb24");
        _avatarService.PostSightInputFrame(sightFrame);
        _ = PresentWebcamAvatarSightOutputWhenReadyAsync(sightFrame.Generation);
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

    private async Task<VisualStimulusDispatchResult> SendVisualStimulusAsync(
        Uri baseUri,
        AvatarVisualSignal visualSignal,
        CancellationToken cancellationToken)
    {
        var request = visualSignal.ToVisualInputRequest();

        var outcome = await _visualInputClient.DispatchWithHemisphereFallbackAsync(
            baseUri,
            request,
            ex => VisualInputDispatchClient.ShouldRetryHemisphereFallback(ex, "V1"),
            cancellationToken);

        if (outcome.FallbackAttempted)
        {
            AddOutputLog("Webcam visual route fallback: retrying explicit L/R hemisphere dispatch.");
            if (outcome.LeftResponse is not null &&
                outcome.RightResponse is null &&
                outcome.RightError is not null)
            {
                AddOutputLog(
                    $"Webcam visual route partial recovery: right hemisphere dispatch failed ({VisualInputDispatchClient.FormatHttpError(outcome.RightError, 120)}).");
            }
            else if (outcome.RightResponse is not null &&
                     outcome.LeftResponse is null &&
                     outcome.LeftError is not null)
            {
                AddOutputLog(
                    $"Webcam visual route partial recovery: left hemisphere dispatch failed ({VisualInputDispatchClient.FormatHttpError(outcome.LeftError, 120)}).");
            }
        }

        var response = outcome.Response;
        return new VisualStimulusDispatchResult(
            visualSignal.Pattern,
            response.GeneratedSpikes,
            response.DeliveredSpikes,
            response.TargetInstances,
            response.RecoveryAttempted,
            response.RecoveryRestarted,
            response.RecoveryHealthy,
            response.RecoveryRetriedInstances,
            response.PausedDueToSleep,
            response.AttentionFocusField,
            response.AttentionFocusHemisphere,
            response.AttentionFocusConfidence);
    }

    private static (double Left, double Right) ComputeHemifieldSaliency(CV.Mat gray, CV.Mat diff)
    {
        if (gray.Empty() || diff.Empty() || gray.Width < 2 || diff.Width < 2)
        {
            return (0.5, 0.5);
        }

        var splitX = gray.Width / 2;
        if (splitX <= 0 || splitX >= gray.Width)
        {
            return (0.5, 0.5);
        }

        using var leftGray = new CV.Mat(gray, new CV.Rect(0, 0, splitX, gray.Height));
        using var rightGray = new CV.Mat(gray, new CV.Rect(splitX, 0, gray.Width - splitX, gray.Height));
        using var leftDiff = new CV.Mat(diff, new CV.Rect(0, 0, splitX, diff.Height));
        using var rightDiff = new CV.Mat(diff, new CV.Rect(splitX, 0, diff.Width - splitX, diff.Height));

        var leftMotion = Math.Clamp(Cv2.Mean(leftDiff).Val0 / 255.0, 0.0, 1.0);
        var rightMotion = Math.Clamp(Cv2.Mean(rightDiff).Val0 / 255.0, 0.0, 1.0);
        var leftLuma = Math.Clamp(Cv2.Mean(leftGray).Val0 / 255.0, 0.0, 1.0);
        var rightLuma = Math.Clamp(Cv2.Mean(rightGray).Val0 / 255.0, 0.0, 1.0);

        var leftRaw = Math.Clamp((0.72 * leftMotion) + (0.28 * leftLuma), 0.0, 1.0);
        var rightRaw = Math.Clamp((0.72 * rightMotion) + (0.28 * rightLuma), 0.0, 1.0);
        var sum = leftRaw + rightRaw;
        if (sum <= 0.0001)
        {
            return (0.5, 0.5);
        }

        return (leftRaw / sum, rightRaw / sum);
    }
}
