using TMPro;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class TowerBountyLabel
    {
        private readonly RectTransform canvas;
        private readonly RectTransform rect;
        private readonly TextMeshProUGUI text;
        private Vector3 anchor;
        private float age = -1f;

        public TowerBountyLabel(RectTransform canvas)
        {
            this.canvas = canvas;
            text = LegacyUiKit.CreateText(canvas, "Bounty Label", string.Empty, Vector2.zero, new Vector2(240f, 64f),
                TowerFeedbackRules.LabelFontSize, LegacyUiKit.Gold, FontStyles.Bold);
            LegacyUiKit.Outlined(text);
            rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.gameObject.SetActive(false);
        }

        public void Play(Vector3 worldAnchor, string content)
        {
            anchor = worldAnchor;
            text.text = content;
            age = 0f;
        }

        public void Hide()
        {
            age = -1f;
            if (rect != null) rect.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime, Camera view)
        {
            if (age < 0f) return;
            age += deltaTime;
            if (age >= TowerFeedbackRules.LabelSeconds)
            {
                Hide();
                return;
            }
            if (view == null) return;
            var screen = view.WorldToScreenPoint(anchor);
            var local = Vector2.zero;
            var visible = screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out local);
            if (rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
            if (!visible) return;
            var progress = age / TowerFeedbackRules.LabelSeconds;
            rect.anchoredPosition = local + Vector2.up * (TowerFeedbackRules.LabelRise * TowerFeedbackRules.EaseOut(progress));
            text.alpha = age < 0.1f ? age / 0.1f : progress > 0.66f ? Mathf.Clamp01((1f - progress) / 0.34f) : 1f;
            rect.localScale = Vector3.one * (age < 0.15f ? 1.3f - 0.3f * (age / 0.15f) : 1f);
        }
    }
}
