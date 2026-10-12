using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    public static class BalanceCapability
    {
        [Serializable] public sealed class Case
        {
            public string name,recruitment;
            public MuscleFatiguePolicy fatiguePolicy;
            public bool feedback,finite=true,standingHeld,settlingPassed,hipTargetsAbsolute,retainFullAnkleCorrection,coronalAnkleTargetsAbsolute;
            public float positionGain,velocityGain,coronalHipShare,coronalGainRatio,coronalDerivativeGain,maximumSettledLinearSpeed,maximumSettledJointSpeed;
            public string worstLimitJoint;public int worstLimitAxis;public double worstLimitTime;
            public float maximumLimitViolation;
            public float maximumReportedFatigueFraction;
            public float hipSagittalOffsetRadians,hipCoronalOffsetRadians,maximumActivationWhileStanding,maximumFatigueWhileStanding;
            public double meanSquaredActivationWhileStanding;
            public int standingEffortSamples;
            public int disturbancesApplied;
            public StandingTrialMetrics metrics=new StandingTrialMetrics();
        }
        [Serializable] public sealed class Report
        {
            public string unity,error="";
            public int protocolVersion=6;public float durationSeconds=120,minimumFinalStandingSeconds=10;
            public float postureRampStartSeconds=.5f,postureRampDurationSeconds=2;
            public bool completed,finite=true,isolated,anyDiagnosticStandingHold;
            public bool neuronalControlAssessed=false,learnedStandingAssessed=false,walkingAssessed=false;
            public List<Case> cases=new List<Case>();
        }
        [Serializable] public sealed class Trace
        {
            public double seconds;
            public float preStepErrorX,preStepErrorZ,preStepVelocityX,preStepVelocityZ;
            public Vector3 centerOfMass,supportCenter;
            public MotorFrame command;
            public float diagnosticHipSagittalOffsetRadians,diagnosticHipCoronalOffsetRadians;
            public BodyFrame body;
        }

        public static IEnumerator Run(WorldController world)
        {
            yield return null;
            var report=new Report{unity=Application.unityVersion};
            var configurations=new List<Case>{Configuration(0,0)};
            configurations.AddRange(from sagittal in new[]{-.08f,-.04f,0,.04f,.08f}
                from coronal in new[]{-.12f,-.06f,0,.06f,.12f}
                where sagittal!=0||coronal!=0 select Configuration(sagittal,coronal));
            int steps=Mathf.RoundToInt(report.durationSeconds/WorldController.StepSeconds);
            foreach(var result in configurations)
            {
                string policy=result.recruitment;var gains=new Vector2(result.positionGain,result.velocityGain);
                report.cases.Add(result);
                string name=result.name;
                try
                {
                    if(world.Connected||!world.IsolatedQualification)throw new InvalidOperationException("Balance fixture must be isolated.");
                    world.DiagnosticMode=true;world.ResetDiagnosticBody(true,policy!="canonical",policy!="full-position");
                    world.Avatar.muscles.DiagnosticFatiguePolicy=result.fatiguePolicy;
                    using(var trace=new StreamWriter(Path.Combine(world.outputDirectory,name+"-trace.jsonl")))
                    for(int step=0;step<steps;step++)
                    {
                        var body=world.Avatar;
                        var frame=BodyFrame.Capture(body,step);var state=frame.articulation.musculoskeletal;
                        // Same gravity-aligned heading as the body sensory frame.
                        var forward=Vector3.ProjectOnPlane(body.pelvis.transform.forward,Vector3.up);
                        var heading=Quaternion.Inverse(forward.sqrMagnitude>.0001f?Quaternion.LookRotation(forward.normalized):Quaternion.identity);
                        var com=body.CenterOfMass();
                        var supportCenter=(body.links["left_foot"].body.GetComponentInChildren<BoxCollider>().bounds.center+
                            body.links["right_foot"].body.GetComponentInChildren<BoxCollider>().bounds.center)*.5f;
                        var error=heading*(com-supportCenter);
                        var velocity=Vector3.zero;foreach(var l in body.links.Values)velocity+=l.body.linearVelocity*l.body.mass;
                        velocity=heading*(velocity/body.totalMass);
                        bool supported=state.uprightFraction>.7f&&body.pelvis.transform.position.y>.6f*body.initialPelvisHeight&&
                            body.LeftLoad+body.RightLoad>.5f*body.totalMass*9.81f;
                        var command=result.feedback?BalanceDiagnosticController.Command(gains.x,gains.y,error.x,error.z,velocity.x,velocity.z,
                            body.Angle("left_foot"),body.Angle("right_foot"),body.Angle("left_foot",1),body.Angle("right_foot",1),supported,
                            result.coronalHipShare,body.Angle("left_thigh",1),body.Angle("right_thigh",1),result.coronalGainRatio,
                            result.hipTargetsAbsolute,result.retainFullAnkleCorrection,result.coronalAnkleTargetsAbsolute,
                            result.coronalDerivativeGain):new MotorFrame{standDrive=1};
                        float postureBlend=supported?Mathf.SmoothStep(0,1,Mathf.Clamp01(
                            (step*WorldController.StepSeconds-report.postureRampStartSeconds)/report.postureRampDurationSeconds)):0;
                        body.muscles.DiagnosticHipSagittalOffsetRadians=result.hipSagittalOffsetRadians*postureBlend;
                        BalanceDiagnosticController.AddCoronalOffsets(command,result.hipCoronalOffsetRadians*postureBlend,
                            -result.hipCoronalOffsetRadians*postureBlend,0,0);
                        world.DiagnosticCommand=command;
                        // Prescribed external pushes test recovery; they never provide balancing assistance.
                        if(step==800||step==1400||step==2200){body.pelvis.AddForce((step==800?Vector3.forward:step==1400?Vector3.right:Vector3.left)*1.5f,ForceMode.Impulse);result.disturbancesApplied++;}
                        world.Step();frame=BodyFrame.Capture(body,step+1);state=frame.articulation.musculoskeletal;
                        result.finite&=body.IsFinite()&&command.IsFinite()&&state.muscles.All(m=>
                            float.IsFinite(m.activation)&&float.IsFinite(m.forceNewtons)&&float.IsFinite(m.lengthFraction)&&float.IsFinite(m.velocityPerSecond)&&float.IsFinite(m.fatigueFraction));
                        result.maximumReportedFatigueFraction=Mathf.Max(result.maximumReportedFatigueFraction,state.muscles.Max(m=>m.fatigueFraction));
                        foreach(var link in body.links.Values)for(int axis=0;axis<link.dofs;axis++){
                            var range=ArticulatedAvatar.JointDrive(link,axis);float angle=body.Angle(link.name,axis);
                            float violation=Mathf.Max(0,range.lowerLimit*Mathf.Deg2Rad-angle,angle-range.upperLimit*Mathf.Deg2Rad);
                            if(violation>result.maximumLimitViolation){result.maximumLimitViolation=violation;result.worstLimitJoint=link.name;result.worstLimitAxis=axis;result.worstLimitTime=(step+1)*WorldController.StepSeconds;}
                        }
                        double previousStanding=result.metrics.standingSeconds;
                        result.metrics.Observe(WorldController.StepSeconds,new StandingObservation{
                            finite=result.finite,uprightFraction=state.uprightFraction,pelvisHeight=body.pelvis.transform.position.y,
                            initialPelvisHeight=body.initialPelvisHeight,leftFootLoad=body.LeftLoad,rightFootLoad=body.RightLoad,
                            bodyWeight=body.totalMass*9.81,otherSupportLoad=body.links.Values.Where(l=>!l.name.EndsWith("foot")&&l.sensor.supportPoints.Count>0).Sum(l=>l.sensor.force),
                            supportMargin=state.balance.supportMarginMeters,limitViolation=ActuatorQualification.LimitViolation(body)});
                        if(result.metrics.standingSeconds>previousStanding){
                            result.meanSquaredActivationWhileStanding+=state.muscles.Average(m=>(double)m.activation*m.activation);
                            result.maximumActivationWhileStanding=Mathf.Max(result.maximumActivationWhileStanding,state.muscles.Max(m=>m.activation));
                            result.maximumFatigueWhileStanding=Mathf.Max(result.maximumFatigueWhileStanding,state.muscles.Max(m=>m.fatigueFraction));
                            result.standingEffortSamples++;
                        }
                        if(step>=steps-200)foreach(var l in body.links.Values){
                            result.maximumSettledLinearSpeed=Mathf.Max(result.maximumSettledLinearSpeed,l.body.linearVelocity.magnitude);
                            for(int axis=0;axis<l.body.dofCount;axis++)result.maximumSettledJointSpeed=Mathf.Max(result.maximumSettledJointSpeed,Mathf.Abs(l.body.jointVelocity[axis]));
                        }
                        if(step%10==0)trace.WriteLine(JsonUtility.ToJson(new Trace{seconds=(step+1)*WorldController.StepSeconds,
                            preStepErrorX=error.x,preStepErrorZ=error.z,preStepVelocityX=velocity.x,preStepVelocityZ=velocity.z,centerOfMass=com,supportCenter=supportCenter,
                            diagnosticHipSagittalOffsetRadians=(float)body.muscles.DiagnosticHipSagittalOffsetRadians,
                            diagnosticHipCoronalOffsetRadians=result.hipCoronalOffsetRadians*postureBlend,command=command,body=frame}));
                        if(step==steps-1)File.WriteAllText(Path.Combine(world.outputDirectory,name+"-body-frame.json"),JsonUtility.ToJson(frame,true));
                        if(!result.finite)throw new InvalidOperationException("Non-finite balance fixture.");
                    }
                    result.settlingPassed=result.maximumSettledLinearSpeed<.15f&&result.maximumSettledJointSpeed<.75f;
                    if(result.standingEffortSamples>0)result.meanSquaredActivationWhileStanding/=result.standingEffortSamples;
                    result.standingHeld=result.metrics.currentStandingSeconds>=report.minimumFinalStandingSeconds&&
                        result.metrics.maximumLimitViolation<.05&&result.settlingPassed&&result.finite;
                }
                catch(Exception error){report.error=error.ToString();report.finite=false;}
                Debug.Log("BALANCE CAPABILITY: "+JsonUtility.ToJson(result));
                yield return null;
                if(!string.IsNullOrEmpty(report.error))break;
            }
            report.isolated=!world.Connected&&world.bodyFramesAccepted==0&&world.retinalFramesAccepted==0&&world.motorPacketsReceived==0;
            report.completed=string.IsNullOrEmpty(report.error)&&report.cases.Count==configurations.Count;
            report.anyDiagnosticStandingHold=report.cases.Any(c=>c.standingHeld);
            File.WriteAllText(Path.Combine(world.outputDirectory,"balance-report.json"),JsonUtility.ToJson(report,true));
            world.WriteReport();Application.Quit(report.completed&&report.finite&&report.isolated?0:1);
        }

        static Case Configuration(float sagittal,float coronal)=>new Case{
            name="hips-s"+sagittal.ToString("+0.00;-0.00;0.00",System.Globalization.CultureInfo.InvariantCulture)+
                "-c"+coronal.ToString("+0.00;-0.00;0.00",System.Globalization.CultureInfo.InvariantCulture),
            fatiguePolicy=MuscleFatiguePolicy.Normal,hipSagittalOffsetRadians=sagittal,hipCoronalOffsetRadians=coronal,
            recruitment="full-position",feedback=true,positionGain=12,velocityGain=2.4f,coronalGainRatio=4,
            coronalDerivativeGain=2.4f,coronalHipShare=0,coronalAnkleTargetsAbsolute=false};
    }
}
