using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Only the circle factory needed by UiKit, copied from old ShapeSprites.
    public static class LegacyUiShapes
    {
        private static Sprite circle;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => circle = null;
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "RK Circle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var center = Vector2.one * (size * 0.5f);
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    var alpha = Mathf.Clamp01(size * 0.5f - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                circle = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
                circle.name = "RK Circle";
                return circle;
            }
        }
    }
}
