using UnityEngine;

namespace ReverseTD.Defense
{
    public class EffectSprite : MonoBehaviour
    {
        private const string ObjectName = "Effect";

        private SpriteRenderer spriteRenderer;
        private Color startColor;
        private Vector3 startPosition;
        private Vector2 startSize;
        private Vector2 endSize;
        private Vector2 travel;
        private float startAngle;
        private float spin;
        private float lifetime;
        private float age;

        public static EffectSprite Spawn(Transform parent, Sprite sprite, Color color, int sortingOrder, Vector3 position, float angle, Vector2 size)
        {
            var effectObject = new GameObject(ObjectName);
            Transform effectTransform = effectObject.transform;
            if (parent != null)
            {
                effectTransform.SetParent(parent, false);
            }
            effectTransform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            effectTransform.localScale = new Vector3(size.x, size.y, 1f);

            var effectRenderer = effectObject.AddComponent<SpriteRenderer>();
            effectRenderer.sprite = sprite;
            effectRenderer.color = color;
            effectRenderer.sortingOrder = sortingOrder;

            var effect = effectObject.AddComponent<EffectSprite>();
            effect.spriteRenderer = effectRenderer;
            effect.startColor = color;
            effect.startPosition = position;
            effect.startSize = size;
            effect.endSize = size;
            effect.startAngle = angle;
            return effect;
        }

        public void Play(float duration, Vector2 travelOffset, Vector2 finalSize, float spinDegrees)
        {
            lifetime = duration;
            travel = travelOffset;
            endSize = finalSize;
            spin = spinDegrees;
        }

        private void LateUpdate()
        {
            if (lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float progress = Mathf.Clamp01(age / lifetime);
            float eased = 1f - (1f - progress) * (1f - progress);

            transform.SetPositionAndRotation(
                startPosition + (Vector3)(travel * eased),
                Quaternion.Euler(0f, 0f, startAngle + spin * eased));

            Vector2 size = Vector2.Lerp(startSize, endSize, eased);
            transform.localScale = new Vector3(size.x, size.y, 1f);
            spriteRenderer.color = Palette.WithAlpha(startColor, startColor.a * (1f - progress));

            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            age += Time.deltaTime;
        }
    }
}
