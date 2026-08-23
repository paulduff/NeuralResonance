using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.WorldSim;

public sealed record WorldRunReport(
    string ProtocolVersion,
    DateTimeOffset PersistedUtc,
    string Reason,
    WorldSimulationSnapshot Snapshot,
    WorldRunStatistics Statistics);

public sealed record WorldRunStatistics(
    double ObservedSeconds,
    IReadOnlyDictionary<string, double> BalancePhaseSeconds,
    IReadOnlyDictionary<string, long> BalancePhaseEntries,
    double MinimumSupportMarginMeters,
    double MaximumDynamicStabilityAllowanceMeters,
    double MaximumAbsoluteFallPitchRadians,
    double MaximumAbsoluteFallRollRadians,
    double PeakCombinedHandLoadNewtons,
    double PeakCombinedFootLoadNewtons,
    double PeakObservedVerticalSupportLoadNewtons,
    long SpinalWithdrawalSamples,
    double SpinalWithdrawalActiveSeconds,
    double PeakSpinalWithdrawalDrive,
    long PostureConflictSamples,
    double PostureConflictSeconds,
    int PeakConcurrentPostureDrives,
    double LocomotorRecruitmentActiveSeconds,
    double IntegralLocomotorRecruitmentSeconds,
    double PeakLocomotorRecruitment,
    WorldGaitRunStatistics Gait,
    IReadOnlyList<WorldWithdrawalSourceRunStatistics> WithdrawalSources,
    IReadOnlyList<WorldContactRunStatistics> Contacts,
    IReadOnlyList<WorldMotorChannelRunStatistics> MotorChannels,
    IReadOnlyList<WorldDeathRunEvent> Deaths,
    ActionAuthorityCumulativeTelemetry? BrainActionAuthority = null);

public sealed record WorldDeathRunEvent(
    long WorldTick,
    double ElapsedSeconds,
    string PrimaryCause,
    double PrimaryCauseDamageFraction,
    double StoredEnergyJoules,
    double HydrationFraction,
    double TissueIntegrityFraction,
    string LastInteractionOutcome,
    IReadOnlyDictionary<string, double> TissueDamageByCause);

public sealed record WorldGaitRunStatistics(
    double EligibleSeconds,
    double LeftStanceSeconds,
    double RightStanceSeconds,
    double LeftSwingSeconds,
    double RightSwingSeconds,
    double DoubleSupportSeconds,
    double UnsupportedSeconds,
    long LeftStanceEntries,
    long RightStanceEntries,
    long LeftSwingEntries,
    long RightSwingEntries,
    long AlternatingSwingTransitions,
    long RepeatedSameSideSwingTransitions,
    long LeftClearedSwingEntries,
    long RightClearedSwingEntries,
    double MaximumLeftSwingSeconds,
    double MaximumRightSwingSeconds,
    double PeakLeftSwingClearanceMeters,
    double PeakRightSwingClearanceMeters);

public sealed record WorldWithdrawalSourceRunStatistics(
    string SourceKey,
    string BodySide,
    string Region,
    string ContactNormalSector,
    int ChannelIndex,
    string MotorProjection,
    long Samples,
    long EpisodeCount,
    double ActiveSeconds,
    double MaximumContinuousSeconds,
    double IntegralAfferentDriveSeconds,
    double IntegralReflexDriveSeconds,
    double PeakAfferentDrive,
    double PeakReflexDrive,
    double PeakRecurrentInhibition,
    double MaximumAfferentAgeMilliseconds);

public sealed record WorldContactRunStatistics(
    string Source,
    string Region,
    long Samples,
    double TotalObservedSeconds,
    double MaximumContinuousSeconds,
    double PeakForceNewtons,
    double PeakImpulseNewtonSeconds,
    double PeakVerticalSupportNewtons);

public sealed record WorldMotorChannelRunStatistics(
    string Channel,
    long Samples,
    double ActiveSeconds,
    double PositiveDriveSeconds,
    double NegativeDriveSeconds,
    double IntegralAbsoluteDriveSeconds,
    double PeakAbsoluteDrive);

internal static class WorldRunReportStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string WriteAtomic(
        string directory,
        string reason,
        WorldSimulationSnapshot snapshot,
        WorldRunStatistics statistics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(statistics);
        Directory.CreateDirectory(directory);

        var safeReason = new string(reason
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');
        if (safeReason.Length == 0)
        {
            safeReason = "snapshot";
        }

        var timestamp = DateTimeOffset.UtcNow;
        var fileName = $"world-run-{snapshot.SessionId}-{snapshot.WorldTick:D10}-{safeReason}-{timestamp:yyyyMMddTHHmmssfffZ}.json";
        var destination = Path.Combine(directory, fileName);
        return WriteAtomicToPath(destination, timestamp, reason, snapshot, statistics);
    }

    public static string WriteRollingAtomic(
        string directory,
        WorldSimulationSnapshot snapshot,
        WorldRunStatistics statistics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(statistics);
        Directory.CreateDirectory(directory);

        var timestamp = DateTimeOffset.UtcNow;
        var destination = Path.Combine(directory, $"world-run-{snapshot.SessionId}-rolling.json");
        return WriteAtomicToPath(destination, timestamp, "rolling-heartbeat", snapshot, statistics);
    }

    private static string WriteAtomicToPath(
        string destination,
        DateTimeOffset timestamp,
        string reason,
        WorldSimulationSnapshot snapshot,
        WorldRunStatistics statistics)
    {
        var temporary = destination + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        var report = new WorldRunReport("dnne.world-run.v8", timestamp, reason, snapshot, statistics);
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(report, JsonOptions));
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
