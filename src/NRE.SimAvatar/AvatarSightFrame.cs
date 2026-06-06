namespace NRE.SimAvatar;

public sealed record AvatarSightFrame(
    int Generation,
    long CaptureTimestampMs,
    int Width,
    int Height,
    int Stride,
    byte[] Pixels,
    double PreviewHeadingDeg,
    string PixelFormat = "Bgra32");
