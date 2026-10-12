using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Nre.UnityWorld {
public sealed class ArticulatedAvatar {
 public sealed class Link { public string name;public ArticulationBody body;public Transform bone;public Quaternion bindRotation;public ContactSensor sensor;public float torqueLimit;public int dofs; }
 public readonly Dictionary<string,Link> links=new Dictionary<string,Link>();
 public ArticulationBody pelvis;public GameObject visual;public float totalMass;public float initialPelvisHeight;
 public float LeftLoad=>Load("left_foot");public float RightLoad=>Load("right_foot");
 public static ArticulatedAvatar Create(GameObject prefab) {
  if(prefab==null)throw new InvalidOperationException("Install the supplied humanoid model with tools/prepare-unity-avatar.py.");
  var avatar=new ArticulatedAvatar();avatar.visual=UnityEngine.Object.Instantiate(prefab);avatar.visual.name="Avatar appearance (physics driven)";
  var animator=avatar.visual.GetComponent<Animator>();
  if(animator==null||animator.avatar==null||!animator.avatar.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("The avatar needs a validated humanoid skeleton.");
  var renderers=avatar.visual.GetComponentsInChildren<SkinnedMeshRenderer>();var bounds=new Bounds();bool first=true;
  foreach(var r in renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);r.updateWhenOffscreen=true;}
  avatar.visual.transform.localScale*=1.74f/bounds.size.y;
  var left=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var right=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
  if(right.position.x<left.position.x)avatar.visual.transform.Rotate(0,180,0);
  var leftArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);var rightArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
  // Start from a relaxed shoulder pose, rather than holding the scan's T-pose.
  leftArm.rotation=Quaternion.AngleAxis(75,Vector3.forward)*leftArm.rotation;
  rightArm.rotation=Quaternion.AngleAxis(-75,Vector3.forward)*rightArm.rotation;
  var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
  var footMin=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
  avatar.visual.transform.position+=new Vector3(-hips.position.x,.10f-footMin,-hips.position.z);
  animator.enabled=false;
  Transform B(HumanBodyBones id)=>animator.GetBoneTransform(id);
  var root=avatar.Add("pelvis",null,B(HumanBodyBones.Hips),null,14.4f,0,0,Quaternion.identity,0,0,0,0);
  root.body.immovable=false;avatar.pelvis=root.body;avatar.initialPelvisHeight=root.body.transform.position.y;
  var spine=avatar.Add("chest",root,B(HumanBodyBones.Spine),B(HumanBodyBones.Neck),25f,1,90,Quaternion.FromToRotation(Vector3.right,Vector3.up),-35,35,0,0);
  avatar.Add("head",spine,B(HumanBodyBones.Head),null,4.6f,2,15,Quaternion.identity,-45,55,-75,75);
  foreach(bool isLeft in new[]{true,false}){
   string side=isLeft?"left":"right";
   var thigh=avatar.Add(side+"_thigh",root,B(isLeft?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg),B(isLeft?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg),6.5f,2,150,Quaternion.Euler(0,isLeft?180:0,isLeft?0:180),-20,120,-26,45);
   var shin=avatar.Add(side+"_shin",thigh,B(isLeft?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg),B(isLeft?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot),3.5f,1,180,Quaternion.identity,0,140,0,0);
   avatar.Add(side+"_foot",shin,B(isLeft?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot),null,1.1f,2,100,Quaternion.Euler(0,isLeft?180:0,isLeft?0:180),-45,30,-15,30);
   var arm=avatar.Add(side+"_arm",spine,B(isLeft?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm),B(isLeft?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),2f,2,60,Quaternion.Euler(0,isLeft?180:0,isLeft?0:180),-40,150,-10,140);
   var forearm=avatar.Add(side+"_forearm",arm,B(isLeft?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),B(isLeft?HumanBodyBones.LeftHand:HumanBodyBones.RightHand),1.2f,1,40,Quaternion.Euler(0,180,0),0,150,0,0);
   avatar.Add(side+"_hand",forearm,B(isLeft?HumanBodyBones.LeftHand:HumanBodyBones.RightHand),null,.4f,0,0,Quaternion.identity,0,0,0,0);
  }
  var colliders=avatar.links.Values.SelectMany(l=>l.body.GetComponentsInChildren<Collider>()).Distinct().ToArray();
  for(int i=0;i<colliders.Length;i++)for(int j=i+1;j<colliders.Length;j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
  Physics.SyncTransforms();return avatar;
 }
 Link Add(string name,Link parent,Transform bone,Transform end,float mass,int dofs,float torque,Quaternion anchor,float min,float max,float min2,float max2){
  if(bone==null)throw new InvalidOperationException("Missing humanoid bone for "+name);
  var obj=new GameObject(name);obj.transform.position=bone.position;obj.transform.rotation=Quaternion.identity;
  if(parent!=null)obj.transform.SetParent(parent.body.transform,true);
  var body=obj.AddComponent<ArticulationBody>();body.mass=mass;body.useGravity=true;body.linearDamping=.05f;body.angularDamping=.05f;body.sleepThreshold=0;body.solverIterations=24;body.solverVelocityIterations=12;
  body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
  body.anchorPosition=Vector3.zero;body.anchorRotation=anchor;
  if(parent!=null){body.matchAnchors=false;body.parentAnchorPosition=parent.body.transform.InverseTransformPoint(obj.transform.position);body.parentAnchorRotation=anchor;
   body.jointType=dofs==0?ArticulationJointType.FixedJoint:dofs==1?ArticulationJointType.RevoluteJoint:ArticulationJointType.SphericalJoint;
   body.twistLock=ArticulationDofLock.LimitedMotion;body.swingYLock=name=="head"?ArticulationDofLock.LimitedMotion:ArticulationDofLock.LockedMotion;
   body.swingZLock=dofs==2&&name!="head"?ArticulationDofLock.LimitedMotion:ArticulationDofLock.LockedMotion;
   if(dofs>0){body.xDrive=Drive(min,max,torque);if(name=="head")body.yDrive=Drive(min2,max2,torque);else if(dofs==2)body.zDrive=Drive(min2,max2,torque);}
  }
  var node=new Link{name=name,body=body,bone=bone,bindRotation=bone.rotation,torqueLimit=torque,dofs=dofs};
  var shape=new GameObject("Collision surface");shape.transform.SetParent(obj.transform,false);
  if(name=="pelvis"||name.EndsWith("foot")||name.EndsWith("hand")){
   var box=shape.AddComponent<BoxCollider>();box.size=name=="pelvis"?new Vector3(.28f,.20f,.20f):name.EndsWith("foot")?new Vector3(.11f,.08f,.25f):new Vector3(.085f,.12f,.05f);
   if(name.EndsWith("foot"))box.center=new Vector3(0,-.035f,.07f);
  }else{
   var direction=end!=null?end.position-bone.position:Vector3.up*.20f;
   shape.transform.localPosition=direction*.5f;shape.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction.normalized);
   var capsule=shape.AddComponent<CapsuleCollider>();capsule.radius=name=="chest"?.13f:name=="head"?.10f:name.EndsWith("thigh")?.065f:.045f;capsule.height=Mathf.Max(direction.magnitude,capsule.radius*2);capsule.direction=1;
  }
  var friction=new PhysicsMaterial("Skin contact"){dynamicFriction=.65f,staticFriction=.8f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average};
  shape.GetComponent<Collider>().sharedMaterial=friction;
  node.sensor=obj.AddComponent<ContactSensor>();node.sensor.avatar=this;node.sensor.region=name;
  totalMass+=mass;links.Add(name,node);return node;
 }
 // PhysX integrates spring/damper drives with the joint constraint solve.
 // Explicit -damping*velocity torque was unstable for low-inertia forearms.
 static ArticulationDrive Drive(float lower,float upper,float limit)=>new ArticulationDrive{lowerLimit=lower,upperLimit=upper,stiffness=8,damping=2.5f,forceLimit=limit,target=0};
 public float Angle(string name,int index=0){var state=links[name].body.jointPosition;return state.dofCount>index?state[index]:0;}
 public float Speed(string name,int index=0){var state=links[name].body.jointVelocity;return state.dofCount>index?state[index]:0;}
 float Load(string name)=>links[name].sensor.force;
 public void BeginStep(){foreach(var l in links.Values)l.sensor.BeginStep();}
 public void SynchronizeSkin(){foreach(var l in links.Values.OrderBy(l=>l.bone.GetComponentsInParent<Transform>().Length)){l.bone.position=l.body.transform.position;l.bone.rotation=l.body.transform.rotation*l.bindRotation;}}
 public void Torques(string name,float first,float second=0,float posturalStiffness=0){var l=links[name];int n=l.body.dofCount;if(n==0)return;
  var drive=l.body.xDrive;drive.stiffness=8+Mathf.Clamp(posturalStiffness,0,150);l.body.xDrive=drive;
  first=Mathf.Clamp(first,-l.torqueLimit,l.torqueLimit);second=Mathf.Clamp(second,-l.torqueLimit,l.torqueLimit);
  l.body.jointForce=n==1?new ArticulationReducedSpace(first):new ArticulationReducedSpace(first,second);
 }
 public void ZeroActuation(){foreach(var l in links.Values)if(l.dofs>0)Torques(l.name,0);}
 public Vector3 BodyPoint(Vector3 world){var p=Quaternion.Inverse(pelvis.transform.rotation)*(world-pelvis.transform.position);p.y+=initialPelvisHeight;return p;}
 public Vector3 BodyVector(Vector3 world)=>Quaternion.Inverse(pelvis.transform.rotation)*world;
 public Vector3 CenterOfMass(){Vector3 total=Vector3.zero;foreach(var l in links.Values)total+=l.body.worldCenterOfMass*l.body.mass;return total/totalMass;}
 public bool IsFinite(){foreach(var l in links.Values){var p=l.body.transform.position;if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))return false;}return true;}
}
public sealed class ContactSensor:MonoBehaviour {
 public ArticulatedAvatar avatar;public string region;public float force,impulse,penetration,slip,duration;public Vector3 point,normal;public readonly List<Vector3> supportPoints=new List<Vector3>();
 bool wasTouching;public void BeginStep(){wasTouching=force>0;force=impulse=penetration=slip=0;supportPoints.Clear();}
 void OnCollisionEnter(Collision c)=>Measure(c);void OnCollisionStay(Collision c)=>Measure(c);
 void Measure(Collision c){if(c.contactCount==0)return;var contact=c.GetContact(0);float dt=WorldController.StepSeconds;for(int i=0;i<c.contactCount;i++){var cp=c.GetContact(i);if(cp.normal.y>.5f)supportPoints.Add(cp.point);}
  force+=c.impulse.magnitude/dt;impulse+=c.impulse.magnitude;point=contact.point;normal=contact.normal;penetration=Mathf.Max(penetration,-contact.separation*1000);
  slip=Vector3.ProjectOnPlane(c.relativeVelocity,contact.normal).magnitude;duration=wasTouching?duration+dt:dt;
 }
}
}
