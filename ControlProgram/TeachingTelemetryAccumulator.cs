using NeuralResonanceEngine.Shared.Contracts;

internal sealed class TeachingTelemetryAccumulator
{
    private const float EventThreshold = 0.0001f;
    private readonly object _gate = new();
    private readonly Dictionary<string, MutableCause> _causes = new(StringComparer.Ordinal);
    private long _physicalFramesObserved;
    private long _respawnTransitions;
    private long _lastPhysicalFrameTick;
    private long _homeostaticDispatches;
    private long _homeostaticFramesBuffered;
    private int _homeostaticCadenceMilliseconds;
    private long _lastHomeostaticDispatchTick;

    public void Observe(long tick, PhysicalBodyTransduction transduction)
    {
        ArgumentNullException.ThrowIfNull(transduction);
        lock (_gate)
        {
            _physicalFramesObserved++;
            _lastPhysicalFrameTick = tick;
            _homeostaticCadenceMilliseconds = transduction.HomeostaticCadenceMilliseconds;
            if (transduction.HomeostaticCadenceDispatch)
            {
                _homeostaticDispatches++;
                _lastHomeostaticDispatchTick = tick;
            }
            else
            {
                _homeostaticFramesBuffered++;
            }
            if (transduction.RespawnTransition)
            {
                _respawnTransitions++;
            }

            Record("energy_restoration", transduction.EnergyRestorationTeachingSignal, tick);
            Record("hydration_restoration", transduction.HydrationRestorationTeachingSignal, tick);
            Record("tissue_recovery", Math.Max(0f, transduction.TissueChange), tick);
            Record("balance_recovery", Math.Max(
                Math.Max(0f, transduction.SupportMarginImprovement),
                Math.Max(0f, transduction.BalanceImprovement)), tick);
            Record("movement_bootstrap_observation",
                transduction.MotorTrainingMode ? transduction.MotionMagnitude : 0f,
                tick);
            Record("ineffective_force", transduction.IneffectiveForceEvidence, tick);
            Record("fatigue", transduction.PeakMuscleFatigueDistress, tick);
            Record("injury", Math.Max(0f, -transduction.TissueChange), tick);
            Record("death", transduction.DeathTransition ? 1f : 0f, tick);
            Record("positive_phasic_total", transduction.PositiveTeachingSignal, tick);
            Record("negative_phasic_total", transduction.NegativeTeachingSignal, tick);
        }
    }

    public TeachingTelemetry GetSnapshot()
    {
        lock (_gate)
        {
            return new TeachingTelemetry(
                _causes
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair => new TeachingCauseTelemetry(
                        pair.Key,
                        pair.Value.EventCount,
                        pair.Value.MagnitudeSum,
                        pair.Value.PeakMagnitude,
                        pair.Value.LastObservedTick))
                    .ToArray(),
                _physicalFramesObserved,
                _respawnTransitions,
                _lastPhysicalFrameTick,
                _homeostaticDispatches,
                _homeostaticFramesBuffered,
                _homeostaticCadenceMilliseconds,
                _lastHomeostaticDispatchTick);
        }
    }

    private void Record(string cause, float magnitude, long tick)
    {
        if (!float.IsFinite(magnitude) || magnitude <= EventThreshold)
        {
            return;
        }

        if (!_causes.TryGetValue(cause, out var state))
        {
            state = new MutableCause();
            _causes[cause] = state;
        }

        state.EventCount++;
        state.MagnitudeSum += magnitude;
        state.PeakMagnitude = Math.Max(state.PeakMagnitude, magnitude);
        state.LastObservedTick = tick;
    }

    private sealed class MutableCause
    {
        public long EventCount { get; set; }
        public double MagnitudeSum { get; set; }
        public float PeakMagnitude { get; set; }
        public long LastObservedTick { get; set; }
    }
}
