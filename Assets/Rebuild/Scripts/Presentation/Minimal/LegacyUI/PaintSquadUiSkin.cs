using System;
using TMPro;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Every font, outline material and sprite the Paint Squad look of LegacyUiKit uses. Built by
    // Editor/PaintSquadUiSkinBuilder into Resources/PaintSquadUiSkin.asset. LegacyUiKit falls back to its
    // procedural sprites and the TMP default font when the asset is missing.
    public sealed class PaintSquadUiSkin : ScriptableObject
    {
        public const string ResourcePath = "PaintSquadUiSkin";

        [Header("Fonts (SIL OFL, see PaintSquad/Fonts/LICENSES.md)")]
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset headingFont;
        [Tooltip("Lilita One with a dark outline and drop shadow, for white titles and button labels.")] public Material headingOutline;
        [Tooltip("Lilita One with a thinner outline, for small labels.")] public Material headingOutlineThin;
        [Tooltip("Fredoka with a dark outline, for light body text over the scene.")] public Material bodyOutline;

        [Header("Shapes")]
        public Sprite rounded;
        public Sprite button;
        public Sprite buttonCircle;
        public Sprite panel;
        public Sprite circle;
        public Sprite ring;
        public Sprite pill;
        public Sprite ribbon;
        public Sprite softShadow;
        public Sprite hexPaper;
        public Sprite glow;
        public Sprite white;

        [Header("Icons (file name without the icon_ prefix)")]
        public NamedIcon[] icons = Array.Empty<NamedIcon>();

        public Sprite Icon(string name)
        {
            foreach (var icon in icons)
                if (icon.name == name) return icon.sprite;
            return null;
        }

        [Serializable]
        public struct NamedIcon
        {
            public string name;
            public Sprite sprite;
        }
    }
}
