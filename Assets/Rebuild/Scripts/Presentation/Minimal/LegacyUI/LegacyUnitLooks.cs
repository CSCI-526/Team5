using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public enum SoldierMark
    {
        None,
        Diamond,
        Plate,
        Cross,
        Crown
    }

    public readonly struct SoldierLook
    {
        public const float DiamondSize = 0.13f;
        public const float PlateSize = 0.16f;
        public const float CrossLength = 0.22f;
        public const float CrossWidth = 0.07f;
        public const float CrownStroke = 0.014f;

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

    public static class LegacyUnitLooks
    {
        private const float OuterSize = 0.4f;
        private const float InnerSize = 0.34f;
        private const float TankOuterSize = 0.44f;
        private const float TankInnerSize = 0.3f;
        private const float DiamondTint = 0.6f;

        private static readonly Color attackBody = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color attackOutline = new Color(0.35f, 0.08f, 0.08f);
        private static readonly Color tankBody = new Color(0.5f, 0.56f, 0.65f);
        private static readonly Color tankOutline = new Color(0.16f, 0.18f, 0.22f);
        private static readonly Color tankPlate = new Color(0.74f, 0.79f, 0.86f);
        private static readonly Color healBody = new Color(0.3f, 0.78f, 0.45f);
        private static readonly Color healOutline = new Color(0.07f, 0.26f, 0.12f);
        private static readonly Color healCross = new Color(0.96f, 1f, 0.97f);

        public static SoldierLook For(UnitRole type) => type switch
        {
            UnitRole.Captain => new SoldierLook(new Color(0.72f, 0.56f, 0.28f), new Color(0.36f, 0.26f, 0.10f), OuterSize, InnerSize, SoldierMark.Crown, Color.white),
            UnitRole.Tank => new SoldierLook(tankBody, tankOutline, TankOuterSize, TankInnerSize, SoldierMark.Plate, tankPlate),
            UnitRole.Heal => new SoldierLook(healBody, healOutline, OuterSize, InnerSize, SoldierMark.Cross, healCross),
            _ => new SoldierLook(attackBody, attackOutline, OuterSize, InnerSize, SoldierMark.Diamond, Color.Lerp(attackBody, Color.white, DiamondTint))
        };

        public static string Name(UnitRole type) => type switch
        {
            UnitRole.Attack => "ATTACK",
            UnitRole.Tank => "TANK",
            UnitRole.Heal => "HEAL",
            UnitRole.Captain => "CAPTAIN",
            _ => string.Empty
        };

        public static Color RarityColor(UnitRarity rarity) => rarity switch
        {
            UnitRarity.Common => new Color(0.75f, 0.75f, 0.75f), // Silver
            UnitRarity.Rare => new Color(0.20f, 0.55f, 0.90f), // Blue
            UnitRarity.Epic => new Color(0.58f, 0.25f, 0.85f), // Purple
            UnitRarity.Legendary => new Color(1.00f, 0.75f, 0.15f), // Gold/Orange
            _ => new Color(0.60f, 0.60f, 0.60f)  // Fallback
        };
    }
}