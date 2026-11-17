using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    public static class TowerPlaytestBuild
    {
        // Stable indices are taken from BattleSession's existing tower references, preserving every tower position.
        private static readonly TowerKind[][] Layouts = {
            new[] { TowerKind.Standard },
            new[] { TowerKind.Standard, TowerKind.Laser },
            new[] { TowerKind.Laser, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard },
            new[] { TowerKind.Laser, TowerKind.Frost, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard },
            new[] { TowerKind.Blast, TowerKind.Laser, TowerKind.Standard, TowerKind.Frost, TowerKind.Standard },
            new[] { TowerKind.Laser, TowerKind.Blast, TowerKind.Standard, TowerKind.Frost, TowerKind.Blast, TowerKind.Standard },
            new[] { TowerKind.Blast, TowerKind.Frost, TowerKind.Blast, TowerKind.Laser, TowerKind.Standard, TowerKind.Standard, TowerKind.Standard, TowerKind.Blast }
        };
        public static void Prepare()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Rebuild/Resources/Menu/Beginning.png");
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var effectPath = "Assets/Rebuild/Resources/TowerEffect.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(effectPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Team5/Tower Effect")), effectPath);
            var report = "Tower playtest: positions and counts preserved\n";
            for (var level = 1; level <= 7; level++)
            {
                var scene = EditorSceneManager.OpenScene(BattleSceneFlow.SceneForLevel(level));
                var session = UnityEngine.Object.FindFirstObjectByType<BattleSession>();
                var towers = session.Towers.Where(t => !t.IsWall).ToArray();
                if (towers.Length != Layouts[level - 1].Length) throw new Exception("Unexpected tower count in level " + level);
                if (session.GetComponent<TowerSpecialEffects>() == null) session.gameObject.AddComponent<TowerSpecialEffects>();
                report += $"Level {level}: {towers.Length} towers\n";
                for (var i = 0; i < towers.Length; i++)
                {
                    var tower = towers[i];
                    var serialized = new SerializedObject(tower);
                    var kind = Layouts[level - 1][i];
                    serialized.FindProperty("kind").enumValueIndex = (int)kind;
                    serialized.FindProperty("isAoe").boolValue = kind == TowerKind.Blast;
                    // Preserve the original normal/sentry directions, HP, footprint and rewards.
                    if (kind == TowerKind.Laser)
                    {
                        if (serialized.FindProperty("attackArea").enumValueIndex == 0) serialized.FindProperty("attackArea").enumValueIndex = 2;
                        serialized.FindProperty("damage").floatValue = 5f * (level <= 2 ? 1f : level == 3 ? 1.5f : level == 4 ? 2f : level == 7 ? 4f : 3f);
                        serialized.FindProperty("attackInterval").floatValue = 3f;
                        serialized.FindProperty("attackWidth").floatValue = .9f;
                    }
                    if (kind == TowerKind.Frost)
                    {
                        serialized.FindProperty("attackArea").enumValueIndex = 0;
                        serialized.FindProperty("range").floatValue = 3.5f;
                        serialized.FindProperty("damage").floatValue = level == 4 ? 2f : level == 7 ? 4f : 3f;
                        serialized.FindProperty("attackInterval").floatValue = 1f;
                    }
                    if (kind == TowerKind.Blast)
                    {
                        serialized.FindProperty("attackArea").enumValueIndex = 0;
                        serialized.FindProperty("damage").floatValue = level == 7 ? 4f : 3f;
                        serialized.FindProperty("attackInterval").floatValue = 1f;
                    }
                    if ((level == 5 && i == 2) || (level == 6 && i == 2) || (level == 7 && i == 4))
                        serialized.FindProperty("attackArea").enumValueIndex = 2;
                    // Late levels trade raw burst damage for overlapping mechanics.
                    if (kind == TowerKind.Standard)
                    {
                        serialized.FindProperty("attackInterval").floatValue = 1f;
                        if (level >= 5) serialized.FindProperty("damage").floatValue = level == 7 ? 4f : 3f;
                    }
                    // Ensure all-direction sentry remains represented from level 3 onward.
                    if ((level == 5 && i == 4) || (level == 6 && i == 5) || (level == 7 && i == 6))
                    {
                        serialized.FindProperty("attackArea").enumValueIndex = 0;
                        serialized.FindProperty("range").floatValue = 3.5f;
                    }
                    if (kind == TowerKind.Laser || (kind == TowerKind.Standard && serialized.FindProperty("attackArea").enumValueIndex != 0))
                        AimAtRoad(session, tower, serialized, kind == TowerKind.Laser);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var definition = tower.CaptureDefinition(i + 1);
                    var name = TowerStyles.Name(definition);
                    tower.name = $"{name} Tower {i + 1}";
                    report += $"  {i+1}: {name}, damage={definition.Damage}, interval={definition.AttackInterval}, area={definition.Area}\n";
                }
                EditorSceneManager.SaveScene(scene);
            }
            // The authoring template remains a faithful copy of level 2's tower rules.
            var template = EditorSceneManager.OpenScene("Assets/Rebuild/Scenes/LevelTemplate.unity");
            var templateTowers = UnityEngine.Object.FindFirstObjectByType<BattleSession>().Towers.Where(t => !t.IsWall).ToArray();
            if (templateTowers.Length == 2)
            {
                var serialized = new SerializedObject(templateTowers[1]);
                var sourcePath = BattleSceneFlow.LevelTwoScene;
                var sourceScene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
                var source = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSession>()).Single().Towers.Where(t => !t.IsWall).ElementAt(1);
                var sourceData = new SerializedObject(source);
                foreach (var field in new[] { "kind", "damage", "attackInterval", "attackWidth", "range", "attackArea" })
                    serialized.CopyFromSerializedProperty(sourceData.FindProperty(field));
                EditorSceneManager.CloseScene(sourceScene, true);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(template);
            }
            var start = EditorSceneManager.OpenScene(BattleSceneFlow.StartScene);
            var menu = UnityEngine.Object.FindFirstObjectByType<LegacySceneMenu>();
            if (menu.GetComponent<BeginningPage>() == null) menu.gameObject.AddComponent<BeginningPage>();
            EditorSceneManager.SaveScene(start);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TowerPlaytestLayout.txt", report);
            Debug.Log(report);
        }
        private static void AimAtRoad(BattleSession session, BattleTower tower, SerializedObject data, bool laser)
        {
            var roads = session.Grid.SceneCells.Where(c => c.Kind == CellKind.Road || c.Kind == CellKind.Spawn || c.Kind == CellKind.Goal).ToArray();
            var width = data.FindProperty("attackWidth").floatValue;
            var range = laser ? 200f : data.FindProperty("range").floatValue;
            var best = (AttackArea)data.FindProperty("attackArea").enumValueIndex;
            int Score(AttackArea area) => roads.Count(c => SquadCombatRules.Contains(c.transform.position - tower.transform.position, area, range, width));
            var bestScore = Score(best);
            foreach (var area in new[] { AttackArea.PositiveX, AttackArea.NegativeX, AttackArea.PositiveZ, AttackArea.NegativeZ })
                if (Score(area) > bestScore) { bestScore = Score(area); best = area; }
            if (bestScore == 0) throw new Exception($"{tower.name}: no walkable road inside any attack lane");
            data.FindProperty("attackArea").enumValueIndex = (int)best;
            var direction = SquadCombatRules.Direction(best);
            if (laser)
                data.FindProperty("range").floatValue = roads.Where(c => SquadCombatRules.Contains(c.transform.position - tower.transform.position, best, 200, width))
                    .Max(c => Vector3.Dot(c.transform.position - tower.transform.position, direction)) + .5f;
            var yaw = best == AttackArea.NegativeX ? 0 : best == AttackArea.PositiveX ? 180 : best == AttackArea.PositiveZ ? 90 : -90;
            var model = tower.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("DirectionalTower_Visual"));
            if (model != null) model.rotation = Quaternion.Euler(0, yaw, 0);
            else tower.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        public static void UpdateLaserDamageAndBuild()
        {
            for (var level = 2; level <= 7; level++)
            {
                var scene = EditorSceneManager.OpenScene(BattleSceneFlow.SceneForLevel(level));
                var towers = UnityEngine.Object.FindFirstObjectByType<BattleSession>().Towers;
                var normalDamage = towers.First(t => !t.IsWall && t.Kind == TowerKind.Standard && t.CaptureDefinition(1).Area != AttackArea.Square).Damage;
                foreach (var tower in towers.Where(t => t.Kind == TowerKind.Laser))
                {
                    var data = new SerializedObject(tower);
                    data.FindProperty("damage").floatValue = normalDamage * 5f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    if (tower.Damage != normalDamage * 5f) throw new Exception("Laser damage mismatch");
                }
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Laser damage only: Level {level} = {normalDamage * 5f}");
            }
            var template = EditorSceneManager.OpenScene("Assets/Rebuild/Scenes/LevelTemplate.unity");
            foreach (var tower in UnityEngine.Object.FindFirstObjectByType<BattleSession>().Towers.Where(t => t.Kind == TowerKind.Laser))
            {
                var data = new SerializedObject(tower);
                data.FindProperty("damage").floatValue = 5f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(template);
            EditorSceneManager.OpenScene(BattleSceneFlow.StartScene);
            BuildMac();
        }
        public static void BuildMac()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.MaximizedWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultIsNativeResolution = true;
            var output = Path.GetFullPath("../Playtest/Reverse TD - Tower Playtest v6.app");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneOSX, locationPathName = output, options = BuildOptions.None
            });
            Debug.Log($"Local playtest build: {result.summary.result} ({result.summary.totalErrors} errors) {output}");
            EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
