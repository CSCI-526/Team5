using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Exactly mirrors the simulation's circle, 76-degree cone, and narrow laser strip hitboxes.
    // V7 readability fix: restore stronger fill/outline while keeping the cleaner V6 styling.
    public sealed class TowerRangeView : MonoBehaviour
    {
        private TowerState tower;
        private TowerDefinition previewDefinition;
        private Mesh mesh;
        private MeshRenderer fill;
        private LineRenderer border;
        private Material fillMaterial, borderMaterial;
        private bool visible = true;
        private AttackArea lastArea = (AttackArea)(-1);
        private float lastRange = -1, lastWidth = -1;
        private Vector3[] points = System.Array.Empty<Vector3>();
        public Bounds DisplayBounds { get; private set; }
        public int TowerId => tower?.Id ?? 0;

        public void Bind(TowerState state) { tower = state; previewDefinition = null; EnsureGeometry(); Refresh(); }
        public void BindPreview(TowerDefinition definition) { tower = null; previewDefinition = definition; EnsureGeometry(); Refresh(); }
        public void SetVisible(bool value) { visible = value; Refresh(); }
        private void LateUpdate() => Refresh();
        private void OnEnable() => Refresh();
        private void OnDisable()
        {
            if (fill != null) fill.enabled = false;
            if (border != null) border.enabled = false;
        }

        private void EnsureGeometry()
        {
            if (mesh != null) return;
            mesh = new Mesh { name = "Tower Attack Shape XZ" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            fill = gameObject.AddComponent<MeshRenderer>();
            fillMaterial = CreateMaterial(new Color(.22f, .60f, 1f, .08f));
            borderMaterial = CreateMaterial(new Color(.22f, .60f, 1f, .62f));
            fill.sharedMaterial = fillMaterial;
            fill.sortingOrder = short.MinValue + 10;
            fill.shadowCastingMode = ShadowCastingMode.Off;
            fill.receiveShadows = false;
            border = new GameObject("Range Outline").AddComponent<LineRenderer>();
            border.transform.SetParent(transform, false);
            border.gameObject.layer = gameObject.layer;
            border.sharedMaterial = borderMaterial;
            border.sortingOrder = short.MinValue + 11;
            border.loop = true;
            border.useWorldSpace = false;
            border.widthMultiplier = .034f;
            border.numCornerVertices = 4;
            border.numCapVertices = 4;
            border.shadowCastingMode = ShadowCastingMode.Off;
            border.receiveShadows = false;
        }

        private void Refresh()
        {
            var definition = tower != null ? tower.Definition : previewDefinition;
            if (definition == null || fill == null || border == null) return;
            var active = isActiveAndEnabled && visible && !definition.IsWall && definition.Range > 0 && definition.Damage > 0 && (tower == null || tower.IsAlive);
            fill.enabled = border.enabled = active;
            if (!active) return;

            var isLaser = SquadCombatRules.IsLaser(definition.Area);
            var baseColor = isLaser
                ? new Color(.97f, .20f, .18f)
                : definition.IsAoe ? new Color(1f, .54f, .18f) : new Color(.13f, .57f, 1f);
            var fillColor = new Color(baseColor.r, baseColor.g, baseColor.b, isLaser ? .20f : .105f);
            var borderColor = new Color(baseColor.r, baseColor.g, baseColor.b, isLaser ? .96f : .84f);
            border.widthMultiplier = isLaser ? .085f : .065f;
            if (fillMaterial.HasProperty("_BaseColor")) fillMaterial.SetColor("_BaseColor", fillColor);
            if (fillMaterial.HasProperty("_Color")) fillMaterial.SetColor("_Color", fillColor);
            if (borderMaterial.HasProperty("_BaseColor")) borderMaterial.SetColor("_BaseColor", borderColor);
            if (borderMaterial.HasProperty("_Color")) borderMaterial.SetColor("_Color", borderColor);

            var position = tower != null ? tower.Position : definition.Position;
            transform.SetPositionAndRotation(position + Vector3.up * .035f, Quaternion.identity);
            transform.localScale = Vector3.one;
            DisplayBounds = SquadCombatRules.AreaBounds(position, definition.Area, definition.Range, definition.AttackWidth);
            if (definition.Area != lastArea || !Mathf.Approximately(definition.Range, lastRange) ||
                !Mathf.Approximately(definition.AttackWidth, lastWidth))
            {
                lastArea = definition.Area; lastRange = definition.Range; lastWidth = definition.AttackWidth;
                CreatePolygon(definition.Area, definition.Range, definition.AttackWidth);
            }
        }

        private void CreatePolygon(AttackArea area, float range, float width)
        {
            var outline = new List<Vector3>();
            if (SquadCombatRules.IsCircle(area))
            {
                for (var i = 0; i < 48; i++)
                {
                    var radians = i * Mathf.PI * 2f / 48f;
                    outline.Add(new Vector3(Mathf.Cos(radians) * range, 0f, Mathf.Sin(radians) * range));
                }
            }
            else if (SquadCombatRules.IsCone(area))
            {
                outline.Add(Vector3.zero);
                var dir = SquadCombatRules.Direction(area);
                var origin = Mathf.Atan2(dir.z, dir.x);
                for (var i = 0; i <= 24; i++)
                {
                    var angle = origin + Mathf.Lerp(-SquadCombatRules.ConeHalfAngleDegrees,
                        SquadCombatRules.ConeHalfAngleDegrees, i / 24f) * Mathf.Deg2Rad;
                    outline.Add(new Vector3(Mathf.Cos(angle) * range, 0f, Mathf.Sin(angle) * range));
                }
            }
            else if (SquadCombatRules.IsLaser(area))
            {
                var forward = SquadCombatRules.Direction(area);
                var side = new Vector3(-forward.z, 0f, forward.x) * (width * .5f);
                outline.Add(-side); outline.Add(forward * range - side);
                outline.Add(forward * range + side); outline.Add(side);
            }
            else
            {
                outline.Add(new Vector3(-range, 0, -range)); outline.Add(new Vector3(-range, 0, range));
                outline.Add(new Vector3(range, 0, range)); outline.Add(new Vector3(range, 0, -range));
            }
            points = outline.ToArray();
            var vertices = new Vector3[points.Length + 1];
            vertices[0] = Vector3.zero;
            for (var i = 0; i < points.Length; i++) vertices[i + 1] = points[i];
            var triangles = new int[points.Length * 3];
            for (var i = 0; i < points.Length; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = ((i + 1) % points.Length) + 1;
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
            border.positionCount = points.Length;
            for (var i = 0; i < points.Length; i++) border.SetPosition(i, points[i]);
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Resources.Load<Shader>("TacticalRangeOverlay") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Tower Range (Runtime)", renderQueue = 3000 };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (fillMaterial != null) Destroy(fillMaterial);
            if (borderMaterial != null) Destroy(borderMaterial);
        }
    }
}
