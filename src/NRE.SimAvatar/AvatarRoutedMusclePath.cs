using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NRE.SimAvatar
{
    internal sealed class MuscleCylinderSpan
    {
        public int StartAnchorIndex { get; }
        public AvatarCylinderMusclePath Path { get; }
        public MuscleCylinderSpan(int startAnchorIndex,AvatarCylinderMusclePath path)
        {if(startAnchorIndex<0||path==null)throw new ArgumentException("Declare a cylinder span.");StartAnchorIndex=startAnchorIndex;Path=path;}
    }

    internal sealed class MuscleCylinderObservation
    {
        public int StartAnchorIndex { get; }
        public MuscleCylinderFrame Frame { get; }
        public double OriginAzimuth { get; }
        public double InsertionAzimuth { get; }
        public MuscleCylinderObservation(int startAnchorIndex,MuscleCylinderFrame frame,double originAzimuth,double insertionAzimuth)
        {StartAnchorIndex=startAnchorIndex;Frame=frame;OriginAzimuth=originAzimuth;InsertionAzimuth=insertionAzimuth;}
    }

    // Declared virtual body, never an unowned extra force sink. This massless
    // reduction does not import the source patellar mass or native knee motion.
    internal sealed class PatellarMuscleConstraint
    {
        public string VirtualBodyId { get; }
        public string ParentBodyId { get; }
        public string ChildBodyId { get; }
        public string MechanismFingerprint { get; }
        public PatellarMuscleConstraint(string virtualBodyId,string parentBodyId,string childBodyId,string mechanismFingerprint)
        {
            if(new[]{virtualBodyId,parentBodyId,childBodyId}.Any(v=>!AvatarMusclePath.ValidKey(v))||
                new[]{virtualBodyId,parentBodyId,childBodyId}.Distinct(StringComparer.Ordinal).Count()!=3||
                mechanismFingerprint==null||mechanismFingerprint.Length!=64||mechanismFingerprint.Any(c=>!Uri.IsHexDigit(c)))
                throw new ArgumentException("Declare distinct virtual/parent/child bodies and the original mechanism identity.");
            VirtualBodyId=virtualBodyId;ParentBodyId=parentBodyId;ChildBodyId=childBodyId;MechanismFingerprint=mechanismFingerprint.ToUpperInvariant();
        }
    }

    // The native owner still has to authenticate this world-space mapping and
    // completed physical sample. No coordinate fit, sign choice or clock here.
    internal sealed class PatellarMuscleObservation
    {
        public PatellarMuscleConstraint Constraint { get; }
        public MusclePathPoint Point { get; }
        public MusclePathVector JacobianMetersPerRadian { get; }
        public MusclePathVector ParentPointVelocity { get; }
        public MusclePathVector ParentAngularVelocity { get; }
        public MusclePathVector ChildAngularVelocity { get; }
        public MusclePathVector PositiveCoordinateAxis { get; }
        public PatellarMuscleObservation(PatellarMuscleConstraint constraint,string attachmentId,string mechanismFingerprint,
            MusclePathVector worldPoint,MusclePathVector worldJacobian,MusclePathVector parentPointVelocity,
            MusclePathVector parentAngularVelocity,MusclePathVector childAngularVelocity,MusclePathVector positiveCoordinateAxis)
        {
            if(constraint==null||!AvatarMusclePath.ValidKey(attachmentId)||mechanismFingerprint!=constraint.MechanismFingerprint)
                throw new ArgumentException("Require the original constrained mechanism and attachment.");
            Constraint=constraint;JacobianMetersPerRadian=worldJacobian;ParentPointVelocity=parentPointVelocity;
            ParentAngularVelocity=parentAngularVelocity;ChildAngularVelocity=childAngularVelocity;PositiveCoordinateAxis=positiveCoordinateAxis;
            var motion=new PatellarForceTransfer(worldPoint,default(MusclePathVector),worldJacobian,parentPointVelocity,
                parentAngularVelocity,childAngularVelocity,positiveCoordinateAxis);
            Point=new MusclePathPoint(attachmentId,constraint.VirtualBodyId,worldPoint,motion.MovingPointVelocityMetersPerSecond);
        }
        internal PatellarForceTransfer Transfer(MusclePathVector force)=>new PatellarForceTransfer(Point.PositionMeters,force,JacobianMetersPerRadian,
            ParentPointVelocity,ParentAngularVelocity,ChildAngularVelocity,PositiveCoordinateAxis);
    }

    internal sealed class MuscleBodyTorque
    {
        public string BodyId { get; }
        public MusclePathVector TorqueNewtonMeters { get; }
        public MusclePathVector AngularVelocityRadiansPerSecond { get; }
        internal MuscleBodyTorque(string id,MusclePathVector torque,MusclePathVector velocity)
        {BodyId=id;TorqueNewtonMeters=torque;AngularVelocityRadiansPerSecond=velocity;}
    }

    internal sealed class RoutedMuscleLoads
    {
        public string PathFingerprint { get; }
        public double AxialTensionNewtons { get; }
        public IReadOnlyList<MusclePathPointForce> PointForces { get; }
        public IReadOnlyList<MuscleBodyTorque> ConstraintTorques { get; }
        public double PowerOnBodiesWatts { get; }
        public double PowerFromPathShorteningWatts { get; }
        public double PowerResidualWatts => PowerOnBodiesWatts-PowerFromPathShorteningWatts;
        internal RoutedMuscleLoads(string fingerprint,double tension,MusclePathPointForce[] forces,MuscleBodyTorque[] torques,double expectedPower)
        {
            PathFingerprint=fingerprint;AxialTensionNewtons=tension;PointForces=Array.AsReadOnly(forces);ConstraintTorques=Array.AsReadOnly(torques);
            PowerFromPathShorteningWatts=expectedPower;
            PowerOnBodiesWatts=forces.Sum(f=>MusclePathVector.Dot(f.Point.VelocityMetersPerSecond,f.ForceNewtons))+
                torques.Sum(t=>MusclePathVector.Dot(t.AngularVelocityRadiansPerSecond,t.TorqueNewtonMeters));
            if(!MusclePathVector.Finite(PowerOnBodiesWatts)||!MusclePathVector.Finite(expectedPower)||!MusclePathVector.Finite(PowerResidualWatts))
                throw new InvalidOperationException("Nonfinite routed body work.");
        }
    }

    internal sealed class RoutedMusclePathSnapshot
    {
        private readonly MusclePathVector[] unitForces;
        private readonly IReadOnlyDictionary<string,PatellarMuscleObservation> virtualPoints;
        public string PathFingerprint { get; }
        public IReadOnlyList<MusclePathPoint> Points { get; }
        public double MusculotendonLengthMeters { get; }
        public double MusculotendonVelocityMetersPerSecond { get; }
        internal RoutedMusclePathSnapshot(string fingerprint,MusclePathPoint[] points,MusclePathVector[] forces,double length,double velocity,
            Dictionary<string,PatellarMuscleObservation> constrained)
        {
            PathFingerprint=fingerprint;Points=Array.AsReadOnly(points);unitForces=forces;virtualPoints=constrained;
            MusculotendonLengthMeters=length;MusculotendonVelocityMetersPerSecond=velocity;
            if(!MusclePathVector.Finite(length)||length<=0||!MusclePathVector.Finite(velocity)||forces.Any(f=>!f.IsFinite))
                throw new InvalidOperationException("Nonfinite routed path.");
        }
        public RoutedMuscleLoads ApplyTension(double tension)
        {
            if(!MusclePathVector.Finite(tension)||tension<0)throw new ArgumentException("Finite nonnegative tension required.");
            var forces=new List<MusclePathPointForce>();var torques=new List<MuscleBodyTorque>();
            for(int i=0;i<Points.Count;i++)
            {
                var p=Points[i];var f=unitForces[i]*tension;if(!f.IsFinite)throw new InvalidOperationException("Unrepresentable routed force.");
                PatellarMuscleObservation binding;
                if(!virtualPoints.TryGetValue(p.AttachmentId,out binding!)){forces.Add(new MusclePathPointForce(p,f));continue;}
                var t=binding.Transfer(f);var c=binding.Constraint;
                forces.Add(new MusclePathPointForce(new MusclePathPoint(p.AttachmentId,c.ParentBodyId,p.PositionMeters,binding.ParentPointVelocity),t.ParentPointForceNewtons));
                torques.Add(new MuscleBodyTorque(c.ChildBodyId,t.ChildTorqueNewtonMeters,binding.ChildAngularVelocity));
                torques.Add(new MuscleBodyTorque(c.ParentBodyId,t.ParentCounterTorqueNewtonMeters,binding.ParentAngularVelocity));
            }
            return new RoutedMuscleLoads(PathFingerprint,tension,forces.ToArray(),torques.ToArray(),-tension*MusculotendonVelocityMetersPerSecond);
        }
    }

    // Ordered source attachments with explicitly selected adjacent cylinder
    // spans. Does not infer OpenSim's hybrid wrap range (-1,-1), drop via points,
    // choose a winding branch, or promote this candidate to a native body.
    internal sealed class AvatarRoutedMusclePath
    {
        public const string Version="ordered-cylinder-patellar-muscle-path-v1";
        private const string GuidePrefix="route-guide:";
        private readonly AvatarMusclePath[] straight;
        private readonly Dictionary<int,AvatarCylinderMusclePath> cylinders;
        private readonly Dictionary<string,PatellarMuscleConstraint> constraints;
        public string Fingerprint { get; }
        public string BodyGeometryFingerprint { get; }
        public IReadOnlyList<MusclePathAnchor> Anchors { get; }
        public AvatarRoutedMusclePath(string muscleId,string geometry,string provenance,IEnumerable<MusclePathAnchor> anchors,
            IEnumerable<MuscleCylinderSpan> wrappedSpans,IEnumerable<PatellarMuscleConstraint> patellarConstraints)
        {
            var basePath=new AvatarMusclePath(muscleId,geometry,provenance,anchors);Anchors=basePath.Anchors;BodyGeometryFingerprint=geometry;
            if(Anchors.Any(a=>a.AttachmentId.StartsWith(GuidePrefix,StringComparison.Ordinal))||wrappedSpans==null||patellarConstraints==null)
                throw new ArgumentException("Declare reserved guide identities, cylinder spans and constrained bodies explicitly.");
            var spans=wrappedSpans.ToArray();var rules=patellarConstraints.ToArray();
            if(spans.Any(s=>s==null||s.StartAnchorIndex>=Anchors.Count-1)||spans.Select(s=>s.StartAnchorIndex).Distinct().Count()!=spans.Length||
                rules.Any(r=>r==null)||rules.Select(r=>r.VirtualBodyId).Distinct(StringComparer.Ordinal).Count()!=rules.Length)
                throw new ArgumentException("Distinct in-range spans and constrained body declarations required.");
            constraints=rules.ToDictionary(r=>r.VirtualBodyId,StringComparer.Ordinal);
            if(rules.Any(r=>!Anchors.Any(a=>a.BodyId==r.VirtualBodyId)||constraints.ContainsKey(r.ParentBodyId)||constraints.ContainsKey(r.ChildBodyId))||
                spans.Any(s=>constraints.ContainsKey(s.Path.GuideBodyId)))throw new ArgumentException("A constrained point requires physical parents; guides cannot be virtual bodies.");
            cylinders=spans.ToDictionary(s=>s.StartAnchorIndex,s=>s.Path);straight=new AvatarMusclePath[Anchors.Count-1];
            for(int i=0;i<straight.Length;i++)
            {
                AvatarCylinderMusclePath c;
                if(cylinders.TryGetValue(i,out c!))
                {if(c.BodyGeometryFingerprint!=geometry||!Same(c.Origin,Anchors[i])||!Same(c.Insertion,Anchors[i+1]))throw new ArgumentException("Cylinder endpoints differ from the ordered source anchors.");}
                else straight[i]=new AvatarMusclePath(muscleId,geometry,provenance,new[]{Anchors[i],Anchors[i+1]});
            }
            using(var bytes=new MemoryStream())
            {
                using(var w=new BinaryWriter(bytes,Encoding.UTF8,true))
                {
                    w.Write(Version);w.Write(basePath.Fingerprint);w.Write(spans.Length);
                    foreach(var s in spans.OrderBy(v=>v.StartAnchorIndex)){w.Write(s.StartAnchorIndex);w.Write(s.Path.Fingerprint);}
                    w.Write(rules.Length);foreach(var r in rules.OrderBy(v=>v.VirtualBodyId,StringComparer.Ordinal))
                    {w.Write(r.VirtualBodyId);w.Write(r.ParentBodyId);w.Write(r.ChildBodyId);w.Write(r.MechanismFingerprint);}
                }
                using(var sha=SHA256.Create())Fingerprint=BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","");
            }
        }
        private static bool Same(MusclePathAnchor a,MusclePathAnchor b)=>a.AttachmentId==b.AttachmentId&&a.BodyId==b.BodyId&&a.LocalPositionMeters.Equals(b.LocalPositionMeters);
        private static bool Same(PatellarMuscleConstraint a,PatellarMuscleConstraint b)=>a.VirtualBodyId==b.VirtualBodyId&&a.ParentBodyId==b.ParentBodyId&&a.ChildBodyId==b.ChildBodyId&&a.MechanismFingerprint==b.MechanismFingerprint;
        public RoutedMusclePathSnapshot Measure(string geometry,IEnumerable<MusclePathPoint> physicalPoints,
            IEnumerable<MuscleCylinderObservation> guides,IEnumerable<PatellarMuscleObservation> patellarPoints)
        {
            if(geometry!=BodyGeometryFingerprint||physicalPoints==null||guides==null||patellarPoints==null)throw new ArgumentException("Declare this geometry and its complete observations.");
            var physical=physicalPoints.ToArray();var virtualInput=patellarPoints.ToArray();var guideInput=guides.ToArray();
            if(physical.Any(p=>p==null||constraints.ContainsKey(p.BodyId))||virtualInput.Any(p=>p==null||!constraints.ContainsKey(p.Constraint.VirtualBodyId)||!Same(constraints[p.Constraint.VirtualBodyId],p.Constraint))||
                guideInput.Any(g=>g==null||!cylinders.ContainsKey(g.StartAnchorIndex))||guideInput.Select(g=>g.StartAnchorIndex).Distinct().Count()!=cylinders.Count||guideInput.Length!=cylinders.Count)
                throw new ArgumentException("Missing, duplicate or unowned physical/virtual/guide observation.");
            var all=physical.Concat(virtualInput.Select(v=>v.Point)).ToArray();
            if(all.Length!=Anchors.Count||all.Any(p=>!p.PositionMeters.IsFinite||!p.VelocityMetersPerSecond.IsFinite)||all.Select(p=>p.AttachmentId).Distinct(StringComparer.Ordinal).Count()!=all.Length)
                throw new ArgumentException("Exactly one finite observation per original anchor required.");
            var byId=all.ToDictionary(p=>p.AttachmentId,StringComparer.Ordinal);var points=new List<MusclePathPoint>();
            foreach(var a in Anchors)
            {MusclePathPoint p;if(!byId.TryGetValue(a.AttachmentId,out p!)||p.BodyId!=a.BodyId)throw new ArgumentException("Original attachment identity differs.");points.Add(p);}
            var forces=Enumerable.Repeat(default(MusclePathVector),Anchors.Count).ToList();double length=0,velocity=0;
            var bySpan=guideInput.ToDictionary(g=>g.StartAnchorIndex);
            for(int i=0;i<straight.Length;i++)
            {
                MusclePathTension loads;AvatarCylinderMusclePath c;
                if(cylinders.TryGetValue(i,out c!))
                {
                    var g=bySpan[i];var s=c.Measure(geometry,g.Frame,points[i],points[i+1],g.OriginAzimuth,g.InsertionAzimuth);
                    length+=s.MusculotendonLengthMeters;velocity+=s.MusculotendonVelocityMetersPerSecond;loads=s.ApplyTension(1);
                }
                else{var s=straight[i].Measure(geometry,new[]{points[i],points[i+1]});length+=s.MusculotendonLengthMeters;velocity+=s.MusculotendonVelocityMetersPerSecond;loads=s.ApplyTension(1);}
                forces[i]=forces[i]+loads.PointForces[0].ForceNewtons;forces[i+1]=forces[i+1]+loads.PointForces[loads.PointForces.Count-1].ForceNewtons;
                for(int j=1;j<loads.PointForces.Count-1;j++)
                {
                    var f=loads.PointForces[j];var p=f.Point;
                    points.Add(new MusclePathPoint(GuidePrefix+i+":"+j,p.BodyId,p.PositionMeters,p.VelocityMetersPerSecond));forces.Add(f.ForceNewtons);
                }
            }
            return new RoutedMusclePathSnapshot(Fingerprint,points.ToArray(),forces.ToArray(),length,velocity,virtualInput.ToDictionary(v=>v.Point.AttachmentId,StringComparer.Ordinal));
        }
    }
}
