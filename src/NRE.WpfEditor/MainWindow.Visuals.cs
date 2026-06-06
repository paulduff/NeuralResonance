using System.Windows.Media;

namespace NRE.WpfEditor;

// Color / visual helper methods. Pure static; no class state.
// Extracted from MainWindow.xaml.cs to reduce that file's size and group like with like.
public partial class MainWindow
{
    private static Color Blend(Color from, Color to, double t)
    {
        var f = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(from.R + ((to.R - from.R) * f)),
            (byte)(from.G + ((to.G - from.G) * f)),
            (byte)(from.B + ((to.B - from.B) * f)));
    }

    private static Color ScaleColor(Color color, double factor)
    {
        var f = Math.Clamp(factor, 0.0, 2.0);
        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(color.R * f), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.G * f), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.B * f), 0, 255));
    }

    private static Color BoostSpikeColor(Color color)
    {
        var boosted = ScaleColor(color, 3.4);
        var minFloor = 210;
        return Color.FromRgb(
            (byte)Math.Max(minFloor, boosted.R),
            (byte)Math.Max(minFloor, boosted.G),
            (byte)Math.Max(minFloor, boosted.B));
    }

    private static Color BrightenPreserveHue(Color color, double lift)
    {
        var amount = Math.Clamp(lift, 0.0, 1.0);
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        if (max <= 0.00001)
        {
            return Color.FromRgb(0, 0, 0);
        }

        var targetMax = max + ((1.0 - max) * amount);
        var scale = targetMax / max;
        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(r * scale * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g * scale * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b * scale * 255.0), 0, 255));
    }

    private static Dictionary<string, Color> BuildUniqueStructureColorMap(IEnumerable<StructureDefinition> definitions)
    {
        var snapshotIds = definitions
            .Select(d => d.SnapshotId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<int>();
        for (var i = 0; i < snapshotIds.Count; i++)
        {
            var id = snapshotIds[i];
            var hue = (i * 137.50776405003785 + 17.0) % 360.0;
            var saturation = 0.64 + (((i * 7) % 5) * 0.06);
            var lightness = 0.47 + (((i * 11) % 4) * 0.08);

            var assigned = false;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var candidate = ColorFromHsl(hue, saturation, lightness);
                var key = (candidate.R << 16) | (candidate.G << 8) | candidate.B;
                if (used.Add(key))
                {
                    map[id] = candidate;
                    assigned = true;
                    break;
                }

                hue = (hue + 9.0 + (attempt * 0.75)) % 360.0;
                lightness = Math.Clamp(lightness + ((attempt % 2 == 0) ? 0.03 : -0.03), 0.30, 0.78);
            }

            if (assigned)
            {
                continue;
            }

            var seed = DeterministicHashCode(id);
            for (var attempt = 0; attempt < 64; attempt++)
            {
                var hueFallback = (seed + (attempt * 31.0)) % 360.0;
                var satFallback = 0.55 + (((seed + (attempt * 13)) % 40) / 100.0);
                var lightFallback = 0.42 + (((seed + (attempt * 17)) % 30) / 100.0);
                var fallback = ColorFromHsl(hueFallback, satFallback, lightFallback);
                var key = (fallback.R << 16) | (fallback.G << 8) | fallback.B;
                if (used.Add(key))
                {
                    map[id] = fallback;
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                map[id] = Color.FromRgb(200, 200, 200);
            }
        }

        return map;
    }

    private static Color ColorFromHsl(double hueDeg, double saturation, double lightness)
    {
        var h = ((hueDeg % 360.0) + 360.0) % 360.0 / 360.0;
        var s = Math.Clamp(saturation, 0.0, 1.0);
        var l = Math.Clamp(lightness, 0.0, 1.0);
        if (s <= 0.00001)
        {
            var mono = (byte)Math.Clamp((int)Math.Round(l * 255.0), 0, 255);
            return Color.FromRgb(mono, mono, mono);
        }

        var q = l < 0.5 ? l * (1.0 + s) : (l + s - (l * s));
        var p = 2.0 * l - q;

        static double HueToRgb(double pLocal, double qLocal, double tLocal)
        {
            var t = tLocal;
            if (t < 0.0) t += 1.0;
            if (t > 1.0) t -= 1.0;
            if (t < (1.0 / 6.0)) return pLocal + ((qLocal - pLocal) * 6.0 * t);
            if (t < 0.5) return qLocal;
            if (t < (2.0 / 3.0)) return pLocal + ((qLocal - pLocal) * ((2.0 / 3.0) - t) * 6.0);
            return pLocal;
        }

        var r = HueToRgb(p, q, h + (1.0 / 3.0));
        var g = HueToRgb(p, q, h);
        var b = HueToRgb(p, q, h - (1.0 / 3.0));
        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(r * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b * 255.0), 0, 255));
    }

    private static int DeterministicHashCode(string text)
    {
        unchecked
        {
            var hash = 5381;
            for (var i = 0; i < text.Length; i++)
            {
                hash = ((hash << 5) + hash) ^ text[i];
            }
            return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
        }
    }
}
