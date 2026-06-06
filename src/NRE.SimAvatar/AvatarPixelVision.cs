namespace NRE.SimAvatar;

public static class AvatarPixelVision
{
    public static void ConvertBgra32ToGrayscale(
        ReadOnlySpan<byte> bgra32,
        int width,
        int height,
        Span<byte> grayscale)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Pixel dimensions must be positive.");
        }

        var pixelCount = checked(width * height);
        if (bgra32.Length < pixelCount * 4)
        {
            throw new ArgumentException("BGRA32 buffer is smaller than width*height*4.", nameof(bgra32));
        }

        if (grayscale.Length < pixelCount)
        {
            throw new ArgumentException("Grayscale buffer is smaller than width*height.", nameof(grayscale));
        }

        for (int i = 0, p = 0; p < pixelCount; p++, i += 4)
        {
            grayscale[p] = (byte)((bgra32[i] + bgra32[i + 1] + bgra32[i + 2]) / 3);
        }
    }
}
