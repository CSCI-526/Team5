using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    // Builds the GitHub Pages player into docs/beta. CLI: -executeMethod Team5.Rebuild.Editor.WebGLBuild.BuildBeta
    public static class WebGLBuild
    {
        public const string OutputPath = "docs/beta";

        [MenuItem("Team5/Rebuild/Build WebGL Beta (docs/beta)")]
        public static void BuildBeta()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"WebGL build {summary.result}: {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
