using System;

namespace Nre.UnityWorld {
// Isolated native experiment wire format; linked verbatim by its .NET driver.
// These records do not select a production body, network connection or controller.
public static class MuscleHingeWire {
 public const int Version=1;
 public const float StepSeconds=.000125f;
 public static double StepMs=>(double)StepSeconds*1000;
 [Serializable] public sealed class Tension {public string muscleId;public double axialNewtons;}
 [Serializable] public sealed class Path {public string muscleId,pathFingerprint;public double lengthMeters,velocityMetersPerSecond;}
 [Serializable] public sealed class Request {
  public int version;public string sessionId,operation,bodyFingerprint,plantFingerprint,stateHash,effortId;
  public long sequence,expectedStep;public double startTimeMs,endTimeMs;public Tension[] tensions;
 }
 [Serializable] public sealed class Reply {
  public int version;public string sessionId,operation,unity,bodyFingerprint,plantFingerprint,geometryJson,stateJson,stateHash,lastAppliedEffortId;
  public long sequence,physicsStep;public double physicsStepMs,physicalTimeMs;
  public bool isolated,paused;public Path[] paths;
 }
 [Serializable] public sealed class Failure {
  public string error,sessionId;public long sequence,physicsStep;public bool physicsAttempted;
  public Reply observed;
 }
 public static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
 public static bool GuidN(string x)=>Guid.TryParseExact(x,"N",out var id)&&id!=Guid.Empty;
 public static bool Hash(string x)=>x!=null&&x.Length==64&&System.Linq.Enumerable.All(x,c=>(c>='0'&&c<='9')||(c>='A'&&c<='F'));
 public static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 public static void Validate(Request r,Reply current) {
  Require(r!=null&&r.version==Version&&r.sessionId==current.sessionId&&r.sequence==checked(current.sequence+1),"Command identity/sequence differs.");
  Require(r.bodyFingerprint==current.bodyFingerprint&&r.expectedStep==current.physicsStep&&r.stateHash==current.stateHash,
   "Command does not name the current body endpoint.");
  Require(r.startTimeMs==current.physicalTimeMs&&Finite(r.startTimeMs),"Command starts at the wrong physical time.");
  if(r.operation=="bind") {
   Require(current.physicsStep==0&&string.IsNullOrEmpty(current.plantFingerprint)&&Hash(r.plantFingerprint),"Plant can bind exactly once before movement.");
  } else Require(Hash(current.plantFingerprint)&&r.plantFingerprint==current.plantFingerprint,"Plant binding differs.");
  if(r.operation=="step") {
   Require(GuidN(r.effortId)&&r.effortId!=current.lastAppliedEffortId&&r.endTimeMs==(current.physicsStep+1)*StepMs,
    "Effort identity or physical endpoint differs.");
   Require(r.tensions!=null&&r.tensions.Length==2&&r.tensions[0]!=null&&r.tensions[1]!=null&&
    r.tensions[0].muscleId=="m0"&&r.tensions[1].muscleId=="m1"&&
    System.Linq.Enumerable.All(r.tensions,t=>Finite(t.axialNewtons)&&t.axialNewtons>=0&&t.axialNewtons<=float.MaxValue),
    "Require exactly the ordered finite nonnegative muscle tensions.");
  } else {
   Require(r.operation=="bind"||r.operation=="inspect"||r.operation=="stop","Unknown operation.");
   Require(string.IsNullOrEmpty(r.effortId)&&r.tensions!=null&&r.tensions.Length==0&&r.endTimeMs==r.startTimeMs,
    "Nonmoving operation must not carry force or time advancement.");
  }
 }
}
}
