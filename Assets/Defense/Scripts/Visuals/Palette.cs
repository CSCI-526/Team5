using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense
{
    public static class Palette
    {
        private const float DarkShade = 0.42f;
        private const float MidShade = 0.78f;
        private const float LightTint = 0.42f;

        private static readonly Color towerShotColor = new Color(0.6f, 0.95f, 1f);
        private static readonly Color soldierShotColor = new Color(1f, 0.8f, 0.25f);
        private static readonly Color flashCoreColor = new Color(1f, 0.965f, 0.84f, 0.95f);

        public static Color FlashCore => flashCoreColor;

        public static Color Shade(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
        }

        public static Color Tint(Color color, float amount)
        {
            return new Color(
                Mathf.Lerp(color.r, 1f, amount),
                Mathf.Lerp(color.g, 1f, amount),
                Mathf.Lerp(color.b, 1f, amount),
                1f);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static Color Dark(Color color)
        {
            return Shade(color, DarkShade);
        }

        public static Color Mid(Color color)
        {
            return Shade(color, MidShade);
        }

        public static Color Light(Color color)
        {
            return Tint(color, LightTint);
        }

        public static Color ShotColor(Team targetTeam)
        {
            return targetTeam == Team.Defender ? soldierShotColor : towerShotColor;
        }
    }
}
