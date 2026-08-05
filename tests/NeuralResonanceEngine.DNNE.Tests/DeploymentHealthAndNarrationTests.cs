using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DeploymentHealthAndNarrationTests
{
    [Fact]
    public void Aggregate_Prefers_Healthy_Instance_Over_Absent_Replica()
    {
        var healthy = Telemetry(status: "OK", attempts: 14, successes: 12, failures: 0, updatedAt: 200);
        var absentReplica = Telemetry(status: "BACKOFF", attempts: 20, successes: 0, failures: 8, updatedAt: 210);

        var aggregate = ServiceTelemetryAggregation.Aggregate([healthy, absentReplica]);

        Assert.Equal("OK", aggregate.LastStatus);
        Assert.Equal(12, aggregate.SuccessCount);
    }

    [Fact]
    public void Aggregate_Marks_Undiscovered_Structure_As_Absent()
    {
        var left = Telemetry(status: "BACKOFF", attempts: 8, successes: 0, failures: 8, updatedAt: 200);
        var right = Telemetry(status: "DEGRADED", attempts: 11, successes: 0, failures: 8, updatedAt: 210);

        var aggregate = ServiceTelemetryAggregation.Aggregate([left, right]);

        Assert.Equal(ServiceTelemetryAggregation.AbsentStatus, aggregate.LastStatus);
        Assert.True(ServiceTelemetryAggregation.IsAbsent(aggregate));
    }

    [Fact]
    public void Health_Counts_Exclude_Intentionally_Absent_Structures()
    {
        var state = new global::SimulationState();
        state.Configure(
            1.0,
            new Dictionary<StructureId, string>
            {
                [StructureId.M1] = "http://localhost:52190",
                [StructureId.V1] = "http://localhost:52225"
            },
            new Dictionary<StructureId, List<SynapticConnection>>());
        state.UpdateServiceTelemetry(StructureId.M1, Telemetry("OK", 12, 10, 0, 200));
        state.UpdateServiceTelemetry(
            StructureId.V1,
            ServiceTelemetryAggregation.Aggregate(
            [
                Telemetry("BACKOFF", 8, 0, 8, 200),
                Telemetry("DEGRADED", 9, 0, 8, 210)
            ]));

        var counts = state.GetServiceHealthCounts();

        Assert.Equal(1, counts.TotalServices);
        Assert.Equal(0, counts.NonOkServices);
    }

    [Fact]
    public void Validation_Reports_Deployed_Count_And_Allows_Axonal_Fanout()
    {
        var state = new global::SimulationState();
        state.Configure(
            1.0,
            new Dictionary<StructureId, string>
            {
                [StructureId.M1] = "http://localhost:52190",
                [StructureId.V1] = "http://localhost:52225"
            },
            new Dictionary<StructureId, List<SynapticConnection>>());
        state.UpdateServiceTelemetry(StructureId.M1, Telemetry("OK", 12, 10, 0, 200));
        state.UpdateServiceTelemetry(
            StructureId.V1,
            ServiceTelemetryAggregation.Aggregate(
            [
                Telemetry("BACKOFF", 8, 0, 8, 200),
                Telemetry("DEGRADED", 9, 0, 8, 210)
            ]));
        state.UpdateTransportStats(TransportRuntimeStats.Empty with
        {
            GeneratedSpikes = 2,
            RoutedSpikes = 6,
            DeliveredSpikes = 6,
            DispatchQueueFlushedBatches = 3
        });

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(state.GetValidationSnapshot()));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("ServiceCount").GetInt32());
        Assert.True(root.GetProperty("Checks").GetProperty("pipelineMonotonic").GetBoolean());
    }

    [Fact]
    public void Narration_Labels_A_Stale_Action_As_A_Transition()
    {
        var actionNeed = global::SimulationState.ResolveNarrativeActionNeed("goal.FindFood", "FindShelter");

        var narration = global::SimulationState.BuildNarrativeSelfStatement(
            "comfortable",
            "shelter",
            "goal.FindFood",
            actionNeed,
            "I remember food at object.food_2");

        Assert.Equal("food", actionNeed);
        Assert.Contains("I need shelter", narration, StringComparison.Ordinal);
        Assert.Contains("my current plan is still", narration, StringComparison.Ordinal);
        Assert.Contains("because I remember food", narration, StringComparison.Ordinal);
    }

    private static ServiceRuntimeTelemetry Telemetry(
        string status,
        int attempts,
        int successes,
        int failures,
        double updatedAt)
        => new(
            LastAckLatencyMs: successes > 0 ? 4 : 0,
            AckLatencyEwmaMs: successes > 0 ? 5 : 0,
            ConsecutiveFailures: failures,
            AttemptCount: attempts,
            SuccessCount: successes,
            TimeoutFailureCount: failures,
            NextRetryTimestampMs: status == "BACKOFF" ? updatedAt + 5_000 : 0,
            LastStatus: status,
            LastError: failures > 0 ? "timeout" : string.Empty,
            LastTickProcessed: 100,
            LastUpdateTimestampMs: updatedAt,
            LatencyLt100MsCount: successes,
            Latency100To250MsCount: 0,
            Latency250To500MsCount: 0,
            Latency500To1000MsCount: 0,
            LatencyGte1000MsCount: 0);
}
