using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nre.UnityWorld.Editor
{
    public static class CoordinateBoundaryCheck
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var output = Environment.GetEnvironmentVariable("NRE_UNITY_OUTPUT");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("NRE_UNITY_OUTPUT is required.");
            Directory.CreateDirectory(output);
            var previousMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                var report = NativeCoordinateProbe.Run();
                File.WriteAllText(Path.Combine(output, "native-coordinate-probe.json"), JsonUtility.ToJson(report, true));
                if (!report.passed) throw new InvalidOperationException("Native coordinate boundary probe failed.");
            }
            finally { Physics.simulationMode = previousMode; }
        }
    }
}
