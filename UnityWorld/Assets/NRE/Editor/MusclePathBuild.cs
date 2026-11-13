using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nre.UnityWorld.Editor {
public static class MusclePathBuild {
 [Serializable] sealed class Summary {public string result;public int errors,warnings;}
 public static void BuildWindows(){
  var output=Environment.GetEnvironmentVariable("NRE_UNITY_OUTPUT");
  if(string.IsNullOrWhiteSpace(output))throw new InvalidOperationException("Explicit isolated build output required.");
  Directory.CreateDirectory(output);
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("Isolated native muscle path qualification").AddComponent<MusclePathNativeQualification>();
  Directory.CreateDirectory("Assets/Scenes");
  const string scene="Assets/Scenes/MusclePathQualification.unity";
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName=Path.Combine(output,"DNNE Muscle Paths.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
  File.WriteAllText(Path.Combine(output,"build-result.json"),JsonUtility.ToJson(new Summary{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings},true));
  if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Isolated muscle path build failed.");
 }
}
}
