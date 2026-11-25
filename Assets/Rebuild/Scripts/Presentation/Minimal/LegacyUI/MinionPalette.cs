using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionPalette
    {
        public static readonly Color Gunmetal = new Color(0.085f, 0.12f, 0.17f);
        public static readonly Color Head = new Color(0.13f, 0.17f, 0.23f);
        public static readonly Color Foundation = new Color(0.19f, 0.24f, 0.31f);
        public static readonly Color BlueStone = new Color(0.22f, 0.4f, 0.66f);
        public static readonly Color Brass = new Color(0.85f, 0.64f, 0.29f);
        public static readonly Color Glass = new Color(0.18f, 0.75f, 0.94f);
        public static readonly Color Trim = new Color(0.64f, 0.72f, 0.8f);
        public static readonly Color Danger = new Color(0.88f, 0.19f, 0.16f);
        public static readonly Color Outline = new Color(0.06f, 0.08f, 0.12f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.3f);
        public static readonly Color Ember = new Color(1f, 0.46f, 0.12f);
        public static readonly Color FlameCore = new Color(1f, 0.86f, 0.38f);
        public static readonly Color FireFace = new Color(0.68f, 0.2f, 0.11f);
        public static readonly Color FireDeep = new Color(0.86f, 0.3f, 0.1f);
        public static readonly Color FireFill = new Color(1f, 0.42f, 0.1f);
        public static readonly Color FlameEdge = new Color(1f, 0.34f, 0.05f);
        public static readonly Color FlameTip = new Color(1f, 0.9f, 0.52f);
        public static readonly Color Scorch = new Color(0.16f, 0.08f, 0.05f);
        public static readonly Color ScorchEmber = new Color(1f, 0.58f, 0.16f);
        public static readonly Color Frost = new Color(0.74f, 0.94f, 1f);
        public static readonly Color IceFace = new Color(0.48f, 0.75f, 0.96f);
        public static readonly Color IceDeep = new Color(0.32f, 0.62f, 0.93f);
        public static readonly Color IceFill = new Color(0.45f, 0.82f, 1f);
        public static readonly Color IceMark = new Color(0.76f, 0.93f, 1f);
        public static readonly Color IceShell = new Color(0.62f, 0.86f, 1f);
        public static readonly Color IceGlyph = new Color(0.92f, 0.98f, 1f);
        public static readonly Color IceSpark = new Color(0.9f, 0.97f, 1f);
        public static readonly Color FireRim = new Color(1f, 0.48f, 0.14f);
        public static readonly Color IceRim = new Color(0.56f, 0.85f, 1f);
        public static readonly Color FireIcon = new Color(1f, 0.54f, 0.16f);
        public static readonly Color IceIcon = new Color(0.85f, 0.95f, 1f);

        public static Color GlowOf(MinionElement element) => element == MinionElement.Fire ? Ember : element == MinionElement.Ice ? Frost : Glass;

        public static Color FillOf(MinionElement element) => element == MinionElement.Fire ? FireFill : element == MinionElement.Ice ? IceFill : Danger;

        public static Color FaceOf(MinionElement element) => element == MinionElement.Fire ? FireFace : element == MinionElement.Ice ? IceFace : BlueStone;

        public static Color TrailOf(MinionElement element) => element == MinionElement.Ice ? IceSpark : Ember;
    }
}
