using UnityEngine;

namespace ReverseTD.Defense
{
    public static class CombatEffects
    {
        private const float ImpactTime = 0.22f;
        private const float ImpactRingStart = 0.2f;
        private const float ImpactRingAlpha = 0.9f;
        private const float SparkSize = 0.06f;
        private const float SparkStart = 0.15f;
        private const float SparkReach = 0.11f;
        private const float SparkTilt = 20f;

        private const float WaveTime = 0.35f;
        private const float WaveStart = 0.9f;
        private const float WaveEnd = 1.75f;
        private const float WaveAlpha = 0.23f;
        private const float DebrisTime = 0.6f;
        private const float DebrisSpin = 90f;
        private const float ScorchSize = 0.8f;

        private static readonly Color scorchColor = new Color(0.043f, 0.055f, 0.07f, 0.7f);

        private static readonly float[] sparkAngles = { 20f, 95f, 160f, 230f, 300f };

        private static readonly Vector4[] rubblePieces =
        {
            new Vector4(-0.14f, 0.08f, 0.16f, 20f),
            new Vector4(0.15f, -0.11f, 0.13f, 55f),
            new Vector4(0.03f, 0.2f, 0.1f, 35f),
            new Vector4(-0.2f, -0.18f, 0.09f, 10f),
        };

        private static readonly Vector4[] debrisPieces =
        {
            new Vector4(20f, 0.55f, 0.18f, 25f),
            new Vector4(75f, 0.78f, 0.12f, 50f),
            new Vector4(120f, 0.5f, 0.16f, 10f),
            new Vector4(160f, 0.85f, 0.1f, 35f),
            new Vector4(205f, 0.6f, 0.2f, 60f),
            new Vector4(250f, 0.9f, 0.11f, 15f),
            new Vector4(290f, 0.52f, 0.15f, 40f),
            new Vector4(335f, 0.8f, 0.13f, 70f),
            new Vector4(48f, 0.98f, 0.08f, 30f),
            new Vector4(228f, 0.36f, 0.1f, 5f),
        };

        private static readonly int[] debrisShades = { 0, 1, 3, 2, 0, 2, 3, 0, 3, 3 };

        public static void SpawnImpact(Vector3 position, Color color, float diameter)
        {
            EffectSprite ring = EffectSprite.Spawn(
                null, ShapeSprites.ThickRing, Palette.WithAlpha(color, ImpactRingAlpha), SortingOrders.Effect,
                position, 0f, new Vector2(ImpactRingStart, ImpactRingStart));
            ring.Play(ImpactTime, Vector2.zero, new Vector2(diameter, diameter), 0f);

            float reach = diameter * 0.5f + SparkReach - SparkStart;
            var sparkSize = new Vector2(SparkSize, SparkSize);
            for (int i = 0; i < sparkAngles.Length; i++)
            {
                Vector2 direction = Direction(sparkAngles[i]);
                EffectSprite spark = EffectSprite.Spawn(
                    null, ShapeSprites.Square, color, SortingOrders.Effect,
                    position + (Vector3)(direction * SparkStart), sparkAngles[i] + SparkTilt, sparkSize);
                spark.Play(ImpactTime, direction * reach, sparkSize, 0f);
            }
        }

        public static void SpawnWreck(Transform parent, Vector3 position, Color towerColor, float scale)
        {
            Color dark = Palette.Dark(towerColor);

            Transform rubble = new GameObject("Rubble").transform;
            if (parent != null)
            {
                rubble.SetParent(parent, false);
            }
            rubble.SetPositionAndRotation(position, Quaternion.identity);

            ShapeSprites.AddPart(
                rubble, "Scorch", ShapeSprites.Square, Vector2.zero,
                new Vector2(ScorchSize, ScorchSize) * scale, scorchColor, SortingOrders.Rubble);
            for (int i = 0; i < rubblePieces.Length; i++)
            {
                Vector4 piece = rubblePieces[i];
                SpriteRenderer pieceRenderer = ShapeSprites.AddPart(
                    rubble, "Piece", ShapeSprites.Square, new Vector2(piece.x, piece.y) * scale,
                    new Vector2(piece.z, piece.z) * scale, dark, SortingOrders.Rubble + 1);
                pieceRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, piece.w);
            }

            EffectSprite wave = EffectSprite.Spawn(
                parent, ShapeSprites.WaveRing, new Color(1f, 1f, 1f, WaveAlpha), SortingOrders.Effect,
                position, 0f, new Vector2(WaveStart, WaveStart) * scale);
            wave.Play(WaveTime, Vector2.zero, new Vector2(WaveEnd, WaveEnd) * scale, 0f);

            for (int i = 0; i < debrisPieces.Length; i++)
            {
                Vector4 piece = debrisPieces[i];
                var size = new Vector2(piece.z, piece.z) * scale;
                EffectSprite debris = EffectSprite.Spawn(
                    parent, ShapeSprites.Square, DebrisColor(towerColor, debrisShades[i]), SortingOrders.Effect + 1,
                    position, piece.w, size);
                debris.Play(DebrisTime, Direction(piece.x) * (piece.y * scale), size, DebrisSpin);
            }
        }

        private static Color DebrisColor(Color towerColor, int shade)
        {
            switch (shade)
            {
                case 1:
                    return Palette.Dark(towerColor);
                case 2:
                    return Palette.Mid(towerColor);
                case 3:
                    return Palette.Light(towerColor);
                default:
                    return Palette.WithAlpha(towerColor, 1f);
            }
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
