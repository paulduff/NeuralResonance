namespace NRE.SimAvatar;

public static class AvatarVisualSignalFactory
{
    public static AvatarVisualSignal FromWebcam(
        double motionSignal,
        double luminanceSignal,
        double leftSaliency,
        double rightSaliency)
    {
        var intensity = (float)Math.Clamp(0.35 + (motionSignal / 30.0), 0.2, 3.0);
        var burstCount = Math.Clamp((int)Math.Round(8.0 + (motionSignal * 0.55)), 4, 64);
        return new AvatarVisualSignal(
            AvatarRuntimeDefaults.UnifiedVisualStreamPattern,
            intensity,
            burstCount,
            leftSaliency,
            rightSaliency,
            Math.Clamp(motionSignal / 255.0, 0.0, 1.0),
            Math.Clamp(luminanceSignal / 255.0, 0.0, 1.0),
            AvatarRuntimeDefaults.UnifiedVisualInputSource);
    }

    public static AvatarVisualSignal FromWorldFrame(float brightness, float leftSaliency, float rightSaliency)
    {
        var clampedBrightness = Math.Clamp(brightness, 0.0f, 1.0f);
        var intensity = (float)Math.Clamp(0.45 + (clampedBrightness * 1.1), 0.2, 2.0);
        var burstCount = (int)Math.Clamp(10 + (clampedBrightness * 32), 8, 64);
        return new AvatarVisualSignal(
            AvatarRuntimeDefaults.UnifiedVisualStreamPattern,
            intensity,
            burstCount,
            leftSaliency,
            rightSaliency,
            0.0,
            clampedBrightness,
            AvatarRuntimeDefaults.UnifiedVisualInputSource);
    }

    public static AvatarVisualSignal FromRenderedFrame(
        string pattern,
        float intensity,
        int burstCount,
        double leftSaliency,
        double rightSaliency,
        double motionSignal,
        double luminanceSignal = 0.0)
        => new(
            string.IsNullOrWhiteSpace(pattern) ? AvatarRuntimeDefaults.UnifiedVisualStreamPattern : pattern,
            Math.Clamp(intensity, 0.05f, 3.5f),
            Math.Clamp(burstCount, 1, 96),
            leftSaliency,
            rightSaliency,
            Math.Clamp(motionSignal, 0.0, 1.0),
            Math.Clamp(luminanceSignal, 0.0, 1.0),
            AvatarRuntimeDefaults.UnifiedVisualInputSource);
}
