using NeuralResonanceEngine.Protocol;

internal sealed record NeuronalVisualAttentionDecision(
    bool Available,
    bool Active,
    string FocusedField,
    string FocusedHemisphere,
    float LeftFieldDrive,
    float RightFieldDrive,
    float LeftHemisphereTrnSuppression,
    float RightHemisphereTrnSuppression,
    float SelectionMargin,
    float FocusConfidence,
    float CircuitCoverage,
    long SustainedSelectionTicks,
    long LastSelectionTick)
{
    public const string Authority = "BilateralPulvinarFrontoparietalCompetition";

    public static NeuronalVisualAttentionDecision Unavailable { get; } = new(
        false,
        false,
        "neutral",
        "M",
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0,
        -1);
}

internal sealed record NeuronalVisualAttentionSnapshot(
    long Tick,
    string Authority,
    bool ReadOnlyMonitor,
    bool CanAcceptAttentionOverrides,
    bool LegacyWinnerEnabled,
    NeuronalVisualAttentionDecision VisualAttention);

internal sealed class NeuronalVisualAttentionRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalVisualAttentionDecision _decision = NeuronalVisualAttentionDecision.Unavailable;

    public NeuronalVisualAttentionDecision Update(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var decoded = NeuronalVisualAttentionDecoder.Decode(snapshots);
        lock (_gate)
        {
            if (tick < _tick)
            {
                return _decision;
            }

            var sameWinner = decoded.Active &&
                _decision.Active &&
                string.Equals(decoded.FocusedField, _decision.FocusedField, StringComparison.Ordinal);
            var sustainedTicks = decoded.Active
                ? sameWinner ? _decision.SustainedSelectionTicks + 1 : 1
                : 0;
            var lastSelectionTick = decoded.Active
                ? sameWinner ? _decision.LastSelectionTick : tick
                : _decision.LastSelectionTick;

            _tick = tick;
            _decision = decoded with
            {
                SustainedSelectionTicks = sustainedTicks,
                LastSelectionTick = lastSelectionTick
            };
            return _decision;
        }
    }

    public NeuronalVisualAttentionSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalVisualAttentionSnapshot(
                _tick,
                NeuronalVisualAttentionDecision.Authority,
                ReadOnlyMonitor: true,
                CanAcceptAttentionOverrides: false,
                LegacyWinnerEnabled: false,
                _decision);
        }
    }
}

internal static class NeuronalVisualAttentionDecoder
{
    private const double ExcitatorySaturationHz = 55.0;
    private const double InhibitorySaturationHz = 35.0;
    private const double MinimumDrive = 0.005;
    private const double MinimumMargin = 0.01;

    private static readonly StructureId[] RequiredStructures =
    [
        StructureId.Pfc,
        StructureId.Ppc,
        StructureId.Pulvinar,
        StructureId.Trn
    ];

    public static NeuronalVisualAttentionDecision Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Count == 0)
        {
            return NeuronalVisualAttentionDecision.Unavailable;
        }

        var requiredPopulationCount = RequiredStructures.Length * 2;
        var observedPopulationCount = RequiredStructures.Sum(structure =>
            (HasPopulation(snapshots, structure, "L") ? 1 : 0) +
            (HasPopulation(snapshots, structure, "R") ? 1 : 0));
        var coverage = observedPopulationCount / (double)requiredPopulationCount;

        var leftPpc = ExcitatoryRate(snapshots, StructureId.Ppc, "L");
        var rightPpc = ExcitatoryRate(snapshots, StructureId.Ppc, "R");
        var leftPfc = ExcitatoryRate(snapshots, StructureId.Pfc, "L");
        var rightPfc = ExcitatoryRate(snapshots, StructureId.Pfc, "R");
        var leftPulvinar = ExcitatoryRate(snapshots, StructureId.Pulvinar, "L");
        var rightPulvinar = ExcitatoryRate(snapshots, StructureId.Pulvinar, "R");
        var leftTrn = InhibitoryRate(snapshots, StructureId.Trn, "L");
        var rightTrn = InhibitoryRate(snapshots, StructureId.Trn, "R");

        // Each visual hemifield is represented by the contralateral thalamocortical population.
        var leftFieldDrive = Math.Clamp(
            (rightPpc * 0.42) +
            (rightPulvinar * 0.34) +
            (rightPfc * 0.24) -
            (rightTrn * 0.30),
            0.0,
            1.0);
        var rightFieldDrive = Math.Clamp(
            (leftPpc * 0.42) +
            (leftPulvinar * 0.34) +
            (leftPfc * 0.24) -
            (leftTrn * 0.30),
            0.0,
            1.0);
        var difference = leftFieldDrive - rightFieldDrive;
        var margin = Math.Abs(difference);
        var winnerDrive = Math.Max(leftFieldDrive, rightFieldDrive);
        var circuitComplete = coverage >= 1.0;
        var active = circuitComplete && winnerDrive > MinimumDrive && margin > MinimumMargin;
        var focusedField = active
            ? difference > 0.0 ? "left" : "right"
            : "neutral";
        var focusedHemisphere = focusedField switch
        {
            "left" => "R",
            "right" => "L",
            _ => "M"
        };
        var confidence = active
            ? Math.Clamp(
                (margin * 2.6) +
                (winnerDrive * 0.42) +
                (coverage * 0.18),
                0.0,
                1.0)
            : 0.0;

        return new NeuronalVisualAttentionDecision(
            Available: observedPopulationCount > 0,
            Active: active,
            FocusedField: focusedField,
            FocusedHemisphere: focusedHemisphere,
            LeftFieldDrive: (float)leftFieldDrive,
            RightFieldDrive: (float)rightFieldDrive,
            LeftHemisphereTrnSuppression: (float)leftTrn,
            RightHemisphereTrnSuppression: (float)rightTrn,
            SelectionMargin: (float)margin,
            FocusConfidence: (float)confidence,
            CircuitCoverage: (float)coverage,
            SustainedSelectionTicks: 0,
            LastSelectionTick: -1);
    }

    public static double GetContralateralSensoryGain(
        string? targetHemisphere,
        float? leftFieldSaliency,
        float? rightFieldSaliency)
    {
        if (leftFieldSaliency is null && rightFieldSaliency is null)
        {
            return 1.0;
        }

        var left = Math.Clamp(leftFieldSaliency ?? 0f, 0f, 1f);
        var right = Math.Clamp(rightFieldSaliency ?? 0f, 0f, 1f);
        var saliency = targetHemisphere?.Trim().ToUpperInvariant() switch
        {
            "L" => right,
            "R" => left,
            _ => (left + right) * 0.5
        };

        // Receptor input remains present at low saliency, while the side-to-side
        // difference is encoded into contralateral neural firing populations.
        return 0.10 + (0.90 * saliency);
    }

    private static bool HasPopulation(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        string hemisphere)
        => snapshots.Any(snapshot =>
            snapshot.StructureId == structure &&
            string.Equals(snapshot.Instance.HemisphereNormalized, hemisphere, StringComparison.Ordinal));

    private static double ExcitatoryRate(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        string hemisphere)
        => NormalizedMeanRate(snapshots, structure, hemisphere, ExcitatorySaturationHz);

    private static double InhibitoryRate(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        string hemisphere)
        => NormalizedMeanRate(snapshots, structure, hemisphere, InhibitorySaturationHz);

    private static double NormalizedMeanRate(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        string hemisphere,
        double saturationHz)
    {
        var values = snapshots
            .Where(snapshot =>
                snapshot.StructureId == structure &&
                string.Equals(snapshot.Instance.HemisphereNormalized, hemisphere, StringComparison.Ordinal))
            .Select(static snapshot => (double)snapshot.MeanFiringRateHz)
            .Where(static value => double.IsFinite(value) && value >= 0.0)
            .ToArray();
        return values.Length == 0
            ? 0.0
            : Math.Clamp(values.Average() / saturationHz, 0.0, 1.0);
    }
}
