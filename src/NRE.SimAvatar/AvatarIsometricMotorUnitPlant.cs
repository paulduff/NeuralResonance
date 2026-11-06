using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

internal sealed record IsometricMotorUnitState(string MotorUnitId, Guid ContactId, double TimeMs,
    double ReleaseDrive, double FreeCalcium, double BoundCalcium, double Activation,
    double ForceNewtons, double ReleasedCalcium, double RecoveredCalcium, double NetBoundCalcium,
    double MaximumCalciumLedgerError, long Arrivals, Guid LastMessageId);

internal sealed record IsometricMotorPlantFrame(Guid SessionId, string AnatomyFingerprint, string PlantFingerprint,
    long CompletedTick, double BodyTimeMs, double StepMs, double CompleteThroughBodyMs,
    long AcceptedEvents, long DeliveredEvents, int PendingEvents, bool Faulted, string? Fault,
    IReadOnlyList<IsometricMotorUnitState> Units)
{
    public bool IsolatedReference => true;
    public bool PhysiologicalCalibrationComplete => false;
}

internal sealed record IsometricMotorPlantStep(IsometricMotorPlantFrame Frame, IReadOnlyList<AvatarMotorAxonArrival> Arrivals);

// Isolated, fixed-length/velocity reference plant. No live Avatar/Unity selects
// it. It accepts intact axonal events, not rates, signed drives or joint targets.
// Serialize callers; this is not a network transport or checkpoint service.
internal sealed class AvatarIsometricMotorUnitPlant
{
    public const string Version = "isolated-isometric-motor-unit-plant-v1";
    private readonly MotorAxonAnatomy _anatomy;
    private readonly MotorAxonClock _clock;
    private readonly AvatarMotorAxonReceiver _receiver;
    private readonly Dictionary<string, AvatarMotorUnitCalcium> _units;
    private readonly MotorAxonContact[] _orderedContacts;
    public double StepMs { get; }
    public string Fingerprint { get; }
    public long CompletedTick { get; private set; }
    public bool Faulted { get; private set; }
    public string? Fault { get; private set; }
    public double BodyTimeMs => TickTime(CompletedTick);

    public AvatarIsometricMotorUnitPlant(MotorAxonAnatomy anatomy, MotorAxonClock clock,
        IReadOnlyDictionary<string, MotorUnitCalciumParameters> parameters, int refinement = 0,
        int maximumPending = 65_536, int maximumEpochEvents = 1_000_000, int retainedBatches = 64)
    {
        ArgumentNullException.ThrowIfNull(anatomy); ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(parameters); clock.Validate();
        if (refinement is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(refinement));
        _anatomy = anatomy; _clock = clock; StepMs = AvatarMotorUnitCalcium.MaximumStepMs / (1 << refinement);
        if ((clock.BodyEpochMs + StepMs) - clock.BodyEpochMs != StepMs)
            throw new ArgumentException("The declared solver step is not representable at this body epoch.");
        _orderedContacts = anatomy.Contacts.OrderBy(c => c.MotorUnitId, StringComparer.Ordinal).ToArray();
        if (parameters.Count != _orderedContacts.Length || _orderedContacts.Any(c => !parameters.ContainsKey(c.MotorUnitId)))
            throw new ArgumentException("Declare exactly one parameter set for every anatomical motor unit.");
        _units = new(StringComparer.Ordinal);
        foreach (var contact in _orderedContacts)
            _units.Add(contact.MotorUnitId, new AvatarMotorUnitCalcium(parameters[contact.MotorUnitId], contact, clock));
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, CalciumVersion = AvatarMotorUnitCalcium.Version, Anatomy = anatomy.Fingerprint, StepMs,
            Units = _orderedContacts.Select(c => new { c.MotorUnitId, Parameters = parameters[c.MotorUnitId] }).ToArray()
        })));
        _receiver = new(anatomy, clock, maximumPending, maximumEpochEvents, retainedBatches);
    }

    public MotorAxonBatchDisposition Accept(MotorAxonBatch batch)
    {
        RequireOperational();
        return _receiver.Accept(batch); // Receiver validates the whole packet before committing it.
    }

    // Output endpoints are integer solver ticks, not arbitrary frame times.
    // Epoch + tick * step preserves the grid across packet/frame partitioning.
    // Future world integration must explicitly reconcile its own time domain.
    public IsometricMotorPlantStep AdvanceToTick(long tick)
    {
        RequireOperational();
        double until = TickTime(tick);
        if (tick < CompletedTick || until > _receiver.CompleteThroughBodyMs)
            throw new ArgumentOutOfRangeException(nameof(tick), "Wait for all declared neuronal sources; never rewind or assume silence.");
        if (tick > CompletedTick && (until <= BodyTimeMs || until - TickTime(tick - 1) != StepMs))
            throw new ArgumentOutOfRangeException(nameof(tick), "Clock resolution cannot represent the declared solver grid.");
        // Input validation precedes the drain. Any later numerical failure is
        // fail-stop and visible, never retried with a partially consumed queue.
        var arrivals = _receiver.AdvanceTo(until);
        try
        {
            var addressed = arrivals.GroupBy(a => a.MotorUnitId).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            foreach (var contact in _orderedContacts)
            {
                var cell = _units[contact.MotorUnitId];
                var local = addressed.GetValueOrDefault(contact.MotorUnitId) ?? [];
                int next = 0;
                for (long step = CompletedTick + 1; step <= tick; ++step)
                {
                    double endpoint = TickTime(step);
                    while (next < local.Length && local[next].ArrivalAtBodyMs <= endpoint)
                    {
                        AdvanceCell(cell, local[next].ArrivalAtBodyMs);
                        cell.Receive(local[next++]);
                    }
                    AdvanceCell(cell, endpoint);
                }
                if (next != local.Length) throw new InvalidOperationException("Undelivered local event; no event may disappear at a frame boundary.");
            }
            CompletedTick = tick;
            return new(Capture(), arrivals);
        }
        catch (Exception error)
        {
            Faulted = true; Fault = error.ToString(); throw;
        }
    }

    private double TickTime(long tick)
    {
        if (tick < 0 || tick > 9_007_199_254_740_991L) throw new ArgumentOutOfRangeException(nameof(tick));
        double result = _clock.BodyEpochMs + tick * StepMs;
        if (!double.IsFinite(result)) throw new ArgumentOutOfRangeException(nameof(tick));
        return result;
    }

    private static void AdvanceCell(AvatarMotorUnitCalcium cell, double until)
    {
        if (until < cell.TimeMs) throw new InvalidOperationException("Motor-unit event arrived behind its physical state.");
        if (until > cell.TimeMs) cell.Advance(until - cell.TimeMs);
    }

    private void RequireOperational()
    {
        if (Faulted) throw new InvalidOperationException("Faulted isolated plant cannot resume or accept more traffic.");
    }

    public IsometricMotorPlantFrame Capture()
    {
        var snapshots = _orderedContacts.Select(contact =>
        {
            var cell = _units[contact.MotorUnitId];
            return new IsometricMotorUnitState(contact.MotorUnitId, contact.ContactId, cell.TimeMs,
                cell.ReleaseDrive, cell.FreeCalcium, cell.BoundCalcium, cell.Activation, cell.IsometricForceNewtons,
                cell.ReleasedCalcium, cell.RecoveredCalcium, cell.NetBoundCalcium, cell.MaximumCalciumLedgerError,
                cell.Arrivals, cell.LastMessageId);
        }).ToArray();
        return new(_clock.SessionId, _anatomy.Fingerprint, Fingerprint, CompletedTick, BodyTimeMs, StepMs,
            _receiver.CompleteThroughBodyMs, _receiver.AcceptedEvents, _receiver.DeliveredEvents, _receiver.PendingEvents,
            Faulted, Fault, Array.AsReadOnly(snapshots));
    }
}
