using System.Text.Json;
using NRE.WorldSim;

namespace NRE.BlazorEditor.Services;

public sealed class WorldStateReader(HeadlessWorldRuntime runtime)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<WorldStateEnvelope> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = runtime.GetSnapshot();
        var ageSeconds = Math.Max(0.0, (DateTimeOffset.UtcNow - snapshot.GeneratedUtc).TotalSeconds);
        var status = snapshot.Running ? "live" : "stopped";
        var message = snapshot.Running
            ? "Authoritative headless WorldSim is live."
            : "Authoritative headless WorldSim is paused.";
        var state = JsonSerializer.SerializeToElement(snapshot, JsonOptions);
        return Task.FromResult(new WorldStateEnvelope(true, ageSeconds, status, message, state));
    }
}

public sealed record WorldStateEnvelope(
    bool Available,
    double AgeSeconds,
    string Status,
    string Message,
    JsonElement? State);
