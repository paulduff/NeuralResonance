using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.ControlProgram.Services;

/// <summary>
/// Stateful local-navigation substrate modeled after hippocampal place binding,
/// retrosplenial heading transformation, and prefrontal action selection. It
/// never receives the hidden maze layout; the graph is learned one observed
/// place at a time from egocentric open/blocked probes.
/// </summary>
public sealed class HippocampalNavigationRuntime
{
    private readonly Dictionary<Cell, int> _visits = [];
    private readonly Dictionary<Cell, Cell> _parents = [];
    private readonly HashSet<Edge> _observedOpenEdges = [];
    private Cell? _lastCell;

    public int DecisionCount { get; private set; }
    public int BacktrackCount { get; private set; }
    public int ExploredPlaceCount => _visits.Count;
    public int LearnedEdgeCount => _observedOpenEdges.Count;

    public HippocampalNavigationDecision Decide(HippocampalNavigationObservation observation)
    {
        int heading = NormalizeQuarter(observation.HeadingQuarter);
        var current = new Cell(observation.Row, observation.Column);
        if (_lastCell is Cell previous && previous != current && !_parents.ContainsKey(current))
        {
            _parents[current] = previous;
        }

        _lastCell = current;
        _visits.TryGetValue(current, out int priorVisits);
        _visits[current] = priorVisits + 1;

        if (observation.GoalReached ||
            (observation.Row == observation.GoalRow && observation.Column == observation.GoalColumn))
        {
            DecisionCount++;
            return new HippocampalNavigationDecision(
                current.Row,
                current.Column,
                current.Row,
                current.Column,
                heading,
                "motor_stop",
                "motor_stop",
                false,
                _visits[current],
                observation.GoalBearingDeg,
                "goal place reached; basal-ganglia motor gate closed");
        }

        List<Candidate> candidates = BuildCandidates(observation, current, heading);
        foreach (Candidate candidate in candidates)
        {
            _observedOpenEdges.Add(Edge.Create(current, candidate.Cell));
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"Navigation observation at {current} exposes no traversable exit.");
        }

        Candidate selected;
        bool backtracking = false;
        Candidate[] unexplored = candidates
            .Where(candidate => !_visits.ContainsKey(candidate.Cell))
            .OrderBy(candidate => ManhattanDistance(candidate.Cell, observation.GoalRow, observation.GoalColumn))
            .ThenBy(candidate => QuarterTurnCost(heading, candidate.HeadingQuarter))
            .ThenBy(candidate => candidate.HeadingQuarter)
            .ToArray();
        if (unexplored.Length > 0)
        {
            selected = unexplored[0];
        }
        else if (_parents.TryGetValue(current, out Cell parent))
        {
            selected = candidates.First(candidate => candidate.Cell == parent);
            backtracking = true;
            BacktrackCount++;
        }
        else
        {
            selected = candidates
                .OrderBy(candidate => _visits.GetValueOrDefault(candidate.Cell))
                .ThenBy(candidate => ManhattanDistance(candidate.Cell, observation.GoalRow, observation.GoalColumn))
                .ThenBy(candidate => QuarterTurnCost(heading, candidate.HeadingQuarter))
                .First();
        }

        DecisionCount++;
        string turnDirective = ResolveTurnDirective(heading, selected.HeadingQuarter);
        int selectedVisits = _visits.GetValueOrDefault(selected.Cell);
        return new HippocampalNavigationDecision(
            current.Row,
            current.Column,
            selected.Cell.Row,
            selected.Cell.Column,
            selected.HeadingQuarter,
            turnDirective,
            "motor_forward",
            backtracking,
            selectedVisits,
            observation.GoalBearingDeg,
            backtracking
                ? $"CA3 place sequence exhausted locally; replay parent {selected.Cell}"
                : $"novel place {selected.Cell}; Manhattan goal estimate {ManhattanDistance(selected.Cell, observation.GoalRow, observation.GoalColumn)}");
    }

    private static List<Candidate> BuildCandidates(
        HippocampalNavigationObservation observation,
        Cell current,
        int heading)
    {
        var candidates = new List<Candidate>(4);
        AddCandidate(candidates, current, heading, observation.ForwardOpen);
        AddCandidate(candidates, current, RotateQuarter(heading, 1), observation.LeftOpen);
        AddCandidate(candidates, current, RotateQuarter(heading, -1), observation.RightOpen);
        AddCandidate(candidates, current, RotateQuarter(heading, 2), observation.RearOpen);
        return candidates;
    }

    private static void AddCandidate(List<Candidate> candidates, Cell current, int heading, bool open)
    {
        if (!open)
        {
            return;
        }

        (int dr, int dc) = DirectionDelta(heading);
        candidates.Add(new Candidate(new Cell(current.Row + dr, current.Column + dc), heading));
    }

    private static string ResolveTurnDirective(int currentHeading, int targetHeading)
    {
        int delta = NormalizeQuarter(targetHeading - currentHeading);
        return delta switch
        {
            0 => "motor_forward",
            1 => "motor_turn_left",
            2 => "motor_about_face_left",
            _ => "motor_turn_right"
        };
    }

    private static int QuarterTurnCost(int currentHeading, int targetHeading)
    {
        int delta = NormalizeQuarter(targetHeading - currentHeading);
        return Math.Min(delta, 4 - delta);
    }

    private static int ManhattanDistance(Cell cell, int goalRow, int goalColumn)
        => Math.Abs(goalRow - cell.Row) + Math.Abs(goalColumn - cell.Column);

    private static int NormalizeQuarter(int value) => NavigationCoordinateFrame.NormalizeQuarter(value);

    private static int RotateQuarter(int headingQuarter, int delta)
        => NavigationCoordinateFrame.RotateQuarter(headingQuarter, delta);

    private static (int Dr, int Dc) DirectionDelta(int headingQuarter)
        => NavigationCoordinateFrame.DirectionDelta(headingQuarter);

    private readonly record struct Cell(int Row, int Column)
    {
        public override string ToString() => $"({Row},{Column})";
    }

    private readonly record struct Candidate(Cell Cell, int HeadingQuarter);

    private readonly record struct Edge(Cell A, Cell B)
    {
        public static Edge Create(Cell first, Cell second)
            => Compare(first, second) <= 0 ? new Edge(first, second) : new Edge(second, first);

        private static int Compare(Cell first, Cell second)
        {
            int row = first.Row.CompareTo(second.Row);
            return row != 0 ? row : first.Column.CompareTo(second.Column);
        }
    }
}

public sealed class HippocampalNavigationSessionManager
{
    public const string ProtocolVersion = NavigationCoordinateFrame.ControlProtocolVersion;
    private const int MotorBurstCount = 16;
    private readonly Dictionary<string, SessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sessionsGate = new();

    public HippocampalNavigationControlResponse Process(HippocampalNavigationControlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string sessionId = NormalizeIdentifier(request.SessionId, "session", 96);
        string mazeId = NormalizeIdentifier(request.MazeId, "maze", 128);
        SessionState session;
        bool resetApplied = false;
        lock (_sessionsGate)
        {
            if (request.Reset || !_sessions.TryGetValue(sessionId, out session!) ||
                !string.Equals(session.MazeId, mazeId, StringComparison.OrdinalIgnoreCase))
            {
                session = new SessionState(mazeId);
                _sessions[sessionId] = session;
                resetApplied = true;
            }
        }

        lock (session.Gate)
        {
            HippocampalNavigationObservation observation = request.Observation;
            bool reachedActiveTarget = session.ActiveDecision is not null &&
                                       observation.Row == session.ActiveDecision.TargetRow &&
                                       observation.Column == session.ActiveDecision.TargetColumn &&
                                       request.AtCellCenter;
            bool departedExpectedEdge = session.ActiveDecision is not null &&
                                        (observation.Row != session.ActiveDecision.FromRow ||
                                         observation.Column != session.ActiveDecision.FromColumn) &&
                                        (observation.Row != session.ActiveDecision.TargetRow ||
                                         observation.Column != session.ActiveDecision.TargetColumn);
            if (session.ActiveDecision is null || reachedActiveTarget || departedExpectedEdge || observation.GoalReached)
            {
                session.ActiveDecision = session.Navigator.Decide(observation);
            }

            HippocampalNavigationDecision decision = session.ActiveDecision;
            double targetHeading = NavigationCoordinateFrame.HeadingDegreesToCellTarget(
                observation.Row,
                observation.Column,
                request.CellOffsetX,
                request.CellOffsetZ,
                decision.TargetRow,
                decision.TargetColumn);
            double headingError = NavigationCoordinateFrame.NormalizeSignedDegrees(targetHeading - request.HeadingDeg);
            string directive = observation.GoalReached
                ? "motor_stop"
                : ResolvePhaseDirective(decision, headingError);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long sequenceBase = checked(session.SpikeSequence * MotorBurstCount);
            HippocampalNavigationMotorSpike[] spikes = directive == "motor_stop"
                ? []
                : Enumerable.Range(0, MotorBurstCount)
                    .Select(index => new HippocampalNavigationMotorSpike(
                        "M1",
                        (index & 1) == 0 ? "L" : "R",
                        now,
                        $"{directive}_{sequenceBase + index}"))
                    .ToArray();
            session.SpikeSequence++;

            return new HippocampalNavigationControlResponse(
                ProtocolVersion,
                sessionId,
                mazeId,
                resetApplied,
                directive,
                headingError,
                decision,
                session.Navigator.DecisionCount,
                session.Navigator.BacktrackCount,
                session.Navigator.ExploredPlaceCount,
                session.Navigator.LearnedEdgeCount,
                spikes);
        }
    }

    public int SessionCount
    {
        get
        {
            lock (_sessionsGate)
            {
                return _sessions.Count;
            }
        }
    }

    private static string ResolvePhaseDirective(HippocampalNavigationDecision decision, double headingError)
    {
        if (Math.Abs(headingError) <= 8.0)
        {
            return decision.ForwardDirective;
        }

        if (Math.Abs(headingError) >= 145.0)
        {
            return headingError >= 0.0 ? "motor_about_face_left" : "motor_about_face_right";
        }

        return headingError >= 0.0 ? "motor_turn_left" : "motor_turn_right";
    }

    private static string NormalizeIdentifier(string value, string fallback, int maximumLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private sealed class SessionState(string mazeId)
    {
        public object Gate { get; } = new();
        public string MazeId { get; } = mazeId;
        public HippocampalNavigationRuntime Navigator { get; } = new();
        public HippocampalNavigationDecision? ActiveDecision { get; set; }
        public long SpikeSequence { get; set; }
    }
}
