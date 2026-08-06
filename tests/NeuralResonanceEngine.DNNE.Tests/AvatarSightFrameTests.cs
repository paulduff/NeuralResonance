using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarSightFrameTests
{
    [Theory]
    [InlineData("Bgra32", 4)]
    [InlineData("Rgb24", 3)]
    public void RawSightFrameAcceptsSupportedPixelFormats(string pixelFormat, int bytesPerPixel)
    {
        var frame = new AvatarSightFrame(
            Generation: 7,
            CaptureTimestampMs: 1234,
            Width: 2,
            Height: 2,
            Stride: 2 * bytesPerPixel,
            Pixels: new byte[4 * bytesPerPixel],
            PreviewHeadingDeg: 90.0,
            PixelFormat: pixelFormat);

        frame.Validate();

        Assert.Equal(pixelFormat, frame.PixelFormat);
        Assert.Equal(90.0, frame.PreviewHeadingDeg);
    }

    [Fact]
    public void RawSightFrameRejectsSemanticPixelFormat()
    {
        var frame = new AvatarSightFrame(
            Generation: 1,
            CaptureTimestampMs: 1,
            Width: 1,
            Height: 1,
            Stride: 4,
            Pixels: new byte[4],
            PreviewHeadingDeg: 0,
            PixelFormat: "RecognizedObject");

        Assert.Throws<ArgumentOutOfRangeException>(frame.Validate);
    }
}
