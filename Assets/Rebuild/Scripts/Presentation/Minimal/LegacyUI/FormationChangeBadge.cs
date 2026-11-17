using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class FormationChangeBadge : MonoBehaviour
    {
        private const float ButtonReferenceSize = 56f;
        private static readonly Color ChipColor = new Color(1f, 0.88f, 0.25f);
        private static readonly Color SpentChipColor = new Color(0.235f, 0.263f, 0.322f);
        private static readonly Color ChipInk = new Color(0.1f, 0.08f, 0f);
        private static readonly Color VeilColor = new Color(0.04f, 0.05f, 0.08f, 0.62f);

        [SerializeField, Tooltip("Chip center relative to the squad button's top-right corner, in 1280-wide reference pixels.")]
        private Vector2 chipOffset = new Vector2(-2f, -2f);
        [SerializeField, Tooltip("Chip diameter including its dark rim, in 1280-wide reference pixels.")]
        private float chipDiameter = 27f;
        [SerializeField, Tooltip("Width of the chip's dark rim, in 1280-wide reference pixels.")]
        private float rimWidth = 2f;
        [SerializeField, Tooltip("Font size of the number on the chip, in 1280-wide reference pixels.")]
        private float numberSize = 15f;

        private FormationChangeBudget budget;
        private Image veil;
        private Image rim;
        private Image fill;
        private TMP_Text number;
        private TMP_Text defeatedLabel;
        private int shown = int.MinValue;

        public static FormationChangeBadge Attach(FormationChangeBudget budget, LegacyBattleHud hud, Button squadButton)
        {
            if (budget == null || squadButton == null) return null;
            var root = LegacyUiKit.CreateRect(squadButton.transform, "Formation Change Badge", Vector2.zero, Vector2.zero);
            LegacyUiKit.Stretch(root);
            root.SetAsLastSibling();
            var badge = root.gameObject.AddComponent<FormationChangeBadge>();
            badge.budget = budget;
            var scale = ((RectTransform)squadButton.transform).sizeDelta.x / ButtonReferenceSize;
            badge.Build(scale, hud != null && hud.LivesLabel != null ? hud.LivesLabel.font : null);
            return badge;
        }

        private void Build(float scale, TMP_FontAsset font)
        {
            veil = LegacyUiKit.CreateImage(transform, "Spent Veil", Vector2.zero, Vector2.zero, VeilColor);
            LegacyUiKit.Stretch(veil.rectTransform);
            var chip = LegacyUiKit.CreateRect(transform, "Chip", Vector2.zero, Vector2.one * chipDiameter);
            chip.anchorMin = chip.anchorMax = Vector2.one;
            chip.anchoredPosition = chipOffset * scale;
            chip.localScale = Vector3.one * scale;
            rim = LegacyUiKit.CreateCircle(chip, "Rim", Vector2.zero, chipDiameter, LegacyUiKit.PanelColor);
            fill = LegacyUiKit.CreateCircle(chip, "Fill", Vector2.zero, chipDiameter - 2f * rimWidth, ChipColor);
            number = LegacyUiKit.CreateText(chip, "Count", string.Empty, Vector2.zero, Vector2.one * chipDiameter, numberSize, ChipInk, FontStyles.Bold);
            if (font != null) number.font = font;
            defeatedLabel=LegacyUiKit.CreateText(transform,"Captain Defeated","CAPTAIN DEFEATED",Vector2.zero,
                new Vector2(170,40)*scale,16*scale,new Color(1f,.35f,.3f),FontStyles.Bold);
            defeatedLabel.rectTransform.anchorMin=defeatedLabel.rectTransform.anchorMax=new Vector2(1,.5f);
            defeatedLabel.rectTransform.pivot=new Vector2(0,.5f);
            defeatedLabel.rectTransform.anchoredPosition=new Vector2(12,0)*scale;
            defeatedLabel.alignment=TextAlignmentOptions.MidlineLeft;
            if(font!=null)defeatedLabel.font=font;
            defeatedLabel.gameObject.SetActive(false);
            Render(FormationChangeRules.Unlimited);
        }

        private void LateUpdate()
        {
            if (budget == null) return;
            var remaining = budget.Remaining;
            if (remaining != shown) Render(remaining);
            defeatedLabel.gameObject.SetActive(budget.CaptainDefeated);
        }

        private void Render(int remaining)
        {
            shown = remaining;
            var limited = remaining != FormationChangeRules.Unlimited;
            rim.enabled = limited;
            fill.enabled = limited;
            number.enabled = limited;
            veil.enabled = limited && remaining == 0;
            if (!limited) return;
            fill.color = remaining > 0 ? ChipColor : SpentChipColor;
            number.color = remaining > 0 ? ChipInk : LegacyUiKit.MutedColor;
            number.text = remaining.ToString();
        }
    }
}
