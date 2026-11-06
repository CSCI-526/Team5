using System.Collections.Generic;
using System.IO;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
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

        [MenuItem("Team5/Rebuild/Open Level 1 Grid Battle")]
        public static void OpenLevelOne() => Open(BattleSceneFlow.BattleScene);

        [MenuItem("Team5/Rebuild/Open Level 2 Grid Battle")]
        public static void OpenLevelTwo() => Open(BattleSceneFlow.LevelTwoScene);

        [MenuItem("Team5/Rebuild/Open Level 3 Grid Battle")]
        public static void OpenLevelThree() => Open(BattleSceneFlow.LevelThreeScene);

        [MenuItem("Team5/Rebuild/Open Level 4 Grid Battle")]
        public static void OpenLevelFour() => Open(BattleSceneFlow.LevelFourScene);

        [MenuItem("Team5/Rebuild/Open Level 5 Grid Battle")]
        public static void OpenLevelFive() => Open(BattleSceneFlow.LevelFiveScene);

        [MenuItem("Team5/Rebuild/Open Level 6 Grid Battle")]
        public static void OpenLevelSix() => Open(BattleSceneFlow.LevelSixScene);

        [MenuItem("Team5/Rebuild/Open Level 7 Grid Battle")]
        public static void OpenLevelSeven() => Open(BattleSceneFlow.LevelSevenScene);

        [MenuItem("Team5/Rebuild/Test/Open Test Start Scene")]
        public static void OpenTestStart() => Open(BattleSceneFlow.TestStartScene);

        [MenuItem("Team5/Rebuild/Test/Open Test Update Scene")]
        public static void OpenTestUpdate() => Open(BattleSceneFlow.TestUpdateScene);

        [MenuItem("Team5/Rebuild/Test/Open Test Level")]
        public static void OpenTestLevel() => Open(BattleSceneFlow.TestLevelScene);

        [MenuItem("Team5/Rebuild/Open Level Settlement")]
        public static void OpenSettlement() => Open(BattleSceneFlow.SettlementScene);

        [MenuItem("Team5/Rebuild/Generate Levels 5, 6 & 7 + Rebalance Economy")]
        public static void GenerateExpansionLevelsAndEconomy()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Exit Play Mode before generating levels.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UpdateEconomyAsset();
            BuildAndSaveLevel(5, BattleSceneFlow.LevelFiveScene);
            BuildAndSaveLevel(6, BattleSceneFlow.LevelSixScene);
            BuildAndSaveLevel(7, BattleSceneFlow.LevelSevenScene);
            EnsureBuildSettingsScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Successfully generated Level 5, Level 6, and Level 7 (with AOE towers) and rebalanced RebuildEconomy.asset!");
        }

        private static void UpdateEconomyAsset()
        {
            var economy = AssetDatabase.LoadAssetAtPath<GameEconomySettings>("Assets/Rebuild/Resources/RebuildEconomy.asset");
            if (economy == null) return;
            var so = new SerializedObject(economy);
            so.FindProperty("startingGold").intValue = 35;
            so.FindProperty("baseSummonCost").intValue = 30;
            so.FindProperty("summonCostIncreasePerPull").intValue = 15;
            var baseBounty = so.FindProperty("baseTowerBounty");
            if (baseBounty != null) baseBounty.intValue = 25;
            var aoeBounty = so.FindProperty("aoeTowerBounty");
            if (aoeBounty != null) aoeBounty.intValue = 45;

            var rewards = so.FindProperty("levelRewards");
            int[] stageGolds = { 45, 65, 90, 120, 155, 195, 240 };
            rewards.arraySize = stageGolds.Length;
            for (var i = 0; i < stageGolds.Length; i++)
            {
                var elem = rewards.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("level").intValue = i + 1;
                elem.FindPropertyRelative("gold").intValue = stageGolds[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(economy);
        }

        private static void BuildAndSaveLevel(int levelNumber, string destinationPath)
        {
            var draft = ScriptableObject.CreateInstance<MapDraft>();
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var dirTower = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1.prefab");
                var sqTower = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1_Square.prefab");

                draft.ResetGrid(24, 18);
                draft.levelNumber = levelNumber;
                for (var i = 0; i < draft.tiles.Length; i++) draft.tiles[i] = (int)CellKind.Ground;

                if (levelNumber == 5)
                {
                    // S-Bend Canyon with 2 AOE Towers + 3 Directional Towers
                    PaintMacroLine(draft, new Vector2Int(1, 4), new Vector2Int(10, 4), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(10, 4), new Vector2Int(10, 13), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(10, 13), new Vector2Int(22, 13), CellKind.Road);
                    draft.Paint(new Vector2Int(1, 4), (int)CellKind.Spawn, 3);
                    draft.Paint(new Vector2Int(22, 13), (int)CellKind.Goal, 3);

                    draft.AppendRoute(new Vector2Int(1, 4));
                    draft.AppendRoute(new Vector2Int(10, 4));
                    draft.AppendRoute(new Vector2Int(10, 13));
                    draft.AppendRoute(new Vector2Int(22, 13));

                    PlacePlatformAndTower(draft, new Vector2Int(6, 7), sqTower, AttackArea.Square, isAoe: true, bounty: 50);
                    PlacePlatformAndTower(draft, new Vector2Int(7, 1), dirTower, AttackArea.PositiveZ, isAoe: false, bounty: 30);
                    PlacePlatformAndTower(draft, new Vector2Int(13, 9), dirTower, AttackArea.NegativeX, isAoe: false, bounty: 35);
                    PlacePlatformAndTower(draft, new Vector2Int(16, 16), sqTower, AttackArea.Square, isAoe: true, bounty: 55);
                    PlacePlatformAndTower(draft, new Vector2Int(19, 10), dirTower, AttackArea.PositiveZ, isAoe: false, bounty: 35);
                }
                else if (levelNumber == 6)
                {
                    // Zig-Zag Bastion with 3 AOE Towers + 3 Directional Towers
                    PaintMacroLine(draft, new Vector2Int(1, 13), new Vector2Int(7, 13), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(7, 13), new Vector2Int(7, 4), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(7, 4), new Vector2Int(16, 4), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(16, 4), new Vector2Int(16, 13), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(16, 13), new Vector2Int(22, 13), CellKind.Road);
                    draft.Paint(new Vector2Int(1, 13), (int)CellKind.Spawn, 3);
                    draft.Paint(new Vector2Int(22, 13), (int)CellKind.Goal, 3);

                    draft.AppendRoute(new Vector2Int(1, 13));
                    draft.AppendRoute(new Vector2Int(7, 13));
                    draft.AppendRoute(new Vector2Int(7, 4));
                    draft.AppendRoute(new Vector2Int(16, 4));
                    draft.AppendRoute(new Vector2Int(16, 13));
                    draft.AppendRoute(new Vector2Int(22, 13));

                    PlacePlatformAndTower(draft, new Vector2Int(4, 10), sqTower, AttackArea.Square, isAoe: true, bounty: 55);
                    PlacePlatformAndTower(draft, new Vector2Int(10, 10), dirTower, AttackArea.NegativeX, isAoe: false, bounty: 35);
                    PlacePlatformAndTower(draft, new Vector2Int(11, 7), sqTower, AttackArea.Square, isAoe: true, bounty: 60);
                    PlacePlatformAndTower(draft, new Vector2Int(13, 1), dirTower, AttackArea.PositiveZ, isAoe: false, bounty: 35);
                    PlacePlatformAndTower(draft, new Vector2Int(19, 7), dirTower, AttackArea.NegativeX, isAoe: false, bounty: 40);
                    PlacePlatformAndTower(draft, new Vector2Int(19, 10), sqTower, AttackArea.Square, isAoe: true, bounty: 60);
                }
                else
                {
                    // Stage 7 Citadel Finale with 4 AOE Towers + 4 Directional Towers
                    PaintMacroLine(draft, new Vector2Int(1, 4), new Vector2Int(7, 4), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(7, 4), new Vector2Int(7, 13), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(7, 13), new Vector2Int(16, 13), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(16, 13), new Vector2Int(16, 4), CellKind.Road);
                    PaintMacroLine(draft, new Vector2Int(16, 4), new Vector2Int(22, 4), CellKind.Road);
                    draft.Paint(new Vector2Int(1, 4), (int)CellKind.Spawn, 3);
                    draft.Paint(new Vector2Int(22, 4), (int)CellKind.Goal, 3);

                    draft.AppendRoute(new Vector2Int(1, 4));
                    draft.AppendRoute(new Vector2Int(7, 4));
                    draft.AppendRoute(new Vector2Int(7, 13));
                    draft.AppendRoute(new Vector2Int(16, 13));
                    draft.AppendRoute(new Vector2Int(16, 4));
                    draft.AppendRoute(new Vector2Int(22, 4));

                    PlacePlatformAndTower(draft, new Vector2Int(4, 7), sqTower, AttackArea.Square, isAoe: true, bounty: 65);
                    PlacePlatformAndTower(draft, new Vector2Int(4, 1), dirTower, AttackArea.PositiveZ, isAoe: false, bounty: 40);
                    PlacePlatformAndTower(draft, new Vector2Int(10, 9), sqTower, AttackArea.Square, isAoe: true, bounty: 65);
                    PlacePlatformAndTower(draft, new Vector2Int(11, 16), dirTower, AttackArea.NegativeZ, isAoe: false, bounty: 45);
                    PlacePlatformAndTower(draft, new Vector2Int(13, 10), sqTower, AttackArea.Square, isAoe: true, bounty: 70);
                    PlacePlatformAndTower(draft, new Vector2Int(13, 7), dirTower, AttackArea.PositiveX, isAoe: false, bounty: 45);
                    PlacePlatformAndTower(draft, new Vector2Int(19, 7), sqTower, AttackArea.Square, isAoe: true, bounty: 75);
                    PlacePlatformAndTower(draft, new Vector2Int(19, 1), dirTower, AttackArea.PositiveZ, isAoe: false, bounty: 45);
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                MapBuilderUtility.Populate(draft, scene);
                EditorSceneManager.SaveScene(scene, destinationPath);
            }
            finally
            {
                Object.DestroyImmediate(draft);
            }
        }

        private static void PaintMacroLine(MapDraft draft, Vector2Int from, Vector2Int to, CellKind kind)
        {
            var step = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));
            for (var p = from; ; p += step * 3)
            {
                draft.Paint(p, (int)kind, 3);
                if (p == to) break;
            }
        }

        private static void PlacePlatformAndTower(MapDraft draft, Vector2Int cell, GameObject prefab, AttackArea area, bool isAoe, int bounty)
        {
            draft.Paint(cell, (int)CellKind.TowerPlatform, 1);
            draft.PlaceTower(cell, prefab, area, isAoe, bounty);
        }

        private static void EnsureBuildSettingsScenes()
        {
            string[] required =
            {
                BattleSceneFlow.StartScene,
                BattleSceneFlow.UpgradeScene,
                BattleSceneFlow.BattleScene,
                BattleSceneFlow.LevelTwoScene,
                BattleSceneFlow.LevelThreeScene,
                BattleSceneFlow.LevelFourScene,
                BattleSceneFlow.LevelFiveScene,
                BattleSceneFlow.LevelSixScene,
                BattleSceneFlow.LevelSevenScene,
                BattleSceneFlow.SettlementScene,
                BattleSceneFlow.EndScene
            };
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in required)
            {
                if (!File.Exists(path)) continue;
                var idx = list.FindIndex(s => s.path == path);
                if (idx >= 0) list[idx].enabled = true;
                else list.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void Open(string path)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Exit Play Mode before opening a scene.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }
    }
}