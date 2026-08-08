namespace NRE.SimAvatar;

/// <summary>
/// Renders an external typed message as visible pixels. The returned sensory
/// frame contains no text, token, language-mode, hemisphere, or neural-target
/// metadata; interpretation belongs to the retinal and cortical pathways.
/// </summary>
public static class AvatarTextSightRenderer
{
    public const int FrameWidth = 384;
    public const int FrameHeight = 192;
    public const int GlyphScale = 3;
    public const int MaximumVisibleCharacters = 120;

    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int Margin = 8;
    private const int CellWidth = (GlyphWidth + 1) * GlyphScale;
    private const int CellHeight = (GlyphHeight + 2) * GlyphScale;
    private const int Columns = (FrameWidth - (Margin * 2)) / CellWidth;
    private const int Rows = (FrameHeight - (Margin * 2)) / CellHeight;

    private static readonly IReadOnlyDictionary<char, byte[]> Glyphs = BuildGlyphs();

    public static AvatarSightFrame Render(
        string text,
        int generation,
        long captureTimestampMs)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Visible text cannot be empty.", nameof(text));
        }

        var stride = checked(FrameWidth * 4);
        var pixels = new byte[checked(stride * FrameHeight)];
        Fill(pixels, blue: 244, green: 246, red: 248, alpha: 255);

        var column = 0;
        var row = 0;
        var visibleCharacters = 0;
        foreach (var sourceCharacter in text)
        {
            if (visibleCharacters >= MaximumVisibleCharacters || row >= Rows)
            {
                break;
            }

            if (sourceCharacter is '\r')
            {
                continue;
            }
            if (sourceCharacter is '\n')
            {
                column = 0;
                row++;
                continue;
            }

            if (column >= Columns)
            {
                column = 0;
                row++;
                if (row >= Rows)
                {
                    break;
                }
            }

            var character = sourceCharacter == '\t'
                ? ' '
                : char.ToUpperInvariant(sourceCharacter);
            if (!char.IsControl(character) && character != ' ')
            {
                DrawGlyph(pixels, stride, column, row, character);
            }

            column++;
            visibleCharacters++;
        }

        return new AvatarSightFrame(
            generation,
            captureTimestampMs,
            FrameWidth,
            FrameHeight,
            stride,
            pixels,
            PreviewHeadingDeg: 0.0,
            PixelFormat: "Bgra32");
    }

    private static void Fill(byte[] pixels, byte blue, byte green, byte red, byte alpha)
    {
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = blue;
            pixels[offset + 1] = green;
            pixels[offset + 2] = red;
            pixels[offset + 3] = alpha;
        }
    }

    private static void DrawGlyph(byte[] pixels, int stride, int column, int row, char character)
    {
        var glyph = Glyphs.TryGetValue(character, out var known) ? known : Glyphs['?'];
        var originX = Margin + (column * CellWidth);
        var originY = Margin + (row * CellHeight);
        for (var glyphY = 0; glyphY < GlyphHeight; glyphY++)
        {
            var bits = glyph[glyphY];
            for (var glyphX = 0; glyphX < GlyphWidth; glyphX++)
            {
                if ((bits & (1 << (GlyphWidth - 1 - glyphX))) == 0)
                {
                    continue;
                }

                for (var scaleY = 0; scaleY < GlyphScale; scaleY++)
                {
                    var pixelY = originY + (glyphY * GlyphScale) + scaleY;
                    for (var scaleX = 0; scaleX < GlyphScale; scaleX++)
                    {
                        var pixelX = originX + (glyphX * GlyphScale) + scaleX;
                        var offset = (pixelY * stride) + (pixelX * 4);
                        pixels[offset] = 24;
                        pixels[offset + 1] = 24;
                        pixels[offset + 2] = 24;
                        pixels[offset + 3] = 255;
                    }
                }
            }
        }
    }

    private static IReadOnlyDictionary<char, byte[]> BuildGlyphs() => new Dictionary<char, byte[]>
    {
        ['A'] = [14, 17, 17, 31, 17, 17, 17],
        ['B'] = [30, 17, 17, 30, 17, 17, 30],
        ['C'] = [14, 17, 16, 16, 16, 17, 14],
        ['D'] = [30, 17, 17, 17, 17, 17, 30],
        ['E'] = [31, 16, 16, 30, 16, 16, 31],
        ['F'] = [31, 16, 16, 30, 16, 16, 16],
        ['G'] = [14, 17, 16, 23, 17, 17, 15],
        ['H'] = [17, 17, 17, 31, 17, 17, 17],
        ['I'] = [14, 4, 4, 4, 4, 4, 14],
        ['J'] = [7, 2, 2, 2, 18, 18, 12],
        ['K'] = [17, 18, 20, 24, 20, 18, 17],
        ['L'] = [16, 16, 16, 16, 16, 16, 31],
        ['M'] = [17, 27, 21, 21, 17, 17, 17],
        ['N'] = [17, 25, 21, 19, 17, 17, 17],
        ['O'] = [14, 17, 17, 17, 17, 17, 14],
        ['P'] = [30, 17, 17, 30, 16, 16, 16],
        ['Q'] = [14, 17, 17, 17, 21, 18, 13],
        ['R'] = [30, 17, 17, 30, 20, 18, 17],
        ['S'] = [15, 16, 16, 14, 1, 1, 30],
        ['T'] = [31, 4, 4, 4, 4, 4, 4],
        ['U'] = [17, 17, 17, 17, 17, 17, 14],
        ['V'] = [17, 17, 17, 17, 17, 10, 4],
        ['W'] = [17, 17, 17, 21, 21, 21, 10],
        ['X'] = [17, 17, 10, 4, 10, 17, 17],
        ['Y'] = [17, 17, 10, 4, 4, 4, 4],
        ['Z'] = [31, 1, 2, 4, 8, 16, 31],
        ['0'] = [14, 17, 19, 21, 25, 17, 14],
        ['1'] = [4, 12, 4, 4, 4, 4, 14],
        ['2'] = [14, 17, 1, 2, 4, 8, 31],
        ['3'] = [30, 1, 1, 14, 1, 1, 30],
        ['4'] = [2, 6, 10, 18, 31, 2, 2],
        ['5'] = [31, 16, 16, 30, 1, 1, 30],
        ['6'] = [14, 16, 16, 30, 17, 17, 14],
        ['7'] = [31, 1, 2, 4, 8, 8, 8],
        ['8'] = [14, 17, 17, 14, 17, 17, 14],
        ['9'] = [14, 17, 17, 15, 1, 1, 14],
        ['.'] = [0, 0, 0, 0, 0, 12, 12],
        [','] = [0, 0, 0, 0, 12, 12, 8],
        ['!'] = [4, 4, 4, 4, 4, 0, 4],
        ['?'] = [14, 17, 1, 2, 4, 0, 4],
        ['\''] = [4, 4, 8, 0, 0, 0, 0],
        ['"'] = [10, 10, 10, 0, 0, 0, 0],
        [':'] = [0, 12, 12, 0, 12, 12, 0],
        [';'] = [0, 12, 12, 0, 12, 12, 8],
        ['-'] = [0, 0, 0, 31, 0, 0, 0],
        ['_'] = [0, 0, 0, 0, 0, 0, 31],
        ['/'] = [1, 1, 2, 4, 8, 16, 16],
        ['('] = [2, 4, 8, 8, 8, 4, 2],
        [')'] = [8, 4, 2, 2, 2, 4, 8],
        ['+'] = [0, 4, 4, 31, 4, 4, 0],
        ['='] = [0, 0, 31, 0, 31, 0, 0]
    };
}
