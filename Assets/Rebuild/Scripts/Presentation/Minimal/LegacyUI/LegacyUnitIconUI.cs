
using Team5.Rebuild.Simulation;
using UnityEngine;
using TMPro;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Shared introduction content for warehouse members and Claim Card previews.
    public static class LegacyUnitIntroductionUI
    {
        public static TMP_Text Build(Transform panel, UnitDefinition stats, bool showAvailability = true)
        {
            var role = stats.Role;
            LegacyUiKit.CreateCircle(panel, "Emblem Ring", new Vector2(0, 75), 76, LegacyUiKit.PanelLineColor);
            var face = LegacyUiKit.CreateCircle(panel, "Emblem", new Vector2(0, 75), 70, LegacyUiKit.PanelColor);
            LegacyUnitIconUI.Create(face.transform, Vector2.zero, 76 * 1.15f).SetType(role);
            LegacyUiKit.CreateText(panel, "Name", LegacyUnitLooks.Name(role), new Vector2(0, 15), new Vector2(200, 24), 18,
                LegacyUiKit.TextColor, FontStyles.Bold, 4);
            var status = showAvailability ? LegacyUiKit.CreateText(panel, "Availability", "", new Vector2(0, -8), new Vector2(200, 20), 14, LegacyUiKit.MutedColor) : null;
            var description = role == UnitRole.Captain ? "Shoots towers / DEF 50%" : role == UnitRole.Tank ? "Takes less damage" : role == UnitRole.Heal ? "Heals neighbors" : "Shoots towers";
            LegacyUiKit.CreateText(panel, "Role", description, new Vector2(0, -32), new Vector2(200, 20), 13, LegacyUiKit.MutedColor);
            LegacyUiKit.CreateImage(panel, "Divider", new Vector2(0, -52), new Vector2(160, 2), LegacyUiKit.PanelLineColor);
            var key = role == UnitRole.Attack || role == UnitRole.Captain ? "ATK" : role == UnitRole.Tank ? "DEF" : "HEAL";
            var value = role == UnitRole.Attack || role == UnitRole.Captain ? $"{stats.Damage:0.##}" : role == UnitRole.Tank ? $"{stats.Defense * 100:0}%" : $"{stats.HealPerSecond:0.##}/s";
            BuildStat(panel, key, value, -76);
            BuildStat(panel, "HP", $"{stats.MaxHp:0.##}", -104);
            return status;
        }

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
            RectTransform rect = LegacyUiKit.CreateRect(parent, "Unit Icon", position, Vector2.one * pixelsPerUnit * 0.5f);
            var icon = rect.gameObject.AddComponent<LegacyUnitIconUI>();
            icon.rect = rect;
            icon.pixelsPerUnit = pixelsPerUnit;
            return icon;
        }

        public void SetType(UnitRole type)
        {
            if (type == shown)
            {
                return;
            }
            shown = type;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            if (type == UnitRole.Test)
            {
                return;
            }

            SoldierLook look = LegacyUnitLooks.For(type);
            float k = pixelsPerUnit;
            Vector2 outer = Vector2.one * look.OuterSize * k;
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
