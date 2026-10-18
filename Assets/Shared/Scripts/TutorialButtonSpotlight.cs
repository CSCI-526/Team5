using ReverseTD.UpgradeUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ReverseTD.Shared
{
    // Four blocking panels leave the original button visible and clickable through a hole.
    public class TutorialButtonSpotlight : MonoBehaviour
    {
        private readonly Image[] shade = new Image[4];
        private readonly Image[] glow = new Image[4];
        private readonly Image[] border = new Image[4];
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform root;
        private RectTransform target;
        private RectTransform pressLabel;

        public static TutorialButtonSpotlight Create(Transform parent, RectTransform button, UnityAction onPress = null)
        {
            RectTransform root = UiKit.CreateRect(parent, "Formation Button Spotlight", Vector2.zero, Vector2.zero);
            UiKit.Stretch(root);
            root.SetAsFirstSibling();
            var spotlight = root.gameObject.AddComponent<TutorialButtonSpotlight>();
            spotlight.root = root;
            spotlight.target = button;
            for (int i = 0; i < 4; i++)
            {
                spotlight.shade[i] = UiKit.CreateImage(root, "Shade " + i, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f));
                spotlight.shade[i].raycastTarget = true;
                spotlight.glow[i] = UiKit.CreateImage(root, "Glow " + i, Vector2.zero, Vector2.zero, UiKit.GoldColor);
                spotlight.border[i] = UiKit.CreateImage(root, "Highlight " + i, Vector2.zero, Vector2.zero, UiKit.GoldColor);
            }
            var label = UiKit.CreateText(root, "Press Hint", "press", Vector2.zero,
                new Vector2(160f, 32f), 24f, Color.white);
            spotlight.pressLabel = label.rectTransform;
            spotlight.pressLabel.anchorMin = spotlight.pressLabel.anchorMax = Vector2.zero;
            if (onPress != null)
            {
                label.raycastTarget = true;
                Button press = label.gameObject.AddComponent<Button>();
                press.targetGraphic = label;
                press.transition = Selectable.Transition.None;
                press.navigation = new Navigation { mode = Navigation.Mode.None };
                press.onClick.AddListener(onPress);
            }
            Canvas.ForceUpdateCanvases();
            spotlight.Refresh();
            return spotlight;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (target == null) return;

            Canvas targetCanvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
                local -= root.rect.min;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            const float padding = 8f;
            float width = root.rect.width;
            float height = root.rect.height;
            float left = Mathf.Clamp(min.x - padding, 0f, width);
            float right = Mathf.Clamp(max.x + padding, 0f, width);
            float bottom = Mathf.Clamp(min.y - padding, 0f, height);
            float top = Mathf.Clamp(max.y + padding, 0f, height);
            pressLabel.anchoredPosition = new Vector2((left + right) * 0.5f, bottom - 28f);

            SetRect(shade[0], 0f, 0f, left, height);
            SetRect(shade[1], right, 0f, width - right, height);
            SetRect(shade[2], left, 0f, right - left, bottom);
            SetRect(shade[3], left, top, right - left, height - top);

            SetFrame(glow, left, bottom, right, top, 8f);
            SetFrame(border, left, bottom, right, top, 3f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < 4; i++)
            {
                border[i].color = new Color(1f, 0.88f, 0.25f, Mathf.Lerp(0.65f, 1f, pulse));
                glow[i].color = new Color(1f, 0.88f, 0.25f, Mathf.Lerp(0.1f, 0.25f, pulse));
            }
        }

        private static void SetFrame(Image[] parts, float left, float bottom, float right, float top, float thickness)
        {
            SetRect(parts[0], left - thickness, bottom - thickness, thickness, top - bottom + 2f * thickness);
            SetRect(parts[1], right, bottom - thickness, thickness, top - bottom + 2f * thickness);
            SetRect(parts[2], left, bottom - thickness, right - left, thickness);
            SetRect(parts[3], left, top, right - left, thickness);
        }

        private static void SetRect(Image image, float x, float y, float width, float height)
        {
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
