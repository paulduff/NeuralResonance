using System;
using UnityEngine;

namespace Nre.UnityWorld {
// Read-only capture for a future isolated physical-owner qualification. This
// helper neither steps physics nor sends a receptor/brain message. Live sensory
// encoding and its previous captures remain unchanged.
public static class SkullMotionCapture {
 public const string Version="native-head-material-point-v1";
 [Serializable] public sealed class Snapshot {
  public string version=Version,headBodyId,sensorId;public long physicsStep;
  public Vector3 localPointMeters,originWorldMeters,rightWorld,upWorld,forwardWorld;
  public Vector3 pointWorldMeters,pointVelocityWorldMetersPerSecond,angularVelocityWorldRadiansPerSecond,gravityWorldMetersPerSecondSquared;
  // Redundant native observations support an independent point-velocity audit.
  public Vector3 centerOfMassWorldMeters,centerOfMassVelocityWorldMetersPerSecond;
  public Quaternion rotation;public Vector3 scale;
 }
 public static Snapshot Capture(ArticulatedAvatar avatar,long physicalStep,string sensorId,Vector3 localPoint){
  if(avatar==null||physicalStep<0||string.IsNullOrWhiteSpace(sensorId)||!Finite(localPoint)||
     !avatar.links.TryGetValue("head",out var link)||link.body==null)
   throw new ArgumentException("Require the actual Avatar head and an explicitly located material point.");
  var head=link.body;var pose=head.transform;var scale=pose.lossyScale;
  if((scale-Vector3.one).sqrMagnitude>1e-12f)throw new InvalidOperationException("Head point capture requires an unscaled physical frame.");
  var point=pose.TransformPoint(localPoint);
  var result=new Snapshot{headBodyId=link.name,sensorId=sensorId,physicsStep=physicalStep,localPointMeters=localPoint,
   originWorldMeters=pose.position,rightWorld=pose.right,upWorld=pose.up,forwardWorld=pose.forward,
   pointWorldMeters=point,pointVelocityWorldMetersPerSecond=head.GetPointVelocity(point),angularVelocityWorldRadiansPerSecond=head.angularVelocity,
   gravityWorldMetersPerSecondSquared=Physics.gravity,centerOfMassWorldMeters=head.worldCenterOfMass,
   centerOfMassVelocityWorldMetersPerSecond=head.linearVelocity,rotation=pose.rotation,scale=scale};
  foreach(var value in new[]{result.originWorldMeters,result.rightWorld,result.upWorld,result.forwardWorld,result.pointWorldMeters,
   result.pointVelocityWorldMetersPerSecond,result.angularVelocityWorldRadiansPerSecond,result.gravityWorldMetersPerSecondSquared,
   result.centerOfMassWorldMeters,result.centerOfMassVelocityWorldMetersPerSecond,result.scale})
   if(!Finite(value))throw new InvalidOperationException("Nonfinite native head observation; no neutral replacement.");
  return result;
 }
 static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
}
}
