using System;
using UnityEditor;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    // Contract only in stage 1. No active-target storage and no runtime-flow migration.
    // This identifies a Scene asset; it never stores terrain, route or tower layout data.
    public readonly struct LevelSceneReference
    {
        public string SceneGuid { get; }
        private LevelSceneReference(string guid) => SceneGuid = guid;

        public static bool TryFromAsset(SceneAsset scene, out LevelSceneReference reference)
        {
            var path = scene != null ? AssetDatabase.GetAssetPath(scene) : "";
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            { reference = default; return false; }
            reference = new LevelSceneReference(AssetDatabase.AssetPathToGUID(path));
            return !string.IsNullOrEmpty(reference.SceneGuid);
        }

        // Resolve on demand so renaming/moving the asset does not leave a stale path.
        public bool TryResolvePath(out string scenePath)
        {
            scenePath = string.IsNullOrEmpty(SceneGuid) ? "" : AssetDatabase.GUIDToAssetPath(SceneGuid);
            return !string.IsNullOrEmpty(scenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
        }
    }

    public interface ILevelTestTargetProvider
    {
        bool TryGetTestTarget(out LevelSceneReference target);
    }

    public interface ILevelTestTargetSelection : ILevelTestTargetProvider
    {
        // Future explicit selection action. Must not copy, rename or overwrite scene contents.
        // A later runtime adapter will receive a build-loadable path, never a UnityEditor type.
        void SetTestTarget(LevelSceneReference target);
    }
}
