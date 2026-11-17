using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

// Prescribed skull measurements, not a native body or neuronal sensory source.
internal static class VestibularMechanicsStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    internal sealed record Change(int EndpointIndex, double[] Values);
    internal sealed record Condition(string Key, string Group, double StepMs, int Intervals,
        long InitialStep, double PhysicalEpochMs, double NeuralEpochMs, Change[] Changes);
    internal sealed record Protocol(string Version, string CandidateVersion, double[] TimeConstantsSeconds,
        int[] DeliveryGroups, Condition[] Cases);

    internal static void Run(string inputPath, string output)
    {
        if (!Path.IsPathFullyQualified(inputPath) || !Path.IsPathFullyQualified(output) ||
            File.Exists(output) || Directory.Exists(output))
            throw new ArgumentException("Require absolute frozen input and new output directory.");
        var spec = JsonSerializer.Deserialize<Protocol>(File.ReadAllText(inputPath), Json);
        Require(spec is not null && spec.Version == "isolated-vestibular-numerics-v1" &&
            spec.CandidateVersion == AvatarClockedVestibularMechanics.Version &&
            spec.TimeConstantsSeconds.SequenceEqual(new[] { AvatarClockedVestibularMechanics.CupulaSeconds,
                AvatarClockedVestibularMechanics.AdaptationSeconds, AvatarClockedVestibularMechanics.OtolithSeconds }) &&
            spec.DeliveryGroups.SequenceEqual(new[] { 1, 17 }) && spec.Cases.Length == 20 &&
            spec.Cases.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() == 20,
            "Unexpected vestibular protocol.");
        Directory.CreateDirectory(output);
        var cases = new List<object>();
        foreach (var condition in spec.Cases)
        {
            Require(condition.Key.Length > 0 && condition.Key.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') &&
                condition.Intervals > 0 && condition.Intervals <= 10000 && condition.Changes.Length > 0 &&
                condition.Changes[0].EndpointIndex == 0 && condition.Changes.All(c =>
                    c.EndpointIndex >= 0 && c.EndpointIndex <= condition.Intervals &&
                    c.Values.Length == 6 && c.Values.All(double.IsFinite)) &&
                condition.Changes.Zip(condition.Changes.Skip(1)).All(p => p.First.EndpointIndex < p.Second.EndpointIndex),
                "Invalid explicit fixture history.");
            var clock = new PhysicalSampleClock(StableNeuralIdentity.Create("vestibular-fixture-v1", condition.Key),
                "isolated-vestibular-body", "prescribed-skull-kinematics", condition.InitialStep,
                condition.StepMs, condition.PhysicalEpochMs, condition.NeuralEpochMs);
            clock.Validate();
            string folder = Path.Combine(output, condition.Key); Directory.CreateDirectory(folder);
            var deliveries = new List<object>();
            string originalHash = null;
            foreach (int group in spec.DeliveryGroups)
            {
                var organ = new AvatarClockedVestibularMechanics(clock);
                string path = Path.Combine(folder, $"delivery-{group}.jsonl");
                int records = 0, retries = 0;
                // Dispose before hashing: completed evidence must have no open writer.
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    for (int start = 0; start <= condition.Intervals; start += group)
                    {
                        for (int index = start; index <= condition.Intervals && index < start + group; index++)
                        {
                            var change = condition.Changes.Last(c => c.EndpointIndex <= index);
                            var v = change.Values;
                            string observationJson = JsonSerializer.Serialize(new
                            {
                                fixture = true, caseKey = condition.Key, endpointIndex = index,
                                values = v, units = "pitch/yaw/roll rad/s; skull right/down/forward m/s^2"
                            }, Json);
                            long step = checked(condition.InitialStep + index);
                            var input = new AvatarVestibularKinematics(step, PhysicalSampleProtocol.Identity(clock, step),
                                PhysicalSampleProtocol.Digest(observationJson), v[0], v[1], v[2], v[3], v[4], v[5]);
                            var receipt = organ.Consume(input);
                            var state = organ.Capture();
                            Require(ReferenceEquals(receipt, organ.Consume(input with { })) &&
                                ReferenceEquals(state, organ.Capture()), "Exact replay changed the completed organ.");
                            retries++;
                            writer.WriteLine(JsonSerializer.Serialize(new
                            {
                                index, observationJson, inputJson = JsonSerializer.Serialize(input), input, receipt
                            }, Json));
                            records++;
                        }
                    }
                }
                string hash;
                using (var stream = File.OpenRead(path)) hash = Convert.ToHexString(SHA256.HashData(stream));
                originalHash ??= hash;
                Require(hash == originalHash && records == condition.Intervals + 1 && retries == records &&
                    !organ.Capture().Faulted && organ.Capture().IntegratedIntervals == condition.Intervals,
                    "Delivery grouping/retries changed the trajectory or record coverage.");
                deliveries.Add(new { group, records, retries, sha256 = hash, finalState = organ.Capture() });
            }
            var result = new { key = condition.Key, clock, condition, deliveries, completed = true,
                deliveryGroupingIdentical = true, prescribedKinematics = true };
            WriteNew(Path.Combine(folder, "result.json"), result);
            cases.Add(result);
            Console.WriteLine($"VESTIBULAR {condition.Key}: both delivery groups captured.");
        }
        WriteNew(Path.Combine(output, "results.json"), new { completed = true, cases,
            candidateVersion = AvatarClockedVestibularMechanics.Version, mechanicalModelExecuted = true,
            neuronalModelExecuted = false, nativeBodyExecuted = false, liveBrainAccessed = false,
            skullTransformationQualified = false, physiologyQualified = false, avatarRecoveryAssessed = false });
    }

    private static void WriteNew(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, value, Json);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
