using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Editor.LevelAuthoring;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    // The catalog contains all authored geometry and encounters. The editor applies it to actual
    // campaign .unity scenes; this source patch does not pretend to include those scene assets.
    public static class ClickMoveStageInstaller
    {
        private const string CatalogPath = "Assets/Rebuild/Editor/StageV3StageCatalog.json";
        private const string BackupFolder = "Logs/StageV7RebuildBackups";
        private const string EncounterRoot = "Stage V3 Encounters";
        private const string LandmarkRoot = "Stage V3 Landmarks";
        private static readonly string[] Scenes =
        {
            BattleSceneFlow.BattleScene, BattleSceneFlow.LevelTwoScene,
            BattleSceneFlow.LevelThreeScene, BattleSceneFlow.LevelFourScene,
            BattleSceneFlow.LevelFiveScene, BattleSceneFlow.LevelSixScene,
            BattleSceneFlow.LevelSevenScene
        };

        [Serializable] private sealed class StageCatalog { public StagePlan[] stages; }
        [Serializable] private sealed class StagePlan
        {
            public int stage, width, height, roadWidth;
            public string name;
            public bool corridor;
            public int[] route, cover;
            public TowerPlan[] towers;
            public ThreatPlan[] hazards;
        }
        [Serializable] private sealed class TowerPlan
        {
            public int x, z;
            public string shape;
            public float range, damage, interval, hp;
            public bool sniperOutpost;
        }
        [Serializable] private sealed class ThreatPlan
        {
            public int x, z, dx, dz;
            public string kind;
            public float radius, damage, interval;
        }

        [MenuItem("Team5/Rebuild/Rebuild Tactical Stages V7 - Meadow (1-7)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before rebuilding levels.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var catalog = ReadCatalog();
            Directory.CreateDirectory(BackupFolder);
            var completed = 0;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            // Validate the entire catalog before changing any saved scene.
            foreach (var plan in catalog.stages)
            {
                var errors = ValidateCatalogGeometry(plan.stage);
                if (errors.Count != 0) throw new InvalidDataException(string.Join(" | ", errors));
            }
            try
            {
            for (var stage = 1; stage <= 7; stage++)
            {
                try
                {
                    var plan = catalog.stages.Single(p => p.stage == stage);
                    ApplyStage(plan);
                    completed++;
                }
                catch (Exception error)
                {
                    Debug.LogError("Stage V7 " + stage + " failed. Its scene was not saved: " + error);
                    if (File.Exists(Scenes[stage - 1])) EditorSceneManager.OpenScene(Scenes[stage - 1], OpenSceneMode.Single);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Stage V7 completed {completed}/7 stages. Review all scene errors and Play Mode behavior before shipping.");
            }
            finally
            {
                if (previous != null && previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static StageCatalog ReadCatalog()
        {
            if (!File.Exists(CatalogPath)) throw new FileNotFoundException("Stage V3 layout catalog not installed", CatalogPath);
            var data = JsonUtility.FromJson<StageCatalog>(File.ReadAllText(CatalogPath));
            if (data == null || data.stages == null || data.stages.Length != 7 ||
                data.stages.Select(p => p.stage).Distinct().Count() != 7)
                throw new InvalidDataException("Stage V3 catalog must contain exactly one layout for stages 1-7.");
            return data;
        }

        private static void ApplyStage(StagePlan plan)
        {
            var path = Scenes[plan.stage - 1];
            var existing = File.Exists(path);
            if (existing)
            {
                var backup = BackupFolder + "/" + Path.GetFileNameWithoutExtension(path) + "_" +
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
                File.Copy(path, backup, false);
            }
            var draft = BuildDraft(plan);
            try
            {
                Scene scene;
                BattleSession session;
                if (existing)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    // Preserve the scene's own BattleRig, HUD, roster and tutorial bindings.
                    // Never silently replace a configured scene with a generic rig.
                    // L5-7 are BattleRig prefab instances with added tile/tower prefabs.
                    // Unpack only the outer rig so Unity permits removal of its old map children.
                    // Nested tile/tower prefab links and resolved scene overrides are retained.
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.GetComponentInChildren<BattleSession>(true) != null &&
                            PrefabUtility.IsAnyPrefabInstanceRoot(root))
                            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    ClearLegacyDecor(scene);
                    session = MapBuilderUtility.ApplyToScene(draft, scene);
                }
                else
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    session = MapBuilderUtility.Populate(draft, scene);
                }
                ConfigureSession(session, plan.stage);
                ConfigureTowers(scene, session, plan);
                ClearLegacyDecor(scene);
                ReplaceRoadArtwork(scene);
                if (plan.stage <= 2) RemoveRailArtwork(scene);
                CreateEncounters(scene, draft, plan);
                // The runtime objective portal is the sole battle landmark; preview pins are separate.
                StageV33VisualPolish.Polish(scene, plan.stage);
                ConfigureTutorial(session, plan.stage);
                ValidateBuiltScene(scene, session, draft, plan);
                var errors = session.ValidateConfiguration();
                errors.AddRange(LevelValidation.Validate(scene).Where(issue => issue.IsError).Select(issue => issue.Message));
                if (errors.Count != 0)
                    throw new InvalidOperationException("Generated stage validation failed: " + string.Join(" | ", errors.Take(15)));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Unable to save scene " + path);
                Debug.Log($"Stage V7 {plan.stage} ({plan.name}): {draft.width}x{draft.length}, " +
                    $"{session.Towers.Count} towers, {plan.hazards.Length} hazards. Saved {path}");
            }
            finally { Object.DestroyImmediate(draft); }
        }

        // Allows NUnit EditMode validation against the EXACT geometry from the shipped JSON file.
        public static List<string> ValidateCatalogGeometry(int stage)
        {
            var plan = ReadCatalog().stages.Single(p => p.stage == stage);
            var draft = BuildDraft(plan);
            try
            {
                var errors = new List<string>();
                var start = draft.route.First();
                var goal = draft.route.Last();
                var seen = new HashSet<Vector2Int> { start };
                var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var adjacent in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var next = current + adjacent;
                        if (seen.Contains(next) || !Clearance(draft, next)) continue;
                        seen.Add(next); queue.Enqueue(next);
                    }
                }
                if (!seen.Contains(goal)) errors.Add($"Level {stage}: goal cannot be reached by a 3x3 squad.");
                if (plan.roadWidth == 5)
                    foreach (var center in draft.route)
                        for (var z = -2; z <= 2; z++) for (var x = -2; x <= 2; x++)
                            if (!RoadSurface(draft.Tile(center + new Vector2Int(x, z))))
                                errors.Add($"Level {stage}: five-wide road is pinched at {center}.");
                var route = draft.route.Select(p => draft.Position(p)).ToArray();
                var road = Enumerable.Range(0, draft.length).SelectMany(z => Enumerable.Range(0, draft.width)
                    .Select(x => new Vector2Int(x, z))).Where(p => RoadSurface(draft.Tile(p))).ToArray();
                // V6 uses negative space deliberately: a five-wide route should occupy
                // no more than 38% of interior grid area. Keep the visible tower bodies
                // away from the outer terrain border, not balanced on the last tile.
                if (road.Length > (draft.width - 2) * (draft.length - 2) * .38f)
                    errors.Add($"Level {stage}: road consumes too much of the battlefield.");
                foreach (var t in plan.towers)
                    if (t.x < 2 || t.z < 2 || t.x > draft.width - 3 || t.z > draft.length - 3)
                        errors.Add($"Level {stage}: tower too close to the terrain border.");
                foreach (var planTower in plan.towers)
                {
                    var point = new Vector2Int(planTower.x, planTower.z);
                    var shape = (AttackArea)Enum.Parse(typeof(AttackArea), planTower.shape);
                    if (draft.Tile(point) != (int)CellKind.TowerPlatform)
                        errors.Add($"Level {stage}: tower overlaps the route.");
                    if (!route.Any(p => SquadCombatRules.Contains(p - draft.Position(point), shape, planTower.range, .6f)) ||
                        road.Count(p => SquadCombatRules.Contains(draft.Position(p) - draft.Position(point), shape, planTower.range, .6f)) < 3)
                        errors.Add($"Level {stage}: tower at {point} has insufficient authored road coverage.");
                    if (shape == AttackArea.Circle)
                        ValidateCircle(planTower, draft, road, errors, stage);
                    else if (planTower.sniperOutpost)
                        errors.Add($"Level {stage}: sniper outposts must use circular coverage.");
                }
                var roadSet = new HashSet<Vector2Int>(road);
                foreach (var danger in plan.hazards)
                {
                    var point = new Vector2Int(danger.x, danger.z);
                    if (!roadSet.Contains(point) || draft.route.Min(p => Vector2Int.Distance(p, point)) > 1.01f)
                        errors.Add($"Level {stage}: hazard at {point} is not on the main road or its inner lane.");
                    if (danger.kind != "Boulder" && CampaignTrapPlacementInstaller.Coverage(roadSet, point, danger.radius) < .90f)
                        errors.Add($"Level {stage}: hazard at {point} must cover at least 90% walkable area.");
                }
                // Tactical traps must stay on playable roads and leave the objective/start clear.
                var tactical = plan.hazards.Where(h => h.kind == "Freeze" || h.kind == "Meteor" || h.kind == "Boulder").ToArray();
                foreach (var h in tactical)
                {
                    var point = new Vector2Int(h.x, h.z);
                    if (draft.Tile(point) != (int)CellKind.Road || Vector2Int.Distance(point, start) < 3.5f || Vector2Int.Distance(point, goal) < 3.5f)
                        errors.Add($"Level {stage}: tactical trap at {point} is off the road or too close to an endpoint.");
                    if (h.kind == "Boulder" && Mathf.Abs(h.dx) + Mathf.Abs(h.dz) != 1)
                        errors.Add($"Level {stage}: boulder at {point} requires a cardinal roll direction.");
                    if (tactical.Any(other => other != h && Vector2Int.Distance(point, new Vector2Int(other.x, other.z)) < (plan.roadWidth == 5 ? 5f : 4f)))
                        errors.Add($"Level {stage}: tactical traps are too close at {point}.");
                }
                if (plan.roadWidth == 5)
                {
                    foreach (var h in tactical)
                    {
                        var center = new Vector2Int(h.x, h.z);
                        if (h.kind == "Boulder")
                        {
                            var across = new Vector2Int(h.dx, h.dz);
                            if (across != Vector2Int.zero && (!RoadSurface(draft.Tile(center + 2 * across)) ||
                                !RoadSurface(draft.Tile(center - 2 * across))))
                                errors.Add($"Level {stage}: boulder at {center} must cross the road.");
                            continue;
                        }
                        // A safe full formation must exist near the danger ring. Grid-centred
                        // lateral lanes are checked first; quarter-cell anchors allow precise dodges.
                        var found = false;
                        for (var z = -12; z <= 12 && !found; z++) for (var x = -12; x <= 12 && !found; x++)
                        {
                            var anchor = new Vector2(center.x + x * .25f, center.y + z * .25f);
                            if (Vector2.Distance(anchor, center) > 3.5f) continue;
                            var safe = true;
                            for (var j = -1; j <= 1 && safe; j++) for (var i = -1; i <= 1 && safe; i++)
                            {
                                var soldier = anchor + new Vector2(i, j) * .86f;
                                if (Vector2.Distance(soldier, center) <= h.radius + .05f) { safe = false; break; }
                                for (var dz = -1; dz <= 1 && safe; dz++) for (var dx = -1; dx <= 1; dx++)
                                    if (!RoadSurface(draft.Tile(Vector2Int.RoundToInt(soldier + new Vector2(dx, dz) * .18f))))
                                    { safe = false; break; }
                            }
                            if (safe) found = true;
                        }
                        if (!found) errors.Add($"Level {stage}: trap at {center} has no nearby safe full-formation anchor.");
                    }
                }
                return errors;
            }
            finally { Object.DestroyImmediate(draft); }
        }

        // A circle must threaten a real road segment, or be an intentional sniper outpost
        // with an escape lane. A tiny incidental intersection no longer passes validation.
        private static void ValidateCircle(TowerPlan tower, MapDraft draft, Vector2Int[] road,
            List<string> errors, int stage)
        {
            var origin = new Vector2Int(tower.x, tower.z);
            var covered = new HashSet<Vector2Int>(road.Where(p => Vector2Int.Distance(p, origin) <= tower.range + .0001f));
            if (!tower.sniperOutpost)
            {
                var full = 0;
                foreach (var center in draft.route)
                {
                    var fits = true;
                    for (var z = -1; z <= 1; z++) for (var x = -1; x <= 1; x++)
                        if (!covered.Contains(center + new Vector2Int(x, z))) fits = false;
                    if (fits) full++;
                }
                if (covered.Count < 24 || full < 5)
                    errors.Add($"Level {stage}: circle at {origin} must cover at least 24 road cells and five full 3x3 formation positions.");
                return;
            }
            var normalRange = new[] { UnitRole.Attack, UnitRole.Captain, UnitRole.Vanguard, UnitRole.Demolisher }
                .Max(role => SquadCombatRules.Create(role).Range);
            var sniperRange = SquadCombatRules.Create(UnitRole.Sniper).Range;
            var edgeDistance = road.Min(p => new Vector2(Mathf.Max(0, Mathf.Abs(p.x - origin.x) - .5f),
                Mathf.Max(0, Mathf.Abs(p.y - origin.y) - .5f)).magnitude);
            var centerDistance = road.Min(p => Vector2Int.Distance(p, origin));
            if (edgeDistance <= normalRange + .1f || centerDistance > sniperRange - .1f)
                errors.Add($"Level {stage}: sniper outpost at {origin} is not within the sniper-only normal attack range band.");
            var safe = new HashSet<Vector2Int>(road.Where(p => !covered.Contains(p)));
            var start = draft.route.First(); var goal = draft.route.Last();
            var seen = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
            if (safe.Contains(start)) { queue.Enqueue(start); seen.Add(start); }
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var next = current + offset;
                    if (safe.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            if (!seen.Contains(goal))
                errors.Add($"Level {stage}: sniper outpost at {origin} has no continuous safe tile-center lane.");
        }

        private static bool Passable(int tile) => tile == (int)CellKind.Road || tile == (int)CellKind.Ground ||
            tile == (int)CellKind.Spawn || tile == (int)CellKind.Goal;
        private static bool RoadSurface(int tile) => tile == (int)CellKind.Road || tile == (int)CellKind.Spawn || tile == (int)CellKind.Goal;
        private static bool Clearance(MapDraft draft, Vector2Int pos)
        {
            for (var z = -1; z <= 1; z++) for (var x = -1; x <= 1; x++)
                if (!RoadSurface(draft.Tile(pos + new Vector2Int(x, z)))) return false;
            return true;
        }

        private static MapDraft BuildDraft(StagePlan plan)
        {
            if (plan == null || plan.route == null || plan.route.Length < 4 || plan.route.Length % 2 != 0 ||
                plan.cover == null || plan.cover.Length % 4 != 0 || plan.towers == null || plan.hazards == null)
                throw new InvalidDataException("Incomplete Stage V3 catalog entry.");
            var draft = ScriptableObject.CreateInstance<MapDraft>();
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                draft.ResetGrid(plan.width, plan.height);
                draft.levelNumber = plan.stage;
                for (var z = 0; z < plan.height; z++) for (var x = 0; x < plan.width; x++)
                    draft.tiles[z * plan.width + x] = x == 0 || z == 0 || x == plan.width - 1 || z == plan.height - 1 || plan.corridor
                        ? (int)CellKind.Obstacle : (int)CellKind.Ground;
                var turns = new List<Vector2Int>();
                for (var i = 0; i < plan.route.Length; i += 2) turns.Add(new Vector2Int(plan.route[i], plan.route[i + 1]));
                foreach (var turn in turns)
                    if (!draft.Inside(turn)) throw new InvalidDataException($"Stage {plan.stage}: waypoint outside map.");
                // Early stages keep one non-walkable ground shoulder beyond the five-wide road
                // for tower platforms and support minions. Ground is not a movement surface.
                if (plan.corridor)
                    foreach (var p in FollowSegments(turns))
                        for (var z = -3; z <= 3; z++) for (var x = -3; x <= 3; x++)
                        {
                            var c = p + new Vector2Int(x, z);
                            if (c.x > 0 && c.y > 0 && c.x < plan.width - 1 && c.y < plan.height - 1)
                                draft.tiles[c.y * plan.width + c.x] = (int)CellKind.Ground;
                        }
                for (var i = 0; i < plan.cover.Length; i += 4)
                    for (var z = plan.cover[i + 1]; z <= plan.cover[i + 3]; z++)
                        for (var x = plan.cover[i]; x <= plan.cover[i + 2]; x++)
                        {
                            var point = new Vector2Int(x, z);
                            if (!draft.Inside(point) || x == 0 || z == 0 || x == plan.width - 1 || z == plan.height - 1)
                                throw new InvalidDataException($"Stage {plan.stage}: cover exceeds playable arena.");
                            draft.tiles[z * plan.width + x] = (int)CellKind.Obstacle;
                        }
                // The route remains a 3x3 formation contract; the playable road is wider.
                var roadWidth = plan.roadWidth == 0 ? 3 : plan.roadWidth;
                if (roadWidth != 3 && roadWidth != 5) throw new InvalidDataException("Road width must be 3 or 5.");
                var roadHalf = roadWidth / 2;
                foreach (var p in FollowSegments(turns))
                    for (var z = -roadHalf; z <= roadHalf; z++) for (var x = -roadHalf; x <= roadHalf; x++)
                    {
                        var c = p + new Vector2Int(x, z);
                        if (!draft.Inside(c) || draft.Tile(c) == (int)CellKind.Obstacle)
                            throw new InvalidDataException($"Stage {plan.stage}: {roadWidth}-wide road hits obstacle at {c}.");
                        draft.tiles[c.y * plan.width + c.x] = (int)CellKind.Road;
                    }
                var start = turns[0]; var goal = turns[turns.Count - 1];
                for (var z = -1; z <= 1; z++) for (var x = -1; x <= 1; x++)
                {
                    var a = start + new Vector2Int(x, z); var b = goal + new Vector2Int(x, z);
                    draft.tiles[a.y * draft.width + a.x] = (int)CellKind.Spawn;
                    draft.tiles[b.y * draft.width + b.x] = (int)CellKind.Goal;
                }
                foreach (var turn in turns) draft.AppendRoute(turn);
                var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1.prefab");
                if (towerPrefab == null) throw new FileNotFoundException("Missing standard 1x1 tower prefab.");
                foreach (var tower in plan.towers)
                {
                    var p = new Vector2Int(tower.x, tower.z);
                    if (!draft.Inside(p) || Passable(draft.Tile(p)) && draft.Tile(p) != (int)CellKind.Ground)
                        throw new InvalidDataException($"Stage {plan.stage}: tower on authored route at {p}.");
                    if (tower.range < 1 || tower.damage <= 0 || tower.interval <= 0 || tower.hp <= 0)
                        throw new InvalidDataException($"Stage {plan.stage}: invalid tower balance at {p}.");
                    var area = (AttackArea)Enum.Parse(typeof(AttackArea), tower.shape);
                    if (area == AttackArea.Square || (SquadCombatRules.IsLaser(area) && plan.stage < 6))
                        throw new InvalidDataException($"Stage {plan.stage}: only later bosses can use precision lasers; squares are obsolete.");
                    draft.tiles[p.y * plan.width + p.x] = (int)CellKind.TowerPlatform;
                    draft.PlaceTower(p, towerPrefab, area, false, 20 + plan.stage * 4);
                }
                foreach (var threat in plan.hazards)
                    if (!Passable(draft.Tile(new Vector2Int(threat.x, threat.z))))
                        throw new InvalidDataException($"Stage {plan.stage}: threat in solid cover.");
                return draft;
            }
            catch { Object.DestroyImmediate(draft); throw; }
        }

        private static IEnumerable<Vector2Int> FollowSegments(IReadOnlyList<Vector2Int> turns)
        {
            for (var i = 1; i < turns.Count; i++)
            {
                var a = turns[i - 1]; var b = turns[i];
                if (a.x != b.x && a.y != b.y) throw new InvalidDataException("Stage routes must turn at right angles.");
                var step = new Vector2Int(Math.Sign(b.x - a.x), Math.Sign(b.y - a.y));
                for (var p = i == 1 ? a : a + step; ; p += step)
                { yield return p; if (p == b) break; }
            }
        }

        private static void ConfigureSession(BattleSession session, int stage)
        {
            // ApplyToScene preserves legacy route width; explicitly upgrade it to the 3x3 squad.
            var route = new SerializedObject(session.Route);
            var requiredWidth = route.FindProperty("requiredWidth");
            if (requiredWidth != null) requiredWidth.intValue = 3;
            route.ApplyModifiedPropertiesWithoutUndo();
            var data = new SerializedObject(session);
            Bool(data, "squadCombat", true); Int(data, "formationSize", 3); Int(data, "unitCount", 6);
            Bool(data, "manualSquadMovement", false); Bool(data, "continuousSquadMovement", true);
            Bool(data, "restrictToTacticalRoad", true); Bool(data, "compactSquadMovement", false); Float(data, "continuousFormationSpacing", .86f);
            Float(data, "continuousSoldierRadius", .18f); Float(data, "continuousVisualFootprint", .38f);
            Float(data, "continuousMovementSpeedMultiplier", stage >= 3 ? 1.28f : 1f);
            Float(data, "levelTimeMultiplier", 1.16f); Int(data, "displayedLevelNumber", stage);
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var owner in session.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattlePresentation>(true)))
                if (owner.Session == session)
                {
                    var view = new SerializedObject(owner);
                    Bool(view, "showUnitLevels", false);
                    Bool(view, "showTowerRanges", true);
                    view.ApplyModifiedPropertiesWithoutUndo();
                }
        }

        private static void ConfigureTowers(Scene scene, BattleSession session, StagePlan plan)
        {
            var found = session.Towers.Where(t => t != null && !t.IsWall).ToArray();
            if (found.Length != plan.towers.Length) throw new InvalidOperationException("Tower count mismatch after map build.");
            for (var i = 0; i < found.Length; i++)
            {
                var stat = plan.towers[i];
                var tower = found.Single(t => Mathf.RoundToInt(t.transform.position.x) == stat.x &&
                    Mathf.RoundToInt(t.transform.position.z) == stat.z);
                var data = new SerializedObject(tower);
                Float(data, "range", stat.range); Float(data, "damage", stat.damage);
                Float(data, "attackInterval", stat.interval); Float(data, "maxHp", stat.hp);
                Float(data, "attackWidth", .60f); Bool(data, "isAoe", false);
                Int(data, "attackArea", (int)(AttackArea)Enum.Parse(typeof(AttackArea), stat.shape));
                Int(data, "goldBounty", 20 + plan.stage * 4);
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var child in tower.GetComponentsInChildren<Transform>(true).Where(t => t != tower.transform && t.name == "Stage V3 Threat Class").ToArray())
                    Object.DestroyImmediate(child.gameObject);
                tower.AlignVisual(session.CaptureTowerDefinition(tower, i + 1, session.Route.CaptureTopCenters()),
                    session.Route.CaptureTopCenters());
                PrefabUtility.RecordPrefabInstancePropertyModifications(tower.transform);
            }
        }

        private static void ValidateBuiltScene(Scene scene, BattleSession session, MapDraft draft, StagePlan plan)
        {
            var all = scene.GetRootGameObjects();
            var towers = all.SelectMany(r => r.GetComponentsInChildren<BattleTower>(true)).ToArray();
            var threats = all.SelectMany(r => r.GetComponentsInChildren<OpenStageThreat>(true)).ToArray();
            var cells = all.SelectMany(r => r.GetComponentsInChildren<BattleCell>(true)).ToArray();
            if (towers.Length != plan.towers.Length || towers.Any(t => !session.Towers.Contains(t)))
                throw new InvalidOperationException("Scene contains unexpected or unregistered towers.");
            if (threats.Length != plan.hazards.Length || cells.Length != draft.width * draft.length)
                throw new InvalidOperationException("Scene contains unexpected hazards or cells.");
            // Match runtime minion planning, including ground-only hosting and actual tower coverage.
            var route = session.Route.CaptureTopCenters();
            var definitions = session.Towers.Select((t, i) => session.CaptureTowerDefinition(t, i + 1, route)).ToArray();
            var placements = MinionPlanner.Plan(route, definitions, plan.stage, MinionSiteScan.Scan(session));
            if (placements.Count != MinionRules.Kinds(plan.stage).Count)
                Debug.LogWarning($"Stage {plan.stage}: {placements.Count}/{MinionRules.Kinds(plan.stage).Count} support groups can be placed; inspect minion placement in Play Mode.");
            if (all.Count(r => r.name == "Stage V3.3 Seamless Terrain") != 1 ||
                all.Count(r => r.name == PaintSquadLevelDressing.RootName) != 1 ||
                all.Any(r => r.name == LandmarkRoot))
                throw new InvalidOperationException("Scene contains duplicate terrain or legacy objective art.");
        }

        private static void ClearLegacyDecor(Scene scene)
        {
            foreach (var obj in scene.GetRootGameObjects().ToArray())
                if (obj.name == "Open Stage Hazards" || obj.name == "Stage V2 Encounters" ||
                    obj.name == EncounterRoot || obj.name == LandmarkRoot ||
                    obj.name == "Stage V3.3 Seamless Terrain" || obj.name == PaintSquadLevelDressing.RootName)
                    Object.DestroyImmediate(obj);
            foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray())
                if (t != null && t.name == "Decorative Border Tiles" &&
                    t.GetComponentInChildren<BattleCell>(true) == null && t.GetComponentInChildren<BattleTower>(true) == null)
                    Object.DestroyImmediate(t.gameObject);
        }
        private static void RemoveRailArtwork(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects().ToArray())
                if (root.name == "Railway Path Artwork" || root.name == "Track Artwork" ||
                    root.name == "Rail Tracks" || root.name == "Carriage") Object.DestroyImmediate(root);
            foreach (var child in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray())
            {
                if (child == null || child.parent == null) continue;
                var label = child.name.ToLowerInvariant();
                if (!(label.Contains("railway") || label.Contains("rail track") || label.Contains("carriage") || label.Contains("trolley"))) continue;
                if (child.GetComponentInChildren<BattleCell>(true) != null || child.GetComponentInChildren<BattleTower>(true) != null) continue;
                Object.DestroyImmediate(child.gameObject);
            }
            foreach (var platform in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SquadPlatformView>(true)).ToArray())
            {
                if (platform.Model != null && platform.Model.gameObject.scene == scene) Object.DestroyImmediate(platform.Model.gameObject);
                Object.DestroyImmediate(platform);
            }
        }

        // Retain Road Cell components for SceneRoute and AI. Swap VISUALS only, removing the
        // misleading stripe/rail geometry; obstacles and START/GOAL tiles provide the route cue.
        private static void ReplaceRoadArtwork(Scene scene)
        {
            var groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Tiles/Ground_Cell.prefab");
            if (groundPrefab == null) return;
            var groundMesh = groundPrefab.GetComponentInChildren<MeshFilter>(true);
            var groundRenderer = groundPrefab.GetComponentInChildren<MeshRenderer>(true);
            var groundSprite = groundPrefab.GetComponentInChildren<SpriteRenderer>(true);
            foreach (var cell in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleCell>(true)))
            {
                if (!cell.IsRouteSurface) continue;
                var meshes = cell.GetComponentsInChildren<MeshRenderer>(true);
                for (var i = 0; i < meshes.Length; i++)
                {
                    meshes[i].enabled = i == 0;
                    if (i != 0 || groundRenderer == null) continue;
                    meshes[i].sharedMaterials = groundRenderer.sharedMaterials;
                    var filter = meshes[i].GetComponent<MeshFilter>();
                    if (filter != null && groundMesh != null) filter.sharedMesh = groundMesh.sharedMesh;
                }
                var sprites = cell.GetComponentsInChildren<SpriteRenderer>(true);
                for (var i = 0; i < sprites.Length; i++)
                {
                    sprites[i].enabled = i == 0;
                    if (i == 0 && groundSprite != null)
                    { sprites[i].sprite = groundSprite.sprite; sprites[i].sharedMaterial = groundSprite.sharedMaterial; }
                }
            }
        }

        private static void CreateEncounters(Scene scene, MapDraft draft, StagePlan plan)
        {
            if (plan.hazards.Length == 0) return;
            var host = new GameObject(EncounterRoot);
            SceneManager.MoveGameObjectToScene(host, scene);
            foreach (var h in plan.hazards)
            {
                var sentry = h.kind == "Sentry";
                var kind = h.kind == "Trap" ? OpenStageThreat.ThreatKind.GroundTrap :
                    (OpenStageThreat.ThreatKind)Enum.Parse(typeof(OpenStageThreat.ThreatKind), h.kind);
                var go = new GameObject(sentry ? "Sentry" : h.kind == "Trap" ? "Pulse Trap" : h.kind + " Trap");
                go.transform.SetParent(host.transform, false);
                go.transform.position = draft.Position(new Vector2Int(h.x, h.z)) + Vector3.up * (sentry ? .50f : .035f);
                var threat = go.AddComponent<OpenStageThreat>();
                threat.Configure(kind, h.radius, h.damage, h.interval, new Vector3(h.dx, 0, h.dz));
                if (threat.IsTactical) continue; // Procedural themed art is built by the runtime view.
                var model = GameObject.CreatePrimitive(sentry ? PrimitiveType.Capsule : PrimitiveType.Cylinder);
                model.transform.SetParent(go.transform, false);
                model.transform.localScale = sentry ? new Vector3(.42f, .40f, .42f) : new Vector3(.52f, .09f, .52f);
                Object.DestroyImmediate(model.GetComponent<Collider>());
                model.GetComponent<Renderer>().sharedMaterial = AccentMaterial(sentry ? "Sentry" : "Trap");
            }
        }
        private static void CreateLandmarks(Scene scene, MapDraft draft, StagePlan plan)
        {
            var turns = plan.route;
            var host = new GameObject(LandmarkRoot);
            SceneManager.MoveGameObjectToScene(host, scene);
            CreateBeacon(host.transform, "START", draft.Position(new Vector2Int(turns[0], turns[1])), "Start");
            CreateBeacon(host.transform, "GOAL", draft.Position(new Vector2Int(turns[turns.Length - 2], turns[turns.Length - 1])), "Goal");
        }
        private static void CreateBeacon(Transform parent, string name, Vector3 position, string color)
        {
            var go = new GameObject(name + " Beacon"); go.transform.SetParent(parent, false);
            go.transform.position = position + Vector3.up * .35f;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.transform.SetParent(go.transform, false);
            cap.transform.localScale = Vector3.one * .54f;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.GetComponent<Renderer>().sharedMaterial = AccentMaterial(color);
        }
        private static Material AccentMaterial(string type)
        {
            var folder = "Assets/Rebuild/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Rebuild", "Materials");
            var path = folder + "/StageV3_" + type + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            var tint = type switch
            {
                "Laser" => new Color(.98f, .14f, .18f),
                "Cone" => new Color(1f, .58f, .20f),
                "Sentry" => new Color(.90f, .18f, .22f),
                "Trap" => new Color(1f, .48f, .12f),
                "Start" => new Color(.24f, .87f, .48f),
                "Goal" => new Color(.13f, .8f, 1f),
                _ => new Color(.24f, .58f, 1f)
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            else material.color = tint;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static void ConfigureTutorial(BattleSession session, int stage)
        {
            var old = session.GetComponent<LevelOneTutorial>();
            if (old != null) old.enabled = false;
            if (stage != 1) return;
            var quick = session.GetComponent<ClickMoveTutorial>() ?? session.gameObject.AddComponent<ClickMoveTutorial>();
            var data = new SerializedObject(quick);
            var binding = data.FindProperty("session");
            if (binding != null) binding.objectReferenceValue = session;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Float(SerializedObject so, string name, float value) { var p = so.FindProperty(name); if (p != null) p.floatValue = value; }
        private static void Int(SerializedObject so, string name, int value) { var p = so.FindProperty(name); if (p != null) p.intValue = value; }
        private static void Bool(SerializedObject so, string name, bool value) { var p = so.FindProperty(name); if (p != null) p.boolValue = value; }
    }
}
