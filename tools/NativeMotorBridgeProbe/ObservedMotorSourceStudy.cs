using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NRE.SimAvatar;

// Execute only inside a frozen visible PowerShell study. This qualifies the
// new adapter against completed source traces; it starts no native player.
internal static class ObservedMotorSourceStudy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    private sealed record Condition(string Key, bool Stimulated, int PacketSamples, bool Retry);

    internal static void Run(string reference, string output)
    {
        if (!Path.IsPathFullyQualified(reference) || !Directory.Exists(reference) ||
            !Path.IsPathFullyQualified(output) || Directory.Exists(output))
            throw new ArgumentException("Supply an absolute qualified source reference and a fresh output folder.");
        using var input = JsonDocument.Parse(File.ReadAllText(Path.Combine(reference, "boundary-input.json")));
        using var qualification = JsonDocument.Parse(File.ReadAllText(Path.Combine(reference, "boundary-analysis.json")));
        var p = input.RootElement; var q = qualification.RootElement;
        Require(p.GetProperty("version").GetString() == "motoneuron-boundary-comparison-v1" &&
            p.GetProperty("durationMs").GetDouble() == 6000 && p.GetProperty("sampleMs").GetDouble() == .1 &&
            p.GetProperty("initialVoltageMv").GetDouble() == -65 && p.GetProperty("thresholdMv").GetDouble() == MotoneuronReferenceCell.SpikeThresholdMv &&
            p.GetProperty("stateAccumulation").GetString() == "Compensated" && p.GetProperty("cell").GetString() == "FivecomptcMG_0" &&
            p.GetProperty("sourceRevision").GetString() == "0d11354490fad5fcd940d016a8d69b000693fd81" &&
            p.GetProperty("stepsMs").EnumerateArray().Select(v => v.GetDouble()).SequenceEqual(new[] { .0125, .00625, .003125 }), "Unqualified source protocol.");
        foreach (string key in new[] { "completed", "numericalCriteriaPassed", "originalControlBytesReproduced", "originalAnalysisReproduced" })
            Require(q.GetProperty(key).GetBoolean(), "Source baseline requirement failed: " + key);
        Require(!q.GetProperty("originalBoundsChanged").GetBoolean(), "Source bounds changed.");
        foreach (string source in new[] { "quiet", "soma-6000" })
        {
            var scenario = p.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("key").GetString() == source);
            var actual = scenario.GetProperty("segments").EnumerateArray().Select(s => new[] {
                s.GetProperty("untilMs").GetDouble(),s.GetProperty("somaCurrentPa").GetDouble(),
                s.GetProperty("dendriteExcitatoryNs").GetDouble(),s.GetProperty("dendriteInhibitoryNs").GetDouble() }).ToArray();
            double[][] expected = source == "quiet" ? [[6000,0,0,0]] : [[1000,0,0,0],[3000,6000,0,0],[6000,0,0,0]];
            Require(scenario.GetProperty("block").GetString() == "None" && actual.Length == expected.Length &&
                actual.Zip(expected).All(pair => pair.First.SequenceEqual(pair.Second)), "Changed source input.");
        }
        using var sourceResults = JsonDocument.Parse(File.ReadAllText(Path.Combine(reference, "source-results.json")));
        Require(sourceResults.RootElement.GetProperty("completed").GetBoolean(), "Incomplete source baseline.");
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(output); Guid evidence = Guid.NewGuid();
        var identity = new ObservedMotorIdentity("isolated-native-motor-source", "FivecomptcMG_0",
            StableNeuralIdentity.Create("observed-native-motor-projection", "FivecomptcMG_0"),
            StableNeuralIdentity.Create("observed-native-motor-synapse", "FivecomptcMG_0"));
        var contact = MotorAxonContact.Create(identity.Instance, identity.Neuron, identity.Projection, "isolated-slow-unit", 2.0625);
        var anatomy = new MotorAxonAnatomy([contact]);
        Condition[] conditions = [new("quiet-10",false,100,false),new("active-1",true,10,false),
            new("active-10",true,100,false),new("active-50",true,500,false),new("active-retry-10",true,100,true)];
        Save(Path.Combine(output,"protocol.json"), new { version=ObservedMotorSource.Version,evidence,identity,contact,
            sourceParameters=MotoneuronReferenceParameters.CatLowThreshold,sourceIntegration=MotoneuronReferenceCell.CompensatedVersion,
            sourceSampleMs=ObservedMotorSource.SampleMs,sourceMaximumStepMs=ObservedMotorSource.MaximumIntegrationStepMs,
            durationMs=ObservedMotorSource.DurationMs,conditions,neuronalModelExecuted=true,
            nativePhysicsExecuted=false,muscleDynamicsExecuted=false,physiologyQualified=false });
        var outcomes = new List<object>(); int success = 0;
        ObservedMotorEmission[] activeEmissions = null;
        foreach (var condition in conditions)
        {
            Console.WriteLine("OBSERVED MOTOR SOURCE " + condition.Key);
            string folder = Path.Combine(output,condition.Key); Directory.CreateDirectory(folder);
            var clock = new MotorAxonClock(Guid.NewGuid(),0,0);
            var lane = new ObservedMotorLane(evidence,identity,condition.Stimulated,anatomy,clock,maximumBatches:1);
            var receiver = new AvatarMotorAxonReceiver(anatomy,clock);
            int published=0,retried=0,blocked=0,emissionCursor=0;
            var delivered = new List<AvatarMotorAxonArrival>();
            try
            {
                using (var trace = new StreamWriter(Path.Combine(folder,"source.csv")))
                using (var packets = new StreamWriter(Path.Combine(folder,"packets.jsonl")))
                using (var emissions = new StreamWriter(Path.Combine(folder,"emissions.jsonl")))
                using (var arrivals = new StreamWriter(Path.Combine(folder,"arrivals.jsonl")))
                {
                    trace.WriteLine("timeMs,"+string.Join(',',Enumerable.Range(0,30).Select(i=>"state"+i)));
                    void Sample(ObservedMotorSnapshot s) => trace.WriteLine(string.Join(',',new[]{s.TimeMs}.Concat(s.State).Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
                    Sample(lane.CaptureSource());
                    for (int sample=1;sample<=ObservedMotorSource.Samples;++sample)
                    {
                        var state=lane.AdvanceOneSample(); Sample(state);
                        foreach (var emission in state.Emissions.Skip(emissionCursor)) emissions.WriteLine(JsonSerializer.Serialize(emission,Compact));
                        emissionCursor=state.Emissions.Count;
                        if (sample%condition.PacketSamples!=0) continue;
                        var before=JsonSerializer.Serialize(state,Compact);
                        var batch=lane.PublishCompleted(); ++published;
                        Require(before==JsonSerializer.Serialize(lane.CaptureSource(),Compact),"Publication changed source state.");
                        if (condition.Retry && published%7==0)
                        {
                            bool rejected=false;
                            try { lane.AdvanceOneSample(); } catch (InvalidOperationException) { rejected=true; }
                            Require(rejected && before==JsonSerializer.Serialize(lane.CaptureSource(),Compact),"Backpressure advanced the source."); ++blocked;
                        }
                        var receipt=receiver.AcceptWithAcknowledgement(batch);
                        packets.WriteLine(JsonSerializer.Serialize(new {action="accept",batch,receipt},Compact));
                        if (condition.Retry && published%7==0)
                        {
                            var retained=lane.Unacknowledged(); Require(retained.Count==1 && ReferenceEquals(batch,retained[0]),"Retried packet was regenerated.");
                            var again=receiver.AcceptWithAcknowledgement(retained[0]);
                            Require(again==receipt && before==JsonSerializer.Serialize(lane.CaptureSource(),Compact),"Retry changed original source state or receipt.");
                            packets.WriteLine(JsonSerializer.Serialize(new {action="retry",batch=retained[0],receipt=again},Compact)); ++retried;
                        }
                        lane.Acknowledge(receipt);
                        Require(before==JsonSerializer.Serialize(lane.CaptureSource(),Compact),"Acknowledgement changed source state.");
                        foreach (var arrival in receiver.AdvanceTo(state.TimeMs)) { arrivals.WriteLine(JsonSerializer.Serialize(arrival,Compact)); delivered.Add(arrival); }
                    }
                }
                var final=lane.CaptureSource(); string name=final.Source+"-h0.003125";
                var expected=sourceResults.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("key").GetString()==name);
                Require(expected.GetProperty("completed").GetBoolean() && !final.Faulted && final.TimeMs==6000 && final.CompletedSamples==60000 &&
                    final.IntegrationCalls==expected.GetProperty("calls").GetInt64() && final.SuppliedDurationUnits==expected.GetProperty("suppliedUnits").GetString() &&
                    final.CrossingsMs.SequenceEqual(expected.GetProperty("crossingsMs").EnumerateArray().Select(v=>v.GetDouble())) &&
                    Digest(Path.Combine(folder,"source.csv"))==Digest(Path.Combine(reference,name+".csv")),"Adapter changed qualified source trajectory, crossings or duration.");
                Require(final.CrossingsMs.SequenceEqual(final.Emissions.Select(e=>e.FiredAtNeuralMs)) &&
                    final.Emissions.Select(e=>e.MessageId).Distinct().Count()==final.Emissions.Count &&
                    final.Emissions.Select(e=>e.Ordinal).SequenceEqual(Enumerable.Range(1,final.Emissions.Count).Select(i=>(long)i)),"Source events lost identity or crossing order.");
                var due=final.Emissions.Where(e=>clock.ToBodyMs(e.FiredAtNeuralMs)+contact.ConductionDelayMs<=6000).ToArray();
                var exactArrivals=due.Select(e=>new AvatarMotorAxonArrival(clock.SessionId,e.SourceInstanceKey,e.MessageId,e.Ordinal,
                    e.SourceNeuronId,e.ProjectionId,e.SourceSynapseId,contact.ContactId,contact.MotorUnitId,e.FiredAtNeuralMs,
                    clock.ToBodyMs(e.FiredAtNeuralMs)+contact.ConductionDelayMs)).ToArray();
                Require(delivered.SequenceEqual(exactArrivals),"Delivery changed original event identity, ordering or timing.");
                Require(receiver.AcceptedEvents==final.Emissions.Count && receiver.DeliveredEvents==delivered.Count &&
                    receiver.PendingEvents==final.Emissions.Count-due.Length && receiver.BodyTimeMs==6000 && receiver.CompleteThroughBodyMs==6000 &&
                    lane.UnpublishedEmissions==0 && lane.Unacknowledged().Count==0,"Incomplete source event accounting.");
                Require(published==ObservedMotorSource.Samples/condition.PacketSamples &&
                    retried==(condition.Retry ? published/7 : 0) && blocked==retried,"Incomplete publication or retry matrix.");
                if (condition.Stimulated)
                {
                    if (activeEmissions is null) activeEmissions=final.Emissions.ToArray();
                    else Require(final.Emissions.SequenceEqual(activeEmissions),"Packet cadence changed original source event identities.");
                }
                Save(Path.Combine(folder,"final.json"),new{source=final,clock,published,retried,blocked,delivered=delivered.Count,receiver.PendingEvents});
                outcomes.Add(new{condition=condition.Key,passed=true,sourceBytesReproduced=true,exactArrivalIdentityAndTiming=true,
                    crossings=final.Emissions.Count,published,retried,blocked,delivered=delivered.Count}); ++success;
            }
            catch (Exception error)
            {
                Save(Path.Combine(folder,"failure.json"),new{error=error.ToString(),source=lane.CaptureSource(),retained=lane.Unacknowledged()});
                outcomes.Add(new{condition=condition.Key,passed=false,error=error.ToString()}); Console.WriteLine(error);
            }
        }
        Save(Path.Combine(output,"results.json"),new{completed=true,sourceAdapterCriteriaPassed=success==5,successfulCases=success,expectedCases=5,cases=outcomes,
            neuronalModelExecuted=true,nativePhysicsExecuted=false,muscleDynamicsExecuted=false,
            liveMotorAuthorityQualified=false,physiologyQualified=false,avatarRecoveryAssessed=false});
        Require(success==5,"Observed-source adapter qualification failed; preserve evidence.");
    }

    private static string Digest(string path) { using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void Save(string path,object value) { using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,value,Json); }
    private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
}
