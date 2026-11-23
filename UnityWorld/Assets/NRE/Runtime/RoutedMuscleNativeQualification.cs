using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Full existing Avatar, analytic paths. This verifies native load transfer;
    // the geometry is not a calibrated human quadriceps or a standing policy.
    internal static class RoutedMuscleNativeQualification
    {
        const float Dt=.005f;
        const double Offset=1.9;
        static readonly string[] Cases={"quiet","left","right","both","both-cut","invalid-last","both-replay"};
        [Serializable] sealed class Report
        {
            public string version="native-routed-muscle-qualification-v1",unity,condition,error,mechanicsJson;
            public bool completed,isolated,paused,connected,neuronalSourceExecuted=false,anatomicalCalibrationQualified=false,avatarStandingQualified=false;
            public int steps,endpoints,lateRejections,replayRejections;
            public string stepSecondsBits;
            public FixtureDeclaration[] fixtures;
        }
        [Serializable] sealed class FixtureDeclaration
        {
            public string muscleId,parent,child,virtualBody,mechanismFingerprint,pathFingerprint,scaleBits,offsetBits;
            public string[] parentAnchorBits,childAnchorBits,originLocalBits,insertionLocalBits;
        }
        [Serializable] sealed class Point { public string id,bodyId;public string[] positionBits,velocityBits; }
        [Serializable] sealed class Path
        {
            public string muscleId,pathFingerprint,qBits,relativeSpeedBits,nativeJointSpeedBits,lengthBits,velocityBits,tensionBits;
            public string[] pivotBits,uBits,vBits,axisBits;
            public Point[] points;
        }
        [Serializable] sealed class Endpoint { public long step;public string stateJson,observationsJson; }
        [Serializable] sealed class Step
        {
            public int index;
            public long beforeStep,afterStep;
            public string beforeState,afterState,loadJson,forceTraceJson;
            public Path[] paths;
            public Path[] rejectedPaths;
            public bool lateRejectedWithoutMutation,replayRejectedWithoutMutation;
        }
        static string B(double v)=>MuscleHingeWire.Encode(v);
        static string[] Bits(MusclePathVector v)=>NativeRoutedMuscleBatch.Bits(v);
        static MusclePathVector D(Vector3 v)=>new MusclePathVector(v.x,v.y,v.z);
        static Vector3 F(MusclePathVector v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
        static MusclePathVector V(double x=0,double y=0,double z=0)=>new MusclePathVector(x,y,z);
        static void Require(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
        static void Write(string directory,string name,object value)
        {using(var f=new FileStream(System.IO.Path.Combine(directory,name),FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(f))w.Write(JsonUtility.ToJson(value,true));}
        // Reconstruct a unit rotation from the original float quaternion. Raw
        // native quaternions/positions remain in every endpoint record.
        static MusclePathVector Rotate(Quaternion raw,MusclePathVector p)
        {
            double norm=Math.Sqrt((double)raw.x*raw.x+(double)raw.y*raw.y+(double)raw.z*raw.z+(double)raw.w*raw.w);
            Require(MusclePathVector.Finite(norm)&&norm>0,"Finite native rotation required.");
            var q=V(raw.x/norm,raw.y/norm,raw.z/norm);double w=raw.w/norm;
            return p+MuscleCylinderFrame.Cross(q,MuscleCylinderFrame.Cross(q,p)+p*w)*2;
        }
        sealed class KneeFixture
        {
            internal readonly string Id;
            readonly string parentId,childId;
            readonly ArticulatedAvatar.Link child;
            readonly ArticulationBody parent;
            readonly AvatarPatellarPath patella;
            readonly PatellarMuscleConstraint constraint;
            readonly AvatarRoutedMusclePath path;
            readonly Vector3 originLocal,insertionLocal;
            readonly double scale;
            internal readonly FixtureDeclaration Declaration;
            internal KneeFixture(ArticulatedAvatar avatar,string side)
            {
                Id=side+"-analytic-knee";parentId=side+"_thigh";childId=side+"_shin";child=avatar.links[childId];parent=avatar.links[parentId].body;
                Require(child.body.transform.parent==parent.transform&&child.dofs==1&&child.body.dofCount==1&&child.coordinates.NativeIndex(0)==0,
                    "Require the actual one-axis knee and its physical parent.");
                Require(child.body.anchorRotation==Quaternion.identity&&child.body.parentAnchorRotation==Quaternion.identity,"Fixture declares the original knee anchor axes.");
                scale=MuscleProfiles.All.Single(p=>p.Joint==childId&&p.Axis==0).MomentArm/2;
                Require(scale>0&&scale<.1,"Use the existing knee profile's declared mechanical scale.");
                originLocal=child.body.parentAnchorPosition+new Vector3(0,(float)(3*scale),0);
                insertionLocal=child.body.anchorPosition+new Vector3(0,(float)(scale*(3.5*Math.Cos(Offset)+Math.Sin(Offset))),
                    (float)(scale*(3.5*Math.Sin(Offset)-Math.Cos(Offset))));
                var knots=new[]{-.1,2.5};var angle=new MuscleCoordinateSpline(knots,knots.Select(q=>q+Offset));var zero=new MuscleCoordinateSpline(knots,new[]{0.0,0.0});
                patella=new AvatarPatellarPath("knee",parentId,side+"-virtual-patella",new string('A',64),V(),0,angle,zero,zero);
                constraint=new PatellarMuscleConstraint(patella.PatellaBodyId,parentId,childId,patella.Fingerprint);
                var anchors=new[]{new MusclePathAnchor("origin",parentId,D(originLocal)),new MusclePathAnchor("patella",constraint.VirtualBodyId,V(3*scale)),
                    new MusclePathAnchor("insertion",childId,D(insertionLocal))};
                var cylinder=new AvatarCylinderMusclePath(Id,"analytic-existing-avatar-knee","explicit-analytic-fixture",anchors[0],anchors[1],parentId,
                    2*scale,.1,1,D(child.body.parentAnchorPosition),V(0,1),V(0,0,1));
                path=new AvatarRoutedMusclePath(Id,"analytic-existing-avatar-knee","explicit-analytic-fixture",anchors,new[]{new MuscleCylinderSpan(0,cylinder)},new[]{constraint});
                Declaration=new FixtureDeclaration{muscleId=Id,parent=parentId,child=childId,virtualBody=constraint.VirtualBodyId,
                    mechanismFingerprint=patella.Fingerprint,pathFingerprint=path.Fingerprint,scaleBits=B(scale),offsetBits=B(Offset),
                    parentAnchorBits=Bits(D(child.body.parentAnchorPosition)),childAnchorBits=Bits(D(child.body.anchorPosition)),originLocalBits=Bits(D(originLocal)),insertionLocalBits=Bits(D(insertionLocal))};
            }
            internal RoutedMuscleLoads Measure(double tension,out Path evidence)
            {
                double q=child.body.jointPosition[0];var pivot=D(parent.transform.TransformPoint(child.body.parentAnchorPosition));
                var u=Rotate(parent.transform.rotation,V(0,1));var v=Rotate(parent.transform.rotation,V(0,0,1));var axis=MuscleCylinderFrame.Cross(u,v);
                var p=patella.Measure(q,V(3*scale));var position=pivot+u*p.ParentLocalPositionMeters.X+v*p.ParentLocalPositionMeters.Y;
                var jacobian=u*p.DerivativeMetersPerRadian.X+v*p.DerivativeMetersPerRadian.Y;
                var binding=new PatellarMuscleObservation(constraint,"patella",patella.Fingerprint,position,jacobian,D(parent.GetPointVelocity(F(position))),
                    D(parent.angularVelocity),D(child.body.angularVelocity),axis);
                var origin=parent.transform.TransformPoint(originLocal);var insertion=child.body.transform.TransformPoint(insertionLocal);
                var physical=new[]{new MusclePathPoint("origin",parentId,D(origin),D(parent.GetPointVelocity(origin))),
                    new MusclePathPoint("insertion",childId,D(insertion),D(child.body.GetPointVelocity(insertion)))};
                var frame=new MuscleCylinderFrame(parentId,pivot,u,v,D(parent.GetPointVelocity(F(pivot))),D(parent.angularVelocity));
                var a=D(origin)-pivot;var b=position-pivot;double aa=Math.Atan2(MusclePathVector.Dot(a,v),MusclePathVector.Dot(a,u));
                double principal=Math.Atan2(MusclePathVector.Dot(b,v),MusclePathVector.Dot(b,u));double ab=principal+2*Math.PI*Math.Round((q+Offset-principal)/(2*Math.PI));
                var snapshot=path.Measure("analytic-existing-avatar-knee",physical,new[]{new MuscleCylinderObservation(0,frame,aa,ab)},new[]{binding});
                double rate=MusclePathVector.Dot(axis,D(child.body.angularVelocity)-D(parent.angularVelocity));
                evidence=new Path{muscleId=Id,pathFingerprint=path.Fingerprint,qBits=B(q),relativeSpeedBits=B(rate),nativeJointSpeedBits=B(child.body.jointVelocity[0]),
                    lengthBits=B(snapshot.MusculotendonLengthMeters),velocityBits=B(snapshot.MusculotendonVelocityMetersPerSecond),tensionBits=B(tension),
                    pivotBits=Bits(pivot),uBits=Bits(u),vBits=Bits(v),axisBits=Bits(axis),points=snapshot.Points.Select(x=>new Point{
                        id=x.AttachmentId,bodyId=x.BodyId,positionBits=Bits(x.PositionMeters),velocityBits=Bits(x.VelocityMetersPerSecond)}).ToArray()};
                return snapshot.ApplyTension(tension);
            }
        }
        static Endpoint Capture(WorldController world)
        {
            var body=world.Avatar;long step=world.physicsSteps;
            return new Endpoint{step=step,stateJson=NativeRoutedMuscleBatch.CaptureState(world),observationsJson=JsonUtility.ToJson(new ArticulatedTendonWire.Observations{
                physicsStep=step,bodyJson=JsonUtility.ToJson(BodyFrame.Capture(body,step)),
                contactsJson=body.links.Values.OrderBy(l=>l.name,StringComparer.Ordinal).Select(l=>JsonUtility.ToJson(ContactFrame.Capture(body,l,step))).ToArray(),
                skullJson=new[]{JsonUtility.ToJson(SkullMotionCapture.Capture(body,step,"centre",Vector3.zero)),
                    JsonUtility.ToJson(SkullMotionCapture.Capture(body,step,"left",new Vector3(-.0625f,0,0))),JsonUtility.ToJson(SkullMotionCapture.Capture(body,step,"right",new Vector3(.0625f,0,0)))}})};
        }
        internal static IEnumerator Run(WorldController world)
        {
            yield return null;
            var report=new Report{unity=Application.unityVersion,condition=WorldController.Argument("--native-routed-case"),stepSecondsBits=B(Dt)};
            try
            {
                Require(Cases.Contains(report.condition)&&world.IsolatedQualification&&!world.Running&&!world.Connected&&world.physicsSteps==0,"Fresh paused isolated routed case required.");
                var mechanics=TendonMechanicsDiagnostic.Prepare(world,"no-joint-friction");var owner=new object();world.ClaimMusclePathOwnership(owner,Dt);mechanics.Bound();
                report.isolated=world.IsolatedQualification;report.paused=!world.Running;report.connected=world.Connected;report.mechanicsJson=mechanics.Json();
                Write(world.outputDirectory,"endpoint-00.json",Capture(world));report.endpoints++;
                world.StepForMusclePaths(owner,()=>{});Write(world.outputDirectory,"endpoint-01.json",Capture(world));report.endpoints++;
                var fixtures=new[]{new KneeFixture(world.Avatar,"left"),new KneeFixture(world.Avatar,"right")};report.fixtures=fixtures.Select(f=>f.Declaration).ToArray();
                for(int i=0;i<7;i++)
                {
                    mechanics.CheckUnchanged();long before=world.physicsSteps;string state=NativeRoutedMuscleBatch.CaptureState(world);
                    var loads=new Dictionary<string,RoutedMuscleLoads>(StringComparer.Ordinal);var paths=new List<Path>();
                    foreach(var fixture in fixtures)
                    {
                        bool selected=report.condition=="both"||report.condition=="both-replay"||report.condition==(fixture.Id.StartsWith("left",StringComparison.Ordinal)?"left":"right");
                        double tension=selected&&i%2==0?40:0;Path path;loads.Add(fixture.Id,fixture.Measure(tension,out path));paths.Add(path);
                    }
                    var row=new Step{index=i,beforeStep=before,beforeState=state,paths=paths.ToArray()};
                    if(report.condition=="invalid-last")
                    {
                        // Use nonzero earlier loads: otherwise an eager partial
                        // application of zeros could make this check vacuous.
                        var bad=new Dictionary<string,RoutedMuscleLoads>();var rejectedPaths=new List<Path>();
                        foreach(var fixture in fixtures){Path rejectedPath;bad.Add(fixture.Id,fixture.Measure(40,out rejectedPath));rejectedPaths.Add(rejectedPath);}
                        row.rejectedPaths=rejectedPaths.ToArray();var last=bad.OrderBy(p=>p.Key,StringComparer.Ordinal).Last();var torques=last.Value.ConstraintTorques.ToArray();
                        torques[torques.Length-1]=new MuscleBodyTorque("absent-body",V(1),V());
                        bad[last.Key]=new RoutedMuscleLoads(last.Value.PathFingerprint,40,last.Value.PointForces.ToArray(),torques,last.Value.PowerFromPathShorteningWatts);
                        bool rejected=false;try{new NativeRoutedMuscleBatch(world,before,state,bad);}catch(ArgumentException){rejected=true;}
                        row.lateRejectedWithoutMutation=rejected&&NativeRoutedMuscleBatch.CaptureState(world)==state;
                        Require(row.lateRejectedWithoutMutation,"Invalid final constraint queued a partial native load.");report.lateRejections++;
                    }
                    var batch=new NativeRoutedMuscleBatch(world,before,state,loads);row.loadJson=batch.Json;
                    Write(world.outputDirectory,"prepared-"+i.ToString("D2")+".json",row);
                    var trace=new NativeTendonForceTrace(world.Avatar,Dt,before);
                    world.StepForMusclePaths(owner,()=>{
                        trace.Record("before-applications",0);batch.Apply(trace.Record);trace.Record("after-all-applications",batch.ApplicationCount);
                        if(report.condition=="both-replay")
                        {
                            string applied=NativeRoutedMuscleBatch.CaptureState(world);bool rejected=false;try{batch.Apply(trace.Record);}catch(InvalidOperationException){rejected=true;}
                            row.replayRejectedWithoutMutation=rejected&&NativeRoutedMuscleBatch.CaptureState(world)==applied;
                            Require(row.replayRejectedWithoutMutation,"Repeated native batch changed force accumulators.");report.replayRejections++;
                        }
                    });
                    trace.Record("after-physics",batch.ApplicationCount);row.forceTraceJson=trace.Json();row.afterStep=world.physicsSteps;row.afterState=NativeRoutedMuscleBatch.CaptureState(world);
                    Require(row.afterStep==before+1,"Routed load batch did not own one physical interval.");
                    Write(world.outputDirectory,"step-"+i.ToString("D2")+".json",row);Write(world.outputDirectory,"endpoint-"+(i+2).ToString("D2")+".json",Capture(world));report.endpoints++;
                }
                mechanics.CheckUnchanged();report.steps=(int)world.physicsSteps;Require(report.steps==8&&report.endpoints==9,"Incomplete native routed capture.");report.completed=true;
            }
            catch(Exception e){report.error=e.ToString();Debug.LogException(e);world.PauseTrial("Paused · routed native qualification failed");}
            Write(world.outputDirectory,"routed-native-result.json",report);Application.Quit(report.completed?0:1);
        }
    }
}
