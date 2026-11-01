using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    public static class LevelTemplateUtility
    {
        public const string TemplateScenePath = "Assets/Rebuild/Scenes/LevelTemplate.unity";
        public const string ReservedTestScenePath = "Assets/Rebuild/Scenes/TestLevel.unity";

        public static bool IsTemplate(string path) => string.Equals(path, TemplateScenePath, StringComparison.OrdinalIgnoreCase);

        // Creates an asset only. Does not open/save scenes, change the build list or select a test target.
        public static SceneAsset CreateLevelFromTemplate(string destination)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating a level.");
            var path = NormalizeDestination(destination);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath) == null)
                throw new InvalidOperationException("LevelTemplate.unity is missing.");
            if (!AssetDatabase.CopyAsset(TemplateScenePath, path))
                throw new IOException("Unity could not copy the template to " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path)
                ?? throw new IOException("The copied scene could not be imported: " + path);
        }

        public static string NormalizeDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination) || Path.IsPathRooted(destination))
                throw new ArgumentException("Choose a project-relative .unity path inside Assets.");
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var full = Path.GetFullPath(Path.Combine(project, destination));
            var assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(full), ".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The new scene must be a .unity file inside Assets.");
            var path = full.Substring(project.Length + 1).Replace('\\', '/');
            if (IsTemplate(path) || string.Equals(path, ReservedTestScenePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("LevelTemplate and TestLevel are protected destinations. Choose a new scene name.");
            if (!Directory.Exists(Path.GetDirectoryName(full)))
                throw new DirectoryNotFoundException("Choose an existing project folder.");
            if (File.Exists(full) || File.Exists(full + ".meta") || Directory.Exists(full) ||
                AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new IOException("The destination already exists. Existing scenes are never overwritten.");
            return path;
        }
    }
}
