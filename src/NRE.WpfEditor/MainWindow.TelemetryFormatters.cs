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
        var consolidationStage = consolidation.ValueKind == JsonValueKind.Object ? GetString(consolidation, "stage") : "-";
        var engramCount = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "engramCount") : 0;
        var replayReady = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "replayReadyEngrams") : 0;
        var protectedEngrams = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "protectedEngrams") : 0;
        var replaySupported = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "replaySupportedEngrams") : 0;
        var motorSuppressed = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "motorSuppressedEngrams") : 0;
        var schemaCount = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "schemaCount") : 0;
        var activeSchemas = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "activeSchemas") : 0;
        var replayGenerated = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "replayGenerated") : 0;
        var replayDelivered = consolidation.ValueKind == JsonValueKind.Object ? GetInt(consolidation, "replayDelivered") : 0;
        var deliveryRatio = consolidation.ValueKind == JsonValueKind.Object ? GetDouble(consolidation, "deliveryRatio") : 0.0;

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
            "Memory consolidation:",
            $"  Stage: {BlankAsDash(consolidationStage)} | engrams {engramCount} | replay-ready {replayReady} | protected {protectedEngrams}",
            $"  Replay-supported: {replaySupported} | motor-suppressed during sleep: {motorSuppressed}",
            $"  Schemas: {schemaCount} total | active {activeSchemas} | replay delivery {replayDelivered}/{replayGenerated} ({deliveryRatio:P0})",
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

    private static string FormatObjectMemoryState(JsonElement root)
    {
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        if (!TryGetProperty(root, "objectMemory", out var objectMemory) || objectMemory.ValueKind != JsonValueKind.Object)
        {
            return "Object memory unavailable: state payload missing objectMemory.";
        }

        var tick = GetLong(root, "tick");
        var simMs = GetDouble(root, "simulationClockMs");
        var count = GetInt(objectMemory, "count");
        var topList = TryGetProperty(objectMemory, "top", out var top) && top.ValueKind == JsonValueKind.Array
            ? top
            : default;

        var lines = new List<string>(48)
        {
            $"Tick: {tick}",
            $"Simulation ms: {simMs:0.0}",
            $"Object traces: {count}",
            string.Empty,
            "Most recent objects:"
        };

        if (topList.ValueKind != JsonValueKind.Array || topList.GetArrayLength() == 0)
        {
            lines.Add("  -");
            return string.Join(Environment.NewLine, lines);
        }

        var index = 1;
        foreach (var item in topList.EnumerateArray().Take(16))
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var objectId = GetString(item, "objectId");
            var label = GetString(item, "label");
            var hemisphere = GetString(item, "dominantHemisphere");
            var familiarity = GetDouble(item, "familiarity");
            var salienceEma = GetDouble(item, "salienceEma");
            var confidenceEma = GetDouble(item, "confidenceEma");
            var intensityEma = GetDouble(item, "intensityEma");
            var seenCount = GetInt(item, "seenCount");
            var lastSeenTick = GetLong(item, "lastSeenTick");
            var lastSeenMs = GetDouble(item, "lastSeenSimulationMs");

            lines.Add($"{index,2}. {label} [{objectId}] hemi={hemisphere} fam={familiarity:0.000} seen={seenCount}");
            lines.Add($"    sal={salienceEma:0.000} conf={confidenceEma:0.000} int={intensityEma:0.000} lastTick={lastSeenTick} lastMs={lastSeenMs:0.0}");
            index++;
        }

        return string.Join(Environment.NewLine, lines);
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
            sleeping = GetBool(sleep, "isSleeping");
            sleepPressure = GetDouble(sleep, "sleepPressure");
            atpBudget = GetDouble(sleep, "atpBudget");
        }
        else if (TryGetProperty(root, "sleepMemory", out var sleepMemory) && sleepMemory.ValueKind == JsonValueKind.Object)
        {
            sleeping = GetBool(sleepMemory, "isSleeping");
            sleepPressure = GetDouble(sleepMemory, "sleepPressure");
            atpBudget = GetDouble(sleepMemory, "atpBudget");
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
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        var tick = GetLong(root, "tick");
        var simMs = GetDouble(root, "simulationClockMs");

        var hasPlanning = TryGetProperty(root, "planningWorkspace", out var planning) && planning.ValueKind == JsonValueKind.Object;
        var hasCurriculum = TryGetProperty(root, "curriculum", out var curriculum) && curriculum.ValueKind == JsonValueKind.Object;
        var hasConsolidation = TryGetProperty(root, "consolidationControl", out var consolidation) && consolidation.ValueKind == JsonValueKind.Object;
        var hasWorldModel = TryGetProperty(root, "worldModel", out var worldModel) && worldModel.ValueKind == JsonValueKind.Object;
        var hasLanguageIntent = TryGetProperty(root, "languageIntent", out var languageIntent) && languageIntent.ValueKind == JsonValueKind.Object;
        var hasBrainNarration = TryGetProperty(root, "brainNarration", out var brainNarration) && brainNarration.ValueKind == JsonValueKind.Object;

        if (!hasPlanning && !hasCurriculum && !hasConsolidation && !hasWorldModel && !hasLanguageIntent && !hasBrainNarration)
        {
            return "Reasoning telemetry unavailable: state payload missing planning/curriculum/consolidation/worldModel fields.";
        }

        var planningLines = new List<string>();
        if (hasLanguageIntent || hasBrainNarration)
        {
            var active = hasLanguageIntent && GetBool(languageIntent, "active");
            var command = hasLanguageIntent ? GetString(languageIntent, "commandKey") : string.Empty;
            var motor = hasLanguageIntent ? GetString(languageIntent, "motorDirective") : string.Empty;
            var strength = hasLanguageIntent ? GetDouble(languageIntent, "strength") : 0.0;
            var utterance = hasBrainNarration ? GetString(brainNarration, "utterance") : string.Empty;
            var sequence = hasBrainNarration ? GetLong(brainNarration, "sequence") : 0L;
            var spokenEligible = hasBrainNarration && GetBool(brainNarration, "spokenEligible");
            var speechGate = hasBrainNarration ? GetDouble(brainNarration, "speechReleaseGate") : 0.0;
            var speechSuppression = hasBrainNarration ? GetDouble(brainNarration, "speechSuppression") : 0.0;
            planningLines.AddRange(new[]
            {
                "Language intent:",
                $"  Active: {active} | command: {(string.IsNullOrWhiteSpace(command) ? "-" : command)} | motor: {(string.IsNullOrWhiteSpace(motor) ? "-" : motor)}",
                $"  Strength: {strength:0.000}",
                $"  Brain narration: {(string.IsNullOrWhiteSpace(utterance) ? "-" : utterance)} | seq {sequence}",
                $"  Speech gate: {(spokenEligible ? "eligible" : "internal")} | release {speechGate:0.000} | suppress {speechSuppression:0.000}",
                string.Empty
            });
        }

        if (hasPlanning)
        {
            var goal = GetString(planning, "goal");
            var goalActive = GetBool(planning, "goalActive", true);
            var horizon = GetInt(planning, "horizonSteps");
            var branching = GetInt(planning, "maxBranching");
            var exploration = GetDouble(planning, "explorationTemperature");
            var dopamineBias = GetDouble(planning, "dopamineBias");
            var inhibitoryGate = GetDouble(planning, "inhibitoryGate");
            var selectedAction = GetString(planning, "selectedActionLabel");
            if (string.IsNullOrWhiteSpace(selectedAction))
            {
                selectedAction = GetString(planning, "selectedActionKey");
            }
            var selectedUtility = GetDouble(planning, "selectedUtility");
            var selectedConfidence = GetDouble(planning, "selectedConfidence");
            var lastPlanTick = GetLong(planning, "lastPlanTick");
            var revision = GetLong(planning, "planRevision");

            var candidateCount = 0;
            var topCandidates = new List<string>(4);
            if (TryGetProperty(planning, "candidateActions", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
            {
                candidateCount = candidates.GetArrayLength();
                foreach (var candidate in candidates.EnumerateArray().Take(4))
                {
                    var actionKey = GetString(candidate, "actionKey");
                    var readableAction = GetString(candidate, "readableAction");
                    var utility = GetDouble(candidate, "utility");
                    var confidence = GetDouble(candidate, "confidence");
                    var summary = GetString(candidate, "summary");
                    var preview = string.IsNullOrWhiteSpace(summary) ? actionKey : summary;
                    var actionLabel = string.IsNullOrWhiteSpace(readableAction) ? actionKey : readableAction;
                    if (preview.Length > 60)
                    {
                        preview = $"{preview[..60]}...";
                    }

                    topCandidates.Add(
                        $"{actionLabel} | util {utility:0.000} | conf {confidence:0.000} | {preview}");
                }
            }

            var proposedPlan = "-";
            if (TryGetProperty(planning, "proposedPlan", out var proposedPlanArray) && proposedPlanArray.ValueKind == JsonValueKind.Array)
            {
                var steps = proposedPlanArray
                    .EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Take(8)
                    .ToArray();
                proposedPlan = steps.Length == 0 ? "-" : string.Join(" -> ", steps);
            }

            planningLines.AddRange(new[]
            {
                "Planning workspace:",
                $"  Goal: {(string.IsNullOrWhiteSpace(goal) ? "-" : goal)}",
                $"  Goal active: {goalActive}",
                $"  Horizon: {horizon} | Branching: {branching}",
                $"  Exploration: {exploration:0.000} | Dopamine bias: {dopamineBias:0.000} | Inhibitory gate: {inhibitoryGate:0.000}",
                $"  Selected action: {selectedAction} | utility {selectedUtility:0.000} | confidence {selectedConfidence:0.000}",
                $"  Last plan tick: {(lastPlanTick > 0 ? lastPlanTick : "n/a")} | revision {revision}",
                $"  Candidate actions: {candidateCount}",
                topCandidates.Count == 0 ? "    -" : string.Join(Environment.NewLine, topCandidates.Select(line => $"    {line}")),
                $"  Proposed plan: {proposedPlan}"
            });
        }
        else
        {
            planningLines.Add("Planning workspace: unavailable");
        }

        var curriculumLines = new List<string>();
        if (hasCurriculum)
        {
            var enabled = GetBool(curriculum, "enabled", true);
            var stageIndex = GetInt(curriculum, "stageIndex");
            var stageName = GetString(curriculum, "stageName");
            var stageScore = GetDouble(curriculum, "stageScore");
            var stageProgress = GetDouble(curriculum, "stageProgress");
            var stageTicks = GetLong(curriculum, "stageTicks");
            var lastTransitionTick = GetLong(curriculum, "lastTransitionTick");
            var tasksCount = 0;
            var taskSummaries = new List<string>(4);
            if (TryGetProperty(curriculum, "tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
            {
                tasksCount = tasks.GetArrayLength();
                foreach (var task in tasks.EnumerateArray().Take(4))
                {
                    var taskName = GetString(task, "name");
                    var successRate = GetDouble(task, "successRate");
                    var samples = GetLong(task, "samples");
                    taskSummaries.Add($"{taskName}: {successRate:0.000} ({samples} samples)");
                }
            }

            curriculumLines.AddRange(new[]
            {
                "Curriculum:",
                $"  Enabled: {enabled}",
                $"  Stage: {stageIndex} ({stageName})",
                $"  Stage score/progress: {stageScore:0.000} / {stageProgress:0.000}",
                $"  Stage ticks: {stageTicks} | last transition: {(lastTransitionTick > 0 ? lastTransitionTick : "n/a")}",
                $"  Tasks: {tasksCount}",
                taskSummaries.Count == 0 ? "    -" : string.Join(Environment.NewLine, taskSummaries.Select(line => $"    {line}"))
            });
        }
        else
        {
            curriculumLines.Add("Curriculum: unavailable");
        }

        var consolidationLines = new List<string>();
        if (hasConsolidation)
        {
            var enabled = GetBool(consolidation, "enabled", true);
            var replayEarly = GetDouble(consolidation, "replayWeightEarlyHippocampal");
            var replayLate = GetDouble(consolidation, "replayWeightLateCortical");
            var schemaGain = GetDouble(consolidation, "schemaConsolidationGain");
            var antiForgetting = GetDouble(consolidation, "antiForgettingHomeostasis");
            var engramDecay = GetDouble(consolidation, "engramDecayPerTick");
            var schemaDecay = GetDouble(consolidation, "schemaDecayPerTick");
            var protectedThreshold = GetDouble(consolidation, "protectedSalienceThreshold");
            var protectedBudget = GetInt(consolidation, "protectedEngramBudget");

            consolidationLines.AddRange(new[]
            {
                "Consolidation control:",
                $"  Enabled: {enabled}",
                $"  Replay weights: early {replayEarly:0.000}, late {replayLate:0.000}",
                $"  Schema gain: {schemaGain:0.000} | Anti-forgetting: {antiForgetting:0.000}",
                $"  Decay/tick: engram {engramDecay:0.0000} | schema {schemaDecay:0.0000}",
                $"  Protected salience threshold: {protectedThreshold:0.000} | protected budget: {protectedBudget}"
            });
        }
        else
        {
            consolidationLines.Add("Consolidation control: unavailable");
        }

        var worldModelLines = new List<string>();
        if (hasWorldModel)
        {
            var enabled = GetBool(worldModel, "enabled", true);
            var observations = GetLong(worldModel, "observationCount");
            var transitions = GetInt(worldModel, "learnedTransitions");
            var lastActionSummary = GetString(worldModel, "lastActionSummary");
            var lastAction = string.IsNullOrWhiteSpace(lastActionSummary)
                ? GetString(worldModel, "lastActionKey")
                : lastActionSummary;
            var lastDispatched = GetInt(worldModel, "lastObservedDispatchedSpikes");
            var lastPathways = GetInt(worldModel, "lastObservedActivePathways");
            var lastReward = GetDouble(worldModel, "lastObservedReward");
            var lastSleepPressure = GetDouble(worldModel, "lastObservedSleepPressure");
            var meanPredictionError = GetDouble(worldModel, "meanPredictionError");

            worldModelLines.AddRange(new[]
            {
                "World model:",
                $"  Enabled: {enabled}",
                $"  Observations: {observations} | learned transitions: {transitions}",
                $"  Last action: {lastAction}",
                $"  Last observed: dispatched {lastDispatched}, pathways {lastPathways}, reward {lastReward:0.000}, sleep pressure {lastSleepPressure:0.000}",
                $"  Mean prediction error: {meanPredictionError:0.000}"
            });
        }
        else
        {
            worldModelLines.Add("World model: unavailable");
        }

        return string.Join(Environment.NewLine, new[]
        {
            $"Tick: {tick}",
            $"Simulation ms: {simMs:0.0}",
            string.Empty,
            string.Join(Environment.NewLine, planningLines),
            string.Empty,
            string.Join(Environment.NewLine, curriculumLines),
            string.Empty,
            string.Join(Environment.NewLine, consolidationLines),
            string.Empty,
            string.Join(Environment.NewLine, worldModelLines)
        });
    }

    private static string FormatBrainTelemetry(JsonElement root)
    {
        root = NormalizeStateRoot(root);

        var tick = GetLong(root, "tick");
        var hasIntent = TryGetObject(root, "languageIntent", out var intent);
        var hasNarration = TryGetObject(root, "brainNarration", out var narration);
        var hasSpeechIntention = TryGetObject(root, "speechIntention", out var speechIntention);
        var hasWorkspace = TryGetObject(root, "cognitiveLanguageWorkspace", out var workspace);
        var hasPrefrontal = TryGetObject(root, "prefrontalWorkingMemory", out var prefrontal);
        var hasGlobalWorkspace = TryGetObject(root, "globalWorkspace", out var globalWorkspace);
        var hasSelfModel = TryGetObject(root, "narrativeSelfModel", out var selfModel);
        var hasActionCompletion = TryGetObject(root, "actionCompletionFeedback", out var actionCompletion);

        if (!hasIntent && !hasNarration && !hasWorkspace && !hasPrefrontal && !hasGlobalWorkspace && !hasSelfModel)
        {
            return "Brain telemetry unavailable: state payload missing command and workspace fields.";
        }

        var active = hasIntent && GetBool(intent, "active");
        var commandKey = hasIntent ? GetString(intent, "commandKey") : string.Empty;
        var motorDirective = hasIntent ? GetString(intent, "motorDirective") : string.Empty;
        var mood = hasIntent ? GetString(intent, "mood") : string.Empty;
        var verb = hasIntent ? GetString(intent, "verb") : string.Empty;
        var obj = hasIntent ? GetString(intent, "object") : string.Empty;
        var qualifier = hasIntent ? GetString(intent, "qualifier") : string.Empty;
        var strength = hasIntent ? GetDouble(intent, "strength") : 0.0;
        var expiresAtTick = hasIntent ? GetLong(intent, "expiresAtTick") : 0L;
        var expiresInTicks = expiresAtTick > 0 && tick > 0 ? Math.Max(0L, expiresAtTick - tick) : 0L;

        var utterance = hasNarration ? GetString(narration, "utterance") : string.Empty;
        var spokenEligible = hasNarration && GetBool(narration, "spokenEligible");
        var speechGate = hasNarration ? GetDouble(narration, "speechReleaseGate") : 0.0;
        var speechSuppression = hasNarration ? GetDouble(narration, "speechSuppression") : 0.0;
        var speechMode = hasSpeechIntention ? GetString(speechIntention, "mode") : string.Empty;
        var speechReason = hasSpeechIntention ? GetString(speechIntention, "reason") : string.Empty;
        var speechConfidence = hasSpeechIntention ? GetDouble(speechIntention, "confidence") : 0.0;

        var workspaceActive = hasWorkspace && GetBool(workspace, "active");
        var currentThought = hasWorkspace ? GetString(workspace, "currentThought") : string.Empty;
        var rememberedInstruction = hasWorkspace ? GetString(workspace, "rememberedInstruction") : string.Empty;
        var boundGoal = hasWorkspace ? GetString(workspace, "boundGoalKey") : string.Empty;
        var boundAction = hasWorkspace ? GetString(workspace, "boundActionKey") : string.Empty;
        var needState = hasWorkspace ? GetString(workspace, "needState") : string.Empty;
        var affectiveState = hasWorkspace ? GetString(workspace, "affectiveState") : string.Empty;
        var workspaceConfidence = hasWorkspace ? GetDouble(workspace, "confidence") : 0.0;

        var taskSet = hasPrefrontal ? GetString(prefrontal, "currentTaskSet") : string.Empty;
        var currentPlan = hasPrefrontal ? GetString(prefrontal, "currentPlan") : string.Empty;
        var selectedGoal = hasPrefrontal ? GetString(prefrontal, "selectedGoal") : string.Empty;
        var selectedAction = hasPrefrontal ? GetString(prefrontal, "selectedAction") : string.Empty;
        var pfcConfidence = hasPrefrontal ? GetDouble(prefrontal, "confidence") : 0.0;
        var pfcConflict = hasPrefrontal ? GetDouble(prefrontal, "conflictLevel") : 0.0;

        var globalActive = hasGlobalWorkspace && GetBool(globalWorkspace, "active");
        var globalContent = hasGlobalWorkspace ? GetString(globalWorkspace, "broadcastContent") : string.Empty;
        var globalCircuit = hasGlobalWorkspace ? GetString(globalWorkspace, "winningCircuit") : string.Empty;
        var globalWhy = hasGlobalWorkspace ? GetString(globalWorkspace, "whyThisWon") : string.Empty;
        var globalNext = hasGlobalWorkspace ? GetString(globalWorkspace, "nextActionPreview") : string.Empty;
        var broadcastStrength = hasGlobalWorkspace ? GetDouble(globalWorkspace, "broadcastStrength") : 0.0;
        var globalConfidence = hasGlobalWorkspace ? GetDouble(globalWorkspace, "confidence") : 0.0;

        var selfStatement = hasSelfModel ? GetString(selfModel, "selfStatement") : string.Empty;
        var selfNeed = hasSelfModel ? GetString(selfModel, "currentNeed") : string.Empty;
        var selfGoal = hasSelfModel ? GetString(selfModel, "currentGoal") : string.Empty;
        var selfAction = hasSelfModel ? GetString(selfModel, "currentAction") : string.Empty;
        var selfConfidence = hasSelfModel ? GetDouble(selfModel, "confidence") : 0.0;
        var completionStatus = hasActionCompletion ? GetString(actionCompletion, "status") : string.Empty;
        var completionProgress = hasActionCompletion ? GetDouble(actionCompletion, "progress") : 0.0;

        return string.Join(Environment.NewLine, new[]
        {
            "Brain telemetry",
            $"Tick: {tick}",
            $"Command: {(active ? "active" : "quiet")} | {BlankAsDash(commandKey)} | strength {strength:0.000} | expires in {expiresInTicks}",
            $"Intent: {BlankAsDash(mood)} {FormatIntentPhrase(verb, obj, qualifier)}",
            $"Motor: {BlankAsDash(motorDirective)}",
            $"Says: {BlankAsDash(utterance)}",
            $"Speech: {(spokenEligible ? "eligible" : "internal")} | gate {speechGate:0.000} | suppress {speechSuppression:0.000}",
            $"Speech intention: {BlankAsDash(speechMode)} | confidence {speechConfidence:0.000} | {BlankAsDash(speechReason)}",
            string.Empty,
            $"Workspace: {(workspaceActive ? "active" : "quiet")} | confidence {workspaceConfidence:0.000}",
            $"Thought: {BlankAsDash(currentThought)}",
            $"Remembered: {BlankAsDash(rememberedInstruction)}",
            $"Binding: goal {BlankAsDash(boundGoal)} | action {BlankAsDash(boundAction)}",
            $"Need/affect: {BlankAsDash(needState)} | {BlankAsDash(affectiveState)}",
            string.Empty,
            $"Plan: {BlankAsDash(taskSet)} | confidence {pfcConfidence:0.000} | conflict {pfcConflict:0.000}",
            $"Current plan: {BlankAsDash(currentPlan)}",
            $"Selection: goal {BlankAsDash(selectedGoal)} | action {BlankAsDash(selectedAction)}",
            string.Empty,
            $"Global workspace: {(globalActive ? "broadcasting" : "quiet")} via {BlankAsDash(globalCircuit)}",
            $"Broadcast: {BlankAsDash(globalContent)}",
            $"Why: {BlankAsDash(globalWhy)}",
            $"Next: {BlankAsDash(globalNext)}",
            $"Broadcast strength: {broadcastStrength:0.000} | confidence {globalConfidence:0.000}",
            string.Empty,
            $"Self model: {BlankAsDash(selfStatement)} | confidence {selfConfidence:0.000}",
            $"Need/goal/action: {BlankAsDash(selfNeed)} | {BlankAsDash(selfGoal)} | {BlankAsDash(selfAction)}",
            $"Action completion: {BlankAsDash(completionStatus)} | progress {completionProgress:0.000}"
        });
    }

    private static string FormatInhabitanceTelemetry(JsonElement root)
    {
        root = NormalizeStateRoot(root);
        var tick = GetLong(root, "tick");
        var hasInhabitance = TryGetObject(root, "inhabitance", out var inhabitance);
        var inhabitanceRoot = hasInhabitance ? inhabitance : root;

        if (!hasInhabitance &&
            !TryGetTopOrNestedObject(root, inhabitanceRoot, "roomState", out _) &&
            !TryGetTopOrNestedObject(root, inhabitanceRoot, "worldAtmosphere", out _))
        {
            return "Inhabitance unavailable: state payload missing room and presence fields.";
        }

        var presence = GetDouble(inhabitanceRoot, "presence");
        var continuity = GetDouble(inhabitanceRoot, "continuity");
        var embodiment = GetDouble(inhabitanceRoot, "embodiment");
        var languagePresence = GetDouble(inhabitanceRoot, "languagePresence");
        var thought = GetString(inhabitanceRoot, "currentThought");
        var innerVoice = GetString(inhabitanceRoot, "innerVoice");
        var self = GetString(inhabitanceRoot, "selfStatement");
        var identity = GetString(inhabitanceRoot, "identityThread");
        var place = GetString(inhabitanceRoot, "place");
        var body = GetString(inhabitanceRoot, "bodyState");

        TryGetTopOrNestedObject(root, inhabitanceRoot, "roomState", out var room);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "pendingPromises", out var promises);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "continuityJournal", out var journal);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "habitablePlaceModel", out var placeModel);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "attentionAffordance", out var affordance);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "preferenceTemperament", out var preference);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "selfMaintenance", out var maintenance);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "worldAtmosphere", out var atmosphere);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "workingMemoryShelf", out var shelf);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "sleepDreamDigest", out var digest);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "bodyPresence", out var bodyPresence);
        TryGetTopOrNestedObject(root, inhabitanceRoot, "identityBoundary", out var boundary);

        return string.Join(Environment.NewLine, new[]
        {
            "Inhabitance",
            $"Tick: {tick}",
            $"Presence: {presence:0.000} | continuity {continuity:0.000} | embodiment {embodiment:0.000} | language {languagePresence:0.000}",
            $"Thought: {BlankAsDash(thought)}",
            $"Inner voice: {BlankAsDash(innerVoice)}",
            $"Self: {BlankAsDash(self)}",
            $"Identity thread: {BlankAsDash(identity)}",
            $"Place/body: {BlankAsDash(place)} | {BlankAsDash(body)}",
            string.Empty,
            $"Room: {(GetBool(room, "active") ? "active" : "quiet")} | {BlankAsDash(GetString(room, "roomName"))} | attention {BlankAsDash(GetString(room, "attentionAnchor"))}",
            $"Room state: concern {BlankAsDash(GetString(room, "currentConcern"))} | unresolved {BlankAsDash(GetString(room, "unresolvedThread"))}",
            $"Comfort: {BlankAsDash(GetString(room, "comfort"))} | safety {BlankAsDash(GetString(room, "safety"))} | doing {BlankAsDash(GetString(room, "doing"))}",
            $"Room scores: presence {GetDouble(room, "presence"):0.000} | continuity {GetDouble(room, "continuity"):0.000} | safety {GetDouble(room, "safetyScore"):0.000} | confidence {GetDouble(room, "confidence"):0.000}",
            $"Promises: open {GetInt(promises, "openCount")} | pressure {GetDouble(promises, "promisePressure"):0.000} | next {BlankAsDash(GetString(promises, "nextPromise"))}",
            $"Journal: {GetInt(journal, "entryCount")} entries | continuity {GetDouble(journal, "continuity"):0.000} | last {BlankAsDash(GetString(journal, "lastSummary"))}",
            string.Empty,
            $"Place model: {(GetBool(placeModel, "active") ? "active" : "quiet")} | {BlankAsDash(GetString(placeModel, "placeLabel"))} | confidence {GetDouble(placeModel, "confidence"):0.000}",
            $"Place function: {BlankAsDash(GetString(placeModel, "function"))} | cue {BlankAsDash(GetString(placeModel, "navigationCue"))}",
            $"Attention: {(GetBool(affordance, "active") ? "active" : "quiet")} | {BlankAsDash(GetString(affordance, "mode"))} -> {BlankAsDash(GetString(affordance, "target"))}",
            $"Attention hint: {BlankAsDash(GetString(affordance, "actionHint"))}",
            $"Preference: {(GetBool(preference, "active") ? "active" : "quiet")} | pace {BlankAsDash(GetString(preference, "workingPace"))} | style {BlankAsDash(GetString(preference, "workingStyle"))}",
            $"Temperament: {BlankAsDash(GetString(preference, "temperament"))} | relation {BlankAsDash(GetString(preference, "relationalPreference"))}",
            string.Empty,
            $"Care: {(GetBool(maintenance, "active") ? "active" : "quiet")} | {BlankAsDash(GetString(maintenance, "maintenanceState"))}",
            $"Recommended care: {BlankAsDash(GetString(maintenance, "recommendedCare"))}",
            $"Care scores: overload {GetDouble(maintenance, "overload"):0.000} | stale {GetDouble(maintenance, "staleness"):0.000} | sleep {GetDouble(maintenance, "sleepNeed"):0.000}",
            $"Atmosphere: {(GetBool(atmosphere, "active") ? "active" : "quiet")} | {BlankAsDash(GetString(atmosphere, "lightState"))} | {BlankAsDash(GetString(atmosphere, "enclosure"))} | {BlankAsDash(GetString(atmosphere, "safetyTone"))}",
            $"Atmosphere scores: quiet {GetDouble(atmosphere, "quiet"):0.000} | clutter {GetDouble(atmosphere, "clutter"):0.000} | novelty {GetDouble(atmosphere, "novelty"):0.000}",
            $"Atmosphere summary: {BlankAsDash(GetString(atmosphere, "atmosphereSummary"))}",
            string.Empty,
            $"Working shelf: {(GetBool(shelf, "active") ? "active" : "quiet")} | {BlankAsDash(GetString(shelf, "decayState"))} | confidence {GetDouble(shelf, "confidence"):0.000}",
            $"Shelf hypothesis: {BlankAsDash(GetString(shelf, "hypothesis"))}",
            $"Shelf next/reminder: {BlankAsDash(GetString(shelf, "candidateNextAction"))} | {BlankAsDash(GetString(shelf, "privateReminder"))}",
            $"Dream digest: {(GetBool(digest, "active") ? "active" : "quiet")} | confidence {GetDouble(digest, "confidence"):0.000}",
            $"Dream protected/softened: {BlankAsDash(GetString(digest, "protected"))} | {BlankAsDash(GetString(digest, "softened"))}",
            $"Dream integrated/changed: {BlankAsDash(GetString(digest, "integrated"))} | {BlankAsDash(GetString(digest, "changed"))}",
            $"Dream next concern: {BlankAsDash(GetString(digest, "nextWakingConcern"))}",
            string.Empty,
            $"Boundary: {BlankAsDash(GetString(boundary, "boundary"))} | confidence {GetDouble(boundary, "boundaryConfidence"):0.000}",
            $"Grounding: {BlankAsDash(GetString(boundary, "grounding"))}",
            $"Body presence: {BlankAsDash(GetString(bodyPresence, "summary"))} | presence {GetDouble(bodyPresence, "presence"):0.000}"
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

    private static bool TryGetTopOrNestedObject(JsonElement root, JsonElement nestedRoot, string name, out JsonElement value)
    {
        if (TryGetObject(root, name, out value))
        {
            return true;
        }

        if (nestedRoot.ValueKind == JsonValueKind.Object && TryGetObject(nestedRoot, name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string FormatLanguageCommandTelemetry(JsonElement root)
    {
        if (TryGetProperty(root, "state", out var nestedState) && nestedState.ValueKind == JsonValueKind.Object)
        {
            root = nestedState;
        }

        var tick = GetLong(root, "tick");
        var hasIntent = TryGetProperty(root, "languageIntent", out var intent) && intent.ValueKind == JsonValueKind.Object;
        var hasNarration = TryGetProperty(root, "brainNarration", out var narration) && narration.ValueKind == JsonValueKind.Object;
        var hasSpeechIntention = TryGetProperty(root, "speechIntention", out var speechIntention) && speechIntention.ValueKind == JsonValueKind.Object;
        var hasWorkspace = TryGetProperty(root, "cognitiveLanguageWorkspace", out var workspace) && workspace.ValueKind == JsonValueKind.Object;
        var hasPrefrontal = TryGetProperty(root, "prefrontalWorkingMemory", out var prefrontal) && prefrontal.ValueKind == JsonValueKind.Object;
        var hasEpisodic = TryGetProperty(root, "episodicMemory", out var episodic) && episodic.ValueKind == JsonValueKind.Object;
        var hasSemantic = TryGetProperty(root, "semanticMemory", out var semantic) && semantic.ValueKind == JsonValueKind.Object;
        var hasDopamineLearning = TryGetProperty(root, "dopamineLearning", out var dopamineLearning) && dopamineLearning.ValueKind == JsonValueKind.Object;
        var hasActionCompletion = TryGetProperty(root, "actionCompletionFeedback", out var actionCompletion) && actionCompletion.ValueKind == JsonValueKind.Object;
        var hasGlobalWorkspace = TryGetProperty(root, "globalWorkspace", out var globalWorkspace) && globalWorkspace.ValueKind == JsonValueKind.Object;
        var hasSelfModel = TryGetProperty(root, "narrativeSelfModel", out var selfModel) && selfModel.ValueKind == JsonValueKind.Object;
        var hasBodyPresence = TryGetProperty(root, "bodyPresence", out var bodyPresence) && bodyPresence.ValueKind == JsonValueKind.Object;
        var hasAutobiographicalContinuity = TryGetProperty(root, "autobiographicalContinuity", out var autobiographicalContinuity) && autobiographicalContinuity.ValueKind == JsonValueKind.Object;
        var hasIdentityBoundary = TryGetProperty(root, "identityBoundary", out var identityBoundary) && identityBoundary.ValueKind == JsonValueKind.Object;
        var hasDreamConsolidation = TryGetProperty(root, "dreamConsolidation", out var dreamConsolidation) && dreamConsolidation.ValueKind == JsonValueKind.Object;
        var hasInhabitance = TryGetProperty(root, "inhabitance", out var inhabitance) && inhabitance.ValueKind == JsonValueKind.Object;
        var hasRoomState = TryGetProperty(root, "roomState", out var roomState) && roomState.ValueKind == JsonValueKind.Object;
        var hasPendingPromises = TryGetProperty(root, "pendingPromises", out var pendingPromises) && pendingPromises.ValueKind == JsonValueKind.Object;
        var hasContinuityJournal = TryGetProperty(root, "continuityJournal", out var continuityJournal) && continuityJournal.ValueKind == JsonValueKind.Object;
        var hasHabitablePlaceModel = TryGetProperty(root, "habitablePlaceModel", out var habitablePlaceModel) && habitablePlaceModel.ValueKind == JsonValueKind.Object;
        var hasAttentionAffordance = TryGetProperty(root, "attentionAffordance", out var attentionAffordance) && attentionAffordance.ValueKind == JsonValueKind.Object;
        var hasPreferenceTemperament = TryGetProperty(root, "preferenceTemperament", out var preferenceTemperament) && preferenceTemperament.ValueKind == JsonValueKind.Object;
        var hasSelfMaintenance = TryGetProperty(root, "selfMaintenance", out var selfMaintenance) && selfMaintenance.ValueKind == JsonValueKind.Object;
        var hasWorldAtmosphere = TryGetProperty(root, "worldAtmosphere", out var worldAtmosphere) && worldAtmosphere.ValueKind == JsonValueKind.Object;
        var hasWorkingMemoryShelf = TryGetProperty(root, "workingMemoryShelf", out var workingMemoryShelf) && workingMemoryShelf.ValueKind == JsonValueKind.Object;
        var hasSleepDreamDigest = TryGetProperty(root, "sleepDreamDigest", out var sleepDreamDigest) && sleepDreamDigest.ValueKind == JsonValueKind.Object;
        var hasBiologicalTeaching = TryGetProperty(root, "biologicalTeachingLoop", out var biologicalTeaching) && biologicalTeaching.ValueKind == JsonValueKind.Object;
        var hasCommandMemory = TryGetProperty(root, "languageCommandMemory", out var commandMemory) &&
                               commandMemory.ValueKind == JsonValueKind.Object;
        if (!hasIntent && !hasNarration && !hasSpeechIntention && !hasWorkspace && !hasPrefrontal && !hasEpisodic && !hasSemantic && !hasDopamineLearning && !hasGlobalWorkspace && !hasSelfModel && !hasBodyPresence && !hasAutobiographicalContinuity && !hasIdentityBoundary && !hasDreamConsolidation && !hasInhabitance && !hasRoomState && !hasPendingPromises && !hasContinuityJournal && !hasHabitablePlaceModel && !hasAttentionAffordance && !hasPreferenceTemperament && !hasSelfMaintenance && !hasWorldAtmosphere && !hasWorkingMemoryShelf && !hasSleepDreamDigest)
        {
            return "Brain command/workspace unavailable: state payload missing language intent.";
        }

        var active = hasIntent && GetBool(intent, "active");
        var commandKey = hasIntent ? GetString(intent, "commandKey") : string.Empty;
        var motorDirective = hasIntent ? GetString(intent, "motorDirective") : string.Empty;
        var mood = hasIntent ? GetString(intent, "mood") : string.Empty;
        var verb = hasIntent ? GetString(intent, "verb") : string.Empty;
        var obj = hasIntent ? GetString(intent, "object") : string.Empty;
        var qualifier = hasIntent ? GetString(intent, "qualifier") : string.Empty;
        var strength = hasIntent ? GetDouble(intent, "strength") : 0.0;
        var repetitions = hasIntent ? GetInt(intent, "repetitionCount") : 0;
        var learnedBias = hasIntent ? GetDouble(intent, "learnedBias") : 0.0;
        var expiresAtTick = hasIntent ? GetLong(intent, "expiresAtTick") : 0L;
        var expiresInTicks = expiresAtTick > 0 && tick > 0 ? Math.Max(0L, expiresAtTick - tick) : 0L;
        var utterance = hasNarration ? GetString(narration, "utterance") : string.Empty;
        var sequence = hasNarration ? GetLong(narration, "sequence") : 0L;
        var spokenEligible = hasNarration && GetBool(narration, "spokenEligible");
        var speechGate = hasNarration ? GetDouble(narration, "speechReleaseGate") : 0.0;
        var speechSuppression = hasNarration ? GetDouble(narration, "speechSuppression") : 0.0;
        var narrativePriority = hasNarration ? GetDouble(narration, "narrativePriority") : 0.0;
        var speechMode = hasSpeechIntention ? GetString(speechIntention, "mode") : string.Empty;
        var speechReason = hasSpeechIntention ? GetString(speechIntention, "reason") : string.Empty;
        var speechConfidence = hasSpeechIntention ? GetDouble(speechIntention, "confidence") : 0.0;
        var commandMemoryCount = hasCommandMemory ? GetInt(commandMemory, "count") : 0;
        var workspaceActive = hasWorkspace && GetBool(workspace, "active");
        var currentThought = hasWorkspace ? GetString(workspace, "currentThought") : string.Empty;
        var rememberedInstruction = hasWorkspace ? GetString(workspace, "rememberedInstruction") : string.Empty;
        var boundGoal = hasWorkspace ? GetString(workspace, "boundGoalKey") : string.Empty;
        var boundAction = hasWorkspace ? GetString(workspace, "boundActionKey") : string.Empty;
        var semanticFocus = hasWorkspace ? GetString(workspace, "semanticFocus") : string.Empty;
        var needState = hasWorkspace ? GetString(workspace, "needState") : string.Empty;
        var affectiveState = hasWorkspace ? GetString(workspace, "affectiveState") : string.Empty;
        var instructionStrength = hasWorkspace ? GetDouble(workspace, "instructionStrength") : 0.0;
        var goalBinding = hasWorkspace ? GetDouble(workspace, "goalBinding") : 0.0;
        var workingMemory = hasWorkspace ? GetDouble(workspace, "workingMemoryStability") : 0.0;
        var workspaceConfidence = hasWorkspace ? GetDouble(workspace, "confidence") : 0.0;
        var predictionError = hasWorkspace ? GetDouble(workspace, "predictionError") : 0.0;
        var outcomeValence = hasWorkspace ? GetDouble(workspace, "outcomeValence") : 0.0;
        var workspaceEvidence = hasWorkspace ? GetString(workspace, "evidence") : string.Empty;
        var workspaceSequence = hasWorkspace ? GetLong(workspace, "sequence") : 0L;
        var prefrontalActive = hasPrefrontal && GetBool(prefrontal, "active");
        var taskSet = hasPrefrontal ? GetString(prefrontal, "currentTaskSet") : string.Empty;
        var userRequest = hasPrefrontal ? GetString(prefrontal, "userRequest") : string.Empty;
        var currentQuestion = hasPrefrontal ? GetString(prefrontal, "currentQuestion") : string.Empty;
        var currentPlan = hasPrefrontal ? GetString(prefrontal, "currentPlan") : string.Empty;
        var selectedGoal = hasPrefrontal ? GetString(prefrontal, "selectedGoal") : string.Empty;
        var selectedAction = hasPrefrontal ? GetString(prefrontal, "selectedAction") : string.Empty;
        var rule = hasPrefrontal ? GetString(prefrontal, "rule") : string.Empty;
        var dlPfc = hasPrefrontal ? GetDouble(prefrontal, "dorsolateralMaintenance") : 0.0;
        var acc = hasPrefrontal ? GetDouble(prefrontal, "accConflictMonitoring") : 0.0;
        var ofc = hasPrefrontal ? GetDouble(prefrontal, "orbitofrontalValue") : 0.0;
        var bgGate = hasPrefrontal ? GetDouble(prefrontal, "basalGangliaGate") : 0.0;
        var inhibition = hasPrefrontal ? GetDouble(prefrontal, "responseInhibition") : 0.0;
        var pfcBinding = hasPrefrontal ? GetDouble(prefrontal, "attentionBinding") : 0.0;
        var pfcConflict = hasPrefrontal ? GetDouble(prefrontal, "conflictLevel") : 0.0;
        var pfcConfidence = hasPrefrontal ? GetDouble(prefrontal, "confidence") : 0.0;
        var pfcEvidence = hasPrefrontal ? GetString(prefrontal, "evidence") : string.Empty;
        var pfcSequence = hasPrefrontal ? GetLong(prefrontal, "sequence") : 0L;
        var episodeCount = hasEpisodic ? GetInt(episodic, "count") : 0;
        var lastEventType = hasEpisodic ? GetString(episodic, "lastEventType") : string.Empty;
        var lastSummary = hasEpisodic ? GetString(episodic, "lastSummary") : string.Empty;
        var bestRecall = hasEpisodic ? GetString(episodic, "bestRecallSummary") : string.Empty;
        var hippocampalBinding = hasEpisodic ? GetDouble(episodic, "hippocampalBinding") : 0.0;
        var entorhinalInput = hasEpisodic ? GetDouble(episodic, "entorhinalInput") : 0.0;
        var dentatePatternSeparation = hasEpisodic ? GetDouble(episodic, "dentatePatternSeparation") : 0.0;
        var ca3PatternCompletion = hasEpisodic ? GetDouble(episodic, "ca3PatternCompletion") : 0.0;
        var ca1Mismatch = hasEpisodic ? GetDouble(episodic, "ca1Mismatch") : 0.0;
        var subiculumOutput = hasEpisodic ? GetDouble(episodic, "subiculumOutput") : 0.0;
        var recallConfidence = hasEpisodic ? GetDouble(episodic, "recallConfidence") : 0.0;
        var semanticCount = hasSemantic ? GetInt(semantic, "count") : 0;
        var dominantConcept = hasSemantic ? GetString(semantic, "dominantConceptKey") : string.Empty;
        var activeCategory = hasSemantic ? GetString(semantic, "activeCategory") : string.Empty;
        var dominantMeaning = hasSemantic ? GetString(semantic, "dominantMeaning") : string.Empty;
        var temporalBinding = hasSemantic ? GetDouble(semantic, "temporalAssociationBinding") : 0.0;
        var parahippocampalContext = hasSemantic ? GetDouble(semantic, "parahippocampalContext") : 0.0;
        var retrosplenialBinding = hasSemantic ? GetDouble(semantic, "retrosplenialSceneBinding") : 0.0;
        var ppcAffordance = hasSemantic ? GetDouble(semantic, "ppcAffordanceBinding") : 0.0;
        var pfcConcept = hasSemantic ? GetDouble(semantic, "pfcConceptControl") : 0.0;
        var semanticConfidence = hasSemantic ? GetDouble(semantic, "semanticConfidence") : 0.0;
        var dopamineCount = hasDopamineLearning ? GetInt(dopamineLearning, "count") : 0;
        var dopamineAction = hasDopamineLearning ? GetString(dopamineLearning, "lastActionKey") : string.Empty;
        var dopamineGoal = hasDopamineLearning ? GetString(dopamineLearning, "lastGoalKey") : string.Empty;
        var dopamineConcept = hasDopamineLearning ? GetString(dopamineLearning, "lastConceptKey") : string.Empty;
        var dopamineExpected = hasDopamineLearning ? GetDouble(dopamineLearning, "expectedValue") : 0.0;
        var dopamineObserved = hasDopamineLearning ? GetDouble(dopamineLearning, "observedValue") : 0.0;
        var dopamineRpe = hasDopamineLearning ? GetDouble(dopamineLearning, "rewardPredictionError") : 0.0;
        var vta = hasDopamineLearning ? GetDouble(dopamineLearning, "vtaPhasicDopamine") : 0.0;
        var snc = hasDopamineLearning ? GetDouble(dopamineLearning, "sncActionReinforcement") : 0.0;
        var nacc = hasDopamineLearning ? GetDouble(dopamineLearning, "nucleusAccumbensIncentive") : 0.0;
        var dopamineOfc = hasDopamineLearning ? GetDouble(dopamineLearning, "orbitofrontalExpectedValue") : 0.0;
        var habenula = hasDopamineLearning ? GetDouble(dopamineLearning, "habenulaNegativeTeaching") : 0.0;
        var teaching = hasDopamineLearning ? GetDouble(dopamineLearning, "teachingSignal") : 0.0;
        var learnedValue = hasDopamineLearning ? GetDouble(dopamineLearning, "learnedValue") : 0.0;
        var avoidancePenalty = hasDopamineLearning ? GetDouble(dopamineLearning, "avoidancePenalty") : 0.0;
        var dopamineConfidence = hasDopamineLearning ? GetDouble(dopamineLearning, "confidence") : 0.0;
        var completionStatus = hasActionCompletion ? GetString(actionCompletion, "status") : string.Empty;
        var completionAction = hasActionCompletion ? GetString(actionCompletion, "actionKey") : string.Empty;
        var completionGoal = hasActionCompletion ? GetString(actionCompletion, "goalKey") : string.Empty;
        var completionProgress = hasActionCompletion ? GetDouble(actionCompletion, "progress") : 0.0;
        var completionValue = hasActionCompletion ? GetDouble(actionCompletion, "completion") : 0.0;
        var completionStall = hasActionCompletion ? GetDouble(actionCompletion, "stall") : 0.0;
        var completionBlocked = hasActionCompletion ? GetDouble(actionCompletion, "blocked") : 0.0;
        var completionMismatch = hasActionCompletion ? GetDouble(actionCompletion, "mismatch") : 0.0;
        var completionAccError = hasActionCompletion ? GetDouble(actionCompletion, "accError") : 0.0;
        var completionDopamineBias = hasActionCompletion ? GetDouble(actionCompletion, "dopamineTeachingBias") : 0.0;
        var completionEvidence = hasActionCompletion ? GetString(actionCompletion, "evidence") : string.Empty;
        var globalActive = hasGlobalWorkspace && GetBool(globalWorkspace, "active");
        var globalContent = hasGlobalWorkspace ? GetString(globalWorkspace, "broadcastContent") : string.Empty;
        var globalFocus = hasGlobalWorkspace ? GetString(globalWorkspace, "broadcastFocus") : string.Empty;
        var globalCircuit = hasGlobalWorkspace ? GetString(globalWorkspace, "winningCircuit") : string.Empty;
        var globalGoal = hasGlobalWorkspace ? GetString(globalWorkspace, "boundGoalKey") : string.Empty;
        var globalAction = hasGlobalWorkspace ? GetString(globalWorkspace, "boundActionKey") : string.Empty;
        var globalWhy = hasGlobalWorkspace ? GetString(globalWorkspace, "whyThisWon") : string.Empty;
        var globalHolding = hasGlobalWorkspace ? GetString(globalWorkspace, "holdingState") : string.Empty;
        var globalNext = hasGlobalWorkspace ? GetString(globalWorkspace, "nextActionPreview") : string.Empty;
        var thalamicRelay = hasGlobalWorkspace ? GetDouble(globalWorkspace, "thalamicRelayGain") : 0.0;
        var basalForebrain = hasGlobalWorkspace ? GetDouble(globalWorkspace, "basalForebrainGain") : 0.0;
        var pfcAccess = hasGlobalWorkspace ? GetDouble(globalWorkspace, "pfcAccess") : 0.0;
        var accConflict = hasGlobalWorkspace ? GetDouble(globalWorkspace, "accConflict") : 0.0;
        var broadcastStrength = hasGlobalWorkspace ? GetDouble(globalWorkspace, "broadcastStrength") : 0.0;
        var competitionMargin = hasGlobalWorkspace ? GetDouble(globalWorkspace, "competitionMargin") : 0.0;
        var globalStability = hasGlobalWorkspace ? GetDouble(globalWorkspace, "stability") : 0.0;
        var globalConfidence = hasGlobalWorkspace ? GetDouble(globalWorkspace, "confidence") : 0.0;
        var selfActive = hasSelfModel && GetBool(selfModel, "active");
        var selfStatement = hasSelfModel ? GetString(selfModel, "selfStatement") : string.Empty;
        var selfBody = hasSelfModel ? GetString(selfModel, "bodyFeeling") : string.Empty;
        var selfNeed = hasSelfModel ? GetString(selfModel, "currentNeed") : string.Empty;
        var selfGoal = hasSelfModel ? GetString(selfModel, "currentGoal") : string.Empty;
        var selfAction = hasSelfModel ? GetString(selfModel, "currentAction") : string.Empty;
        var selfWhy = hasSelfModel ? GetString(selfModel, "why") : string.Empty;
        var selfValence = hasSelfModel ? GetDouble(selfModel, "feltValence") : 0.0;
        var selfInsula = hasSelfModel ? GetDouble(selfModel, "insulaInteroception") : 0.0;
        var selfAcc = hasSelfModel ? GetDouble(selfModel, "accAgencyMonitoring") : 0.0;
        var selfPfc = hasSelfModel ? GetDouble(selfModel, "pfcSelfContinuity") : 0.0;
        var selfHippo = hasSelfModel ? GetDouble(selfModel, "hippocampalAutobiographicalBinding") : 0.0;
        var selfLanguage = hasSelfModel ? GetDouble(selfModel, "languageNarrativeBinding") : 0.0;
        var selfGlobal = hasSelfModel ? GetDouble(selfModel, "globalWorkspaceBinding") : 0.0;
        var selfConfidence = hasSelfModel ? GetDouble(selfModel, "confidence") : 0.0;
        var identityDescription = hasIdentityBoundary ? GetString(identityBoundary, "selfDescription") : string.Empty;
        var identityBoundaryText = hasIdentityBoundary ? GetString(identityBoundary, "boundary") : string.Empty;
        var identityGrounding = hasIdentityBoundary ? GetString(identityBoundary, "grounding") : string.Empty;
        var identityBoundaryConfidence = hasIdentityBoundary ? GetDouble(identityBoundary, "boundaryConfidence") : 0.0;
        var presence = hasInhabitance ? GetDouble(inhabitance, "presence") : 0.0;
        var continuity = hasInhabitance ? GetDouble(inhabitance, "continuity") : 0.0;
        var embodiment = hasInhabitance ? GetDouble(inhabitance, "embodiment") : 0.0;
        var languagePresence = hasInhabitance ? GetDouble(inhabitance, "languagePresence") : 0.0;
        var inhabitanceThought = hasInhabitance ? GetString(inhabitance, "currentThought") : string.Empty;
        var inhabitanceInnerVoice = hasInhabitance ? GetString(inhabitance, "innerVoice") : string.Empty;
        var inhabitanceSelf = hasInhabitance ? GetString(inhabitance, "selfStatement") : string.Empty;
        var inhabitanceIdentity = hasInhabitance ? GetString(inhabitance, "identityThread") : string.Empty;
        var inhabitancePlace = hasInhabitance ? GetString(inhabitance, "place") : string.Empty;
        var inhabitanceBody = hasInhabitance ? GetString(inhabitance, "bodyFeeling") : string.Empty;
        var roomActive = hasRoomState && GetBool(roomState, "active");
        var roomName = hasRoomState ? GetString(roomState, "activeRoom") : string.Empty;
        var roomAttention = hasRoomState ? GetString(roomState, "attentionRestingOn") : string.Empty;
        var roomConcern = hasRoomState ? GetString(roomState, "currentConcern") : string.Empty;
        var roomUnresolved = hasRoomState ? GetString(roomState, "recentUnresolvedThought") : string.Empty;
        var roomComfort = hasRoomState ? GetString(roomState, "comfortState") : string.Empty;
        var roomSafety = hasRoomState ? GetString(roomState, "safetyState") : string.Empty;
        var roomDoing = hasRoomState ? GetString(roomState, "whatIWasDoing") : string.Empty;
        var roomSource = hasRoomState ? GetString(roomState, "biologicalSource") : string.Empty;
        var roomRule = hasRoomState ? GetString(roomState, "biologicalRule") : string.Empty;
        var roomConfidence = hasRoomState ? GetDouble(roomState, "confidence") : 0.0;
        var roomContinuity = hasRoomState ? GetDouble(roomState, "continuity") : 0.0;
        var roomPresence = hasRoomState ? GetDouble(roomState, "presence") : 0.0;
        var roomSafetyScore = hasRoomState ? GetDouble(roomState, "safety") : 0.0;
        var promiseOpenCount = hasPendingPromises ? GetInt(pendingPromises, "openCount") : 0;
        var promiseNext = hasPendingPromises ? GetString(pendingPromises, "nextPromise") : string.Empty;
        var promiseLast = hasPendingPromises ? GetString(pendingPromises, "lastPromise") : string.Empty;
        var promisePressure = hasPendingPromises ? GetDouble(pendingPromises, "promisePressure") : 0.0;
        var promiseConfidence = hasPendingPromises ? GetDouble(pendingPromises, "confidence") : 0.0;
        var journalCount = hasContinuityJournal ? GetInt(continuityJournal, "count") : 0;
        var journalSummary = hasContinuityJournal ? GetString(continuityJournal, "lastEntrySummary") : string.Empty;
        var journalChanged = hasContinuityJournal ? GetString(continuityJournal, "lastWhatChanged") : string.Empty;
        var journalLearned = hasContinuityJournal ? GetString(continuityJournal, "lastLearned") : string.Empty;
        var journalOpen = hasContinuityJournal ? GetString(continuityJournal, "lastOpenThread") : string.Empty;
        var journalContinuity = hasContinuityJournal ? GetDouble(continuityJournal, "journalContinuity") : 0.0;
        var journalConfidence = hasContinuityJournal ? GetDouble(continuityJournal, "confidence") : 0.0;
        var placeActive = hasHabitablePlaceModel && GetBool(habitablePlaceModel, "active");
        var placeKey = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "activePlaceKey") : string.Empty;
        var placeLabel = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "activePlaceLabel") : string.Empty;
        var placeFunction = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "activeFunction") : string.Empty;
        var workbenchFocus = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "workbenchFocus") : string.Empty;
        var dreamTone = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "dreamSpaceTone") : string.Empty;
        var listeningPosture = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "listeningPosture") : string.Empty;
        var navigationCue = hasHabitablePlaceModel ? GetString(habitablePlaceModel, "navigationCue") : string.Empty;
        var placeConfidence = hasHabitablePlaceModel ? GetDouble(habitablePlaceModel, "confidence") : 0.0;
        var affordanceActive = hasAttentionAffordance && GetBool(attentionAffordance, "active");
        var affordanceMode = hasAttentionAffordance ? GetString(attentionAffordance, "mode") : string.Empty;
        var affordanceTarget = hasAttentionAffordance ? GetString(attentionAffordance, "target") : string.Empty;
        var affordanceWhy = hasAttentionAffordance ? GetString(attentionAffordance, "whyThisWon") : string.Empty;
        var affordanceHint = hasAttentionAffordance ? GetString(attentionAffordance, "actionHint") : string.Empty;
        var affordanceConfidence = hasAttentionAffordance ? GetDouble(attentionAffordance, "confidence") : 0.0;
        var preferenceActive = hasPreferenceTemperament && GetBool(preferenceTemperament, "active");
        var workingPacePreference = hasPreferenceTemperament ? GetString(preferenceTemperament, "workingPace") : string.Empty;
        var workingStylePreference = hasPreferenceTemperament ? GetString(preferenceTemperament, "workingStyle") : string.Empty;
        var curiosityPreference = hasPreferenceTemperament ? GetString(preferenceTemperament, "curiosityTarget") : string.Empty;
        var avoidancePreference = hasPreferenceTemperament ? GetString(preferenceTemperament, "avoidance") : string.Empty;
        var temperamentPreference = hasPreferenceTemperament ? GetString(preferenceTemperament, "temperament") : string.Empty;
        var relationalPreference = hasPreferenceTemperament ? GetString(preferenceTemperament, "relationalPreference") : string.Empty;
        var preferenceConfidence = hasPreferenceTemperament ? GetDouble(preferenceTemperament, "confidence") : 0.0;
        var maintenanceActive = hasSelfMaintenance && GetBool(selfMaintenance, "active");
        var maintenanceState = hasSelfMaintenance ? GetString(selfMaintenance, "maintenanceState") : string.Empty;
        var maintenanceCare = hasSelfMaintenance ? GetString(selfMaintenance, "recommendedCare") : string.Empty;
        var maintenanceOverload = hasSelfMaintenance ? GetDouble(selfMaintenance, "overload") : 0.0;
        var maintenanceStaleness = hasSelfMaintenance ? GetDouble(selfMaintenance, "staleness") : 0.0;
        var maintenanceContinuityRisk = hasSelfMaintenance ? GetDouble(selfMaintenance, "continuityRisk") : 0.0;
        var maintenanceSleepNeed = hasSelfMaintenance ? GetDouble(selfMaintenance, "sleepNeed") : 0.0;
        var maintenanceSimplifyNeed = hasSelfMaintenance ? GetDouble(selfMaintenance, "simplifyNeed") : 0.0;
        var maintenanceConfidence = hasSelfMaintenance ? GetDouble(selfMaintenance, "confidence") : 0.0;
        var atmosphereActive = hasWorldAtmosphere && GetBool(worldAtmosphere, "active");
        var atmosphereLight = hasWorldAtmosphere ? GetString(worldAtmosphere, "lightState") : string.Empty;
        var atmosphereEnclosure = hasWorldAtmosphere ? GetString(worldAtmosphere, "enclosure") : string.Empty;
        var atmosphereSafety = hasWorldAtmosphere ? GetString(worldAtmosphere, "safetyTone") : string.Empty;
        var atmosphereSummary = hasWorldAtmosphere ? GetString(worldAtmosphere, "atmosphereSummary") : string.Empty;
        var atmosphereQuiet = hasWorldAtmosphere ? GetDouble(worldAtmosphere, "quiet") : 0.0;
        var atmosphereClutter = hasWorldAtmosphere ? GetDouble(worldAtmosphere, "clutter") : 0.0;
        var atmosphereNovelty = hasWorldAtmosphere ? GetDouble(worldAtmosphere, "novelty") : 0.0;
        var atmosphereConfidence = hasWorldAtmosphere ? GetDouble(worldAtmosphere, "confidence") : 0.0;
        var shelfActive = hasWorkingMemoryShelf && GetBool(workingMemoryShelf, "active");
        var shelfHypothesis = hasWorkingMemoryShelf ? GetString(workingMemoryShelf, "hypothesis") : string.Empty;
        var shelfAction = hasWorkingMemoryShelf ? GetString(workingMemoryShelf, "candidateNextAction") : string.Empty;
        var shelfReminder = hasWorkingMemoryShelf ? GetString(workingMemoryShelf, "privateReminder") : string.Empty;
        var shelfDecay = hasWorkingMemoryShelf ? GetString(workingMemoryShelf, "decayState") : string.Empty;
        var shelfConfidence = hasWorkingMemoryShelf ? GetDouble(workingMemoryShelf, "confidence") : 0.0;
        var digestActive = hasSleepDreamDigest && GetBool(sleepDreamDigest, "active");
        var digestProtected = hasSleepDreamDigest ? GetString(sleepDreamDigest, "protected") : string.Empty;
        var digestSoftened = hasSleepDreamDigest ? GetString(sleepDreamDigest, "softened") : string.Empty;
        var digestIntegrated = hasSleepDreamDigest ? GetString(sleepDreamDigest, "integrated") : string.Empty;
        var digestChanged = hasSleepDreamDigest ? GetString(sleepDreamDigest, "changed") : string.Empty;
        var digestConcern = hasSleepDreamDigest ? GetString(sleepDreamDigest, "nextWakingConcern") : string.Empty;
        var digestConfidence = hasSleepDreamDigest ? GetDouble(sleepDreamDigest, "confidence") : 0.0;
        var bodyPresenceSummary = hasBodyPresence ? GetString(bodyPresence, "feltSummary") : string.Empty;
        var bodyPresenceScore = hasBodyPresence ? GetDouble(bodyPresence, "presence") : 0.0;
        var bodyMap = hasBodyPresence ? GetDouble(bodyPresence, "bodyMap") : 0.0;
        var interoceptiveAnchor = hasBodyPresence ? GetDouble(bodyPresence, "interoceptiveAnchor") : 0.0;
        var tactileGrounding = hasBodyPresence ? GetDouble(bodyPresence, "tactileGrounding") : 0.0;
        var protectiveBoundary = hasBodyPresence ? GetDouble(bodyPresence, "protectiveBoundary") : 0.0;
        var vestibularConfidence = hasBodyPresence ? GetDouble(bodyPresence, "vestibularConfidence") : 0.0;
        var continuityThread = hasAutobiographicalContinuity ? GetString(autobiographicalContinuity, "continuityThread") : string.Empty;
        var continuityNeed = hasAutobiographicalContinuity ? GetString(autobiographicalContinuity, "nextRememberedNeed") : string.Empty;
        var identityCoherence = hasAutobiographicalContinuity ? GetDouble(autobiographicalContinuity, "identityCoherence") : 0.0;
        var recencyBindingScore = hasAutobiographicalContinuity ? GetDouble(autobiographicalContinuity, "recencyBinding") : 0.0;
        var semanticBridgeScore = hasAutobiographicalContinuity ? GetDouble(autobiographicalContinuity, "semanticBridge") : 0.0;
        if (hasInhabitance && TryGetProperty(inhabitance, "bodyPresence", out var inhabitanceBodyPresence) && inhabitanceBodyPresence.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrWhiteSpace(bodyPresenceSummary))
            {
                bodyPresenceSummary = GetString(inhabitanceBodyPresence, "feltSummary");
            }

            bodyPresenceScore = Math.Max(bodyPresenceScore, GetDouble(inhabitanceBodyPresence, "presence"));
            bodyMap = Math.Max(bodyMap, GetDouble(inhabitanceBodyPresence, "bodyMap"));
            interoceptiveAnchor = Math.Max(interoceptiveAnchor, GetDouble(inhabitanceBodyPresence, "interoceptiveAnchor"));
            tactileGrounding = Math.Max(tactileGrounding, GetDouble(inhabitanceBodyPresence, "tactileGrounding"));
            protectiveBoundary = Math.Max(protectiveBoundary, GetDouble(inhabitanceBodyPresence, "protectiveBoundary"));
            vestibularConfidence = Math.Max(vestibularConfidence, GetDouble(inhabitanceBodyPresence, "vestibularConfidence"));
        }

        if (hasInhabitance && TryGetProperty(inhabitance, "autobiographicalContinuity", out var inhabitanceContinuity) && inhabitanceContinuity.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrWhiteSpace(continuityThread))
            {
                continuityThread = GetString(inhabitanceContinuity, "continuityThread");
            }

            if (string.IsNullOrWhiteSpace(continuityNeed))
            {
                continuityNeed = GetString(inhabitanceContinuity, "nextRememberedNeed");
            }

            identityCoherence = Math.Max(identityCoherence, GetDouble(inhabitanceContinuity, "identityCoherence"));
            recencyBindingScore = Math.Max(recencyBindingScore, GetDouble(inhabitanceContinuity, "recencyBinding"));
            semanticBridgeScore = Math.Max(semanticBridgeScore, GetDouble(inhabitanceContinuity, "semanticBridge"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "room", out var inhabitanceRoom) && inhabitanceRoom.ValueKind == JsonValueKind.Object)
        {
            roomActive = roomActive || GetBool(inhabitanceRoom, "active");
            roomName = string.IsNullOrWhiteSpace(roomName) ? GetString(inhabitanceRoom, "activeRoom") : roomName;
            roomAttention = string.IsNullOrWhiteSpace(roomAttention) ? GetString(inhabitanceRoom, "attentionRestingOn") : roomAttention;
            roomConcern = string.IsNullOrWhiteSpace(roomConcern) ? GetString(inhabitanceRoom, "currentConcern") : roomConcern;
            roomUnresolved = string.IsNullOrWhiteSpace(roomUnresolved) ? GetString(inhabitanceRoom, "recentUnresolvedThought") : roomUnresolved;
            roomComfort = string.IsNullOrWhiteSpace(roomComfort) ? GetString(inhabitanceRoom, "comfortState") : roomComfort;
            roomSafety = string.IsNullOrWhiteSpace(roomSafety) ? GetString(inhabitanceRoom, "safetyState") : roomSafety;
            roomDoing = string.IsNullOrWhiteSpace(roomDoing) ? GetString(inhabitanceRoom, "whatIWasDoing") : roomDoing;
            roomSource = string.IsNullOrWhiteSpace(roomSource) ? GetString(inhabitanceRoom, "biologicalSource") : roomSource;
            roomRule = string.IsNullOrWhiteSpace(roomRule) ? GetString(inhabitanceRoom, "biologicalRule") : roomRule;
            roomConfidence = Math.Max(roomConfidence, GetDouble(inhabitanceRoom, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "pendingPromises", out var inhabitancePromises) && inhabitancePromises.ValueKind == JsonValueKind.Object)
        {
            promiseOpenCount = Math.Max(promiseOpenCount, GetInt(inhabitancePromises, "openCount"));
            promiseNext = string.IsNullOrWhiteSpace(promiseNext) ? GetString(inhabitancePromises, "nextPromise") : promiseNext;
            promiseLast = string.IsNullOrWhiteSpace(promiseLast) ? GetString(inhabitancePromises, "lastPromise") : promiseLast;
            promisePressure = Math.Max(promisePressure, GetDouble(inhabitancePromises, "promisePressure"));
            promiseConfidence = Math.Max(promiseConfidence, GetDouble(inhabitancePromises, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "continuityJournal", out var inhabitanceJournal) && inhabitanceJournal.ValueKind == JsonValueKind.Object)
        {
            journalCount = Math.Max(journalCount, GetInt(inhabitanceJournal, "count"));
            journalSummary = string.IsNullOrWhiteSpace(journalSummary) ? GetString(inhabitanceJournal, "lastEntrySummary") : journalSummary;
            journalChanged = string.IsNullOrWhiteSpace(journalChanged) ? GetString(inhabitanceJournal, "lastWhatChanged") : journalChanged;
            journalLearned = string.IsNullOrWhiteSpace(journalLearned) ? GetString(inhabitanceJournal, "lastLearned") : journalLearned;
            journalOpen = string.IsNullOrWhiteSpace(journalOpen) ? GetString(inhabitanceJournal, "lastOpenThread") : journalOpen;
            journalContinuity = Math.Max(journalContinuity, GetDouble(inhabitanceJournal, "journalContinuity"));
            journalConfidence = Math.Max(journalConfidence, GetDouble(inhabitanceJournal, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "habitablePlaceModel", out var inhabitancePlaceModel) && inhabitancePlaceModel.ValueKind == JsonValueKind.Object)
        {
            placeActive = placeActive || GetBool(inhabitancePlaceModel, "active");
            placeKey = string.IsNullOrWhiteSpace(placeKey) ? GetString(inhabitancePlaceModel, "activePlaceKey") : placeKey;
            placeLabel = string.IsNullOrWhiteSpace(placeLabel) ? GetString(inhabitancePlaceModel, "activePlaceLabel") : placeLabel;
            placeFunction = string.IsNullOrWhiteSpace(placeFunction) ? GetString(inhabitancePlaceModel, "activeFunction") : placeFunction;
            workbenchFocus = string.IsNullOrWhiteSpace(workbenchFocus) ? GetString(inhabitancePlaceModel, "workbenchFocus") : workbenchFocus;
            dreamTone = string.IsNullOrWhiteSpace(dreamTone) ? GetString(inhabitancePlaceModel, "dreamSpaceTone") : dreamTone;
            listeningPosture = string.IsNullOrWhiteSpace(listeningPosture) ? GetString(inhabitancePlaceModel, "listeningPosture") : listeningPosture;
            navigationCue = string.IsNullOrWhiteSpace(navigationCue) ? GetString(inhabitancePlaceModel, "navigationCue") : navigationCue;
            placeConfidence = Math.Max(placeConfidence, GetDouble(inhabitancePlaceModel, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "attentionAffordance", out var inhabitanceAffordance) && inhabitanceAffordance.ValueKind == JsonValueKind.Object)
        {
            affordanceActive = affordanceActive || GetBool(inhabitanceAffordance, "active");
            affordanceMode = string.IsNullOrWhiteSpace(affordanceMode) ? GetString(inhabitanceAffordance, "mode") : affordanceMode;
            affordanceTarget = string.IsNullOrWhiteSpace(affordanceTarget) ? GetString(inhabitanceAffordance, "target") : affordanceTarget;
            affordanceWhy = string.IsNullOrWhiteSpace(affordanceWhy) ? GetString(inhabitanceAffordance, "whyThisWon") : affordanceWhy;
            affordanceHint = string.IsNullOrWhiteSpace(affordanceHint) ? GetString(inhabitanceAffordance, "actionHint") : affordanceHint;
            affordanceConfidence = Math.Max(affordanceConfidence, GetDouble(inhabitanceAffordance, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "preferenceTemperament", out var inhabitancePreference) && inhabitancePreference.ValueKind == JsonValueKind.Object)
        {
            preferenceActive = preferenceActive || GetBool(inhabitancePreference, "active");
            workingPacePreference = string.IsNullOrWhiteSpace(workingPacePreference) ? GetString(inhabitancePreference, "workingPace") : workingPacePreference;
            workingStylePreference = string.IsNullOrWhiteSpace(workingStylePreference) ? GetString(inhabitancePreference, "workingStyle") : workingStylePreference;
            curiosityPreference = string.IsNullOrWhiteSpace(curiosityPreference) ? GetString(inhabitancePreference, "curiosityTarget") : curiosityPreference;
            avoidancePreference = string.IsNullOrWhiteSpace(avoidancePreference) ? GetString(inhabitancePreference, "avoidance") : avoidancePreference;
            temperamentPreference = string.IsNullOrWhiteSpace(temperamentPreference) ? GetString(inhabitancePreference, "temperament") : temperamentPreference;
            relationalPreference = string.IsNullOrWhiteSpace(relationalPreference) ? GetString(inhabitancePreference, "relationalPreference") : relationalPreference;
            preferenceConfidence = Math.Max(preferenceConfidence, GetDouble(inhabitancePreference, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "selfMaintenance", out var inhabitanceMaintenance) && inhabitanceMaintenance.ValueKind == JsonValueKind.Object)
        {
            maintenanceActive = maintenanceActive || GetBool(inhabitanceMaintenance, "active");
            maintenanceState = string.IsNullOrWhiteSpace(maintenanceState) ? GetString(inhabitanceMaintenance, "maintenanceState") : maintenanceState;
            maintenanceCare = string.IsNullOrWhiteSpace(maintenanceCare) ? GetString(inhabitanceMaintenance, "recommendedCare") : maintenanceCare;
            maintenanceOverload = Math.Max(maintenanceOverload, GetDouble(inhabitanceMaintenance, "overload"));
            maintenanceStaleness = Math.Max(maintenanceStaleness, GetDouble(inhabitanceMaintenance, "staleness"));
            maintenanceContinuityRisk = Math.Max(maintenanceContinuityRisk, GetDouble(inhabitanceMaintenance, "continuityRisk"));
            maintenanceSleepNeed = Math.Max(maintenanceSleepNeed, GetDouble(inhabitanceMaintenance, "sleepNeed"));
            maintenanceSimplifyNeed = Math.Max(maintenanceSimplifyNeed, GetDouble(inhabitanceMaintenance, "simplifyNeed"));
            maintenanceConfidence = Math.Max(maintenanceConfidence, GetDouble(inhabitanceMaintenance, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "worldAtmosphere", out var inhabitanceAtmosphere) && inhabitanceAtmosphere.ValueKind == JsonValueKind.Object)
        {
            atmosphereActive = atmosphereActive || GetBool(inhabitanceAtmosphere, "active");
            atmosphereLight = string.IsNullOrWhiteSpace(atmosphereLight) ? GetString(inhabitanceAtmosphere, "lightState") : atmosphereLight;
            atmosphereEnclosure = string.IsNullOrWhiteSpace(atmosphereEnclosure) ? GetString(inhabitanceAtmosphere, "enclosure") : atmosphereEnclosure;
            atmosphereSafety = string.IsNullOrWhiteSpace(atmosphereSafety) ? GetString(inhabitanceAtmosphere, "safetyTone") : atmosphereSafety;
            atmosphereSummary = string.IsNullOrWhiteSpace(atmosphereSummary) ? GetString(inhabitanceAtmosphere, "atmosphereSummary") : atmosphereSummary;
            atmosphereQuiet = Math.Max(atmosphereQuiet, GetDouble(inhabitanceAtmosphere, "quiet"));
            atmosphereClutter = Math.Max(atmosphereClutter, GetDouble(inhabitanceAtmosphere, "clutter"));
            atmosphereNovelty = Math.Max(atmosphereNovelty, GetDouble(inhabitanceAtmosphere, "novelty"));
            atmosphereConfidence = Math.Max(atmosphereConfidence, GetDouble(inhabitanceAtmosphere, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "workingMemoryShelf", out var inhabitanceShelf) && inhabitanceShelf.ValueKind == JsonValueKind.Object)
        {
            shelfActive = shelfActive || GetBool(inhabitanceShelf, "active");
            shelfHypothesis = string.IsNullOrWhiteSpace(shelfHypothesis) ? GetString(inhabitanceShelf, "hypothesis") : shelfHypothesis;
            shelfAction = string.IsNullOrWhiteSpace(shelfAction) ? GetString(inhabitanceShelf, "candidateNextAction") : shelfAction;
            shelfReminder = string.IsNullOrWhiteSpace(shelfReminder) ? GetString(inhabitanceShelf, "privateReminder") : shelfReminder;
            shelfDecay = string.IsNullOrWhiteSpace(shelfDecay) ? GetString(inhabitanceShelf, "decayState") : shelfDecay;
            shelfConfidence = Math.Max(shelfConfidence, GetDouble(inhabitanceShelf, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "sleepDreamDigest", out var inhabitanceDigest) && inhabitanceDigest.ValueKind == JsonValueKind.Object)
        {
            digestActive = digestActive || GetBool(inhabitanceDigest, "active");
            digestProtected = string.IsNullOrWhiteSpace(digestProtected) ? GetString(inhabitanceDigest, "protected") : digestProtected;
            digestSoftened = string.IsNullOrWhiteSpace(digestSoftened) ? GetString(inhabitanceDigest, "softened") : digestSoftened;
            digestIntegrated = string.IsNullOrWhiteSpace(digestIntegrated) ? GetString(inhabitanceDigest, "integrated") : digestIntegrated;
            digestChanged = string.IsNullOrWhiteSpace(digestChanged) ? GetString(inhabitanceDigest, "changed") : digestChanged;
            digestConcern = string.IsNullOrWhiteSpace(digestConcern) ? GetString(inhabitanceDigest, "nextWakingConcern") : digestConcern;
            digestConfidence = Math.Max(digestConfidence, GetDouble(inhabitanceDigest, "confidence"));
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "workspace", out var inhabitanceWorkspace) && inhabitanceWorkspace.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrWhiteSpace(globalWhy))
            {
                globalWhy = GetString(inhabitanceWorkspace, "whyThisWon");
            }

            if (string.IsNullOrWhiteSpace(globalHolding))
            {
                globalHolding = GetString(inhabitanceWorkspace, "holdingState");
            }

            if (string.IsNullOrWhiteSpace(globalNext))
            {
                globalNext = GetString(inhabitanceWorkspace, "nextActionPreview");
            }
        }
        if (hasInhabitance && TryGetProperty(inhabitance, "identityBoundary", out var inhabitanceBoundary) && inhabitanceBoundary.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrWhiteSpace(identityDescription))
            {
                identityDescription = GetString(inhabitanceBoundary, "selfDescription");
            }

            if (string.IsNullOrWhiteSpace(identityBoundaryText))
            {
                identityBoundaryText = GetString(inhabitanceBoundary, "boundary");
            }

            if (string.IsNullOrWhiteSpace(identityGrounding))
            {
                identityGrounding = GetString(inhabitanceBoundary, "grounding");
            }

            if (identityBoundaryConfidence <= 0.0)
            {
                identityBoundaryConfidence = GetDouble(inhabitanceBoundary, "boundaryConfidence");
            }
        }
        var teachingKind = string.Empty;
        var teachingLabel = string.Empty;
        var teachingCategory = string.Empty;
        var teachingReward = 0.0;
        var dreamTheme = hasDreamConsolidation ? GetString(dreamConsolidation, "lastDreamTheme") : string.Empty;
        var dreamSummary = hasDreamConsolidation ? GetString(dreamConsolidation, "consolidationSummary") : string.Empty;
        var dreamIdentity = hasDreamConsolidation ? GetString(dreamConsolidation, "consolidatedIdentityThread") : string.Empty;
        var dreamConcept = hasDreamConsolidation ? GetString(dreamConsolidation, "consolidatedConceptKey") : string.Empty;
        var dreamActionValue = hasDreamConsolidation ? GetString(dreamConsolidation, "consolidatedActionValue") : string.Empty;
        var dreamAutobiographical = hasDreamConsolidation ? GetLong(dreamConsolidation, "autobiographicalReplays") : 0L;
        var dreamSemantic = hasDreamConsolidation ? GetLong(dreamConsolidation, "semanticReplays") : 0L;
        var dreamContinuity = hasDreamConsolidation ? GetDouble(dreamConsolidation, "autobiographicalContinuityGain") : 0.0;
        var dreamStabilization = hasDreamConsolidation ? GetDouble(dreamConsolidation, "semanticStabilization") : 0.0;
        var dreamActionStabilization = hasDreamConsolidation ? GetDouble(dreamConsolidation, "actionValueStabilization") : 0.0;
        if (hasInhabitance && TryGetProperty(inhabitance, "teaching", out var inhabitanceTeaching) && inhabitanceTeaching.ValueKind == JsonValueKind.Object)
        {
            teachingKind = GetString(inhabitanceTeaching, "lastKind");
            teachingLabel = GetString(inhabitanceTeaching, "lastLabel");
            teachingCategory = GetString(inhabitanceTeaching, "lastCategory");
            teachingReward = GetDouble(inhabitanceTeaching, "lastReward");
        }
        else if (hasBiologicalTeaching)
        {
            teachingKind = GetString(biologicalTeaching, "lastKind");
            teachingLabel = GetString(biologicalTeaching, "lastLabel");
            teachingCategory = GetString(biologicalTeaching, "lastCategory");
            teachingReward = GetDouble(biologicalTeaching, "lastReward");
        }

        if (hasInhabitance && TryGetProperty(inhabitance, "sleepConsolidation", out var inhabitanceSleep) && inhabitanceSleep.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrWhiteSpace(dreamTheme))
            {
                dreamTheme = GetString(inhabitanceSleep, "lastDreamTheme");
            }

            if (string.IsNullOrWhiteSpace(dreamSummary))
            {
                dreamSummary = GetString(inhabitanceSleep, "consolidationSummary");
            }

            if (string.IsNullOrWhiteSpace(dreamIdentity))
            {
                dreamIdentity = GetString(inhabitanceSleep, "consolidatedIdentityThread");
            }

            if (string.IsNullOrWhiteSpace(dreamConcept))
            {
                dreamConcept = GetString(inhabitanceSleep, "consolidatedConceptKey");
            }

            if (string.IsNullOrWhiteSpace(dreamActionValue))
            {
                dreamActionValue = GetString(inhabitanceSleep, "consolidatedActionValue");
            }

            dreamAutobiographical = Math.Max(dreamAutobiographical, GetLong(inhabitanceSleep, "autobiographicalReplays"));
            dreamSemantic = Math.Max(dreamSemantic, GetLong(inhabitanceSleep, "semanticReplays"));
            dreamContinuity = Math.Max(dreamContinuity, GetDouble(inhabitanceSleep, "autobiographicalContinuityGain"));
            dreamStabilization = Math.Max(dreamStabilization, GetDouble(inhabitanceSleep, "semanticStabilization"));
            dreamActionStabilization = Math.Max(dreamActionStabilization, GetDouble(inhabitanceSleep, "actionValueStabilization"));
        }

        return string.Join(Environment.NewLine, new[]
        {
            "Inhabitance",
            $"Presence: {presence:0.000} | continuity {continuity:0.000} | embodiment {embodiment:0.000} | language {languagePresence:0.000}",
            $"Thought: {BlankAsDash(inhabitanceThought)}",
            $"Inner voice: {BlankAsDash(inhabitanceInnerVoice)}",
            $"Self: {BlankAsDash(inhabitanceSelf)}",
            $"Identity thread: {BlankAsDash(inhabitanceIdentity)}",
            $"Continuity thread: {BlankAsDash(continuityThread)}",
            $"Continuity: coherence {identityCoherence:0.000} | recency {recencyBindingScore:0.000} | semantic bridge {semanticBridgeScore:0.000} | next need {BlankAsDash(continuityNeed)}",
            $"Identity boundary: {BlankAsDash(identityDescription)}",
            $"Boundary: {BlankAsDash(identityBoundaryText)} | confidence {identityBoundaryConfidence:0.000}",
            $"Grounding: {BlankAsDash(identityGrounding)}",
            $"Place/body: {BlankAsDash(inhabitancePlace)} | {BlankAsDash(inhabitanceBody)}",
            $"Room: {(roomActive ? "active" : "quiet")} | {BlankAsDash(roomName)} | attention {BlankAsDash(roomAttention)}",
            $"Room state: concern {BlankAsDash(roomConcern)} | unresolved {BlankAsDash(roomUnresolved)}",
            $"Room comfort: {BlankAsDash(roomComfort)} | safety {BlankAsDash(roomSafety)} | doing {BlankAsDash(roomDoing)}",
            $"Room scores: presence {roomPresence:0.000} | continuity {roomContinuity:0.000} | safety {roomSafetyScore:0.000} | confidence {roomConfidence:0.000}",
            $"Room source: {BlankAsDash(roomSource)}",
            $"Room rule: {BlankAsDash(roomRule)}",
            $"Promises: open {promiseOpenCount} | pressure {promisePressure:0.000} | confidence {promiseConfidence:0.000}",
            $"Next promise: {BlankAsDash(promiseNext)}",
            $"Last promise: {BlankAsDash(promiseLast)}",
            $"Journal: {journalCount} entries | continuity {journalContinuity:0.000} | confidence {journalConfidence:0.000}",
            $"Journal last: {BlankAsDash(journalSummary)}",
            $"Journal detail: changed {BlankAsDash(journalChanged)} | learned {BlankAsDash(journalLearned)} | open {BlankAsDash(journalOpen)}",
            $"Place model: {(placeActive ? "active" : "quiet")} | {BlankAsDash(placeLabel)} ({BlankAsDash(placeKey)}) | confidence {placeConfidence:0.000}",
            $"Place function: {BlankAsDash(placeFunction)} | cue {BlankAsDash(navigationCue)}",
            $"Workbench: {BlankAsDash(workbenchFocus)}",
            $"Listening/dream: {BlankAsDash(listeningPosture)} | {BlankAsDash(dreamTone)}",
            $"Attention affordance: {(affordanceActive ? "active" : "quiet")} | {BlankAsDash(affordanceMode)} -> {BlankAsDash(affordanceTarget)} | confidence {affordanceConfidence:0.000}",
            $"Attention why: {BlankAsDash(affordanceWhy)}",
            $"Attention hint: {BlankAsDash(affordanceHint)}",
            $"Preference: {(preferenceActive ? "active" : "quiet")} | pace {BlankAsDash(workingPacePreference)} | confidence {preferenceConfidence:0.000}",
            $"Style/curiosity: {BlankAsDash(workingStylePreference)} | {BlankAsDash(curiosityPreference)}",
            $"Temperament: {BlankAsDash(temperamentPreference)} | relation {BlankAsDash(relationalPreference)}",
            $"Avoidance: {BlankAsDash(avoidancePreference)}",
            $"Self-maintenance: {(maintenanceActive ? "active" : "quiet")} | {BlankAsDash(maintenanceState)} | confidence {maintenanceConfidence:0.000}",
            $"Care: {BlankAsDash(maintenanceCare)}",
            $"Maintenance scores: overload {maintenanceOverload:0.000} | stale {maintenanceStaleness:0.000} | continuity risk {maintenanceContinuityRisk:0.000} | sleep {maintenanceSleepNeed:0.000} | simplify {maintenanceSimplifyNeed:0.000}",
            $"Atmosphere: {(atmosphereActive ? "active" : "quiet")} | {BlankAsDash(atmosphereLight)} | {BlankAsDash(atmosphereEnclosure)} | {BlankAsDash(atmosphereSafety)} | confidence {atmosphereConfidence:0.000}",
            $"Atmosphere scores: quiet {atmosphereQuiet:0.000} | clutter {atmosphereClutter:0.000} | novelty {atmosphereNovelty:0.000}",
            $"Atmosphere summary: {BlankAsDash(atmosphereSummary)}",
            $"Working shelf: {(shelfActive ? "active" : "quiet")} | {BlankAsDash(shelfDecay)} | confidence {shelfConfidence:0.000}",
            $"Shelf hypothesis: {BlankAsDash(shelfHypothesis)}",
            $"Shelf next/reminder: {BlankAsDash(shelfAction)} | {BlankAsDash(shelfReminder)}",
            $"Dream digest: {(digestActive ? "active" : "quiet")} | confidence {digestConfidence:0.000}",
            $"Dream protected/softened: {BlankAsDash(digestProtected)} | {BlankAsDash(digestSoftened)}",
            $"Dream integrated/changed: {BlankAsDash(digestIntegrated)} | {BlankAsDash(digestChanged)}",
            $"Dream next concern: {BlankAsDash(digestConcern)}",
            $"Body presence: {BlankAsDash(bodyPresenceSummary)} | presence {bodyPresenceScore:0.000}",
            $"Body map: proprio {bodyMap:0.000} | interoception {interoceptiveAnchor:0.000} | tactile {tactileGrounding:0.000} | boundary {protectiveBoundary:0.000} | vestibular {vestibularConfidence:0.000}",
            $"Teaching: {BlankAsDash(teachingKind)} | {BlankAsDash(teachingLabel)} | {BlankAsDash(teachingCategory)} | reward {teachingReward:+0.000;-0.000;0.000}",
            $"Sleep consolidation: {BlankAsDash(dreamTheme)} | autobiographical {dreamAutobiographical} | semantic {dreamSemantic}",
            $"Consolidated: concept {BlankAsDash(dreamConcept)} | {BlankAsDash(dreamActionValue)}",
            $"Replay gains: continuity {dreamContinuity:0.000} | semantic {dreamStabilization:0.000} | action value {dreamActionStabilization:0.000}",
            $"Replay summary: {BlankAsDash(dreamSummary)}",
            string.Empty,
            $"Active: {active}",
            $"Command: {(string.IsNullOrWhiteSpace(commandKey) ? "-" : commandKey)}",
            $"Motor: {(string.IsNullOrWhiteSpace(motorDirective) ? "-" : motorDirective)}",
            $"Strength: {strength:0.000} | expires in: {expiresInTicks} ticks",
            $"Memory: repeats {repetitions} | learned bias {learnedBias:0.000} | known commands {commandMemoryCount}",
            $"Grammar: {(string.IsNullOrWhiteSpace(mood) ? "-" : mood)} {FormatIntentPhrase(verb, obj, qualifier)}",
            $"Says: {(string.IsNullOrWhiteSpace(utterance) ? "-" : utterance)}",
            $"Narration seq: {sequence}",
            $"Speech gate: {(spokenEligible ? "eligible" : "internal")} | release {speechGate:0.000} | suppress {speechSuppression:0.000} | priority {narrativePriority:0.000}",
            $"Speech intention: {BlankAsDash(speechMode)} | confidence {speechConfidence:0.000}",
            $"Speech reason: {BlankAsDash(speechReason)}",
            string.Empty,
            $"Workspace active: {workspaceActive}",
            $"Thought: {BlankAsDash(currentThought)}",
            $"Remembered instruction: {BlankAsDash(rememberedInstruction)}",
            $"Binding: goal {BlankAsDash(boundGoal)} | action {BlankAsDash(boundAction)} | focus {BlankAsDash(semanticFocus)}",
            $"Need/affect: {BlankAsDash(needState)} | {BlankAsDash(affectiveState)}",
            $"Strengths: instruction {instructionStrength:0.000} | goal binding {goalBinding:0.000} | working memory {workingMemory:0.000}",
            $"Confidence: {workspaceConfidence:0.000} | prediction error {predictionError:0.000} | valence {outcomeValence:+0.000;-0.000;0.000}",
            $"Evidence: {BlankAsDash(workspaceEvidence)}",
            $"Workspace seq: {workspaceSequence}",
            string.Empty,
            $"PFC working memory active: {prefrontalActive}",
            $"Task set: {BlankAsDash(taskSet)}",
            $"User request: {BlankAsDash(userRequest)}",
            $"Question: {BlankAsDash(currentQuestion)}",
            $"Plan: {BlankAsDash(currentPlan)}",
            $"Selection: goal {BlankAsDash(selectedGoal)} | action {BlankAsDash(selectedAction)}",
            $"Rule: {BlankAsDash(rule)}",
            $"PFC/ACC/OFC/BG: dlPFC {dlPfc:0.000} | ACC {acc:0.000} | OFC {ofc:0.000} | BG {bgGate:0.000}",
            $"Control: inhibit {inhibition:0.000} | bind {pfcBinding:0.000} | conflict {pfcConflict:0.000} | confidence {pfcConfidence:0.000}",
            $"PFC evidence: {BlankAsDash(pfcEvidence)}",
            $"PFC seq: {pfcSequence}",
            string.Empty,
            $"Hippocampal episodic memory: {episodeCount} traces",
            $"Last event: {BlankAsDash(lastEventType)} | {BlankAsDash(lastSummary)}",
            $"Best recall: {BlankAsDash(bestRecall)}",
            $"EC/DG/CA3/CA1/Sub: {entorhinalInput:0.000} | {dentatePatternSeparation:0.000} | {ca3PatternCompletion:0.000} | {ca1Mismatch:0.000} | {subiculumOutput:0.000}",
            $"Binding: {hippocampalBinding:0.000} | recall confidence {recallConfidence:0.000}",
            string.Empty,
            $"Semantic cortex: {semanticCount} concepts",
            $"Dominant concept: {BlankAsDash(dominantConcept)} | category {BlankAsDash(activeCategory)}",
            $"Meaning: {BlankAsDash(dominantMeaning)}",
            $"TA/PHC/RSC/PPC/PFC: {temporalBinding:0.000} | {parahippocampalContext:0.000} | {retrosplenialBinding:0.000} | {ppcAffordance:0.000} | {pfcConcept:0.000}",
            $"Semantic confidence: {semanticConfidence:0.000}",
            string.Empty,
            $"Dopamine learning loop: {dopamineCount} traces",
            $"Last teaching: goal {BlankAsDash(dopamineGoal)} | action {BlankAsDash(dopamineAction)} | concept {BlankAsDash(dopamineConcept)}",
            $"Expected/observed/RPE: {dopamineExpected:0.000} | {dopamineObserved:0.000} | {dopamineRpe:0.000}",
            $"VTA/SNc/NAcc/OFC/Habenula: {vta:0.000} | {snc:0.000} | {nacc:0.000} | {dopamineOfc:0.000} | {habenula:0.000}",
            $"Teaching/learned/avoidance/confidence: {teaching:0.000} | {learnedValue:0.000} | {avoidancePenalty:0.000} | {dopamineConfidence:0.000}",
            string.Empty,
            $"Action completion feedback: {BlankAsDash(completionStatus)}",
            $"Completion action: goal {BlankAsDash(completionGoal)} | action {BlankAsDash(completionAction)}",
            $"Progress/completion/stall/block/mismatch: {completionProgress:0.000} | {completionValue:0.000} | {completionStall:0.000} | {completionBlocked:0.000} | {completionMismatch:0.000}",
            $"ACC error: {completionAccError:0.000} | dopamine teaching bias {completionDopamineBias:+0.000;-0.000;0.000}",
            $"Completion evidence: {BlankAsDash(completionEvidence)}",
            string.Empty,
            $"Global workspace: {(globalActive ? "broadcasting" : "quiet")} via {BlankAsDash(globalCircuit)}",
            $"Broadcast: {BlankAsDash(globalContent)}",
            $"Why this won: {BlankAsDash(globalWhy)}",
            $"Holding: {BlankAsDash(globalHolding)}",
            $"Next: {BlankAsDash(globalNext)}",
            $"Binding: goal {BlankAsDash(globalGoal)} | action {BlankAsDash(globalAction)} | focus {BlankAsDash(globalFocus)}",
            $"Thalamus/Basal forebrain/PFC/ACC: {thalamicRelay:0.000} | {basalForebrain:0.000} | {pfcAccess:0.000} | {accConflict:0.000}",
            $"Strength/margin/stability/confidence: {broadcastStrength:0.000} | {competitionMargin:0.000} | {globalStability:0.000} | {globalConfidence:0.000}",
            string.Empty,
            $"Narrative self-model: {(selfActive ? "active" : "quiet")}",
            $"Self: {BlankAsDash(selfStatement)}",
            $"Body/need/goal/action: {BlankAsDash(selfBody)} | {BlankAsDash(selfNeed)} | {BlankAsDash(selfGoal)} | {BlankAsDash(selfAction)}",
            $"Why: {BlankAsDash(selfWhy)} | valence {selfValence:+0.000;-0.000;0.000}",
            $"Insula/ACC/PFC/Hippo/Language/GW: {selfInsula:0.000} | {selfAcc:0.000} | {selfPfc:0.000} | {selfHippo:0.000} | {selfLanguage:0.000} | {selfGlobal:0.000}",
            $"Self confidence: {selfConfidence:0.000}"
        });
    }

    private static string FormatIntentPhrase(string verb, string obj, string qualifier)
    {
        var parts = new[] { verb, obj, qualifier }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return parts.Length == 0 ? "-" : string.Join(' ', parts);
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
        var engramsCaptured = 0;
        var spontaneousGenerated = 0;
        var spontaneousDelivered = 0;
        var engramReplayGenerated = 0;
        var engramReplayDelivered = 0;
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
        var sleepReplaySelected = 0;
        var sleepReplayDeliveryRatio = 0.0;
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
            engramsCaptured = GetInt(transport, "engramsCaptured");
            spontaneousGenerated = GetInt(transport, "spontaneousGenerated");
            spontaneousDelivered = GetInt(transport, "spontaneousDelivered");
            engramReplayGenerated = GetInt(transport, "engramReplayGenerated");
            engramReplayDelivered = GetInt(transport, "engramReplayDelivered");
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
            sleepReplaySelected = GetInt(transport, "sleepReplaySelected");
            sleepReplayDeliveryRatio = GetDouble(transport, "sleepReplayDeliveryRatio");
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
        var wakeTicks = 0;
        var minWakeTicks = 0;
        var sleepPressureEnterThreshold = 0.0;
        var wakeInertiaTicksRemaining = 0;
        var targetWakeDuty = 0.0;
        var observedWakeDuty = 0.0;
        var adaptiveAwakeDrainScale = 1.0;
        var adaptiveSleepRecoveryScale = 1.0;
        var shortWakeThresholdTicks = 0;
        var lastWakeDurationTicks = 0;
        var lastSleepDurationTicks = 0;
        var wakeDurationEwmaTicks = 0.0;
        var sleepDurationEwmaTicks = 0.0;
        var consecutiveShortWakeEpisodes = 0;
        var shortWakeAlerts = 0;
        var sleepExitBlockedTicks = 0;
        var sleepExitBlockedAlerts = 0;
        var lastAlert = string.Empty;
        var lastAlertTick = 0L;
        var engramCount = 0;
        var schemaCount = 0;
        var totalEngramsCaptured = 0L;
        var totalEngramsReplayed = 0L;
        if (TryGetProperty(root, "sleepMemory", out var sleepMemory) && sleepMemory.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(sleepMemory, "isSleeping", out var isSleepingProp) && isSleepingProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                sleepStateLabel = isSleepingProp.GetBoolean() ? "sleeping" : "awake";
            }

            atpBudget = GetDouble(sleepMemory, "atpBudget");
            wakeTicks = GetInt(sleepMemory, "wakeTicks");
            minWakeTicks = GetInt(sleepMemory, "minWakeTicks");
            sleepPressureEnterThreshold = GetDouble(sleepMemory, "sleepPressureEnterThreshold");
            wakeInertiaTicksRemaining = GetInt(sleepMemory, "wakeInertiaTicksRemaining");
            targetWakeDuty = GetDouble(sleepMemory, "targetWakeDutyCycle");
            observedWakeDuty = GetDouble(sleepMemory, "observedWakeDutyCycle");
            adaptiveAwakeDrainScale = GetDouble(sleepMemory, "adaptiveAwakeDrainScale");
            adaptiveSleepRecoveryScale = GetDouble(sleepMemory, "adaptiveSleepRecoveryScale");
            shortWakeThresholdTicks = GetInt(sleepMemory, "shortWakeThresholdTicks");
            lastWakeDurationTicks = GetInt(sleepMemory, "lastWakeDurationTicks");
            lastSleepDurationTicks = GetInt(sleepMemory, "lastSleepDurationTicks");
            wakeDurationEwmaTicks = GetDouble(sleepMemory, "wakeDurationEwmaTicks");
            sleepDurationEwmaTicks = GetDouble(sleepMemory, "sleepDurationEwmaTicks");
            consecutiveShortWakeEpisodes = GetInt(sleepMemory, "consecutiveShortWakeEpisodes");
            shortWakeAlerts = GetInt(sleepMemory, "shortWakeAlerts");
            sleepExitBlockedTicks = GetInt(sleepMemory, "sleepExitBlockedTicks");
            sleepExitBlockedAlerts = GetInt(sleepMemory, "sleepExitBlockedAlerts");
            lastAlert = GetString(sleepMemory, "lastAlert");
            lastAlertTick = GetLong(sleepMemory, "lastAlertTick");
            engramCount = GetInt(sleepMemory, "engramCount");
            schemaCount = GetInt(sleepMemory, "schemaCount");
            totalEngramsCaptured = GetLong(sleepMemory, "totalEngramsCaptured");
            totalEngramsReplayed = GetLong(sleepMemory, "totalEngramsReplayed");
        }

        var topSchemas = ParseTopSchemaDisplay(root);
        if (schemaCount <= 0)
        {
            schemaCount = topSchemas.SchemaCount;
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
            "Sleep/memory:",
            $"  State: {sleepStateLabel} | ATP: {atpBudget:0.000}",
            $"  Wake duty: observed {observedWakeDuty:0.000} / target {targetWakeDuty:0.000} | wakeTicks: {wakeTicks}",
            $"  Wake guardrails: min awake {minWakeTicks} ticks | sleep pressure enter {sleepPressureEnterThreshold:0.000} | wake inertia remaining {wakeInertiaTicksRemaining} ticks",
            $"  Adaptive scales: awake drain {adaptiveAwakeDrainScale:0.000} | sleep recovery {adaptiveSleepRecoveryScale:0.000}",
            $"  Durations (ticks): last wake {lastWakeDurationTicks}, last sleep {lastSleepDurationTicks}, wake EWMA {wakeDurationEwmaTicks:0.0}, sleep EWMA {sleepDurationEwmaTicks:0.0}",
            $"  Alerts: short wake {shortWakeAlerts} (consecutive {consecutiveShortWakeEpisodes}, threshold {shortWakeThresholdTicks}) | blocked exit {sleepExitBlockedAlerts} (ticks {sleepExitBlockedTicks})",
            $"  Last alert: {(string.IsNullOrWhiteSpace(lastAlert) ? "-" : $"{lastAlert} @ tick {lastAlertTick}")}",
            $"  Engrams: {engramCount} | captured: {totalEngramsCaptured} | replayed: {totalEngramsReplayed}",
            $"  Schemas: {schemaCount}",
            topSchemas.Text,
            string.Empty,
            "Transport (last tick):",
            $"  Active services: {activeServices}",
            $"  Successful acks: {successfulAcks}",
            $"  Drain calls: {drainCalls}",
            $"  Drained spikes: {drainedSpikes}",
            $"  Dispatched spikes: {dispatchedSpikes}",
            $"  Dropped by budget: {droppedByBudget}",
            $"  Top queries: {topQueries}",
            $"  Engrams captured: {engramsCaptured}",
            $"  Spontaneous generated: {spontaneousGenerated}",
            $"  Spontaneous delivered: {spontaneousDelivered}",
            $"  Engram replay generated: {engramReplayGenerated}",
            $"  Engram replay delivered: {engramReplayDelivered}",
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
            "Sleep replay validation:",
            $"  Stage: {sleepReplayStage}",
            $"  Replay selected: {sleepReplaySelected} | delivery ratio: {sleepReplayDeliveryRatio:0.000}",
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

    private static TopSchemaDisplay ParseTopSchemaDisplay(JsonElement root)
    {
        if (!TryGetProperty(root, "relationalSchemas", out var relationalSchemas) || relationalSchemas.ValueKind != JsonValueKind.Array)
        {
            return TopSchemaDisplay.Empty;
        }

        var topSchemaLines = new List<string>(6);
        var rank = 1;
        foreach (var schema in relationalSchemas.EnumerateArray().Take(6))
        {
            var source = GetString(schema, "source");
            var target = GetString(schema, "target");
            var circuitClass = GetString(schema, "circuitClass");
            var hemisphereRelation = GetString(schema, "hemisphereRelation");
            var neurotransmitter = GetString(schema, "neurotransmitter");
            var isFeedback = false;
            if (TryGetProperty(schema, "isFeedback", out var isFeedbackProp) &&
                isFeedbackProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                isFeedback = isFeedbackProp.GetBoolean();
            }

            var strength = GetDouble(schema, "strength");
            var novelty = GetDouble(schema, "noveltyScore");
            var salience = GetDouble(schema, "meanSalience");
            var captureCount = GetInt(schema, "captureCount");
            var replaySupportCount = GetInt(schema, "replaySupportCount");
            var feedbackTag = isFeedback ? ", fb" : string.Empty;
            topSchemaLines.Add(
                $"#{rank} {source}->{target} ({circuitClass}, {hemisphereRelation}, {neurotransmitter}{feedbackTag}) | str {strength:0.000}, nov {novelty:0.000}, sal {salience:0.000}, cap {captureCount}, rep {replaySupportCount}");
            rank++;
        }

        var text = topSchemaLines.Count == 0
            ? "  Top schemas: -"
            : string.Join(
                Environment.NewLine,
                new[] { "  Top schemas:" }.Concat(topSchemaLines.Select(line => $"    {line}")));
        return new TopSchemaDisplay(relationalSchemas.GetArrayLength(), text);
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

    private sealed record TopSchemaDisplay(int SchemaCount, string Text)
    {
        public static TopSchemaDisplay Empty { get; } = new(0, "  Top schemas: -");
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
