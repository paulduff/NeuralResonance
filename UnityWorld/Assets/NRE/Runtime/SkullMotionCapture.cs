using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nre.UnityWorld {
// Read-only capture for the isolated physical-owner qualification. This
// helper neither steps physics nor sends a receptor/brain message. Live sensory
// encoding and its previous captures remain unchanged.
public static class SkullMotionCapture {
 public const string Version="native-head-material-point-v2";
 [Serializable] public sealed class ScaleNode {public string name;public Vector3 localScale;}
 [Serializable] public sealed class ScaleControl {public string key;public bool rejected;public ScaleNode[] hierarchy;public Vector3 lossyScale;}
 [Serializable] public sealed class ScaleControlReport {public string version="native-head-scale-controls-v1";public ScaleControl[] controls;}
 [Serializable] public sealed class Snapshot {
  public string version=Version,headBodyId,sensorId;public long physicsStep;
  public Vector3 localPointMeters,originWorldMeters,rightWorld,upWorld,forwardWorld;
  public Vector3 pointWorldMeters,pointVelocityWorldMetersPerSecond,angularVelocityWorldRadiansPerSecond,gravityWorldMetersPerSecondSquared;
  // Redundant native observations support an independent point-velocity audit.
  public Vector3 centerOfMassWorldMeters,centerOfMassVelocityWorldMetersPerSecond;
  public Quaternion rotation;public Vector3 scale;
  public ScaleNode[] hierarchy;public bool legacyScaleGuardPassed;
 }
 public static Snapshot Capture(ArticulatedAvatar avatar,long physicalStep,string sensorId,Vector3 localPoint){
  if(avatar==null||physicalStep<0||string.IsNullOrWhiteSpace(sensorId)||!Finite(localPoint)||
     !avatar.links.TryGetValue("head",out var link)||link.body==null)
   throw new ArgumentException("Require the actual Avatar head and an explicitly located material point.");
  var head=link.body;var pose=head.transform;var scale=pose.lossyScale;
  var point=pose.TransformPoint(localPoint);
  var result=new Snapshot{headBodyId=link.name,sensorId=sensorId,physicsStep=physicalStep,localPointMeters=localPoint,
   originWorldMeters=pose.position,rightWorld=pose.right,upWorld=pose.up,forwardWorld=pose.forward,
   pointWorldMeters=point,pointVelocityWorldMetersPerSecond=head.GetPointVelocity(point),angularVelocityWorldRadiansPerSecond=head.angularVelocity,
   gravityWorldMetersPerSecondSquared=Physics.gravity,centerOfMassWorldMeters=head.worldCenterOfMass,
   centerOfMassVelocityWorldMetersPerSecond=head.linearVelocity,rotation=pose.rotation,scale=scale,
   hierarchy=Hierarchy(pose),legacyScaleGuardPassed=(scale-Vector3.one).sqrMagnitude<=1e-12f};
  // Authored scale is the physical precondition. The float world-matrix
  // decomposition (lossyScale) is retained as evidence, not used to infer an
  // intentional geometry change. Preserve all measurements on rejection too.
  try {RequireUnitHierarchy(result.hierarchy);}
  catch(Exception error){throw new InvalidOperationException("Invalid physical head scale: "+JsonUtility.ToJson(result),error);}
  foreach(var value in new[]{result.originWorldMeters,result.rightWorld,result.upWorld,result.forwardWorld,result.pointWorldMeters,
   result.pointVelocityWorldMetersPerSecond,result.angularVelocityWorldRadiansPerSecond,result.gravityWorldMetersPerSecondSquared,
   result.centerOfMassWorldMeters,result.centerOfMassVelocityWorldMetersPerSecond,result.scale})
   if(!Finite(value))throw new InvalidOperationException("Nonfinite native head observation; no neutral replacement.");
  return result;
 }
 static ScaleNode[] Hierarchy(Transform pose){
  var nodes=new List<ScaleNode>();for(var node=pose;node!=null;node=node.parent)nodes.Add(new ScaleNode{name=node.name,localScale=node.localScale});
  return nodes.ToArray();
 }
 static void RequireUnitHierarchy(ScaleNode[] nodes){
  if(nodes==null||nodes.Length==0)throw new InvalidOperationException("Missing physical transform hierarchy.");
  foreach(var node in nodes)if(node==null||string.IsNullOrEmpty(node.name)||node.localScale.x!=1f||node.localScale.y!=1f||node.localScale.z!=1f)
   throw new InvalidOperationException("Each physical ancestor must have exactly unit configured local scale.");
 }
 public static ScaleControlReport RunScaleControls(){
  var root=new GameObject("scale-root");var chest=new GameObject("scale-chest");var head=new GameObject("scale-head");
  chest.transform.SetParent(root.transform,false);head.transform.SetParent(chest.transform,false);
  var results=new List<ScaleControl>();
  void Record(string key,bool expectRejection){
   var nodes=Hierarchy(head.transform);bool rejected=false;
   try{RequireUnitHierarchy(nodes);}catch(InvalidOperationException){rejected=true;}
   if(rejected!=expectRejection)throw new InvalidOperationException("Physical scale control failed: "+key);
   results.Add(new ScaleControl{key=key,rejected=rejected,hierarchy=nodes,lossyScale=head.transform.lossyScale});
  }
  try{
   for(int i=0;i<15;i++){
    root.transform.localRotation=Quaternion.Euler(13*i,29*i,7*i);
    chest.transform.localRotation=Quaternion.Euler(17*i,-11*i,23*i);
    head.transform.localRotation=Quaternion.Euler(-19*i,31*i,5*i);Record("unit-rotation-"+i,false);
   }
   var invalid=new[]{new Vector3(2,2,2),new Vector3(.5f,1,1),new Vector3(-1,1,1),
    new Vector3(1.0000001192092896f,1,1),new Vector3(.9999999403953552f,1,1),new Vector3(0,1,1)};
   var transforms=new[]{head.transform,chest.transform,root.transform};
   for(int level=0;level<transforms.Length;level++)for(int kind=0;kind<invalid.Length;kind++){
    transforms[level].localScale=invalid[kind];Record("reject-level-"+level+"-kind-"+kind,true);transforms[level].localScale=Vector3.one;
   }
   return new ScaleControlReport{controls=results.ToArray()};
  }finally{UnityEngine.Object.Destroy(root);} // No physics components were created.
 }
 static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
}
}
