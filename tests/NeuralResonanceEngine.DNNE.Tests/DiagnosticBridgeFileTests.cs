using System.Text;
using Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DiagnosticBridgeFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dnne-read-lock-" + Guid.NewGuid().ToString("N"));
    public DiagnosticBridgeFileTests() => Directory.CreateDirectory(_directory);
    private string FilePath => Path.Combine(_directory, "reply.json");

    [Fact]
    public void Missing_reply_remains_missing_and_is_not_created()
    {
        Assert.Equal(BridgeReadOutcome.Missing, DiagnosticBridgeFiles.TryRead(FilePath, out var text));
        Assert.Null(text); Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Published_utf8_reply_is_read_without_modifying_evidence()
    {
        const string original = "{\"label\":\"body \u03b1\",\"step\":240}\n";
        File.WriteAllText(FilePath, original, new UTF8Encoding(false));
        byte[] bytes = File.ReadAllBytes(FilePath); var stamp = File.GetLastWriteTimeUtc(FilePath);
        Assert.Equal(BridgeReadOutcome.Ready, DiagnosticBridgeFiles.TryRead(FilePath, out var text));
        Assert.Equal(original, text); Assert.Equal(bytes, File.ReadAllBytes(FilePath));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(FilePath));
    }

    [Theory]
    [InlineData(FileShare.None)]
    [InlineData(FileShare.Write)]
    public void Exclusive_read_conflict_recovers_by_reading_the_same_reply(FileShare sharing)
    {
        File.WriteAllText(FilePath, "{\"step\":240}"); byte[] original = File.ReadAllBytes(FilePath);
        using (var hold = new FileStream(FilePath, FileMode.Open, FileAccess.Read, sharing))
        {
            for (int i = 0; i < 2; ++i)
            {
                Assert.Equal(BridgeReadOutcome.SharingViolation, DiagnosticBridgeFiles.TryRead(FilePath, out var unavailable));
                Assert.Null(unavailable);
            }
        }
        Assert.Equal(BridgeReadOutcome.Ready, DiagnosticBridgeFiles.TryRead(FilePath, out var recovered));
        Assert.Equal("{\"step\":240}", recovered); Assert.Equal(original, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void Persistent_reply_lock_times_out_without_modifying_the_reply()
    {
        File.WriteAllText(FilePath, "{\"step\":240}"); byte[] original = File.ReadAllBytes(FilePath);
        using (var hold = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<TimeoutException>(() => DiagnosticBridgeFiles.ReadPublished(FilePath, 20));
        Assert.Equal(original, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void Access_error_is_not_misclassified_as_a_retryable_read_lock()
    {
        Assert.Throws<UnauthorizedAccessException>(() => DiagnosticBridgeFiles.TryRead(_directory, out _));
    }

    public void Dispose() { File.Delete(FilePath); Directory.Delete(_directory); }
}
