using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Exactly mirrors the simulation's circle, 76-degree cone, and narrow laser strip hitboxes.
    // All normal towers use Circle or Cone; straight beams are telegraphed precision towers.
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
            fillMaterial = CreateMaterial(new Color(.22f, .6f, 1f, .10f));
            borderMaterial = CreateMaterial(new Color(.22f, .6f, 1f, .58f));
            fill.sharedMaterial = fillMaterial;
            fill.shadowCastingMode = ShadowCastingMode.Off;
            fill.receiveShadows = false;
            border = new GameObject("Range Outline").AddComponent<LineRenderer>();
            border.transform.SetParent(transform, false);
            border.sharedMaterial = borderMaterial;
            border.loop = true;
            border.useWorldSpace = false;
            border.widthMultiplier = .035f;
            border.shadowCastingMode = ShadowCastingMode.Off;
            border.receiveShadows = false;
        }

        private void Refresh()
        {
            var definition = tower != null ? tower.Definition : previewDefinition;
            if (definition == null || fill == null || border == null) return;
            var active = isActiveAndEnabled && visible && !definition.IsWall && (tower == null || tower.IsAlive);
            fill.enabled = border.enabled = active;
            if (!active) return;

            var color = SquadCombatRules.IsLaser(definition.Area)
                ? new Color(1f, .16f, .14f)
                : definition.IsAoe ? new Color(1f, .46f, .16f) : new Color(.27f, .6f, 1f);
            fillMaterial.SetColor("_BaseColor", new Color(color.r, color.g, color.b, .10f));
            borderMaterial.SetColor("_BaseColor", new Color(color.r, color.g, color.b, .72f));

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
            else // legacy Square geometry is retained only for older scenes/test levels.
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
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
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
