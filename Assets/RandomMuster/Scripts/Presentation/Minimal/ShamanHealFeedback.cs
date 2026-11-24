using UnityEngine;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    [DefaultExecutionOrder(50)]
    public sealed class ShamanHealFeedback : MonoBehaviour
    {
        private const float Lifetime = .9f;
        private static readonly Color FlashColor = new(1f, .52f, .48f);
        private static readonly Color HealColor = new(1f, .19f, .23f);

        private Transform recipient;
        private SpriteRenderer[] sprites;
        private Text icon;
        private float shownAt;

        public static void Show(Transform target)
        {
            var overlay = new GameObject($"Heal Feedback - {target.name}", typeof(Canvas));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            var feedback = overlay.AddComponent<ShamanHealFeedback>();
            feedback.recipient = target;
            feedback.sprites = target.GetComponentsInChildren<SpriteRenderer>();
            feedback.shownAt = Time.unscaledTime;

            var marker = new GameObject("Red Cross", typeof(RectTransform), typeof(Text), typeof(Outline));
            marker.transform.SetParent(overlay.transform, false);
            var icon = marker.GetComponent<Text>();
            icon.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            icon.fontStyle = FontStyle.Bold;
            icon.fontSize = 28;
            icon.text = "+";
            icon.alignment = TextAnchor.MiddleCenter;
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(42f, 42f);
            var outline = marker.GetComponent<Outline>();
            outline.effectColor = new Color(.24f, .025f, .04f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            feedback.icon = icon;
            feedback.LateUpdate();
        }

        private void LateUpdate()
        {
            var elapsed = Time.unscaledTime - shownAt;
            if (elapsed >= Lifetime || recipient == null || !recipient.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                icon.enabled = false;
                return;
            }

            var top = camera.WorldToScreenPoint(recipient.position);
            foreach (var sprite in sprites)
            {
                if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy) continue;
                var bounds = sprite.bounds;
                var point = camera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
                if (point.y > top.y) top = point;
            }

            icon.enabled = top.z > 0f;
            var progress = elapsed / Lifetime;
            var resolutionScale = Mathf.Clamp(Screen.height / 720f, .75f, 2.5f);
            var rise = Mathf.SmoothStep(0f, 12f, progress);
            var pulse = Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(elapsed / .21f));
            icon.rectTransform.position = new Vector3(top.x, top.y + (rise - 6f) * resolutionScale, 0f);
            icon.rectTransform.localScale = Vector3.one * (pulse * resolutionScale);

            var color = Color.Lerp(FlashColor, HealColor, Mathf.Clamp01(elapsed / .12f));
            color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.52f, 1f, progress));
            icon.color = color;
        }
    }
}
