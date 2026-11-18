#nullable disable
using System;

namespace Nre.UnityWorld {
// Isolated native experiment wire format; linked verbatim by its .NET driver.
// These records do not select a production body, network connection or controller.
public static class MuscleHingeWire {
 public const int Version=2;
 public const float StepSeconds=.000125f;
 public static double StepMs=>(double)StepSeconds*1000;
 // Exact powers of two preserve common physical endpoints across the isolated
 // refinement matrix. The default remains the previously qualified resolution.
 public static float StepSecondsAt(int resolution){
  Require(resolution>=-1&&resolution<=3,"Unsupported isolated physical resolution.");
  return resolution==-1?StepSeconds*2:StepSeconds/(1<<resolution);
 }
 public static double StepMsAt(int resolution)=>(double)StepSecondsAt(resolution)*1000;
 // JSON decimal parsers need not agree on the last binary bit. Mechanical
 // scalars carry canonical IEEE-754 bits; no tolerance, decimal reparsing or
 // numerical rounding is used at the wire boundary. Traces retain doubles.
 public static string Encode(double value){Require(Finite(value),"Nonfinite wire scalar.");return BitConverter.DoubleToInt64Bits(value).ToString("X16",System.Globalization.CultureInfo.InvariantCulture);}
 public static double Decode(string bits){
  Require(bits!=null&&bits.Length==16&&System.Linq.Enumerable.All(bits,c=>(c>='0'&&c<='9')||(c>='A'&&c<='F')),"Malformed wire scalar bits.");
  double value=BitConverter.Int64BitsToDouble(unchecked((long)Convert.ToUInt64(bits,16)));
  Require(Finite(value),"Nonfinite wire scalar.");return value;
 }
 [Serializable] public struct Real {
  public string bits;
  public static implicit operator Real(double value)=>new Real{bits=Encode(value)};
  public static implicit operator double(Real value)=>Decode(value.bits);
  public static bool operator ==(Real a,Real b)=>a.bits==b.bits;
  public static bool operator !=(Real a,Real b)=>!(a==b);
  public static bool operator ==(Real a,double b)=>a.bits==Encode(b);
  public static bool operator !=(Real a,double b)=>!(a==b);
  public static bool operator ==(double a,Real b)=>b==a;
  public static bool operator !=(double a,Real b)=>!(b==a);
  public override bool Equals(object value)=>value is Real other&&this==other;
  public override int GetHashCode()=>(bits??"").GetHashCode();
 }
 [Serializable] public sealed class Tension {public string muscleId;public Real axialNewtons;}
 [Serializable] public sealed class Path {public string muscleId,pathFingerprint;public Real lengthMeters,velocityMetersPerSecond;}
 [Serializable] public sealed class Request {
  public int version;public string sessionId,operation,bodyFingerprint,plantFingerprint,stateHash,effortId;
  public long sequence,expectedStep;public Real startTimeMs,endTimeMs;public Tension[] tensions;
 }
 [Serializable] public sealed class Reply {
  public int version;public string sessionId,operation,unity,bodyFingerprint,plantFingerprint,geometryJson,stateJson,stateHash,lastAppliedEffortId;
  public long sequence,physicsStep;public Real physicsStepMs,physicalTimeMs;
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
  Require(current!=null&&(current.physicsStepMs==StepMsAt(-1)||current.physicsStepMs==StepMsAt(0)||current.physicsStepMs==StepMsAt(1)||
   current.physicsStepMs==StepMsAt(2)||current.physicsStepMs==StepMsAt(3)),
   "Unsupported isolated physical clock.");
  Require(r!=null&&r.version==Version&&r.sessionId==current.sessionId&&r.sequence==checked(current.sequence+1),"Command identity/sequence differs.");
  Require(r.bodyFingerprint==current.bodyFingerprint&&r.expectedStep==current.physicsStep&&r.stateHash==current.stateHash,
   "Command does not name the current body endpoint.");
  Require(r.startTimeMs==current.physicalTimeMs&&Finite(r.startTimeMs),"Command starts at the wrong physical time.");
  if(r.operation=="bind") {
   Require(current.physicsStep==0&&string.IsNullOrEmpty(current.plantFingerprint)&&Hash(r.plantFingerprint),"Plant can bind exactly once before movement.");
  } else Require(Hash(current.plantFingerprint)&&r.plantFingerprint==current.plantFingerprint,"Plant binding differs.");
  if(r.operation=="step") {
   Require(GuidN(r.effortId)&&r.effortId!=current.lastAppliedEffortId&&r.endTimeMs==(current.physicsStep+1)*(double)current.physicsStepMs,
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
