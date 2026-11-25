using System;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionGlyphs
    {
        private static readonly Vector2[] Embers =
        {
            new Vector2(-0.31f, 0.18f), new Vector2(0.24f, 0.29f), new Vector2(0.05f, -0.34f), new Vector2(-0.12f, -0.08f), new Vector2(0.36f, -0.12f),
            new Vector2(-0.38f, -0.25f), new Vector2(0.14f, 0.05f), new Vector2(-0.02f, 0.41f), new Vector2(0.29f, -0.38f), new Vector2(-0.22f, 0.4f)
        };

        public static Texture2D Mask(string name, int size, Func<float, float, float> alphaAt) =>
            Paint(name, size, (x, y) => new Color(1f, 1f, 1f, alphaAt(x, y)));

        public static Texture2D Paint(string name, int size, Func<float, float, Color> colorAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            var half = size / 2f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var color = colorAt((x + 0.5f - half) / half, (y + 0.5f - half) / half);
                    color.a = Mathf.Clamp01(color.a);
                    pixels[y * size + x] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public static float SnowGlyph(float x, float y) => Snow(new Vector2(x, y), 0.085f, 0.9f);

        public static float FlameGlyph(float x, float y) => Flame(new Vector2(x, y));

        public static Color FlameMark(float x, float y)
        {
            var p = new Vector2(x, y);
            var inner = Flame(new Vector2(p.x * 1.9f, p.y * 1.6f + 0.35f));
            var color = Color.Lerp(MinionPalette.Ember, MinionPalette.FlameCore, inner);
            color.a = Flame(p);
            return color;
        }

        public static Color FrostMark(float x, float y)
        {
            var p = new Vector2(x, y);
            var radius = p.magnitude;
            var arms = Snow(p, 0.05f, 0.92f);
            var fill = (1f - Smooth(0.25f, 0.92f, radius)) * 0.42f;
            var core = 1f - Smooth(0.12f, 0.18f, radius);
            var color = Color.Lerp(MinionPalette.IceMark, Color.white, Mathf.Max(core, arms * 0.35f));
            color.a = Mathf.Max(Mathf.Max(arms, fill), core);
            return color;
        }

        public static float ScorchBlot(float x, float y)
        {
            var radius = Mathf.Sqrt(x * x + y * y);
            var angle = Mathf.Atan2(y, x);
            var edge = 0.6f + 0.12f * Mathf.Sin(angle * 7f + 2.1f) + 0.07f * Mathf.Sin(angle * 13f + 4.4f);
            return (1f - Smooth(edge - 0.1f, edge, radius)) * 0.82f;
        }

        public static float ScorchEmbers(float x, float y)
        {
            var p = new Vector2(x, y);
            var alpha = 0f;
            foreach (var ember in Embers) alpha = Mathf.Max(alpha, 1f - Smooth(0.035f, 0.07f, (p - ember).magnitude));
            return alpha;
        }

        public static Color FlameCard(float x, float y)
        {
            var height = y * 0.5f + 0.5f;
            var width = Mathf.Lerp(0.7f, 0.04f, Mathf.Pow(Mathf.Max(0f, height), 0.85f));
            var distance = Mathf.Abs(x) / width;
            var bottom = Smooth(0f, 0.14f, height);
            var core = (1f - Smooth(0f, 0.55f, distance)) * (1f - Smooth(0.15f, 0.6f, height));
            var color = Color.Lerp(MinionPalette.FlameEdge, MinionPalette.FlameTip, core);
            color.a = (1f - Smooth(0.7f, 1f, distance)) * bottom * (1f - Smooth(0.82f, 1f, height));
            return color;
        }

        public static float Smooth(float from, float to, float value)
        {
            var t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float Snow(Vector2 p, float thick, float length)
        {
            var radius = p.magnitude;
            var step = Mathf.PI / 3f;
            var folded = Mathf.Repeat(Mathf.Atan2(p.y, p.x) + step * 0.5f, step) - step * 0.5f;
            var q = new Vector2(Mathf.Cos(folded), Mathf.Abs(Mathf.Sin(folded))) * radius;
            var distance = Segment(q, Vector2.zero, new Vector2(length, 0f));
            distance = Mathf.Min(distance, Segment(q, new Vector2(length * 0.46f, 0f), new Vector2(length * 0.68f, length * 0.22f)));
            distance = Mathf.Min(distance, Segment(q, new Vector2(length * 0.7f, 0f), new Vector2(length * 0.85f, length * 0.15f)));
            return 1f - Smooth(thick, thick + 0.035f, distance);
        }

        private static float Flame(Vector2 p)
        {
            var height = Mathf.Clamp01((p.y + 0.35f) / 1.25f);
            var body = (p - new Vector2(0f, -0.35f)).magnitude - 0.45f;
            var width = 0.45f * Mathf.Pow(1f - height, 0.9f);
            var tip = p.y > -0.35f ? Mathf.Abs(p.x + 0.1f * Mathf.Sin(height * 3.2f)) - width : 1f;
            return 1f - Smooth(-0.02f, 0.02f, Mathf.Min(body, tip));
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }
    }
}
