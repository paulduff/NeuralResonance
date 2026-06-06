using System;
using NRE.Core.Engine;
using Xunit;

namespace NRE.Tests;

public sealed class PixelVisionTests
{
    [Fact]
    public void SetVisualFrame_Drives_V1_Hierarchy_Activity()
    {
        // Build a small engine with SensoryHierarchy enabled.
        var opt = new NreEngineOptions
        {
            W = 8, H = 8, D = 8,
            EnableSensoryHierarchy = true,
            SensoryVisualWidth = 16,
            EnableConsoleDiagnostics = false
        };
        using var e = new NreEngine(opt, seed: 11);
        e.Start();

        // Baseline: no frame, no visual stimulus → V1 activity should be ~0.
        for (int i = 0; i < 8; i++) e.Step(0.016f);
        var baseline = e.SensoryHierarchy.Snapshot().V1MeanActivity;

        // Drive a real frame: a vertical-stripes pattern. This should excite V1's
        // orientation-tuned channels via the existing Gabor filtering.
        const int fw = 64, fh = 64;
        var frame = new float[fw * fh];
        for (int y = 0; y < fh; y++)
        for (int x = 0; x < fw; x++)
        {
            frame[y * fw + x] = ((x / 4) % 2) == 0 ? 0.95f : 0.05f;
        }
        e.SetVisualFrame(frame, fw, fh);

        // Step enough times for the hierarchy refresh cadence (StepIndex % 4 == 0).
        for (int i = 0; i < 32; i++) e.Step(0.016f);
        var driven = e.SensoryHierarchy.Snapshot().V1MeanActivity;

        Assert.True(driven > baseline + 0.0005f,
            $"V1 mean activity did not rise after SetVisualFrame: baseline={baseline:0.0000}, driven={driven:0.0000}");
    }

    [Fact]
    public void ClearVisualFrame_Reverts_To_Synthetic_Path()
    {
        var opt = new NreEngineOptions
        {
            W = 8, H = 8, D = 8,
            EnableSensoryHierarchy = true,
            SensoryVisualWidth = 16,
            EnableConsoleDiagnostics = false
        };
        using var e = new NreEngine(opt, seed: 11);
        e.Start();

        // Push a frame, then clear it. Engine should not throw and should accept
        // a subsequent synthetic stimulus call (the pre-existing path).
        var frame = new float[16 * 16];
        for (int i = 0; i < frame.Length; i++) frame[i] = 0.5f;
        e.SetVisualFrame(frame, 16, 16);
        for (int i = 0; i < 4; i++) e.Step(0.016f);

        e.ClearVisualFrame();
        // After clear, synthetic-path SetVisualStimulus should regain control.
        e.SetVisualStimulus(intensity01: 0.4f, speedHz: 2.0f, spatialFreq: 0.2f);
        for (int i = 0; i < 4; i++) e.Step(0.016f);
        // No assertion needed beyond "didn't throw" — the smoke test confirms the
        // frame buffer can be released without leaving the engine in a broken state.
    }

    [Fact]
    public void SetVisualFrame_Accepts_Byte_Grayscale_Overload()
    {
        var opt = new NreEngineOptions
        {
            W = 8, H = 8, D = 8,
            EnableSensoryHierarchy = true,
            SensoryVisualWidth = 8,
            EnableConsoleDiagnostics = false
        };
        using var e = new NreEngine(opt, seed: 11);
        e.Start();

        // 8-bit grayscale buffer, the common renderer output shape.
        const int fw = 32, fh = 32;
        var bytes = new byte[fw * fh];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i % 256);
        e.SetVisualFrame(bytes, fw, fh);
        for (int i = 0; i < 16; i++) e.Step(0.016f);

        var snap = e.SensoryHierarchy.Snapshot();
        Assert.True(snap.V1MeanActivity >= 0f); // sanity: no NaN / negative
    }

    [Fact]
    public void SetVisualFrame_Throws_On_Invalid_Dimensions()
    {
        var opt = new NreEngineOptions { W = 8, H = 8, D = 8, EnableConsoleDiagnostics = false };
        using var e = new NreEngine(opt, seed: 11);
        // Zero / negative dimensions.
        Assert.Throws<ArgumentException>(() => e.SetVisualFrame(new float[1], 0, 1));
        Assert.Throws<ArgumentException>(() => e.SetVisualFrame(new float[1], 1, 0));
        // Buffer too small for stated dimensions.
        Assert.Throws<ArgumentException>(() => e.SetVisualFrame(new float[3], 2, 2));
    }
}
