using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static Nre.UnityWorld.MuscleHingeWire;
using BodyState=Nre.UnityWorld.MusclePathNativeQualification.BodyState;

namespace Nre.UnityWorld {
// Sole physics owner in a dedicated empty scene. File traffic is diagnostic,
// not a timing benchmark or the production Avatar transport.
public sealed class MuscleHingeBridge : MonoBehaviour {
 [Serializable] public sealed class Geometry {
  public string version="native-opposing-hinge-v1",unity;public double stepSeconds;
  public Vector3[] anchors;public float mass,centralInertia,centerY;
  public bool gravity=false,rootImmovable=true;public float damping=0,friction=0,driveForce=0;
 }
 [Serializable] public sealed class State {public double angle,speed;public BodyState[] m0,m1;}
 ArticulationBody root,child;Vector3[][] anchors;AvatarMusclePath[] paths;
 string directory,session,geometry,bodyFingerprint;Reply last;bool physicsAttempted;long actualSteps;
 readonly System.Collections.Generic.HashSet<string> effortIds=new System.Collections.Generic.HashSet<string>();
 static MusclePathVector D(Vector3 v)=>new MusclePathVector(v.x,v.y,v.z);
 static Vector3 F(MusclePathVector v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
 static string Argument(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
 static string Digest(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
 static void WriteNew(string file,object value){string temp=file+".tmp";using(var s=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(s))w.Write(JsonUtility.ToJson(value));File.Move(temp,file);}
 ArticulationBody Make(string name,Transform parent){
  var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);
  var collider=go.AddComponent<SphereCollider>();collider.radius=.04f;collider.center=parent==null?Vector3.zero:new Vector3(0,.3f,0);
  var b=go.AddComponent<ArticulationBody>();b.mass=2;b.useGravity=false;b.linearDamping=0;b.angularDamping=0;b.sleepThreshold=0;b.jointFriction=0;
  b.centerOfMass=collider.center;b.inertiaTensor=Vector3.one*.15f;b.inertiaTensorRotation=Quaternion.identity;b.solverIterations=24;b.solverVelocityIterations=12;
  if(parent==null)b.immovable=true;
  else {b.matchAnchors=false;b.anchorPosition=Vector3.zero;b.parentAnchorPosition=Vector3.zero;b.anchorRotation=Quaternion.identity;b.parentAnchorRotation=Quaternion.identity;
   b.jointType=ArticulationJointType.RevoluteJoint;b.twistLock=ArticulationDofLock.FreeMotion;
   b.xDrive=new ArticulationDrive{stiffness=0,damping=0,forceLimit=0,target=0,targetVelocity=0};}
  return b;
 }
 BodyState[] Bodies(int i)=>new[]{root,child}.Select((b,j)=>{
  var p=b.transform.TransformPoint(anchors[i][j]);return new BodyState{id=b.name,position=b.transform.position,rotation=b.transform.rotation,center=b.worldCenterOfMass,
   mass=b.mass,inertia=b.inertiaTensor,velocity=b.linearVelocity,angularVelocity=b.angularVelocity,localAnchor=anchors[i][j],point=p,pointVelocity=b.GetPointVelocity(p)};
 }).ToArray();
 MusclePathPoint[] Observations(BodyState[] states)=>states.Select((b,j)=>new MusclePathPoint("a"+j,b.id,D(b.point),D(b.pointVelocity))).ToArray();
 Reply Capture(string operation,long sequence,long step,string plant,string effort){
  Require(Physics.simulationMode==SimulationMode.Script&&root.immovable&&child.dofCount==1,"Native physics ownership changed.");
  Require(!root.useGravity&&!child.useGravity&&child.linearDamping==0&&child.angularDamping==0&&child.jointFriction==0&&child.xDrive.forceLimit==0,
   "Unowned gravity, damping or drive enabled.");
  Require(child.mass==2&&child.inertiaTensor==Vector3.one*.15f&&child.centerOfMass==new Vector3(0,.3f,0)&&
   child.jointType==ArticulationJointType.RevoluteJoint&&child.twistLock==ArticulationDofLock.FreeMotion&&
   child.anchorPosition==Vector3.zero&&child.parentAnchorPosition==Vector3.zero&&child.anchorRotation==Quaternion.identity&&child.parentAnchorRotation==Quaternion.identity,
   "Native hinge parameters changed.");
  var state=new State{angle=child.jointPosition[0],speed=child.jointVelocity[0],m0=Bodies(0),m1=Bodies(1)};
  Require(Finite(state.angle)&&Finite(state.speed),"Nonfinite joint state.");
  var measurements=new MuscleHingeWire.Path[2];
  for(int i=0;i<2;++i){var measured=paths[i].Measure(bodyFingerprint,Observations(i==0?state.m0:state.m1));
   measurements[i]=new MuscleHingeWire.Path{muscleId="m"+i,pathFingerprint=paths[i].Fingerprint,lengthMeters=measured.MusculotendonLengthMeters,velocityMetersPerSecond=measured.MusculotendonVelocityMetersPerSecond};}
  string stateJson=JsonUtility.ToJson(state);
  return new Reply{version=MuscleHingeWire.Version,sessionId=session,operation=operation,sequence=sequence,unity=Application.unityVersion,
   bodyFingerprint=bodyFingerprint,plantFingerprint=plant??"",geometryJson=geometry,stateJson=stateJson,stateHash=Digest(stateJson),lastAppliedEffortId=effort??"",
   physicsStep=step,physicsStepMs=StepMs,physicalTimeMs=step*StepMs,isolated=true,paused=true,paths=measurements};
 }
 IEnumerator Start(){
  bool ready=false;
  try {
   directory=Argument("--motor-bridge");session=Argument("--motor-session");
   Require(!string.IsNullOrWhiteSpace(directory)&&!Directory.Exists(directory)&&GuidN(session),"Fresh isolated bridge directory/session required.");
   Require(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).All(b=>b==this)&&FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None).Length==0,"Empty isolated scene required.");
   double rest=double.Parse(Argument("--motor-rest-length"),CultureInfo.InvariantCulture);Require(Finite(rest)&&rest>.1&&rest<1,"Invalid synthetic rest length.");
   Directory.CreateDirectory(directory);Physics.simulationMode=SimulationMode.Script;Physics.gravity=Vector3.zero;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
   root=Make("fixed-root",null);child=Make("hinge-child",root.transform);
   float z=(float)Math.Sqrt(rest*rest-Math.Pow((double).4f-(double).3f,2));
   anchors=new[]{new[]{new Vector3(0,.3f,z),new Vector3(0,.4f,0)},new[]{new Vector3(0,.3f,-z),new Vector3(0,.4f,0)}};
   geometry=JsonUtility.ToJson(new Geometry{unity=Application.unityVersion,stepSeconds=(double)StepSeconds,
    anchors=anchors.SelectMany(a=>a).ToArray(),mass=child.mass,centralInertia=child.inertiaTensor.x,centerY=child.centerOfMass.y});
   bodyFingerprint=Digest(geometry);
   paths=anchors.Select((a,i)=>new AvatarMusclePath("m"+i,bodyFingerprint,"isolated-opposing-hinge-v1",
    new[]{new MusclePathAnchor("a0",root.name,D(a[0])),new MusclePathAnchor("a1",child.name,D(a[1]))})).ToArray();
   Physics.SyncTransforms();ready=true;
  }catch(Exception e){Fail(e);}
  if(!ready){Application.Quit(1);yield break;}
  yield return null; // Register components, with zero physical steps and no time advance.
  try {last=Capture("hello",0,0,"","");Require(child.jointPosition[0]==0&&child.jointVelocity[0]==0,"Unevolved initial hinge required.");WriteNew(System.IO.Path.Combine(directory,"hello.json"),last);}
  catch(Exception e){Fail(e);Application.Quit(1);yield break;}
  var waiting=System.Diagnostics.Stopwatch.StartNew();
  while(true){
   string file=System.IO.Path.Combine(directory,"request-"+(last.sequence+1).ToString("D6")+".json");
   if(!File.Exists(file)){if(waiting.Elapsed.TotalMinutes>5){Fail(new TimeoutException("No command for five minutes."));Application.Quit(1);yield break;}yield return null;continue;}
   bool stop=false,failed=false;physicsAttempted=false;
   try {
    var current=Capture(last.operation,last.sequence,last.physicsStep,last.plantFingerprint,last.lastAppliedEffortId);
    Require(current.stateHash==last.stateHash,"Native state changed without an owned step.");
    var request=JsonUtility.FromJson<Request>(File.ReadAllText(file));Validate(request,current);
    long step=last.physicsStep;string plant=last.plantFingerprint,effort=last.lastAppliedEffortId;
    if(request.operation=="bind")plant=request.plantFingerprint;
    if(request.operation=="step"){
     Require(effortIds.Add(request.effortId),"Repeated effort identity.");
     var states=new[]{Bodies(0),Bodies(1)};
     var forces=Enumerable.Range(0,2).Select(i=>paths[i].Measure(bodyFingerprint,Observations(states[i])).ApplyTension(request.tensions[i].axialNewtons)).ToArray();
     // Validate every converted force before the first force is queued.
     foreach(var f in forces.SelectMany(f=>f.PointForces))Require(D(F(f.ForceNewtons)).IsFinite,"Native force cannot be represented.");
     physicsAttempted=true;
     for(int i=0;i<2;++i)for(int j=0;j<2;++j)(j==0?root:child).AddForceAtPosition(F(forces[i].PointForces[j].ForceNewtons),states[i][j].point,ForceMode.Force);
     Physics.Simulate(StepSeconds);actualSteps=checked(actualSteps+1);step=actualSteps;effort=request.effortId;
    }
    last=Capture(request.operation,request.sequence,step,plant,effort);
    WriteNew(System.IO.Path.Combine(directory,"reply-"+last.sequence.ToString("D6")+".json"),last);
    waiting.Restart();stop=request.operation=="stop";
   }catch(Exception e){Fail(e);failed=true;}
   if(failed||stop){Application.Quit(failed?1:0);yield break;}
  }
 }
 void Fail(Exception e){Debug.LogException(e);if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory)){
  Reply observed=null;try{if(last!=null)observed=Capture("failure",last.sequence,actualSteps,last.plantFingerprint,last.lastAppliedEffortId);}catch{}
  WriteNew(System.IO.Path.Combine(directory,"failure.json"),new Failure{error=e.ToString(),sessionId=session,sequence=last==null?0:last.sequence,
   physicsStep=actualSteps,physicsAttempted=physicsAttempted,observed=observed});}}
}
}
