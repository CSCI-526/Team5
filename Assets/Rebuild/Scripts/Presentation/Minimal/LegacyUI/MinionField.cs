using System;
using System.Collections.Generic;
using System.Globalization;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    [DisallowMultipleComponent]
    public sealed class MinionField : MonoBehaviour
    {
        [SerializeField, Tooltip("Simulation seconds the squad may stand still without hurting anything before a minion next to it steps aside.")]
        private float stuckSeconds = 3f;
        [SerializeField, Tooltip("How far a minion that steps aside is lifted, in world units, so the battle no longer sees it.")]
        private float liftHeight = 1000f;
        [SerializeField, Tooltip("A minion only steps aside when a living unit is at most this far from its footprint, in tiles.")]
        private float nearDistance = 1f;

        private readonly List<MinionPost> posts = new List<MinionPost>();
        private readonly List<Material> materials = new List<Material>();
        private BattleSession session;
        private double lastActivity;
        private float lastProgress = -1f;
        private float lastTowerHp = float.MaxValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForFirstScene() => TryCreate(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || BattleSceneFlow.IsPreviewScene(scene)) return;
            BattleSession found = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<MinionField>() != null) return;
                if (found == null) found = root.GetComponentInChildren<BattleSession>(true);
            }
            if (found == null || !found.SquadCombat || found.Grid == null || found.Route == null || found.Simulation != null) return;
            if (!MinionRules.HasMinions(found.DisplayedLevelNumber)) return;
            var host = new GameObject("Minions");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<MinionField>().Build(found);
        }

        private void Build(BattleSession battleSession)
        {
            session = battleSession;
            var errors = new List<string>();
            session.Grid.RebuildIndex(errors);
            var occupied = new HashSet<Vector3Int>();
            var definitions = new List<TowerDefinition>();
            var existing = session.Towers;
            for (var i = 0; i < existing.Count; i++)
            {
                var tower = existing[i];
                if (tower == null) continue;
                tower.CollectFootprintErrors(session.Grid, occupied, errors);
                if (!tower.HasValidConfiguration) continue;
                try { definitions.Add(tower.CaptureDefinition(i + 1)); }
                catch (ArgumentException) { }
            }
            Vector3[] route;
            try { route = session.Route.CaptureTopCenters(); }
            catch (Exception) { return; }
            FindParts(out var mesh, out var material);
            var level = session.DisplayedLevelNumber;
            var hp = MinionRules.Hp(level);
            var damage = MinionRules.Damage(level);
            foreach (var spot in MinionPlanner.Plan(route, definitions, level))
                foreach (var cell in spot.Cells)
                {
                    var post = Spawn(cell, hp, damage, occupied, mesh, material);
                    if (post != null) posts.Add(post);
                }
            session.SimulationCreated += OnSimulationCreated;
        }

        private MinionPost Spawn(Vector3 cell, float hp, float damage, HashSet<Vector3Int> occupied, Mesh mesh, Material material)
        {
            var home = new Vector3(Mathf.Round(cell.x), cell.y, Mathf.Round(cell.z));
            var minion = new GameObject("Minion " + Mathf.RoundToInt(home.x) + "," + Mathf.RoundToInt(home.z));
            minion.transform.SetParent(transform, false);
            minion.transform.SetPositionAndRotation(home, Quaternion.identity);
            var tower = minion.AddComponent<BattleTower>();
            JsonUtility.FromJsonOverwrite("{\"isWall\":true,\"isMinion\":true,\"maxHp\":" + Number(hp) + ",\"damage\":" + Number(damage) +
                ",\"range\":" + Number(MinionRules.AttackRange) + ",\"attackInterval\":" + Number(MinionRules.AttackInterval) +
                ",\"targetRule\":" + (int)TargetRule.Nearest + ",\"attackArea\":" + (int)AttackArea.Square + ",\"attackWidth\":0.9}", tower);
            var errors = new List<string>();
            var trial = new HashSet<Vector3Int>(occupied);
            if (tower.HasValidConfiguration) tower.CollectFootprintErrors(session.Grid, trial, errors);
            else errors.Add("invalid minion stats");
            if (errors.Count > 0)
            {
                JsonUtility.FromJsonOverwrite("{\"isWall\":false}", tower);
                minion.SetActive(false);
                Destroy(minion);
                Debug.LogWarning("Minion skipped at " + home + ": " + errors[0], this);
                return null;
            }
            occupied.UnionWith(trial);
            var look = MinionLook.Build(minion.transform, mesh, material, materials);
            return new MinionPost(tower, home, tower.Footprint, look);
        }

        private static string Number(float value) => value.ToString(CultureInfo.InvariantCulture);

        private void FindParts(out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;
            foreach (var root in session.gameObject.scene.GetRootGameObjects())
            {
                var presentation = root.GetComponentInChildren<BattlePresentation>(true);
                if (presentation == null || presentation.UnitPrefab == null) continue;
                var filter = presentation.UnitPrefab.GetComponentInChildren<MeshFilter>(true);
                var renderer = presentation.UnitPrefab.GetComponentInChildren<MeshRenderer>(true);
                if (filter == null || renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
                mesh = filter.sharedMesh;
                material = renderer.sharedMaterial;
                return;
            }
            var cell = session.Grid.GetComponentInChildren<MeshRenderer>(true);
            var cellFilter = cell != null ? cell.GetComponent<MeshFilter>() : null;
            if (cellFilter == null) return;
            mesh = cellFilter.sharedMesh;
            material = cell.sharedMaterial;
        }

        private void OnSimulationCreated(BattleSimulation next)
        {
            lastActivity = 0;
            lastProgress = -1f;
            lastTowerHp = float.MaxValue;
            foreach (var post in posts) post.Restore();
        }

        private void Update()
        {
            if (session == null) return;
            var simulation = session.Simulation;
            if (simulation == null) return;
            foreach (var post in posts) post.Animate(Time.time, Time.deltaTime);
            if (simulation.Outcome != BattleOutcome.Running || simulation.IsPaused) return;
            var progress = -1f;
            for (var i = 0; i < simulation.Units.Count; i++)
                if (simulation.Units[i].IsAlive) progress = Mathf.Max(progress, simulation.Units[i].RouteProgress);
            var towerHp = 0f;
            for (var i = 0; i < simulation.Towers.Count; i++) towerHp += Mathf.Max(0f, simulation.Towers[i].Hp);
            var time = simulation.ElapsedTime;
            if (progress > lastProgress + 0.0001f || towerHp < lastTowerHp - 0.0001f) lastActivity = time;
            lastProgress = Mathf.Max(lastProgress, progress);
            lastTowerHp = towerHp;
            if (time - lastActivity < stuckSeconds) return;
            lastActivity = time;
            foreach (var post in posts)
                if (!post.Lifted && post.IsAlive && NearSquad(post, simulation)) post.StepAside(liftHeight);
        }

        private bool NearSquad(MinionPost post, BattleSimulation simulation)
        {
            for (var i = 0; i < simulation.Units.Count; i++)
            {
                var unit = simulation.Units[i];
                if (unit.IsAlive && post.DistanceTo(unit.Position) <= nearDistance) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (session != null) session.SimulationCreated -= OnSimulationCreated;
            foreach (var material in materials)
                if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
