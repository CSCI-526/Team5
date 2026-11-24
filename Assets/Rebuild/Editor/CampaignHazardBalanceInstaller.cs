using System;
using System.IO;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor
{
    // Stats-only update: never create/delete/move a hazard or regenerate a level.
    public static class CampaignHazardBalanceInstaller
    {
        private const string CatalogPath = "Assets/Rebuild/Editor/StageV3StageCatalog.json";
        private static readonly string[] Paths = { BattleSceneFlow.BattleScene, BattleSceneFlow.LevelTwoScene,
            BattleSceneFlow.LevelThreeScene, BattleSceneFlow.LevelFourScene, BattleSceneFlow.LevelFiveScene,
            BattleSceneFlow.LevelSixScene, BattleSceneFlow.LevelSevenScene };
        [Serializable] private sealed class Catalog { public Stage[] stages; }
        [Serializable] private sealed class Stage { public int stage; public Hazard[] hazards; }
        [Serializable] private sealed class Hazard { public int x, z; public string kind; public float radius, damage, interval; }

        [MenuItem("Team5/Rebuild/Apply Campaign Hazard Balance (Stats Only)")]
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
                var backup = "Logs/CampaignHazardBalanceBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                Directory.CreateDirectory(backup);
                foreach (var path in Paths) File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), false);
                foreach (var plan in catalog.stages.OrderBy(s => s.stage))
                {
                    var path = Paths[plan.stage - 1];
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var matched = Match(scene, plan);
                    for (var i = 0; i < matched.Length; ++i)
                    {
                        var component = matched[i]; var stat = plan.hazards[i];
                        var data = new SerializedObject(component);
                        Set(data, "damage", stat.damage);
                        data.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                        EditorUtility.SetDirty(component);
                    }
                    if (matched.Length == 0) continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path + ". Original scenes backed up at " + backup);
                    Debug.Log($"Level {plan.stage}: applied damage to {matched.Length} hazards.");
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Campaign hazard balance applied to 7/7 stages. Scene backups: " + backup);
            }
            finally { if (previous != null && previous.Length != 0) EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static OpenStageThreat[] Match(Scene scene, Stage plan)
        {
            var roots = scene.GetRootGameObjects();
            var sessions = roots.SelectMany(r => r.GetComponentsInChildren<BattleSession>(true)).ToArray();
            var hazards = roots.SelectMany(r => r.GetComponentsInChildren<OpenStageThreat>(true)).ToArray();
            if (sessions.Length != 1 || sessions[0].DisplayedLevelNumber != plan.stage || plan.hazards == null ||
                hazards.Length != plan.hazards.Length)
                throw Mismatch(scene, "hazard count or session mismatch");
            var matched = new OpenStageThreat[plan.hazards.Length];
            for (var i = 0; i < matched.Length; ++i)
            {
                var p = plan.hazards[i];
                if (!float.IsFinite(p.damage) || p.damage <= 0 || !float.IsFinite(p.radius) || p.radius <= 0 ||
                    !float.IsFinite(p.interval) || p.interval <= 0 || (p.kind != "Trap" && p.kind != "Sentry"))
                    throw new InvalidDataException("Invalid hazard balance values at stage " + plan.stage);
                var kind = p.kind == "Trap" ? OpenStageThreat.ThreatKind.GroundTrap : OpenStageThreat.ThreatKind.Sentry;
                var candidates = hazards.Where(t => Mathf.Abs(t.transform.position.x - p.x) < .01f &&
                    Mathf.Abs(t.transform.position.z - p.z) < .01f && t.Kind == kind).ToArray();
                if (candidates.Length != 1) throw Mismatch(scene, $"missing or duplicate hazard at ({p.x}, {p.z})");
                var hazard = candidates[0];
                var data = new SerializedObject(hazard);
                var interval = data.FindProperty("hitInterval");
                if (Mathf.Abs(hazard.Radius - p.radius) > .01f || interval == null ||
                    Mathf.Abs(interval.floatValue - p.interval) > .01f || matched.Contains(hazard))
                    throw Mismatch(scene, $"radius/interval mismatch at ({p.x}, {p.z})");
                if (data.FindProperty("damage") == null) throw new InvalidDataException("Missing hazard damage field.");
                matched[i] = hazard;
            }
            return matched;
        }

        private static InvalidOperationException Mismatch(Scene scene, string reason) => new InvalidOperationException(
            $"{scene.name}: {reason}. Apply the latest V4 layout first using Rebuild Tactical Stages V4 (1-7). No stats-only changes were saved during preflight.");
        private static void Set(SerializedObject so, string key, float value)
        {
            var property = so.FindProperty(key);
            if (property == null) throw new InvalidDataException("Missing serialized OpenStageThreat field: " + key);
            property.floatValue = value;
        }
    }
}
