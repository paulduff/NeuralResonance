using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarMazeOptions(
    int Rows = 15,
    int Columns = 15,
    int Seed = 317,
    double CellSize = 1.0,
    double AvatarRadius = 0.18,
    double GoalRadius = 0.24,
    int ExtraOpeningDivisor = 18);

public readonly record struct AvatarMazeObservation(
    int Row,
    int Column,
    int HeadingQuarter,
    double X,
    double Z,
    bool ForwardOpen,
    bool LeftOpen,
    bool RightOpen,
    bool RearOpen,
    int GoalRow,
    int GoalColumn,
    double GoalBearingDeg,
    double DistanceToGoal,
    int CollisionCount,
    int TransitionCount,
    bool GoalReached);

public readonly record struct AvatarMazeTransition(
    double PreviousX,
    double PreviousZ,
    double X,
    double Z,
    double HeadingDeg,
    double ForwardVelocity,
    double TurnRateDeg,
    double DistanceTravelled,
    double Progress,
    bool Collision,
    bool EnteredNewCell,
    bool GoalReached);

/// <summary>
/// Deterministic headless maze physics shared by embodied evaluations. The maze
/// layout stays private; agents receive only local wall probes and goal-relative
/// sensory information through <see cref="Observe"/>.
/// </summary>
public sealed class AvatarMazeEnvironment
{
    private static readonly (int Dr, int Dc)[] CarveDirections =
    [
        (-2, 0),
        (2, 0),
        (0, -2),
        (0, 2)
    ];

    private readonly AvatarMazeOptions _options;
    private readonly char[][] _layout;
    private readonly int _rows;
    private readonly int _columns;
    private readonly int _goalRow;
    private readonly int _goalColumn;
    private int _lastRow;
    private int _lastColumn;

    public AvatarMazeEnvironment(AvatarMazeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _rows = NormalizeDimension(options.Rows);
        _columns = NormalizeDimension(options.Columns);
        _options = options with { Rows = _rows, Columns = _columns };
        _goalRow = _rows - 2;
        _goalColumn = _columns - 2;
        _layout = GenerateLayout(_rows, _columns, options.Seed, options.ExtraOpeningDivisor);

        X = CellCenter(1);
        Z = CellCenter(1);
        HeadingDeg = 90.0;
        _lastRow = 1;
        _lastColumn = 1;
        WalkableCellCount = _layout.Sum(static row => row.Count(static cell => cell != '#'));
        ShortestPathCells = FindShortestPathCells();
    }

    public int Seed => _options.Seed;
    public int Rows => _rows;
    public int Columns => _columns;
    public int GoalRow => _goalRow;
    public int GoalColumn => _goalColumn;
    public double CellSize => _options.CellSize;
    public int WalkableCellCount { get; }
    public int ShortestPathCells { get; }
    public double X { get; private set; }
    public double Z { get; private set; }
    public double HeadingDeg { get; private set; }
    public double TotalDistanceTravelled { get; private set; }
    public int CollisionCount { get; private set; }
    public int TransitionCount { get; private set; }
    public bool GoalReached { get; private set; }

    public AvatarMazeObservation Observe()
    {
        (int row, int column) = ResolveCell(X, Z);
        int heading = NavigationCoordinateFrame.HeadingQuarterFromDegrees(HeadingDeg);
        (int forwardDr, int forwardDc) = NavigationCoordinateFrame.DirectionDelta(heading);
        (int leftDr, int leftDc) = NavigationCoordinateFrame.DirectionDelta(NavigationCoordinateFrame.RotateQuarter(heading, 1));
        (int rightDr, int rightDc) = NavigationCoordinateFrame.DirectionDelta(NavigationCoordinateFrame.RotateQuarter(heading, -1));
        (int rearDr, int rearDc) = NavigationCoordinateFrame.DirectionDelta(NavigationCoordinateFrame.RotateQuarter(heading, 2));
        double goalX = CellCenter(_goalColumn);
        double goalZ = CellCenter(_goalRow);
        double absoluteBearing = AvatarKinematics.NormalizeDegrees(
            Math.Atan2(goalX - X, goalZ - Z) * 180.0 / Math.PI);

        return new AvatarMazeObservation(
            row,
            column,
            heading,
            X,
            Z,
            IsWalkable(row + forwardDr, column + forwardDc),
            IsWalkable(row + leftDr, column + leftDc),
            IsWalkable(row + rightDr, column + rightDc),
            IsWalkable(row + rearDr, column + rearDc),
            _goalRow,
            _goalColumn,
            NavigationCoordinateFrame.NormalizeSignedDegrees(absoluteBearing - HeadingDeg),
            Math.Sqrt(DistanceSquared(X, Z, goalX, goalZ)),
            CollisionCount,
            TransitionCount,
            GoalReached);
    }

    public AvatarMazeTransition Advance(AvatarMotorOutput movement, double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Maze time step must be finite and positive.");
        }

        double previousX = X;
        double previousZ = Z;
        double previousDistance = DistanceToGoal(previousX, previousZ);
        HeadingDeg = AvatarKinematics.AdvanceHeading(HeadingDeg, movement.TurnRateDeg, deltaSeconds);
        (double directionX, double directionZ) = AvatarKinematics.ForwardDirection(HeadingDeg);
        double nextX = X + (directionX * movement.ForwardSpeed * deltaSeconds);
        double nextZ = Z + (directionZ * movement.ForwardSpeed * deltaSeconds);
        bool collision = false;

        if (!Collides(nextX, nextZ))
        {
            X = nextX;
            Z = nextZ;
        }
        else
        {
            collision = true;
            if (!Collides(nextX, Z))
            {
                X = nextX;
            }

            if (!Collides(X, nextZ))
            {
                Z = nextZ;
            }

            CollisionCount++;
        }

        double travelled = Math.Sqrt(DistanceSquared(previousX, previousZ, X, Z));
        TotalDistanceTravelled += travelled;
        double currentDistance = DistanceToGoal(X, Z);
        (int row, int column) = ResolveCell(X, Z);
        bool enteredNewCell = row != _lastRow || column != _lastColumn;
        if (enteredNewCell)
        {
            _lastRow = row;
            _lastColumn = column;
            TransitionCount++;
        }

        GoalReached |= currentDistance <= _options.GoalRadius;
        return new AvatarMazeTransition(
            previousX,
            previousZ,
            X,
            Z,
            HeadingDeg,
            movement.ForwardSpeed,
            movement.TurnRateDeg,
            travelled,
            Math.Clamp((previousDistance - currentDistance) / _options.CellSize, -1.0, 1.0),
            collision,
            enteredNewCell,
            GoalReached);
    }

    public AvatarBodyTelemetry CreateBodyTelemetry(AvatarMazeTransition? transition = null)
    {
        double contact = transition?.Collision == true ? 0.86 : 0.0;
        return new AvatarBodyTelemetry(
            ForwardVelocity: transition?.ForwardVelocity ?? 0.0,
            TurnRateDeg: transition?.TurnRateDeg ?? 0.0,
            ContactLevel: contact,
            LeftMotorDrive: 0.0,
            RightMotorDrive: 0.0,
            Hunger: 0.32,
            Health: transition?.Collision == true ? 0.96 : 1.0,
            TactileFront: transition?.Collision == true ? 0.86 : 0.0,
            TactileLeft: 0.0,
            TactileRight: 0.0,
            TactileGround: 0.18,
            PainLevel: transition?.Collision == true ? 0.18 : 0.0);
    }

    public AvatarObjectObservation CreateGoalObservation()
    {
        AvatarMazeObservation observation = Observe();
        double salience = Math.Clamp(0.42 + (1.0 / Math.Max(1.0, observation.DistanceToGoal)), 0.0, 0.98);
        string hemisphere = observation.GoalBearingDeg switch
        {
            < -6.0 => "R",
            > 6.0 => "L",
            _ => "M"
        };
        return new AvatarObjectObservation(
            $"maze.{Seed}.goal",
            "navigation goal",
            salience,
            0.96,
            1.1,
            24,
            observation.DistanceToGoal,
            hemisphere);
    }

    public static double HeadingDegreesForQuarter(int headingQuarter)
        => NavigationCoordinateFrame.NormalizeQuarter(headingQuarter) * 90.0;

    public string LayoutFingerprint()
    {
        uint hash = 2166136261;
        for (int row = 0; row < _rows; row++)
        {
            for (int column = 0; column < _columns; column++)
            {
                hash ^= _layout[row][column];
                hash *= 16777619;
            }
        }

        return $"{_rows}x{_columns}:{hash:X8}";
    }

    private static int NormalizeDimension(int value)
    {
        int normalized = Math.Max(7, value);
        return (normalized & 1) == 0 ? normalized + 1 : normalized;
    }

    private static char[][] GenerateLayout(int rows, int columns, int seed, int extraOpeningDivisor)
    {
        char[][] layout = Enumerable.Range(0, rows)
            .Select(_ => new string('#', columns).ToCharArray())
            .ToArray();
        var random = new Random(seed);
        var stack = new Stack<(int Row, int Column)>();
        var neighbors = new (int Row, int Column, int WallRow, int WallColumn)[CarveDirections.Length];
        layout[1][1] = '.';
        stack.Push((1, 1));

        while (stack.Count > 0)
        {
            (int row, int column) = stack.Peek();
            int count = 0;
            foreach ((int dr, int dc) in CarveDirections)
            {
                int nextRow = row + dr;
                int nextColumn = column + dc;
                if (nextRow <= 0 || nextRow >= rows - 1 || nextColumn <= 0 || nextColumn >= columns - 1 ||
                    layout[nextRow][nextColumn] != '#')
                {
                    continue;
                }

                neighbors[count++] = (nextRow, nextColumn, row + (dr / 2), column + (dc / 2));
            }

            if (count == 0)
            {
                stack.Pop();
                continue;
            }

            var chosen = neighbors[random.Next(count)];
            layout[chosen.WallRow][chosen.WallColumn] = '.';
            layout[chosen.Row][chosen.Column] = '.';
            stack.Push((chosen.Row, chosen.Column));
        }

        if (extraOpeningDivisor > 0)
        {
            int openings = (rows * columns) / Math.Max(8, extraOpeningDivisor);
            for (int index = 0; index < openings; index++)
            {
                int row = random.Next(1, rows - 1);
                int column = random.Next(1, columns - 1);
                if (layout[row][column] != '#')
                {
                    continue;
                }

                bool horizontal = layout[row][column - 1] != '#' && layout[row][column + 1] != '#';
                bool vertical = layout[row - 1][column] != '#' && layout[row + 1][column] != '#';
                if (horizontal || vertical)
                {
                    layout[row][column] = '.';
                }
            }
        }

        layout[1][1] = 'S';
        layout[rows - 2][columns - 2] = 'G';
        return layout;
    }

    private int FindShortestPathCells()
    {
        var queue = new Queue<(int Row, int Column, int Distance)>();
        var visited = new HashSet<(int Row, int Column)> { (1, 1) };
        queue.Enqueue((1, 1, 0));
        while (queue.Count > 0)
        {
            (int row, int column, int distance) = queue.Dequeue();
            if (row == _goalRow && column == _goalColumn)
            {
                return distance;
            }

            for (int direction = 0; direction < 4; direction++)
            {
                (int dr, int dc) = NavigationCoordinateFrame.DirectionDelta(direction);
                var next = (Row: row + dr, Column: column + dc);
                if (IsWalkable(next.Row, next.Column) && visited.Add(next))
                {
                    queue.Enqueue((next.Row, next.Column, distance + 1));
                }
            }
        }

        throw new InvalidOperationException("Generated maze does not contain a path from start to goal.");
    }

    private bool Collides(double x, double z)
    {
        (int centerRow, int centerColumn) = ResolveCell(x, z);
        double half = _options.CellSize * 0.5;
        for (int row = centerRow - 1; row <= centerRow + 1; row++)
        {
            for (int column = centerColumn - 1; column <= centerColumn + 1; column++)
            {
                if (IsWalkable(row, column))
                {
                    continue;
                }

                double centerX = CellCenter(column);
                double centerZ = CellCenter(row);
                double nearestX = Math.Clamp(x, centerX - half, centerX + half);
                double nearestZ = Math.Clamp(z, centerZ - half, centerZ + half);
                if (DistanceSquared(x, z, nearestX, nearestZ) < _options.AvatarRadius * _options.AvatarRadius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private (int Row, int Column) ResolveCell(double x, double z)
        => (
            Math.Clamp((int)Math.Round(z / _options.CellSize), 0, _rows - 1),
            Math.Clamp((int)Math.Round(x / _options.CellSize), 0, _columns - 1));

    private bool IsWalkable(int row, int column)
        => row >= 0 && row < _rows && column >= 0 && column < _columns && _layout[row][column] != '#';

    private double CellCenter(int index) => index * _options.CellSize;

    private double DistanceToGoal(double x, double z)
        => Math.Sqrt(DistanceSquared(x, z, CellCenter(_goalColumn), CellCenter(_goalRow)));

    private static int CountOpenExits(AvatarMazeObservation observation)
        => (observation.ForwardOpen ? 1 : 0) +
           (observation.LeftOpen ? 1 : 0) +
           (observation.RightOpen ? 1 : 0) +
           (observation.RearOpen ? 1 : 0);

    private static double DistanceSquared(double x1, double z1, double x2, double z2)
    {
        double dx = x2 - x1;
        double dz = z2 - z1;
        return (dx * dx) + (dz * dz);
    }
}
