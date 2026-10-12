using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    public static class MuscleQualification
    {
        [Serializable] public sealed class Case {
            public bool musclesEnabled, preserveActiveCapacity, finite=true, standingHeld;
            public float fixedStandDrive; public int reportedMuscles;
            public string worstLimitJoint; public int worstLimitAxis;
            public double worstLimitTime;public float maximumLimitViolation, peakActivation, peakForceNewtons, peakFatigue;
            public float maximumSettledLinearSpeed,maximumSettledJointSpeed;public bool settlingPassed;
            public StandingTrialMetrics metrics=new StandingTrialMetrics();
        }
        [Serializable] public sealed class Report {
            public string unity, error="";public bool completed,finite=true,isolated,walkingAssessed=false;
            public List<Case> cases=new List<Case>();
        }

        public static IEnumerator Run(WorldController world)
        {
            yield return null;
            var report=new Report{unity=Application.unityVersion};
            foreach(var result in new[]{new Case{musclesEnabled=false,fixedStandDrive=0},new Case{musclesEnabled=false,fixedStandDrive=1},
                new Case{musclesEnabled=true,fixedStandDrive=0},new Case{musclesEnabled=true,fixedStandDrive=1},
                new Case{musclesEnabled=true,preserveActiveCapacity=true,fixedStandDrive=0},
                new Case{musclesEnabled=true,preserveActiveCapacity=true,fixedStandDrive=1}})
            {
                bool muscles=result.musclesEnabled;float drive=result.fixedStandDrive;report.cases.Add(result);
                try {
                    if(world.Connected)throw new InvalidOperationException("Muscle comparison must be isolated from DNNE.");
                    world.DiagnosticMode=true;world.ResetDiagnosticBody(muscles,result.preserveActiveCapacity);
                    world.DiagnosticCommand=new MotorFrame{standDrive=drive};
                    string name=(muscles?(result.preserveActiveCapacity?"full-active-muscles":"muscles"):"torques")+"-stand-"+drive;
                    using(var trace=new StreamWriter(Path.Combine(world.outputDirectory,name+"-trace.jsonl")))
                    for(int step=0;step<1000;step++)
                    {
                        world.Step();var body=world.Avatar;var frame=BodyFrame.Capture(body,step+1);
                        var state=frame.articulation.musculoskeletal;
                        result.finite&=body.IsFinite();
                        if(step>=800)foreach(var link in body.links.Values){
                            result.maximumSettledLinearSpeed=Mathf.Max(result.maximumSettledLinearSpeed,link.body.linearVelocity.magnitude);
                            for(int axis=0;axis<link.body.dofCount;axis++)result.maximumSettledJointSpeed=Mathf.Max(result.maximumSettledJointSpeed,Mathf.Abs(link.body.jointVelocity[axis]));
                        }
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
                        result.reportedMuscles=state.muscles.Length;
                        foreach(var m in state.muscles){result.peakActivation=Mathf.Max(result.peakActivation,m.activation);result.peakForceNewtons=Mathf.Max(result.peakForceNewtons,m.forceNewtons);result.peakFatigue=Mathf.Max(result.peakFatigue,m.fatigueFraction);}
                        if(step%10==0)trace.WriteLine(JsonUtility.ToJson(new Trace{seconds=(step+1)*WorldController.StepSeconds,body=frame}));
                        if(step==999)File.WriteAllText(Path.Combine(world.outputDirectory,name+"-body-frame.json"),JsonUtility.ToJson(frame,true));
                        if(!result.finite)throw new InvalidOperationException("Non-finite muscle fixture.");
                    }
                    result.standingHeld=result.metrics.currentStandingSeconds>=5&&result.maximumLimitViolation<.05;
                    result.settlingPassed=result.maximumSettledLinearSpeed<.15f&&result.maximumSettledJointSpeed<.75f;
                }catch(Exception error){report.error=error.ToString();report.finite=false;}
                Debug.Log("MUSCLE COMPARISON: "+JsonUtility.ToJson(result));
                yield return null;
                if(!string.IsNullOrEmpty(report.error))break;
            }
            report.isolated=!world.Connected&&world.bodyFramesAccepted==0&&world.retinalFramesAccepted==0;
            report.completed=string.IsNullOrEmpty(report.error)&&report.cases.Count==6;
            File.WriteAllText(Path.Combine(world.outputDirectory,"muscle-report.json"),JsonUtility.ToJson(report,true));
            world.WriteReport();Application.Quit(report.completed&&report.finite&&report.isolated?0:1);
        }
        [Serializable] public sealed class Trace { public double seconds;public BodyFrame body; }
    }
}
