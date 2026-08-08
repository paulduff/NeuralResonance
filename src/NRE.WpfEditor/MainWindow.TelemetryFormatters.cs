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

    private static string FormatBrainDashboard(JsonElement root)
    {
        root = NormalizeStateRoot(root);
        var hasPerception = TryGetObject(root, "neuronalPerception", out var perception);
        var hasMemory = TryGetObject(root, "neuronalMemory", out var memory);
        var hasAttention = TryGetObject(root, "neuronalAttentionWorkspace", out var attention);
        var hasSleep = TryGetObject(root, "neuronalSleepConsolidation", out var sleep);
        var hasAffect = TryGetObject(root, "neuronalAffectValuation", out var affect);
        var hasExecutive = TryGetObject(root, "neuronalExecutive", out var executive);
        var hasLanguage = TryGetObject(root, "neuronalLanguageGrounding", out var language);
        var hasMotor = TryGetObject(root, "neuronalMotor", out var motor);

        if (!hasPerception && !hasMemory && !hasAttention && !hasSleep &&
            !hasAffect && !hasExecutive && !hasLanguage && !hasMotor)
        {
            return "Neuronal dashboard unavailable: state payload contains no neuronal decoder state.";
        }

        return string.Join(Environment.NewLine, new[]
        {
            "Measured neuronal decoder dashboard",
            $"Tick: {GetLong(root, "tick")}",
            $"Perception: {(GetBool(perception, "active") ? "active" : "quiet")} | ensemble {GetInt(perception, "dominantEnsemble")} | confidence {GetDouble(perception, "confidence"):0.000} | coverage {GetDouble(perception, "circuitCoverage"):0.000}",
            $"Memory: {(GetBool(memory, "recallActive") ? "recalling" : "quiet")} | ensemble {GetInt(memory, "recalledEnsemble")} | strength {GetDouble(memory, "recallStrength"):0.000} | consolidation {GetDouble(memory, "corticalConsolidation"):0.000}",
            $"Attention: {(GetBool(attention, "active") ? "active" : "quiet")} | selected {GetInt(attention, "selectedChannel")} | broadcast {GetInt(attention, "broadcastChannel")} | confidence {GetDouble(attention, "confidence"):0.000}",
            string.Empty,
            $"Sleep: {(GetBool(sleep, "stateActive") ? $"state {GetInt(sleep, "state")}" : "quiet")} | confidence {GetDouble(sleep, "stateConfidence"):0.000} | replay {(GetBool(sleep, "replayActive") ? GetInt(sleep, "replayEnsemble").ToString() : "idle")}",
            $"Affect: {(GetBool(affect, "active") ? "active" : "quiet")} | channel {GetInt(affect, "dominantChannel")} | confidence {GetDouble(affect, "confidence"):0.000}",
            $"Affect populations A/D/H/E: {GetDouble(affect, "appetitiveDrive"):0.000} | {GetDouble(affect, "defensiveDrive"):0.000} | {GetDouble(affect, "homeostaticDrive"):0.000} | {GetDouble(affect, "exploratoryDrive"):0.000}",
            $"Executive: {(GetBool(executive, "active") ? "active" : "quiet")} | action channel {GetInt(executive, "selectedActionChannel")} | context channel {GetInt(executive, "maintainedContextChannel")} | confidence {GetDouble(executive, "confidence"):0.000}",
            string.Empty,
            $"Language grounding: {(GetBool(language, "grounded") ? "grounded" : "deferred")} | percept {GetInt(language, "perceptEnsemble")} | recall {GetInt(language, "memoryEnsemble")} | attention {GetInt(language, "attentionChannel")}",
            $"Language confidence/uncertainty/coverage: {GetDouble(language, "groundingConfidence"):0.000} | {GetDouble(language, "uncertainty"):0.000} | {GetDouble(language, "languageCircuitCoverage"):0.000} | speech authorized {GetBool(language, "speechAuthorized")}",
            string.Empty,
            $"Motor: {(GetBool(motor, "active") ? "active" : "quiet")} | channel {GetInt(motor, "selectedActionChannel")} | confidence {GetDouble(motor, "confidence"):0.000} | inhibition {GetDouble(motor, "outputInhibition"):0.000}",
            $"Motor drives L/R/F/T: {GetDouble(motor, "leftDrive"):0.000} | {GetDouble(motor, "rightDrive"):0.000} | {GetDouble(motor, "forwardDrive"):0.000} | {GetDouble(motor, "turnDrive"):0.000}",
            "All values are read-only measurements; this dashboard cannot authorize cognition or movement."
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
            $"  Inhibitory scale: {sleepInhibitoryScale:0.000} | excitatory scale: {sleepExcitatoryScale:0.000}"
        });
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
