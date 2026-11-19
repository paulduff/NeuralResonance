using System.Text.Json;
using NeuralResonanceEngine.Protocol;

internal sealed record ProjectionReceiverEngineObservation(double? StartedThroughMs,long CompletedTick,
    int QueuedEvents,int ReceivingContacts,string CellStateJson,string UndrainedOutputJson);

public sealed partial class StructureEngine
{
    internal int MinimumIsolatedProjectionDelayMs(StructureId source)
    {
        lock(_stateGate)
        {
            RequireIsolatedProjectionRecipient();
            if(source is not (StructureId.VestibularNuclei or StructureId.ReticularFormation or StructureId.SpinalCordMotor))
                throw new ArgumentException("Declare an original vestibular/reticular/spinal source.");
            // ClampDelay enforces this lower bound for either feed-forward or
            // feedback events, regardless of cell, transmitter or spike type.
            // This is a bound on the existing calculation, not an added delay.
            var tract=GetTractDelayWindow(source,_profile.StructureId);
            return Math.Max(0,Math.Min(tract.MinMs,_feedbackDelayWindow.MinMs));
        }
    }
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
