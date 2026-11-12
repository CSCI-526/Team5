using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Runs after unit interpolation and inspection camera movement. Presentation only.
    [DefaultExecutionOrder(300)]
    public sealed class WallOcclusionView : MonoBehaviour
    {
        private sealed class WallVisual
        {
            public Renderer Renderer;
            public Material[] Original, Faded;
            public ShadowCastingMode Shadows;
            public bool IsFaded;
        }
        private readonly Dictionary<BattleTower, List<WallVisual>> walls = new Dictionary<BattleTower, List<WallVisual>>();
        private readonly List<Vector3[]> units = new List<Vector3[]>();
        private readonly Vector3[] projectedWall = new Vector3[8];
        private BattlePresentation presentation;
        private BattleSimulation simulation;
        public void Bind(BattlePresentation owner) { Restore(); presentation = owner; }

        private void LateUpdate()
        {
            if (presentation == null || !presentation.isActiveAndEnabled) return;
            var session = presentation.Session;
            var next = session != null ? session.Simulation : null;
            if (next != simulation) { Restore(); simulation = next; }
            var camera = Camera.main;
            if (simulation == null || camera == null) { Restore(); return; }
            units.Clear();
            foreach (var unit in simulation.Units)
                if (unit.IsAlive && presentation.TryGetView(unit.Id, out var view) && view.isActiveAndEnabled &&
                    view.TryGetVisibleBounds(out var bounds))
                {
                    var points = new Vector3[8];
                    Project(camera, bounds, points);
                    units.Add(points);
                }
            foreach (var wall in session.Towers)
            {
                if (!wall.IsWall) continue;
                if (!walls.TryGetValue(wall, out var visuals))
                {
                    visuals = new List<WallVisual>();
                    foreach (var renderer in wall.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer is LineRenderer) continue;
                        visuals.Add(new WallVisual { Renderer = renderer, Original = renderer.sharedMaterials,
                            Shadows = renderer.shadowCastingMode });
                    }
                    walls.Add(wall, visuals);
                }
                var occluded = false;
                if (wall.RuntimeState != null && wall.RuntimeState.IsAlive)
                    foreach (var visual in visuals)
                    {
                        if (visual.Renderer == null || !visual.Renderer.enabled || !visual.Renderer.gameObject.activeInHierarchy) continue;
                        Project(camera, visual.Renderer.bounds, projectedWall);
                        foreach (var unit in units)
                            if (WallOcclusionGeometry.Occludes(projectedWall, unit)) { occluded = true; break; }
                        if (occluded) break;
                    }
                // Fade the whole wall, retaining the brick gaps and its original stone palette.
                foreach (var visual in visuals) SetFaded(visual, occluded);
            }
        }

        private static void Project(Camera camera, Bounds bounds, Vector3[] result)
        {
            var i = 0;
            for (var z = -1; z <= 1; z += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var x = -1; x <= 1; x += 2)
                result[i++] = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
        }

        private static void SetFaded(WallVisual visual, bool faded)
        {
            if (visual.Renderer == null || visual.IsFaded == faded) return;
            if (faded && visual.Faded == null)
            {
                visual.Faded = new Material[visual.Original.Length];
                for (var i = 0; i < visual.Original.Length; i++)
                {
                    var source = visual.Original[i];
                    if (source == null) continue;
                    var material = new Material(source) { name = source.name + " (Wall See Through)" };
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_Blend", 0);
                    material.SetFloat("_ZWrite", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.DisableKeyword("_ALPHAMODULATE_ON");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetShaderPassEnabled("DepthOnly", false);
                    material.SetShaderPassEnabled("DepthNormals", false);
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    var color = material.GetColor("_BaseColor");
                    color.a = .50f;
                    material.SetColor("_BaseColor", color);
                    visual.Faded[i] = material;
                }
            }
            visual.Renderer.sharedMaterials = faded ? visual.Faded : visual.Original;
            visual.Renderer.shadowCastingMode = faded ? ShadowCastingMode.Off : visual.Shadows;
            visual.IsFaded = faded;
        }

        private void Restore()
        {
            foreach (var visuals in walls.Values)
                foreach (var visual in visuals)
                {
                    SetFaded(visual, false);
                    if (visual.Faded != null)
                        foreach (var material in visual.Faded) if (material != null) Destroy(material);
                }
            walls.Clear();
            units.Clear();
            simulation = null;
        }
        private void OnDisable() => Restore();
    }

    // Conservative projected-silhouette test: includes even tiny edge overlaps, not just center rays.
    public static class WallOcclusionGeometry
    {
        public static bool Occludes(Vector3[] wall, Vector3[] unit)
        {
            var nearestWall = float.PositiveInfinity;
            var farthestWall = float.NegativeInfinity;
            var farthestUnit = float.NegativeInfinity;
            foreach (var p in wall) { nearestWall = Mathf.Min(nearestWall, p.z); farthestWall = Mathf.Max(farthestWall, p.z); }
            foreach (var p in unit) farthestUnit = Mathf.Max(farthestUnit, p.z);
            if (farthestWall <= 0 || farthestUnit <= 0 || nearestWall >= farthestUnit) return false;
            var a = Hull(wall);
            var b = Hull(unit);
            return a.Count >= 3 && b.Count >= 3 && !Separated(a, b) && !Separated(b, a);
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        private static List<Vector2> Hull(Vector3[] points)
        {
            var sorted = new List<Vector2>();
            foreach (var p in points) sorted.Add(new Vector2(p.x, p.y));
            sorted.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var hull = new List<Vector2>();
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            var lower = hull.Count;
            for (var i = sorted.Count - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;
        }
        private static bool Separated(List<Vector2> a, List<Vector2> b)
        {
            for (var i = 0; i < a.Count; i++)
            {
                var edge = a[(i + 1) % a.Count] - a[i];
                var axis = new Vector2(-edge.y, edge.x);
                var minA = float.PositiveInfinity; var maxA = float.NegativeInfinity;
                var minB = float.PositiveInfinity; var maxB = float.NegativeInfinity;
                foreach (var p in a) { var d = Vector2.Dot(p, axis); minA = Mathf.Min(minA, d); maxA = Mathf.Max(maxA, d); }
                foreach (var p in b) { var d = Vector2.Dot(p, axis); minB = Mathf.Min(minB, d); maxB = Mathf.Max(maxB, d); }
                if (maxA < minB || maxB < minA) return true;
            }
            return false;
        }
    }
}
