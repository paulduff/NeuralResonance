using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld {
// Only attached by MusclePathBuild in its separate qualification scene/player.
// Free articulation roots isolate point-force application from joint constraints.
public sealed class MusclePathNativeQualification : MonoBehaviour {
 const double PositionBound=2e-6,VelocityBound=2e-6,ForceBound=3e-6,PowerBound=2e-5;
 [Serializable] public sealed class BodyState {
  public string id; public Vector3 position,center,velocity,angularVelocity,inertia; public Quaternion rotation;
  public float mass; public Vector3 localAnchor,point,pointVelocity,appliedForce;
 }
 [Serializable] public sealed class Case {
  public string key,error,pathFingerprint;public bool bent,rotated,completed,passed;
  public double stepSeconds,tensionNewtons,lengthMeters,pathVelocityMetersPerSecond,powerWatts,powerResidualWatts;
  public double maximumPositionError,maximumPointVelocityError,maximumForceRoundingError,maximumLinearVelocityError,maximumAngularVelocityError,maximumClearedForceVelocityError;
  public double nativePowerError;public long startStep,endStep;public BodyState[] before,afterForce,afterClear;
 }
 [Serializable] public sealed class Report {
  public string unity,sessionId,error;public bool completed,passed,isolated=true,neuronalModelExecuted=false,avatarLoaded=false,anatomicalCalibrationQualified=false;
  public double positionBound=PositionBound,velocityBound=VelocityBound,forceRoundingBound=ForceBound,powerBound=PowerBound;
  public long actualPhysicsSteps; public Case[] cases;
 }
 string directory;long steps;Report report;readonly List<Case> cases=new List<Case>();
 static MusclePathVector D(Vector3 v)=>new MusclePathVector(v.x,v.y,v.z);
 static Vector3 F(MusclePathVector v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
 static MusclePathVector Cross(MusclePathVector a,MusclePathVector b)=>new MusclePathVector(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
 static MusclePathVector Rotate(Quaternion q,MusclePathVector p){var v=new MusclePathVector(q.x,q.y,q.z);return p+Cross(v,Cross(v,p)+p*q.w)*2;}
 static double Error(MusclePathVector a,MusclePathVector b)=>(a-b).Magnitude;
 static bool Finite(BodyState b)=>D(b.position).IsFinite&&D(b.center).IsFinite&&D(b.velocity).IsFinite&&D(b.angularVelocity).IsFinite&&
  D(b.inertia).IsFinite&&D(b.point).IsFinite&&D(b.pointVelocity).IsFinite&&MusclePathVector.Finite(b.rotation.x)&&MusclePathVector.Finite(b.rotation.y)&&
  MusclePathVector.Finite(b.rotation.z)&&MusclePathVector.Finite(b.rotation.w)&&MusclePathVector.Finite(b.mass)&&b.mass>0;
 static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
 static string Argument(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
 static void WriteNew(string path,object value){using(var s=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(s)){w.Write(JsonUtility.ToJson(value,true));}}
 IEnumerator Start(){
  report=new Report{unity=Application.unityVersion,sessionId=Argument("--path-session")};
  bool setup=false;
  try {
   directory=Argument("--path-output");
   Require(Guid.TryParseExact(report.sessionId,"N",out _)&&!string.IsNullOrWhiteSpace(directory)&&!Directory.Exists(directory),"Fresh explicit native qualification session/output required.");
   Require(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).All(b=>b==this)&&FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None).Length==0,"Qualification scene must contain only this physics owner and no existing body.");
   Directory.CreateDirectory(directory);Physics.simulationMode=SimulationMode.Script;Physics.gravity=Vector3.zero;setup=true;
  }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
  if(!setup){Application.Quit(1);yield break;}
  foreach(bool bent in new[]{false,true})foreach(bool rotated in new[]{false,true})foreach(float step in new[]{.01f,.001f,.000125f})foreach(double tension in new[]{0.0,10.0}){
   var row=new Case{key="case-"+cases.Count.ToString("D2",CultureInfo.InvariantCulture),bent=bent,rotated=rotated,stepSeconds=(double)step,tensionNewtons=tension,startStep=steps};
   cases.Add(row);var bodies=new List<ArticulationBody>();
   try {
    int count=bent?3:2;
    for(int i=0;i<count;++i){
     var go=new GameObject("path-body-"+i);go.transform.position=new Vector3(i*.8f,bent&&i==1?.6f:0,.1f*i);
     go.transform.rotation=rotated?Quaternion.Euler(23,-37,51):Quaternion.identity;
     var collider=go.AddComponent<SphereCollider>();collider.radius=.05f;
     var body=go.AddComponent<ArticulationBody>();bodies.Add(body);
     body.mass=2+i;body.useGravity=false;body.immovable=false;body.linearDamping=0;body.angularDamping=0;
     body.sleepThreshold=0;body.centerOfMass=Vector3.zero;body.inertiaTensor=Vector3.one*(.15f*(i+1));body.inertiaTensorRotation=Quaternion.identity;
     body.solverIterations=24;body.solverVelocityIterations=12;
    }
    Physics.SyncTransforms();
   }catch(Exception e){row.error=e.ToString();}
   yield return null; // Component registration only: scripted physics does not advance.
   try {if(row.error==null)RunCase(row,bodies.ToArray(),step);}
   catch(Exception e){row.error=e.ToString();Debug.LogException(e);}
   finally {
    row.endStep=steps;WriteNew(Path.Combine(directory,row.key+".json"),row);
    foreach(var body in bodies)if(body!=null)Destroy(body.gameObject);
   }
   yield return null; // Complete destruction before creating the next isolated fixture.
  }
  report.cases=cases.ToArray();report.actualPhysicsSteps=steps;report.completed=cases.Count==24&&cases.All(c=>c.completed);
  report.passed=report.completed&&steps==48&&cases.All(c=>c.passed);
  WriteNew(Path.Combine(directory,"results.json"),report);Application.Quit(report.passed?0:1);
 }
 BodyState[] Capture(ArticulationBody[] bodies,Vector3[] anchors){
  return bodies.Select((body,i)=>{
   var point=body.transform.TransformPoint(anchors[i]);
   return new BodyState{id=body.name,position=body.transform.position,rotation=body.transform.rotation,center=body.worldCenterOfMass,
    mass=body.mass,inertia=body.inertiaTensor,velocity=body.linearVelocity,angularVelocity=body.angularVelocity,
    localAnchor=anchors[i],point=point,pointVelocity=body.GetPointVelocity(point)};
  }).ToArray();
 }
 void Step(float dt){Require(Physics.simulationMode==SimulationMode.Script,"Another physics owner changed simulation mode.");Physics.Simulate(dt);steps++;}
 void RunCase(Case row,ArticulationBody[] bodies,float dt){
  Require(FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None).Length==bodies.Length,"Unexpected physical body in fixture scene.");
  var anchors=bodies.Select((b,i)=>new Vector3(.04f,-.025f*(i+1),.03f)).ToArray();
  for(int i=0;i<bodies.Length;++i){
   var b=bodies[i];b.linearVelocity=new Vector3(.1f*(i+1),-.2f,.05f);b.angularVelocity=new Vector3(.2f,-.1f,.05f*(i+1));b.WakeUp();
   Require(b.isRoot&&b.mass==2+i&&b.inertiaTensor==Vector3.one*(.15f*(i+1))&&b.centerOfMass==Vector3.zero,"Declared free-body mass/inertia changed.");
  }
  row.before=Capture(bodies,anchors);
  var path=new AvatarMusclePath("native-fixture",row.key,"Synthetic free articulation roots; no human anatomy",
   row.before.Select((b,i)=>new MusclePathAnchor("anchor-"+i,b.id,D(b.localAnchor))));
  var points=row.before.Select((b,i)=>new MusclePathPoint("anchor-"+i,b.id,D(b.point),D(b.pointVelocity))).ToArray();
  var geometry=path.Measure(row.key,points);var forces=geometry.ApplyTension(row.tensionNewtons);
  row.pathFingerprint=path.Fingerprint;row.lengthMeters=geometry.MusculotendonLengthMeters;row.pathVelocityMetersPerSecond=geometry.MusculotendonVelocityMetersPerSecond;
  row.powerWatts=forces.PowerOnBodiesWatts;row.powerResidualWatts=forces.PowerResidualWatts;
  double nativePower=0;
  for(int i=0;i<bodies.Length;++i){
   var b=row.before[i];var theoreticalPoint=D(b.position)+Rotate(b.rotation,D(b.localAnchor));
   row.maximumPositionError=Math.Max(row.maximumPositionError,Error(theoreticalPoint,D(b.point)));
   var pointVelocity=D(b.velocity)+Cross(D(b.angularVelocity),D(b.point)-D(b.center));
   row.maximumPointVelocityError=Math.Max(row.maximumPointVelocityError,Error(pointVelocity,D(b.pointVelocity)));
   var force=forces.PointForces[i].ForceNewtons;b.appliedForce=F(force);
   row.maximumForceRoundingError=Math.Max(row.maximumForceRoundingError,Error(force,D(b.appliedForce)));
   nativePower+=MusclePathVector.Dot(D(b.appliedForce),pointVelocity);
  }
  row.nativePowerError=Math.Abs(nativePower-row.powerWatts);
  WriteNew(Path.Combine(directory,row.key+"-initial.json"),row);
  Require(row.maximumPositionError<=PositionBound&&row.maximumPointVelocityError<=VelocityBound&&row.maximumForceRoundingError<=ForceBound&&row.nativePowerError<=PowerBound&&Math.Abs(row.powerResidualWatts)<=1e-11,"Native geometry/force conversion preflight failed.");
  for(int i=0;i<bodies.Length;++i)bodies[i].AddForceAtPosition(row.before[i].appliedForce,row.before[i].point,ForceMode.Force);
  Step(dt);row.afterForce=Capture(bodies,anchors);
  for(int i=0;i<bodies.Length;++i){
   var b=row.before[i];var applied=D(b.appliedForce);var torque=Cross(D(b.point)-D(b.center),applied);
   var expectedV=D(b.velocity)+applied*((double)dt/b.mass);var expectedW=D(b.angularVelocity)+torque*((double)dt/b.inertia.x);
   row.maximumLinearVelocityError=Math.Max(row.maximumLinearVelocityError,Error(expectedV,D(row.afterForce[i].velocity)));
   row.maximumAngularVelocityError=Math.Max(row.maximumAngularVelocityError,Error(expectedW,D(row.afterForce[i].angularVelocity)));
  }
  WriteNew(Path.Combine(directory,row.key+"-forced.json"),row);
  Require(row.afterForce.All(Finite),"Nonfinite native state after force application.");
  Step(dt);row.afterClear=Capture(bodies,anchors); // No second force: verify per-step accumulator clears.
  for(int i=0;i<bodies.Length;++i){
   row.maximumClearedForceVelocityError=Math.Max(row.maximumClearedForceVelocityError,Math.Max(
    Error(D(row.afterForce[i].velocity),D(row.afterClear[i].velocity)),Error(D(row.afterForce[i].angularVelocity),D(row.afterClear[i].angularVelocity))));
  }
  row.completed=true;row.passed=row.afterClear.All(Finite)&&row.maximumLinearVelocityError<=VelocityBound&&row.maximumAngularVelocityError<=VelocityBound&&row.maximumClearedForceVelocityError<=VelocityBound;
  Require(row.passed,"Native linear/angular impulse or force-clear criteria failed; preserve original bounds.");
 }
}
}
