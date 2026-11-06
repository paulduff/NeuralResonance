using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

// Offline intervention on captured contacts. No StructureEngine, membrane,
// recurrent network, service, save loader or invented neuronal spike source.
internal static class ContactReplayStudy
{
    private const string Rule = "STDP+SynapticTaggingCapture";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private enum Intervention { Recorded, NoTagCapture, NoDopamine, NoAcetylcholine }
    internal sealed record InputCase(string Key, string File, string Sha256, int Events);
    internal sealed record Input(string Version, string SourceRevision, InputCase[] Cases);

    public static void Run(string inputDirectory, string outputDirectory)
    {
        if (!Path.IsPathFullyQualified(inputDirectory) || !Path.IsPathFullyQualified(outputDirectory) ||
            !Directory.Exists(inputDirectory) || Directory.Exists(outputDirectory))
            throw new ArgumentException("Existing absolute evidence directory and fresh absolute output required.");
        Directory.CreateDirectory(outputDirectory);
        void Save(string name, object value) => File.WriteAllText(Path.Combine(outputDirectory, name), JsonSerializer.Serialize(value, Json));
        var replays = new List<object>(); var cadence = new List<object>();
        try
        {
            var spec = JsonSerializer.Deserialize<Input>(File.ReadAllText(Path.Combine(inputDirectory, "input.json")), Json)
                ?? throw new InvalidOperationException("Missing contact evidence specification.");
            string[] names = ["continuous-colocated", "continuous-neighbor", "continuous-simultaneous", "continuous-unmodulated"];
            if (spec.Version != "cortical-contact-replay-v1" || spec.SourceRevision != "aeb40be364e2e44dd4d83e47f9a9641d326c6765" ||
                spec.Cases is null || !spec.Cases.Select(x => x.Key).SequenceEqual(names))
                throw new InvalidOperationException("Unexpected reviewed contact cohort.");
            foreach (var entry in spec.Cases)
            {
                if (entry.File != entry.Key + ".jsonl") throw new InvalidOperationException("Unexpected input path.");
                string input = Path.Combine(inputDirectory, entry.File);
                if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))) != entry.Sha256)
                    throw new InvalidOperationException("Captured evidence hash changed.");
                var events = File.ReadLines(input).Select(line => JsonSerializer.Deserialize<MemoryLearningEvidence>(line, Json)
                    ?? throw new InvalidOperationException("Missing event.")).ToArray();
                if (events.Length != entry.Events || events.Length == 0) throw new InvalidOperationException("Incomplete events.");
                Validate(events);
                foreach (var mode in Enum.GetValues<Intervention>())
                {
                    Console.WriteLine(entry.Key + " / " + mode);
                    var contact = Restore(events[0].Before);
                    var delta = new Dictionary<string, double> { ["pre"] = 0, ["post"] = 0, ["modulation"] = 0 };
                    int lostUpdates = 0; double spentWithNoStoredChange = 0;
                    using var trace = new StreamWriter(Path.Combine(outputDirectory, entry.Key + "-" + mode + ".jsonl"));
                    var compact = new JsonSerializerOptions(Json) { WriteIndented = false };
                    for (int i = 0; i < events.Length; ++i)
                    {
                        var e = events[i]; var before = MemoryContactEvidence.From(contact);
                        if (mode == Intervention.Recorded && before != e.Before)
                            throw new InvalidOperationException($"Recorded before-state mismatch: {entry.Key}/{i}.");
                        if (e.Kind is "pre" or "post")
                            ContactPlasticity.Spike(contact, Rule, e.Kind == "pre", e.TimestampMs, e.Activity);
                        else
                            ContactPlasticity.Modulate(contact, mode == Intervention.NoTagCapture ? "STDP" : Rule,
                                e.TimestampMs, e.ElapsedMs, new NeuromodState
                                {
                                    DopamineLevel = mode == Intervention.NoDopamine ? 0 : e.Dopamine,
                                    AcetylcholineLevel = mode == Intervention.NoAcetylcholine ? 0 : e.Acetylcholine
                                }, e.Teaching, e.Activity);
                        var after = MemoryContactEvidence.From(contact);
                        if (mode == Intervention.Recorded && after != e.After)
                            throw new InvalidOperationException($"Recorded after-state mismatch: {entry.Key}/{i}.");
                        double change = (double)after.Weight - before.Weight;
                        delta[e.Kind] += change;
                        double spent = after.AbsoluteChange - before.AbsoluteChange;
                        if (spent > 0 && change == 0) { ++lostUpdates; spentWithNoStoredChange += spent; }
                        trace.WriteLine(JsonSerializer.Serialize(new { index = i, e.Kind, e.MessageId, e.TimestampMs,
                            e.ElapsedMs, e.Activity, e.Teaching, recordedDopamine = e.Dopamine,
                            recordedAcetylcholine = e.Acetylcholine, intervention = mode.ToString(), before, after }, compact));
                    }
                    var final = MemoryContactEvidence.From(contact);
                    replays.Add(new { entry.Key, intervention = mode.ToString(), eventCount = events.Length,
                        preEvents = events.Count(e => e.Kind == "pre"), postEvents = events.Count(e => e.Kind == "post"),
                        modulationEvents = events.Count(e => e.Kind == "modulation"), initial = events[0].Before, final,
                        weightChangeByKind = delta, spentWithNoStoredChange, lostUpdates,
                        recordedStatesExactlyReproduced = mode == Intervention.Recorded,
                        spikesAndActivityHeldFixed = true, recurrentNetworkResimulated = false });
                }
            }
            // Controlled cadence study of the CURRENT reduced rule. Its closed
            // form omits implementation dead zones/float rounding. Disagreement
            // is measured, not made into a new passing biological criterion.
            foreach (string pairing in new[] { "pre-post", "post-pre", "unpaired" })
            foreach (double step in new[] { 4d, 2, 1, .5, .25, .125, .0625, .03125, .015625 })
            {
                var contact = new SynapseState(StableNeuralIdentity.Create("contact-cadence", pairing), NTEnum.GLUTAMATE, 1);
                if (pairing != "unpaired")
                {
                    ContactPlasticity.Spike(contact, "SynapticTaggingCapture", pairing == "pre-post", 10, 0);
                    ContactPlasticity.Spike(contact, "SynapticTaggingCapture", pairing != "pre-post", 20, 0);
                }
                else ContactPlasticity.DecayTo(contact, 20);
                var initial = MemoryContactEvidence.From(contact);
                var modulation = new NeuromodState { AcetylcholineLevel = 1, DopamineLevel = 1 };
                int count = checked((int)(1000 / step));
                for (int i = 1; i <= count; ++i)
                    ContactPlasticity.Modulate(contact, "SynapticTaggingCapture", 20 + i * step, step, modulation, 0, 0);
                // dw/ds = .35*.02*tag0*exp(-s/8)*(1-w/5) for a positive
                // tag, or the same coefficient times w/5 for a negative tag.
                // Initial tag is declared from the recorded float state; this
                // is a continuous-limit calculation, not an independent fit.
                double tauSeconds = ContactPlasticity.TagTauMs / 1000d;
                double integral = (double).35f * PlasticityRules.RestoredSpikeDensityLearningScale * initial.Tag *
                    tauSeconds * (1 - Math.Exp(-1 / tauSeconds)) / 5;
                double continuousLimit = initial.Tag >= 0 ? 5 - (5 - initial.Weight) * Math.Exp(-integral)
                    : initial.Weight * Math.Exp(integral);
                var final = MemoryContactEvidence.From(contact);
                if (pairing == "unpaired" && (final.Weight != 1 || final.Tag != 0 || final.Eligibility != 0))
                    throw new InvalidOperationException("Modulation changed an unpaired blank contact.");
                cadence.Add(new { pairing, stepMs = step, durationMs = 1000, initial, final, continuousLimit,
                    absoluteWeightDifference = Math.Abs(final.Weight - continuousLimit),
                    continuousLimitExcludesDeadZonesAndRounding = true, biologicalCalibration = false });
            }
            Save("results.json", new { completed = true, replayCases = replays, cadenceCases = cadence,
                sourceLearningRuleChanged = false, biologicalConsolidationQualified = false, originalTestRepaired = false,
                liveBrainAccessed = false, avatarRecoveryAssessed = false });
            Console.WriteLine("Contact replay complete: recorded states reproduced; fixed-spike interventions and cadence differences saved.");
        }
        catch (Exception error)
        {
            Save("FAILED.json", new { error = error.ToString(), replayCases = replays, cadenceCases = cadence });
            throw;
        }
    }

    private static SynapseState Restore(MemoryContactEvidence c) => new(c.ContactId, c.Neurotransmitter, c.Weight, c.Baseline)
    {
        ProjectionId = c.ProjectionId, SourceInstanceKey = c.SourceInstance, SourceStructure = c.Source,
        SourceNeuronId = c.SourceNeuron, LastTargetNeuronIndex = c.Target, IsSyntheticBackground = c.Synthetic,
        PreTrace = c.PreTrace, PostTrace = c.PostTrace, EligibilityTrace = c.Eligibility, SynapticTagTrace = c.Tag,
        ThetaM = c.Theta, UpdateCount = c.Updates, LastUpdateTimestampMs = c.TimestampMs,
        PlasticityBudgetQuanta = c.Budget, LastPlasticityBudgetTimestampMs = c.BudgetTimestampMs,
        TotalAbsolutePlasticityChange = c.AbsoluteChange
    };

    private static void Validate(MemoryLearningEvidence[] events)
    {
        var first = events[0].Before;
        for (int i = 0; i < events.Length; ++i)
        {
            var e = events[i]; var before = e.Before; var after = e.After;
            if (e.Kind is not ("pre" or "post" or "modulation") || !double.IsFinite(e.TimestampMs) ||
                !double.IsFinite(e.ElapsedMs) || e.ElapsedMs < 0 || e.TimestampMs < before.TimestampMs ||
                after.TimestampMs != e.TimestampMs || e.Kind == "pre" && e.MessageId is null ||
                e.Kind != "pre" && e.MessageId is not null ||
                !float.IsFinite(e.Activity) || !float.IsFinite(e.Teaching) || !float.IsFinite(e.Dopamine) || !float.IsFinite(e.Acetylcholine))
                throw new InvalidOperationException("Invalid learning event.");
            foreach (var state in new[] { before, after })
            {
                if (state.ContactId != first.ContactId || state.ProjectionId != first.ProjectionId ||
                    state.Source != StructureId.EntorhinalCortex || state.SourceInstance != first.SourceInstance ||
                    state.SourceNeuron != "memory-feature-140" || state.Target != 166 ||
                    state.Neurotransmitter != NTEnum.GLUTAMATE || state.Synthetic || state.Baseline != 1 ||
                    MemoryContactEvidence.From(Restore(state)) != state)
                    throw new InvalidOperationException("Unexpected or incomplete cue contact identity/state.");
            }
            if (i > 0 && events[i - 1].After != before)
                throw new InvalidOperationException("Unrecorded contact change between events.");
        }
    }
}
