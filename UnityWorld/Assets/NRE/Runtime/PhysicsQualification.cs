using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Nre.UnityWorld {
public static class PhysicsQualification {
 public static IEnumerator Run(WorldController world) {
  yield return null;
  var report=new Report{unity=Application.unityVersion,stepSeconds=WorldController.StepSeconds};
  try {
   if(world.Connected)throw new InvalidOperationException("Qualification must be isolated from DNNE.");
   world.DiagnosticMode=true;world.DiagnosticCommand=new MotorFrame();
   var body=world.Avatar;report.massKg=body.totalMass;report.links=body.links.Count;
   float start=body.pelvis.transform.position.y;float peakContact=0;
   for(int i=0;i<1000;i++){
    world.Step();peakContact=Mathf.Max(peakContact,body.links.Values.Sum(l=>l.sensor.force));
    if(i>=800)foreach(var link in body.links.Values){
     report.maximumSettledLinearSpeed=Mathf.Max(report.maximumSettledLinearSpeed,link.body.linearVelocity.magnitude);
     for(int d=0;d<link.body.dofCount;d++)report.maximumSettledJointSpeed=Mathf.Max(report.maximumSettledJointSpeed,Mathf.Abs(link.body.jointVelocity[d]));
    }
   }
   report.gravityDisplacementMeters=start-body.pelvis.transform.position.y;
   report.floorContactPeakNewtons=peakContact;
   report.minimumColliderHeight=body.links.Values.SelectMany(l=>l.body.GetComponentsInChildren<Collider>()).Distinct().Min(c=>c.bounds.min.y);
   report.finite=body.IsFinite();
   File.WriteAllText(Path.Combine(world.outputDirectory,"body-frame-example.json"),JsonUtility.ToJson(BodyFrame.Capture(body,1),true));
   File.WriteAllText(Path.Combine(world.outputDirectory,"contact-frame-example.json"),JsonUtility.ToJson(ContactFrame.Capture(body,body.links["left_foot"],1),true));
   // The actuator fixture is anchored and airborne. It is a calibration test,
   // never the production standing mechanism or a gait controller.
   body.pelvis.immovable=true;body.pelvis.TeleportRoot(new Vector3(0,2.5f,0),Quaternion.identity);
   foreach(var link in body.links.Values){link.body.useGravity=false;int dofs=link.body.dofCount;
    if(dofs==1){link.body.jointPosition=new ArticulationReducedSpace(0);link.body.jointVelocity=new ArticulationReducedSpace(0);}
    if(dofs==2){link.body.jointPosition=new ArticulationReducedSpace(0,0);link.body.jointVelocity=new ArticulationReducedSpace(0,0);}
   }
   world.DiagnosticCommand=new MotorFrame{leftElbowDrive=.8f};
   for(int i=0;i<120;i++)world.Step();
   report.elbowResponseRadians=body.Angle("left_forearm");
   report.elbowWithinLimits=report.elbowResponseRadians>=-.02f&&report.elbowResponseRadians<=150*Mathf.Deg2Rad+.05f;
   report.authorityGatePassed=!new MotorFrame{active=true,confidence=.4f,minimumOutputConfidence=.45f}.IsAuthorized()&&new MotorFrame{active=true,confidence=.8f,minimumOutputConfidence=.45f}.IsAuthorized()&&!new MotorFrame{active=true,confidence=float.NaN,minimumOutputConfidence=.45f}.IsAuthorized();
   report.isolated=world.bodyFramesAccepted==0&&world.retinalFramesAccepted==0&&!world.Connected;
   report.settlingPassed=report.maximumSettledLinearSpeed<.15f&&report.maximumSettledJointSpeed<.75f;
   report.qualified=report.finite&&report.isolated&&report.authorityGatePassed&&report.gravityDisplacementMeters>.01f&&report.floorContactPeakNewtons>50&&report.minimumColliderHeight>-.03f&&report.elbowResponseRadians>.15f&&report.elbowWithinLimits&&report.settlingPassed;
   report.completed=true;
  }catch(Exception error){report.error=error.ToString();report.completed=false;report.qualified=false;}
  File.WriteAllText(Path.Combine(world.outputDirectory,"qualification.json"),JsonUtility.ToJson(report,true));
  world.WriteReport();Debug.Log("Unity mechanical qualification: "+JsonUtility.ToJson(report));
  Application.Quit(report.qualified?0:1);
 }
 [Serializable] public sealed class Report {
  public string unity,error;public bool completed,qualified,finite,isolated,authorityGatePassed,elbowWithinLimits,settlingPassed,walkingAssessed=false;
  public int links;public float massKg,stepSeconds,gravityDisplacementMeters,floorContactPeakNewtons,minimumColliderHeight,elbowResponseRadians,maximumSettledLinearSpeed,maximumSettledJointSpeed;
 }
}
}
