using System.Text.Json;

namespace NRE.SimAvatar;

public static class AvatarDispatchSpikeParser
{
    public static List<AvatarDispatchSpike> ParseDispatchSpikes(JsonElement root, long sinceMs, out long maxWallClockMs)
    {
        maxWallClockMs = sinceMs;
        if (!AvatarJson.TryGetProperty(root, "dispatchSpikes", out var dispatchArray) || dispatchArray.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var entries = new List<AvatarDispatchSpike>(dispatchArray.GetArrayLength());
        foreach (var item in dispatchArray.EnumerateArray())
        {
            var wallClockMs = AvatarJson.GetLong(item, "wallClockUnixMs", "wall_clock_unix_ms");
            if (wallClockMs <= sinceMs)
            {
                continue;
            }

            if (wallClockMs > maxWallClockMs)
            {
                maxWallClockMs = wallClockMs;
            }

            var sourceStructure = AvatarJson.ParseAnyStructureId(item, "sourceStructure", "source_structure");
            if (string.IsNullOrWhiteSpace(sourceStructure))
            {
                continue;
            }

            var sourceHemisphere = AvatarJson.NormalizeHemisphere(AvatarJson.GetString(item, "sourceHemisphere", "source_hemisphere"));
            var sourceNeuronId = AvatarJson.GetString(item, "sourceNeuronId", "source_neuron_id");
            entries.Add(new AvatarDispatchSpike(sourceStructure, sourceHemisphere, wallClockMs, sourceNeuronId));
        }

        return entries;
    }
}
