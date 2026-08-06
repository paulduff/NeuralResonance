using System.Globalization;
using System.Text.Json;
using System.Text;

namespace NRE.WpfEditor;

// Telemetry/state JSON formatters and parse helpers used by the UI.
// Pure static methods producing display strings from JsonElement input.
// Extracted from MainWindow.xaml.cs.
public partial class MainWindow
{
    private static string FormatConnectomeReport(JsonElement report)
    {
        var tick = GetLong(report, "generatedAtTick");
        var structureCount = 0;
        var sourcesWithOutbound = 0;
        var targetsWithInbound = 0;
        var projectionCount = 0;
        var feedbackProjectionCount = 0;
        var bidirectionalCoverage = false;
        var missingAsSource = Array.Empty<string>();
        var missingAsTarget = Array.Empty<string>();

        if (TryGetProperty(report, "coverage", out var coverage) && coverage.ValueKind == JsonValueKind.Object)
        {
            structureCount = GetInt(coverage, "structureCount");
            sourcesWithOutbound = GetInt(coverage, "sourcesWithOutbound");
            targetsWithInbound = GetInt(coverage, "targetsWithInbound");
            projectionCount = GetInt(coverage, "projectionCount");
            feedbackProjectionCount = GetInt(coverage, "feedbackProjectionCount");
            bidirectionalCoverage = TryGetProperty(coverage, "bidirectionalCoverage", out var coverageBool) &&
                                    coverageBool.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                                    coverageBool.GetBoolean();

            if (TryGetProperty(coverage, "missingAsSource", out var missingSourcesArray) && missingSourcesArray.ValueKind == JsonValueKind.Array)
            {
                missingAsSource = missingSourcesArray
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }

            if (TryGetProperty(coverage, "missingAsTarget", out var missingTargetsArray) && missingTargetsArray.ValueKind == JsonValueKind.Array)
            {
                missingAsTarget = missingTargetsArray
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }
        }

        var neurotransmitterBreakdown = "-";
        if (TryGetProperty(report, "neurotransmitterDistribution", out var ntDistribution) && ntDistribution.ValueKind == JsonValueKind.Object)
        {
            var ntPairs = ntDistribution.EnumerateObject()
                .Select(p =>
                {
                    var count = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var value)
                        ? value
                        : (p.Value.ValueKind == JsonValueKind.String && int.TryParse(p.Value.GetString(), out var parsed) ? parsed : 0);
                    return $"{p.Name}={count}";
                })
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
            if (ntPairs.Length > 0)
            {
                neurotransmitterBreakdown = string.Join(", ", ntPairs);
            }
        }

        var pathwayClassBreakdown = "-";
        if (TryGetProperty(report, "pathwayClassDistribution", out var classDistribution) && classDistribution.ValueKind == JsonValueKind.Object)
        {
            var classPairs = classDistribution.EnumerateObject()
                .Select(p =>
                {
                    var count = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var value)
                        ? value
                        : (p.Value.ValueKind == JsonValueKind.String && int.TryParse(p.Value.GetString(), out var parsed) ? parsed : 0);
                    return new { Name = p.Name, Count = count };
                })
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .Take(10)
                .Select(p => $"{p.Name}={p.Count}")
                .ToArray();
            if (classPairs.Length > 0)
            {
                pathwayClassBreakdown = string.Join(", ", classPairs);
            }
        }

        var driftStatus = "UNKNOWN";
        var nonOkServices = 0;
        var activePathways = 0;
        var routeDrops = 0;
        var queueDroppedSpikes = 0;
        var dispatchErrors = 0;
        var warnings = Array.Empty<string>();
        if (TryGetProperty(report, "drift", out var drift) && drift.ValueKind == JsonValueKind.Object)
        {
            driftStatus = GetString(drift, "status");
            nonOkServices = GetInt(drift, "nonOkServices");
            activePathways = GetInt(drift, "activePathways");
            queueDroppedSpikes = GetInt(drift, "queueDroppedSpikes");
            dispatchErrors = GetInt(drift, "dispatchErrors");

            if (TryGetProperty(drift, "routeDrops", out var routeDropsElement) && routeDropsElement.ValueKind == JsonValueKind.Object)
            {
                routeDrops = GetInt(routeDropsElement, "noConnectivity")
                             + GetInt(routeDropsElement, "noTarget")
                             + GetInt(routeDropsElement, "targetUnavailable")
                             + GetInt(routeDropsElement, "backpressure");
            }

            if (TryGetProperty(drift, "warnings", out var warningsArray) && warningsArray.ValueKind == JsonValueKind.Array)
            {
                warnings = warningsArray
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Take(8)
                    .ToArray();
            }
        }

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Coverage: {(bidirectionalCoverage ? "PASS" : "FAIL")} | structures {sourcesWithOutbound}/{structureCount} outbound, {targetsWithInbound}/{structureCount} inbound",
            $"Projections: total {projectionCount} | feedback-tagged {feedbackProjectionCount}",
            $"Missing as source: {(missingAsSource.Length == 0 ? "-" : string.Join(", ", missingAsSource))}",
            $"Missing as target: {(missingAsTarget.Length == 0 ? "-" : string.Join(", ", missingAsTarget))}",
            string.Empty,
            "Neurotransmitter distribution:",
            $"  {neurotransmitterBreakdown}",
            string.Empty,
            "Pathway classes (top 10):",
            $"  {pathwayClassBreakdown}",
            string.Empty,
            $"Drift status: {driftStatus}",
            $"  non-OK services: {nonOkServices} | active pathways: {activePathways}",
            $"  route drops: {routeDrops} | queue dropped spikes: {queueDroppedSpikes} | dispatch errors: {dispatchErrors}",
            $"  warnings: {(warnings.Length == 0 ? "-" : string.Join(" | ", warnings))}"
        });
    }

    private static string FormatVisualAttention(JsonElement root)
    {
        if (!TryGetProperty(root, "visualAttention", out var attention) || attention.ValueKind != JsonValueKind.Object)
        {
            return "Visual attention unavailable: state payload missing visualAttention.";
        }

        var tick = GetLong(root, "tick");
        var authority = GetString(attention, "authority");
        var available = GetBool(attention, "available");
        var active = GetBool(attention, "active");
        var leftFieldDrive = GetDouble(attention, "leftFieldDrive");
        var rightFieldDrive = GetDouble(attention, "rightFieldDrive");
        var leftTrn = GetDouble(attention, "leftHemisphereTrnSuppression");
        var rightTrn = GetDouble(attention, "rightHemisphereTrnSuppression");
        var focusField = GetString(attention, "focusedField");
        var focusHemisphere = GetString(attention, "focusedHemisphere");
        var focusConfidence = GetDouble(attention, "focusConfidence");
        var selectionMargin = GetDouble(attention, "selectionMargin");
        var circuitCoverage = GetDouble(attention, "circuitCoverage");
        var sustainedSelectionTicks = GetLong(attention, "sustainedSelectionTicks");
        var lastSelectionTick = GetLong(attention, "lastSelectionTick");

        var focusFieldLabel = string.IsNullOrWhiteSpace(focusField) ? "neutral" : focusField;
        var focusHemiLabel = string.IsNullOrWhiteSpace(focusHemisphere) ? "M" : focusHemisphere;
        var targetHemisphere = focusHemiLabel is "L" or "R" ? focusHemiLabel : "both";
        var selectionAgeTicks = (tick > 0 && lastSelectionTick >= 0) ? Math.Max(0L, tick - lastSelectionTick) : -1;

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Authority: {(string.IsNullOrWhiteSpace(authority) ? "neuronal" : authority)}",
            $"Circuit observed: {(available ? "yes" : "no")} | active: {(active ? "yes" : "no")}",
            $"Circuit coverage: {circuitCoverage:P1}",
            $"Focused field: {focusFieldLabel}",
            $"Focused hemisphere (contralateral target): {targetHemisphere}",
            $"Focus confidence: {focusConfidence:0.000}",
            $"Selection margin: {selectionMargin:0.000}",
            $"Sustained neural winner: {sustainedSelectionTicks} ticks",
            $"Selection age: {(selectionAgeTicks >= 0 ? $"{selectionAgeTicks} ticks" : "n/a")}",
            string.Empty,
            "Bilateral field drive:",
            $"  Left field (right hemisphere): {leftFieldDrive:0.000}",
            $"  Right field (left hemisphere): {rightFieldDrive:0.000}",
            string.Empty,
            "TRN suppression:",
            $"  Left hemisphere:  {leftTrn:0.000}",
            $"  Right hemisphere: {rightTrn:0.000}",
            string.Empty,
            "Visual attention is read-only neuronal telemetry; no controller override is enabled."
        });
    }

    private static string FormatLimbicState(JsonElement root)
    {
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        var tick = GetLong(root, "tick");
        var simMs = GetDouble(root, "simulationClockMs");
        var stage = "unknown";
        var lastUpdatedTick = 0L;

        var salience = 0.0;
        var threat = 0.0;
        var interoceptive = 0.0;
        var aversive = 0.0;
        var hippocampalContext = 0.0;
        var expectedReward = 0.0;
        var observedReward = 0.0;
        var valence = 0.0;
        var rewardPredictionError = 0.0;

        var currentDopamine = 0.0;
        var currentSerotonin = 0.0;
        var currentAcetylcholine = 0.0;
        var currentNorepinephrine = 0.0;

        var targetDopamine = 0.0;
        var targetSerotonin = 0.0;
        var targetAcetylcholine = 0.0;
        var targetNorepinephrine = 0.0;

        if (TryGetProperty(root, "limbicState", out var limbicState) && limbicState.ValueKind == JsonValueKind.Object)
        {
            stage = GetString(limbicState, "stage");
            lastUpdatedTick = GetLong(limbicState, "lastUpdatedTick");
            salience = GetDouble(limbicState, "salience");
            threat = GetDouble(limbicState, "threat");
            interoceptive = GetDouble(limbicState, "interoceptiveDrive");
            aversive = GetDouble(limbicState, "aversiveDrive");
            hippocampalContext = GetDouble(limbicState, "hippocampalContext");
            expectedReward = GetDouble(limbicState, "expectedReward");
            observedReward = GetDouble(limbicState, "observedReward");
            valence = GetDouble(limbicState, "valence");
            rewardPredictionError = GetDouble(limbicState, "rewardPredictionError");

            targetDopamine = GetDouble(limbicState, "dopamineTarget");
            targetSerotonin = GetDouble(limbicState, "serotoninTarget");
            targetAcetylcholine = GetDouble(limbicState, "acetylcholineTarget");
            targetNorepinephrine = GetDouble(limbicState, "norepinephrineTarget");

            if (TryGetProperty(root, "globalNeuromodState", out var globalNeuromod) && globalNeuromod.ValueKind == JsonValueKind.Object)
            {
                currentDopamine = GetDouble(globalNeuromod, "dopamineLevel");
                currentSerotonin = GetDouble(globalNeuromod, "serotoninLevel");
                currentAcetylcholine = GetDouble(globalNeuromod, "acetylcholineLevel");
                currentNorepinephrine = GetDouble(globalNeuromod, "norepinephrineLevel");
            }
        }
        else if (TryGetProperty(root, "limbic", out var limbic) && limbic.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(limbic, "drives", out var nestedDrives) && nestedDrives.ValueKind == JsonValueKind.Object)
            {
                stage = GetString(limbic, "stage");
                lastUpdatedTick = GetLong(limbic, "lastUpdatedTick");
                salience = GetDouble(nestedDrives, "salience");
                threat = GetDouble(nestedDrives, "threat");
                interoceptive = GetDouble(nestedDrives, "interoceptiveDrive");
                aversive = GetDouble(nestedDrives, "aversiveDrive");
                hippocampalContext = GetDouble(nestedDrives, "hippocampalContext");
                expectedReward = GetDouble(nestedDrives, "expectedReward");
                observedReward = GetDouble(nestedDrives, "observedReward");
                valence = GetDouble(nestedDrives, "valence");
                rewardPredictionError = GetDouble(nestedDrives, "rewardPredictionError");
            }
            else
            {
                stage = GetString(limbic, "stage");
                lastUpdatedTick = GetLong(limbic, "lastUpdatedTick");
                salience = GetDouble(limbic, "salience");
                threat = GetDouble(limbic, "threat");
                interoceptive = GetDouble(limbic, "interoceptiveDrive");
                aversive = GetDouble(limbic, "aversiveDrive");
                hippocampalContext = GetDouble(limbic, "hippocampalContext");
                expectedReward = GetDouble(limbic, "expectedReward");
                observedReward = GetDouble(limbic, "observedReward");
                valence = GetDouble(limbic, "valence");
                rewardPredictionError = GetDouble(limbic, "rewardPredictionError");
            }

            targetDopamine = GetDouble(limbic, "dopamineTarget");
            targetSerotonin = GetDouble(limbic, "serotoninTarget");
            targetAcetylcholine = GetDouble(limbic, "acetylcholineTarget");
            targetNorepinephrine = GetDouble(limbic, "norepinephrineTarget");

            if (TryGetProperty(limbic, "neuromod", out var limbicNeuromod) && limbicNeuromod.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(limbicNeuromod, "current", out var current) && current.ValueKind == JsonValueKind.Object)
                {
                    currentDopamine = GetDouble(current, "dopamineLevel");
                    currentSerotonin = GetDouble(current, "serotoninLevel");
                    currentAcetylcholine = GetDouble(current, "acetylcholineLevel");
                    currentNorepinephrine = GetDouble(current, "norepinephrineLevel");
                }

                if (TryGetProperty(limbicNeuromod, "targets", out var targets) && targets.ValueKind == JsonValueKind.Object)
                {
                    targetDopamine = GetDouble(targets, "dopamineTarget");
                    targetSerotonin = GetDouble(targets, "serotoninTarget");
                    targetAcetylcholine = GetDouble(targets, "acetylcholineTarget");
                    targetNorepinephrine = GetDouble(targets, "norepinephrineTarget");
                }
            }
            else if (TryGetProperty(root, "globalNeuromodState", out var globalNeuromod) && globalNeuromod.ValueKind == JsonValueKind.Object)
            {
                currentDopamine = GetDouble(globalNeuromod, "dopamineLevel");
                currentSerotonin = GetDouble(globalNeuromod, "serotoninLevel");
                currentAcetylcholine = GetDouble(globalNeuromod, "acetylcholineLevel");
                currentNorepinephrine = GetDouble(globalNeuromod, "norepinephrineLevel");
            }
        }
        else if (TryGetProperty(root, "drives", out var drives) && drives.ValueKind == JsonValueKind.Object)
        {
            stage = GetString(root, "stage");
            lastUpdatedTick = GetLong(root, "lastUpdatedTick");
            salience = GetDouble(drives, "salience");
            threat = GetDouble(drives, "threat");
            interoceptive = GetDouble(drives, "interoceptiveDrive");
            aversive = GetDouble(drives, "aversiveDrive");
            hippocampalContext = GetDouble(drives, "hippocampalContext");
            expectedReward = GetDouble(drives, "expectedReward");
            observedReward = GetDouble(drives, "observedReward");
            valence = GetDouble(drives, "valence");
            rewardPredictionError = GetDouble(drives, "rewardPredictionError");

            if (TryGetProperty(root, "neuromod", out var neuromod) && neuromod.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(neuromod, "current", out var current) && current.ValueKind == JsonValueKind.Object)
                {
                    currentDopamine = GetDouble(current, "dopamineLevel");
                    currentSerotonin = GetDouble(current, "serotoninLevel");
                    currentAcetylcholine = GetDouble(current, "acetylcholineLevel");
                    currentNorepinephrine = GetDouble(current, "norepinephrineLevel");
                }

                if (TryGetProperty(neuromod, "targets", out var targets) && targets.ValueKind == JsonValueKind.Object)
                {
                    targetDopamine = GetDouble(targets, "dopamineTarget");
                    targetSerotonin = GetDouble(targets, "serotoninTarget");
                    targetAcetylcholine = GetDouble(targets, "acetylcholineTarget");
                    targetNorepinephrine = GetDouble(targets, "norepinephrineTarget");
                }
            }
        }
        else if (TryGetProperty(root, "stage", out var stageProp) && stageProp.ValueKind == JsonValueKind.String)
        {
            stage = GetString(root, "stage");
            lastUpdatedTick = GetLong(root, "lastUpdatedTick");
            salience = GetDouble(root, "salience");
            threat = GetDouble(root, "threat");
            interoceptive = GetDouble(root, "interoceptiveDrive");
            aversive = GetDouble(root, "aversiveDrive");
            hippocampalContext = GetDouble(root, "hippocampalContext");
            expectedReward = GetDouble(root, "expectedReward");
            observedReward = GetDouble(root, "observedReward");
            valence = GetDouble(root, "valence");
            rewardPredictionError = GetDouble(root, "rewardPredictionError");
            targetDopamine = GetDouble(root, "dopamineTarget");
            targetSerotonin = GetDouble(root, "serotoninTarget");
            targetAcetylcholine = GetDouble(root, "acetylcholineTarget");
            targetNorepinephrine = GetDouble(root, "norepinephrineTarget");

            if (TryGetProperty(root, "globalNeuromodState", out var globalNeuromod) && globalNeuromod.ValueKind == JsonValueKind.Object)
            {
                currentDopamine = GetDouble(globalNeuromod, "dopamineLevel");
                currentSerotonin = GetDouble(globalNeuromod, "serotoninLevel");
                currentAcetylcholine = GetDouble(globalNeuromod, "acetylcholineLevel");
                currentNorepinephrine = GetDouble(globalNeuromod, "norepinephrineLevel");
            }
        }
        else
        {
            return "Limbic telemetry unavailable: state payload missing limbic telemetry fields.";
        }

        stage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
        var deltaDopamine = targetDopamine - currentDopamine;
        var deltaSerotonin = targetSerotonin - currentSerotonin;
        var deltaAcetylcholine = targetAcetylcholine - currentAcetylcholine;
        var deltaNorepinephrine = targetNorepinephrine - currentNorepinephrine;

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Simulation ms: {simMs:0.0}",
            $"Stage: {stage}",
            $"Last updated tick: {(lastUpdatedTick > 0 ? lastUpdatedTick : "n/a")}",
            string.Empty,
            "Limbic drives:",
            $"  Salience:            {salience:0.000}",
            $"  Threat:              {threat:0.000}",
            $"  Interoceptive:       {interoceptive:0.000}",
            $"  Aversive:            {aversive:0.000}",
            $"  Hippocampal context: {hippocampalContext:0.000}",
            $"  Expected reward:     {expectedReward:0.000}",
            $"  Observed reward:     {observedReward:0.000}",
            $"  Valence:             {valence:0.000}",
            $"  RPE:                 {rewardPredictionError:0.000}",
            string.Empty,
            "Neuromod (current -> target | delta):",
            $"  Dopamine:       {currentDopamine:0.000} -> {targetDopamine:0.000} | {deltaDopamine:+0.000;-0.000;0.000}",
            $"  Serotonin:      {currentSerotonin:0.000} -> {targetSerotonin:0.000} | {deltaSerotonin:+0.000;-0.000;0.000}",
            $"  Acetylcholine:  {currentAcetylcholine:0.000} -> {targetAcetylcholine:0.000} | {deltaAcetylcholine:+0.000;-0.000;0.000}",
            $"  Norepinephrine: {currentNorepinephrine:0.000} -> {targetNorepinephrine:0.000} | {deltaNorepinephrine:+0.000;-0.000;0.000}"
        });
    }

    private static string FormatBrainDashboard(JsonElement root)
    {
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        if (!TryGetProperty(root, "brainBehavior", out var dashboard) || dashboard.ValueKind != JsonValueKind.Object)
        {
            return "Brain dashboard unavailable: state payload missing brainBehavior.";
        }

        var tick = GetLong(dashboard, "tick");
        if (tick <= 0)
        {
            tick = GetLong(root, "tick");
        }

        TryGetProperty(dashboard, "sleep", out var sleep);
        TryGetProperty(dashboard, "drives", out var drives);
        TryGetProperty(dashboard, "body", out var body);
        TryGetProperty(dashboard, "language", out var language);
        TryGetProperty(dashboard, "sensory", out var sensory);
        TryGetProperty(dashboard, "cerebellum", out var cerebellum);
        TryGetProperty(dashboard, "consolidation", out var consolidation);

        var sleeping = sleep.ValueKind == JsonValueKind.Object && GetBool(sleep, "isSleeping");
        var sleepPressure = sleep.ValueKind == JsonValueKind.Object ? GetDouble(sleep, "sleepPressure") : 0.0;
        var sleepPressureNorm = sleep.ValueKind == JsonValueKind.Object ? GetDouble(sleep, "pressureNormalized") : 0.0;
        var motorInhibition = sleep.ValueKind == JsonValueKind.Object ? GetDouble(sleep, "motorInhibition") : 0.0;
        var wakeInertia = sleep.ValueKind == JsonValueKind.Object ? GetInt(sleep, "wakeInertiaTicksRemaining") : 0;
        var tiredDrive = sleep.ValueKind == JsonValueKind.Object ? GetDouble(sleep, "tiredDrive") : 0.0;

        var stage = drives.ValueKind == JsonValueKind.Object ? GetString(drives, "stage") : "-";
        var threat = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "threat") : 0.0;
        var hungerThirst = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "hungerThirstDrive") : 0.0;
        var anxiety = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "environmentAnxiety") : 0.0;
        var darkness = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "darkness") : 0.0;
        var shelterNeed = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "shelterNeed") : 0.0;
        var hunger = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "hunger") : 0.0;
        var predatorThreat = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "predatorThreat") : 0.0;
        var inShelter = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "inShelter") : 0.0;
        var health = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "health") : 1.0;
        var shelterSafety = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "shelterSafety") : 0.0;
        var exposure = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "exposure") : 1.0;
        var fightIntent = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "fightIntent") : 0.0;
        var flightIntent = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "flightIntent") : 0.0;
        var shelterIntent = drives.ValueKind == JsonValueKind.Object ? GetDouble(drives, "shelterIntent") : 0.0;

        var forwardVelocity = body.ValueKind == JsonValueKind.Object ? GetDouble(body, "forwardVelocity") : 0.0;
        var turnRate = body.ValueKind == JsonValueKind.Object ? GetDouble(body, "turnRateDeg") : 0.0;
        var leftMotor = body.ValueKind == JsonValueKind.Object ? GetDouble(body, "leftMotorDrive") : 0.0;
        var rightMotor = body.ValueKind == JsonValueKind.Object ? GetDouble(body, "rightMotorDrive") : 0.0;
        var contact = body.ValueKind == JsonValueKind.Object ? GetDouble(body, "contactLevel") : 0.0;

        var commandKey = language.ValueKind == JsonValueKind.Object ? GetString(language, "commandKey") : string.Empty;
        var intent = language.ValueKind == JsonValueKind.Object ? GetString(language, "intent") : string.Empty;
        var motorDirective = language.ValueKind == JsonValueKind.Object ? GetString(language, "motorDirective") : string.Empty;
        var utterance = language.ValueKind == JsonValueKind.Object ? GetString(language, "utterance") : string.Empty;
        var languageStrength = language.ValueKind == JsonValueKind.Object ? GetDouble(language, "strength") : 0.0;
        var activeSource = sensory.ValueKind == JsonValueKind.Object ? GetString(sensory, "activeSource") : "unknown";
        var avatarVision = sensory.ValueKind == JsonValueKind.Object && GetBool(sensory, "avatarVisionEnabled");
        var spontaneous = sensory.ValueKind == JsonValueKind.Object && GetBool(sensory, "spontaneousSpikingEnabled");
        var cerebellarWindow = cerebellum.ValueKind == JsonValueKind.Object ? GetInt(cerebellum, "recentWindowTicks") : 0;
        var cerebellarInput = cerebellum.ValueKind == JsonValueKind.Object ? GetInt(cerebellum, "recentInputSpikes") : 0;
        var cerebellarOutput = cerebellum.ValueKind == JsonValueKind.Object ? GetInt(cerebellum, "recentOutputSpikes") : 0;
        var cerebellarLastTick = cerebellum.ValueKind == JsonValueKind.Object ? GetLong(cerebellum, "lastSpikeTick") : long.MinValue;
        var consolidationAuthority = consolidation.ValueKind == JsonValueKind.Object ? GetString(consolidation, "authority") : "-";
        var sleepCircuitObserved = consolidation.ValueKind == JsonValueKind.Object && GetBool(consolidation, "circuitObserved");
        var neuronalReplayActive = consolidation.ValueKind == JsonValueKind.Object && GetBool(consolidation, "replayActive");
        var neuronalReplayEnsemble = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "replayEnsemble") : -1;

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Stage: {(string.IsNullOrWhiteSpace(stage) ? "-" : stage)}",
            $"Sleep: {(sleeping ? "asleep" : "awake")} | pressure {sleepPressure:0.000} ({sleepPressureNorm:P0}) | motor inhibition {motorInhibition:0.000} | wake inertia {wakeInertia}",
            $"Sensory source: {activeSource} | avatar vision {(avatarVision ? "on" : "off")} | spontaneous {(spontaneous ? "on" : "off")}",
            string.Empty,
            "Drives:",
            $"  Hunger/thirst: {hungerThirst:0.000} | tired: {tiredDrive:0.000} | darkness: {darkness:0.000}",
            $"  Threat:        {threat:0.000} | anxiety: {anxiety:0.000} | shelter need: {shelterNeed:0.000}",
            $"  World body:    hunger {hunger:0.000} | predator {predatorThreat:0.000} | health {health:0.000}",
            $"  Safety:        in shelter {inShelter:0.000} | shelter safety {shelterSafety:0.000} | exposure {exposure:0.000}",
            $"  Fight:         {fightIntent:0.000} | flight: {flightIntent:0.000} | shelter intent: {shelterIntent:0.000}",
            string.Empty,
            "Body and motor:",
            $"  Forward velocity: {forwardVelocity:0.000} | turn rate: {turnRate:0.000} deg | contact: {contact:0.000}",
            $"  Motor drive L/R:  {leftMotor:0.000} / {rightMotor:0.000}",
            string.Empty,
            "Cerebellum:",
            $"  Recent window: {cerebellarWindow} ticks | input spikes: {cerebellarInput} | output spikes: {cerebellarOutput}",
            $"  Last cerebellar spike tick: {(cerebellarLastTick > 0 ? cerebellarLastTick.ToString() : "n/a")}",
            string.Empty,
            "Neuronal memory consolidation:",
            $"  Authority: {BlankAsDash(consolidationAuthority)} | circuit {(sleepCircuitObserved ? "observed" : "not observed")}",
            $"  Replay: {(neuronalReplayActive ? $"ensemble {neuronalReplayEnsemble}" : "idle")}",
            string.Empty,
            "Language:",
            $"  Intent: {BlankAsDash(intent)} | command: {BlankAsDash(commandKey)} | directive: {BlankAsDash(motorDirective)} | strength: {languageStrength:0.000}",
            $"  Narration: {BlankAsDash(utterance)}"
        });
    }

    private static string FormatCircuitAudit(JsonElement root)
    {
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        if (!TryGetProperty(root, "circuitAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
        {
            return "Circuit audit unavailable: state payload missing circuitAudit.";
        }

        TryGetProperty(audit, "summary", out var summary);
        TryGetProperty(audit, "warnings", out var warnings);
        TryGetProperty(audit, "functionSupport", out var functionSupport);
        TryGetProperty(audit, "items", out var items);

        var lines = new StringBuilder(4096);
        if (summary.ValueKind == JsonValueKind.Object)
        {
            lines.AppendLine($"Tick: {GetLong(summary, "tick")} | window: {GetLong(summary, "recentWindowTicks")} ticks");
            lines.AppendLine($"Structures: {GetInt(summary, "structureCount")} | OK: {GetInt(summary, "okCount")} | warnings: {GetInt(summary, "warningCount")} | notices: {GetInt(summary, "noticeCount")}");
            lines.AppendLine($"Silent: {GetInt(summary, "silentCount")} | disconnected: {GetInt(summary, "disconnectedCount")} | input-no-output: {GetInt(summary, "receivesInputNoOutputCount")} | alive-idle: {GetInt(summary, "aliveNotParticipatingCount")}");
            lines.AppendLine($"Never spiked: {GetInt(summary, "neverSpikedCount")} | no-route: {GetInt(summary, "noRouteCount")} | visible-disconnected: {GetInt(summary, "registeredDisconnectedCount")} | route-without-service: {GetInt(summary, "connectomeWithoutServiceCount")}");
            lines.AppendLine($"Functions: {GetInt(summary, "functionCount")} | active: {GetInt(summary, "activeFunctionCount")} | weak: {GetInt(summary, "weakFunctionCount")} | unsupported: {GetInt(summary, "unsupportedFunctionCount")} | mean support: {GetDouble(summary, "functionSupportMean"):0.000}");
            lines.AppendLine($"Spikes generated/routed/delivered: {GetInt(summary, "generatedSpikes")} / {GetInt(summary, "routedSpikes")} / {GetInt(summary, "deliveredSpikes")} | active pathways: {GetInt(summary, "activePathways")}");
        }
        else
        {
            lines.AppendLine("Circuit audit summary unavailable.");
        }

        AppendMotorPathwayAudit(lines, items);

        lines.AppendLine();
        lines.AppendLine("Action warnings:");
        if (warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() == 0)
        {
            lines.AppendLine("  -");
        }
        else
        {
            var index = 1;
            foreach (var warning in warnings.EnumerateArray().Where(IsWarningFinding).Take(24))
            {
                if (warning.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var structure = GetString(warning, "structure");
                var severity = GetString(warning, "severity");
                var serviceStatus = GetString(warning, "serviceStatus");
                var recentInput = GetInt(warning, "recentInputSpikes");
                var recentOutput = GetInt(warning, "recentOutputSpikes");
                var lifetimeInput = GetInt(warning, "lifetimeInputSpikes");
                var lifetimeOutput = GetInt(warning, "lifetimeOutputSpikes");
                var incomingRoutes = GetInt(warning, "incomingRoutes");
                var outgoingRoutes = GetInt(warning, "outgoingRoutes");
                var issueText = ReadStringArray(warning, "issues");
                lines.AppendLine($"{index,2}. {structure} [{BlankAsDash(severity)}] {issueText}");
                lines.AppendLine($"    routes in/out {incomingRoutes}/{outgoingRoutes} | recent spikes in/out {recentInput}/{recentOutput} | lifetime in/out {lifetimeInput}/{lifetimeOutput} | service {BlankAsDash(serviceStatus)}");
                index++;
            }

            if (index == 1)
            {
                lines.AppendLine("  -");
            }
        }

        lines.AppendLine();
        lines.AppendLine("Quiet notices:");
        if (warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() == 0)
        {
            lines.AppendLine("  -");
        }
        else
        {
            var index = 1;
            foreach (var warning in warnings.EnumerateArray().Where(IsNoticeFinding).Take(16))
            {
                if (warning.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var structure = GetString(warning, "structure");
                var serviceStatus = GetString(warning, "serviceStatus");
                var recentInput = GetInt(warning, "recentInputSpikes");
                var recentOutput = GetInt(warning, "recentOutputSpikes");
                var issueText = ReadStringArray(warning, "issues");
                var silenceCause = GetString(warning, "silenceCause");
                lines.AppendLine($"{index,2}. {structure} {issueText} | recent {recentInput}/{recentOutput} | service {BlankAsDash(serviceStatus)} | cause {BlankAsDash(silenceCause)}");
                index++;
            }

            if (index == 1)
            {
                lines.AppendLine("  -");
            }
        }

        lines.AppendLine();
        lines.AppendLine("Brain function circuit support:");
        if (functionSupport.ValueKind != JsonValueKind.Array || functionSupport.GetArrayLength() == 0)
        {
            lines.AppendLine("  -");
        }
        else
        {
            foreach (var entry in functionSupport.EnumerateArray().Take(24))
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var active = GetBool(entry, "active") ? "active" : "quiet";
                var warning = GetString(entry, "warning");
                var suffix = string.IsNullOrWhiteSpace(warning) ? string.Empty : $" | {warning}";
                lines.AppendLine($"  {GetString(entry, "displayName"),-34} {GetString(entry, "status"),-11} support {GetDouble(entry, "support"):0.000} | {active}{suffix}");
            }
        }

        lines.AppendLine();
        lines.AppendLine("Most quiet participating circuits:");
        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
        {
            lines.AppendLine("  -");
        }
        else
        {
            var shown = 0;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var issueText = ReadStringArray(item, "issues");
                if (issueText == "-")
                {
                    continue;
                }

                lines.AppendLine($"  {GetString(item, "structure"),-28} in/out spikes {GetInt(item, "recentInputSpikes"),4}/{GetInt(item, "recentOutputSpikes"),4} | {issueText}");
                shown++;
                if (shown >= 20)
                {
                    break;
                }
            }

            if (shown == 0)
            {
                lines.AppendLine("  -");
            }
        }

        return lines.ToString().TrimEnd();
    }

    private static bool IsWarningFinding(JsonElement finding)
        => finding.ValueKind == JsonValueKind.Object &&
           GetString(finding, "severity").Equals("warn", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoticeFinding(JsonElement finding)
        => finding.ValueKind == JsonValueKind.Object &&
           !GetString(finding, "severity").Equals("warn", StringComparison.OrdinalIgnoreCase);

    private static readonly (string Label, string[] Structures)[] MotorAuditStages =
    [
        ("PFC", ["Pfc"]),
        ("ACC", ["Acc"]),
        ("PM", ["PremotorCortex"]),
        ("Str", ["Striatum"]),
        ("STN", ["Stn"]),
        ("GPi/SNr", ["GPi", "Snr"]),
        ("MThal", ["MotorThalamus"]),
        ("SMA", ["Sma"]),
        ("M1", ["M1"]),
        ("DCN", ["DeepCerebellarNuclei"]),
        ("Spinal", ["SpinalCordMotor"])
    ];

    private static void AppendMotorPathwayAudit(StringBuilder lines, JsonElement items)
    {
        lines.AppendLine();
        lines.AppendLine("Motor pathway chain:");
        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
        {
            lines.AppendLine("  unavailable");
            return;
        }

        var byStructure = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var structure = GetString(item, "structure");
            if (!string.IsNullOrWhiteSpace(structure))
            {
                byStructure[structure] = item;
            }
        }

        var parts = new List<string>(MotorAuditStages.Length);
        var activeFlags = new List<bool>(MotorAuditStages.Length);
        foreach (var stage in MotorAuditStages)
        {
            var recentInput = 0;
            var recentOutput = 0;
            var serviceOk = false;
            foreach (var structure in stage.Structures)
            {
                if (!byStructure.TryGetValue(structure, out var item))
                {
                    continue;
                }

                recentInput += GetInt(item, "recentInputSpikes");
                recentOutput += GetInt(item, "recentOutputSpikes");
                serviceOk |= GetString(item, "serviceStatus").Equals("OK", StringComparison.OrdinalIgnoreCase);
            }

            var active = recentInput > 0 || recentOutput > 0;
            activeFlags.Add(active);
            var status = active ? $"{recentInput}/{recentOutput}" : serviceOk ? "quiet" : "down";
            parts.Add($"{stage.Label} {status}");
        }

        lines.AppendLine($"  {string.Join(" | ", parts)}");
        lines.AppendLine($"  Break: {ResolveMotorAuditBreak(activeFlags)}");
    }

    private static string ResolveMotorAuditBreak(IReadOnlyList<bool> activeFlags)
    {
        var anyActive = false;
        for (var i = 0; i < activeFlags.Count && i < MotorAuditStages.Length; i++)
        {
            if (activeFlags[i])
            {
                anyActive = true;
                continue;
            }

            if (anyActive)
            {
                return $"near {MotorAuditStages[i].Label}";
            }
        }

        return anyActive ? "descending chain active" : "chain quiet";
    }

    private static string FormatProsodyTelemetry(JsonElement root)
    {
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        JsonElement payload = default;
        if (TryGetProperty(root, "prosodyTelemetry", out var embeddedProsody) && embeddedProsody.ValueKind == JsonValueKind.Object)
        {
            payload = embeddedProsody;
        }
        else if (TryGetProperty(root, "languageBridge", out var languageBridgeProbe) && languageBridgeProbe.ValueKind == JsonValueKind.Object)
        {
            payload = root;
        }
        else if (TryGetProperty(root, "backoff", out var backoffProbe) && backoffProbe.ValueKind == JsonValueKind.Object)
        {
            payload = root;
        }
        else
        {
            return "Prosody telemetry unavailable: state payload missing prosody telemetry fields.";
        }

        var tick = GetLong(payload, "tick");
        var simMs = GetDouble(payload, "simulationClockMs");
        if (tick <= 0)
        {
            tick = GetLong(root, "tick");
        }

        if (simMs <= 0.0)
        {
            simMs = GetDouble(root, "simulationClockMs");
        }

        var sleeping = false;
        var sleepPressure = 0.0;
        var atpBudget = 0.0;
        if (TryGetProperty(payload, "sleep", out var sleep) && sleep.ValueKind == JsonValueKind.Object)
        {
            sleeping = GetBool(sleep, "neuronalSleepObserved");
            sleepPressure = GetDouble(sleep, "homeostaticPressure");
            atpBudget = GetDouble(sleep, "atpBudget");
        }
        else if (TryGetProperty(root, "metabolicPhysiology", out var physiology) && physiology.ValueKind == JsonValueKind.Object)
        {
            sleeping = GetBool(physiology, "neuronalSleepObserved");
            sleepPressure = GetDouble(physiology, "homeostaticPressure");
            atpBudget = GetDouble(physiology, "atpBudget");
        }

        var stage = "-";
        var salience = 0.0;
        var threat = 0.0;
        var valence = 0.0;
        var rpe = 0.0;
        if (TryGetProperty(payload, "limbic", out var limbic) && limbic.ValueKind == JsonValueKind.Object)
        {
            stage = GetString(limbic, "stage");
            salience = GetDouble(limbic, "salience");
            threat = GetDouble(limbic, "threat");
            valence = GetDouble(limbic, "valence");
            rpe = GetDouble(limbic, "rewardPredictionError");
        }
        else if (TryGetProperty(root, "limbicState", out var limbicState) && limbicState.ValueKind == JsonValueKind.Object)
        {
            stage = GetString(limbicState, "stage");
            salience = GetDouble(limbicState, "salience");
            threat = GetDouble(limbicState, "threat");
            valence = GetDouble(limbicState, "valence");
            rpe = GetDouble(limbicState, "rewardPredictionError");
        }

        var dopamine = 0.0;
        var serotonin = 0.0;
        var acetylcholine = 0.0;
        var norepinephrine = 0.0;
        if (TryGetProperty(payload, "neuromod", out var neuromod) && neuromod.ValueKind == JsonValueKind.Object)
        {
            dopamine = GetDouble(neuromod, "dopamineLevel");
            serotonin = GetDouble(neuromod, "serotoninLevel");
            acetylcholine = GetDouble(neuromod, "acetylcholineLevel");
            norepinephrine = GetDouble(neuromod, "norepinephrineLevel");
        }
        else if (TryGetProperty(root, "globalNeuromodState", out var globalNeuromod) && globalNeuromod.ValueKind == JsonValueKind.Object)
        {
            dopamine = GetDouble(globalNeuromod, "dopamineLevel");
            serotonin = GetDouble(globalNeuromod, "serotoninLevel");
            acetylcholine = GetDouble(globalNeuromod, "acetylcholineLevel");
            norepinephrine = GetDouble(globalNeuromod, "norepinephrineLevel");
        }

        var generated = 0;
        var delivered = 0;
        var dispatchErrors = 0;
        var lastError = "-";
        if (TryGetProperty(payload, "languageBridge", out var languageBridge) && languageBridge.ValueKind == JsonValueKind.Object)
        {
            generated = GetInt(languageBridge, "perceptionLanguageGenerated");
            delivered = GetInt(languageBridge, "perceptionLanguageDelivered");
            dispatchErrors = GetInt(languageBridge, "perceptionLanguageDispatchErrors");
            lastError = GetString(languageBridge, "perceptionLanguageLastError");
        }
        else if (TryGetProperty(root, "transportStats", out var transportStats) && transportStats.ValueKind == JsonValueKind.Object)
        {
            generated = GetInt(transportStats, "perceptionLanguageGenerated");
            delivered = GetInt(transportStats, "perceptionLanguageDelivered");
            dispatchErrors = GetInt(transportStats, "perceptionLanguageDispatchErrors");
            lastError = GetString(transportStats, "perceptionLanguageLastError");
        }

        if (string.IsNullOrWhiteSpace(lastError))
        {
            lastError = "-";
        }

        var attempts = 0L;
        var resolved = 0L;
        var fallbackSelections = 0L;
        var backoffDispatchErrors = 0L;
        var modeStateCount = 0;
        var graphCount = 0;
        var edgeCount = 0;
        var topEdgeSummary = "-";
        if (TryGetProperty(payload, "backoff", out var backoff) && backoff.ValueKind == JsonValueKind.Object)
        {
            attempts = GetLong(backoff, "languageBackoffAttempts");
            resolved = GetLong(backoff, "languageBackoffResolved");
            fallbackSelections = GetLong(backoff, "languageBackoffFallbackSelections");
            backoffDispatchErrors = GetLong(backoff, "languageBackoffDispatchErrors");

            if (TryGetProperty(backoff, "modeStates", out var modeStates) && modeStates.ValueKind == JsonValueKind.Array)
            {
                modeStateCount = modeStates.GetArrayLength();
            }

            if (TryGetProperty(backoff, "graphs", out var graphs) && graphs.ValueKind == JsonValueKind.Array)
            {
                graphCount = graphs.GetArrayLength();
            }

            if (TryGetProperty(backoff, "edges", out var edges) && edges.ValueKind == JsonValueKind.Array)
            {
                edgeCount = edges.GetArrayLength();
                var first = edges.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    var source = GetString(first, "source");
                    var target = GetString(first, "target");
                    var edgeDelivered = GetLong(first, "deliveredSpikes");
                    var edgeErrors = GetLong(first, "dispatchErrors");
                    topEdgeSummary = $"{source} -> {target} | delivered {edgeDelivered} | errors {edgeErrors}";
                }
            }
        }
        else if (TryGetProperty(root, "transportStats", out var rootTransportStats) && rootTransportStats.ValueKind == JsonValueKind.Object)
        {
            attempts = GetLong(rootTransportStats, "languageBackoffAttempts");
            resolved = GetLong(rootTransportStats, "languageBackoffResolved");
            fallbackSelections = GetLong(rootTransportStats, "languageBackoffFallbackSelections");
            backoffDispatchErrors = GetLong(rootTransportStats, "languageBackoffDispatchErrors");
            if (TryGetProperty(rootTransportStats, "languageBackoffModeStates", out var modeStates) && modeStates.ValueKind == JsonValueKind.Array)
            {
                modeStateCount = modeStates.GetArrayLength();
            }

            if (TryGetProperty(rootTransportStats, "languageBackoffGraphs", out var graphs) && graphs.ValueKind == JsonValueKind.Array)
            {
                graphCount = graphs.GetArrayLength();
            }

            if (TryGetProperty(rootTransportStats, "languageBackoffTopEdges", out var edges) && edges.ValueKind == JsonValueKind.Array)
            {
                edgeCount = edges.GetArrayLength();
            }
        }

        var deliveryRatio = generated > 0 ? (double)delivered / generated : 0.0;
        var safeStage = string.IsNullOrWhiteSpace(stage) ? "-" : stage;

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Simulation ms: {simMs:0.0}",
            $"Sleep: {(sleeping ? "sleeping" : "awake")} | ATP {atpBudget:0.000} | pressure {sleepPressure:0.000}",
            string.Empty,
            $"Limbic stage: {safeStage} | sal {salience:0.000} | thr {threat:0.000} | val {valence:0.000} | rpe {rpe:0.000}",
            $"Neuromod: DA {dopamine:0.000} | 5-HT {serotonin:0.000} | ACh {acetylcholine:0.000} | NE {norepinephrine:0.000}",
            string.Empty,
            "Perception-language bridge:",
            $"  Generated: {generated}",
            $"  Delivered: {delivered} ({deliveryRatio:0.000})",
            $"  Dispatch errors: {dispatchErrors}",
            $"  Last error: {lastError}",
            string.Empty,
            "Prosody backoff:",
            $"  Attempts: {attempts} | Resolved: {resolved} | Fallback picks: {fallbackSelections} | Dispatch errors: {backoffDispatchErrors}",
            $"  Mode states: {modeStateCount} | Graphs: {graphCount} | Edges: {edgeCount}",
            $"  Top edge: {topEdgeSummary}"
        });
    }
    private static string FormatReasoningState(JsonElement root)
    {
        root = NormalizeStateRoot(root);
        var tick = GetLong(root, "tick");
        var hasAuthority = TryGetObject(root, "cognitionAuthority", out var authority);
        var hasAttention = TryGetObject(root, "neuronalAttentionWorkspace", out var attention);
        var hasExecutive = TryGetObject(root, "neuronalExecutive", out var executive);
        var hasMemory = TryGetObject(root, "neuronalMemory", out var memory);
        var hasSleep = TryGetObject(root, "neuronalSleepConsolidation", out var sleep);

        if (!hasAuthority && !hasAttention && !hasExecutive && !hasMemory && !hasSleep)
        {
            return "Neuronal cognition telemetry unavailable.";
        }

        var maintainedChannels = TryGetProperty(attention, "maintainedChannels", out var maintained) &&
                                 maintained.ValueKind == JsonValueKind.Array
            ? maintained.GetArrayLength()
            : 0;

        return string.Join(Environment.NewLine, new[]
        {
            "Neuronal cognition",
            $"Tick: {tick}",
            $"Authority: {BlankAsDash(GetString(authority, "authority"))}",
            $"Symbolic authorization: {(GetBool(authority, "symbolicScaffoldCanAuthorize") ? "ENABLED" : "disabled")}",
            $"Semantic motor injection: {(GetBool(authority, "semanticMotorInjectionAllowed") ? "ENABLED" : "disabled")}",
            $"World-goal steering: {(GetBool(authority, "worldGoalSteeringAllowed") ? "ENABLED" : "disabled")}",
            string.Empty,
            $"Attention workspace: {(GetBool(attention, "active") ? "active" : "quiet")} | channel {GetInt(attention, "selectedChannel")} | margin {GetDouble(attention, "selectionMargin"):0.000}",
            $"Maintained channels: {maintainedChannels} | broadcast {GetInt(attention, "broadcastChannel")} | coverage {GetDouble(attention, "circuitCoverage"):0.000}",
            $"Executive circuit: {(GetBool(executive, "active") ? "active" : "quiet")} | committed {GetBool(executive, "committed")} | action {GetInt(executive, "selectedActionChannel")}",
            $"Executive context: {GetInt(executive, "maintainedContextChannel")} | stability {GetDouble(executive, "taskSetStability"):0.000} | confidence {GetDouble(executive, "confidence"):0.000}",
            string.Empty,
            $"Synaptic memory: {(GetBool(memory, "recallActive") ? "recalling" : "quiet")} | ensemble {GetInt(memory, "recalledEnsemble")} | strength {GetDouble(memory, "recallStrength"):0.000}",
            $"Learned synapses: {GetInt(memory, "learnedSynapseCount")} | engram {GetDouble(memory, "engramStrength"):0.000} | consolidation {GetDouble(memory, "corticalConsolidation"):0.000}",
            $"Sleep circuit: {(GetBool(sleep, "stateActive") ? "active" : "quiet")} | state {GetInt(sleep, "state")} | confidence {GetDouble(sleep, "stateConfidence"):0.000}",
            $"Neuronal replay: {(GetBool(sleep, "replayActive") ? "active" : "quiet")} | ensemble {GetInt(sleep, "replayEnsemble")} | strength {GetDouble(sleep, "replayStrength"):0.000}"
        });
    }
    private static string FormatBrainTelemetry(JsonElement root)
    {
        root = NormalizeStateRoot(root);
        var tick = GetLong(root, "tick");
        var hasPerception = TryGetObject(root, "neuronalPerception", out var perception);
        var hasMemory = TryGetObject(root, "neuronalMemory", out var memory);
        var hasAttention = TryGetObject(root, "neuronalAttentionWorkspace", out var attention);
        var hasLanguage = TryGetObject(root, "neuronalLanguageGrounding", out var language);
        var hasMotor = TryGetObject(root, "neuronalMotor", out var motor);

        if (!hasPerception && !hasMemory && !hasAttention && !hasLanguage && !hasMotor)
        {
            return "Neuronal brain telemetry unavailable.";
        }

        return string.Join(Environment.NewLine, new[]
        {
            "Neuronal brain telemetry",
            $"Tick: {tick}",
            $"Perception: {(GetBool(perception, "active") ? "active" : "quiet")} | ensemble {GetInt(perception, "dominantEnsemble")} | confidence {GetDouble(perception, "confidence"):0.000}",
            $"Percept coverage/persistence/novelty: {GetDouble(perception, "circuitCoverage"):0.000} | {GetDouble(perception, "persistence"):0.000} | {GetDouble(perception, "novelty"):0.000}",
            $"Memory: {(GetBool(memory, "recallActive") ? "recalling" : "quiet")} | ensemble {GetInt(memory, "recalledEnsemble")} | strength {GetDouble(memory, "recallStrength"):0.000}",
            $"Attention: {(GetBool(attention, "active") ? "active" : "quiet")} | selected {GetInt(attention, "selectedChannel")} | broadcast {GetInt(attention, "broadcastChannel")}",
            string.Empty,
            $"Language circuit: observed {GetBool(language, "circuitObserved")} | available {GetBool(language, "available")} | grounded {GetBool(language, "grounded")}",
            $"Grounded label: {BlankAsDash(GetString(language, "groundedLabel"))}",
            $"Language reference: percept {GetInt(language, "perceptEnsemble")} | memory {GetInt(language, "memoryEnsemble")} | attention {GetInt(language, "attentionChannel")}",
            $"Comprehension/expression: {GetDouble(language, "comprehensionDrive"):0.000} | {GetDouble(language, "expressionDrive"):0.000}",
            $"Grounding confidence/uncertainty: {GetDouble(language, "groundingConfidence"):0.000} | {GetDouble(language, "uncertainty"):0.000}",
            $"Speech authorized by circuit: {GetBool(language, "speechAuthorized")}",
            string.Empty,
            $"Motor: {(GetBool(motor, "active") ? "active" : "quiet")} | selected action {GetInt(motor, "selectedActionChannel")} | confidence {GetDouble(motor, "confidence"):0.000}",
            $"Drive L/R/F/T: {GetDouble(motor, "leftDrive"):0.000} | {GetDouble(motor, "rightDrive"):0.000} | {GetDouble(motor, "forwardDrive"):0.000} | {GetDouble(motor, "turnDrive"):0.000}",
            $"Motor/action coverage: {GetDouble(motor, "motorCircuitCoverage"):0.000} | {GetDouble(motor, "actionCircuitCoverage"):0.000}"
        });
    }
    private static string FormatInhabitanceTelemetry(JsonElement root)
    {
        root = NormalizeStateRoot(root);
        var tick = GetLong(root, "tick");
        var hasBody = TryGetObject(root, "bodyState", out var body);
        var hasAffect = TryGetObject(root, "neuronalAffectValuation", out var affect);
        var hasMotor = TryGetObject(root, "neuronalMotor", out var motor);

        if (!hasBody && !hasAffect && !hasMotor)
        {
            return "Embodied neuronal telemetry unavailable.";
        }

        return string.Join(Environment.NewLine, new[]
        {
            "Embodied neuronal interface",
            $"Tick: {tick}",
            "Body values below are raw receptor substrate, not cognitive decisions.",
            $"Body velocity/turn/contact: {GetDouble(body, "forwardVelocity"):0.000} | {GetDouble(body, "turnRateDeg"):0.000} | {GetDouble(body, "contactLevel"):0.000}",
            $"Touch front/left/right/ground: {GetDouble(body, "tactileFront"):0.000} | {GetDouble(body, "tactileLeft"):0.000} | {GetDouble(body, "tactileRight"):0.000} | {GetDouble(body, "tactileGround"):0.000}",
            $"Interoception hunger/health/pain: {GetDouble(body, "hunger"):0.000} | {GetDouble(body, "health"):0.000} | {GetDouble(body, "painLevel"):0.000}",
            $"Observed motor L/R/asymmetry: {GetDouble(body, "leftMotorDrive"):0.000} | {GetDouble(body, "rightMotorDrive"):0.000} | {GetDouble(body, "motorAsymmetry"):0.000}",
            string.Empty,
            $"Neuronal valuation: {(GetBool(affect, "active") ? "active" : "quiet")} | channel {GetInt(affect, "dominantChannel")} | confidence {GetDouble(affect, "confidence"):0.000}",
            $"Appetitive/defensive/homeostatic/exploratory: {GetDouble(affect, "appetitiveDrive"):0.000} | {GetDouble(affect, "defensiveDrive"):0.000} | {GetDouble(affect, "homeostaticDrive"):0.000} | {GetDouble(affect, "exploratoryDrive"):0.000}",
            $"Valence +/- and arousal: {GetDouble(affect, "positiveValence"):0.000} | {GetDouble(affect, "negativeValence"):0.000} | {GetDouble(affect, "arousal"):0.000}",
            $"Neuronal motor output: {(GetBool(motor, "active") ? "active" : "quiet")} | forward {GetDouble(motor, "forwardDrive"):0.000} | turn {GetDouble(motor, "turnDrive"):0.000}"
        });
    }

    private static JsonElement NormalizeStateRoot(JsonElement root)
    {
        return TryGetObject(root, "state", out var nestedState) ? nestedState : root;
    }

    private static bool TryGetObject(JsonElement root, string name, out JsonElement value)
    {
        if (TryGetProperty(root, name, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string FormatTransportStats(JsonElement root)
    {
        var tick = GetLong(root, "tick");
        var simMs = GetDouble(root, "simulationClockMs");
        var tickDuration = GetDouble(root, "tickDurationMs");
        var profileName = GetString(root, "performanceProfileName");
        var serviceCount = GetInt(root, "serviceCount");
        var lastSnapshotTick = GetLong(root, "lastSnapshotTick");
        var lastSnapshotSimMs = GetDouble(root, "lastSnapshotSimulationMs");
        var lastSnapshotWallClockUnixMs = GetLong(root, "lastSnapshotWallClockUnixMs");
        var snapshotAgeTicks = lastSnapshotTick > 0 && tick >= lastSnapshotTick ? tick - lastSnapshotTick : -1;
        var snapshotAgeMs = lastSnapshotSimMs > 0 && simMs >= lastSnapshotSimMs ? simMs - lastSnapshotSimMs : -1.0;
        var snapshotWallClockText = lastSnapshotWallClockUnixMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastSnapshotWallClockUnixMs).ToLocalTime().ToString("HH:mm:ss")
            : "n/a";

        var nonOk = 0;
        if (TryGetProperty(root, "serviceTelemetry", out var telemetry) && telemetry.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in telemetry.EnumerateObject())
            {
                if (!TryGetProperty(entry.Value, "lastStatus", out var statusProp) || statusProp.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var status = statusProp.GetString() ?? string.Empty;
                if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    nonOk++;
                }
            }
        }

        var activeServices = 0;
        var successfulAcks = 0;
        var drainCalls = 0;
        var drainedSpikes = 0;
        var dispatchedSpikes = 0;
        var droppedByBudget = 0;
        var topQueries = 0;
        var spontaneousGenerated = 0;
        var spontaneousDelivered = 0;
        var spontaneousDispatchErrors = 0;
        var totalSpontaneousGenerated = 0L;
        var totalSpontaneousDelivered = 0L;
        var totalSpontaneousDispatchErrors = 0L;
        var spontaneousLastError = string.Empty;
        var activePathways = 0;
        var dispatchQueueQueuedBatches = 0;
        var dispatchQueueQueuedSpikes = 0;
        var dispatchQueuePeakBatches = 0;
        var dispatchQueuePeakSpikes = 0;
        var dispatchQueueDroppedBatches = 0;
        var dispatchQueueDroppedSpikes = 0;
        var dispatchQueueFlushedBatches = 0;
        var dispatchQueueFlushActiveTargets = 0;
        var dispatchQueueFlushMaxTargetBurstSpikes = 0;
        var dispatchQueueDispatchErrors = 0;
        var dispatchQueueLastError = string.Empty;
        var generatedSpikes = 0;
        var routedSpikes = 0;
        var deliveredSpikes = 0;
        var routeDroppedNoConnectivity = 0;
        var routeDroppedNoTargets = 0;
        var routeDroppedTargetUnavailable = 0;
        var routeDroppedByBackpressure = 0;
        var adaptivePressure = 0.0;
        var adaptiveScale = 1.0;
        var effectivePerService = 0;
        var effectivePerTick = 0;
        var effectiveTopQueries = 0;
        var effectiveAckTimeoutMs = 0;
        var effectiveIoTimeoutMs = 0;
        var effectivePublishWaitMs = 0;
        var effectivePublishSettleMs = 0;
        var ackLatencyEwmaMs = 0.0;
        var ackLatencyLt100Ms = 0;
        var ackLatency100To250Ms = 0;
        var ackLatency250To500Ms = 0;
        var ackLatency500To1000Ms = 0;
        var ackLatencyGte1000Ms = 0;
        var tickWallMs = 0.0;
        var tickWallP50Ms = 0.0;
        var tickWallP95Ms = 0.0;
        var tickWallP99Ms = 0.0;
        var degradeSignal = "none";
        var sleepReplayStage = "awake";
        var sleepInhibitoryScale = 1.0;
        var sleepExcitatoryScale = 1.0;
        var perceptionLanguageGenerated = 0;
        var perceptionLanguageDelivered = 0;
        var perceptionLanguageDispatchErrors = 0;
        var perceptionLanguageLastError = string.Empty;
        var languageBackoff = LanguageBackoffDisplay.Empty;
        if (TryGetProperty(root, "transportStats", out var transport) && transport.ValueKind == JsonValueKind.Object)
        {
            activeServices = GetInt(transport, "activeServices");
            successfulAcks = GetInt(transport, "successfulAcks");
            drainCalls = GetInt(transport, "drainCalls");
            drainedSpikes = GetInt(transport, "drainedSpikes");
            dispatchedSpikes = GetInt(transport, "dispatchedSpikes");
            droppedByBudget = GetInt(transport, "droppedByBudget");
            topQueries = GetInt(transport, "topQueries");
            spontaneousGenerated = GetInt(transport, "spontaneousGenerated");
            spontaneousDelivered = GetInt(transport, "spontaneousDelivered");
            spontaneousDispatchErrors = GetInt(transport, "spontaneousDispatchErrors");
            totalSpontaneousGenerated = GetLong(transport, "totalSpontaneousGenerated");
            totalSpontaneousDelivered = GetLong(transport, "totalSpontaneousDelivered");
            totalSpontaneousDispatchErrors = GetLong(transport, "totalSpontaneousDispatchErrors");
            spontaneousLastError = GetString(transport, "spontaneousLastError");
            activePathways = GetInt(transport, "activePathways");
            dispatchQueueQueuedBatches = GetInt(transport, "dispatchQueueQueuedBatches");
            dispatchQueueQueuedSpikes = GetInt(transport, "dispatchQueueQueuedSpikes");
            dispatchQueuePeakBatches = GetInt(transport, "dispatchQueuePeakBatches");
            dispatchQueuePeakSpikes = GetInt(transport, "dispatchQueuePeakSpikes");
            dispatchQueueDroppedBatches = GetInt(transport, "dispatchQueueDroppedBatches");
            dispatchQueueDroppedSpikes = GetInt(transport, "dispatchQueueDroppedSpikes");
            dispatchQueueFlushedBatches = GetInt(transport, "dispatchQueueFlushedBatches");
            dispatchQueueFlushActiveTargets = GetInt(transport, "dispatchQueueFlushActiveTargets");
            dispatchQueueFlushMaxTargetBurstSpikes = GetInt(transport, "dispatchQueueFlushMaxTargetBurstSpikes");
            dispatchQueueDispatchErrors = GetInt(transport, "dispatchQueueDispatchErrors");
            dispatchQueueLastError = GetString(transport, "dispatchQueueLastError");
            generatedSpikes = GetInt(transport, "generatedSpikes");
            routedSpikes = GetInt(transport, "routedSpikes");
            deliveredSpikes = GetInt(transport, "deliveredSpikes");
            routeDroppedNoConnectivity = GetInt(transport, "routeDroppedNoConnectivity");
            routeDroppedNoTargets = GetInt(transport, "routeDroppedNoTargets");
            routeDroppedTargetUnavailable = GetInt(transport, "routeDroppedTargetUnavailable");
            routeDroppedByBackpressure = GetInt(transport, "routeDroppedByBackpressure");
            adaptivePressure = GetDouble(transport, "adaptivePressure");
            adaptiveScale = GetDouble(transport, "adaptiveScale");
            effectivePerService = GetInt(transport, "effectiveMaxSpikeDispatchPerServicePerTick");
            effectivePerTick = GetInt(transport, "effectiveMaxSpikeDispatchTotalPerTick");
            effectiveTopQueries = GetInt(transport, "effectiveMaxTopQueriesPerTick");
            effectiveAckTimeoutMs = GetInt(transport, "effectiveTickAckTimeoutMs");
            effectiveIoTimeoutMs = GetInt(transport, "effectiveTickIoTimeoutMs");
            effectivePublishWaitMs = GetInt(transport, "effectiveTickPublishWaitMs");
            effectivePublishSettleMs = GetInt(transport, "effectiveTickPublishSettleMs");
            ackLatencyEwmaMs = GetDouble(transport, "ackLatencyEwmaMs");
            ackLatencyLt100Ms = GetInt(transport, "ackLatencyLt100Ms");
            ackLatency100To250Ms = GetInt(transport, "ackLatency100To250Ms");
            ackLatency250To500Ms = GetInt(transport, "ackLatency250To500Ms");
            ackLatency500To1000Ms = GetInt(transport, "ackLatency500To1000Ms");
            ackLatencyGte1000Ms = GetInt(transport, "ackLatencyGte1000Ms");
            tickWallMs = GetDouble(transport, "tickWallMs");
            tickWallP50Ms = GetDouble(transport, "tickWallP50Ms");
            tickWallP95Ms = GetDouble(transport, "tickWallP95Ms");
            tickWallP99Ms = GetDouble(transport, "tickWallP99Ms");
            degradeSignal = GetString(transport, "degradeSignal");
            sleepReplayStage = GetString(transport, "sleepReplayStage");
            sleepInhibitoryScale = GetDouble(transport, "sleepInhibitoryScale");
            sleepExcitatoryScale = GetDouble(transport, "sleepExcitatoryScale");
            perceptionLanguageGenerated = GetInt(transport, "perceptionLanguageGenerated");
            perceptionLanguageDelivered = GetInt(transport, "perceptionLanguageDelivered");
            perceptionLanguageDispatchErrors = GetInt(transport, "perceptionLanguageDispatchErrors");
            perceptionLanguageLastError = GetString(transport, "perceptionLanguageLastError");
            languageBackoff = ParseLanguageBackoffDisplay(transport);
        }

        var snapshotStatus = lastSnapshotTick <= 0
            ? "none yet"
            : $"tick {lastSnapshotTick} @ {lastSnapshotSimMs:0.0} ms (wall {snapshotWallClockText})";
        var snapshotAgeStatus = snapshotAgeTicks < 0
            ? "n/a"
            : $"{snapshotAgeTicks} ticks ({snapshotAgeMs:0.0} ms sim)";

        var sleepStateLabel = "awake";
        var atpBudget = 0.0;
        var homeostaticPressure = 0.0;
        var wakeTicks = 0;
        var sleepTicks = 0;
        var neuronalCircuitObserved = false;
        var neuronalState = "Wake";
        var neuronalStateConfidence = 0.0;
        var neuronalReplayActive = false;
        var neuronalReplayEnsemble = -1;
        if (TryGetProperty(root, "metabolicPhysiology", out var physiology) && physiology.ValueKind == JsonValueKind.Object)
        {
            sleepStateLabel = GetBool(physiology, "neuronalSleepObserved") ? "sleeping" : "awake";
            atpBudget = GetDouble(physiology, "atpBudget");
            homeostaticPressure = GetDouble(physiology, "homeostaticPressure");
            wakeTicks = GetInt(physiology, "wakeTicks");
            sleepTicks = GetInt(physiology, "sleepTicks");
        }

        if (TryGetProperty(root, "neuronalSleepConsolidation", out var neuronalSleep) &&
            neuronalSleep.ValueKind == JsonValueKind.Object)
        {
            neuronalCircuitObserved = GetBool(neuronalSleep, "circuitObserved");
            neuronalState = GetString(neuronalSleep, "state");
            if (string.IsNullOrWhiteSpace(neuronalState))
            {
                neuronalState = GetInt(neuronalSleep, "state") switch
                {
                    1 => "Nrem",
                    2 => "Rem",
                    _ => "Wake"
                };
            }
            neuronalStateConfidence = GetDouble(neuronalSleep, "stateConfidence");
            neuronalReplayActive = GetBool(neuronalSleep, "replayActive");
            neuronalReplayEnsemble = GetInt(neuronalSleep, "replayEnsemble");
        }

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Simulation ms: {simMs:0.0} | Tick dt: {tickDuration:0.###} ms",
            $"Profile: {(string.IsNullOrWhiteSpace(profileName) ? "normal" : profileName)}",
            $"Services: {serviceCount} total | {nonOk} non-OK",
            $"Last snapshot: {snapshotStatus}",
            $"Snapshot age: {snapshotAgeStatus}",
            string.Empty,
            "Neuronal sleep and metabolic physiology:",
            $"  Neuronal state: {neuronalState} ({neuronalStateConfidence:0.000}) | circuit {(neuronalCircuitObserved ? "observed" : "not observed")}",
            $"  Physiology: {sleepStateLabel} | ATP {atpBudget:0.000} | pressure {homeostaticPressure:0.000}",
            $"  Observed duration: wake {wakeTicks} ticks | sleep {sleepTicks} ticks",
            $"  Neuronal replay: {(neuronalReplayActive ? $"ensemble {neuronalReplayEnsemble}" : "idle")}",
            string.Empty,
            "Transport (last tick):",
            $"  Active services: {activeServices}",
            $"  Successful acks: {successfulAcks}",
            $"  Drain calls: {drainCalls}",
            $"  Drained spikes: {drainedSpikes}",
            $"  Dispatched spikes: {dispatchedSpikes}",
            $"  Dropped by budget: {droppedByBudget}",
            $"  Top queries: {topQueries}",
            $"  Spontaneous generated: {spontaneousGenerated}",
            $"  Spontaneous delivered: {spontaneousDelivered}",
            $"  Spontaneous dispatch errors: {spontaneousDispatchErrors}",
            $"  Spontaneous totals: gen {totalSpontaneousGenerated} | del {totalSpontaneousDelivered} | err {totalSpontaneousDispatchErrors}",
            $"  Spontaneous last error: {(string.IsNullOrWhiteSpace(spontaneousLastError) ? "-" : spontaneousLastError)}",
            $"  Active pathways: {activePathways}",
            $"  Queue queued batches: {dispatchQueueQueuedBatches}",
            $"  Queue queued spikes: {dispatchQueueQueuedSpikes}",
            $"  Queue peak batches: {dispatchQueuePeakBatches}",
            $"  Queue peak spikes: {dispatchQueuePeakSpikes}",
            $"  Queue dropped batches: {dispatchQueueDroppedBatches}",
            $"  Queue dropped spikes: {dispatchQueueDroppedSpikes}",
            $"  Queue flushed batches: {dispatchQueueFlushedBatches}",
            $"  Queue flush active targets: {dispatchQueueFlushActiveTargets}",
            $"  Queue flush max target burst: {dispatchQueueFlushMaxTargetBurstSpikes}",
            $"  Queue dispatch errors: {dispatchQueueDispatchErrors}",
            $"  Queue last error: {(string.IsNullOrWhiteSpace(dispatchQueueLastError) ? "-" : dispatchQueueLastError)}",
            string.Empty,
            "Tick wall-time:",
            $"  Last: {tickWallMs:0.0} ms | p50: {tickWallP50Ms:0.0} ms | p95: {tickWallP95Ms:0.0} ms | p99: {tickWallP99Ms:0.0} ms",
            $"  Degrade signal: {(string.IsNullOrWhiteSpace(degradeSignal) ? "-" : degradeSignal)}",
            string.Empty,
            "Spike truth (last tick):",
            $"  Generated: {generatedSpikes}",
            $"  Routed: {routedSpikes}",
            $"  Delivered: {deliveredSpikes}",
            $"  Dropped (no connectivity): {routeDroppedNoConnectivity}",
            $"  Dropped (no target): {routeDroppedNoTargets}",
            $"  Dropped (target unavailable): {routeDroppedTargetUnavailable}",
            $"  Dropped (queue backpressure): {routeDroppedByBackpressure}",
            string.Empty,
            "Adaptive throttling:",
            $"  Pressure: {adaptivePressure:0.000} | scale: {adaptiveScale:0.000}",
            $"  Effective budgets: spikes/svc {effectivePerService}, spikes/tick {effectivePerTick}, topQueries {effectiveTopQueries}",
            $"  Effective timeouts: ack {effectiveAckTimeoutMs}ms, io {effectiveIoTimeoutMs}ms, publish wait {effectivePublishWaitMs}ms, settle {effectivePublishSettleMs}ms",
            string.Empty,
            "Language backoff:",
            $"  Attempts: {languageBackoff.Attempts} | resolved: {languageBackoff.Resolved} | fallbacks: {languageBackoff.FallbackSelections} | dispatch errors: {languageBackoff.DispatchErrors}",
            $"  Graphs: {languageBackoff.GraphsText}",
            $"  Modes: {languageBackoff.ModesText}",
            $"  Top edges: {languageBackoff.TopEdgesText}",
            string.Empty,
            "Ack latency buckets (cumulative):",
            $"  EWMA latency: {ackLatencyEwmaMs:0.0} ms",
            $"  <100ms: {ackLatencyLt100Ms}",
            $"  100-250ms: {ackLatency100To250Ms}",
            $"  250-500ms: {ackLatency250To500Ms}",
            $"  500-1000ms: {ackLatency500To1000Ms}",
            $"  >=1000ms: {ackLatencyGte1000Ms}",
            string.Empty,
            "Neuronal sleep modulation:",
            $"  Stage: {sleepReplayStage}",
            $"  Inhibitory scale: {sleepInhibitoryScale:0.000} | excitatory scale: {sleepExcitatoryScale:0.000}",
            string.Empty,
            "Perception-language conditioning:",
            $"  Generated: {perceptionLanguageGenerated} | delivered: {perceptionLanguageDelivered} | dispatch errors: {perceptionLanguageDispatchErrors}",
            $"  Last error: {(string.IsNullOrWhiteSpace(perceptionLanguageLastError) ? "-" : perceptionLanguageLastError)}"
        });
    }

    private static LanguageBackoffDisplay ParseLanguageBackoffDisplay(JsonElement transport)
    {
        var topEdgeLines = new List<string>(4);
        var graphLines = new List<string>(6);
        var modeLines = new List<string>(6);

        if (TryGetProperty(transport, "languageBackoffTopEdges", out var topEdges) && topEdges.ValueKind == JsonValueKind.Array)
        {
            foreach (var edge in topEdges.EnumerateArray().Take(4))
            {
                var mode = GetString(edge, "mode");
                var graphId = GetString(edge, "graphId");
                var source = GetString(edge, "source");
                var target = GetString(edge, "target");
                var delivered = GetLong(edge, "deliveredSpikes");
                var attempts = GetLong(edge, "attempts");
                var resolved = GetLong(edge, "resolved");
                var errors = GetLong(edge, "dispatchErrors");
                var marker = GetInt(edge, "rank") == 0 ? "*" : "-";
                topEdgeLines.Add(
                    $"{marker} {mode}/{graphId}: {source}->{target} | del {delivered} | res {resolved}/{attempts} | err {errors}");
            }
        }

        if (TryGetProperty(transport, "languageBackoffGraphs", out var graphs) && graphs.ValueKind == JsonValueKind.Array)
        {
            foreach (var graph in graphs.EnumerateArray().Take(6))
            {
                var mode = GetString(graph, "mode");
                var graphId = GetString(graph, "graphId");
                var isCurrent = false;
                if (TryGetProperty(graph, "isCurrent", out var isCurrentProp) &&
                    isCurrentProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    isCurrent = isCurrentProp.GetBoolean();
                }

                var composite = GetDouble(graph, "compositeScore");
                var score = GetDouble(graph, "scoreEwma");
                var delivered = GetLong(graph, "deliveredSpikes");
                var errors = GetLong(graph, "dispatchErrors");
                var marker = isCurrent ? ">" : "-";
                graphLines.Add(
                    $"{marker} {mode}/{graphId}: comp {composite:0.000}, ewma {score:0.000}, del {delivered}, err {errors}");
            }
        }

        if (TryGetProperty(transport, "languageBackoffModeStates", out var modeStates) && modeStates.ValueKind == JsonValueKind.Array)
        {
            foreach (var modeState in modeStates.EnumerateArray().Take(6))
            {
                var mode = GetString(modeState, "mode");
                var graph = GetString(modeState, "currentGraphId");
                var switched = GetLong(modeState, "lastSwitchTick");
                var evaluated = GetLong(modeState, "lastEvaluationTick");
                var resolvedTick = GetLong(modeState, "lastResolutionTick");
                modeLines.Add(
                    $"- {mode}: graph={graph} | switch {switched} | eval {evaluated} | resolve {resolvedTick}");
            }
        }

        return new LanguageBackoffDisplay(
            GetLong(transport, "languageBackoffAttempts"),
            GetLong(transport, "languageBackoffResolved"),
            GetLong(transport, "languageBackoffFallbackSelections"),
            GetLong(transport, "languageBackoffDispatchErrors"),
            topEdgeLines.Count == 0 ? "-" : string.Join(" || ", topEdgeLines),
            graphLines.Count == 0 ? "-" : string.Join(" || ", graphLines),
            modeLines.Count == 0 ? "-" : string.Join(" || ", modeLines));
    }


    private sealed record LanguageBackoffDisplay(
        long Attempts,
        long Resolved,
        long FallbackSelections,
        long DispatchErrors,
        string TopEdgesText,
        string GraphsText,
        string ModesText)
    {
        public static LanguageBackoffDisplay Empty { get; } = new(0, 0, 0, 0, "-", "-", "-");
    }

    private static string AppendFrameSpikeMetrics(string baseStatsText, FrameSpikeMetrics metrics)
    {
        var header = string.IsNullOrWhiteSpace(baseStatsText)
            ? "Transport stats unavailable."
            : baseStatsText;

        return string.Join(Environment.NewLine, new[]
        {
            header,
            string.Empty,
            "Spike truth chain:",
            $"  Transport generated/routed/delivered: {metrics.GeneratedSpikes}/{metrics.RoutedSpikes}/{metrics.DeliveredSpikes}",
            $"  Renderer highlighted neurons: {metrics.VisibleNeuronHighlights}",
            string.Empty,
            "Renderer (last frame):",
            $"  Dispatch traces: {metrics.DispatchTraceCount}",
            $"  Distinct neuron IDs: {metrics.DistinctNeuronIdCount}",
            $"  Structures with neuron-ID spikes: {metrics.StructuresWithNeuronSpikes}",
            $"  Visible neuron spikes: {metrics.VisibleNeuronHighlights}",
            $"  Unmatched neuron IDs: {metrics.UnmatchedNeuronIds}"
        });
    }
}
