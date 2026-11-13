using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld {
// Isolated diagnostic bridge. Files preserve every request/reply; no live
// service, receptor or neuronal input consumes this lane. Only the .NET probe
// requests steps. Wall waits do not drive physics or claim biological time.
public static class PhysicalSampleQualification {
 public const int ProtocolVersion=1;
 [Serializable] public sealed class Request {public int version;public string sessionId,operation;public long sequence,expectedStep;}
 [Serializable] public sealed class Reply {
  public int version=ProtocolVersion;public string sessionId,operation,unity,bodyJson;
  public string[] contactsJson;public long sequence,physicsStep;public double physicsStepMs;
  public bool paused,isolated,connected,finite,musclesEnabled;public float pelvisHeightMeters;
  public int links;public long bodyFramesAccepted,retinalFramesAccepted,contactFramesAccepted;
  public bool directStepRejected,wrongOwnerRejected;
 }
 [Serializable] sealed class Failure {public string error,sessionId;public long physicsStep,expectedSequence;}

 public static IEnumerator Run(WorldController world) {
  yield return null;
  string directory=Path.Combine(world.outputDirectory,"bridge");
  string session=WorldController.Argument("--physical-sample-qualify");
  object owner=new object();long sequence=1;bool failed=false,done=false;
  bool directRejected=false,wrongRejected=false;
  try {
   if(!Guid.TryParseExact(session,"N",out _)||Directory.Exists(directory))throw new InvalidOperationException("Fresh declared bridge session required.");
   if(!world.IsolatedQualification||world.Connected||world.Running||world.physicsSteps!=0||!world.Avatar.MusclesEnabled)
    throw new InvalidOperationException("Bridge requires a paused fresh muscular Avatar without DNNE.");
   Directory.CreateDirectory(directory);world.ClaimPhysicalSampleOwnership(owner);
   try {world.Step();}catch(InvalidOperationException){directRejected=true;}
   try {world.StepForPhysicalSample(new object());}catch(InvalidOperationException){wrongRejected=true;}
   if(!directRejected||!wrongRejected||world.physicsSteps!=0)throw new InvalidOperationException("Competing physical step was not rejected.");
   WriteNew(Path.Combine(directory,"hello.json"),JsonUtility.ToJson(Capture(world,session,0,"hello",directRejected,wrongRejected),true));
  }catch(Exception e){SaveFailure(world,directory,session,sequence,e);failed=true;}
  if(failed){Application.Quit(1);yield break;}
  float waitStarted=Time.realtimeSinceStartup;
  while(!done&&!failed){
   string requestPath=Path.Combine(directory,"request-"+sequence.ToString("D6")+".json");
   if(!File.Exists(requestPath)){
    if(Time.realtimeSinceStartup-waitStarted>180){SaveFailure(world,directory,session,sequence,new TimeoutException("No next bridge request; physics remains paused."));failed=true;break;}
    yield return null;continue;
   }
   try {
    if(sequence>1000)throw new InvalidOperationException("Diagnostic request bound exceeded.");
    var request=JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
    if(request==null||request.version!=ProtocolVersion||request.sessionId!=session||request.sequence!=sequence||request.expectedStep!=world.physicsSteps)
     throw new InvalidOperationException("Bridge identity, sequence or expected physical step differs.");
    if(request.operation=="step")world.StepForPhysicalSample(owner);
    else if(request.operation=="stop")done=true;
    else if(request.operation!="inspect")throw new InvalidOperationException("Unknown diagnostic bridge request.");
    var reply=Capture(world,session,sequence,request.operation,directRejected,wrongRejected);
    if(!reply.finite||reply.connected||!reply.paused||!reply.isolated)throw new InvalidOperationException("Physical state or isolation was lost.");
    WriteNew(Path.Combine(directory,"reply-"+sequence.ToString("D6")+".json"),JsonUtility.ToJson(reply,true));
    sequence++;waitStarted=Time.realtimeSinceStartup;
   }catch(Exception e){SaveFailure(world,directory,session,sequence,e);failed=true;}
   yield return null;
  }
  Application.Quit(failed?1:0);
 }

 static Reply Capture(WorldController world,string session,long sequence,string operation,bool directRejected,bool wrongRejected){
  var body=world.Avatar;
  return new Reply{sessionId=session,sequence=sequence,operation=operation,unity=Application.unityVersion,
   physicsStep=world.physicsSteps,physicsStepMs=(double)WorldController.StepSeconds*1000,
   bodyJson=JsonUtility.ToJson(BodyFrame.Capture(body,world.physicsSteps)),
   contactsJson=body.links.Values.OrderBy(l=>l.name,StringComparer.Ordinal).Select(l=>JsonUtility.ToJson(ContactFrame.Capture(body,l,world.physicsSteps))).ToArray(),
   paused=!world.Running,isolated=world.IsolatedQualification,connected=world.Connected,finite=body.IsFinite(),musclesEnabled=body.MusclesEnabled,
   pelvisHeightMeters=body.pelvis.transform.position.y,links=body.links.Count,
   bodyFramesAccepted=world.bodyFramesAccepted,retinalFramesAccepted=world.retinalFramesAccepted,contactFramesAccepted=world.contactFramesAccepted,
   directStepRejected=directRejected,wrongOwnerRejected=wrongRejected};
 }
 static void WriteNew(string path,string text){
  string temporary=path+".tmp";
  using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
  using(var writer=new StreamWriter(stream)){writer.Write(text);}
  File.Move(temporary,path); // No overwrite: a replay never recaptures old time.
 }
 static void SaveFailure(WorldController world,string directory,string session,long sequence,Exception error){
  Debug.LogException(error);
  try {
   Directory.CreateDirectory(directory);
   WriteNew(Path.Combine(directory,"failure.json"),JsonUtility.ToJson(new Failure{
    sessionId=session,expectedSequence=sequence,physicsStep=world.physicsSteps,error=error.ToString()},true));
  }catch(Exception storageError){Debug.LogException(storageError);} // Still quit failed; never overwrite earlier evidence.
 }
}
}
