using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.StageDiagnostics
{
    // No dependency on any gameplay assembly. Never saves or modifies scenes.
    public static class StageContextDiagnostics
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "StageDiagnostics");
        private static string Stamp => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", Inv);
        [Serializable] private class Snapshot
        {
            public string scene, scenePath, phase, utc, unityVersion;
            public List<Node> objects = new List<Node>();
            public List<string> dependencies = new List<string>();
            public List<string> runtimeState = new List<string>();
            public List<string> warnings = new List<string>();
        }
        [Serializable] private class Node
        {
            public int id, parentId, layer;
            public string name, hierarchy, prefabPath, tag, cellKind;
            public bool activeSelf, activeInHierarchy;
            public Vector3 position, rotation, scale;
            public List<Part> components = new List<Part>();
        }
        [Serializable] private class Part
        {
            public int id;
            public string type, scriptPath, serialized, assetPath;
            public bool enabled, hasBounds;
            public Bounds bounds;
            public List<string> references = new List<string>();
            public List<string> values = new List<string>();
            public Vector3[] linePositions;
        }

        [MenuItem("Team5/Diagnostics/Export saved campaign scenes (1-7)")]
        public static void ExportCampaign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before exporting saved scenes."); return; }
            var folder = Path.Combine(Output, "Saved_" + Stamp);
            Directory.CreateDirectory(folder);
            var errors = new List<string>();
            for (int level = 1; level <= 7; level++)
            {
                string path = "Assets/Rebuild/Scenes/Level" + level + "GridBattle.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { errors.Add("Missing: " + path); continue; }
                Scene scene = default;
                try
                {
                    // Preview scenes avoid replacing the developer's open working scene.
                    scene = EditorSceneManager.OpenPreviewScene(path);
                    Capture(scene, folder, "saved");
                }
                catch (Exception e) { errors.Add(path + ": " + e); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }
            File.WriteAllLines(Path.Combine(folder, "export_errors.txt"), errors.ToArray());
            Debug.Log("Stage diagnostics saved: " + folder + " (" + errors.Count + " scene errors)");
        }

        [MenuItem("Team5/Diagnostics/Export current scene snapshot")]
        public static void ExportCurrent()
        {
            var folder = Path.Combine(Output, (Application.isPlaying ? "Play_" : "Edit_") + Stamp);
            Directory.CreateDirectory(folder);
            var scene = SceneManager.GetActiveScene();
            Capture(scene, folder, Application.isPlaying ? "play" : "edit");
            // Save a Game View image when in play mode; no screenshots in edit mode.
            if (Application.isPlaying) ScreenCapture.CaptureScreenshot(Path.Combine(folder, "game_view.png"));
            Debug.Log("Stage diagnostics saved: " + folder);
        }

        private static void Capture(Scene scene, string folder, string phase)
        {
            var data = new Snapshot { scene = scene.name, scenePath = scene.path, phase = phase,
                utc = DateTime.UtcNow.ToString("o", Inv), unityVersion = Application.unityVersion };
            if (!string.IsNullOrEmpty(scene.path)) data.dependencies.AddRange(AssetDatabase.GetDependencies(scene.path, true));
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                var node = new Node { id = go.GetInstanceID(), parentId = t.parent == null ? 0 : t.parent.gameObject.GetInstanceID(),
                    name = go.name, hierarchy = Hierarchy(t), prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go),
                    tag = go.tag, layer = go.layer, activeSelf = go.activeSelf, activeInHierarchy = go.activeInHierarchy,
                    position = t.position, rotation = t.eulerAngles, scale = t.lossyScale };
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) { data.warnings.Add("Missing component: " + node.hierarchy); continue; }
                    var type = component.GetType();
                    var part = new Part { id = component.GetInstanceID(), type = type.FullName, enabled = true };
                    if (component is Behaviour behaviour) part.enabled = behaviour.enabled;
                    if (component is Renderer renderer)
                    {
                        part.enabled = renderer.enabled; part.hasBounds = true; part.bounds = renderer.bounds;
                        foreach (var material in renderer.sharedMaterials) if (material != null)
                            part.values.Add("material=" + material.name + " | " + AssetDatabase.GetAssetPath(material));
                    }
                    if (component is MeshFilter filter && filter.sharedMesh != null)
                        part.assetPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (component is SpriteRenderer sprite && sprite.sprite != null)
                        part.assetPath = AssetDatabase.GetAssetPath(sprite.sprite);
                    if (component is Collider collider) { part.enabled = collider.enabled; part.hasBounds = true; part.bounds = collider.bounds; }
                    if (component is Collider2D collider2D) { part.enabled = collider2D.enabled; part.hasBounds = true; part.bounds = collider2D.bounds; }
                    if (component is LineRenderer line)
                    {
                        int count = Math.Min(line.positionCount, 256);
                        part.linePositions = new Vector3[count];
                        for (int i = 0; i < count; i++)
                        {
                            var p = line.GetPosition(i);
                            part.linePositions[i] = line.useWorldSpace ? p : line.transform.TransformPoint(p);
                        }
                        part.values.Add("linePositionCount=" + line.positionCount);
                    }
                    if (component is MonoBehaviour mb)
                    {
                        var script = MonoScript.FromMonoBehaviour(mb);
                        if (script != null) part.scriptPath = AssetDatabase.GetAssetPath(script);
                    }
                    bool gameplay = (type.Namespace ?? "").StartsWith("Team5", StringComparison.Ordinal);
                    if (gameplay || component is Camera || component is LineRenderer || component is Light)
                    {
                        try
                        {
                            part.serialized = EditorJsonUtility.ToJson(component, false);
                            var so = new SerializedObject(component);
                            var prop = so.GetIterator();
                            while (prop.Next(true))
                                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                                {
                                    var target = prop.objectReferenceValue;
                                    if (target != null) part.references.Add(prop.propertyPath + " => " + Describe(target));
                                }
                            if (type.Name == "BattleCell") node.cellKind = Convert.ToString(Read(component, "Kind"), Inv);
                        }
                        catch (Exception e) { data.warnings.Add(node.hierarchy + " / " + type.Name + ": " + e.Message); }
                    }
                    if (phase == "play" && type.Name == "BattleSession") CaptureRuntime(component, data.runtimeState);
                    if (phase == "play" && type.Name == "BattleTower")
                    {
                        var state = Read(component, "RuntimeState");
                        if (state != null) { part.values.Add(Summary(state)); part.values.Add(Summary(Read(state, "Definition"))); }
                    }
                    node.components.Add(part);
                }
                data.objects.Add(node);
            }
            string name = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            File.WriteAllText(Path.Combine(folder, name + ".json"), JsonUtility.ToJson(data, true));
            WriteMap(data, Path.Combine(folder, name + "_map.svg"));
        }

        private static string Hierarchy(Transform t)
        {
            return t.parent == null ? t.name + "[" + t.GetSiblingIndex() + "]" : Hierarchy(t.parent) + "/" + t.name + "[" + t.GetSiblingIndex() + "]";
        }
        private static string Describe(Object value)
        {
            var asset = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(asset)) return asset + " | " + value.name;
            if (value is Component c) return Hierarchy(c.transform) + " | " + c.GetType().Name + " | id=" + c.GetInstanceID();
            if (value is GameObject go) return Hierarchy(go.transform) + " | id=" + go.GetInstanceID();
            return value.name + " | id=" + value.GetInstanceID();
        }
        private static object Read(object instance, string property)
        {
            if (instance == null) return null;
            try { return instance.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance); }
            catch { return null; }
        }
        private static string Summary(object instance)
        {
            if (instance == null) return "null";
            var sb = new StringBuilder(instance.GetType().Name);
            foreach (var p in instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                if (p.GetIndexParameters().Length != 0 || !(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Vector3) || type == typeof(Vector2Int) || type == typeof(Vector3Int))) continue;
                sb.Append(" | ").Append(p.Name).Append('=').Append(Convert.ToString(Read(instance, p.Name), Inv));
            }
            return sb.ToString();
        }
        private static void CaptureRuntime(object session, List<string> rows)
        {
            rows.Add(Summary(session));
            var sim = Read(session, "Simulation");
            if (sim == null) return;
            rows.Add(Summary(sim));
            foreach (string property in new[] { "Towers", "Units", "Projectiles" })
                if (Read(sim, property) is IEnumerable items)
                    foreach (var item in items)
                    {
                        rows.Add(property + ": " + Summary(item));
                        var definition = Read(item, "Definition");
                        if (definition != null) rows.Add("  " + Summary(definition));
                    }
        }
        private static string F(float v) => v.ToString("0.###", Inv);
        private static string Xml(string value) => System.Security.SecurityElement.Escape(value ?? "") ?? "";
        private static void WriteMap(Snapshot data, string path)
        {
            var cells = data.objects.Where(n => !string.IsNullOrEmpty(n.cellKind)).ToList();
            if (cells.Count == 0) return;
            float minX = cells.Min(n => n.position.x) - 2, maxX = cells.Max(n => n.position.x) + 2;
            float minZ = cells.Min(n => n.position.z) - 2, maxZ = cells.Max(n => n.position.z) + 2;
            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='{F(minX)} {F(-maxZ)} {F(maxX-minX)} {F(maxZ-minZ)}' width='1000' height='800'>");
            sb.AppendLine($"<rect x='{F(minX)}' y='{F(-maxZ)}' width='{F(maxX-minX)}' height='{F(maxZ-minZ)}' fill='#283b38'/>");
            foreach (var n in cells.OrderBy(n => n.position.y))
            {
                string color = n.cellKind == "Road" ? "#cfbc8b" : n.cellKind == "Spawn" ? "#438bdd" : n.cellKind == "Goal" ? "#47c89b" : n.cellKind == "Obstacle" ? "#344e48" : n.cellKind == "TowerPlatform" ? "#8b838f" : "#658b75";
                sb.AppendLine($"<rect x='{F(n.position.x-.5f)}' y='{F(-n.position.z-.5f)}' width='1' height='1' fill='{color}' stroke='#35463e' stroke-width='.025'><title>{Xml(n.hierarchy)} {Xml(n.cellKind)}</title></rect>");
            }
            foreach (var n in data.objects.Where(n => n.components.Any(c => c.type.EndsWith(".BattleTower") || c.type.EndsWith(".OpenStageThreat"))))
            {
                bool tower = n.components.Any(c => c.type.EndsWith(".BattleTower"));
                sb.AppendLine($"<circle cx='{F(n.position.x)}' cy='{F(-n.position.z)}' r='.32' fill='{(tower ? "#e45950" : "#f6b34e")}' stroke='white' stroke-width='.06'><title>{Xml(n.hierarchy)} | active={n.activeInHierarchy}</title></circle>");
            }
            sb.AppendLine("</svg>"); File.WriteAllText(path, sb.ToString());
        }
    }
}
