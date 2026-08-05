using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.ControlProgram.Services;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ContinuousNavigationBenchmarkTests
{
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(2, -1, 0)]
    [InlineData(3, 0, -1)]
    public void EveryWorldUsesTheSameNavigationCoordinateFrame(int quarter, int expectedRowDelta, int expectedColumnDelta)
    {
        Assert.Equal((expectedRowDelta, expectedColumnDelta), NavigationCoordinateFrame.DirectionDelta(quarter));
        Assert.Equal(quarter, NavigationCoordinateFrame.HeadingQuarterFromDegrees(quarter * 90.0));
        Assert.Equal(quarter, NavigationCoordinateFrame.NormalizeQuarter(quarter + 4));
        var (worldX, worldZ) = AvatarKinematics.ForwardDirection(quarter * 90.0);
        Assert.Equal(expectedColumnDelta, worldX, precision: 6);
        Assert.Equal(expectedRowDelta, worldZ, precision: 6);
    }

    [Fact]
    public void RelativeBearingSignIsStableAcrossWorlds()
    {
        Assert.Equal(90.0, NavigationCoordinateFrame.RelativeBearingDegrees(0, 0, 1, 0, 0), precision: 6);
        Assert.Equal(-90.0, NavigationCoordinateFrame.RelativeBearingDegrees(0, 0, -1, 0, 0), precision: 6);
        Assert.Equal(0.0, NavigationCoordinateFrame.RelativeBearingDegrees(0, 0, 0, 1, 0), precision: 6);
    }

    [Fact]
    public void NavigationSessionSteersTowardTheActualTargetCellCenter()
    {
        var sessions = new HippocampalNavigationSessionManager();
        HippocampalNavigationControlRequest request = CreateControlRequest(
            reset: true,
            atCellCenter: false,
            row: 1,
            column: 1,
            forwardOpen: true,
            rearOpen: false) with
        {
            CellOffsetX = 0.4,
            CellOffsetZ = 0.2
        };

        HippocampalNavigationControlResponse response = sessions.Process(request);

        Assert.Equal("motor_turn_right", response.MotorDirective);
        Assert.Equal(-26.565, response.HeadingErrorDeg, precision: 3);
    }

    [Fact]
    public void SeededMazeGenerationIsDeterministicAndDistinct()
    {
        var first = new AvatarMazeEnvironment(new AvatarMazeOptions(Seed: 317));
        var replay = new AvatarMazeEnvironment(new AvatarMazeOptions(Seed: 317));
        var unseen = new AvatarMazeEnvironment(new AvatarMazeOptions(Seed: 911));

        Assert.Equal(first.LayoutFingerprint(), replay.LayoutFingerprint());
        Assert.Equal(first.ShortestPathCells, replay.ShortestPathCells);
        Assert.NotEqual(first.LayoutFingerprint(), unseen.LayoutFingerprint());
        Assert.True(first.Observe().ForwardOpen || first.Observe().LeftOpen || first.Observe().RightOpen);
    }

    [Fact]
    public void HippocampalNavigatorReplaysItsParentAtADeadEnd()
    {
        var navigator = new HippocampalNavigationRuntime();
        HippocampalNavigationDecision outward = navigator.Decide(new HippocampalNavigationObservation(
            Row: 1,
            Column: 1,
            HeadingQuarter: 1,
            ForwardOpen: true,
            LeftOpen: false,
            RightOpen: false,
            RearOpen: false,
            GoalRow: 5,
            GoalColumn: 5,
            GoalBearingDeg: 0,
            DistanceToGoal: 8,
            CollisionCount: 0,
            GoalReached: false));
        HippocampalNavigationDecision returnTrip = navigator.Decide(new HippocampalNavigationObservation(
            Row: 1,
            Column: 2,
            HeadingQuarter: 1,
            ForwardOpen: false,
            LeftOpen: false,
            RightOpen: false,
            RearOpen: true,
            GoalRow: 5,
            GoalColumn: 5,
            GoalBearingDeg: 90,
            DistanceToGoal: 7,
            CollisionCount: 0,
            GoalReached: false));

        Assert.Equal((1, 2), (outward.TargetRow, outward.TargetColumn));
        Assert.True(returnTrip.Backtracking);
        Assert.Equal((1, 1), (returnTrip.TargetRow, returnTrip.TargetColumn));
        Assert.Equal(1, navigator.BacktrackCount);
        Assert.Contains("replay parent", returnTrip.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NavigationSessionRetainsAPlanUntilTheAvatarReachesTheTargetCellCenter()
    {
        var sessions = new HippocampalNavigationSessionManager();
        HippocampalNavigationControlResponse first = sessions.Process(CreateControlRequest(
            reset: true,
            atCellCenter: true,
            row: 1,
            column: 1,
            forwardOpen: true,
            rearOpen: false));
        HippocampalNavigationControlResponse held = sessions.Process(CreateControlRequest(
            reset: false,
            atCellCenter: false,
            row: 1,
            column: 1,
            forwardOpen: true,
            rearOpen: false));
        HippocampalNavigationControlResponse advanced = sessions.Process(CreateControlRequest(
            reset: false,
            atCellCenter: true,
            row: 2,
            column: 1,
            forwardOpen: true,
            rearOpen: true));

        Assert.True(first.ResetApplied);
        Assert.Equal((2, 1), (first.Decision.TargetRow, first.Decision.TargetColumn));
        Assert.Equal(first.Decision, held.Decision);
        Assert.Equal(1, held.DecisionCount);
        Assert.Equal(2, advanced.DecisionCount);
        Assert.Equal((3, 1), (advanced.Decision.TargetRow, advanced.Decision.TargetColumn));
        Assert.All(first.MotorSpikes, static spike => Assert.Equal("M1", spike.SourceStructure));
    }

    [Fact]
    public void NavigationRouteRejectsContradictoryWorldObservationsAndResetStartsFresh()
    {
        var sessions = new HippocampalNavigationSessionManager();
        HippocampalNavigationControlRequest blocked = CreateControlRequest(
            reset: true,
            atCellCenter: true,
            row: 1,
            column: 1,
            forwardOpen: false,
            rearOpen: false);
        IResult invalid = NavigationRoutes.PostDecision(blocked, sessions);
        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalid).StatusCode);

        HippocampalNavigationControlResponse first = sessions.Process(CreateControlRequest(
            reset: true,
            atCellCenter: true,
            row: 1,
            column: 1,
            forwardOpen: true,
            rearOpen: false));
        HippocampalNavigationControlResponse reset = sessions.Process(CreateControlRequest(
            reset: true,
            atCellCenter: true,
            row: 1,
            column: 1,
            forwardOpen: true,
            rearOpen: false));

        Assert.Equal(1, first.DecisionCount);
        Assert.True(reset.ResetApplied);
        Assert.Equal(1, reset.DecisionCount);
        Assert.Equal(NavigationCoordinateFrame.ControlProtocolVersion, reset.ProtocolVersion);
    }

    [Fact]
    public void ProductionAvatarNavigatesAnUnseenMazeWithoutCollision()
    {
        ContinuousNavigationResult result = ContinuousNavigationBenchmark.Run([911]);
        ContinuousNavigationScenarioResult scenario = Assert.Single(result.Scenarios);

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Criteria.Concat(scenario.Criteria)));
        Assert.True(scenario.GoalReached);
        Assert.Equal(0, scenario.Collisions);
        Assert.True(scenario.Backtracks > 0);
        Assert.True(scenario.ExploredPlaces > 1);
        Assert.True(scenario.AvatarPlaceMemories > 1);
        Assert.True(scenario.BrainMotorSpikes > 0);
        Assert.Equal(scenario.BrainMotorSpikes, scenario.AvatarMotorEvents);
        Assert.True(scenario.BodyMessages > 0);
        Assert.True(scenario.OutcomeMessages > 0);
        Assert.True(scenario.ObjectMessages > 0);
    }

    private static HippocampalNavigationControlRequest CreateControlRequest(
        bool reset,
        bool atCellCenter,
        int row,
        int column,
        bool forwardOpen,
        bool rearOpen)
        => new(
            SessionId: "rendered-maze-test",
            MazeId: "maze-317",
            Reset: reset,
            AtCellCenter: atCellCenter,
            HeadingDeg: 0.0,
            Observation: new HippocampalNavigationObservation(
                Row: row,
                Column: column,
                HeadingQuarter: 0,
                ForwardOpen: forwardOpen,
                LeftOpen: false,
                RightOpen: false,
                RearOpen: rearOpen,
                GoalRow: 5,
                GoalColumn: 1,
                GoalBearingDeg: 0.0,
                DistanceToGoal: Math.Max(0, 5 - row),
                CollisionCount: 0,
                GoalReached: false));
}
