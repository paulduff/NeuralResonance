#nullable enable
using System.Globalization;
using System.Text;
using System.Text.Json;
using NRE.SimAvatar;

internal static class CoupledCanalStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed record Phase(long EndTick, long BaseStepTicks, double[] Input);
    internal sealed record Condition(string Key, string Regime, string ConditionName, double[] Initial, Phase[] Phases, long DurationTicks);

    internal static void Run(string input, string output)
    {
        if (!Path.IsPathFullyQualified(input) || !Path.IsPathFullyQualified(output) || Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Require frozen absolute protocol and new output directory.");
        using var document = JsonDocument.Parse(File.ReadAllText(input)); var root = document.RootElement;
        if (root.GetProperty("version").GetString() != "published-coupled-canal-trajectory-comparison-v1" ||
            root.GetProperty("candidateVersion").GetString() != AvatarCoupledCanalDynamics.Version ||
            root.GetProperty("ticksPerSecond").GetInt64() != 67108864 || root.GetProperty("expectedCases").GetInt32() != 15 ||
            root.GetProperty("expectedNativeRuns").GetInt32() != 45)
            throw new ArgumentException("Unexpected coupled canal protocol.");
        long ticksPerSecond = root.GetProperty("ticksPerSecond").GetInt64();
        int[] refinements = root.GetProperty("refinements").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        if (!refinements.SequenceEqual(new[] { 1, 2, 4 })) throw new ArgumentException("Require the declared refinement sequence.");
        var conditions = root.GetProperty("cases").EnumerateArray().Select(x => new Condition(
            x.GetProperty("key").GetString()!, x.GetProperty("regime").GetString()!, x.GetProperty("condition").GetString()!,
            x.GetProperty("initial").EnumerateArray().Select(v => v.GetDouble()).ToArray(),
            x.GetProperty("phases").EnumerateArray().Select(p => new Phase(p.GetProperty("endTick").GetInt64(),
                p.GetProperty("baseStepTicks").GetInt64(), p.GetProperty("input").EnumerateArray().Select(v => v.GetDouble()).ToArray())).ToArray(),
            x.GetProperty("durationTicks").GetInt64())).ToArray();
        if (conditions.Length != 15 || conditions.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != 15)
            throw new ArgumentException("Incomplete case set.");
        foreach (var c in conditions)
        {
            if (string.IsNullOrEmpty(c.Key) || c.Key.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-') ||
                c.Initial.Length != 12 || !c.Initial.All(double.IsFinite) || c.Phases.Length == 0)
                throw new ArgumentException("Invalid mechanical fixture.");
            long previous = 0;
            foreach (var p in c.Phases)
            {
                if (p.EndTick <= previous || p.BaseStepTicks <= 0 || p.BaseStepTicks % 4 != 0 ||
                    (p.EndTick - previous) % p.BaseStepTicks != 0 || p.Input.Length != 3 || !p.Input.All(double.IsFinite))
                    throw new ArgumentException("Invalid phase interval, refinement or acceleration.");
                previous = p.EndTick;
            }
            if (previous != c.DurationTicks) throw new ArgumentException("Incomplete interval coverage.");
        }
        Directory.CreateDirectory(output);
        Write(Path.Combine(output, "input.json"), root);
        var parameters = AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
        Write(Path.Combine(output, "parameters.json"), parameters);
        var reports = new List<object>(); bool complete = true;
        foreach (var c in conditions)
        foreach (int refinement in refinements)
        {
            string key = c.Key + "-r" + refinement;
            Console.WriteLine("PUBLISHED CANAL " + key);
            string folder = Path.Combine(output, key); Directory.CreateDirectory(folder);
            var state = new AvatarCanalMechanicalPoint(Vector(c.Initial[..6]), Vector(c.Initial[6..]));
            long tick = 0, steps = 0; string? error = null;
            var solvers = c.Phases.Select(p => p.BaseStepTicks / refinement).Distinct().ToDictionary(t => t,
                t => new AvatarCoupledCanalDynamics(parameters, (double)t / ticksPerSecond));
            Write(Path.Combine(folder, "initial.json"), new { key, refinement, condition = c, tick, state,
                parameters.Fingerprint, solvers = solvers.Select(p => new { stepTicks = p.Key, p.Value.StepSeconds, p.Value.Fingerprint }) });
            try
            {
                using var stream = new FileStream(Path.Combine(folder, "trajectory.csv"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.WriteLine("tick,step,phase,stepTicks," + string.Join(',', Enumerable.Range(0, 12).Select(i => "state" + i)) +
                    "," + string.Join(',', Enumerable.Range(0, 18).Select(i => "stage1_" + i)) +
                    "," + string.Join(',', Enumerable.Range(0, 18).Select(i => "stage2_" + i)) +
                    ",energyBefore,energyAfter,inputWork,physicalDamping,numericalDamping,energyResidual,backwardError");
                for (int phase = 0; phase < c.Phases.Length; phase++)
                {
                    var p = c.Phases[phase]; long dt = p.BaseStepTicks / refinement;
                    var solver = solvers[dt]; var acceleration = new AvatarCanalPrimeAcceleration(p.Input[0], p.Input[1], p.Input[2]);
                    while (tick < p.EndTick)
                    {
                        if (tick + dt > p.EndTick) throw new InvalidOperationException("Step crosses a declared input boundary.");
                        var result = solver.AdvanceHeld(state, acceleration);
                        double[] row = [.. result.After.VolumeMeters3.ToArray(), .. result.After.FlowMeters3PerSecond.ToArray(),
                            .. result.FirstStage.Point.VolumeMeters3.ToArray(), .. result.FirstStage.Point.FlowMeters3PerSecond.ToArray(), .. result.FirstStage.AccelerationMeters3PerSecond2.ToArray(),
                            .. result.LastStage.Point.VolumeMeters3.ToArray(), .. result.LastStage.Point.FlowMeters3PerSecond.ToArray(), .. result.LastStage.AccelerationMeters3PerSecond2.ToArray(),
                            result.EnergyBeforeJoules, result.EnergyAfterJoules, result.InputWorkJoules, result.PhysicalDampingJoules,
                            result.NumericalDampingJoules, result.EnergyIdentityResidualJoules, result.MaximumStageBackwardError];
                        if (!row.All(double.IsFinite)) throw new ArithmeticException("Nonfinite mechanical record.");
                        tick += dt; steps++;
                        writer.WriteLine(string.Join(',', new[] { tick.ToString(), steps.ToString(), phase.ToString(), dt.ToString() }
                            .Concat(row.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))));
                        state = result.After;
                    }
                }
            }
            catch (Exception exception) { error = exception.ToString(); complete = false; Console.WriteLine(error); }
            var report = new { key, refinement, completed = error is null && tick == c.DurationTicks, tick, steps, state, error };
            Write(Path.Combine(folder, "final.json"), report); reports.Add(report);
        }
        Write(Path.Combine(output, "results.json"), new { completed = complete, runs = reports, sourceToSkullFrameQualified = false,
            localBundleMapQualified = false, liveBrainAccessed = false, nativeBodyExecuted = false, physiologyQualified = false, avatarRecoveryAssessed = false });
        if (!complete || reports.Count != 45) throw new InvalidOperationException("Canal capture failed; all partial prefixes retained.");
    }
    private static AvatarCanalVector Vector(double[] x) => new(x[0], x[1], x[2], x[3], x[4], x[5]);
    private static void Write(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, Json);
    }
}
