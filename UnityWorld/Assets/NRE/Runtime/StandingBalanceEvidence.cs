using System;

namespace Nre.UnityWorld
{
    public sealed class StandingBalanceObservation
    {
        public double supportMarginMeters, pitchRadians, rollRadians, balanceError;
        public double leftFootLoadNewtons, rightFootLoadNewtons;
        public bool fallen;
    }

    // Read-only physical diagnostics. Neither a sensory neuron nor a controller:
    // a nonzero error, lost support, or return to support proves no neural action.
    [Serializable]
    public sealed class StandingBalanceEvidence
    {
        public const string Version = "standing-balance-evidence-v1";
        public string version = Version;
        public long initialPhysicsStep = -1, lastPhysicsStep = -1, completedSteps;
        public double stepSeconds;
        public bool initialFeetLoaded, initialMarginNegative, initialFallen;
        public long marginExitEvents, footSupportLossEvents;
        public double firstNegativeMarginSeconds = -1, firstMarginExitSeconds = -1;
        public double firstFootSupportLossSeconds = -1, firstFallenSeconds = -1;
        public double maximumBalanceError, maximumAbsPitchRadians, maximumAbsRollRadians;
        bool previousMarginNegative, previousFeetLoaded;

        public void Begin(long physicsStep, double seconds, StandingBalanceObservation sample)
        {
            if (initialPhysicsStep >= 0) throw new InvalidOperationException("The physical baseline is already recorded.");
            if (physicsStep < 0 || !Finite(seconds) || seconds <= 0) throw new ArgumentException("Invalid physical clock.");
            Validate(sample);
            initialPhysicsStep = lastPhysicsStep = physicsStep;
            stepSeconds = seconds;
            initialFeetLoaded = FeetLoaded(sample);
            initialMarginNegative = sample.supportMarginMeters < 0;
            initialFallen = sample.fallen;
            Accumulate(0, sample, false);
        }

        public void Observe(long physicsStep, StandingBalanceObservation sample)
        {
            if (initialPhysicsStep < 0) throw new InvalidOperationException("Record the actual baseline before stepping.");
            if (lastPhysicsStep == long.MaxValue || physicsStep != lastPhysicsStep + 1)
                throw new ArgumentException("Retain every consecutive physical endpoint exactly once.");
            Validate(sample);
            long steps = checked(completedSteps + 1);
            double time = steps * stepSeconds;
            if (!Finite(time)) throw new ArgumentException("Unrepresentable physical time.");
            Accumulate(time, sample, true);
            completedSteps = steps;
            lastPhysicsStep = physicsStep;
        }

        void Accumulate(double time, StandingBalanceObservation sample, bool comparePrevious)
        {
            bool negative = sample.supportMarginMeters < 0, loaded = FeetLoaded(sample);
            if (negative && firstNegativeMarginSeconds < 0) firstNegativeMarginSeconds = time;
            if (comparePrevious && negative && !previousMarginNegative)
            {
                marginExitEvents++;
                if (firstMarginExitSeconds < 0) firstMarginExitSeconds = time;
            }
            // An initially unsupported spawn is not a transition out of support.
            // One unloaded foot during weight transfer is not loss of both feet.
            if (comparePrevious && previousFeetLoaded && !loaded)
            {
                footSupportLossEvents++;
                if (firstFootSupportLossSeconds < 0) firstFootSupportLossSeconds = time;
            }
            if (sample.fallen && firstFallenSeconds < 0) firstFallenSeconds = time;
            maximumBalanceError = Math.Max(maximumBalanceError, sample.balanceError);
            maximumAbsPitchRadians = Math.Max(maximumAbsPitchRadians, Math.Abs(sample.pitchRadians));
            maximumAbsRollRadians = Math.Max(maximumAbsRollRadians, Math.Abs(sample.rollRadians));
            previousMarginNegative = negative;
            previousFeetLoaded = loaded;
        }

        static bool FeetLoaded(StandingBalanceObservation sample) => sample.leftFootLoadNewtons > 0 || sample.rightFootLoadNewtons > 0;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static void Validate(StandingBalanceObservation sample)
        {
            if (sample == null || !Finite(sample.supportMarginMeters) || !Finite(sample.pitchRadians) || !Finite(sample.rollRadians) ||
                !Finite(sample.balanceError) || sample.balanceError < 0 || sample.balanceError > 1 ||
                !Finite(sample.leftFootLoadNewtons) || sample.leftFootLoadNewtons < 0 ||
                !Finite(sample.rightFootLoadNewtons) || sample.rightFootLoadNewtons < 0)
                throw new ArgumentException("Preserve finite physical measurements without replacing invalid values.");
        }
    }
}
