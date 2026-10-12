using System;

namespace Nre.UnityWorld
{
    public sealed class StandingObservation
    {
        public bool finite, authorizedMotor, legActuationApplied;
        public long motorTick;
        public double uprightFraction, pelvisHeight, initialPelvisHeight;
        public double leftFootLoad, rightFootLoad, bodyWeight, otherSupportLoad;
        public double supportMargin, limitViolation, standDrive;
    }

    // Measurement only. This class cannot select an action or command the body.
    [Serializable]
    public sealed class StandingTrialMetrics
    {
        public double simulationSeconds, standingSeconds, currentStandingSeconds, longestStandingSeconds;
        public double minimumLeftLoadShare = 1, maximumLeftLoadShare, leftDominantSeconds, rightDominantSeconds;
        public double maximumLimitViolation, maximumStandDrive;
        public long samples, authorizedSteps, stepsWithLegActuation, uniqueAuthorizedMotorTicks, fallingSamples;
        public bool finite = true;
        long lastMotorTick = -1;

        public bool StandingCriterionMet => finite && currentStandingSeconds >= 10 &&
            maximumLimitViolation < .05 && stepsWithLegActuation > 0 && uniqueAuthorizedMotorTicks >= 2;
        public bool WeightTransferObserved => leftDominantSeconds >= .5 && rightDominantSeconds >= .5;

        public void Observe(double seconds, StandingObservation sample)
        {
            if (seconds <= 0 || !Finite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            samples++;
            simulationSeconds += seconds;
            finite &= sample.finite && Array.TrueForAll(new[] { sample.uprightFraction, sample.pelvisHeight,
                sample.initialPelvisHeight, sample.leftFootLoad, sample.rightFootLoad, sample.bodyWeight,
                sample.otherSupportLoad, sample.supportMargin, sample.limitViolation, sample.standDrive }, Finite);
            if (!finite) { currentStandingSeconds = 0; return; }
            maximumLimitViolation = Math.Max(maximumLimitViolation, sample.limitViolation);
            if (sample.authorizedMotor)
            {
                authorizedSteps++;
                maximumStandDrive = Math.Max(maximumStandDrive, sample.standDrive);
                if (sample.legActuationApplied) stepsWithLegActuation++;
                if (sample.motorTick > lastMotorTick) { uniqueAuthorizedMotorTicks++; lastMotorTick = sample.motorTick; }
            }
            double totalFootLoad = sample.leftFootLoad + sample.rightFootLoad;
            bool standing = sample.bodyWeight > 0 && sample.initialPelvisHeight > 0 &&
                sample.uprightFraction >= .85 && sample.pelvisHeight >= sample.initialPelvisHeight * .7 &&
                sample.leftFootLoad >= sample.bodyWeight * .1 && sample.rightFootLoad >= sample.bodyWeight * .1 &&
                totalFootLoad >= sample.bodyWeight * .7 && sample.otherSupportLoad <= sample.bodyWeight * .05 &&
                sample.supportMargin >= 0 && sample.limitViolation < .05;
            if (sample.uprightFraction < .3 || sample.pelvisHeight < sample.initialPelvisHeight * .4) fallingSamples++;
            if (!standing) { currentStandingSeconds = 0; return; }
            standingSeconds += seconds;
            currentStandingSeconds += seconds;
            longestStandingSeconds = Math.Max(longestStandingSeconds, currentStandingSeconds);
            double share = sample.leftFootLoad / totalFootLoad;
            minimumLeftLoadShare = Math.Min(minimumLeftLoadShare, share);
            maximumLeftLoadShare = Math.Max(maximumLeftLoadShare, share);
            if (share >= .6) leftDominantSeconds += seconds;
            if (share <= .4) rightDominantSeconds += seconds;
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
