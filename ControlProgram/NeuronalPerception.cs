using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalPerceptDecision(
    bool Available,
    bool Active,
    int DominantEnsemble,
    double DominanceMargin,
    double Confidence,
    double CircuitCoverage,
    double Persistence,
    double Novelty,
    IReadOnlyList<PerceptEnsembleActivity> Ensembles)
{
    public static NeuronalPerceptDecision Unavailable { get; } = new(
        false,
        false,
        -1,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        []);
}

internal sealed record PerceptLanguageAnnotation(
    long Tick,
    int EnsembleIndex,
    string ObjectId,
    string Label,
    double Confidence,
    long AttachedUnixMs);

internal sealed record NeuronalPerceptInterpretation(
    string Authority,
    bool Available,
    bool Active,
    int DominantEnsemble,
    string? ObjectId,
    string? Label,
    bool LanguageAnnotationAttached,
    double Confidence,
    double DominanceMargin,
    double CircuitCoverage,
    double Persistence,
    double Novelty,
    bool ReadOnly,
    bool CanCreatePercepts,
    bool CanCreateMemories);

internal sealed record NeuronalPerceptionSnapshot(
    long Tick,
    NeuronalPerceptDecision Percept,
    NeuronalPerceptInterpretation Interpretation,
    IReadOnlyList<PerceptLanguageAnnotation> LanguageAnnotations);

internal sealed class NeuronalPerceptionRuntime
{
    public const string Authority = "DistributedPerceptEnsembleCompetition";
    private const int MaxAuditAnnotations = 64;
    private readonly object _gate = new();
    private readonly Queue<PerceptLanguageAnnotation> _annotations = new();
    private long _tick = -1;
    private NeuronalPerceptDecision _percept = NeuronalPerceptDecision.Unavailable;

    public NeuronalPerceptDecision Update(long tick, IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var percept = NeuronalPerceptionDecoder.Decode(snapshots);
        lock (_gate)
        {
            if (tick >= _tick)
            {
                _tick = tick;
                _percept = percept;
            }

            return _percept;
        }
    }

    public NeuronalPerceptionSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var annotations = _annotations.ToArray();
            return new NeuronalPerceptionSnapshot(
                _tick,
                _percept,
                BuildInterpretation(_tick, _percept, annotations),
                annotations);
        }
    }

    public bool TryAttachLanguageAnnotation(
        string objectId,
        string label,
        double confidence,
        out PerceptLanguageAnnotation? annotation,
        out string? error)
    {
        lock (_gate)
        {
            if (!_percept.Available || !_percept.Active || _percept.DominantEnsemble < 0)
            {
                annotation = null;
                error = "No active neuronal percept is available for language annotation.";
                return false;
            }

            var normalizedObjectId = NormalizeText(objectId, "unidentified", 128);
            var normalizedLabel = NormalizeText(label, "unlabelled", 96);
            annotation = new PerceptLanguageAnnotation(
                _tick,
                _percept.DominantEnsemble,
                normalizedObjectId,
                normalizedLabel,
                Math.Clamp(confidence, 0.0, 1.0),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _annotations.Enqueue(annotation);
            while (_annotations.Count > MaxAuditAnnotations)
            {
                _annotations.Dequeue();
            }

            error = null;
            return true;
        }
    }

    private static string NormalizeText(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static NeuronalPerceptInterpretation BuildInterpretation(
        long tick,
        NeuronalPerceptDecision percept,
        IReadOnlyList<PerceptLanguageAnnotation> annotations)
    {
        var annotation = percept.Active
            ? annotations.LastOrDefault(item =>
                item.Tick == tick &&
                item.EnsembleIndex == percept.DominantEnsemble)
            : null;
        return new NeuronalPerceptInterpretation(
            Authority,
            percept.Available,
            percept.Active,
            percept.DominantEnsemble,
            annotation?.ObjectId,
            annotation?.Label,
            annotation is not null,
            percept.Confidence,
            percept.DominanceMargin,
            percept.CircuitCoverage,
            percept.Persistence,
            percept.Novelty,
            ReadOnly: true,
            CanCreatePercepts: false,
            CanCreateMemories: false);
    }
}

internal static class NeuronalPerceptionDecoder
{
    private const int EnsembleCount = 8;

    public static NeuronalPerceptDecision Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var perceptSnapshots = snapshots
            .Where(static snapshot => snapshot.PerceptEnsembleDiagnostics is not null)
            .ToArray();
        if (perceptSnapshots.Length == 0)
        {
            return NeuronalPerceptDecision.Unavailable;
        }

        var observed = perceptSnapshots.Select(static snapshot => snapshot.StructureId).ToHashSet();
        var featureCoverage = HasAny(observed,
            StructureId.Retina, StructureId.V1, StructureId.V2, StructureId.V3, StructureId.V4,
            StructureId.Mt, StructureId.A1, StructureId.AuditoryAssociationCortex,
            StructureId.S1, StructureId.SecondarySomatosensoryCortex) ? 1.0 : 0.0;
        var bindingCoverage = HasAny(observed,
            StructureId.V4, StructureId.InferotemporalCortex, StructureId.FusiformGyrus,
            StructureId.TemporalAssociation, StructureId.Pfc) ? 1.0 : 0.0;
        var salienceCoverage = HasAny(observed, StructureId.Pulvinar, StructureId.Thalamus, StructureId.Pfc) ? 1.0 : 0.0;
        var familiarityCoverage = HasAny(observed, StructureId.PerirhinalCortex, StructureId.ParahippocampalCortex) ? 1.0 : 0.0;
        var indexCoverage = HasAny(observed,
            StructureId.EntorhinalCortex, StructureId.DentateGyrus, StructureId.CA3, StructureId.CA1) ? 1.0 : 0.0;
        var coverage = (featureCoverage + bindingCoverage + salienceCoverage + familiarityCoverage + indexCoverage) / 5.0;

        var ensembles = new PerceptEnsembleActivity[EnsembleCount];
        var scores = new double[EnsembleCount];
        for (var ensemble = 0; ensemble < EnsembleCount; ensemble++)
        {
            var values = perceptSnapshots
                .Select(static snapshot => snapshot.PerceptEnsembleDiagnostics!)
                .SelectMany(static diagnostics => diagnostics.Ensembles)
                .Where(item => item.EnsembleIndex == ensemble)
                .ToArray();
            var visual = Average(values, static item => item.VisualFeatureDrive);
            var motion = Average(values, static item => item.MotionConsistency);
            var auditory = Average(values, static item => item.AuditoryFeatureDrive);
            var somatic = Average(values, static item => item.SomatosensoryFeatureDrive);
            var binding = Average(values, static item => item.RecurrentBinding);
            var salience = Average(values, static item => item.Salience);
            var familiarity = Average(values, static item => item.Familiarity);
            var hippocampal = Average(values, static item => item.HippocampalIndex);
            var novelty = Average(values, static item => item.Novelty);
            var feature = Math.Max(visual, Math.Max(auditory, somatic));
            var confidence = Math.Clamp(
                (feature * 0.28f) +
                (motion * 0.06f) +
                (binding * 0.25f) +
                (salience * 0.14f) +
                (familiarity * 0.10f) +
                (hippocampal * 0.12f) +
                (novelty * 0.05f),
                0f,
                1f);
            ensembles[ensemble] = new PerceptEnsembleActivity(
                ensemble,
                visual,
                motion,
                auditory,
                somatic,
                binding,
                salience,
                familiarity,
                hippocampal,
                novelty,
                confidence);
            scores[ensemble] = confidence;
        }

        var ranked = Enumerable.Range(0, EnsembleCount)
            .OrderByDescending(index => scores[index])
            .ThenBy(static index => index)
            .ToArray();
        var dominant = ranked[0];
        var margin = Math.Max(0.0, scores[dominant] - scores[ranked[1]]);
        var dominantActivity = ensembles[dominant];
        var confidenceWithCoverage = Math.Clamp(
            (scores[dominant] * 0.72) +
            (coverage * 0.18) +
            (Math.Clamp(margin * 5.0, 0.0, 1.0) * 0.10),
            0.0,
            1.0);
        var active = coverage >= 0.40 &&
            confidenceWithCoverage >= 0.18 &&
            scores[dominant] > 0.025 &&
            margin > 0.001 &&
            dominantActivity.RecurrentBinding > 0.025f &&
            Math.Max(
                dominantActivity.VisualFeatureDrive,
                Math.Max(dominantActivity.AuditoryFeatureDrive, dominantActivity.SomatosensoryFeatureDrive)) > 0.025f;

        return new NeuronalPerceptDecision(
            true,
            active,
            active ? dominant : -1,
            margin,
            confidenceWithCoverage,
            coverage,
            dominantActivity.RecurrentBinding,
            dominantActivity.Novelty,
            ensembles);
    }

    private static float Average(
        IReadOnlyList<PerceptEnsembleActivity> values,
        Func<PerceptEnsembleActivity, float> selector)
    {
        var active = values.Select(selector).Where(static value => value > 0f).ToArray();
        return active.Length == 0 ? 0f : (float)active.Average();
    }

    private static bool HasAny(IReadOnlySet<StructureId> observed, params StructureId[] candidates)
        => candidates.Any(observed.Contains);
}
