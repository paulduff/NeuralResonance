using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BodyState=Nre.UnityWorld.MusclePathNativeQualification.BodyState;

namespace Nre.UnityWorld {
// Isolated calibration fixture. No production root/pose is anchored or reset.
public sealed class MusclePathHingeQualification : MonoBehaviour {
 const double GeometryBound=2e-6,VelocityBound=2e-6,TorqueBound=3e-6,PowerBound=2e-5;
 [Serializable] public sealed class Case {
  public string key,error,pathFingerprint;public int side;public bool completed,passed;
  public double targetAngleRadians,stepSeconds,tensionNewtons,effectiveInertia;
  public double angleBefore,angleAfterForce,angleAfterClear,speedBefore,speedAfterForce,speedAfterClear;
  public double lengthMeters,pathSpeedMetersPerSecond,analyticLengthMeters,analyticLengthDerivative,measuredTorque,analyticTorque,powerWatts;
  public double geometryError,pointVelocityError,torqueError,powerError,impulseSpeedError,clearSpeedError;
  public double forcedAngleError,forcedAngleAllowance,clearAngleError,endpointPoseError,endpointLinearVelocityError,endpointAngularVelocityError;
  public double rootPositionError,rootLinearSpeed,rootAngularSpeed,rootRotationError;
  public long startStep,endStep;public BodyState[] before,afterForce,afterClear;
 }
 [Serializable] public sealed class Report {
  public string unity,sessionId,error;public bool completed,passed,isolated=true,neuronalModelExecuted=false,avatarLoaded=false,anatomicalCalibrationQualified=false;
  public double geometryBound=GeometryBound,angleRoundoffBound=GeometryBound,velocityBound=VelocityBound,torqueBound=TorqueBound,powerBound=PowerBound;
  public int warmupStepsPerCase=1;public long actualPhysicsSteps;public Case[] cases;
 }
 readonly List<Case> cases=new List<Case>();string directory;long steps;
 static MusclePathVector D(Vector3 v)=>new MusclePathVector(v.x,v.y,v.z);
 static Vector3 F(MusclePathVector v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
 static MusclePathVector V(double x=0,double y=0,double z=0)=>new MusclePathVector(x,y,z);
 static MusclePathVector Cross(MusclePathVector a,MusclePathVector b)=>V(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
 static double Error(MusclePathVector a,MusclePathVector b)=>(a-b).Magnitude;
 static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 static string Argument(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
 static void WriteNew(string path,object value){using(var s=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(s))w.Write(JsonUtility.ToJson(value,true));}
 static bool Finite(BodyState b)=>D(b.position).IsFinite&&D(b.center).IsFinite&&D(b.velocity).IsFinite&&D(b.angularVelocity).IsFinite&&D(b.point).IsFinite&&D(b.pointVelocity).IsFinite&&D(b.inertia).IsFinite&&MusclePathVector.Finite(b.mass)&&b.mass>0&&
  MusclePathVector.Finite(b.rotation.x)&&MusclePathVector.Finite(b.rotation.y)&&MusclePathVector.Finite(b.rotation.z)&&MusclePathVector.Finite(b.rotation.w);
 void Step(float dt){Require(Physics.simulationMode==SimulationMode.Script,"Physics ownership changed.");Physics.Simulate(dt);steps++;}
 ArticulationBody Make(string name,Transform parent){
  var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);
  var collider=go.AddComponent<SphereCollider>();collider.radius=.04f;collider.center=parent==null?Vector3.zero:new Vector3(0,.3f,0);
  var b=go.AddComponent<ArticulationBody>();b.mass=2;b.useGravity=false;b.linearDamping=0;b.angularDamping=0;b.sleepThreshold=0;b.jointFriction=0;
  b.centerOfMass=collider.center;b.inertiaTensor=Vector3.one*.15f;b.inertiaTensorRotation=Quaternion.identity;b.solverIterations=24;b.solverVelocityIterations=12;
  if(parent==null)b.immovable=true;
  else {
   b.matchAnchors=false;b.anchorPosition=Vector3.zero;b.parentAnchorPosition=Vector3.zero;b.anchorRotation=Quaternion.identity;b.parentAnchorRotation=Quaternion.identity;
   b.jointType=ArticulationJointType.RevoluteJoint;b.twistLock=ArticulationDofLock.FreeMotion;
   b.xDrive=new ArticulationDrive{stiffness=0,damping=0,forceLimit=0,target=0,targetVelocity=0};
  }
  return b;
 }
 BodyState[] Capture(ArticulationBody[] bodies,Vector3[] anchors)=>bodies.Select((b,i)=>{
  var p=b.transform.TransformPoint(anchors[i]);return new BodyState{id=b.name,position=b.transform.position,rotation=b.transform.rotation,center=b.worldCenterOfMass,
   mass=b.mass,inertia=b.inertiaTensor,velocity=b.linearVelocity,angularVelocity=b.angularVelocity,localAnchor=anchors[i],point=p,pointVelocity=b.GetPointVelocity(p)};
 }).ToArray();
 void CheckEndpoint(Case row,BodyState[] states,double q,double speed,double center){
  var root=states[0];var child=states[1];double r=child.localAnchor.y;
  var point=V(0,r*Math.Cos(q),r*Math.Sin(q));var com=V(0,center*Math.Cos(q),center*Math.Sin(q));
  row.endpointPoseError=Math.Max(row.endpointPoseError,Math.Max(D(child.position).Magnitude,Math.Max(Error(point,D(child.point)),Error(com,D(child.center)))));
  var omega=V(speed);row.endpointAngularVelocityError=Math.Max(row.endpointAngularVelocityError,Error(omega,D(child.angularVelocity)));
  row.endpointLinearVelocityError=Math.Max(row.endpointLinearVelocityError,Math.Max(Error(Cross(omega,com),D(child.velocity)),Error(Cross(omega,point),D(child.pointVelocity))));
  row.rootPositionError=Math.Max(row.rootPositionError,D(root.position).Magnitude);
  row.rootLinearSpeed=Math.Max(row.rootLinearSpeed,D(root.velocity).Magnitude);row.rootAngularSpeed=Math.Max(row.rootAngularSpeed,D(root.angularVelocity).Magnitude);
  // Quaternion components are dimensionless; keep this separate from metres/s.
  row.rootRotationError=Math.Max(row.rootRotationError,Math.Max(D(new Vector3(root.rotation.x,root.rotation.y,root.rotation.z)).Magnitude,Math.Abs(Math.Abs(root.rotation.w)-1)));
 }
 IEnumerator Start(){
  var report=new Report{unity=Application.unityVersion,sessionId=Argument("--hinge-session")};bool ready=false;
  try {
   directory=Argument("--hinge-output");Require(Guid.TryParseExact(report.sessionId,"N",out _)&&!string.IsNullOrWhiteSpace(directory)&&!Directory.Exists(directory),"Fresh explicit hinge session/output required.");
   Require(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).All(b=>b==this)&&FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None).Length==0,"Empty isolated fixture scene required.");
   Directory.CreateDirectory(directory);Physics.simulationMode=SimulationMode.Script;Physics.gravity=Vector3.zero;ready=true;
  }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
  if(!ready){Application.Quit(1);yield break;}
  foreach(float angle in new[]{-.6f,0f,.6f})foreach(int side in new[]{-1,1})foreach(float dt in new[]{.001f,.000125f})foreach(double tension in new[]{0.0,10.0}){
   var row=new Case{key="case-"+cases.Count.ToString("D2"),targetAngleRadians=(double)angle,side=side,stepSeconds=(double)dt,tensionNewtons=tension,startStep=steps};cases.Add(row);
   ArticulationBody root=null,child=null;
   try {root=Make("fixed-root",null);child=Make("hinge-child",root.transform);Physics.SyncTransforms();}catch(Exception e){row.error=e.ToString();}
   yield return null;
   try {if(row.error==null)RunCase(row,root,child,angle,dt);}catch(Exception e){row.error=e.ToString();Debug.LogException(e);}
   finally {row.endStep=steps;WriteNew(Path.Combine(directory,row.key+".json"),row);if(root!=null)Destroy(root.gameObject);}
   yield return null;
  }
  report.cases=cases.ToArray();report.actualPhysicsSteps=steps;report.completed=cases.Count==24&&cases.All(c=>c.completed);report.passed=report.completed&&steps==72&&cases.All(c=>c.passed);
  WriteNew(Path.Combine(directory,"results.json"),report);Application.Quit(report.passed?0:1);
 }
 void RunCase(Case row,ArticulationBody root,ArticulationBody child,float angle,float dt){
  Require(root.isRoot&&root.immovable&&!child.isRoot&&child.dofCount==1,"Require one fixed root and exactly one native hinge coordinate.");
  Require(FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None).Length==2,"Unexpected physical body in scene.");
  child.jointPosition=new ArticulationReducedSpace(angle);child.jointVelocity=new ArticulationReducedSpace(.25f);child.jointForce=new ArticulationReducedSpace(0);child.WakeUp();
  Step(dt); // Explicit isolated pose registration; no muscle force. Included in step evidence.
  row.angleBefore=child.jointPosition[0];row.speedBefore=child.jointVelocity[0];
  var bodies=new[]{root,child};var anchors=new[]{new Vector3(0,.3f,row.side*.25f),new Vector3(0,.4f,0)};
  row.before=Capture(bodies,anchors);
  var path=new AvatarMusclePath("hinge-fixture",row.key,"Synthetic constrained hinge; no human anatomy",row.before.Select((b,i)=>new MusclePathAnchor("anchor-"+i,b.id,D(b.localAnchor))));
  var measured=path.Measure(row.key,row.before.Select((b,i)=>new MusclePathPoint("anchor-"+i,b.id,D(b.point),D(b.pointVelocity))));var forces=measured.ApplyTension(row.tensionNewtons);
  row.pathFingerprint=path.Fingerprint;row.lengthMeters=measured.MusculotendonLengthMeters;row.pathSpeedMetersPerSecond=measured.MusculotendonVelocityMetersPerSecond;row.powerWatts=forces.PowerOnBodiesWatts;
  double q=row.angleBefore,r=anchors[1].y,center=child.centerOfMass.y;
  var point=V(0,r*Math.Cos(q),r*Math.Sin(q));var derivative=V(0,-r*Math.Sin(q),r*Math.Cos(q));
  var delta=point-D(anchors[0]);row.analyticLengthMeters=delta.Magnitude;row.analyticLengthDerivative=MusclePathVector.Dot(delta,derivative)/row.analyticLengthMeters;
  row.analyticTorque=-row.tensionNewtons*row.analyticLengthDerivative;row.measuredTorque=Cross(D(row.before[1].point),forces.PointForces[1].ForceNewtons).X;
  row.effectiveInertia=(double)child.inertiaTensor.x+child.mass*center*center;
  CheckEndpoint(row,row.before,row.angleBefore,row.speedBefore,center);
  row.geometryError=Math.Max(Error(point,D(row.before[1].point)),Math.Abs(row.lengthMeters-row.analyticLengthMeters));
  row.pointVelocityError=Math.Max(Error(derivative*row.speedBefore,D(row.before[1].pointVelocity)),Math.Abs(row.pathSpeedMetersPerSecond-row.analyticLengthDerivative*row.speedBefore));
  row.torqueError=Math.Abs(row.measuredTorque-row.analyticTorque);row.powerError=Math.Abs(row.powerWatts-row.analyticTorque*row.speedBefore);
  for(int i=0;i<2;++i)row.before[i].appliedForce=F(forces.PointForces[i].ForceNewtons);
  WriteNew(Path.Combine(directory,row.key+"-initial.json"),row);
  Require(row.before.All(Finite)&&Math.Abs(row.angleBefore-((double)angle+.25*(double)dt))<=GeometryBound&&Math.Abs(row.speedBefore-.25)<=VelocityBound,"Hinge registration/initial speed differs from declared state.");
  Require(child.mass==2&&child.inertiaTensor==Vector3.one*.15f&&child.centerOfMass==new Vector3(0,.3f,0),"Hinge mass/inertia/center differs.");
  Require(row.geometryError<=GeometryBound&&row.pointVelocityError<=VelocityBound&&row.torqueError<=TorqueBound&&row.powerError<=PowerBound&&row.endpointPoseError<=GeometryBound&&row.endpointLinearVelocityError<=VelocityBound&&row.endpointAngularVelocityError<=VelocityBound,"Hinge geometry, point velocity, moment arm or power preflight failed.");
  double nativeTorque=Cross(D(row.before[1].point),D(row.before[1].appliedForce)).X;
  for(int i=0;i<2;++i)bodies[i].AddForceAtPosition(row.before[i].appliedForce,row.before[i].point,ForceMode.Force);
  Step(dt);row.afterForce=Capture(bodies,anchors);row.angleAfterForce=child.jointPosition[0];row.speedAfterForce=child.jointVelocity[0];
  row.impulseSpeedError=Math.Abs(row.speedAfterForce-(row.speedBefore+nativeTorque*(double)dt/row.effectiveInertia));
  double acceleration=nativeTorque/row.effectiveInertia,h=(double)dt;
  // A single native step may use first-order position integration. Preserve
  // its analytical truncation envelope; do not call this trajectory convergence.
  row.forcedAngleError=Math.Abs(row.angleAfterForce-(row.angleBefore+row.speedBefore*h+.5*acceleration*h*h));
  row.forcedAngleAllowance=GeometryBound+.5*Math.Abs(acceleration)*h*h;
  CheckEndpoint(row,row.afterForce,row.angleAfterForce,row.speedAfterForce,center);
  WriteNew(Path.Combine(directory,row.key+"-forced.json"),row);
  Require(row.afterForce.All(Finite),"Nonfinite forced hinge state.");
  Step(dt);row.afterClear=Capture(bodies,anchors);row.angleAfterClear=child.jointPosition[0];row.speedAfterClear=child.jointVelocity[0];row.clearSpeedError=Math.Abs(row.speedAfterClear-row.speedAfterForce);
  row.clearAngleError=Math.Abs(row.angleAfterClear-(row.angleAfterForce+row.speedAfterForce*h));CheckEndpoint(row,row.afterClear,row.angleAfterClear,row.speedAfterClear,center);
  row.completed=true;row.passed=row.afterClear.All(Finite)&&row.impulseSpeedError<=VelocityBound&&row.clearSpeedError<=VelocityBound&&row.forcedAngleError<=row.forcedAngleAllowance&&row.clearAngleError<=GeometryBound&&
   row.endpointPoseError<=GeometryBound&&row.endpointLinearVelocityError<=VelocityBound&&row.endpointAngularVelocityError<=VelocityBound&&row.rootPositionError<=GeometryBound&&row.rootLinearSpeed<=VelocityBound&&row.rootAngularSpeed<=VelocityBound&&row.rootRotationError<=GeometryBound&&MusclePathVector.Finite(row.angleAfterForce)&&MusclePathVector.Finite(row.angleAfterClear);
  Require(row.passed,"Hinge impulse, clearing or fixed-root criteria failed; preserve original bounds.");
 }
}
}
