using NeuralResonanceEngine.Protocol;
using System.Globalization;
using System.Text.Json;

namespace NRE.SimAvatar;

public static class AvatarJson
{
    public static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (candidate.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate.Value;
                return true;
            }

            if (NamesMatchIgnoringUnderscores(candidate.Name, propertyName))
            {
                value = candidate.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool NamesMatchIgnoringUnderscores(ReadOnlySpan<char> candidate, ReadOnlySpan<char> requested)
    {
        var candidateIndex = 0;
        var requestedIndex = 0;
        while (true)
        {
            while (candidateIndex < candidate.Length && candidate[candidateIndex] == '_')
            {
                candidateIndex++;
            }

            while (requestedIndex < requested.Length && requested[requestedIndex] == '_')
            {
                requestedIndex++;
            }

            if (candidateIndex >= candidate.Length || requestedIndex >= requested.Length)
            {
                break;
            }

            if (char.ToUpperInvariant(candidate[candidateIndex]) != char.ToUpperInvariant(requested[requestedIndex]))
            {
                return false;
            }

            candidateIndex++;
            requestedIndex++;
        }

        while (candidateIndex < candidate.Length && candidate[candidateIndex] == '_')
        {
            candidateIndex++;
        }

        while (requestedIndex < requested.Length && requested[requestedIndex] == '_')
        {
            requestedIndex++;
        }

        return candidateIndex == candidate.Length && requestedIndex == requested.Length;
    }

    public static string GetString(JsonElement element, params string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            if (!TryGetProperty(element, propertyNames[i], out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    public static long GetLong(JsonElement element, params string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            if (!TryGetProperty(element, propertyNames[i], out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                if (value.TryGetInt64(out var parsedLong))
                {
                    return parsedLong;
                }

                return (long)value.GetDouble();
            }

            if (value.ValueKind == JsonValueKind.String &&
                long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0L;
    }

    public static int GetInt(JsonElement element, params string[] propertyNames)
    {
        var value = GetLong(element, propertyNames);
        return value > int.MaxValue ? int.MaxValue : (int)Math.Max(int.MinValue, value);
    }

    public static double GetDouble(JsonElement element, params string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            if (!TryGetProperty(element, propertyNames[i], out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetDouble();
            }

            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0.0;
    }

    public static bool GetBool(JsonElement element, params string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            if (!TryGetProperty(element, propertyNames[i], out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return false;
    }

    public static bool IsSleepingState(JsonElement stateElement)
    {
        if (!TryGetProperty(stateElement, "neuronalSleepConsolidation", out var neuronalSleep) ||
            neuronalSleep.ValueKind != JsonValueKind.Object ||
            !GetBool(neuronalSleep, "available") ||
            !GetBool(neuronalSleep, "stateActive"))
        {
            return false;
        }

        if (TryGetProperty(neuronalSleep, "state", out var stateValue) &&
            stateValue.ValueKind == JsonValueKind.Number &&
            stateValue.TryGetInt32(out var stateCode))
        {
            return stateCode is 1 or 2;
        }

        var state = GetString(neuronalSleep, "state");
        return state.Equals("nrem", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("rem", StringComparison.OrdinalIgnoreCase);
    }

    public static string ParseAnyStructureId(JsonElement element, params string[] propertyNames)
    {
        for (var i = 0; i < propertyNames.Length; i++)
        {
            if (!TryGetProperty(element, propertyNames[i], out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var enumOrdinal) &&
                Enum.IsDefined(typeof(StructureId), enumOrdinal))
            {
                return ((StructureId)enumOrdinal).ToString();
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var raw = value.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out enumOrdinal) &&
                Enum.IsDefined(typeof(StructureId), enumOrdinal))
            {
                return ((StructureId)enumOrdinal).ToString();
            }

            return NormalizeStructure(raw);
        }

        return string.Empty;
    }

    public static string NormalizeHemisphere(string hemisphere)
    {
        if (string.IsNullOrWhiteSpace(hemisphere))
        {
            return "M";
        }

        return hemisphere.Trim().ToUpperInvariant() switch
        {
            "LEFT" => "L",
            "RIGHT" => "R",
            "MIDLINE" => "M",
            "BOTH" => "M",
            "L" => "L",
            "R" => "R",
            "M" => "M",
            _ => "M"
        };
    }

    public static string NormalizeStructure(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("L_", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("R_", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("M_", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[2..];
        }

        return trimmed;
    }
}
