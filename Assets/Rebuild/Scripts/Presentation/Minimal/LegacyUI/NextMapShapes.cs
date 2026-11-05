using System;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class NextMapShapes
    {
        private const int TriangleSize = 64;
        private const int HatchSize = 24;
        private const float HatchPeriod = 12f;
        private const float HatchStripe = 3f;

        private static Sprite triangle;
        private static Sprite hatch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            triangle = null;
            hatch = null;
        }

        public static Sprite Triangle => triangle != null ? triangle : (triangle = CreateTriangle());
        public static Sprite Hatch => hatch != null ? hatch : (hatch = CreateHatch());

        private static Sprite CreateTriangle()
        {
            var tip = new Vector2(TriangleSize - 6f, TriangleSize / 2f);
            var bottom = new Vector2(10f, 6f);
            var top = new Vector2(10f, TriangleSize - 6f);
            return Create("Next Map Triangle", TriangleSize, TextureWrapMode.Clamp, TriangleSize, point =>
                Mathf.Min(Edge(point, tip, bottom), Mathf.Min(Edge(point, bottom, top), Edge(point, top, tip))));
        }

        private static Sprite CreateHatch()
        {
            return Create("Next Map Hatch", HatchSize, TextureWrapMode.Repeat, 100f, point =>
            {
                var along = Mathf.Repeat(point.x + point.y, HatchPeriod);
                return (HatchStripe / 2f - Mathf.Abs(along - HatchPeriod / 2f)) / Mathf.Sqrt(2f);
            });
        }

        private static float Edge(Vector2 point, Vector2 from, Vector2 to)
        {
            var edge = to - from;
            return Vector2.Dot(point - from, new Vector2(edge.y, -edge.x).normalized);
        }

        private static Sprite Create(string name, int size, TextureWrapMode wrap, float pixelsPerUnit, Func<Vector2, float> inside)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var alpha = Mathf.Clamp01(inside(new Vector2(x + 0.5f, y + 0.5f)) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
