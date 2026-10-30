using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public readonly struct SoldierLook
    {
        public const float DiamondSize = 0.13f;
        public const float PlateSize = 0.16f;
        public const float CrossLength = 0.22f;
        public const float CrossWidth = 0.07f;
        public const float CrownStroke = 0.014f;
        // Shared line segments keep the warehouse, formation and world-space crown identical.
        public static readonly Vector2[] CrownSegments =
        {
            new Vector2(-0.10f, 0.065f), new Vector2(-0.065f, -0.055f),
            new Vector2(-0.065f, -0.055f), new Vector2(0.065f, -0.055f),
            new Vector2(0.065f, -0.055f), new Vector2(0.10f, 0.065f),
            new Vector2(0.10f, 0.065f), new Vector2(0.04f, 0.025f),
            new Vector2(0.04f, 0.025f), new Vector2(0, 0.09f),
            new Vector2(0, 0.09f), new Vector2(-0.04f, 0.025f),
            new Vector2(-0.04f, 0.025f), new Vector2(-0.10f, 0.065f),
            new Vector2(-0.07f, -0.09f), new Vector2(0.07f, -0.09f)
        };

        public SoldierLook(Color body, Color outline, float outerSize, float innerSize, SoldierMark mark, Color markColor)
        {
            Body = body;
            Outline = outline;
            OuterSize = outerSize;
            InnerSize = innerSize;
            Mark = mark;
            MarkColor = markColor;
        }

        public Color Body { get; }

        public Color Outline { get; }

        public float OuterSize { get; }

        public float InnerSize { get; }

        public SoldierMark Mark { get; }

        public Color MarkColor { get; }
    }
}
