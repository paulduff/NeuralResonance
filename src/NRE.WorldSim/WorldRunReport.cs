using System.Text.Json;

namespace NRE.WorldSim;

public sealed record WorldRunReport(
    string ProtocolVersion,
    DateTimeOffset PersistedUtc,
    string Reason,
    WorldSimulationSnapshot Snapshot);

internal static class WorldRunReportStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string WriteAtomic(string directory, string reason, WorldSimulationSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(directory);

        var safeReason = new string(reason
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');
        if (safeReason.Length == 0)
        {
            safeReason = "snapshot";
        }

        var timestamp = DateTimeOffset.UtcNow;
        var fileName = $"world-run-{snapshot.SessionId}-{snapshot.WorldTick:D10}-{safeReason}-{timestamp:yyyyMMddTHHmmssfffZ}.json";
        var destination = Path.Combine(directory, fileName);
        var temporary = destination + $".{Environment.ProcessId}.tmp";
        var report = new WorldRunReport("dnne.world-run.v1", timestamp, reason, snapshot);
        File.WriteAllText(temporary, JsonSerializer.Serialize(report, JsonOptions));
        File.Move(temporary, destination, overwrite: true);
        return destination;
    }
}
