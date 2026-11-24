using System;
using System.Collections.Generic;
using System.Linq;
using Team5.BasicTowerTrial.Session;
using Team5.BasicTowerTrial.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.BasicTowerTrial.Editor.LevelAuthoring
{
    public static class MapBuilderUtility
    {
        public const string Kit = "Assets/RandomMuster/Prefabs/LevelBuilding/";
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        }

        public static BattleSession Populate(MapDraft draft, Scene scene)
        {
            RequireEditMode();
            if (!scene.IsValid() || !scene.isLoaded || scene.rootCount != 0)
                throw new ArgumentException("Generation requires a new empty scene.");
            if (draft == null || draft.tiles.Length != draft.width * draft.length)
                throw new ArgumentException("Create a grid first.");
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Systems/BattleRig_3D.prefab");
            if (rig == null) throw new InvalidOperationException("BattleRig_3D prefab is missing.");
            var host = (GameObject)PrefabUtility.InstantiatePrefab(rig, scene);
            host.name = "Map Builder Level";
            var session = host.GetComponentInChildren<BattleSession>(true);
            var cells = new Dictionary<Vector2Int, BattleCell>();
            var prefabs = Enum.GetValues(typeof(CellKind)).Cast<CellKind>().ToDictionary(kind => kind,
                kind => AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Tiles/" + kind + "_Cell.prefab"));
            for (var z = 0; z < draft.length; z++)
                for (var x = 0; x < draft.width; x++)
                {
                    var p = new Vector2Int(x, z); var value = draft.Tile(p);
                    if (value == -1) continue;
                    if (!Enum.IsDefined(typeof(CellKind), value) || prefabs[(CellKind)value] == null)
                        throw new ArgumentException($"Unknown tile at ({x}, {z}).");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[(CellKind)value], scene);
                    go.transform.SetParent(session.Grid.transform, false);
                    go.transform.position = draft.Position(p);
                    go.name = $"{(CellKind)value} ({x}, {z})";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    cells.Add(p, go.GetComponent<BattleCell>());
                }
            var route = new SerializedObject(session.Route);
            var entries = route.FindProperty("cells"); entries.arraySize = draft.route.Count;
            for (var i = 0; i < draft.route.Count; i++)
            {
                if (!cells.TryGetValue(draft.route[i], out var cell)) throw new ArgumentException("Route contains an empty cell.");
                entries.GetArrayElementAtIndex(i).objectReferenceValue = cell;
            }
            route.FindProperty("requiredWidth").intValue = 3;
            route.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(session);
            var towers = settings.FindProperty("towers"); towers.arraySize = draft.towers.Count;
            for (var i = 0; i < draft.towers.Count; i++)
            {
                var stamp = draft.towers[i];
                if (stamp.prefab == null || !PrefabUtility.IsPartOfPrefabAsset(stamp.prefab)) throw new ArgumentException("Tower needs a prefab asset.");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(stamp.prefab, scene);
                go.transform.SetParent(host.transform, false);
                go.transform.position = draft.Position(stamp.cell) + Vector3.up * .25f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                var tower = go.GetComponent<BattleTower>();
                if (!string.IsNullOrEmpty(stamp.configuration)) JsonUtility.FromJsonOverwrite(stamp.configuration, tower);
                var serialized = new SerializedObject(tower);
                if (tower.IsWall)
                    serialized.FindProperty("wallAlongZ").boolValue = stamp.prefab.GetComponent<BattleTower>().Footprint.y == 3;
                serialized.FindProperty("attackArea").enumValueIndex = (int)stamp.area;
                var isAoeProp = serialized.FindProperty("isAoe");
                if (isAoeProp != null) isAoeProp.boolValue = stamp.isAoe;
                var bountyProp = serialized.FindProperty("goldBounty");
                if (bountyProp != null) bountyProp.intValue = stamp.goldBounty > 0 ? stamp.goldBounty : (stamp.isAoe ? 40 : 25);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                towers.GetArrayElementAtIndex(i).objectReferenceValue = tower;
            }
            settings.FindProperty("autoStart").boolValue = true;
            settings.FindProperty("useSceneRosterOnly").boolValue = false;
            settings.FindProperty("displayedLevelNumber").intValue = Mathf.Max(1, draft.levelNumber);
            settings.ApplyModifiedPropertiesWithoutUndo();
            FrameGeneratedCamera(host, draft);
            foreach (var component in host.GetComponentsInChildren<MonoBehaviour>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            return session;
        }

        private static void FrameGeneratedCamera(GameObject host, MapDraft draft)
        {
            var camera = host.GetComponentInChildren<Camera>(true);
            if (camera == null) return;
            var center = draft.Position(Vector2Int.zero) + new Vector3((draft.width - 1) * .5f, 1.5f, (draft.length - 1) * .5f);
            var halfSize = new Vector3(draft.width * .5f, 2, draft.length * .5f);
            var inverse = Quaternion.Inverse(camera.transform.rotation);
            var vertical = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            var horizontal = vertical * (16f / 9);
            var distance = 10f; var ortho = 1f;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = inverse * Vector3.Scale(halfSize, new Vector3(x, y, z));
                        distance = Mathf.Max(distance, Mathf.Abs(corner.x) / horizontal - corner.z, Mathf.Abs(corner.y) / vertical - corner.z);
                        ortho = Mathf.Max(ortho, Mathf.Abs(corner.y), Mathf.Abs(corner.x) / (16f / 9));
                    }
            distance = distance * 1.15f + 2;
            camera.transform.position = center - camera.transform.forward * distance;
            if (camera.orthographic) camera.orthographicSize = ortho * 1.15f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + halfSize.magnitude + 10);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }

        public static List<string> Validate(MapDraft draft)
        {
            RequireEditMode();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                Populate(draft, preview);
                return LevelValidation.Validate(preview).Select(issue => (issue.IsError ? "ERROR: " : "WARNING: ") + issue.Message).ToList();
            }
            catch (Exception error) { return new List<string> { "ERROR: " + error.Message }; }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static Scene Generate(MapDraft draft)
        {
            RequireEditMode();
            if (Validate(draft).Any(message => message.StartsWith("ERROR:"))) throw new InvalidOperationException("Fix map validation errors before generation.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException("Generation cancelled.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                Populate(draft, scene);
                foreach (var root in scene.GetRootGameObjects()) Undo.RegisterCreatedObjectUndo(root, "Generate Map");
                SceneManager.SetActiveScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                return scene;
            }
            catch { EditorSceneManager.CloseScene(scene, true); throw; }
        }

        public static void SaveNew(Scene scene, string destination)
        {
            RequireEditMode();
            var path = LevelTemplateUtility.NormalizeDestination(destination);
            if (!scene.IsValid() || !scene.isLoaded || !string.IsNullOrEmpty(scene.path))
                throw new ArgumentException("Save New only accepts the unsaved generated scene.");
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Scene could not be saved.");
        }

        public static BattleSession ApplyToScene(MapDraft draft, Scene scene)
        {
            RequireEditMode();
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("Open a level scene first.");
            if (draft == null || draft.width < 3 || draft.length < 3 || draft.tiles == null || draft.tiles.Length != draft.width * draft.length)
                throw new ArgumentException("Create or read a map grid first.");
            var sessions = LevelValidation.Components<BattleSession>(scene);
            if (sessions.Length != 1 || sessions[0].Grid == null || sessions[0].Route == null)
                throw new ArgumentException("The current scene needs exactly one BattleSession with a grid and route.");
            var session = sessions[0];
            if (session.Grid.gameObject.scene != scene || session.Route.gameObject.scene != scene)
                throw new ArgumentException("Grid and route must belong to the current scene.");
            if (draft.sourceSceneHandle != 0 && draft.sourceSceneHandle != scene.handle || draft.sourceSession != null && draft.sourceSession != session)
                throw new InvalidOperationException("This draft was read from another scene. Read the current scene before applying it.");
            var oldCells = LevelValidation.Components<BattleCell>(scene);
            if (oldCells.Any(cell => !cell.transform.IsChildOf(session.Grid.transform)) ||
                oldCells.Select(cell => cell.Occupancy.y).Distinct().Count() > 1)
                throw new ArgumentException("Apply supports one flat map inside this session's grid.");
            var existing = new Dictionary<Vector3Int, BattleCell>();
            foreach (var cell in oldCells)
                if (!existing.TryAdd(cell.Occupancy, cell)) throw new ArgumentException("Repair duplicate map cells before applying.");
            var oldTowers = LevelValidation.Components<BattleTower>(scene);
            var retainedTowers = new HashSet<BattleTower>();
            foreach (var stamp in draft.towers)
                if (stamp.source != null && (!oldTowers.Contains(stamp.source) || !retainedTowers.Add(stamp.source)))
                    throw new ArgumentException("A tower source is duplicated or no longer belongs to this scene. Read the scene again.");
            var prefabs = Enum.GetValues(typeof(CellKind)).Cast<CellKind>().ToDictionary(kind => kind,
                kind => AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Tiles/" + kind + "_Cell.prefab"));

            try
            {
                using (var edit = new LevelEditScope(scene, "Apply Map to Current Scene"))
                {
                    edit.Record(session); edit.Record(session.Route);
                    var cells = new Dictionary<Vector2Int, BattleCell>();
                    var retainedCells = new HashSet<BattleCell>();
                    for (var z = 0; z < draft.length; z++)
                        for (var x = 0; x < draft.width; x++)
                        {
                            var p = new Vector2Int(x, z); var value = draft.Tile(p);
                            if (value == -1) continue;
                            if (!Enum.IsDefined(typeof(CellKind), value) || prefabs[(CellKind)value] == null)
                                throw new ArgumentException($"Unknown tile at ({x}, {z}).");
                            var position = draft.Position(p);
                            var key = new Vector3Int(draft.originX + x, draft.layer, draft.originZ + z);
                            if (existing.TryGetValue(key, out var previous) && (int)previous.Kind == value)
                            {
                                retainedCells.Add(previous); cells.Add(p, previous); continue;
                            }
                            var go = CreateMapObject(prefabs[(CellKind)value], scene, session.Grid.transform, position, edit);
                            go.name = $"{(CellKind)value} ({key.x}, {key.z})";
                            cells.Add(p, go.GetComponent<BattleCell>());
                        }
                    var route = new SerializedObject(session.Route);
                    var entries = route.FindProperty("cells"); entries.arraySize = draft.route.Count;
                    for (var i = 0; i < draft.route.Count; i++)
                    {
                        if (!cells.TryGetValue(draft.route[i], out var cell)) throw new ArgumentException("Route contains an empty cell.");
                        entries.GetArrayElementAtIndex(i).objectReferenceValue = cell;
                    }
                    route.ApplyModifiedPropertiesWithoutUndo(); // Keep the existing route width and all session settings.
                    foreach (var cell in oldCells)
                        if (!retainedCells.Contains(cell)) DestroyMapObject(cell.gameObject, edit);

                    var settings = new SerializedObject(session);
                    var towerEntries = settings.FindProperty("towers"); towerEntries.arraySize = draft.towers.Count;
                    var appliedTowers = new List<BattleTower>();
                    foreach (var stamp in draft.towers)
                    {
                        var tower = stamp.source;
                        var position = draft.Position(stamp.cell) + Vector3.up * BattleCell.LayerHeight / 2;
                        if (tower == null)
                        {
                            if (stamp.prefab == null || !PrefabUtility.IsPartOfPrefabAsset(stamp.prefab) || stamp.prefab.GetComponent<BattleTower>() == null)
                                throw new ArgumentException("Tower needs a prefab with BattleTower on its root.");
                            var go = CreateMapObject(stamp.prefab, scene, session.Grid.transform.parent, position, edit);
                            tower = go.GetComponent<BattleTower>(); edit.Record(tower);
                            if (!string.IsNullOrEmpty(stamp.configuration)) JsonUtility.FromJsonOverwrite(stamp.configuration, tower);
                            var serialized = new SerializedObject(tower);
                            if (tower.IsWall)
                                serialized.FindProperty("wallAlongZ").boolValue = stamp.prefab.GetComponent<BattleTower>().Footprint.y == 3;
                            serialized.FindProperty("attackArea").enumValueIndex = (int)stamp.area;
                            serialized.FindProperty("isAoe").boolValue = stamp.isAoe;
                            serialized.FindProperty("goldBounty").intValue = stamp.goldBounty;
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                        }
                        else { edit.Record(tower.transform); tower.transform.position = position; }
                        towerEntries.GetArrayElementAtIndex(appliedTowers.Count).objectReferenceValue = tower;
                        appliedTowers.Add(tower);
                    }
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var tower in oldTowers)
                        if (!retainedTowers.Contains(tower)) DestroyMapObject(tower.gameObject, edit);
                    var errors = LevelValidation.Validate(scene).Where(issue => issue.IsError).Select(issue => issue.Message).ToArray();
                    if (errors.Length != 0) throw new InvalidOperationException("Map was not applied:\n" + string.Join("\n", errors.Take(12)));
                    Undo.RegisterCompleteObjectUndo(draft, "Apply Map to Current Scene");
                    draft.sourceSession = session;
                    draft.sourceSceneHandle = scene.handle;
                    for (var i = 0; i < draft.towers.Count; i++) draft.towers[i].source = appliedTowers[i];
                    edit.Commit();
                }
            }
            finally
            {
                // Grid/wall lookup caches are derived state and must also match after a rollback.
                session.ValidateConfiguration();
            }
            return session;
        }

        private static GameObject CreateMapObject(GameObject prefab, Scene scene, Transform parent, Vector3 position, LevelEditScope edit)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            edit.RegisterCreated(go);
            // Transform snapshots do not record parenting. Track it explicitly so Redo restores grid ownership.
            Undo.SetTransformParent(go.transform, parent, "Parent Map Object");
            edit.Record(go); edit.Record(go.transform);
            go.transform.position = position;
            return go;
        }

        private static void DestroyMapObject(GameObject go, LevelEditScope edit)
        {
            if (go.GetComponentsInChildren<BattleSession>(true).Length != 0 || go.GetComponentsInChildren<BattleGrid>(true).Length != 0 ||
                go.GetComponentsInChildren<SceneRoute>(true).Length != 0 || go.GetComponentsInChildren<Camera>(true).Length != 0 ||
                go.GetComponentsInChildren<Canvas>(true).Length != 0 || go.GetComponentsInChildren<Team5.BasicTowerTrial.Presentation.UnitView>(true).Length != 0 ||
                go.GetComponentsInChildren<BattleCell>(true).Length + go.GetComponentsInChildren<BattleTower>(true).Length != 1)
                throw new InvalidOperationException($"{go.name} contains other level objects. Move them outside the map tile/tower before replacing it.");
            if (PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetNearestPrefabInstanceRoot(go) != go)
                throw new InvalidOperationException($"{go.name} is embedded in a larger prefab. Use separate tile/tower prefab instances for map editing.");
            edit.Destroy(go);
        }

        public static MapDraft Read(Scene scene)
        {
            RequireEditMode();
            var cells = LevelValidation.Components<BattleCell>(scene);
            var sessions = LevelValidation.Components<BattleSession>(scene);
            if (cells.Length == 0 || sessions.Length != 1) throw new ArgumentException("Open a scene with one session and a nonempty grid.");
            if (cells.Select(c => c.Occupancy.y).Distinct().Count() != 1) throw new ArgumentException("Only single-layer maps can be read by this version.");
            var errors = new List<string>(); sessions[0].Grid.RebuildIndex(errors);
            if (errors.Count != 0) throw new ArgumentException(errors[0]);
            var result = ScriptableObject.CreateInstance<MapDraft>();
            result.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var minX = cells.Min(c => c.Occupancy.x); var minZ = cells.Min(c => c.Occupancy.z);
                result.ResetGrid(Math.Max(3, cells.Max(c => c.Occupancy.x) - minX + 1), Math.Max(3, cells.Max(c => c.Occupancy.z) - minZ + 1));
                result.originX = minX; result.originZ = minZ; result.layer = cells[0].Occupancy.y;
                result.levelNumber = sessions[0].DisplayedLevelNumber;
                result.sourceSession = sessions[0];
                result.sourceSceneHandle = scene.handle;
                foreach (var cell in cells) result.tiles[(cell.Occupancy.z - minZ) * result.width + cell.Occupancy.x - minX] = (int)cell.Kind;
                foreach (var cell in sessions[0].Route.Cells)
                {
                    if (cell == null) throw new ArgumentException("Repair missing route references before importing.");
                    result.route.Add(new Vector2Int(cell.Occupancy.x - minX, cell.Occupancy.z - minZ));
                }
                foreach (var tower in sessions[0].Towers)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(tower.gameObject);
                    if (source == null || source.GetComponent<BattleTower>() == null)
                        throw new ArgumentException("Import requires towers created from tower prefabs; custom scene towers are not converted automatically.");
                    if (tower.IsWall)
                        source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Towers/" + (tower.IsWoodenGate ? "WoodenGate_3x1_" : "Wall_3x1_") + (tower.Footprint.y == 3 ? "Z" : "X") + ".prefab");
                    var serialized = new SerializedObject(tower);
                    result.towers.Add(new MapTowerStamp
                    {
                        cell = new Vector2Int(Mathf.RoundToInt(tower.transform.position.x) - minX, Mathf.RoundToInt(tower.transform.position.z) - minZ),
                        prefab = source,
                        area = (Team5.BasicTowerTrial.Simulation.AttackArea)serialized.FindProperty("attackArea").enumValueIndex,
                        isAoe = tower.IsAoe,
                        goldBounty = tower.GoldBounty,
                        configuration = JsonUtility.ToJson(tower),
                        source = tower
                    });
                }
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result); throw; }
        }
    }
}
