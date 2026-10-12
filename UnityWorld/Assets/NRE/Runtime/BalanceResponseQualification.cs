using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Measures a small command's effect in the free, supported body. Individual
    // anchored-axis qualification does not establish a closed-chain COM response.
    public static class BalanceResponseQualification
    {
        [Serializable] public sealed class Case
        {
            public string name;
            public float leftHipOffset,rightHipOffset,leftAnkleOffset,rightAnkleOffset;
            public bool finite=true;
            public float maximumLimitViolation;
            public StandingTrialMetrics metrics=new StandingTrialMetrics();
        }
        [Serializable] public sealed class Report
        {
            public string unity,error="";
            public int protocolVersion=1;
            public float durationSeconds=4,pulseStartSeconds=1,pulseEndSeconds=1.5f,pulseRadians=.03f;
            public bool completed,finite=true,isolated;
            public bool neuronalControlAssessed=false,learnedStandingAssessed=false,walkingAssessed=false;
            public List<Case> cases=new List<Case>();
        }
        [Serializable] public sealed class Trace
        {
            public double seconds;
            public bool pulseApplied;
            public Vector3 centerOfMass,supportCenter,centerOfMassVelocity;
            public float preStepErrorX,preStepErrorZ;
            // PhysX reduced-space actuator forces after torque clamping, N m.
            // Passive constraint/drive forces are not claimed as included here.
            public float leftHipTorque,rightHipTorque,leftAnkleTorque,rightAnkleTorque;
            public MotorFrame command;
            public BodyFrame body;
        }

        public static IEnumerator Run(WorldController world)
        {
            yield return null;
            var report=new Report{unity=Application.unityVersion};
            report.cases.Add(new Case{name="control-before"});
            foreach(float sign in new[]{1f,-1f}){
                string suffix=sign>0?"positive":"negative";float pulse=sign*report.pulseRadians;
                report.cases.Add(new Case{name="hips-"+suffix,leftHipOffset=pulse,rightHipOffset=-pulse});
                report.cases.Add(new Case{name="ankles-"+suffix,leftAnkleOffset=pulse,rightAnkleOffset=-pulse});
                report.cases.Add(new Case{name="left-hip-"+suffix,leftHipOffset=pulse});
                report.cases.Add(new Case{name="right-hip-"+suffix,rightHipOffset=pulse});
            }
            report.cases.Add(new Case{name="control-after"});
            foreach(var result in report.cases)
            {
                try
                {
                    if(world.Connected||!world.IsolatedQualification)throw new InvalidOperationException("Response fixture must be isolated.");
                    world.DiagnosticMode=true;world.ResetDiagnosticBody(true,true,false);
                    using(var trace=new StreamWriter(Path.Combine(world.outputDirectory,result.name+"-trace.jsonl")))
                    for(int step=0;step<400;step++)
                    {
                        var avatar=world.Avatar;var before=BodyFrame.Capture(avatar,step).articulation.musculoskeletal;
                        var forward=Vector3.ProjectOnPlane(avatar.pelvis.transform.forward,Vector3.up);
                        var heading=Quaternion.Inverse(forward.sqrMagnitude>.0001f?Quaternion.LookRotation(forward.normalized):Quaternion.identity);
                        var com=avatar.CenterOfMass();
                        var supportCenter=(avatar.links["left_foot"].body.GetComponentInChildren<BoxCollider>().bounds.center+
                            avatar.links["right_foot"].body.GetComponentInChildren<BoxCollider>().bounds.center)*.5f;
                        var error=heading*(com-supportCenter);
                        var velocity=Vector3.zero;foreach(var link in avatar.links.Values)velocity+=link.body.linearVelocity*link.body.mass;
                        velocity=heading*(velocity/avatar.totalMass);
                        bool supported=before.uprightFraction>.7f&&avatar.pelvis.transform.position.y>.6f*avatar.initialPelvisHeight&&
                            avatar.LeftLoad+avatar.RightLoad>.5f*avatar.totalMass*9.81f;
                        var command=BalanceDiagnosticController.Command(12,2.4,error.x,error.z,velocity.x,velocity.z,
                            avatar.Angle("left_foot"),avatar.Angle("right_foot"),avatar.Angle("left_foot",1),avatar.Angle("right_foot",1),supported);
                        bool pulseApplied=step>=100&&step<150;
                        if(pulseApplied)BalanceDiagnosticController.AddCoronalOffsets(command,result.leftHipOffset,result.rightHipOffset,result.leftAnkleOffset,result.rightAnkleOffset);
                        world.DiagnosticCommand=command;world.Step();
                        var frame=BodyFrame.Capture(avatar,step+1);var state=frame.articulation.musculoskeletal;
                        result.finite&=avatar.IsFinite()&&command.IsFinite()&&state.muscles.All(m=>
                            float.IsFinite(m.activation)&&float.IsFinite(m.forceNewtons)&&float.IsFinite(m.fatigueFraction));
                        result.maximumLimitViolation=Mathf.Max(result.maximumLimitViolation,ActuatorQualification.LimitViolation(avatar));
                        result.metrics.Observe(WorldController.StepSeconds,new StandingObservation{
                            finite=result.finite,uprightFraction=state.uprightFraction,pelvisHeight=avatar.pelvis.transform.position.y,
                            initialPelvisHeight=avatar.initialPelvisHeight,leftFootLoad=avatar.LeftLoad,rightFootLoad=avatar.RightLoad,
                            bodyWeight=avatar.totalMass*9.81,otherSupportLoad=avatar.links.Values.Where(l=>!l.name.EndsWith("foot")&&l.sensor.supportPoints.Count>0).Sum(l=>l.sensor.force),
                            supportMargin=state.balance.supportMarginMeters,limitViolation=ActuatorQualification.LimitViolation(avatar)});
                        if(step%2==0)trace.WriteLine(JsonUtility.ToJson(new Trace{seconds=(step+1)*WorldController.StepSeconds,
                            pulseApplied=pulseApplied,centerOfMass=com,supportCenter=supportCenter,centerOfMassVelocity=velocity,
                            preStepErrorX=error.x,preStepErrorZ=error.z,command=command,body=frame,
                            leftHipTorque=Torque(avatar,"left_thigh"),rightHipTorque=Torque(avatar,"right_thigh"),
                            leftAnkleTorque=Torque(avatar,"left_foot"),rightAnkleTorque=Torque(avatar,"right_foot")}));
                        if(step==399)File.WriteAllText(Path.Combine(world.outputDirectory,result.name+"-body-frame.json"),JsonUtility.ToJson(frame,true));
                        if(!result.finite)throw new InvalidOperationException("Non-finite response fixture.");
                    }
                }
                catch(Exception error){report.error=error.ToString();report.finite=false;}
                Debug.Log("BALANCE RESPONSE: "+JsonUtility.ToJson(result));
                yield return null;
                if(!string.IsNullOrEmpty(report.error))break;
            }
            report.isolated=!world.Connected&&world.bodyFramesAccepted==0&&world.retinalFramesAccepted==0&&world.motorPacketsReceived==0;
            report.completed=string.IsNullOrEmpty(report.error);
            File.WriteAllText(Path.Combine(world.outputDirectory,"response-report.json"),JsonUtility.ToJson(report,true));
            world.WriteReport();Application.Quit(report.completed&&report.finite&&report.isolated?0:1);
        }

        static float Torque(ArticulatedAvatar avatar,string joint)
        {
            var link=avatar.links[joint];return link.body.jointForce[link.coordinates.NativeIndex(1)];
        }
    }
}
