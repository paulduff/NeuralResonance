using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SnapshotStoreTests
{
    [Fact]
    public async Task StoreBoundsMemoryPersistsSnapshotsAndClearsAtomically()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dnne-snapshots-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "snapshots.ndjson");
        Directory.CreateDirectory(directory);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SnapshotStore:Path"] = path,
                ["SnapshotStore:MaxInMemorySnapshots"] = "32",
                ["SnapshotStore:FlushEvery"] = "1"
            })
            .Build();

        var store = new SnapshotStore(configuration);
        try
        {
            for (var tick = 0; tick < 40; tick++)
            {
                await store.AppendAsync(CreateSnapshot(tick));
            }

            Assert.Equal(32, store.GetAll().Count);
            Assert.Equal(39, store.GetLatest()?.Tick);
            Assert.Equal(40, ReadLiveFileLines(path).Count);

            await store.ClearAsync();

            Assert.Empty(store.GetAll());
            Assert.Null(store.GetLatest());
            Assert.Contains("simulation_restart", string.Join(Environment.NewLine, ReadLiveFileLines(path)));

            await store.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await store.AppendAsync(CreateSnapshot(41)));
        }
        finally
        {
            await store.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static BrainSnapshot CreateSnapshot(long tick) => new(
        tick,
        tick,
        new NeuromodState(),
        new Dictionary<BrainRhythm, double>(),
        0,
        Array.Empty<StructureSnapshot>(),
        Array.Empty<ActivePathway>());

    private static IReadOnlyList<string> ReadLiveFileLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }
        return lines;
    }
}
