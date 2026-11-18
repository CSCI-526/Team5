using System.Linq;
using Team5.Rebuild.Session;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    // Builds the GitHub Pages player into docs/beta. CLI: -executeMethod Team5.Rebuild.Editor.WebGLBuild.BuildBeta
    public static class WebGLBuild
    {
        public const string OutputPath = "docs/beta";

        [MenuItem("Team5/Rebuild/Build WebGL Gold (campaign only, docs/gold)")]
        public static void BuildGold()
        {
            var scenes = new[] { BattleSceneFlow.StartScene }
                .Concat(Enumerable.Range(1, BattleSceneFlow.TotalCampaignLevels).Select(BattleSceneFlow.SceneForLevel))
                .Concat(new[] { BattleSceneFlow.UpgradeScene, BattleSceneFlow.SettlementScene, BattleSceneFlow.EndScene }).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = "docs/gold", target = BuildTarget.WebGL, options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"Gold WebGL campaign build {summary.result}: {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // Compile check only: same scenes and options as BuildBeta, written to the gitignored Logs/WebGLCheck.
        // CLI: -executeMethod Team5.Rebuild.Editor.WebGLBuild.BuildCheck
        public static void BuildCheck() => Build("Logs/WebGLCheck");

        [MenuItem("Team5/Rebuild/Build WebGL Beta (docs/beta)")]
        public static void BuildBeta() => Build(OutputPath);

        private static void Build(string outputPath)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"WebGL build to {outputPath} {summary.result}: {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
