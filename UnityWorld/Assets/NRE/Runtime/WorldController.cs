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
 public string outputDirectory;
 public Camera leftEye,rightEye;
 MotorFrame motor=new MotorFrame();readonly AvatarSpinalLocomotorCircuit spinal=new AvatarSpinalLocomotorCircuit();
 float accumulator,lastMotorAt=-100,lastLeaseAt=-100,readyAt;long lastMotorTick=-1,sequence;
 string leaseToken;bool connecting;Camera view;Vector3 previewTarget;
 const string brain="http://127.0.0.1:5080";const string editor="http://127.0.0.1:5090/editor/api/world/unity";
 Texture2D retinalTexture;RenderTexture leftRetina,rightRetina;
 public MotorFrame DiagnosticCommand;public bool DiagnosticMode;
 void Awake(){
  Physics.simulationMode=SimulationMode.Script;Physics.gravity=new Vector3(0,-9.81f,0);Time.fixedDeltaTime=StepSeconds;
  Avatar=ArticulatedAvatar.Create(avatarPrefab);view=Camera.main;previewTarget=new Vector3(0,.85f,0);
  CreateEye(true);CreateEye(false);retinalTexture=new Texture2D(64,40,TextureFormat.RGB24,false);
  outputDirectory=Argument("--output")??Path.Combine(Application.persistentDataPath,"runs",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(outputDirectory);
  StartCoroutine(MotorLoop());StartCoroutine(BodyLoop());StartCoroutine(VisionLoop());
  if(Argument("--capture")!=null)StartCoroutine(CapturePreview());
  if(Argument("--qualify")!=null)StartCoroutine(PhysicsQualification.Run(this));
 }
 void CreateEye(bool left){var eye=new GameObject(left?"Left physical eye":"Right physical eye");eye.transform.SetParent(Avatar.links["head"].body.transform,false);eye.transform.localPosition=new Vector3(left?-.032f:.032f,.12f,.115f);
  var camera=eye.AddComponent<Camera>();camera.fieldOfView=85;camera.nearClipPlane=.03f;camera.farClipPlane=40;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.31f,.38f);camera.enabled=false;
  var target=new RenderTexture(64,40,16,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
  if(left){leftEye=camera;leftRetina=target;}else{rightEye=camera;rightRetina=target;}
 }
 void Update(){
  if(Connected&&(Time.realtimeSinceStartup-lastLeaseAt>7||Time.realtimeSinceStartup-lastMotorAt>1.25f)){Running=false;Status="Paused · brain/lease telemetry stale";}
  if(Running){accumulator=Mathf.Min(accumulator+Time.unscaledDeltaTime,.12f);while(accumulator>=StepSeconds){Step();accumulator-=StepSeconds;}}
  else accumulator=0;
  Avatar.SynchronizeSkin();
 }
 public void Step(){
  MotorFrame command=DiagnosticMode?DiagnosticCommand:Connected&&motor.IsAuthorized()?motor:new MotorFrame();
  command=command??new MotorFrame();
  float descending=(Mathf.Abs(command.leftDrive)+Mathf.Abs(command.rightDrive))*.5f;
  var population=spinal.Advance(StepSeconds,descending,Mathf.Clamp01(Avatar.LeftLoad/(Avatar.totalMass*9.81f)),Mathf.Clamp01(Avatar.RightLoad/(Avatar.totalMass*9.81f)));
  float sign=Math.Sign(command.leftDrive+command.rightDrive);float stand=command.standDrive;
  void Leg(string side,float cycle,float swing,float coronal,float ankle,float roll){
   Avatar.Torques(side+"_thigh",cycle*70*sign*descending,coronal*70,stand*80);
   Avatar.Torques(side+"_shin",swing*35*descending,posturalStiffness:stand*120);
   Avatar.Torques(side+"_foot",ankle*65+cycle*10*descending,roll*35);
  }
  Leg("left",(float)population.LeftCycle,(float)population.LeftFlexorRecruitment,command.leftHipCoronalDrive,command.leftAnkleSagittalDrive,command.leftAnkleCoronalDrive);
  Leg("right",(float)population.RightCycle,(float)population.RightFlexorRecruitment,command.rightHipCoronalDrive,command.rightAnkleSagittalDrive,command.rightAnkleCoronalDrive);
  Avatar.Torques("left_arm",command.leftShoulderSagittalDrive*40,command.leftShoulderCoronalDrive*40);
  Avatar.Torques("right_arm",command.rightShoulderSagittalDrive*40,command.rightShoulderCoronalDrive*40);
  Avatar.Torques("left_forearm",command.leftElbowDrive*25);Avatar.Torques("right_forearm",command.rightElbowDrive*25);
  Avatar.Torques("chest",command.trunkYawDrive*50);Avatar.Torques("head",command.headPitchDrive*8,command.headYawDrive*8);
  Avatar.BeginStep();Physics.Simulate(StepSeconds);physicsSteps++;
  if(!Avatar.IsFinite()){Running=false;Status="Paused · non-finite physical state";}
 }
 void OnGUI(){
  GUI.Box(new Rect(16,16,560,168),"NRE · Unity physical world");
  GUI.Label(new Rect(32,44,530,24),Status);
  bool ready=!connecting&&(!Connected||(Time.realtimeSinceStartup>=readyAt&&Time.realtimeSinceStartup-lastLeaseAt<7&&Time.realtimeSinceStartup-lastMotorAt<1.25f));
  GUI.enabled=ready;
  if(GUI.Button(new Rect(32,75,110,30),Running?"Pause":"Play")){Running=!Running;Status=Running?(Connected?"Live · DNNE authority":"Live · isolated mechanics preview"):"Paused";}
  GUI.enabled=!connecting;
  if(GUI.Button(new Rect(152,75,150,30),Connected?"Disconnect DNNE":"Connect DNNE")){if(Connected)StartCoroutine(Disconnect());else StartCoroutine(Connect());}
  GUI.enabled=true;
  if(GUI.Button(new Rect(312,75,110,30),"Front view"))PositionCamera(new Vector3(0,1.4f,4.3f));
  if(GUI.Button(new Rect(432,75,110,30),"Side view"))PositionCamera(new Vector3(4.3f,1.4f,.2f));
  GUI.Label(new Rect(32,114,525,24),$"Step {physicsSteps}  |  Feet: {Avatar.LeftLoad:F0} N / {Avatar.RightLoad:F0} N  |  {Avatar.totalMass:F1} kg");
  GUI.Label(new Rect(32,140,525,24),$"Body accepted: {bodyFramesAccepted}  |  Retina accepted: {retinalFramesAccepted}  |  Rejected: {rejectedFrames}");
  GUI.Box(new Rect(16,Screen.height-80,800,65),"DNNE = brain · Avatar = body/nervous system · Unity = environment\nMechanics prototype: simplified muscle torques, fixed physiology reference, no learned walking claim.\nAvatar: Mike Alger · CC BY 4.0 (source and license in the project credits)");
 }
 void PositionCamera(Vector3 p){view.transform.position=p;view.transform.LookAt(previewTarget);}
 IEnumerator Connect(){
  Running=false;connecting=true;Status="Acquiring exclusive physical world…";
  using(var request=JsonPost(editor+"/acquire",JsonUtility.ToJson(new LeaseRequest{owner="unity-"+Guid.NewGuid().ToString("N")}))){yield return request.SendWebRequest();
   if(request.result!=UnityWebRequest.Result.Success){Status="Paused · editor handoff unavailable ("+request.responseCode+")";connecting=false;yield break;}
   var reply=JsonUtility.FromJson<LeaseReply>(request.downloadHandler.text);if(reply==null||!reply.accepted||string.IsNullOrEmpty(reply.token)){Status="Paused · handoff denied";connecting=false;yield break;}
   leaseToken=reply.token;lastLeaseAt=Time.realtimeSinceStartup;readyAt=Time.realtimeSinceStartup+Mathf.Max(10,reply.drainSeconds);lastMotorAt=-100;
  }
  connecting=false;Status="Paused · draining old WorldSim sensory requests (10 seconds)";StartCoroutine(LeaseLoop());
 }
 IEnumerator Disconnect(){Running=false;string token=leaseToken;leaseToken=null;motor=new MotorFrame();Status="Paused · isolated mechanics preview";
  if(token!=null)using(var request=JsonPost(editor+"/release",JsonUtility.ToJson(new LeaseRequest{token=token}))){yield return request.SendWebRequest();}
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
   if(envelope.runtime.tick>lastMotorTick){motor=envelope.runtime;lastMotorTick=motor.tick;lastMotorAt=Time.realtimeSinceStartup;}
  }
 }}
 bool CanSend()=>Running&&Connected&&Time.realtimeSinceStartup>=readyAt&&Time.realtimeSinceStartup-lastLeaseAt<7&&Time.realtimeSinceStartup-lastMotorAt<1.25f;
 IEnumerator BodyLoop(){while(true){yield return new WaitForSecondsRealtime(.05f);if(!CanSend())continue;
  var frame=BodyFrame.Capture(Avatar,++sequence);
  using(var request=JsonPost(brain+"/api/v1/admin/input/body-frame",JsonUtility.ToJson(frame))){yield return request.SendWebRequest();Count(request,false);}
  if(!CanSend())continue;
  foreach(var link in Avatar.links.Values.ToArray()){
   if(link.sensor.force<=0||!CanSend())continue;
   var contact=ContactFrame.Capture(Avatar,link,++sequence);
   using(var request=JsonPost(brain+"/api/v1/admin/input/contact-frame",JsonUtility.ToJson(contact))){yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)rejectedFrames++;}
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
  if(reply!=null&&reply.accepted){if(vision)retinalFramesAccepted++;else bodyFramesAccepted++;}else rejectedFrames++;
 }
 static UnityWebRequest JsonPost(string url,string json){var request=new UnityWebRequest(url,"POST"){uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),downloadHandler=new DownloadHandlerBuffer(),timeout=2};request.SetRequestHeader("Content-Type","application/json");return request;}
 IEnumerator CapturePreview(){yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory,"unity-world-preview.png"));}
 public static string Argument(string name){var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length;i++)if(args[i]==name)return i+1<args.Length&&!args[i+1].StartsWith("--")?args[i+1]:"true";return null;}
 void OnApplicationQuit(){WriteReport();}
 public void WriteReport(){File.WriteAllText(Path.Combine(outputDirectory,"session.json"),JsonUtility.ToJson(new Session{unity=Application.unityVersion,physicsSteps=physicsSteps,bodyFramesAccepted=bodyFramesAccepted,retinalFramesAccepted=retinalFramesAccepted,rejectedFrames=rejectedFrames,connected=Connected,paused=!Running,finite=Avatar.IsFinite(),massKg=Avatar.totalMass,status=Status},true));}
 [Serializable] class Session{public string unity,status;public long physicsSteps,bodyFramesAccepted,retinalFramesAccepted,rejectedFrames;public bool connected,paused,finite;public float massKg;}
 [Serializable] class LeaseRequest{public string owner,token;}
 [Serializable] class LeaseReply{public bool accepted;public string token;public int leaseSeconds,drainSeconds;}
 [Serializable] class AcceptedReply{public bool accepted;}
 [Serializable] class MotorEnvelope{public MotorFrame runtime;}
}
[Serializable] public sealed class MotorFrame {
 public bool active;public long tick,sequence;public float confidence,minimumOutputConfidence;
 public float leftDrive,rightDrive,leftHipCoronalDrive,rightHipCoronalDrive,leftAnkleSagittalDrive,rightAnkleSagittalDrive,leftAnkleCoronalDrive,rightAnkleCoronalDrive,leftShoulderSagittalDrive,rightShoulderSagittalDrive,leftShoulderCoronalDrive,rightShoulderCoronalDrive,leftElbowDrive,rightElbowDrive,headYawDrive,headPitchDrive,trunkYawDrive,standDrive;
 public bool IsAuthorized()=>active&&IsFinite()&&minimumOutputConfidence>0&&confidence>=minimumOutputConfidence;
 public bool IsFinite()=>new[]{confidence,minimumOutputConfidence,leftDrive,rightDrive,leftHipCoronalDrive,rightHipCoronalDrive,leftAnkleSagittalDrive,rightAnkleSagittalDrive,leftAnkleCoronalDrive,rightAnkleCoronalDrive,leftShoulderSagittalDrive,rightShoulderSagittalDrive,leftShoulderCoronalDrive,rightShoulderCoronalDrive,leftElbowDrive,rightElbowDrive,headYawDrive,headPitchDrive,trunkYawDrive,standDrive}.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Mathf.Abs(v)<=1);
}
}
