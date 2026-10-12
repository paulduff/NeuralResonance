using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Nre.UnityWorld {
[Serializable] public sealed class BodyFrame {
 public long sequence,timestampMs;
 public float linearVelocityXMetersPerSecond,linearVelocityYMetersPerSecond,linearVelocityZMetersPerSecond,angularVelocityXRadiansPerSecond,angularVelocityYRadiansPerSecond,angularVelocityZRadiansPerSecond;
 // Energy, hydration and tissue are advanced by the canonical Avatar rules.
 // Temperature and oxygen remain reference values until those plants migrate.
 public float storedEnergyJoules=8000000,tissueIntegrityFraction=1,coreTemperatureCelsius=37,bloodOxygenSaturationFraction=.98f,hydrationFraction=1;
 public string inputSource="avatar_unity_body";public bool motorTrainingMode=false;public ArticulationFrame articulation;
 public static BodyFrame Capture(ArticulatedAvatar a,long sequence){var v=a.BodyVector(a.pelvis.linearVelocity);var w=a.BodyVector(a.pelvis.angularVelocity);
  var result=new BodyFrame{sequence=sequence,timestampMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),linearVelocityXMetersPerSecond=v.x,linearVelocityYMetersPerSecond=v.y,linearVelocityZMetersPerSecond=v.z,angularVelocityXRadiansPerSecond=w.x,angularVelocityYRadiansPerSecond=w.y,angularVelocityZRadiansPerSecond=w.z};
  var physiology=a.physiology.State;result.storedEnergyJoules=(float)physiology.StoredEnergyJoules;result.hydrationFraction=(float)physiology.HydrationFraction;result.tissueIntegrityFraction=(float)physiology.TissueIntegrityFraction;
  result.articulation=new ArticulationFrame{
   leftHipAngleRadians=a.Angle("left_thigh"),rightHipAngleRadians=a.Angle("right_thigh"),leftKneeAngleRadians=a.Angle("left_shin"),rightKneeAngleRadians=a.Angle("right_shin"),leftAnkleAngleRadians=a.Angle("left_foot"),rightAnkleAngleRadians=a.Angle("right_foot"),leftFootLoadNewtons=a.LeftLoad,rightFootLoadNewtons=a.RightLoad,
   leftShoulderAngleRadians=a.Angle("left_arm"),rightShoulderAngleRadians=a.Angle("right_arm"),leftElbowAngleRadians=a.Angle("left_forearm"),rightElbowAngleRadians=a.Angle("right_forearm"),leftHandLoadNewtons=BodySensorMath.HandReceptorLoad(a.links["left_hand"].sensor.force),rightHandLoadNewtons=BodySensorMath.HandReceptorLoad(a.links["right_hand"].sensor.force),
   leftShoulderAbductionRadians=a.Angle("left_arm",1),rightShoulderAbductionRadians=a.Angle("right_arm",1),neckPitchRadians=a.Angle("head"),neckYawRadians=a.Angle("head",1),leftHipAbductionRadians=a.Angle("left_thigh",1),rightHipAbductionRadians=a.Angle("right_thigh",1),leftAnkleRollRadians=a.Angle("left_foot",1),rightAnkleRollRadians=a.Angle("right_foot",1),trunkYawRadians=a.Angle("chest")};
  var up=a.pelvis.transform.up;float pitch=Mathf.Atan2(up.z,up.y),roll=Mathf.Atan2(-up.x,up.y);
  // The canonical vestibular organ adds root, relative trunk and neck motion.
  // Sending pelvis tilt again here double-counts its tilt and angular derivative.
  var rootRotation=a.pelvis.transform.rotation;var chestRotation=a.links["chest"].body.transform.rotation;
  var trunk=BodySensorMath.RelativeTrunkTilt(new System.Numerics.Quaternion(rootRotation.x,rootRotation.y,rootRotation.z,rootRotation.w),
   new System.Numerics.Quaternion(chestRotation.x,chestRotation.y,chestRotation.z,chestRotation.w));
  result.articulation.trunkPitchRadians=trunk.pitch;result.articulation.trunkRollRadians=trunk.roll;
  result.articulation.leftFootPressure=FootPressure(a,"left_foot");result.articulation.rightFootPressure=FootPressure(a,"right_foot");
  // Balance lives in a gravity-aligned frame, not the tumbling receptor frame.
  var forward=Vector3.ProjectOnPlane(a.pelvis.transform.forward,Vector3.up);
  var inverseHeading=Quaternion.Inverse(forward.sqrMagnitude>.0001f?Quaternion.LookRotation(forward.normalized):Quaternion.identity);
  var origin=new Vector3(a.pelvis.transform.position.x,0,a.pelvis.transform.position.z);
  Vector3 GroundPoint(Vector3 point)=>inverseHeading*(point-origin);
  var comWorld=a.CenterOfMass();var com=GroundPoint(comWorld);var velocity=Vector3.zero;foreach(var l in a.links.Values)velocity+=l.body.linearVelocity*l.body.mass;velocity=inverseHeading*(velocity/a.totalMass);
  var contacts=a.links.Values.Where(l=>l.sensor.force>0&&l.sensor.normal.y>.5f).SelectMany(l=>l.sensor.supportPoints).Select(GroundPoint).ToArray();
  var hull=Hull(contacts.Select(p=>new Vector2(p.x,p.z)).ToArray());float load=a.LeftLoad+a.RightLoad;
  float upright=Mathf.Clamp01(Vector3.Dot(up,Vector3.up));var copWorld=Vector3.zero;float support=0;
  foreach(var l in a.links.Values.Where(l=>l.sensor.force>0&&l.sensor.normal.y>.5f&&l.sensor.supportPoints.Count>0)){
   var center=l.sensor.supportPoints.Aggregate(Vector3.zero,(sum,p)=>sum+p)/l.sensor.supportPoints.Count;
   copWorld+=center*l.sensor.force;support+=l.sensor.force;
  }
  var cop=support>0?GroundPoint(copWorld/support):new Vector3();float frequency=Mathf.Sqrt(9.81f/Mathf.Max(.1f,comWorld.y));var extrapolated=new Vector2(com.x+velocity.x/frequency,com.z+velocity.z/frequency);
  float margin=Margin(hull,extrapolated);string phase=upright<.3f?"fallen":contacts.Length==0?"airborne":margin<0?"falling":"stable";
  result.articulation.musculoskeletal=new MusculoskeletalFrame{posture=upright>.7f?"upright":"inclined",bodyHeightMeters=Mathf.Max(0,a.links["head"].body.transform.position.y+.2f),uprightFraction=upright,supportFraction=Mathf.Clamp01(support/(a.totalMass*9.81f)),balanceError=BodySensorMath.PhysicalBalanceError(margin,pitch,roll,phase),muscles=Array.Empty<MuscleFrame>(),balance=new BalanceFrame{
   centerOfMassXMeters=com.x,centerOfMassYMeters=com.y,centerOfMassZMeters=com.z,centerOfMassVelocityXMetersPerSecond=velocity.x,centerOfMassVelocityZMetersPerSecond=velocity.z,extrapolatedCenterOfMassXMeters=extrapolated.x,extrapolatedCenterOfMassZMeters=extrapolated.y,centerOfPressureXMeters=cop.x,centerOfPressureZMeters=cop.z,supportAreaSquareMeters=Area(hull),supportMarginMeters=margin,fallPitchRadians=pitch,fallRollRadians=roll,fallPitchVelocityRadiansPerSecond=w.x,fallRollVelocityRadiansPerSecond=w.z,phase=phase}};
  if(a.MusclesEnabled)result.articulation.musculoskeletal.muscles=a.muscles.Capture().Select(m=>new MuscleFrame{
   name=m.Name,side=m.Side,activation=(float)m.Activation,forceNewtons=(float)m.ForceNewtons,
   lengthFraction=(float)m.LengthFraction,velocityPerSecond=(float)m.VelocityPerSecond,fatigueFraction=(float)m.FatigueFraction}).ToArray();
  return result;
 }
 static FootPressureFrame FootPressure(ArticulatedAvatar a,string name){var l=a.links[name];var box=l.body.GetComponentInChildren<BoxCollider>();
  var points=l.sensor.supportPoints.Select(p=>box.transform.InverseTransformPoint(p)-box.center).Select(p=>new PlantarContact(p.x,p.z)).ToArray();
  return PlantarPressure.Capture(name=="left_foot",l.sensor.force,points);
 }
 static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
 static List<Vector2> Hull(Vector2[] points){var sorted=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();if(sorted.Length<3)return sorted.ToList();var lower=new List<Vector2>();var upper=new List<Vector2>();foreach(var p in sorted){while(lower.Count>=2&&Cross(lower[lower.Count-2],lower[lower.Count-1],p)<=0)lower.RemoveAt(lower.Count-1);lower.Add(p);}foreach(var p in sorted.Reverse()){while(upper.Count>=2&&Cross(upper[upper.Count-2],upper[upper.Count-1],p)<=0)upper.RemoveAt(upper.Count-1);upper.Add(p);}lower.RemoveAt(lower.Count-1);upper.RemoveAt(upper.Count-1);lower.AddRange(upper);return lower;}
 static float Area(List<Vector2> hull){float area=0;for(int i=0;i<hull.Count;i++){var a=hull[i];var b=hull[(i+1)%hull.Count];area+=a.x*b.y-b.x*a.y;}return Mathf.Abs(area)*.5f;}
 static float Margin(List<Vector2> hull,Vector2 point){if(hull.Count<3)return -.1f;float distance=float.MaxValue;bool inside=true;for(int i=0;i<hull.Count;i++){var a=hull[i];var b=hull[(i+1)%hull.Count];inside&=Cross(a,b,point)>=0;float t=Mathf.Clamp01(Vector2.Dot(point-a,b-a)/Mathf.Max(.000001f,(b-a).sqrMagnitude));distance=Mathf.Min(distance,Vector2.Distance(point,a+t*(b-a)));}return inside?distance:-distance;}
}
[Serializable] public sealed class ArticulationFrame {
 public FootPressureFrame leftFootPressure,rightFootPressure;
 public float leftHipAngleRadians,rightHipAngleRadians,leftKneeAngleRadians,rightKneeAngleRadians,leftAnkleAngleRadians,rightAnkleAngleRadians,leftFootLoadNewtons,rightFootLoadNewtons,leftShoulderAngleRadians,rightShoulderAngleRadians,leftElbowAngleRadians,rightElbowAngleRadians,leftHandLoadNewtons,rightHandLoadNewtons,manipulatorExtensionFraction,trunkPitchRadians,trunkRollRadians,leftShoulderAbductionRadians,rightShoulderAbductionRadians,neckYawRadians,neckPitchRadians,supportPlaneOffsetMeters,leftHipAbductionRadians,rightHipAbductionRadians,leftAnkleRollRadians,rightAnkleRollRadians,trunkYawRadians;
 public MusculoskeletalFrame musculoskeletal;
}
[Serializable] public sealed class MusculoskeletalFrame {public string posture;public float bodyHeightMeters,uprightFraction,supportFraction,balanceError;public MuscleFrame[] muscles;public BalanceFrame balance;}
[Serializable] public sealed class MuscleFrame {public string name,side;public float activation,forceNewtons,lengthFraction,velocityPerSecond,fatigueFraction;}
[Serializable] public sealed class BalanceFrame {public float centerOfMassXMeters,centerOfMassYMeters,centerOfMassZMeters,centerOfMassVelocityXMetersPerSecond,centerOfMassVelocityZMetersPerSecond,extrapolatedCenterOfMassXMeters,extrapolatedCenterOfMassZMeters,centerOfPressureXMeters,centerOfPressureZMeters,supportAreaSquareMeters,supportMarginMeters,fallPitchRadians,fallRollRadians,fallPitchVelocityRadiansPerSecond,fallRollVelocityRadiansPerSecond,rightingForceFraction,dynamicStabilityAllowanceMeters;public string phase;}
[Serializable] public sealed class ContactFrame {
 public long sequence,timestampMs;public float bodyPositionX,bodyPositionY,bodyPositionZ,surfaceNormalX,surfaceNormalY,surfaceNormalZ,forceNewtons,impulseNewtonSeconds,penetrationMillimeters,tangentialSpeedMetersPerSecond,contactAreaSquareMillimeters,durationMilliseconds;public string inputSource;
 public static ContactFrame Capture(ArticulatedAvatar a,ArticulatedAvatar.Link link,long sequence){var c=link.sensor;var p=a.BodyPoint(c.point);var n=a.BodyVector(c.normal);return new ContactFrame{sequence=sequence,timestampMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),bodyPositionX=p.x,bodyPositionY=p.y,bodyPositionZ=p.z,surfaceNormalX=n.x,surfaceNormalY=n.y,surfaceNormalZ=n.z,forceNewtons=Mathf.Min(c.force,20000),impulseNewtonSeconds=Mathf.Min(c.impulse,1000),penetrationMillimeters=Mathf.Min(c.penetration,250),tangentialSpeedMetersPerSecond=Mathf.Min(c.slip,100),contactAreaSquareMillimeters=link.name.EndsWith("foot")?8000:2000,durationMilliseconds=Mathf.Min(c.duration*1000,60000),inputSource="avatar_unity_"+link.name+"_contact"};}
}
}
