namespace NRE.SimAvatar;

public sealed record AvatarSightFrame(
    int Generation,
    long CaptureTimestampMs,
    int Width,
    int Height,
    int Stride,
    byte[] Pixels,
    double PreviewHeadingDeg,
    string PixelFormat = "Bgra32")
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Pixels);
        if (Width <= 0 || Height <= 0 || Stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Sight-frame dimensions and stride must be positive.");
        }

        var bytesPerPixel = PixelFormat.Trim().ToUpperInvariant() switch
        {
            "BGRA32" => 4,
            "RGB24" => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(PixelFormat), "Sight-frame pixel format must be Bgra32 or Rgb24.")
        };
        var minimumStride = checked(Width * bytesPerPixel);
        if (Stride < minimumStride)
        {
            throw new ArgumentException("Sight-frame stride is smaller than the pixel width.", nameof(Stride));
        }

        var requiredBytes = checked(((Height - 1) * Stride) + minimumStride);
        if (Pixels.Length < requiredBytes)
        {
            throw new ArgumentException("Sight-frame pixel buffer is smaller than its dimensions require.", nameof(Pixels));
        }
    }
}
