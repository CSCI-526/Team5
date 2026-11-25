using System.Collections.Generic;
using Team5.Rebuild.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // One mesh, no colliders, no new scene assets. Uses the same surface predicate as navigation.
    public sealed class TacticalRoadView : MonoBehaviour
    {
        private Mesh mesh;
        private Material roadMaterial, edgeMaterial;
        public static TacticalRoadView Create(BattleSession session, Transform parent, int layer)
        {
            if (session == null || !session.UsesTacticalRoad || session.Grid == null) return null;
            var host = new GameObject("Tactical road surface");
            host.transform.SetParent(parent, false);
            host.layer = layer;
            var view = host.AddComponent<TacticalRoadView>();
            view.Build(session);
            return view;
        }
        private void Build(BattleSession session)
        {
            var shader = Resources.Load<Shader>("BattleObjectiveUnlit");
            if (shader == null) shader = Shader.Find("Team5/Rebuild/BattleObjectiveUnlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            var grid = session.Grid;
            grid.RebuildIndex(new List<string>());
            var cells = new Dictionary<Vector3Int, Vector3>();
            foreach (var cell in grid.SceneCells)
                if (session.IsMovementSurface(cell) && grid.HasClearTop(cell)) cells[cell.Occupancy] = cell.TopCenter;
            var vertices = new List<Vector3>(); var fill = new List<int>(); var edges = new List<int>();
            foreach (var item in cells)
            {
                var center = item.Value + Vector3.up * .012f;
                Quad(vertices, fill, center, .5f, .5f);
                const float edge = .035f;
                if (!cells.ContainsKey(item.Key + Vector3Int.left)) Quad(vertices, edges, center + new Vector3(-.5f + edge, .004f, 0), edge, .5f);
                if (!cells.ContainsKey(item.Key + Vector3Int.right)) Quad(vertices, edges, center + new Vector3(.5f - edge, .004f, 0), edge, .5f);
                if (!cells.ContainsKey(item.Key + new Vector3Int(0, 0, -1))) Quad(vertices, edges, center + new Vector3(0, .004f, -.5f + edge), .5f, edge);
                if (!cells.ContainsKey(item.Key + new Vector3Int(0, 0, 1))) Quad(vertices, edges, center + new Vector3(0, .004f, .5f - edge), .5f, edge);
            }
            for (var i = 0; i < vertices.Count; i++) vertices[i] = transform.InverseTransformPoint(vertices[i]);
            mesh = new Mesh { name = "Walkable road", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16, subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetTriangles(fill, 0); mesh.SetTriangles(edges, 1); mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            // Draw the ground overlay before renderers sorted by world Z at any position.
            renderer.sortingOrder = short.MinValue;
            roadMaterial = MaterialFor(shader, new Color(.76f, .70f, .56f, .68f), 2996);
            edgeMaterial = MaterialFor(shader, new Color(.91f, .85f, .67f, .90f), 2997);
            renderer.sharedMaterials = new[] { roadMaterial, edgeMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        private static Material MaterialFor(Shader shader, Color color, int queue)
        {
            var material = new Material(shader) { renderQueue = queue };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }
        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 center, float x, float z)
        {
            var i = vertices.Count;
            vertices.Add(center + new Vector3(-x, 0, -z)); vertices.Add(center + new Vector3(-x, 0, z));
            vertices.Add(center + new Vector3(x, 0, z)); vertices.Add(center + new Vector3(x, 0, -z));
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (roadMaterial != null) Destroy(roadMaterial);
            if (edgeMaterial != null) Destroy(edgeMaterial);
        }
    }
}
