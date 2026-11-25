using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionMeshes
    {
        private const int Segments = 14;
        private const int Rings = 8;

        public static Mesh Box()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Face(vertices, normals, uvs, triangles, Vector3.up, Vector3.right, Vector3.forward);
            Face(vertices, normals, uvs, triangles, Vector3.down, Vector3.right, Vector3.back);
            Face(vertices, normals, uvs, triangles, Vector3.forward, Vector3.left, Vector3.up);
            Face(vertices, normals, uvs, triangles, Vector3.back, Vector3.right, Vector3.up);
            Face(vertices, normals, uvs, triangles, Vector3.right, Vector3.forward, Vector3.up);
            Face(vertices, normals, uvs, triangles, Vector3.left, Vector3.back, Vector3.up);
            return Build("Minion Box", vertices, normals, uvs, triangles);
        }

        public static Mesh Ball()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var ring = 0; ring <= Rings; ring++)
            {
                var v = (float)ring / Rings;
                var polar = v * Mathf.PI;
                for (var segment = 0; segment <= Segments; segment++)
                {
                    var u = (float)segment / Segments;
                    var azimuth = u * Mathf.PI * 2f;
                    var normal = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    vertices.Add(normal * 0.5f);
                    normals.Add(normal);
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }
            var stride = Segments + 1;
            for (var ring = 0; ring < Rings; ring++)
                for (var segment = 0; segment < Segments; segment++)
                {
                    var a = ring * stride + segment;
                    var b = a + stride;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            return Build("Minion Ball", vertices, normals, uvs, triangles);
        }

        public static Mesh Tube()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var segment = 0; segment <= Segments; segment++)
            {
                var u = (float)segment / Segments;
                var angle = u * Mathf.PI * 2f;
                var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(normal * 0.5f + Vector3.down * 0.5f);
                vertices.Add(normal * 0.5f + Vector3.up * 0.5f);
                normals.Add(normal);
                normals.Add(normal);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }
            for (var segment = 0; segment < Segments; segment++)
            {
                var a = segment * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            Cap(vertices, normals, uvs, triangles, 0.5f, Vector3.up);
            Cap(vertices, normals, uvs, triangles, -0.5f, Vector3.down);
            return Build("Minion Tube", vertices, normals, uvs, triangles);
        }

        public static Mesh Flat()
        {
            var vertices = new List<Vector3> { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            var normals = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var uvs = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            return Build("Minion Flat", vertices, normals, uvs, new List<int> { 0, 1, 2, 0, 2, 3 });
        }

        public static Mesh Cone()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var apex = Vector3.up * 0.5f;
            var bottom = Vector3.down * 0.5f;
            for (var segment = 0; segment < Segments; segment++)
            {
                var from = segment * Mathf.PI * 2f / Segments;
                var to = (segment + 1) * Mathf.PI * 2f / Segments;
                var a = new Vector3(Mathf.Cos(from) * 0.5f, -0.5f, Mathf.Sin(from) * 0.5f);
                var b = new Vector3(Mathf.Cos(to) * 0.5f, -0.5f, Mathf.Sin(to) * 0.5f);
                Facet(vertices, normals, uvs, triangles, a, apex, b, Slope(from), Slope((from + to) * 0.5f), Slope(to), Vector2.zero);
                Facet(vertices, normals, uvs, triangles, bottom, a, b, Vector3.down, Vector3.down, Vector3.down, Vector2.zero);
            }
            return Build("Minion Cone", vertices, normals, uvs, triangles);
        }

        public static Mesh Gem()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var corners = new[] { Vector3.right * 0.5f, Vector3.forward * 0.5f, Vector3.left * 0.5f, Vector3.back * 0.5f };
            foreach (var tip in new[] { Vector3.up * 0.5f, Vector3.down * 0.5f })
                for (var i = 0; i < corners.Length; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % corners.Length];
                    var normal = (tip + a + b).normalized;
                    Facet(vertices, normals, uvs, triangles, tip, a, b, normal, normal, normal, Vector2.zero);
                }
            return Build("Minion Gem", vertices, normals, uvs, triangles);
        }

        public static Mesh HexPrism()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var corners = new Vector3[6];
            for (var i = 0; i < corners.Length; i++)
            {
                var angle = (i + 0.5f) * Mathf.PI / 3f;
                corners[i] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
            }
            var lift = Vector3.up * 0.5f;
            for (var i = 0; i < corners.Length; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Length];
                var side = ((a + b) * 0.5f).normalized;
                var shade = new Vector2((1.5f + i % 3) / 4f, 0.5f);
                Facet(vertices, normals, uvs, triangles, a - lift, a + lift, b + lift, side, side, side, shade);
                Facet(vertices, normals, uvs, triangles, a - lift, b + lift, b - lift, side, side, side, shade);
                Facet(vertices, normals, uvs, triangles, lift, a + lift, b + lift, Vector3.up, Vector3.up, Vector3.up, new Vector2(0.125f, 0.5f));
                Facet(vertices, normals, uvs, triangles, -lift, a - lift, b - lift, Vector3.down, Vector3.down, Vector3.down, new Vector2(0.875f, 0.5f));
            }
            return Build("Minion Hex Prism", vertices, normals, uvs, triangles);
        }

        public static Mesh Card()
        {
            var vertices = new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            var normals = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uvs = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            return Build("Minion Card", vertices, normals, uvs, new List<int> { 0, 1, 2, 0, 2, 3 });
        }

        private static Vector3 Slope(float angle) => new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)).normalized;

        private static void Facet(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 normalA, Vector3 normalB, Vector3 normalC, Vector2 uv)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
            {
                var corner = b;
                b = c;
                c = corner;
                var normal = normalB;
                normalB = normalC;
                normalC = normal;
            }
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normalA);
            normals.Add(normalB);
            normals.Add(normalC);
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static void Face(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 normal, Vector3 right, Vector3 up)
        {
            var start = vertices.Count;
            var center = normal * 0.5f;
            vertices.Add(center - right * 0.5f - up * 0.5f);
            vertices.Add(center - right * 0.5f + up * 0.5f);
            vertices.Add(center + right * 0.5f + up * 0.5f);
            vertices.Add(center + right * 0.5f - up * 0.5f);
            for (var i = 0; i < 4; i++) normals.Add(normal);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        private static void Cap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, float height, Vector3 normal)
        {
            var center = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (var segment = 0; segment <= Segments; segment++)
            {
                var angle = (float)segment / Segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, height, Mathf.Sin(angle) * 0.5f));
                normals.Add(normal);
                uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
            for (var segment = 0; segment < Segments; segment++)
            {
                var a = center + 1 + segment;
                if (normal.y > 0f) triangles.AddRange(new[] { center, a + 1, a });
                else triangles.AddRange(new[] { center, a, a + 1 });
            }
        }

        private static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
