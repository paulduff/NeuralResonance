namespace NeuralResonanceEngine.Shared.Contracts;

public readonly record struct HippocampalNavigationObservation(
    int Row,
    int Column,
    int HeadingQuarter,
    bool ForwardOpen,
    bool LeftOpen,
    bool RightOpen,
    bool RearOpen,
    int GoalRow,
    int GoalColumn,
    double GoalBearingDeg,
    double DistanceToGoal,
    int CollisionCount,
    bool GoalReached);

public sealed record HippocampalNavigationDecision(
    int FromRow,
    int FromColumn,
    int TargetRow,
    int TargetColumn,
    int TargetHeadingQuarter,
    string TurnDirective,
    string ForwardDirective,
    bool Backtracking,
    int VisitCount,
    double GoalBearingDeg,
    string Evidence);

public sealed record HippocampalNavigationControlRequest(
    string SessionId,
    string MazeId,
    bool Reset,
    bool AtCellCenter,
    double HeadingDeg,
    HippocampalNavigationObservation Observation,
    double CellOffsetX = 0.0,
    double CellOffsetZ = 0.0);

public sealed record HippocampalNavigationMotorSpike(
    string SourceStructure,
    string SourceHemisphere,
    long WallClockUnixMs,
    string SourceNeuronId);

public sealed record HippocampalNavigationControlResponse(
    string ProtocolVersion,
    string SessionId,
    string MazeId,
    bool ResetApplied,
    string MotorDirective,
    double HeadingErrorDeg,
    HippocampalNavigationDecision Decision,
    int DecisionCount,
    int BacktrackCount,
    int ExploredPlaceCount,
    int LearnedEdgeCount,
    IReadOnlyList<HippocampalNavigationMotorSpike> MotorSpikes);

/// <summary>
/// Canonical coordinate frame shared by every embodied world. Quarter zero is
/// positive Z/increasing row; quarter rotation increases toward positive X/
/// increasing column. Positive relative bearing is a left turn.
/// </summary>
public static class NavigationCoordinateFrame
{
    public const string ControlProtocolVersion = "dnne.navigation-control.v2";

    public static int HeadingQuarterFromDegrees(double headingDeg)
        => NormalizeQuarter((int)Math.Round(NormalizeHeadingDegrees(headingDeg) / 90.0));

    public static int NormalizeQuarter(int value) => ((value % 4) + 4) % 4;

    public static int RotateQuarter(int headingQuarter, int delta)
        => NormalizeQuarter(headingQuarter + delta);

    public static (int Dr, int Dc) DirectionDelta(int headingQuarter)
        => NormalizeQuarter(headingQuarter) switch
        {
            0 => (1, 0),
            1 => (0, 1),
            2 => (-1, 0),
            _ => (0, -1)
        };

    public static double NormalizeHeadingDegrees(double value)
    {
        double wrapped = value % 360.0;
        return wrapped < 0.0 ? wrapped + 360.0 : wrapped;
    }

    public static double NormalizeSignedDegrees(double value)
    {
        double wrapped = ((value + 540.0) % 360.0) - 180.0;
        return wrapped == -180.0 ? 180.0 : wrapped;
    }

    public static double RelativeBearingDegrees(
        double fromX,
        double fromZ,
        double toX,
        double toZ,
        double headingDeg)
    {
        double absoluteBearing = Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI;
        return NormalizeSignedDegrees(absoluteBearing - headingDeg);
    }

    public static double HeadingDegreesToCellTarget(
        int currentRow,
        int currentColumn,
        double cellOffsetX,
        double cellOffsetZ,
        int targetRow,
        int targetColumn)
    {
        double deltaX = targetColumn - currentColumn - cellOffsetX;
        double deltaZ = targetRow - currentRow - cellOffsetZ;
        return NormalizeHeadingDegrees(Math.Atan2(deltaX, deltaZ) * 180.0 / Math.PI);
    }
}
