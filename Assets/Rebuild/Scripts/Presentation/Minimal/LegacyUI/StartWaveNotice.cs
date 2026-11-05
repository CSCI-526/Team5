using TMPro;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class StartWaveNotice : MonoBehaviour
    {
        private const string Message = "Drag a unit into your squad to start";
        private static readonly Color EmptyCountColor = new Color(1f, 0.35f, 0.35f);

        [SerializeField, Tooltip("Gap in pixels between the bottom of Start Wave and the center of the notice.")] private float gap = 18f;

        private LegacyUpgradeScene owner;
        private RectTransform root;
        private RectTransform button;
        private TMP_Text label;
        private TMP_Text count;
        private Color countColor;
        private bool shown;

        public static StartWaveNotice Attach(LegacyUpgradeScene owner, RectTransform root)
        {
            if (owner == null || root == null || owner.StartWaveButton == null) return null;
            var label = LegacyUiKit.CreateText(root, "Start Wave Notice", Message, Vector2.zero, new Vector2(480f, 26f), 16,
                LegacyUiKit.HighlightColor, FontStyles.Bold);
            var buttonText = owner.StartWaveButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null) label.font = buttonText.font;
            label.enabled = false;
            var notice = label.gameObject.AddComponent<StartWaveNotice>();
            notice.owner = owner;
            notice.root = root;
            notice.label = label;
            notice.button = (RectTransform)owner.StartWaveButton.transform;
            notice.count = owner.SquadCountLabel;
            if (notice.count != null) notice.countColor = notice.count.color;
            return notice;
        }

        private void Update()
        {
            var empty = owner != null && owner.Preparation != null && owner.Preparation.DeployedCount == 0;
            if (empty != shown)
            {
                shown = empty;
                label.enabled = empty;
                if (count != null) count.color = empty ? EmptyCountColor : countColor;
            }
            if (shown) Place();
        }

        private void Place()
        {
            var rect = button.rect;
            var bottom = root.InverseTransformPoint(button.TransformPoint(new Vector3(rect.center.x, rect.yMin, 0f)));
            label.rectTransform.anchoredPosition = new Vector2(bottom.x, bottom.y - gap);
        }

        private void OnDestroy()
        {
            if (count != null) count.color = countColor;
        }
    }
}
