using UnityEngine;

namespace ReverseTD.Defense
{
    public class HealthBar : MonoBehaviour
    {
        private const float DefaultHeight = 0.13f;
        private const float DefaultInset = 0.025f;

        private static readonly Color backgroundColor = new Color(0.055f, 0.067f, 0.082f, 0.92f);
        private static readonly Color fullColor = new Color(0.35f, 0.85f, 0.4f);
        private static readonly Color emptyColor = new Color(0.9f, 0.25f, 0.2f);

        private SpriteRenderer fillRenderer;
        private Transform fill;
        private float fillWidth;
        private float fillHeight;

        public void Initialize(float barWidth, float barHeight = DefaultHeight, float inset = DefaultInset)
        {
            fillWidth = barWidth - inset * 2f;
            fillHeight = barHeight - inset * 2f;
            CreatePart("Background", backgroundColor, SortingOrders.HealthBar).transform.localScale = new Vector3(barWidth, barHeight, 1f);
            fillRenderer = CreatePart("Fill", fullColor, SortingOrders.HealthBarFill);
            fill = fillRenderer.transform;
            SetFraction(1f);
        }

        public void SetFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            fill.localScale = new Vector3(fillWidth * fraction, fillHeight, 1f);
            fill.localPosition = new Vector3((fraction - 1f) * fillWidth * 0.5f, 0f, 0f);
            fillRenderer.color = Color.Lerp(emptyColor, fullColor, fraction);
        }

        private SpriteRenderer CreatePart(string partName, Color color, int sortingOrder)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);

            var partRenderer = part.AddComponent<SpriteRenderer>();
            partRenderer.sprite = ShapeSprites.Square;
            partRenderer.color = color;
            partRenderer.sortingOrder = sortingOrder;
            return partRenderer;
        }
    }
}
