using Team5.Rebuild.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class GameMenuScenes
    {
        [MenuItem("Team5/Rebuild/Open Start Game")]
        public static void OpenStart() => Open(BattleSceneFlow.StartScene);
        [MenuItem("Team5/Rebuild/Open End Game")]
        public static void OpenEnd() => Open(BattleSceneFlow.EndScene);
        [MenuItem("Team5/Rebuild/Open Upgrade Scene")]
        public static void OpenUpgrade() => Open(BattleSceneFlow.UpgradeScene);
        [MenuItem("Team5/Rebuild/Open Level 2 Grid Battle")]
        public static void OpenLevelTwo() => Open(BattleSceneFlow.LevelTwoScene);
        [MenuItem("Team5/Rebuild/Open Level 3 Grid Battle")]
        public static void OpenLevelThree() => Open(BattleSceneFlow.LevelThreeScene);
        [MenuItem("Team5/Rebuild/Open Level Settlement")]
        public static void OpenSettlement() => Open(BattleSceneFlow.SettlementScene);
        private static void Open(string path)
        {
            if (Application.isPlaying) { Debug.LogWarning("Exit Play Mode before opening a scene."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
