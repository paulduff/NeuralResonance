using System.Text.Json;
using NeuralResonanceEngine.Protocol;

internal sealed record ProjectionReceiverEngineObservation(double? StartedThroughMs,long CompletedTick,
    int QueuedEvents,int ReceivingContacts,string CellStateJson,string UndrainedOutputJson);

public sealed partial class StructureEngine
{
    // Read-only access to the existing delay calculation and actual failed-step
    // state. These methods neither enqueue a spike nor advance/repair a cell.
    internal double DescribeIsolatedProjectionArrival(SpikeMessage message)
    {
        lock(_stateGate)
        {
            RequireIsolatedProjectionRecipient();
            if(message.TargetStructure!=_profile.StructureId)throw new ArgumentException("Foreign projection recipient.");
            double arrival=message.TimestampMs+ComputeConductionDelayMs(message);
            if(!double.IsFinite(arrival)||arrival<message.TimestampMs)throw new ArgumentException("Invalid original conduction time.");
            return arrival;
        }
    }
    internal ProjectionReceiverEngineObservation ObserveIsolatedProjectionRecipient()
    {
        lock(_stateGate)
        {
            RequireIsolatedProjectionRecipient();
            return new(double.IsFinite(_lastBiologicalTimestampMs)?_lastBiologicalTimestampMs:null,_lastProcessedTick,
                _feedForward.Count+_feedback.Count,_inboundSynapses.Count,
                JsonSerializer.Serialize(_neurons.Select(n=>new{n.Id,n.MembranePotentialMillivolts,n.NetSynapticCurrent,
                    n.FiringRateHz,n.ActivityTrace,Receptors=n.CaptureCorticalReceptors()})),JsonSerializer.Serialize(_outbound.ToArray()));
        }
    }
    private void RequireIsolatedProjectionRecipient()
    {
        if(!_instanceKey.StartsWith("isolated-",StringComparison.Ordinal)||
            _profile.StructureId is not (StructureId.ReticularFormation or StructureId.SpinalCordMotor))
            throw new InvalidOperationException("This observer is restricted to isolated reticular/spinal receiving populations.");
    }
}
