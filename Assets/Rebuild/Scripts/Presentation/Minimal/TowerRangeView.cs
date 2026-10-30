using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Optional read-only observer. No colliders, targeting or combat commands.
    public sealed class TowerRangeView : MonoBehaviour
    {
        private TowerState tower;
        private TowerDefinition previewDefinition;
        private Mesh mesh;
        private MeshRenderer fill;
        private LineRenderer border;
        private Material fillMaterial, borderMaterial;
        private Vector3[] corners;
        private bool visible = true;
        public Bounds DisplayBounds { get; private set; }
        public int TowerId => tower?.Id ?? 0;

        public void Bind(TowerState state)
        {
            tower = state;
            previewDefinition = null;
            EnsureGeometry();
            Refresh();
        }

        // A map preview needs only the authored range configuration, never a running tower.
        public void BindPreview(TowerDefinition definition)
        {
            tower = null;
            previewDefinition = definition;
            EnsureGeometry();
            Refresh();
        }

        private void EnsureGeometry()
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Tower Range XZ Quad" };
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                fill = gameObject.AddComponent<MeshRenderer>();
                // Same alpha values as the original Team5 indicator.
                fillMaterial = CreateMaterial(new Color(0.25f, 0.55f, 1, 0.15f));
                borderMaterial = CreateMaterial(new Color(0.25f, 0.55f, 1, 0.7f));
                fill.sharedMaterial = fillMaterial;
                fill.shadowCastingMode = ShadowCastingMode.Off;
                fill.receiveShadows = false;
                border = new GameObject("Border").AddComponent<LineRenderer>();
                border.transform.SetParent(transform, false);
                border.sharedMaterial = borderMaterial;
                border.loop = true;
                border.useWorldSpace = true;
                border.positionCount = 4;
                border.widthMultiplier = 0.035f;
                border.shadowCastingMode = ShadowCastingMode.Off;
                border.receiveShadows = false;
            }
        }

        public void SetVisible(bool value) { visible = value; Refresh(); }
        private void LateUpdate() => Refresh();
        private void OnEnable() => Refresh();
        private void OnDisable()
        {
            if (fill != null) fill.enabled = false;
            if (border != null) border.enabled = false;
        }

        private void Refresh()
        {
            var definition = tower != null ? tower.Definition : previewDefinition;
            if (definition == null || fill == null || border == null) return;
            fill.enabled = border.enabled = isActiveAndEnabled && visible && (tower == null || tower.IsAlive);
            var position = tower != null ? tower.Position : definition.Position;
            var bounds = SquadCombatRules.AreaBounds(position, definition.Area, definition.Range, definition.AttackWidth);
            // Top of the tower's support surface; a small lift prevents terrain z-fighting.
            transform.SetPositionAndRotation(bounds.center + Vector3.up * 0.035f, Quaternion.identity);
            transform.localScale = Vector3.one;
            if (DisplayBounds != bounds || mesh.vertexCount == 0)
            {
                var half = bounds.extents;
                corners = new[] { new Vector3(-half.x, 0, -half.z), new Vector3(-half.x, 0, half.z),
                    new Vector3(half.x, 0, half.z), new Vector3(half.x, 0, -half.z) };
                mesh.vertices = corners;
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
            }
            DisplayBounds = bounds;
            for (var i = 0; i < 4; i++) border.SetPosition(i, transform.TransformPoint(corners[i]));
        }

        private static Material CreateMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Tower Range (Runtime)", renderQueue = 3000 };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
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
