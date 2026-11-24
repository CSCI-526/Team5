using Team5.BasicTowerTrial.Presentation.LegacyUI;
using Team5.BasicTowerTrial.Simulation;
using TMPro;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation
{
    internal sealed class TavernUnitHoverCard : MonoBehaviour
    {
        private const float Width = 224;
        private const float Height = 334;
        private const float DisplayScale = .5f;
        private const float Gap = 10;
        private const float Margin = 8;
        private static readonly Color Rim = new Color32(185, 145, 86, 255);
        private static readonly Color Paper = new Color32(248, 227, 190, 255);

        private readonly Vector3[] corners = new Vector3[4];
        private readonly TMP_Text[] values = new TMP_Text[6];
        private RectTransform layer, panel;
        private LegacyUnitIconUI portrait;
        private TMP_Text title, effect;
        private TavernOfferHover hovered;
        private (UnitRole Role, bool Upgraded)? displayed;

        internal static TavernUnitHoverCard Create(RectTransform viewport)
        {
            var layer = LegacyUiKit.CreateRect(viewport, "Tavern offer hover", Vector2.zero, Vector2.zero);
            LegacyUiKit.Stretch(layer);
            var popup = layer.gameObject.AddComponent<TavernUnitHoverCard>();
            popup.layer = layer;
            popup.Build();
            return popup;
        }

        internal void Show(TavernOfferHover offer)
        {
            hovered = offer;
            layer.SetAsLastSibling();
            Refresh();
        }

        internal void Hide(TavernOfferHover offer)
        {
            if (hovered != offer) return;
            hovered = null;
            panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (hovered != null) Refresh();
        }

        private void Refresh()
        {
            var selection = hovered.Unit;
            if (!hovered.isActiveAndEnabled || !selection.HasValue || !MusterZombieRules.IsZombie(selection.Value.Role))
            {
                Hide(hovered);
                return;
            }
            var (role, upgraded) = selection.Value;
            if (selection != displayed)
            {
                displayed = selection;
                var stats = MusterZombieRules.Create(role, upgraded);
                portrait.SetType(role, upgraded);
                title.text = LegacyUnitLooks.Name(role);
                effect.text = MusterZombieCatalog.SpecialEffect(role, upgraded);
                values[0].text = $"{stats.MaxHp:0}";
                values[1].text = stats.Damage == stats.MaximumDamage ? $"{stats.Damage:0}" : $"{stats.Damage:0}–{stats.MaximumDamage:0}";
                values[2].text = ProtectionRating(stats.Defense);
                values[3].text = ProtectionRating(stats.MagicResistance);
                values[4].text = MovementRating(stats.Speed);
                var lives = MusterZombieRules.GoalLifeCost(role);
                values[5].text = lives == 1 ? "1 life" : $"{lives} lives";
            }
            panel.gameObject.SetActive(true);
            Position(hovered.Rect);
        }

        private static string ProtectionRating(float fraction) => fraction switch
        {
            <= 0f => "None",
            <= .30f => "Low",
            <= .60f => "Medium",
            <= .90f => "High",
            < 1f => "Great",
            _ => "Immune"
        };

        private static string MovementRating(float speed)
        {
            // Muster normalizes the Shaman's original speed of 38.4 to 1 world unit per second.
            var originalSpeed = speed * 38.4f;
            return originalSpeed < 21f ? "Slow" : originalSpeed < 45f ? "Medium" : "Fast";
        }

        private void Position(RectTransform offer)
        {
            offer.GetWorldCorners(corners);
            var upperLeft = layer.InverseTransformPoint(corners[1]);
            var upperRight = layer.InverseTransformPoint(corners[2]);
            var viewport = layer.rect;
            var scale = Mathf.Min(DisplayScale, Mathf.Min((viewport.width - Margin * 2) / Width, (viewport.height - Margin * 2) / Height));
            if (scale <= 0f) return;
            var width = Width * scale;
            var height = Height * scale;
            var right = upperRight.x + Gap;
            var left = upperLeft.x - Gap - width;
            var x = right + width <= viewport.xMax - Margin || left < viewport.xMin + Margin ? right : left;

            panel.localScale = Vector3.one * scale;
            panel.localPosition = new Vector3(Mathf.Clamp(x, viewport.xMin + Margin, viewport.xMax - Margin - width),
                Mathf.Clamp(upperLeft.y, viewport.yMin + Margin + height, viewport.yMax - Margin), 0f);
        }

        private void Build()
        {
            panel = LegacyUiKit.CreateImage(layer, "Unit introduction", Vector2.zero, new Vector2(Width, Height),
                new Color32(40, 26, 22, 255), 9).rectTransform;
            panel.pivot = new Vector2(0f, 1f);
            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            LegacyUiKit.CreateImage(panel, "Gold rim", Vector2.zero, new Vector2(Width - 4, Height - 4), Rim, 8);
            LegacyUiKit.CreateImage(panel, "Walnut card", Vector2.zero, new Vector2(Width - 9, Height - 9),
                new Color32(63, 43, 35, 255), 6);

            portrait = LegacyUnitIconUI.Create(panel, new Vector2(0f, 84f), 280f);
            title = Label("Unit name", "", 0f, 200f, 20f, Paper);
            var effectFrame = LegacyUiKit.CreateImage(panel, "Special effect frame", new Vector2(0f, -46f),
                new Vector2(198f, 55f), Rim, 4);
            LegacyUiKit.CreateImage(effectFrame.transform, "Recess", Vector2.zero, new Vector2(196f, 53f),
                new Color32(45, 30, 28, 255), 4);
            effect = LegacyUiKit.CreateText(effectFrame.transform, "Special effect text", "", Vector2.zero,
                new Vector2(188f, 49f), 12f, Paper);
            effect.enableAutoSizing = true;
            effect.fontSizeMin = 10f;
            effect.fontSizeMax = 12f;
            effect.textWrappingMode = TextWrappingModes.Normal;
            effect.overflowMode = TextOverflowModes.Ellipsis;

            Stat(0, "HP", -50, -98);
            Stat(1, "ATK", 50, -98);
            Stat(2, "ARMOR", -50, -122);
            Stat(3, "MAGIC RES", 50, -122);
            Stat(4, "MOVE", -50, -146);
            Stat(5, "GOAL LIVES", 50, -146);
            panel.gameObject.SetActive(false);
        }

        private TMP_Text Label(string name, string text, float y, float width, float size, Color color) =>
            LegacyUiKit.CreateText(panel, name, text, new Vector2(0f, y), new Vector2(width, size + 6), size, color, FontStyles.Bold);

        private void Stat(int index, string label, float x, float y)
        {
            var strip = LegacyUiKit.CreateImage(panel, label, new Vector2(x, y), new Vector2(96, 20),
                new Color32(45, 30, 26, 255), 3);
            TavernStatIcons.Draw(strip.transform, index);
            values[index] = LegacyUiKit.CreateText(strip.transform, "Value", "", new Vector2(11, 0), new Vector2(64, 18),
                14f, Paper, FontStyles.Bold);
            values[index].alignment = TextAlignmentOptions.Left;
        }
    }
}
