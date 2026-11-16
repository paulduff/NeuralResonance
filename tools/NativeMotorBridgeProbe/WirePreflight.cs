using System.Diagnostics;
using System.Text.Json;
using Nre.UnityWorld;
using static Nre.UnityWorld.MuscleHingeWire;
using Path=System.IO.Path;

internal static class WirePreflight
{
    private sealed record Sample(string Key, double DecimalValue, Real ExactValue, string ExpectedBits, bool CompareDecimal);

    internal static void Run(string player, string reference, string output)
    {
        Console.WriteLine("NATIVE WIRE PREFLIGHT: replay failed decimal timestamp and verify 1,040 exact scalar round trips before physics.");
        string legacyText = DiagnosticBridgeFiles.ReadPublished(Path.Combine(reference, "failed-request-000033.json"), 30000);
        using var legacy = JsonDocument.Parse(legacyText);
        using var previous = JsonDocument.Parse(DiagnosticBridgeFiles.ReadPublished(Path.Combine(reference, "last-reply-000032.json"), 30000));
        Require(legacy.RootElement.GetProperty("endTimeMs").GetDouble() == 10 * StepMs &&
            previous.RootElement.GetProperty("physicsStep").GetInt64() == 9, "Original failure evidence differs.");
        string oldEffort = previous.RootElement.GetProperty("lastAppliedEffortId").GetString();
        double tension = legacy.RootElement.GetProperty("tensions")[0].GetProperty("axialNewtons").GetDouble();
        using var parameters = JsonDocument.Parse(DiagnosticBridgeFiles.ReadPublished(Path.Combine(reference, "muscle-input.json"), 30000));
        double rest = parameters.RootElement.GetProperty("restPathLengthMeters").GetDouble();
        var samples = Enumerable.Range(0, 1025).Select(i => Make("clock-" + i, i * StepMs, true)).ToList();
        // Extreme/subnormal values exercise the binary codec only. They are not
        // mechanical inputs, nor requirements on the legacy decimal parser.
        double[] extra = [0, BitConverter.Int64BitsToDouble(long.MinValue), double.Epsilon, -double.Epsilon,
            double.MinValue, double.MaxValue, float.MaxValue, -float.MaxValue, .125,
            Math.BitIncrement(StepMs), Math.BitDecrement(StepMs), tension, rest, Math.BitIncrement(1.0), Math.BitDecrement(1.0)];
        for (int i = 0; i < extra.Length; ++i) samples.Add(Make("scalar-" + i, extra[i], i < 2 || i > 5));
        string[] invalid = [null, "", "0", "00000000000000000", "000000000000000g", "3ff0000000000000",
            " 3FF0000000000000", "7FF0000000000000", "FFF0000000000000", "7FF8000000000001"];
        foreach (string bits in invalid)
        {
            bool rejected = false; try { Decode(bits); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Host codec accepted malformed/nonfinite bits.");
        }
        string input = Path.Combine(output, "wire-preflight-input.json"), resultFile = Path.Combine(output, "wire-preflight-native.json");
        NativeHingeBody.Write(input, new { samples, invalidBits = invalid, legacyRequestJson = legacyText, legacyLastEffortId = oldEffort });
        var start = new ProcessStartInfo(player) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(player) };
        foreach (string arg in new[] { "-batchmode", "-nographics", "-logFile", Path.Combine(output, "wire-preflight-player.log"),
            "--wire-input", input, "--wire-output", resultFile }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Wire preflight player did not start.");
        try
        {
            if (!process.WaitForExit(180000)) throw new TimeoutException("Native wire preflight timed out.");
            Require(process.ExitCode == 0, "Native wire preflight failed; inspect preserved decoder evidence before a physical retry.");
            using var document = JsonDocument.Parse(DiagnosticBridgeFiles.ReadPublished(resultFile, 30000)); var result = document.RootElement;
            Require(result.GetProperty("completed").GetBoolean() && result.GetProperty("passed").GetBoolean() &&
                !result.GetProperty("physicsExecuted").GetBoolean() && result.GetProperty("exactCases").GetInt32() == 1040 &&
                result.GetProperty("rejectedMalformedScalars").GetInt32() == 10 && result.GetProperty("unity").GetString() == "6000.3.19f1",
                "Incomplete native codec evidence.");
            var rows = result.GetProperty("cases").EnumerateArray().ToArray(); Require(rows.Length == samples.Count, "Missing roundtrip samples.");
            for (int i = 0; i < rows.Length; ++i)
            {
                var row = rows[i]; var sample = samples[i];
                Real returned = JsonSerializer.Deserialize<Real>(row.GetProperty("returnedExact"), NativeHingeBody.Json);
                Require(row.GetProperty("key").GetString() == sample.Key && row.GetProperty("expectedBits").GetString() == sample.ExpectedBits &&
                    row.GetProperty("exactDecodedBits").GetString() == sample.ExpectedBits && returned.bits == sample.ExpectedBits &&
                    Encode((double)returned) == sample.ExpectedBits, "Native-to-host scalar changed its bits.");
            }
            Require(result.GetProperty("legacyGuidValid").GetBoolean() && result.GetProperty("legacyFreshEffort").GetBoolean() &&
                result.GetProperty("legacyStartExact").GetBoolean() && !result.GetProperty("legacyEndExact").GetBoolean() &&
                result.GetProperty("expectedEndBits").GetString() == Encode(10 * StepMs), "Original decimal-parser cause not confirmed.");
            NativeHingeBody.Write(Path.Combine(output, "wire-preflight-host.json"), new { passed = true, exactRoundTrips = 1040,
                malformedScalarsRejectedEachSide = 10, originalDecimalFailureReproduced = true, physicsExecuted = false });
            Console.WriteLine("Original decimal decoding mismatch reproduced; all exact scalar round trips passed. Proceeding to isolated native cases.");
        }
        finally { if (!process.HasExited) { process.Kill(true); process.WaitForExit(); } }
    }
    private static Sample Make(string key, double value, bool compare) => new(key, compare ? value : 0, value, Encode(value), compare);
}
