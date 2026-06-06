using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace NRE.WpfEditor;

// Camera, viewport interaction, hover label, and auto-fit zoom.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private void ApplyPresetView(double yawDeg, double pitchDeg)
    {
        _isApplyingPresetView = true;
        try
        {
            _targetYaw = yawDeg;
            _targetPitch = Math.Clamp(pitchDeg, -90, 90);
            _yawRotation.Angle = _targetYaw;
            _pitchRotation.Angle = _targetPitch;
            _targetZoom = _sceneZoom;
            if (YawSlider is not null)
            {
                YawSlider.Value = Math.Clamp(yawDeg, YawSlider.Minimum, YawSlider.Maximum);
            }
            ScheduleCameraAutoFit(immediate: true);
        }
        finally
        {
            _isApplyingPresetView = false;
        }

        AddOutputLog($"View preset applied: yaw {yawDeg:0} deg, pitch {pitchDeg:0} deg");
    }

    private void AnteriorViewButton_OnClick(object sender, RoutedEventArgs e) => ApplyPresetView(0, 0);

    private void PosteriorViewButton_OnClick(object sender, RoutedEventArgs e) => ApplyPresetView(180, 0);

    private void SuperiorViewButton_OnClick(object sender, RoutedEventArgs e) => ApplyPresetView(0, 90);

    private void InferiorViewButton_OnClick(object sender, RoutedEventArgs e) => ApplyPresetView(0, -90);

    private void LeftViewButton_OnClick(object sender, RoutedEventArgs e) => ApplyPresetView(-90, 0);

    private void RightViewButton_OnClick(object sender, RoutedEventArgs e) => ApplyPresetView(90, 0);

    private void ResetViewButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPresetView(0, 0);
    }

    private void LockPresetTransformsCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        ApplyPresetTransformLock(true);
        EndDrag();
        AddOutputLog("Preset transform lock enabled.");
    }

    private void LockPresetTransformsCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ApplyPresetTransformLock(false);
        AddOutputLog("Preset transform lock disabled.");
    }

    private void ApplyPresetTransformLock(bool locked)
    {
        _presetTransformsLocked = locked;
        if (YawSlider is not null)
        {
            YawSlider.IsEnabled = !locked;
        }
    }

    private void BrainViewport_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_presetTransformsLocked)
        {
            BrainViewport.Focus();
            e.Handled = true;
            return;
        }

        _isDragging = true;
        _lastMousePosition = e.GetPosition(BrainViewport);
        BrainViewport.CaptureMouse();
        BrainViewport.Focus();
        e.Handled = true;
    }

    private void BrainViewport_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        e.Handled = true;
    }

    private void BrainViewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(BrainViewport);
        UpdateHoverLabel(current);

        if (_presetTransformsLocked)
        {
            if (_isDragging)
            {
                EndDrag();
            }

            return;
        }

        if (!_isDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        var delta = current - _lastMousePosition;
        _lastMousePosition = current;
        _inputQueue.Enqueue(new InputDelta(delta.X, delta.Y, 0));
        _inputSignal.Release();
        e.Handled = true;
    }

    private void BrainViewport_OnMouseLeave(object sender, MouseEventArgs e)
    {
        HideHoverLabel();
        if (_isDragging && e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
        }
    }

    private void BrainViewport_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        HideHoverLabel();
        EndDrag();
    }

    private void BrainViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom intentionally ignores _presetTransformsLocked - the lock is
        // meant to freeze the preset orientation (yaw/pitch) only. Zoom is a
        // separate axis and should always respond to the wheel so the user can
        // see detail even while a preset view is active.
        _inputQueue.Enqueue(new InputDelta(0, 0, e.Delta));
        _inputSignal.Release();
        e.Handled = true;
    }

    private void UpdateHoverLabel(Point cursor)
    {
        if (_structureAnchorPoints.Count == 0 || BrainViewport.ActualWidth < 2 || BrainViewport.ActualHeight < 2)
        {
            HideHoverLabel();
            return;
        }

        string? bestId = null;
        var bestDist2 = double.MaxValue;
        var bestScreen = new Point();
        var bestDepth = double.MaxValue;

        foreach (var kv in _structureAnchorPoints)
        {
            if (!TryProjectToViewport(kv.Value, out var screen, out var depth))
            {
                continue;
            }

            var dx = screen.X - cursor.X;
            var dy = screen.Y - cursor.Y;
            var d2 = (dx * dx) + (dy * dy);
            if (d2 < bestDist2 || (Math.Abs(d2 - bestDist2) < 0.01 && depth < bestDepth))
            {
                bestDist2 = d2;
                bestId = kv.Key;
                bestScreen = screen;
                bestDepth = depth;
            }
        }

        const double hoverRadiusPx = 44.0;
        if (bestId is null || bestDist2 > (hoverRadiusPx * hoverRadiusPx) || !_structureVisuals.TryGetValue(bestId, out var visual))
        {
            HideHoverLabel();
            return;
        }

        HoverLabelText.Text = $"{visual.DisplayName} ({visual.Hemisphere})";
        HoverLabelBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(HoverLabelBorder, Math.Clamp(bestScreen.X + 14, 4, Math.Max(4, BrainViewport.ActualWidth - 220)));
        Canvas.SetTop(HoverLabelBorder, Math.Clamp(bestScreen.Y - 26, 4, Math.Max(4, BrainViewport.ActualHeight - 40)));
    }

    private void HideHoverLabel()
    {
        if (HoverLabelBorder.Visibility != Visibility.Collapsed)
        {
            HoverLabelBorder.Visibility = Visibility.Collapsed;
        }
    }

    private bool TryProjectToViewport(Point3D worldPoint, out Point screen, out double depth)
    {
        screen = new Point();
        depth = double.MaxValue;
        if (BrainViewport.Camera is not PerspectiveCamera camera)
        {
            return false;
        }

        var transformed = ApplySceneTransform(worldPoint);
        var relX = transformed.X - camera.Position.X;
        var relY = transformed.Y - camera.Position.Y;
        var relZ = camera.Position.Z - transformed.Z;
        if (relZ <= 0.02)
        {
            return false;
        }

        var halfHeight = BrainViewport.ActualHeight * 0.5;
        if (halfHeight <= 0.0)
        {
            return false;
        }

        var fovRadians = DegreesToRadians(camera.FieldOfView);
        var focal = halfHeight / Math.Tan(fovRadians * 0.5);
        var sx = (relX * focal / relZ) + (BrainViewport.ActualWidth * 0.5);
        var sy = (-relY * focal / relZ) + (BrainViewport.ActualHeight * 0.5);

        if (double.IsNaN(sx) || double.IsNaN(sy) || double.IsInfinity(sx) || double.IsInfinity(sy))
        {
            return false;
        }

        screen = new Point(sx, sy);
        depth = relZ;
        return true;
    }

    private Point3D ApplySceneTransform(Point3D point)
        => ApplySceneTransform(point, _sceneZoom);

    private Point3D ApplySceneTransform(Point3D point, double sceneZoom)
    {
        var clampedZoom = Math.Clamp(sceneZoom, MinSceneZoom, MaxSceneZoom);
        var sceneScale = BaseSceneScale * clampedZoom;
        var p = new Point3D(
            point.X * _globalPulseScale.ScaleX,
            point.Y * _globalPulseScale.ScaleY,
            point.Z * _globalPulseScale.ScaleZ);
        p = new Point3D(
            p.X * sceneScale,
            p.Y * sceneScale,
            p.Z * sceneScale);
        return RotateLocalPoint(p, _pitchRotation.Angle, _yawRotation.Angle, 0);
    }

    private void ScheduleCameraAutoFit(bool immediate = false)
    {
        if (_cameraFitSamplePoints.Count == 0)
        {
            return;
        }

        if (immediate)
        {
            ApplyAutoFitCamera();
            return;
        }

        _cameraFitDebounceTimer.Stop();
        _cameraFitDebounceTimer.Start();
    }

    private void ApplyAutoFitCamera()
    {
        if (_cameraFitSamplePoints.Count == 0 || BrainViewport.ActualWidth < 2 || BrainViewport.ActualHeight < 2)
        {
            return;
        }

        var fitZoom = ComputeFitZoomForCurrentView();
        _targetZoom = fitZoom;
        _sceneZoom = fitZoom;

        // Apply immediately so the viewport always frames the full brain without waiting on smoothing.
        var scaleValue = BaseSceneScale * _sceneZoom;
        _sceneScale.ScaleX = scaleValue;
        _sceneScale.ScaleY = scaleValue;
        _sceneScale.ScaleZ = scaleValue;
    }

    private double ComputeFitZoomForCurrentView()
    {
        const double edgePaddingPx = 56.0;
        var low = MinSceneZoom;
        var high = MaxSceneZoom;
        var best = low;
        if (!DoesZoomFitViewport(low, edgePaddingPx))
        {
            return low;
        }

        for (var i = 0; i < 20; i++)
        {
            var mid = (low + high) * 0.5;
            if (DoesZoomFitViewport(mid, edgePaddingPx))
            {
                best = mid;
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return Math.Clamp(best * 0.90, MinSceneZoom, MaxSceneZoom);
    }

    private bool DoesZoomFitViewport(double zoom, double edgePaddingPx)
    {
        if (BrainViewport.Camera is not PerspectiveCamera camera)
        {
            return false;
        }

        var width = BrainViewport.ActualWidth;
        var height = BrainViewport.ActualHeight;
        if (width < 2.0 || height < 2.0)
        {
            return false;
        }

        var marginX = Math.Min(width * 0.2, edgePaddingPx);
        var marginY = Math.Min(height * 0.2, edgePaddingPx);
        var fovRadians = DegreesToRadians(camera.FieldOfView);
        if (fovRadians <= 0.0)
        {
            return false;
        }

        var halfHeight = height * 0.5;
        var focal = halfHeight / Math.Tan(fovRadians * 0.5);
        var halfWidth = width * 0.5;

        foreach (var worldPoint in _cameraFitSamplePoints)
        {
            var transformed = ApplySceneTransform(worldPoint, zoom);
            var relX = transformed.X - camera.Position.X;
            var relY = transformed.Y - camera.Position.Y;
            var relZ = camera.Position.Z - transformed.Z;
            if (relZ <= 0.02)
            {
                return false;
            }

            var sx = (relX * focal / relZ) + halfWidth;
            var sy = (-relY * focal / relZ) + halfHeight;
            if (double.IsNaN(sx) || double.IsNaN(sy) || double.IsInfinity(sx) || double.IsInfinity(sy))
            {
                return false;
            }

            if (sx < marginX || sx > (width - marginX) || sy < marginY || sy > (height - marginY))
            {
                return false;
            }
        }

        return true;
    }

    private void EndDrag()
    {
        _isDragging = false;
        if (BrainViewport.IsMouseCaptured)
        {
            BrainViewport.ReleaseMouseCapture();
        }
    }
}
