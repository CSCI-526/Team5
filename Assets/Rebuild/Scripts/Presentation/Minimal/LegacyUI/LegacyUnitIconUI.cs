using Team5.Rebuild.Simulation;
using UnityEngine;
using TMPro;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class LegacyUnitIntroductionUI
    {
        /// <summary>
        /// Builds a unit card. Compact cards (Unit Storage) show identity only; the full
        /// stats live behind a click in the upgrade screen's unit details panel.
        /// </summary>
        public static TMP_Text Build(Transform panel, UnitDefinition stats, bool showAvailability = true, bool compact = false)
        {
            var role = stats.Role;
            var rarityColor = LegacyUnitLooks.RarityColor(stats.Rarity);
            // Compact cards drop the stat block, so lower the identity block to keep it centered.
            var y = compact ? -25f : 0f;

            LegacyUiKit.CreateCircle(panel, "Emblem Ring", new Vector2(0, 75 + y), 76, rarityColor);
            var face = LegacyUiKit.CreateCircle(panel, "Emblem", new Vector2(0, 75 + y), 70, LegacyUiKit.PanelColor);
            LegacyUnitIconUI.Create(face.transform, Vector2.zero, 76 * 1.15f).SetType(role);

            LegacyUiKit.CreateText(panel, "Name", $"{LegacyUnitLooks.Name(role)}  Lv.{stats.Level}", new Vector2(0, 18 + y), new Vector2(200, 24), 17,
                LegacyUiKit.TextColor, FontStyles.Bold, 2);

            LegacyUiKit.CreateText(panel, "Rarity", stats.Rarity.ToString().ToUpperInvariant(), new Vector2(0, -2 + y), new Vector2(200, 18), 13,
                rarityColor, FontStyles.Bold, 2);

            var status = showAvailability
                ? LegacyUiKit.CreateText(panel, "Availability", "", new Vector2(0, -20 + y), new Vector2(200, 20), 13, LegacyUiKit.MutedColor)
                : null;

            if (compact)
            {
                LegacyUiKit.CreateImage(panel, "Divider", new Vector2(0, -78), new Vector2(160, 2), rarityColor);
                LegacyUiKit.CreateText(panel, "Details Hint", "CLICK FOR DETAILS", new Vector2(0, -100), new Vector2(200, 20), 13,
                    LegacyUiKit.HighlightColor, FontStyles.Bold, 2);
                return status;
            }

            LegacyUiKit.CreateText(panel, "Role", ShortDescription(role), new Vector2(0, -38), new Vector2(200, 18), 12, LegacyUiKit.MutedColor);
            LegacyUiKit.CreateImage(panel, "Divider", new Vector2(0, -54), new Vector2(160, 2), rarityColor);

            var isAttacker = SquadCombatRules.CanAttackTowers(role);
            var isLegendary = SquadCombatRules.IsLegendaryRole(role);
            var key = isLegendary ? "SKILL" : isAttacker ? "ATK" : role == UnitRole.Tank ? "DEF" : "HEAL";
            var value = isLegendary ? "ACTIVE"
                : isAttacker ? $"{stats.Damage:0.##}"
                : role == UnitRole.Tank ? $"{stats.Defense * 100:0}%"
                : $"{stats.HealPerSecond:0.##}/s";

            BuildStat(panel, key, value, -76);
            BuildStat(panel, "HP", $"{stats.MaxHp:0.##}", -104);
            return status;
        }

        public static string ShortDescription(UnitRole role) => role switch
        {
            UnitRole.Captain => "Formation skill / unique",
            UnitRole.Frostweaver => "Click map: freeze towers",
            UnitRole.Warcaller => "Boost ally tower damage",
            UnitRole.Aegis => "Block 2 hits per ally",
            UnitRole.Tank => "Heavy frontline shield",
            UnitRole.Heal => "Heals 8 neighbors",
            UnitRole.Sniper => "Long-range tower hunter",
            UnitRole.Vanguard => "Armored bruiser / ATK+DEF",
            UnitRole.Demolisher => "2.25x Siege DMG vs Towers",
            _ => "1.5x DMG vs Towers"
        };

        public static string LongDescription(UnitDefinition unit) => unit.Role switch
        {
            UnitRole.Attack => "A steady tower breaker with fast attacks and 1.5x damage against towers. Place it where it can fire while tougher teammates take the hits.",
            UnitRole.Tank => "High health and damage reduction help this unit absorb incoming fire. Protect teammates through positioning. It cannot attack or taunt enemies.",
            UnitRole.Heal => "Heals teammates in the eight neighboring formation slots every 2 seconds. Cannot heal itself. A central position lets it cover more allies.",
            UnitRole.Sniper => "The longest attack range in the squad, with powerful shots and 1.85x damage against towers. Its low health makes safe positioning essential.",
            UnitRole.Vanguard => "An armored fighter that combines damage reduction with tower attacks. Useful in positions that need to absorb damage while helping break towers.",
            UnitRole.Demolisher => "A siege specialist dealing 2.25x damage against towers. Its attacks hit one tower at a time. Keep it protected and within range to make the most of its damage.",
            UnitRole.Captain or UnitRole.Frostweaver or UnitRole.Warcaller or UnitRole.Aegis =>
                LegendarySkillRules.Description(unit.Role, unit.Level),
            _ => "Arrange this unit in your squad before starting the next battle."
        };

        private static void BuildStat(Transform panel, string name, string value, float y)
        {
            var size = new Vector2(148, 24);
            var label = LegacyUiKit.CreateText(panel, "Stat", name, new Vector2(0, y), size, 14, LegacyUiKit.MutedColor, FontStyles.Bold, 2);
            label.alignment = TextAlignmentOptions.Left;
            var number = LegacyUiKit.CreateText(panel, "Value", value, new Vector2(0, y), size, 18, LegacyUiKit.TextColor, FontStyles.Bold);
            number.alignment = TextAlignmentOptions.Right;
        }
    }

    public class LegacyUnitIconUI : MonoBehaviour
    {
        private const float ShadowAlpha = 0.5f;
        private static readonly Vector2 shadowOffset = new Vector2(0.03f, -0.05f);

        private RectTransform rect;
        private float pixelsPerUnit;
        private UnitRole shown = UnitRole.Test;

        public RectTransform Rect => rect;

        public static LegacyUnitIconUI Create(Transform parent, Vector2 position, float pixelsPerUnit)
        {
            var rect = LegacyUiKit.CreateRect(parent, "Unit Icon", position, Vector2.one * pixelsPerUnit * 0.5f);
            var icon = rect.gameObject.AddComponent<LegacyUnitIconUI>();
            icon.rect = rect;
            icon.pixelsPerUnit = pixelsPerUnit;
            return icon;
        }

        public void SetType(UnitRole type)
        {
            if (type == shown) return;
            shown = type;

            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            if (type == UnitRole.Test) return;

            var look = LegacyUnitLooks.For(type);
            var k = pixelsPerUnit;
            var outer = Vector2.one * look.OuterSize * k;
            LegacyUiKit.CreateImage(transform, "Shadow", shadowOffset * k, outer, new Color(0f, 0f, 0f, ShadowAlpha));
            LegacyUiKit.CreateImage(transform, "Outline", Vector2.zero, outer, look.Outline);
            LegacyUiKit.CreateImage(transform, "Body", Vector2.zero, Vector2.one * look.InnerSize * k, look.Body);

            switch (look.Mark)
            {
                case SoldierMark.Crown:
                    for (var i = 0; i < SoldierLook.CrownSegments.Length; i += 2)
                    {
                        var start = SoldierLook.CrownSegments[i];
                        var end = SoldierLook.CrownSegments[i + 1];
                        var delta = end - start;
                        var line = LegacyUiKit.CreateImage(transform, "Crown " + i, (start + end) * (k / 2),
                            new Vector2(delta.magnitude, SoldierLook.CrownStroke) * k, look.MarkColor);
                        line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    }
                    break;
                case SoldierMark.Diamond:
                    LegacyUiKit.CreateImage(transform, "Mark", Vector2.zero, Vector2.one * SoldierLook.DiamondSize * k, look.MarkColor)
                        .rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case SoldierMark.Plate:
                    LegacyUiKit.CreateImage(transform, "Mark", Vector2.zero, Vector2.one * SoldierLook.PlateSize * k, look.MarkColor);
                    break;
                case SoldierMark.Cross:
                    LegacyUiKit.CreateImage(transform, "Mark Across", Vector2.zero, new Vector2(SoldierLook.CrossLength, SoldierLook.CrossWidth) * k, look.MarkColor);
                    LegacyUiKit.CreateImage(transform, "Mark Upright", Vector2.zero, new Vector2(SoldierLook.CrossWidth, SoldierLook.CrossLength) * k, look.MarkColor);
                    break;
            }
        }
    }
}
