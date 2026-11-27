using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    // Stats-only update: never create/delete/move a tower or regenerate a level.
    public static class CampaignTowerBalanceInstaller
    {
        private const string CatalogPath = "Assets/Rebuild/Editor/StageV3StageCatalog.json";
        private static readonly string[] Paths = { BattleSceneFlow.BattleScene, BattleSceneFlow.LevelTwoScene,
            BattleSceneFlow.LevelThreeScene, BattleSceneFlow.LevelFourScene, BattleSceneFlow.LevelFiveScene,
            BattleSceneFlow.LevelSixScene, BattleSceneFlow.LevelSevenScene };
        [Serializable] private sealed class Catalog { public Stage[] stages; }
        [Serializable] private sealed class Stage { public int stage; public Tower[] towers; }
        [Serializable] private sealed class Tower { public int x, z; public string shape; public float range, hp, damage, interval; }

        [MenuItem("Team5/Rebuild/Apply Campaign Tower Balance (Stats Only)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
            if (catalog?.stages == null || catalog.stages.Length != Paths.Length ||
                !catalog.stages.Select(s => s.stage).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, Paths.Length)))
                throw new InvalidDataException("Expected seven unique campaign stages.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Preflight every map before touching any saved scene.
                foreach (var plan in catalog.stages)
                    Match(EditorSceneManager.OpenScene(Paths[plan.stage - 1], OpenSceneMode.Single), plan);
                var backup = "Logs/CampaignTowerBalanceBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                Directory.CreateDirectory(backup);
                foreach (var path in Paths) File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), false);
                foreach (var plan in catalog.stages.OrderBy(s => s.stage))
                {
                    var path = Paths[plan.stage - 1];
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var matched = Match(scene, plan);
                    for (var i = 0; i < matched.Length; ++i)
                    {
                        var component = matched[i]; var stat = plan.towers[i];
                        var data = new SerializedObject(component);
                        Set(data, "maxHp", stat.hp); Set(data, "damage", stat.damage); Set(data, "attackInterval", stat.interval);
                        data.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                        EditorUtility.SetDirty(component);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path + ". Original scenes backed up at " + backup);
                    Debug.Log($"Level {plan.stage}: applied final tower stats to {matched.Length} towers.");
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Campaign tower balance applied to 7/7 stages. Scene backups: " + backup);
            }
            finally { if (previous != null && previous.Length != 0) EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static BattleTower[] Match(Scene scene, Stage plan)
        {
            var roots = scene.GetRootGameObjects();
            var sessions = roots.SelectMany(r => r.GetComponentsInChildren<BattleSession>(true)).ToArray();
            var towers = roots.SelectMany(r => r.GetComponentsInChildren<BattleTower>(true)).Where(t => !t.IsWall).ToArray();
            if (sessions.Length != 1 || sessions[0].DisplayedLevelNumber != plan.stage || plan.towers == null ||
                towers.Length != plan.towers.Length || towers.Any(t => !sessions[0].Towers.Contains(t)))
                throw Mismatch(scene, "tower count, registration, or session mismatch");
            var matched = new BattleTower[plan.towers.Length];
            for (var i = 0; i < matched.Length; ++i)
            {
                var p = plan.towers[i];
                if (!float.IsFinite(p.hp) || p.hp <= 0 || !float.IsFinite(p.damage) || p.damage <= 0 ||
                    !float.IsFinite(p.interval) || p.interval <= 0 || !Enum.TryParse<AttackArea>(p.shape, out var area))
                    throw new InvalidDataException("Invalid balance values at stage " + plan.stage);
                var candidates = towers.Where(t => Mathf.Abs(t.transform.position.x - p.x) < .01f &&
                    Mathf.Abs(t.transform.position.z - p.z) < .01f).ToArray();
                if (candidates.Length != 1) throw Mismatch(scene, $"missing or duplicate tower at ({p.x}, {p.z})");
                var tower = candidates[0]; var def = tower.CaptureDefinition(i + 1);
                if (def.Area != area || Mathf.Abs(def.Range - p.range) > .01f || def.IsAoe || matched.Contains(tower))
                    throw Mismatch(scene, $"shape/range mismatch at ({p.x}, {p.z})");
                matched[i] = tower;
            }
            return matched;
        }

        private static InvalidOperationException Mismatch(Scene scene, string reason) => new InvalidOperationException(
            $"{scene.name}: {reason}. Apply the latest V6 battlefield layout first using Rebuild Tactical Stages V6 - Battlefield Polish (1-7). No stats-only changes were saved during preflight.");
        private static void Set(SerializedObject so, string key, float value)
        {
            var property = so.FindProperty(key);
            if (property == null) throw new InvalidDataException("Missing serialized BattleTower field: " + key);
            property.floatValue = value;
        }
    }
}
