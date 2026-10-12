using System;
using System.Collections;
using System.Collections.Generic;
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
   report.storedEnergyJoules=body.physiology.State.StoredEnergyJoules;report.hydrationFraction=body.physiology.State.HydrationFraction;report.tissueIntegrityFraction=body.physiology.State.TissueIntegrityFraction;report.contactDamageFraction=body.physiology.ContactDamage;
   File.WriteAllText(Path.Combine(world.outputDirectory,"body-frame-example.json"),JsonUtility.ToJson(BodyFrame.Capture(body,1),true));
   File.WriteAllText(Path.Combine(world.outputDirectory,"contact-frame-example.json"),JsonUtility.ToJson(ContactFrame.Capture(body,body.links["left_foot"],1),true));
   // The actuator fixture is anchored and airborne. It is a calibration test,
   // never the production standing mechanism or a gait controller.
   body.pelvis.immovable=true;body.pelvis.TeleportRoot(new Vector3(0,2.5f,0),Quaternion.identity);
   foreach(var link in body.links.Values){link.body.useGravity=false;
    body.SetCalibrationPose(link,0);
   }
   world.DiagnosticCommand=new MotorFrame{leftElbowDrive=.8f};
   for(int i=0;i<120;i++)world.Step();
   report.elbowResponseRadians=body.Angle("left_forearm");
   report.elbowWithinLimits=report.elbowResponseRadians>=-.02f&&report.elbowResponseRadians<=150*Mathf.Deg2Rad+.05f;
   report.authorityGatePassed=!new MotorFrame{active=true,confidence=.4f,minimumOutputConfidence=.45f}.IsAuthorized()&&new MotorFrame{active=true,confidence=.8f,minimumOutputConfidence=.45f}.IsAuthorized()&&!new MotorFrame{active=true,confidence=float.NaN,minimumOutputConfidence=.45f}.IsAuthorized();
   report.isolated=world.bodyFramesAccepted==0&&world.retinalFramesAccepted==0&&!world.Connected;
   report.settlingPassed=report.maximumSettledLinearSpeed<.15f&&report.maximumSettledJointSpeed<.75f;
   // Run after passive measurements, with the avatar already anchored and
   // gravity disabled, so this separate rig cannot advance the fall fixture.
   report.nativeCoordinates=NativeCoordinateProbe.Run();
   report.nativeCoordinatesPassed=report.nativeCoordinates.passed;
   File.WriteAllText(Path.Combine(world.outputDirectory,"native-coordinate-probe.json"),JsonUtility.ToJson(report.nativeCoordinates,true));
   if(!report.nativeCoordinatesPassed)throw new InvalidOperationException("Native coordinate boundary probe failed.");
  }catch(Exception error){report.error=error.ToString();report.completed=false;report.qualified=false;}
  // Enumerators cannot yield inside try/catch. Advance the isolated fixture
  // explicitly so failures still produce a report and a non-zero player exit.
  if(string.IsNullOrEmpty(report.error)){
   var calibration=ActuatorQualification.Run(world,report.actuators);
   bool more=true;while(more){
    try{more=calibration.MoveNext();}catch(Exception error){report.error=error.ToString();more=false;}
    if(more)yield return calibration.Current;
   }
  }
  report.actuatorsPassed=report.actuators.Count==19&&report.actuators.All(a=>a.passed);
  report.completed=string.IsNullOrEmpty(report.error);
  report.qualified=report.completed&&report.nativeCoordinatesPassed&&report.finite&&report.isolated&&report.authorityGatePassed&&report.gravityDisplacementMeters>.01f&&report.floorContactPeakNewtons>50&&report.minimumColliderHeight>-.03f&&report.elbowResponseRadians>.15f&&report.elbowWithinLimits&&report.settlingPassed&&report.actuatorsPassed;
  File.WriteAllText(Path.Combine(world.outputDirectory,"qualification.json"),JsonUtility.ToJson(report,true));
  world.WriteReport();Debug.Log("Unity mechanical qualification: "+JsonUtility.ToJson(report));
  Application.Quit(report.qualified?0:1);
 }
 [Serializable] public sealed class Report {
  public string unity,error;public bool completed,qualified,nativeCoordinatesPassed,finite,isolated,authorityGatePassed,elbowWithinLimits,settlingPassed,actuatorsPassed,walkingAssessed=false;
  public NativeCoordinateProbe.Report nativeCoordinates;
  public double storedEnergyJoules,hydrationFraction,tissueIntegrityFraction,contactDamageFraction;
  public List<ActuatorQualification.AxisResult> actuators=new List<ActuatorQualification.AxisResult>();
  public int links;public float massKg,stepSeconds,gravityDisplacementMeters,floorContactPeakNewtons,minimumColliderHeight,elbowResponseRadians,maximumSettledLinearSpeed,maximumSettledJointSpeed;
 }
}
}
