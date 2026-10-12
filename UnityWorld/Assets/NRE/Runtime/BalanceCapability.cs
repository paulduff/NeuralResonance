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
            public bool feedback,finite=true,standingHeld,settlingPassed,hipTargetsAbsolute,retainFullAnkleCorrection;
            public float positionGain,velocityGain,coronalHipShare,coronalGainRatio,maximumSettledLinearSpeed,maximumSettledJointSpeed;
            public string worstLimitJoint;public int worstLimitAxis;public double worstLimitTime;
            public float maximumLimitViolation;
            public int disturbancesApplied;
            public StandingTrialMetrics metrics=new StandingTrialMetrics();
        }
        [Serializable] public sealed class Report
        {
            public string unity,error="";
            public int protocolVersion=3;public float durationSeconds=30;
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
            public BodyFrame body;
        }

        public static IEnumerator Run(WorldController world)
        {
            yield return null;
            var report=new Report{unity=Application.unityVersion};
            var configurations=new[]{
                Configuration("ankle-only",0),
                Configuration("relative-hips-25-split-ankles",.25f),
                Configuration("relative-hips-25-full-ankles",.25f,retainAnkles:true),
                Configuration("absolute-hips-25-split-ankles",.25f,absolute:true),
                Configuration("absolute-hips-25-full-ankles",.25f,absolute:true,retainAnkles:true),
                Configuration("absolute-hips-10-full-ankles",.1f,absolute:true,retainAnkles:true),
                Configuration("absolute-hips-50-full-ankles",.5f,absolute:true,retainAnkles:true)};
            foreach(var result in configurations)
            {
                string policy=result.recruitment;var gains=new Vector2(result.positionGain,result.velocityGain);
                report.cases.Add(result);
                string name=result.name;
                try
                {
                    if(world.Connected||!world.IsolatedQualification)throw new InvalidOperationException("Balance fixture must be isolated.");
                    world.DiagnosticMode=true;world.ResetDiagnosticBody(true,policy!="canonical",policy!="full-position");
                    using(var trace=new StreamWriter(Path.Combine(world.outputDirectory,name+"-trace.jsonl")))
                    for(int step=0;step<3000;step++)
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
                            result.hipTargetsAbsolute,result.retainFullAnkleCorrection):new MotorFrame{standDrive=1};
                        world.DiagnosticCommand=command;
                        // Prescribed external pushes test recovery; they never provide balancing assistance.
                        if(step==800||step==1400||step==2200){body.pelvis.AddForce((step==800?Vector3.forward:step==1400?Vector3.right:Vector3.left)*1.5f,ForceMode.Impulse);result.disturbancesApplied++;}
                        world.Step();frame=BodyFrame.Capture(body,step+1);state=frame.articulation.musculoskeletal;
                        result.finite&=body.IsFinite()&&command.IsFinite()&&state.muscles.All(m=>
                            float.IsFinite(m.activation)&&float.IsFinite(m.forceNewtons)&&float.IsFinite(m.lengthFraction)&&float.IsFinite(m.velocityPerSecond)&&float.IsFinite(m.fatigueFraction));
                        foreach(var link in body.links.Values)for(int axis=0;axis<link.dofs;axis++){
                            var range=ArticulatedAvatar.JointDrive(link,axis);float angle=body.Angle(link.name,axis);
                            float violation=Mathf.Max(0,range.lowerLimit*Mathf.Deg2Rad-angle,angle-range.upperLimit*Mathf.Deg2Rad);
                            if(violation>result.maximumLimitViolation){result.maximumLimitViolation=violation;result.worstLimitJoint=link.name;result.worstLimitAxis=axis;result.worstLimitTime=(step+1)*WorldController.StepSeconds;}
                        }
                        result.metrics.Observe(WorldController.StepSeconds,new StandingObservation{
                            finite=result.finite,uprightFraction=state.uprightFraction,pelvisHeight=body.pelvis.transform.position.y,
                            initialPelvisHeight=body.initialPelvisHeight,leftFootLoad=body.LeftLoad,rightFootLoad=body.RightLoad,
                            bodyWeight=body.totalMass*9.81,otherSupportLoad=body.links.Values.Where(l=>!l.name.EndsWith("foot")&&l.sensor.supportPoints.Count>0).Sum(l=>l.sensor.force),
                            supportMargin=state.balance.supportMarginMeters,limitViolation=ActuatorQualification.LimitViolation(body)});
                        if(step>=2800)foreach(var l in body.links.Values){
                            result.maximumSettledLinearSpeed=Mathf.Max(result.maximumSettledLinearSpeed,l.body.linearVelocity.magnitude);
                            for(int axis=0;axis<l.body.dofCount;axis++)result.maximumSettledJointSpeed=Mathf.Max(result.maximumSettledJointSpeed,Mathf.Abs(l.body.jointVelocity[axis]));
                        }
                        if(step%10==0)trace.WriteLine(JsonUtility.ToJson(new Trace{seconds=(step+1)*WorldController.StepSeconds,
                            preStepErrorX=error.x,preStepErrorZ=error.z,preStepVelocityX=velocity.x,preStepVelocityZ=velocity.z,centerOfMass=com,supportCenter=supportCenter,command=command,body=frame}));
                        if(step==2999)File.WriteAllText(Path.Combine(world.outputDirectory,name+"-body-frame.json"),JsonUtility.ToJson(frame,true));
                        if(!result.finite)throw new InvalidOperationException("Non-finite balance fixture.");
                    }
                    result.standingHeld=result.metrics.currentStandingSeconds>=5&&result.metrics.maximumLimitViolation<.05;
                    result.settlingPassed=result.maximumSettledLinearSpeed<.15f&&result.maximumSettledJointSpeed<.75f;
                }
                catch(Exception error){report.error=error.ToString();report.finite=false;}
                Debug.Log("BALANCE CAPABILITY: "+JsonUtility.ToJson(result));
                yield return null;
                if(!string.IsNullOrEmpty(report.error))break;
            }
            report.isolated=!world.Connected&&world.bodyFramesAccepted==0&&world.retinalFramesAccepted==0&&world.motorPacketsReceived==0;
            report.completed=string.IsNullOrEmpty(report.error)&&report.cases.Count==configurations.Length;
            report.anyDiagnosticStandingHold=report.cases.Any(c=>c.standingHeld);
            File.WriteAllText(Path.Combine(world.outputDirectory,"balance-report.json"),JsonUtility.ToJson(report,true));
            world.WriteReport();Application.Quit(report.completed&&report.finite&&report.isolated?0:1);
        }

        static Case Configuration(string name,float share,bool absolute=false,bool retainAnkles=false)=>new Case{
            name=name,recruitment="full-position",feedback=true,positionGain=12,velocityGain=2.4f,coronalGainRatio=1,
            coronalHipShare=share,hipTargetsAbsolute=absolute,retainFullAnkleCorrection=retainAnkles};
    }
}
