using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class SquadBattleMenu
    {
        public const string ScenePath = "Assets/Rebuild/Scenes/SquadBattlePrototype.unity";
        [MenuItem("Team5/Rebuild/Open Level 1 Grid Battle")]
        public static void OpenLevelOne()
        {
            if (Application.isPlaying) { Debug.LogWarning("Exit Play Mode before opening a scene."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(Team5.Rebuild.Session.BattleSceneFlow.BattleScene);
        }
        [MenuItem("Team5/Rebuild/Open Squad Battle Prototype")]
        public static void Open()
        {
            if (Application.isPlaying) { Debug.LogWarning("Exit Play Mode before opening a scene."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
