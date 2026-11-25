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
        [SerializeField, Tooltip("Real seconds a first-encounter hint stays on screen.")]
        private float hintSeconds = 5f;
        [SerializeField, Tooltip("Tiles between a living unit and a living shield bearer when the bearer hint first shows.")]
        private float noticeDistance = 5f;

        private readonly List<MinionPost> posts = new List<MinionPost>();
        private readonly List<MinionShieldView> shields = new List<MinionShieldView>();
        private readonly List<MinionLobber> lobbers = new List<MinionLobber>();
        private readonly List<MinionGuard> guards = new List<MinionGuard>();
        private readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> unitBlocked = new HashSet<Vector2Int>();
        private readonly Dictionary<int, MinionPost> postsById = new Dictionary<int, MinionPost>();
        private double lastTime = -1d;
        private MinionMaterials materials;
        private MinionHint hint;
        private BattleSession session;
        private BattleSimulation bound;

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
            materials = new MinionMaterials();
            var baseline = session.ValidateConfiguration().Count;
            Vector3[] route;
            try { route = session.Route.CaptureTopCenters(); }
            catch (Exception) { return; }
            var level = session.DisplayedLevelNumber;
            var towers = new List<BattleTower>(session.Towers);
            var definitions = new List<TowerDefinition>();
            var occupied = new HashSet<Vector3Int>();
            var ignored = new List<string>();
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null) continue;
                tower.CollectFootprintErrors(session.Grid, occupied, ignored);
                if (!tower.HasValidConfiguration) continue;
                try { definitions.Add(session.CaptureTowerDefinition(tower, i + 1, route)); }
                catch (ArgumentException) { }
            }
            var sites = MinionSiteScan.Scan(session);
            foreach (var placement in MinionPlanner.Plan(route, definitions, level, sites))
                foreach (var cell in placement.Cells)
                {
                    var post = Spawn(placement, cell, level, occupied);
                    if (post != null) posts.Add(post);
                }
            if (posts.Count > 0 && session.ValidateConfiguration().Count > baseline)
            {
                Debug.LogWarning("Minions removed because they made the battle configuration invalid.", this);
                foreach (var post in posts)
                    if (post.Tower != null) DestroyImmediate(post.Tower.gameObject);
                posts.Clear();
                session.ValidateConfiguration();
            }
            BuildViews(towers, level, sites);
            hint = new MinionHint(transform, hintSeconds);
            session.SimulationCreated += OnSimulationCreated;
            if (session.Simulation != null) OnSimulationCreated(session.Simulation);
        }

        private MinionPost Spawn(MinionPlacement placement, Vector3 cell, int level, HashSet<Vector3Int> occupied)
        {
            var bearer = placement.Kind == MinionKind.Bearer;
            var home = new Vector3(Mathf.Round(cell.x), cell.y, Mathf.Round(cell.z));
            var minion = new GameObject((bearer ? "Bearer " : "Lobber ") + Mathf.RoundToInt(home.x) + "," + Mathf.RoundToInt(home.z));
            minion.transform.SetParent(transform, false);
            minion.transform.SetPositionAndRotation(home, Quaternion.identity);
            var tower = minion.AddComponent<BattleTower>();
            JsonUtility.FromJsonOverwrite(bearer ? BearerJson(level, placement.TowerId) : LobberJson(level), tower);
            var errors = new List<string>();
            var trial = new HashSet<Vector3Int>(occupied);
            if (tower.HasValidConfiguration) tower.CollectFootprintErrors(session.Grid, trial, errors);
            else errors.Add("invalid minion stats");
            if (errors.Count > 0)
            {
                Debug.LogWarning("Minion skipped at " + home + ": " + errors[0], this);
                DestroyImmediate(minion);
                return null;
            }
            occupied.UnionWith(trial);
            var facing = bearer ? home - placement.TowerPosition : placement.TowerPosition - home;
            var rig = MinionLook.Build(minion.transform, placement.Kind, facing, materials, placement.Element);
            return new MinionPost(tower, placement.Kind, bearer ? placement.TowerId : 0, rig, placement.Element);
        }

        private void BuildViews(IReadOnlyList<BattleTower> towers, int level, MinionSites sites)
        {
            var bearersByTower = new Dictionary<int, List<MinionPost>>();
            var elemental = false;
            foreach (var post in posts)
            {
                if (post.Kind == MinionKind.Lobber)
                {
                    lobbers.Add(new MinionLobber(transform, post, MinionRules.BombDamage(level), materials, post.Element));
                    elemental |= post.Element != MinionElement.None;
                    continue;
                }
                if (post.GuardedTowerId <= 0) continue;
                if (!bearersByTower.TryGetValue(post.GuardedTowerId, out var list)) bearersByTower[post.GuardedTowerId] = list = new List<MinionPost>();
                list.Add(post);
            }
            foreach (var pair in bearersByTower)
            {
                var index = pair.Key - 1;
                if (index < 0 || index >= towers.Count || towers[index] == null) continue;
                var tower = towers[index].transform.position;
                shields.Add(new MinionShieldView(transform, tower, pair.Key, pair.Value, materials));
                guards.Add(new MinionGuard(tower, pair.Value, sites));
            }
            if (elemental) gameObject.AddComponent<UnitStatusView>().Bind(session, materials);
        }

        private static string BearerJson(int level, int towerId) =>
            "{\"isWall\":true,\"isMinion\":true,\"maxHp\":" + Number(MinionRules.Hp(MinionKind.Bearer, level)) +
            ",\"damage\":" + Number(MinionRules.BearerDamage(level)) + ",\"range\":" + Number(MinionRules.BearerRange) +
            ",\"attackInterval\":" + Number(MinionRules.BearerInterval) + ",\"targetRule\":" + (int)TargetRule.Nearest +
            ",\"attackArea\":" + (int)AttackArea.Circle + ",\"attackWidth\":0.9,\"shieldsTowerId\":" + towerId +
            ",\"shieldDamageScale\":" + Number(MinionRules.ShieldDamageScale) + "}";

        private static string LobberJson(int level) =>
            "{\"isWall\":true,\"isMinion\":true,\"maxHp\":" + Number(MinionRules.Hp(MinionKind.Lobber, level)) +
            ",\"damage\":0,\"range\":0,\"attackInterval\":" + Number(MinionRules.LobberInterval) +
            ",\"targetRule\":" + (int)TargetRule.Nearest + ",\"attackArea\":" + (int)AttackArea.Circle + ",\"attackWidth\":0.9}";

        private static string Number(float value) => value.ToString(CultureInfo.InvariantCulture);

        private void OnSimulationCreated(BattleSimulation next)
        {
            Unbind();
            postsById.Clear();
            lastTime = -1d;
            foreach (var post in posts) post.Restore();
            foreach (var shield in shields) shield.Reset();
            foreach (var lobber in lobbers) lobber.Reset();
            foreach (var guard in guards) guard.Reset();
            if (next == null) return;
            bound = next;
            foreach (var post in posts)
            {
                if (post.Tower != null && post.Tower.RuntimeState != null)
                    postsById[post.Tower.RuntimeState.Id] = post;
                if (post.GuardedTowerId > 0 && (post.GuardedTowerId > next.Towers.Count || next.Towers[post.GuardedTowerId - 1].Definition.IsWall))
                    Debug.LogWarning(post.Tower.name + " guards tower " + post.GuardedTowerId + ", which is not a tower in this battle.", this);
            }
            next.TowerAttacked += OnTowerAttacked;
            next.TowerDamaged += OnTowerDamaged;
        }

        private void Unbind()
        {
            if (bound == null) return;
            bound.TowerAttacked -= OnTowerAttacked;
            bound.TowerDamaged -= OnTowerDamaged;
            bound = null;
        }

        private void OnTowerAttacked(TowerAttackEvent attack)
        {
            if (postsById.TryGetValue(attack.TowerId, out var post)) post.Kick();
        }

        private void OnTowerDamaged(TowerDamageEvent damage)
        {
            if (bound == null) return;
            foreach (var shield in shields)
                if (shield.TowerId == damage.TowerId && bound.IsTowerShielded(damage.TowerId)) shield.Flash();
        }

        private void Update()
        {
            if (session == null) return;
            var simulation = session.Simulation;
            if (simulation == null)
            {
                foreach (var lobber in lobbers) lobber.Reset();
                foreach (var shield in shields) shield.Reset();
                return;
            }
            var time = Time.time;
            var delta = Time.deltaTime;
            var now = simulation.ElapsedTime;
            var simulated = lastTime < 0d ? 0f : Mathf.Max(0f, (float)(now - lastTime));
            lastTime = now;
            if (simulation.Outcome == BattleOutcome.Running && !simulation.IsPaused) Guard(simulation);
            for (var i = 0; i < posts.Count; i++) posts[i].Animate(time, delta, simulated, i);
            foreach (var shield in shields) shield.Tick(simulation, time, delta);
            foreach (var lobber in lobbers)
            {
                lobber.Tick(simulation, time, delta);
                if (lobber.MarkedThisFrame && hint != null) hint.Show(MinionKind.Lobber, lobber.Element);
            }
            if (hint == null) return;
            if (NearBearer(simulation)) hint.Show(MinionKind.Bearer);
            hint.Tick(Time.unscaledDeltaTime);
        }

        private void Guard(BattleSimulation simulation)
        {
            if (guards.Count == 0) return;
            blocked.Clear();
            unitBlocked.Clear();
            var sum = Vector3.zero;
            var alive = 0;
            foreach (var unit in simulation.Units)
            {
                if (!unit.IsAlive) continue;
                sum += unit.Position;
                alive++;
                var radius = unit.CollisionRadius + .005f;
                for (var x = Mathf.FloorToInt(unit.Position.x - radius + .5f); x <= Mathf.FloorToInt(unit.Position.x + radius + .5f); x++)
                    for (var z = Mathf.FloorToInt(unit.Position.z - radius + .5f); z <= Mathf.FloorToInt(unit.Position.z + radius + .5f); z++)
                        unitBlocked.Add(new Vector2Int(x, z));
            }
            if (alive == 0) return;
            blocked.UnionWith(unitBlocked);
            foreach (var post in posts)
                if (post.IsAlive) blocked.Add(post.Cell);
            var squad = sum / alive;
            foreach (var guard in guards) guard.Tick(squad, blocked, unitBlocked);
        }

        private bool NearBearer(BattleSimulation simulation)
        {
            foreach (var post in posts)
            {
                if (post.Kind != MinionKind.Bearer || !post.IsAlive) continue;
                var position = post.Position;
                foreach (var unit in simulation.Units)
                    if (unit.IsAlive && MinionBombRules.FlatDistanceSquared(unit.Position, position) <= noticeDistance * noticeDistance) return true;
            }
            return false;
        }

        private void OnDisable()
        {
            foreach (var lobber in lobbers) lobber.Reset();
            foreach (var shield in shields) shield.Reset();
            lastTime = -1d;
        }

        private void OnDestroy()
        {
            Unbind();
            if (session != null) session.SimulationCreated -= OnSimulationCreated;
            if (materials != null) materials.Dispose();
        }
    }
}
