using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Nre.UnityWorld.Editor {
public static class WorldBuild {
 public static void Inspect() {
  var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Avatar/MikeAlger.fbx");
  importer.animationType=ModelImporterAnimationType.Human;
  importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
  importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Avatar/MikeAlger.fbx");
  var instance=UnityEngine.Object.Instantiate(model);var animator=instance.GetComponent<Animator>();
  var report=new Inspection{unity=Application.unityVersion,human=animator!=null&&animator.avatar!=null&&animator.avatar.isHuman,valid=animator!=null&&animator.avatar!=null&&animator.avatar.isValid,
   meshes=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.name+" ("+r.sharedMesh.vertexCount+" vertices)").ToArray(),
   bones=instance.GetComponentsInChildren<Transform>().Select(t=>t.name).ToArray()};
  Directory.CreateDirectory("../artifacts/unity-world-20261012");File.WriteAllText("../artifacts/unity-world-20261012/avatar-inspection.json",JsonUtility.ToJson(report,true));
  UnityEngine.Object.DestroyImmediate(instance);
  if(!report.human||!report.valid)throw new Exception("Avatar humanoid import did not qualify.");
 }

 public static void Prepare() {
  Inspect();
  UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Physical floor";floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(24,.1f,24);
  var floorMaterial=new Material(Shader.Find("Standard")){color=new Color(.29f,.34f,.35f)};
  floorMaterial=SaveMaterial(floorMaterial,"Assets/NRE/Floor.mat");floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
  floor.GetComponent<Collider>().sharedMaterial=new PhysicsMaterial("Ground grip"){staticFriction=.8f,dynamicFriction=.65f,bounciness=0};
  var lines=new Material(Shader.Find("Standard")){color=new Color(.39f,.48f,.48f)};lines=SaveMaterial(lines,"Assets/NRE/Grid.mat");
  for(int i=-12;i<=12;i++)foreach(bool alongX in new[]{true,false}){var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.name="Floor metre grid";UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());line.transform.position=alongX?new Vector3(0,.002f,i):new Vector3(i,.002f,0);line.transform.localScale=alongX?new Vector3(24,.002f,.015f):new Vector3(.015f,.002f,24);line.GetComponent<Renderer>().sharedMaterial=lines;}
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Avatar/MikeAlger_Texture.jpeg");var skin=new Material(Shader.Find("Standard")){color=Color.white,mainTexture=texture};skin.SetFloat("_Glossiness",.2f);skin=SaveMaterial(skin,"Assets/NRE/Avatar.mat");
  var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Avatar/MikeAlger.fbx");
  var host=new GameObject("DNNE Unity world");var controller=host.AddComponent<WorldController>();controller.avatarPrefab=model;
  // The prefab's importer-created materials are replaced by one saved diffuse material.
  var template=UnityEngine.Object.Instantiate(model);template.name="Physics driven humanoid";foreach(var r in template.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(skin,r.sharedMaterials.Length).ToArray();
  var saved=PrefabUtility.SaveAsPrefabAsset(template,"Assets/Avatar/PhysicsHumanoid.prefab");UnityEngine.Object.DestroyImmediate(template);controller.avatarPrefab=saved;
  var cam=new GameObject("World camera"){tag="MainCamera"};var camera=cam.AddComponent<Camera>();cam.transform.position=new Vector3(3.1f,1.65f,3.4f);cam.transform.LookAt(new Vector3(0,.85f,0));camera.fieldOfView=43;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.20f,.25f);
  var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-35,0);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.53f,.58f);
  for(int i=0;i<3;i++){var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Contact test block "+i;obstacle.transform.position=new Vector3(-3+i*2,.25f,4);obstacle.transform.localScale=new Vector3(.6f,.5f,.6f);obstacle.GetComponent<Renderer>().sharedMaterial=floorMaterial;}
  Directory.CreateDirectory("Assets/Scenes");UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/QualificationWorld.unity");
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/QualificationWorld.unity",true)};
  PlayerSettings.companyName="Neural Resonance";PlayerSettings.productName="DNNE Unity World";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.runInBackground=true;
  PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
  AssetDatabase.SaveAssets();
 }
 static Material SaveMaterial(Material material,string path){var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null){EditorUtility.CopySerialized(material,old);UnityEngine.Object.DestroyImmediate(material);}else AssetDatabase.CreateAsset(material,path);return old!=null?old:material;}
 public static void BuildWindows() {
  Prepare();
  var output=Environment.GetEnvironmentVariable("NRE_UNITY_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("NRE_UNITY_OUTPUT must name the local build directory.");
  output=Path.GetFullPath(output);Directory.CreateDirectory(output);
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/QualificationWorld.unity"},locationPathName=Path.Combine(output,"DNNE Unity World.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
  File.WriteAllText(Path.Combine(output,"build-result.json"),JsonUtility.ToJson(new BuildSummary{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings},true));
  if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Unity player build failed: "+report.summary.result);
 }
 [Serializable] class BuildSummary{public string result;public int errors,warnings;}

 [Serializable] class Inspection {public string unity;public bool human,valid;public string[] meshes,bones;}
}
}
