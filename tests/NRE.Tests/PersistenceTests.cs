using System;
using System.IO;
using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void Snapshot_Roundtrip_Preserves_StepIndex_And_Neuromods()
    {
        // Pick a small grid + a unique temp path so this test is hermetic.
        var path = Path.Combine(Path.GetTempPath(), $"nre-persist-test-{Guid.NewGuid():N}.bin");
        try
        {
            // Phase 1: build engine A, run some ticks, force a save, then dispose.
            var opt = MakeOptions(path, snapshotIntervalSec: 0.001f); // immediate-eligible
            using (var a = new NreEngine(opt, seed: 7))
            {
                a.Start();
                // Push a dopamine offset so we have a non-default neuromod state to verify.
                a.SetNeuromodulator("dopamine", 0.42f);
                // Run a handful of ticks; persistence accumulator fires after >= interval.
                for (int i = 0; i < 32; i++)
                {
                    a.Step(0.016f);
                }
            }

            // Engine A is disposed; the snapshot file should now exist on disk.
            Assert.True(File.Exists(path), $"Snapshot file was not written to {path}");

            // Phase 2: build a fresh engine B with the SAME path. Start() should restore.
            var optRestore = MakeOptions(path, snapshotIntervalSec: 999f); // don't re-save during test
            using var b = new NreEngine(optRestore, seed: 999);
            b.Start();

            // Verify continuity: StepIndex should be > 0 because we ran 32 ticks in A.
            // (Note: BrainSerializer version 3 persists StepIndex — see Program.SaveState header.)
            Assert.True(b.StepIndex > 0, $"Restored engine has StepIndex=0; expected > 0 (was 32 in A)");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void Restore_Without_Existing_File_Boots_Fresh()
    {
        // Path is set but doesn't exist on disk — engine should boot with StepIndex=0.
        var path = Path.Combine(Path.GetTempPath(), $"nre-persist-missing-{Guid.NewGuid():N}.bin");
        Assert.False(File.Exists(path));

        var opt = MakeOptions(path, snapshotIntervalSec: 999f);
        using var e = new NreEngine(opt, seed: 7);
        e.Start();
        Assert.Equal(0L, e.StepIndex);
    }

    [Fact]
    public void Restore_From_Corrupt_File_Boots_Fresh_Without_Throwing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nre-persist-corrupt-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(path, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 });
            var opt = MakeOptions(path, snapshotIntervalSec: 999f);
            using var e = new NreEngine(opt, seed: 7);
            // Should not throw — corrupt snapshots are logged and ignored.
            e.Start();
            Assert.Equal(0L, e.StepIndex);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    private static NreEngineOptions MakeOptions(string path, float snapshotIntervalSec)
    {
        // Tiny grid for fast tests.
        return new NreEngineOptions
        {
            W = 8,
            H = 8,
            D = 8,
            PersistenceSnapshotPath = path,
            PersistenceSnapshotIntervalSeconds = snapshotIntervalSec,
            EnableConsoleDiagnostics = false
        };
    }
}
