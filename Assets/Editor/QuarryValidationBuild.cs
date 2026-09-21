using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class QuarryValidationBuild
{
    public static void BuildWindows()
    {
        string productName = PlayerSettings.productName;
        try
        {
            PlayerSettings.productName = "QuantumQuarry Validation";
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = Path.GetFullPath("Builds/Validation/QuantumQuarryValidation.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "QUARRY_VALIDATION" }
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Validation build failed: {report.summary.result}");
            Debug.Log("Isolated Quarry validation player built successfully.");
        }
        finally
        {
            PlayerSettings.productName = productName;
            AssetDatabase.SaveAssets();
        }
    }
}
