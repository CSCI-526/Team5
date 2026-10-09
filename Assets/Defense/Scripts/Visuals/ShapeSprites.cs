using UnityEngine;

namespace ReverseTD.Defense
{
    public static class ShapeSprites
    {
        private const int SquareSize = 4;
        private const int CircleSize = 64;
        private const int RingSize = 256;
        private const float RingThickness = 4f;
        private const int ThickRingSize = 64;
        private const float ThickRingThickness = 6f;
        private const int WaveRingSize = 128;
        private const float WaveRingThickness = 3f;
        private const int DotTileSize = 64;
        private const float DotRadius = 2f;

        private static Sprite square;
        private static Sprite circle;
        private static Sprite ring;
        private static Sprite thickRing;
        private static Sprite waveRing;
        private static Sprite dotTile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            square = null;
            circle = null;
            ring = null;
            thickRing = null;
            waveRing = null;
            dotTile = null;
        }

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    square = CreateSprite("RK Square", SquareSize, SquareAlpha, false);
                }
                return square;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    circle = CreateSprite("RK Circle", CircleSize, CircleAlpha, false);
                }
                return circle;
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = CreateSprite("RK Ring", RingSize, RingAlpha, false);
                }
                return ring;
            }
        }

        public static Sprite ThickRing
        {
            get
            {
                if (thickRing == null)
                {
                    thickRing = CreateSprite("RK Thick Ring", ThickRingSize, ThickRingAlpha, true);
                }
                return thickRing;
            }
        }

        public static Sprite WaveRing
        {
            get
            {
                if (waveRing == null)
                {
                    waveRing = CreateSprite("RK Wave Ring", WaveRingSize, WaveRingAlpha, true);
                }
                return waveRing;
            }
        }

        public static Sprite DotTile
        {
            get
            {
                if (dotTile == null)
                {
                    dotTile = CreateSprite("RK Dot Tile", DotTileSize, DotAlpha, true);
                }
                return dotTile;
            }
        }

        public static SpriteRenderer AddPart(Transform parent, string partName, Sprite sprite, Vector2 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = new Vector3(size.x, size.y, 1f);

            var partRenderer = part.AddComponent<SpriteRenderer>();
            partRenderer.sprite = sprite;
            partRenderer.color = color;
            partRenderer.sortingOrder = sortingOrder;
            return partRenderer;
        }

        private static float SquareAlpha(float distance, float radius)
        {
            return 1f;
        }

        private static float CircleAlpha(float distance, float radius)
        {
            return radius - distance + 0.5f;
        }

        private static float RingAlpha(float distance, float radius)
        {
            return BandAlpha(distance, radius, RingThickness);
        }

        private static float ThickRingAlpha(float distance, float radius)
        {
            return BandAlpha(distance, radius, ThickRingThickness);
        }

        private static float WaveRingAlpha(float distance, float radius)
        {
            return BandAlpha(distance, radius, WaveRingThickness);
        }

        private static float DotAlpha(float distance, float radius)
        {
            return DotRadius - distance + 0.5f;
        }

        private static float BandAlpha(float distance, float radius, float thickness)
        {
            return Mathf.Min(radius - distance + 0.5f, distance - (radius - thickness) + 0.5f);
        }

        private static Sprite CreateSprite(string spriteName, int size, System.Func<float, float, float> alphaAt, bool mipmaps)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipmaps)
            {
                name = spriteName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear
            };

            float radius = size * 0.5f;
            var center = new Vector2(radius, radius);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(alphaAt(distance, radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(mipmaps, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            sprite.name = spriteName;
            return sprite;
        }
    }
}
