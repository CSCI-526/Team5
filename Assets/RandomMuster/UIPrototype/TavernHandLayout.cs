using UnityEngine;

namespace RandomMuster.Prototype
{
    [ExecuteAlways]
    internal sealed class TavernHandLayout : MonoBehaviour
    {
        [SerializeField, Min(1)] private float usableWidth = 568;
        [SerializeField, Min(1)] private float preferredSpacing = 104;
        [SerializeField, Min(0)] private float fanDrop = 17;
        [SerializeField, Range(0, 25)] private float outerAngle = 13;
        [SerializeField, Min(.1f)] private float cardScale = 1.3f;

        private void OnEnable() => Arrange();
        private void OnTransformChildrenChanged() => Arrange();

        public void Arrange()
        {
            var count = 0;
            float widest = 0;
            for (var i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i) is not RectTransform card || !card.gameObject.activeSelf || !card.name.StartsWith("Hand card ", System.StringComparison.Ordinal)) continue;
                count++;
                widest = Mathf.Max(widest, card.rect.width + card.rect.height * Mathf.Sin(outerAngle * Mathf.Deg2Rad));
            }

            if (count == 0) return;
            var scale = Mathf.Min(cardScale, usableWidth / Mathf.Max(1, widest));
            var spacing = Mathf.Min(preferredSpacing, Mathf.Max(0, usableWidth - widest * scale) / Mathf.Max(1, count - 1));
            var middle = (count - 1) * .5f;
            var index = 0;
            for (var i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i) is not RectTransform card || !card.gameObject.activeSelf || !card.name.StartsWith("Hand card ", System.StringComparison.Ordinal)) continue;
                var offset = index++ - middle;
                var fan = offset / Mathf.Max(2, middle);
                card.anchoredPosition = new Vector2(offset * spacing, 85 - fanDrop * fan * fan);
                card.localRotation = Quaternion.Euler(0, 0, -outerAngle * fan);
                card.localScale = Vector3.one * scale;
            }
        }
    }
}
