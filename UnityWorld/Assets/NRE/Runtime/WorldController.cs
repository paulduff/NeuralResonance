using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace Nre.UnityWorld {
public sealed class WorldController:MonoBehaviour {
 public const float StepSeconds=.01f;
 public GameObject avatarPrefab;
 public ArticulatedAvatar Avatar {get;private set;}
 public bool Running {get;private set;}
 public bool Connected=>!string.IsNullOrEmpty(leaseToken);
 public string Status {get;private set;}="Paused · isolated mechanics preview";
 public long physicsSteps,bodyFramesAccepted,retinalFramesAccepted,rejectedFrames;
 public long motorPacketsReceived,bodySpikesGenerated,bodyFramesRejected,contactFramesAccepted,contactFramesRejected;
 public MotorFrame LastAppliedCommand {get;private set;}=new MotorFrame();
 public bool LegActuationApplied {get;private set;}
 public MotorFrame ObservedMotor=>motor;
 public float MotorAgeSeconds=>Time.realtimeSinceStartup-lastMotorAt;
 public float LastBodyRequestSeconds {get;private set;}
 public float MaximumBodyRequestSeconds {get;private set;}
 public bool LeaseReleaseAccepted {get;private set;}
 public StandingTrial Trial {get;private set;}
 public bool IsolatedQualification {get;private set;}
 public string outputDirectory;
 public Camera leftEye,rightEye;
 MotorFrame motor=new MotorFrame();readonly AvatarSpinalLocomotorCircuit spinal=new AvatarSpinalLocomotorCircuit();
 float accumulator,lastMotorAt=-100,lastLeaseAt=-100,readyAt;long lastMotorTick=-1,sequence;
 string leaseToken;bool connecting;Camera view;Vector3 previewTarget;
 const string brain="http://127.0.0.1:5080";const string editor="http://127.0.0.1:5090/editor/api/world/unity";
 Texture2D retinalTexture;RenderTexture leftRetina,rightRetina;
 public MotorFrame DiagnosticCommand;public bool DiagnosticMode;
 void Awake(){
  if(new[]{"--qualify","--standing-trial","--muscle-qualify","--balance-qualify","--response-qualify"}.Count(flag=>Argument(flag)!=null)>1)throw new InvalidOperationException("Isolated qualifications and live observation are separate modes.");
  IsolatedQualification=new[]{"--qualify","--muscle-qualify","--balance-qualify","--response-qualify"}.Any(flag=>Argument(flag)!=null);
  Physics.simulationMode=SimulationMode.Script;Physics.gravity=new Vector3(0,-9.81f,0);Time.fixedDeltaTime=StepSeconds;
  Avatar=ArticulatedAvatar.Create(avatarPrefab);view=Camera.main;previewTarget=new Vector3(0,.85f,0);
  if(Argument("--muscles")!=null)Avatar.EnableMuscles();
  CreateEye(true);CreateEye(false);retinalTexture=new Texture2D(64,40,TextureFormat.RGB24,false);
  outputDirectory=Argument("--output")??Path.Combine(Application.persistentDataPath,"runs",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(outputDirectory);
  StartCoroutine(MotorLoop());StartCoroutine(BodyLoop());StartCoroutine(VisionLoop());
  if(Argument("--capture")!=null)StartCoroutine(CapturePreview());
  if(Argument("--qualify")!=null)StartCoroutine(PhysicsQualification.Run(this));
  if(Argument("--standing-trial")!=null){Trial=new StandingTrial(Argument("--standing-trial"));StartCoroutine(Trial.Run(this));}
  if(Argument("--muscle-qualify")!=null)StartCoroutine(MuscleQualification.Run(this));
  if(Argument("--balance-qualify")!=null)StartCoroutine(BalanceCapability.Run(this));
  if(Argument("--response-qualify")!=null)StartCoroutine(BalanceResponseQualification.Run(this));
 }
 void CreateEye(bool left){var eye=new GameObject(left?"Left physical eye":"Right physical eye");eye.transform.SetParent(Avatar.links["head"].body.transform,false);eye.transform.localPosition=new Vector3(left?-.032f:.032f,.12f,.115f);
  var camera=eye.AddComponent<Camera>();camera.fieldOfView=85;camera.nearClipPlane=.03f;camera.farClipPlane=40;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.31f,.38f);camera.enabled=false;
  var target=new RenderTexture(64,40,16,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
  if(left){leftEye=camera;leftRetina=target;}else{rightEye=camera;rightRetina=target;}
 }
 void Update(){
  if(Connected&&(Time.realtimeSinceStartup-lastLeaseAt>7||Time.realtimeSinceStartup-lastMotorAt>1.25f)){Running=false;Status="Paused · brain/lease telemetry stale";}
  if(Running){accumulator=Mathf.Min(accumulator+Time.unscaledDeltaTime,.12f);while(Running&&accumulator>=StepSeconds){Step();accumulator-=StepSeconds;}}
  else accumulator=0;
  Avatar.SynchronizeSkin();
 }
 public void Step(){
  if(DiagnosticMode&&!IsolatedQualification)throw new InvalidOperationException("Diagnostic commands cannot enter live operation.");
  if(!IsolatedQualification)Avatar.muscles.RequireLivePolicy();
  MotorFrame command=DiagnosticMode?DiagnosticCommand:Connected&&motor.IsAuthorized()?motor:new MotorFrame();
  command=command??new MotorFrame();
  LastAppliedCommand=command;LegActuationApplied=false;
  float descending=(Mathf.Abs(command.leftDrive)+Mathf.Abs(command.rightDrive))*.5f;
  var population=spinal.Advance(StepSeconds,descending,Mathf.Clamp01(Avatar.LeftLoad/(Avatar.totalMass*9.81f)),Mathf.Clamp01(Avatar.RightLoad/(Avatar.totalMass*9.81f)));
  var efforts=Avatar.MusclesEnabled?Avatar.muscles.Advance(command,population,(name,axis)=>Avatar.Angle(name,axis),
   (name,axis)=>Avatar.Speed(name,axis),Avatar.physiology.MotorCapacity,StepSeconds,Avatar.BoundMuscleTarget):MotorActuation.Build(command,population,Avatar.physiology.MotorCapacity);
  foreach(var effort in efforts){
   if((effort.Joint.EndsWith("thigh")||effort.Joint.EndsWith("shin")||effort.Joint.EndsWith("foot"))&&(Math.Abs(effort.First)+Math.Abs(effort.Second)+Math.Abs(effort.Stiffness)>.00001f))LegActuationApplied=true;
   Avatar.Torques(effort.Joint,effort.First,effort.Second,effort.Stiffness);
  }
  Avatar.BeginStep();Physics.Simulate(StepSeconds);physicsSteps++;
  Avatar.AdvancePhysiology(StepSeconds);
  if(!Avatar.IsFinite()){Running=false;Status="Paused · non-finite physical state";}
  Trial?.AfterStep(this);
 }
 void OnGUI(){
  GUI.Box(new Rect(16,16,560,168),"NRE · Unity physical world");
  GUI.Label(new Rect(32,44,530,24),Status);
  bool ready=!connecting&&(!Connected||(Time.realtimeSinceStartup>=readyAt&&Time.realtimeSinceStartup-lastLeaseAt<7&&Time.realtimeSinceStartup-lastMotorAt<1.25f));
  GUI.enabled=ready;
  if(Trial!=null)GUI.enabled=ready&&Connected&&!Trial.Finished;
  if(GUI.Button(new Rect(32,75,110,30),Running?"Pause":"Play")){Running=!Running;Status=Running?(Connected?"Live · DNNE authority":"Live · isolated mechanics preview"):"Paused";}
  GUI.enabled=!connecting&&(Trial==null||!Trial.Finished);
  if(GUI.Button(new Rect(152,75,150,30),Connected?"Disconnect DNNE":"Connect DNNE")){if(Connected)StartCoroutine(Disconnect());else StartCoroutine(Connect());}
  GUI.enabled=true;
  if(GUI.Button(new Rect(312,75,110,30),"Front view"))PositionCamera(new Vector3(0,1.4f,4.3f));
  if(GUI.Button(new Rect(432,75,110,30),"Side view"))PositionCamera(new Vector3(4.3f,1.4f,.2f));
  GUI.Label(new Rect(32,114,525,24),$"Step {physicsSteps}  |  Feet: {Avatar.LeftLoad:F0} N / {Avatar.RightLoad:F0} N  |  {Avatar.totalMass:F1} kg");
  GUI.Label(new Rect(32,140,525,24),$"Body accepted: {bodyFramesAccepted}  |  Retina accepted: {retinalFramesAccepted}  |  Rejected: {rejectedFrames}");
  if(Trial!=null)GUI.Label(new Rect(32,190,750,24),Trial.Caption+" · no scripted balance or gait");
  var state=Avatar.physiology.State;
  string plant=Avatar.MusclesEnabled?"38 Avatar muscles with measured joint feedback":"simplified joint torques";
  GUI.Box(new Rect(16,Screen.height-100,800,85),$"DNNE = brain · Avatar = body/nervous system · Unity = environment\nEnergy: {state.StoredEnergyJoules/1000000:F3} MJ · Hydration: {state.HydrationFraction:P1} · Tissue: {state.TissueIntegrityFraction:P2}\nSimulation physiology; {plant}; standing/walking unqualified.\nAvatar: Mike Alger · CC BY 4.0 (source and license in the project credits)");
 }
 void PositionCamera(Vector3 p){view.transform.position=p;view.transform.LookAt(previewTarget);}
 IEnumerator Connect(){
  if(IsolatedQualification){Running=false;Status="Paused · qualification cannot connect to DNNE";yield break;}
  Running=false;connecting=true;Status="Acquiring exclusive physical world…";
  using(var request=JsonPost(editor+"/acquire",JsonUtility.ToJson(new LeaseRequest{owner="unity-"+Guid.NewGuid().ToString("N")}))){yield return request.SendWebRequest();
   if(request.result!=UnityWebRequest.Result.Success){Status="Paused · editor handoff unavailable ("+request.responseCode+")";connecting=false;yield break;}
   var reply=JsonUtility.FromJson<LeaseReply>(request.downloadHandler.text);if(reply==null||!reply.accepted||string.IsNullOrEmpty(reply.token)){Status="Paused · handoff denied";connecting=false;yield break;}
   leaseToken=reply.token;lastLeaseAt=Time.realtimeSinceStartup;readyAt=Time.realtimeSinceStartup+Mathf.Max(10,reply.drainSeconds);lastMotorAt=-100;
  }
  connecting=false;Status="Paused · draining old WorldSim sensory requests (10 seconds)";StartCoroutine(LeaseLoop());
 }
 IEnumerator Disconnect(){Running=false;string token=leaseToken;leaseToken=null;motor=new MotorFrame();Status="Paused · isolated mechanics preview";LeaseReleaseAccepted=token==null;
  if(token!=null)using(var request=JsonPost(editor+"/release",JsonUtility.ToJson(new LeaseRequest{token=token}))){yield return request.SendWebRequest();LeaseReleaseAccepted=request.result==UnityWebRequest.Result.Success&&JsonUtility.FromJson<LeaseReply>(request.downloadHandler.text)?.accepted==true;}
 }
 IEnumerator LeaseLoop(){string token=leaseToken;while(leaseToken==token){
  yield return new WaitForSecondsRealtime(2);
  if(leaseToken!=token)yield break;
  using(var request=JsonPost(editor+"/heartbeat",JsonUtility.ToJson(new LeaseRequest{token=token}))){yield return request.SendWebRequest();
   if(request.result!=UnityWebRequest.Result.Success){Running=false;Status="Paused · world lease heartbeat failed";continue;}
   lastLeaseAt=Time.realtimeSinceStartup;if(Time.realtimeSinceStartup>=readyAt&&!Running)Status="Paused · DNNE connected; press Play";
  }
 }}
 IEnumerator MotorLoop(){while(true){yield return new WaitForSecondsRealtime(.1f);if(!Connected)continue;
  using(var request=UnityWebRequest.Get(brain+"/api/v1/neuronal-motor")){request.timeout=2;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)continue;
   string json=request.downloadHandler.text;if(!json.Contains("\"minimumOutputConfidence\""))continue;
   var envelope=JsonUtility.FromJson<MotorEnvelope>(json);if(envelope?.runtime==null||!envelope.runtime.IsFinite())continue;
   if(envelope.runtime.tick>lastMotorTick){motor=envelope.runtime;lastMotorTick=motor.tick;lastMotorAt=Time.realtimeSinceStartup;motorPacketsReceived++;
    // Retain full recovery-latch/action diagnostics discarded by the command projection.
    if(Trial!=null)File.AppendAllText(Path.Combine(outputDirectory,"motor-telemetry.jsonl"),JsonUtility.ToJson(
     new MotorTelemetry{physicsStep=physicsSteps,realtimeSeconds=Time.realtimeSinceStartup,running=Running,responseJson=json})+"\n");
   }
  }
 }}
 bool CanSend()=>Running&&Connected&&Time.realtimeSinceStartup>=readyAt&&Time.realtimeSinceStartup-lastLeaseAt<7&&Time.realtimeSinceStartup-lastMotorAt<1.25f;
 IEnumerator BodyLoop(){while(true){yield return new WaitForSecondsRealtime(.05f);if(!CanSend())continue;
  var frame=BodyFrame.Capture(Avatar,++sequence);
  using(var request=JsonPost(brain+"/api/v1/admin/input/body-frame",JsonUtility.ToJson(frame))){float sentAt=Time.realtimeSinceStartup;yield return request.SendWebRequest();LastBodyRequestSeconds=Time.realtimeSinceStartup-sentAt;MaximumBodyRequestSeconds=Mathf.Max(MaximumBodyRequestSeconds,LastBodyRequestSeconds);Count(request,false);}
  if(!CanSend())continue;
  foreach(var link in Avatar.links.Values.ToArray()){
   if(link.sensor.force<=0||!CanSend())continue;
   var contact=ContactFrame.Capture(Avatar,link,++sequence);
   using(var request=JsonPost(brain+"/api/v1/admin/input/contact-frame",JsonUtility.ToJson(contact))){yield return request.SendWebRequest();var reply=request.result==UnityWebRequest.Result.Success?JsonUtility.FromJson<AcceptedReply>(request.downloadHandler.text):null;if(reply!=null&&reply.accepted)contactFramesAccepted++;else{contactFramesRejected++;rejectedFrames++;}}
  }
 }}
 IEnumerator VisionLoop(){while(true){yield return new WaitForSecondsRealtime(.125f);if(!CanSend())continue;
  foreach(var camera in new[]{leftEye,rightEye}){
   if(!CanSend())continue;camera.Render();var previous=RenderTexture.active;RenderTexture.active=camera.targetTexture;
   retinalTexture.ReadPixels(new Rect(0,0,64,40),0,0);retinalTexture.Apply();RenderTexture.active=previous;
   var bytes=retinalTexture.GetRawTextureData<byte>().ToArray();
   // Unity ReadPixels starts at the lower row; DNNE retinal pixels start at the top.
   var upright=new byte[bytes.Length];for(int y=0;y<40;y++)Buffer.BlockCopy(bytes,y*192,upright,(39-y)*192,192);
   string side=camera==leftEye?"left":"right";
   using(var request=new UnityWebRequest(brain+"/api/v1/admin/input/visual-frame?width=64&height=40&stride=192&pixelFormat=rgb24&inputSource=avatar_retina_"+side,"POST")){
    request.uploadHandler=new UploadHandlerRaw(upright);request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/octet-stream");request.timeout=2;yield return request.SendWebRequest();Count(request,true);
   }
  }
 }}
 void Count(UnityWebRequest request,bool vision){var reply=request.result==UnityWebRequest.Result.Success?JsonUtility.FromJson<AcceptedReply>(request.downloadHandler.text):null;
  if(reply!=null&&reply.accepted){if(vision)retinalFramesAccepted++;else{bodyFramesAccepted++;bodySpikesGenerated+=reply.generatedSpikes;}}else{rejectedFrames++;if(!vision)bodyFramesRejected++;}
 }
 static UnityWebRequest JsonPost(string url,string json){var request=new UnityWebRequest(url,"POST"){uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),downloadHandler=new DownloadHandlerBuffer(),timeout=2};request.SetRequestHeader("Content-Type","application/json");return request;}
 IEnumerator CapturePreview(){yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory,"unity-world-preview.png"));}
 public static string Argument(string name){var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length;i++)if(args[i]==name)return i+1<args.Length&&!args[i+1].StartsWith("--")?args[i+1]:"true";return null;}
 public bool ReadyForTrial=>Connected&&!connecting&&Time.realtimeSinceStartup>=readyAt&&Time.realtimeSinceStartup-lastLeaseAt<7&&Time.realtimeSinceStartup-lastMotorAt<1.25f;
 public IEnumerator ConnectForTrial()=>Connect();
 public IEnumerator DisconnectForTrial()=>Disconnect();
 public void PauseTrial(string reason){Running=false;Status=reason;}
 public void ResetDiagnosticBody(bool muscles,bool preserveActiveCapacity=false,bool preserveVelocityCapacity=true,double diagnosticStrengthScale=1){if(Connected||!DiagnosticMode||!IsolatedQualification)throw new InvalidOperationException("Body reset is an isolated fixture only.");
  Running=false;Avatar.pelvis.gameObject.SetActive(false);Avatar.visual.SetActive(false);Destroy(Avatar.pelvis.gameObject);Destroy(Avatar.visual);
  Avatar=ArticulatedAvatar.Create(avatarPrefab,diagnosticStrengthScale);if(muscles)Avatar.EnableMuscles(preserveActiveCapacity,preserveVelocityCapacity);spinal.Reset();
 }
 void OnApplicationQuit(){Trial?.OnClosing(this);WriteReport();}
 public void WriteReport(){File.WriteAllText(Path.Combine(outputDirectory,"session.json"),JsonUtility.ToJson(new Session{unity=Application.unityVersion,physicsSteps=physicsSteps,bodyFramesAccepted=bodyFramesAccepted,retinalFramesAccepted=retinalFramesAccepted,rejectedFrames=rejectedFrames,connected=Connected,paused=!Running,finite=Avatar.IsFinite(),massKg=Avatar.totalMass,status=Status},true));}
 [Serializable] class Session{public string unity,status;public long physicsSteps,bodyFramesAccepted,retinalFramesAccepted,rejectedFrames;public bool connected,paused,finite;public float massKg;}
 [Serializable] class LeaseRequest{public string owner,token;}
 [Serializable] class LeaseReply{public bool accepted;public string token;public int leaseSeconds,drainSeconds;}
 [Serializable] class AcceptedReply{public bool accepted;public long generatedSpikes;}
 [Serializable] class MotorEnvelope{public MotorFrame runtime;}
 [Serializable] class MotorTelemetry{public long physicsStep;public float realtimeSeconds;public bool running;public string responseJson;}
}
}
