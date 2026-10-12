using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    public sealed class StandingTrial
    {
        readonly double duration;
        readonly StandingTrialMetrics metrics = new StandingTrialMetrics();
        bool prepared, started, stopped, saved;
        string failure = "";
        float startedAt;
        long packetsAtStart;
        double nextTraceAt;
        StreamWriter trace;
        public bool Finished => saved;
        public string Caption => saved ? "Trial complete · paused" :
            $"Standing observation · {metrics.simulationSeconds:F1}/{duration:F0} simulation seconds";

        public StandingTrial(string seconds)
        {
            if (!double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out duration) ||
                double.IsNaN(duration) || double.IsInfinity(duration) || duration < 10 || duration > 120)
                throw new ArgumentException("Standing trial duration must be 10–120 simulation seconds.");
        }

        public IEnumerator Run(WorldController world)
        {
            world.PauseTrial("Paused · preparing DNNE standing observation");
            trace = new StreamWriter(Path.Combine(world.outputDirectory, "standing-trace.jsonl"));
            yield return world.ConnectForTrial();
            if (!world.Connected) { failure = "Exclusive world handoff failed."; stopped = true; }
            prepared = true;
            float waitingAt = Time.realtimeSinceStartup;
            while (!stopped)
            {
                if (!started && Time.realtimeSinceStartup - waitingAt > 1200)
                { failure = "Trial was not started within 20 minutes."; stopped = true; }
                if (!started && Time.realtimeSinceStartup - waitingAt > 45 && !world.ReadyForTrial)
                { failure = "DNNE motor telemetry or lease readiness was not established."; stopped = true; }
                if (!world.Connected) { failure = "DNNE connection was released."; stopped = true; }
                if (started && !world.ReadyForTrial)
                { failure = "Fresh brain telemetry or the exclusive world lease was lost."; stopped = true; }
                if (started && Time.realtimeSinceStartup - startedAt > 180)
                { failure = "Trial exceeded its 180-second wall-time budget."; stopped = true; }
                yield return null;
            }
            world.PauseTrial(string.IsNullOrEmpty(failure) ? "Paused · standing observation complete" : "Paused · " + failure);
            // Finish already-issued sensory requests before publishing final counters.
            yield return new WaitForSecondsRealtime(2.2f);
            yield return world.DisconnectForTrial();
            Save(world);
        }

        public void AfterStep(WorldController world)
        {
            if (!prepared || stopped) return;
            if (!started) { started = true; startedAt = Time.realtimeSinceStartup; packetsAtStart = world.motorPacketsReceived; }
            var body = world.Avatar;
            var frame = BodyFrame.Capture(body, world.physicsSteps);
            var physical = frame.articulation.musculoskeletal;
            var motor = world.LastAppliedCommand;
            metrics.Observe(WorldController.StepSeconds, new StandingObservation {
                finite = body.IsFinite(), authorizedMotor = motor.IsAuthorized(), motorTick = motor.tick,
                legActuationApplied = world.LegActuationApplied, standDrive = motor.standDrive,
                uprightFraction = physical.uprightFraction, pelvisHeight = body.pelvis.transform.position.y,
                initialPelvisHeight = body.initialPelvisHeight, leftFootLoad = body.LeftLoad, rightFootLoad = body.RightLoad,
                bodyWeight = body.totalMass * 9.81, supportMargin = physical.balance.supportMarginMeters,
                otherSupportLoad = body.links.Values.Where(l => !l.name.EndsWith("foot") && l.sensor.supportPoints.Count > 0).Sum(l => l.sensor.force),
                limitViolation = ActuatorQualification.LimitViolation(body) });
            if (metrics.simulationSeconds >= nextTraceAt)
            {
                var sample = new TraceSample { simulationSeconds = metrics.simulationSeconds,
                    physicsStep = world.physicsSteps, motor = motor, observedMotor = world.ObservedMotor,
                    motorAgeSeconds = world.MotorAgeSeconds, bodyRequestSeconds = world.LastBodyRequestSeconds,
                    legActuationApplied = world.LegActuationApplied,
                    body = frame, bodyFramesAccepted = world.bodyFramesAccepted,
                    bodySpikesGenerated = world.bodySpikesGenerated, retinalFramesAccepted = world.retinalFramesAccepted };
                trace.WriteLine(JsonUtility.ToJson(sample));
                trace.Flush();
                nextTraceAt = metrics.simulationSeconds + .2;
                WriteProgress(world);
            }
            if (!metrics.finite) { failure = "Non-finite physical state."; stopped = true; }
            if (metrics.simulationSeconds + .000001 >= duration) stopped = true;
            if (stopped) world.PauseTrial("Paused · finishing standing observation");
        }

        void WriteProgress(WorldController world)
        {
            var progress = new Progress { simulationSeconds = metrics.simulationSeconds, durationSeconds = duration,
                standingSeconds = metrics.standingSeconds, longestStandingSeconds = metrics.longestStandingSeconds,
                stepsWithLegActuation = metrics.stepsWithLegActuation, bodyFramesAccepted = world.bodyFramesAccepted,
                bodySpikesGenerated = world.bodySpikesGenerated, retinalFramesAccepted = world.retinalFramesAccepted };
            string destination = Path.Combine(world.outputDirectory, "standing-progress.json");
            AtomicJson(destination, JsonUtility.ToJson(progress));
        }

        static void AtomicJson(string destination, string json)
        {
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }

        public void OnClosing(WorldController world)
        {
            if (saved) return;
            failure = "Player closed before the observation completed.";
            stopped = true;
            Save(world);
        }

        void Save(WorldController world)
        {
            if (saved) return;
            trace?.Dispose(); trace = null;
            bool completed = started && string.IsNullOrEmpty(failure) && metrics.simulationSeconds + .000001 >= duration;
            var report = new Report { unity = Application.unityVersion, error = failure, completed = completed,
                operatorStarted = started, finite = metrics.finite && world.Avatar.IsFinite(), metrics = metrics,
                motorPacketsReceived = world.motorPacketsReceived, motorPacketsDuringTrial = world.motorPacketsReceived-packetsAtStart,
                maximumBodyRequestSeconds = world.MaximumBodyRequestSeconds, bodyFramesAccepted = world.bodyFramesAccepted,
                bodySpikesGenerated = world.bodySpikesGenerated, retinalFramesAccepted = world.retinalFramesAccepted,
                rejectedFrames = world.rejectedFrames, bodyFramesRejected = world.bodyFramesRejected,
                contactFramesAccepted = world.contactFramesAccepted, contactFramesRejected = world.contactFramesRejected,
                bodyFeedbackObserved = world.bodyFramesAccepted > 0 && world.bodySpikesGenerated > 0,
                freshMotorObserved = world.motorPacketsReceived-packetsAtStart >= 2,
                disconnectedAtCompletion = !world.Connected, worldLeaseReleaseAccepted = world.LeaseReleaseAccepted,
                pausedAtCompletion = !world.Running };
            report.integrationChecksPassed = completed && report.finite && report.bodyFeedbackObserved &&
                report.freshMotorObserved && report.retinalFramesAccepted > 0 && report.disconnectedAtCompletion &&
                report.worldLeaseReleaseAccepted && report.pausedAtCompletion;
            report.standingCriterionMet = report.integrationChecksPassed && metrics.StandingCriterionMet;
            report.weightTransferObserved = metrics.WeightTransferObserved;
            AtomicJson(Path.Combine(world.outputDirectory, "standing-report.json"), JsonUtility.ToJson(report, true));
            saved = true;
            world.WriteReport();
            Debug.Log("Standing observation: " + JsonUtility.ToJson(report));
        }

        [Serializable] sealed class TraceSample {
            public double simulationSeconds; public long physicsStep, bodyFramesAccepted, bodySpikesGenerated, retinalFramesAccepted;
            public bool legActuationApplied; public MotorFrame motor, observedMotor; public BodyFrame body;
            public float motorAgeSeconds, bodyRequestSeconds;
        }
        [Serializable] sealed class Progress {
            public double simulationSeconds, durationSeconds, standingSeconds, longestStandingSeconds;
            public long stepsWithLegActuation, bodyFramesAccepted, bodySpikesGenerated, retinalFramesAccepted;
        }
        [Serializable] public sealed class Report {
            public string unity, error; public StandingTrialMetrics metrics;
            public bool completed, operatorStarted, finite, integrationChecksPassed, standingCriterionMet, weightTransferObserved;
            public bool bodyFeedbackObserved, freshMotorObserved, disconnectedAtCompletion, worldLeaseReleaseAccepted, pausedAtCompletion;
            // API admission/generated spikes do not prove deferred neuronal delivery or learning.
            public bool sensoryDeliveryVerified = false, learnedStandingAssessed = false, walkingAssessed = false;
            public long motorPacketsReceived, motorPacketsDuringTrial, bodyFramesAccepted, bodySpikesGenerated, retinalFramesAccepted, rejectedFrames;
            public float maximumBodyRequestSeconds;
            public long bodyFramesRejected, contactFramesAccepted, contactFramesRejected;
        }
    }
}
