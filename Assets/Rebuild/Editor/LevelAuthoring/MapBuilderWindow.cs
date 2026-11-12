using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    public sealed class MapBuilderWindow : EditorWindow
    {
        [SerializeField] private MapDraft draft;
        private int width = 24, length = 18, brushSize = 3, tool;
        private float zoom = 26;
        private Vector2 gridScroll, panelScroll;
        [SerializeField] private float footerHeight = 140;
        private Rect splitterRect, gridViewport;
        private float splitStartY, splitStartHeight;
        private float workspaceTop = 280;
        private bool resizingSplitter;
        private GameObject towerPrefab;
        private AttackArea area = AttackArea.NegativeX;
        private bool towerIsAoe;
        private int towerGoldBounty = 25;
        private List<string> messages = new List<string>();
        private bool painting;
        private Vector2Int lastPaint;
        private Vector2Int hover = new Vector2Int(-1, -1);
        private Scene generated;
        private string generatedSnapshot;
        private static readonly string[] Tools = { "Ground", "Road", "Obstacle", "TowerPlatform", "Spawn", "Goal", "Erase tile", "Route", "Place tower", "Remove tower" };
        private static readonly Color[] Colors = { new Color(.28f, .48f, .31f), new Color(.64f, .53f, .32f), new Color(.25f, .26f, .29f), new Color(.18f, .42f, .85f), new Color(.85f, .22f, .25f), new Color(.12f, .73f, .86f) };

        [MenuItem("Team5/Rebuild/Map Builder (2D Grid)")]
        public static void Open() => GetWindow<MapBuilderWindow>("Map Builder");

        private void OnEnable()
        {
            if (draft == null)
            {
                draft = CreateInstance<MapDraft>(); draft.hideFlags = HideFlags.HideAndDontSave;
                draft.ResetGrid(width, length);
            }
            Undo.undoRedoPerformed += OnUndo;
            minSize = new Vector2(650, 600);
            wantsMouseMove = true;
            saveChangesMessage = "This grid is a temporary draft. Save it as a Unity Scene before closing, or discard it.";
        }
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndo;
        private void OnDestroy() { if (draft != null) DestroyImmediate(draft); }
        private void OnUndo() { painting = false; messages.Clear(); hasUnsavedChanges = true; Repaint(); }
        private void Changed() { messages.Clear(); hasUnsavedChanges = true; Repaint(); }
        private void Mutate(string label, Action action)
        {
            Undo.RegisterCompleteObjectUndo(draft, label);
            try { action(); Changed(); }
            catch (Exception error) { messages = new List<string> { error.Message }; }
        }
        private bool CanReplaceDraft() => !hasUnsavedChanges || EditorUtility.DisplayDialog("Replace temporary draft?", "Your unsaved grid edits will be replaced. Saved scenes are unaffected.", "Replace draft", "Cancel");

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                HandleSplitterInput();
                using (new GUILayout.AreaScope(new Rect(0, 0, position.width, position.height)))
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.HelpBox("Draw a single-layer map, mark the route, then generate a standalone scene. Ctrl+Z / Ctrl+Y undo grid edits. Grid drafts exist only in this window.", MessageType.Info);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        width = EditorGUILayout.IntField("Width (X)", width);
                        length = EditorGUILayout.IntField("Length (Z)", length);
                        if (GUILayout.Button("New empty grid") && CanReplaceDraft()) Mutate("New Grid", () => draft.ResetGrid(width, length));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Grid: {draft.width} x {draft.length} | Origin: {draft.originX}, {draft.originZ} | Layer: {draft.layer}");
                        var number = Mathf.Max(1, EditorGUILayout.IntField("Level number", draft.levelNumber, GUILayout.Width(210)));
                        if (number != draft.levelNumber) Mutate("Set Draft Level Number", () => draft.levelNumber = number);
                    }
                    tool = GUILayout.SelectionGrid(tool, Tools, 5);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        brushSize = EditorGUILayout.Popup("Brush", brushSize == 3 ? 1 : 0, new[] { "1 x 1", "3 x 3" }) == 1 ? 3 : 1;
                        zoom = EditorGUILayout.Slider("Zoom", zoom, 16, 48);
                        if (GUILayout.Button("Fill grid") && tool < 4 && EditorUtility.DisplayDialog("Fill entire grid?", "Replace all tile types? Route and towers remain and will be validated.", "Fill", "Cancel"))
                            Mutate("Fill Grid", () => { for (var i = 0; i < draft.tiles.Length; i++) draft.tiles[i] = tool; });
                    }
                    if (tool == 4 || tool == 5) EditorGUILayout.HelpBox("Spawn/Goal mark a 3x3 macro region. They do not create nine independent spawn events. Start/end the route on its center.", MessageType.None);
                    if (tool == 7)
                    {
                        EditorGUILayout.HelpBox("Click road centerline points in travel order. Straight segments are filled automatically. Add corners explicitly; paint a full 3-cell-wide corridor first.", MessageType.None);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Undo last route cell") && draft.route.Count > 0) Mutate("Trim Route", () => draft.route.RemoveAt(draft.route.Count - 1));
                            if (GUILayout.Button("Clear route")) Mutate("Clear Route", () => draft.route.Clear());
                        }
                    }
                    if (tool == 8)
                    {
                        towerPrefab = (GameObject)EditorGUILayout.ObjectField("Tower prefab", towerPrefab, typeof(GameObject), false);
                        area = (AttackArea)EditorGUILayout.EnumPopup("Attack direction / area", area);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            towerIsAoe = EditorGUILayout.Toggle("AOE Multi-Target Tower", towerIsAoe);
                            towerGoldBounty = Mathf.Max(0, EditorGUILayout.IntField("Destroy Gold Bounty", towerGoldBounty));
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Directional 1x1"))
                            {
                                towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1.prefab");
                                area = AttackArea.NegativeX;
                                towerIsAoe = false;
                                towerGoldBounty = 25;
                            }
                            if (GUILayout.Button("Square 1x1"))
                            {
                                towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1_Square.prefab");
                                area = AttackArea.Square;
                                towerIsAoe = false;
                                towerGoldBounty = 30;
                            }
                            if (GUILayout.Button("AOE Tower 1x1 (New)"))
                            {
                                towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Tower_Attack_1x1_Square.prefab");
                                area = AttackArea.Square;
                                towerIsAoe = true;
                                towerGoldBounty = 45;
                            }
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Wall 3x1 (X)"))
                                towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Wall_3x1_X.prefab");
                            if (GUILayout.Button("Wall 1x3 (Z)"))
                                towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Wall_3x1_Z.prefab");
                        }
                        if (towerPrefab != null && towerPrefab.GetComponent<BattleTower>()?.IsWall == true)
                            EditorGUILayout.HelpBox("Wall: place on 3 Road cells. HP only; no attacks or gold reward. Destroy it to clear the path.", MessageType.Info);
                        EditorGUILayout.LabelField("Hover: tinted cells show attack coverage. Orange = AOE Tower, Yellow = Single-Target.");
                    }
                    var maxPanelHeight = Mathf.Max(100, position.height - workspaceTop - 128);
                    EditorGUI.BeginChangeCheck();
                    var panelHeight = EditorGUILayout.Slider("Bottom panel height", footerHeight, 100, maxPanelHeight);
                    if (EditorGUI.EndChangeCheck()) { footerHeight = panelHeight; Repaint(); }
                    var headerEnd = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint) workspaceTop = headerEnd.yMax + 2;
                }
                DrawWorkspace();
            }
        }

        private void DrawWorkspace()
        {
            var top = Mathf.Clamp(workspaceTop, 0, Mathf.Max(0, position.height - 228));
            var maxFooter = Mathf.Max(100, position.height - top - 128);
            footerHeight = Mathf.Clamp(footerHeight, 100, maxFooter);
            var dividerY = position.height - footerHeight - 8;
            gridViewport = new Rect(0, top, position.width, Mathf.Max(120, dividerY - top));
            splitterRect = new Rect(0, dividerY, position.width, 8);

            using (new GUILayout.AreaScope(gridViewport)) DrawGrid();

            EditorGUI.DrawRect(splitterRect, new Color(.12f, .12f, .12f));
            EditorGUI.DrawRect(new Rect(splitterRect.center.x - 24, splitterRect.y + 3, 48, 2), new Color(.5f, .5f, .5f));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            GUI.Label(splitterRect, new GUIContent("", "Drag to resize the grid and information panels. Double-click to reset."));

            using (new GUILayout.AreaScope(new Rect(0, dividerY + 8, position.width, footerHeight)))
            using (new EditorGUILayout.VerticalScope(GUILayout.Height(footerHeight)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Read layout from active scene…") && CanReplaceDraft())
                    {
                        try
                        {
                            var read = MapBuilderUtility.Read(SceneManager.GetActiveScene());
                            Mutate("Read Scene Layout", () => EditorUtility.CopySerialized(read, draft));
                            DestroyImmediate(read); width = draft.width; length = draft.length;
                        }
                        catch (Exception error) { messages = new List<string> { error.Message }; }
                    }
                    if (GUILayout.Button("Validate map")) messages = MapBuilderUtility.Validate(draft);
                    if (GUILayout.Button("Generate & preview…")) Generate();
                    using (new EditorGUI.DisabledScope(!generated.IsValid() || !generated.isLoaded || !string.IsNullOrEmpty(generated.path) || generatedSnapshot != EditorJsonUtility.ToJson(draft)))
                        if (GUILayout.Button("Save new level…")) SaveGenerated();
                }
                EditorGUILayout.HelpBox("Read layout imports tiles, route and prefab towers only. Generated copies use the standard system rig. Existing scene files and game-flow targets are never overwritten or reassigned.", MessageType.None);
                panelScroll = EditorGUILayout.BeginScrollView(panelScroll, GUILayout.ExpandHeight(true));
                foreach (var message in messages.Take(60)) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
                if (messages.Count > 60) EditorGUILayout.LabelField($"… {messages.Count - 60} more issues. Fix the first errors and validate again.");
                EditorGUILayout.EndScrollView();
            }
        }

        private void HandleSplitterInput()
        {
            var id = GUIUtility.GetControlID("MapBuilderSplitter".GetHashCode(), FocusType.Passive);
            var e = Event.current;
            if (!GUI.enabled) return;
            if (e.type == EventType.MouseDown && e.button == 0 && splitterRect.Contains(e.mousePosition))
            {
                painting = false;
                if (e.clickCount == 2)
                {
                    footerHeight = 140;
                    resizingSplitter = false;
                }
                else
                {
                    resizingSplitter = true;
                    GUIUtility.hotControl = id;
                    splitStartY = e.mousePosition.y;
                    splitStartHeight = footerHeight;
                }
                Repaint(); e.Use();
            }
            else if (resizingSplitter && e.type == EventType.MouseDrag)
            {
                footerHeight = Mathf.Clamp(splitStartHeight - (e.mousePosition.y - splitStartY),
                    100, Mathf.Max(100, position.height - workspaceTop - 128));
                Repaint(); e.Use();
            }
            else if (resizingSplitter && e.rawType == EventType.MouseUp)
            {
                resizingSplitter = false;
                if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
                if (e.type != EventType.Used) e.Use();
                Repaint();
            }
        }

        private void DrawGrid()
        {
            var pointerInViewport = !resizingSplitter &&
                new Rect(0, 0, gridViewport.width, gridViewport.height).Contains(Event.current.mousePosition);
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(gridViewport.height), GUILayout.ExpandWidth(true));
            var grid = GUILayoutUtility.GetRect(draft.width * zoom + 28, draft.length * zoom + 28, GUILayout.ExpandWidth(false));
            var e = Event.current;
            hover = new Vector2Int(Mathf.FloorToInt((e.mousePosition.x - grid.x - 28) / zoom), draft.length - 1 - Mathf.FloorToInt((e.mousePosition.y - grid.y - 22) / zoom));
            var preview = towerPrefab != null ? towerPrefab.GetComponent<BattleTower>() : null;
            var attackWidth = preview != null ? new SerializedObject(preview).FindProperty("attackWidth").floatValue : 0;
            var routeLabels = draft.route.Select((point, index) => new { point, index }).GroupBy(entry => entry.point).ToDictionary(group => group.Key, group => group.First().index);
            var towerLabels = draft.towers.GroupBy(tower => tower.cell).ToDictionary(group => group.Key, group => group.First());
            for (var x = 0; x < draft.width; x++) GUI.Label(new Rect(grid.x + 28 + x * zoom, grid.y, zoom, 20), x.ToString(), EditorStyles.miniLabel);
            for (var z = 0; z < draft.length; z++)
            {
                GUI.Label(new Rect(grid.x, grid.y + 22 + (draft.length - 1 - z) * zoom, 28, zoom), z.ToString(), EditorStyles.miniLabel);
                for (var x = 0; x < draft.width; x++)
                {
                    var p = new Vector2Int(x, z);
                    var rect = new Rect(grid.x + 28 + x * zoom, grid.y + 22 + (draft.length - 1 - z) * zoom, zoom - 1, zoom - 1);
                    var value = draft.Tile(p);
                    EditorGUI.DrawRect(rect, value < 0 ? new Color(.17f, .17f, .18f) : Colors[value]);
                    if (tool == 8 && preview != null && !preview.IsWall && draft.Inside(hover) && SquadCombatRules.Contains(new Vector3(x - hover.x, 0, z - hover.y), area, preview.Range,
                        attackWidth))
                    {
                        EditorGUI.DrawRect(rect, towerIsAoe ? new Color(1f, 0.35f, 0.1f, 0.35f) : new Color(1f, .8f, .15f, .23f));
                    }
                    foreach (var stamp in draft.towers)
                    {
                        var wall = stamp.prefab != null ? stamp.prefab.GetComponent<BattleTower>() : null;
                        if (wall != null && wall.IsWall && Mathf.Abs(stamp.cell.x - x) <= wall.Footprint.x / 2 &&
                            Mathf.Abs(stamp.cell.y - z) <= wall.Footprint.y / 2)
                            EditorGUI.DrawRect(rect, new Color(0.58f, 0.56f, 0.52f, 0.85f));
                    }
                    towerLabels.TryGetValue(p, out var tower);
                    if (routeLabels.TryGetValue(p, out var routeIndex)) GUI.Label(rect, routeIndex.ToString(), EditorStyles.whiteMiniLabel);
                    if (tower != null)
                    {
                        var symbol = tower.prefab.GetComponent<BattleTower>().IsWall ? "W" : tower.isAoe ? "✹" : tower.area == AttackArea.Square ? "▣" : tower.area == AttackArea.NegativeX ? "←" : tower.area == AttackArea.PositiveX ? "→" : tower.area == AttackArea.PositiveZ ? "↑" : "↓";
                        GUI.Label(rect, symbol, EditorStyles.whiteBoldLabel);
                    }
                }
            }
            if (e.type == EventType.MouseUp) painting = false;
            if (pointerInViewport && GUIUtility.hotControl == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && draft.Inside(hover))
            {
                if (e.type == EventType.MouseDown)
                {
                    Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(draft, "Draw Map");
                    painting = true; lastPaint = hover;
                }
                try
                {
                    if (painting && tool <= 6)
                    {
                        var count = Mathf.Max(Mathf.Abs(hover.x - lastPaint.x), Mathf.Abs(hover.y - lastPaint.y));
                        for (var i = 0; i <= count; i++)
                        {
                            var p = count == 0 ? hover : Vector2Int.RoundToInt(Vector2.Lerp(lastPaint, hover, (float)i / count));
                            draft.Paint(p, tool == 6 ? -1 : tool, tool == 4 || tool == 5 ? 3 : brushSize);
                        }
                        lastPaint = hover; Changed();
                    }
                    else if (e.type == EventType.MouseDown)
                    {
                        if (tool == 7) draft.AppendRoute(hover);
                        if (tool == 8) draft.PlaceTower(hover, towerPrefab, area, towerIsAoe, towerGoldBounty);
                        if (tool == 9) draft.towers.RemoveAll(t => Mathf.Abs(t.cell.x - hover.x) <= t.prefab.GetComponent<BattleTower>().Footprint.x / 2 && Mathf.Abs(t.cell.y - hover.y) <= t.prefab.GetComponent<BattleTower>().Footprint.y / 2);
                        Changed();
                    }
                }
                catch (Exception error) { messages = new List<string> { error.Message }; }
                e.Use();
            }
            EditorGUILayout.EndScrollView();
            if (e.type == EventType.MouseMove) Repaint();
        }

        private bool Generate()
        {
            try
            {
                messages = MapBuilderUtility.Validate(draft);
                if (messages.Any(m => m.StartsWith("ERROR:"))) return false;
                generated = MapBuilderUtility.Generate(draft);
                generatedSnapshot = EditorJsonUtility.ToJson(draft);
                messages = new List<string> { "Generated a new unsaved level. Save it before Play. After further grid edits, generate again." };
                return true;
            }
            catch (Exception error) { messages = new List<string> { error.Message }; return false; }
        }
        private void SaveGenerated()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save new level", "NewMap", "unity", "Choose a NEW scene name. Existing scenes will not be overwritten.", "Assets/Rebuild/Scenes");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                MapBuilderUtility.SaveNew(generated, path);
                hasUnsavedChanges = false;
                messages = new List<string> { "Saved: " + path + ". No test/campaign entry was changed." };
            }
            catch (Exception error) { messages = new List<string> { error.Message }; }
        }
        public override void SaveChanges()
        {
            if (!Generate()) return;
            if (generated.IsValid() && generated.isLoaded && string.IsNullOrEmpty(generated.path)) SaveGenerated();
            if (!hasUnsavedChanges) base.SaveChanges();
        }
    }
}