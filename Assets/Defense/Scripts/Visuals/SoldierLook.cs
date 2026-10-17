using UnityEngine;

namespace ReverseTD.Defense
{
    public readonly struct SoldierLook
    {
        public const float DiamondSize = 0.13f;
        public const float PlateSize = 0.16f;
        public const float CrossLength = 0.22f;
        public const float CrossWidth = 0.07f;

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
