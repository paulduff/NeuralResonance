using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarVisualSignalTests
{
    [Fact]
    public void WebcamVisionUsesUnifiedAvatarVisualSignal()
    {
        var signal = AvatarVisualSignalFactory.FromWebcam(
            motionSignal: 300.0,
            luminanceSignal: 127.5,
            leftSaliency: 0.25,
            rightSaliency: 0.75);

        var request = signal.ToVisualInputRequest();

        Assert.Equal(AvatarRuntimeDefaults.UnifiedVisualStreamPattern, request.Pattern);
        Assert.Equal(AvatarRuntimeDefaults.UnifiedVisualInputSource, request.InputSource);
        Assert.Equal(AvatarRuntimeDefaults.UnifiedVisualStreamSourceStructure, request.SourceStructure);
        Assert.Equal("V1", request.TargetStructure);
        Assert.Equal(3.0f, request.Intensity);
        Assert.Equal(64, request.BurstCount);
        Assert.Equal(1.0, signal.MotionSignal);
        Assert.Equal(0.5, signal.LuminanceSignal, 3);
        Assert.Equal(0.25f, request.LeftFieldSaliency);
        Assert.Equal(0.75f, request.RightFieldSaliency);
        Assert.True(request.UseAttentionRouting);
    }

    [Fact]
    public void WorldFrameVisionUsesSameSignalShape()
    {
        var signal = AvatarVisualSignalFactory.FromWorldFrame(
            brightness: 0.5f,
            leftSaliency: 0.6f,
            rightSaliency: 0.4f);

        var request = signal.ToVisualInputRequest(
            targetStructure: "V1",
            sourceStructure: "Retina");

        Assert.Equal(AvatarRuntimeDefaults.UnifiedVisualStreamPattern, signal.Pattern);
        Assert.Equal(AvatarRuntimeDefaults.UnifiedVisualInputSource, signal.InputSource);
        Assert.Equal(1.0f, request.Intensity);
        Assert.Equal(26, request.BurstCount);
        Assert.Equal("Retina", request.SourceStructure);
        Assert.Equal(0.5, signal.LuminanceSignal);
        Assert.Equal(0.6f, request.LeftFieldSaliency);
        Assert.Equal(0.4f, request.RightFieldSaliency);
    }

    [Fact]
    public void Bgra32FramesConvertToSharedGrayscaleBuffer()
    {
        byte[] bgra32 =
        [
            10, 20, 30, 255,
            30, 60, 90, 255,
            0, 0, 255, 255,
            90, 30, 0, 255
        ];
        byte[] grayscale = new byte[4];

        AvatarPixelVision.ConvertBgra32ToGrayscale(bgra32, width: 2, height: 2, grayscale);

        Assert.Equal([20, 60, 85, 40], grayscale);
    }

    [Fact]
    public void AvatarSightFrameCarriesPreviewOutputPixelsSeparatelyFromInputSignal()
    {
        byte[] pixels = [1, 2, 3, 255];
        var sightFrame = new AvatarSightFrame(
            Generation: 7,
            CaptureTimestampMs: 1234,
            Width: 1,
            Height: 1,
            Stride: 4,
            Pixels: pixels,
            PreviewHeadingDeg: 90.0);

        var inputSignal = AvatarVisualSignalFactory.FromWorldFrame(0.5f, 0.25f, 0.75f);

        Assert.Same(pixels, sightFrame.Pixels);
        Assert.Equal("Bgra32", sightFrame.PixelFormat);
        Assert.Equal(90.0, sightFrame.PreviewHeadingDeg);
        Assert.Equal(0.25, inputSignal.LeftSaliency);
        Assert.Equal(0.75, inputSignal.RightSaliency);
    }
}
