using System;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionIcons
    {
        private const int Size = 128;
        private static Sprite flame;
        private static Sprite snow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            flame = null;
            snow = null;
        }

        public static Sprite Flame => flame != null ? flame : (flame = Create("Minion Flame Icon", MinionGlyphs.FlameGlyph));

        public static Sprite Snow => snow != null ? snow : (snow = Create("Minion Snow Icon", MinionGlyphs.SnowGlyph));

        public static Sprite Of(MinionElement element) => element == MinionElement.Fire ? Flame : element == MinionElement.Ice ? Snow : null;

        private static Sprite Create(string name, Func<float, float, float> alphaAt)
        {
            var sprite = Sprite.Create(MinionGlyphs.Mask(name, Size, alphaAt), new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }
    }
}
