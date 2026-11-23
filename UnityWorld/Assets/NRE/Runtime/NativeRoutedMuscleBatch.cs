using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Prepares the complete load set without queuing a force. The external
    // physical owner calls Apply exactly once inside its exclusive step.
    internal sealed class NativeRoutedMuscleBatch
    {
        [Serializable] internal sealed class Point
        {
            public string muscleId,pathFingerprint,attachmentId,bodyId;
            public string[] requestedPositionBits,requestedForceBits,appliedPositionBits,appliedForceBits,materialVelocityBits;
        }
        [Serializable] internal sealed class Torque
        {
            public string muscleId,pathFingerprint,bodyId;
            public int constraintLoadIndex;
            public string[] requestedTorqueBits,appliedTorqueBits,angularVelocityBits;
        }
        [Serializable] internal sealed class Evidence
        {
            public string version="native-routed-muscle-loads-v1",stateBefore,mechanicsBefore;
            public long physicsStep;
            public string requestedPowerBits,nativeBoundaryPowerBits;
            public Point[] points;
            public Torque[] torques;
        }
        [Serializable] sealed class BodyState
        {
            public string id;
            public int instance;
            public string[] position,rotation,center,velocity,angular,force,torque,jointPosition,jointVelocity;
        }
        [Serializable] sealed class State { public long step;public BodyState[] bodies; }
        sealed class ForceCommand { internal ArticulationBody body;internal Vector3 point,force; }
        sealed class TorqueCommand { internal ArticulationBody body;internal Vector3 torque; }
        readonly WorldController world;
        readonly ForceCommand[] points;
        readonly TorqueCommand[] torques;
        readonly Evidence evidence;
        readonly int thread;
        bool attempted;
        internal string Json=>JsonUtility.ToJson(evidence);
        internal int ApplicationCount=>points.Length+torques.Length;
        internal static string[] Bits(MusclePathVector v)=>new[]{MuscleHingeWire.Encode(v.X),MuscleHingeWire.Encode(v.Y),MuscleHingeWire.Encode(v.Z)};
        internal static string[] Bits(Vector3 v)=>Bits(new MusclePathVector(v.x,v.y,v.z));
        static string[] Bits(Quaternion q)=>new[]{MuscleHingeWire.Encode(q.x),MuscleHingeWire.Encode(q.y),MuscleHingeWire.Encode(q.z),MuscleHingeWire.Encode(q.w)};
        static string[] Bits(ArticulationReducedSpace q)=>Enumerable.Range(0,q.dofCount).Select(i=>MuscleHingeWire.Encode(q[i])).ToArray();
        static Vector3 F(MusclePathVector v)
        {
            var f=new Vector3((float)v.X,(float)v.Y,(float)v.Z);
            if(!v.IsFinite||!new MusclePathVector(f.x,f.y,f.z).IsFinite)throw new ArgumentException("Load is not representable at the native float boundary.");
            return f;
        }
        static void Healthy(WorldController w)
        {
            if(w==null||!w.IsolatedQualification||w.Connected||w.Running||!w.Avatar.DirectTendonForcesEnabled||!w.Avatar.IsFinite())
                throw new InvalidOperationException("Require a finite paused isolated body under direct muscle ownership.");
        }
        internal static string CaptureState(WorldController w)
        {
            Healthy(w);float dt=w.Avatar.PhysicalStepSeconds;
            return JsonUtility.ToJson(new State{step=w.physicsSteps,bodies=w.Avatar.links.Values.OrderBy(l=>l.name,StringComparer.Ordinal).Select(l=>new BodyState{
                id=l.name,instance=l.body.GetInstanceID(),position=Bits(l.body.transform.position),rotation=Bits(l.body.transform.rotation),center=Bits(l.body.worldCenterOfMass),
                velocity=Bits(l.body.linearVelocity),angular=Bits(l.body.angularVelocity),force=Bits(l.body.GetAccumulatedForce(dt)),torque=Bits(l.body.GetAccumulatedTorque(dt)),
                jointPosition=Bits(l.body.jointPosition),jointVelocity=Bits(l.body.jointVelocity)}).ToArray()});
        }
        internal NativeRoutedMuscleBatch(WorldController world,long expectedStep,string expectedState,IReadOnlyDictionary<string,RoutedMuscleLoads> loads)
        {
            Healthy(world);this.world=world;thread=Thread.CurrentThread.ManagedThreadId;
            if(world.physicsSteps!=expectedStep||CaptureState(world)!=expectedState||loads==null||loads.Count==0||loads.Any(p=>!AvatarMusclePath.ValidKey(p.Key)||p.Value==null))
                throw new ArgumentException("Require the exact completed body state and a complete declared load batch.");
            var physicalPoints=new List<ForceCommand>();var physicalTorques=new List<TorqueCommand>();var pointEvidence=new List<Point>();var torqueEvidence=new List<Torque>();
            double requestedPower=0,nativePower=0;
            foreach(var entry in loads.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var load=entry.Value;
                if(!MuscleHingeWire.Hash(load.PathFingerprint)||load.PointForces.Select(p=>p.Point.AttachmentId).Distinct(StringComparer.Ordinal).Count()!=load.PointForces.Count)
                    throw new ArgumentException("Require a valid path identity and distinct material attachments.");
                requestedPower+=load.PowerOnBodiesWatts;
                foreach(var p in load.PointForces)
                {
                    ArticulatedAvatar.Link link;
                    if(!world.Avatar.links.TryGetValue(p.Point.BodyId,out link)||!AvatarMusclePath.ValidKey(p.Point.AttachmentId))
                        throw new ArgumentException("A routed point load has no actual body/attachment owner.");
                    var position=F(p.Point.PositionMeters);var force=F(p.ForceNewtons);var velocity=link.body.GetPointVelocity(position);
                    physicalPoints.Add(new ForceCommand{body=link.body,point=position,force=force});
                    nativePower+=(double)force.x*velocity.x+(double)force.y*velocity.y+(double)force.z*velocity.z;
                    pointEvidence.Add(new Point{muscleId=entry.Key,pathFingerprint=load.PathFingerprint,attachmentId=p.Point.AttachmentId,bodyId=p.Point.BodyId,
                        requestedPositionBits=Bits(p.Point.PositionMeters),requestedForceBits=Bits(p.ForceNewtons),appliedPositionBits=Bits(position),appliedForceBits=Bits(force),materialVelocityBits=Bits(velocity)});
                }
                int index=0;
                foreach(var t in load.ConstraintTorques)
                {
                    ArticulatedAvatar.Link link;
                    if(!world.Avatar.links.TryGetValue(t.BodyId,out link))throw new ArgumentException("A constraint moment has no actual body owner.");
                    var torque=F(t.TorqueNewtonMeters);var velocity=link.body.angularVelocity;
                    physicalTorques.Add(new TorqueCommand{body=link.body,torque=torque});
                    nativePower+=(double)torque.x*velocity.x+(double)torque.y*velocity.y+(double)torque.z*velocity.z;
                    torqueEvidence.Add(new Torque{muscleId=entry.Key,pathFingerprint=load.PathFingerprint,bodyId=t.BodyId,constraintLoadIndex=index++,
                        requestedTorqueBits=Bits(t.TorqueNewtonMeters),appliedTorqueBits=Bits(torque),angularVelocityBits=Bits(velocity)});
                }
            }
            if(!MusclePathVector.Finite(requestedPower)||!MusclePathVector.Finite(nativePower))throw new ArgumentException("Nonfinite complete body power.");
            // No native AddForce/AddTorque has occurred, including if the last
            // declaration was invalid. Do not expose mutable command arrays.
            points=physicalPoints.ToArray();torques=physicalTorques.ToArray();
            evidence=new Evidence{physicsStep=expectedStep,stateBefore=expectedState,mechanicsBefore=ArticulatedMechanicsIdentity.Capture(world.Avatar),
                requestedPowerBits=MuscleHingeWire.Encode(requestedPower),nativeBoundaryPowerBits=MuscleHingeWire.Encode(nativePower),points=pointEvidence.ToArray(),torques=torqueEvidence.ToArray()};
        }
        internal void Apply(Action<string,int> observe)
        {
            if(Thread.CurrentThread.ManagedThreadId!=thread||attempted)throw new InvalidOperationException("A prepared native load batch has one main-thread attempt.");
            attempted=true;Healthy(world);
            if(CaptureState(world)!=evidence.stateBefore||ArticulatedMechanicsIdentity.Capture(world.Avatar)!=evidence.mechanicsBefore)
                throw new InvalidOperationException("Body, mechanics or accumulators changed after load preparation.");
            int count=0;
            foreach(var p in points){p.body.AddForceAtPosition(p.force,p.point,ForceMode.Force);observe?.Invoke("after-point",++count);}
            foreach(var t in torques){t.body.AddTorque(t.torque,ForceMode.Force);observe?.Invoke("after-constraint-torque",++count);}
            // An exception after any application is terminal for this batch;
            // replaying a partially applied native force could double its load.
        }
    }
}
