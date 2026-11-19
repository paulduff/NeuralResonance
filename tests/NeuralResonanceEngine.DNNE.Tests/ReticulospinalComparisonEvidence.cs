using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

internal sealed record ReticulospinalTrial(string Reticular,string Spinal,string Deliveries,
    IReadOnlyList<VestibularProjectionFrame> Inputs,IReadOnlyList<ReticulospinalFrame> Frames);
internal sealed record ReticulospinalTrialAccounting(int OriginalNeuronalEvents,int RoutedEvents,int DeliveredEvents,int WaitingEvents,
    string[] CanonicalDeliveries,string[] CanonicalWaiting);

// Cross-execution comparison may omit only freshly allocated event identifiers.
// Each original identifier and complete payload is still verified within its
// own execution, including any event not yet due at the receiving boundary.
internal static class ReticulospinalComparisonEvidence
{
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static string Hash(string value)=>VestibularProjectionOutbox.Hash(value);

    internal static void Compare(ReticulospinalTrial whole,ReticulospinalTrial partitioned)
    {
        string directory=Environment.GetEnvironmentVariable("NRE_RETICULOSPINAL_EVIDENCE_DIR")??
            Path.Combine(Path.GetTempPath(),"nre-reticulospinal-comparison",Guid.NewGuid().ToString("N"));
        if(!Path.IsPathFullyQualified(directory))throw new ArgumentException("Use an absolute fresh evidence directory.");
        Directory.CreateDirectory(directory);
        void Save(string name,object value)
        {using var stream=new FileStream(Path.Combine(directory,name),FileMode.CreateNew,FileAccess.Write);using var writer=new StreamWriter(stream,new UTF8Encoding(false));writer.Write(Json(value));}
        Save("runs.json",new{Whole=whole,Partitioned=partitioned}); // Preserve both original executions even if accounting fails.
        var left=Reconcile(whole);var right=Reconcile(partitioned);
        Save("comparison.json",new{ReticularEqual=whole.Reticular==partitioned.Reticular,SpinalEqual=whole.Spinal==partitioned.Spinal,
            RawDeliveriesEqual=whole.Deliveries==partitioned.Deliveries,CanonicalDeliveriesEqual=left.CanonicalDeliveries.SequenceEqual(right.CanonicalDeliveries),
            CanonicalWaitingEqual=left.CanonicalWaiting.SequenceEqual(right.CanonicalWaiting),Whole=left,Partitioned=right,
            ExcludedAcrossExecutions=new[]{"OriginalMessageId","OriginalJson.MessageId","waiting MessageJson.MessageId"},
            OriginalIdentifiersVerifiedWithinEachExecution=true,NumericalTolerance=0});
        Assert.Equal(whole.Reticular,partitioned.Reticular);
        Assert.Equal(whole.Spinal,partitioned.Spinal);
        Assert.Equal(left.CanonicalDeliveries,right.CanonicalDeliveries);
        Assert.Equal(left.CanonicalWaiting,right.CanonicalWaiting);
    }

    private static ReticulospinalTrialAccounting Reconcile(ReticulospinalTrial run)
    {
        var sent=new Dictionary<(string Recipient,Guid Id),string>();
        var delivered=new HashSet<(string Recipient,Guid Id)>();var waiting=new HashSet<(string Recipient,Guid Id)>();
        var canonicalDeliveries=new List<string>();var canonicalWaiting=new List<string>();var originalDeliveries=new List<string>();
        int originalNeuronalEvents=0;
        void Packet(VestibularProjectionPacket packet,Dictionary<Guid,string>? originalEvents=null)
        {
            Assert.Equal(Hash(packet.CanonicalJson),packet.Sha256);
            using var doc=JsonDocument.Parse(packet.CanonicalJson);var root=doc.RootElement;
            Assert.Equal(packet.EventCount,root.GetProperty("Events").GetArrayLength());
            foreach(var row in root.GetProperty("Events").EnumerateArray())
            {
                string originalJson=row.GetProperty("OriginalEventJson").GetString()!,routedJson=row.GetProperty("RoutedEventJson").GetString()!;
                var original=JsonSerializer.Deserialize<SpikeMessage>(originalJson)!;var routed=JsonSerializer.Deserialize<SpikeMessage>(routedJson)!;
                Assert.NotEqual(Guid.Empty,original.MessageId);Assert.NotEqual(Guid.Empty,routed.MessageId);
                Assert.Equal(StableNeuralIdentity.Create(VestibularProjectionOutbox.Version,original.MessageId,row.GetProperty("ProjectionId").GetGuid(),packet.TargetInstance),routed.MessageId);
                if(originalEvents is not null)Assert.Equal(originalEvents[original.MessageId],originalJson);
                Assert.True(sent.TryAdd((packet.TargetInstance,routed.MessageId),routedJson));
            }
        }
        foreach(var frame in run.Inputs)foreach(var packet in frame.Packets)Packet(packet);
        foreach(var frame in run.Frames)
        {
            Assert.Equal(Hash(frame.CanonicalJson),frame.Sha256);
            using var doc=JsonDocument.Parse(frame.CanonicalJson);
            foreach(var row in doc.RootElement.GetProperty("Transfers").EnumerateArray())
            {
                var original=JsonSerializer.Deserialize<VestibularRecipientFrame>(row.GetProperty("Original").GetRawText())!;
                var projection=JsonSerializer.Deserialize<VestibularProjectionFrame>(row.GetProperty("Projection").GetRawText())!;
                Assert.Equal(Hash(original.CanonicalJson),original.Sha256);Assert.Equal(original.Sha256,projection.SourceFrameSha256);
                using var source=JsonDocument.Parse(original.CanonicalJson);var root=source.RootElement;
                var events=root.GetProperty("ReceiverStepJson").EnumerateArray().SelectMany(step=>
                    JsonSerializer.Deserialize<StructureStepResult>(step.GetString()!)!.OutboundSpikes).ToDictionary(e=>e.MessageId,Json);
                originalNeuronalEvents+=events.Count;
                Assert.Equal(events.Count,Assert.Single(projection.Packets).EventCount);
                foreach(var packet in projection.Packets)Packet(packet,events);
            }
        }
        foreach(var frame in run.Frames)
        {
            using var doc=JsonDocument.Parse(frame.CanonicalJson);
            foreach(var row in doc.RootElement.GetProperty("Transfers").EnumerateArray())
            {
                using var source=JsonDocument.Parse(row.GetProperty("Original").GetProperty("CanonicalJson").GetString()!);var root=source.RootElement;
                string recipient=root.GetProperty("SourceInstance").GetString()!;
                foreach(var item in root.GetProperty("ActualDeliveries").EnumerateArray())
                {
                    var delivery=JsonSerializer.Deserialize<RubralInputDelivery>(item.GetRawText())!;var key=(recipient,delivery.OriginalMessageId);
                    Assert.True(delivered.Add(key));Assert.Equal(sent[key],delivery.OriginalJson);Assert.Equal(delivery.ArrivalAtMs,delivery.AppliedAtMs);
                    var spike=JsonSerializer.Deserialize<SpikeMessage>(delivery.OriginalJson)!;Assert.Equal(delivery.OriginalMessageId,spike.MessageId);
                    originalDeliveries.Add(item.GetRawText());spike.MessageId=Guid.Empty;
                    canonicalDeliveries.Add(Json(new{Recipient=recipient,Delivery=delivery with{OriginalMessageId=Guid.Empty,OriginalJson=Json(spike)}}));
                }
            }
        }
        using(var last=JsonDocument.Parse(run.Frames[^1].CanonicalJson))foreach(string name in new[]{"Reticular","Spinal"})
        {
            var state=last.RootElement.GetProperty(name).GetProperty("Source");
            Assert.Equal(0,state.GetProperty("Actual").GetProperty("QueuedEvents").GetInt32());
            using var retained=JsonDocument.Parse(state.GetProperty("RetainedEvidenceJson").GetString()!);var root=retained.RootElement;
            string recipient=root.GetProperty("SourceInstance").GetString()!;
            foreach(var row in root.GetProperty("WaitingEvents").EnumerateArray())
            {
                string json=row.GetProperty("MessageJson").GetString()!;var spike=JsonSerializer.Deserialize<SpikeMessage>(json)!;var key=(recipient,spike.MessageId);
                Assert.Equal(sent[key],json);Assert.True(waiting.Add(key));Assert.DoesNotContain(key,delivered);spike.MessageId=Guid.Empty;
                canonicalWaiting.Add(Json(new{Recipient=recipient,MessageJson=Json(spike),Arrival=row.GetProperty("Arrival").GetDouble()}));
            }
        }
        Assert.Equal(sent.Count,delivered.Count+waiting.Count);Assert.Equal(run.Deliveries,Json(originalDeliveries.Order(StringComparer.Ordinal)));
        return new(originalNeuronalEvents,sent.Count,delivered.Count,waiting.Count,
            canonicalDeliveries.Order(StringComparer.Ordinal).ToArray(),canonicalWaiting.Order(StringComparer.Ordinal).ToArray());
    }
}
