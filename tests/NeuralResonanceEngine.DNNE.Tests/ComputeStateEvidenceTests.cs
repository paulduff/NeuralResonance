using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ComputeStateEvidenceTests
{
    private static PriorityQueue<int,int> Queue(int direction) => new(Comparer<int>.Create((a,b)=>direction*a.CompareTo(b)));

    [Fact]
    public void DelegateQueueComparerSerializesAndStoredQueueContentsRemainVisible()
    {
        var first=Queue(1);var second=Queue(1);
        first.Enqueue(41,3);second.Enqueue(41,3);
        var state=StateEvidence.Capture(first)!;
        string serialized=JsonSerializer.Serialize(state);
        Assert.Contains("delegate",serialized);Assert.Contains("invocations",serialized);
        Assert.Equal(StateEvidence.Hash(state),StateEvidence.Hash(StateEvidence.Capture(second)!));
        second.Enqueue(42,4);
        Assert.NotEqual(StateEvidence.Hash(state),StateEvidence.Hash(StateEvidence.Capture(second)!));
    }

    [Fact]
    public void ComparatorClosureStateIsPreservedEvenWhenBothQueuesAreEmpty()
    {
        Assert.NotEqual(StateEvidence.Hash(StateEvidence.Capture(Queue(1))!),
            StateEvidence.Hash(StateEvidence.Capture(Queue(-1))!));
    }

    [Fact]
    public void UnknownNativePointersAreRejectedInsteadOfRoundedOrSilentlyOmitted()
    {
        Assert.Throws<NotSupportedException>(()=>StateEvidence.Capture(new IntPtr(123)));
        Assert.Throws<NotSupportedException>(()=>StateEvidence.Capture(new UIntPtr(123)));
    }
}
