using System.Diagnostics;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

internal static class MotorPlantStudy
{
    internal static object Run(string root)
    {
        Directory.CreateDirectory(root);
        var json = new JsonSerializerOptions { WriteIndented = true };
        void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, json));
        Save(Path.Combine(root, "protocol.json"), new { unitCounts = new[] { 1, 32, 256 }, durationMs = 1000,
            frameMs = 20, pairs = 3, stepMs = .125, arrivalPattern = "one event per unit every 40 ms, with fractional conduction delay",
            inputIsFixture = true, neuronalSourceQualified = false, body = "fixed-length isolated motor-unit plant",
            networkTransportMeasured = false, unityPhysicsMeasured = false });
        var results = new List<MotorRun>();
        foreach (int count in new[] { 1, 32, 256 })
        {
            var clock = new MotorAxonClock(StableNeuralIdentity.Create("compute-motor-session", count), 0, 0);
            var contacts = Enumerable.Range(0, count).Select(i => MotorAxonContact.Create("isolated-spinal-compute",
                "cell-" + i, StableNeuralIdentity.Create("compute-motor-projection", i), "unit-" + i, .03125)).ToArray();
            var anatomy = new MotorAxonAnatomy(contacts);
            var parameters = contacts.ToDictionary(c => c.MotorUnitId, _ => MotorUnitCalciumParameters.NagamoriSlowUnit1);
            var inputs = Enumerable.Range(0, 50).Select(frame => frame % 2 != 0 ? Array.Empty<SpikeMessage>() : contacts.Select((contact, i) =>
                new SpikeMessage { MessageId = StableNeuralIdentity.Create("compute-motor-event", count, frame, i),
                    TimestampMs = frame * 20 + 1 + (i % 4) * .03125, SourceStructure = StructureId.SpinalCordMotor,
                    SourceInstanceKey = contact.SourceInstanceKey, SourceNeuronId = contact.SourceNeuronId,
                    ProjectionId = contact.ProjectionId, SynapseId = StableNeuralIdentity.Create("compute-motor-release", i),
                    Neurotransmitter = NTEnum.ACETYLCHOLINE }).OrderBy(s => s.TimestampMs).ThenBy(s => s.SourceNeuronId, StringComparer.Ordinal).ToArray()).ToArray();
            Save(Path.Combine(root, $"inputs-{count}.json"), inputs);
            for (int pair = -1; pair < 3; ++pair)
            foreach (bool wire in pair < 0 ? new[] { true } : pair % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                string name = pair < 0 ? "warmup" : $"pair-{pair}-{(wire ? "json" : "direct")}";
                Console.WriteLine($"motor units {count} / {name}");
                string directory = Path.Combine(root, count + "-" + name); Directory.CreateDirectory(directory);
                var publisher = new MotorAxonPublisher(anatomy, clock, "isolated-spinal-compute");
                var plant = new AvatarIsometricMotorUnitPlant(anatomy, clock, parameters);
                double publishMs = 0, wireMs = 0, receiveMs = 0, advanceMs = 0; long wireBytes = 0;
                var outputs = new List<IsometricMotorPlantStep>();
                for (int frame = 0; frame < 50; ++frame)
                {
                    long start = Stopwatch.GetTimestamp();
                    var batch = publisher.Publish(inputs[frame], (frame + 1) * 20);
                    publishMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    if (wire)
                    {
                        start = Stopwatch.GetTimestamp();
                        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(batch);
                        batch = JsonSerializer.Deserialize<MotorAxonBatch>(bytes) ?? throw new InvalidOperationException("Missing decoded motor batch.");
                        wireMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds; wireBytes += bytes.Length;
                    }
                    start = Stopwatch.GetTimestamp(); plant.Accept(batch); receiveMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    start = Stopwatch.GetTimestamp(); var result = plant.AdvanceToTick((frame + 1) * 160);
                    advanceMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds; outputs.Add(result);
                }
                var final = plant.Capture();
                if (final.Faulted || final.PendingEvents != 0 || final.DeliveredEvents != 25L * count ||
                    final.AcceptedEvents != final.DeliveredEvents || final.Units.Any(u => u.Arrivals != 25 || !double.IsFinite(u.ForceNewtons)))
                    throw new InvalidOperationException("Motor fixture delivery/accounting incomplete.");
                var state = StateEvidence.Capture(plant)!;
                File.WriteAllBytes(Path.Combine(directory, "frames.json"), JsonSerializer.SerializeToUtf8Bytes(outputs));
                File.WriteAllBytes(Path.Combine(directory, "state-evidence.json"), JsonSerializer.SerializeToUtf8Bytes(state));
                var run = new MotorRun(count, name, pair, wire, publishMs, wireMs, receiveMs, advanceMs, wireBytes,
                    StateEvidence.Hash(outputs), StateEvidence.Hash(state));
                results.Add(run); Save(Path.Combine(directory, "timing.json"), run);
                var contrast = results.Where(r => r.Units == count && r.Pair == pair).ToArray();
                if (pair >= 0 && contrast.Length == 2 &&
                    (contrast[0].OutputsSha256 != contrast[1].OutputsSha256 || contrast[0].StateSha256 != contrast[1].StateSha256))
                    throw new InvalidOperationException("Serialization changed motor-unit state/events.");
            }
        }
        return new { runs = results, exactSerializationPairs = 9 };
    }
    private sealed record MotorRun(int Units, string Run, int Pair, bool JsonRoundTrip,
        double PublishMs, double SerializationMs, double ReceiveMs, double AdvanceMs, long SerializedBytes,
        string OutputsSha256, string StateSha256);
}
