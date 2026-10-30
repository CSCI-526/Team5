using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
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

        public static SoldierLook For(UnitRole type)
        {
            switch (type)
            {
                case UnitRole.Captain:
                    return new SoldierLook(new Color(0.72f, 0.56f, 0.28f), new Color(0.36f, 0.26f, 0.10f),
                        OuterSize, InnerSize, SoldierMark.Crown, Color.white);
                case UnitRole.Tank:
                    return new SoldierLook(tankBody, tankOutline, TankOuterSize, TankInnerSize, SoldierMark.Plate, tankPlate);
                case UnitRole.Heal:
                    return new SoldierLook(healBody, healOutline, OuterSize, InnerSize, SoldierMark.Cross, healCross);
                default:
                    return new SoldierLook(attackBody, attackOutline, OuterSize, InnerSize, SoldierMark.Diamond,
                        Color.Lerp(attackBody, Color.white, DiamondTint));
            }
        }

        public static string Name(UnitRole type)
        {
            switch (type)
            {
                case UnitRole.Attack:
                    return "ATTACK";
                case UnitRole.Tank:
                    return "TANK";
                case UnitRole.Heal:
                    return "HEAL";
                case UnitRole.Captain:
                    return "CAPTAIN";
                default:
                    return string.Empty;
            }
        }
    }
}
