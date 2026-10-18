using UnityEngine;

namespace ReverseTD.Defense
{
    public class RangeIndicator : MonoBehaviour
    {
        private const float Alpha = 0.15f;
        private const float BorderWidth = 0.025f;

        private TowerDefinition definition;
        private SpriteRenderer fill;
        private readonly SpriteRenderer[] borders = new SpriteRenderer[4];
        private readonly SpriteRenderer[] arrow = new SpriteRenderer[2];
        private float shownRange;
        private float range;
        private float shownWidth;
        private Color shownColor;

        public void Initialize(TowerDefinition towerDefinition, Vector2 direction, float attackRange)
        {
            definition = towerDefinition;
            range = attackRange;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            fill = CreatePart("Fill");
            for (int i = 0; i < borders.Length; i++) borders[i] = CreatePart("Border " + i);
            for (int i = 0; i < arrow.Length; i++) arrow[i] = CreatePart("Direction " + i);
            Apply();
        }

        private void LateUpdate()
        {
            if (range != shownRange || definition.AttackWidth != shownWidth || definition.Color != shownColor)
            {
                Apply();
            }
        }

        private void Apply()
        {
            shownRange = range;
            shownWidth = definition.AttackWidth;
            shownColor = definition.Color;

            Vector2 middle = new Vector2(shownRange * 0.5f, 0f);
            Color edge = Palette.WithAlpha(shownColor, 0.7f);
            SetPart(fill, middle, new Vector2(shownRange, shownWidth), Palette.WithAlpha(shownColor, Alpha));
            SetPart(borders[0], middle + new Vector2(0f, shownWidth * 0.5f), new Vector2(shownRange, BorderWidth), edge);
            SetPart(borders[1], middle - new Vector2(0f, shownWidth * 0.5f), new Vector2(shownRange, BorderWidth), edge);
            SetPart(borders[2], Vector2.zero, new Vector2(BorderWidth, shownWidth), edge);
            SetPart(borders[3], new Vector2(shownRange, 0f), new Vector2(BorderWidth, shownWidth), edge);

            float arrowLength = Mathf.Min(0.2f, shownWidth * 0.8f, shownRange * 0.5f);
            float inset = arrowLength * 0.3535534f;
            for (int i = 0; i < arrow.Length; i++)
            {
                float sign = i == 0 ? 1f : -1f;
                SetPart(arrow[i], new Vector2(shownRange - inset, inset * sign), new Vector2(arrowLength, BorderWidth), edge);
                arrow[i].transform.localRotation = Quaternion.Euler(0f, 0f, -45f * sign);
            }
        }

        private SpriteRenderer CreatePart(string partName)
        {
            return ShapeSprites.AddPart(transform, partName, ShapeSprites.Square, Vector2.zero, Vector2.one, Color.white, SortingOrders.RangeIndicator);
        }

        private static void SetPart(SpriteRenderer part, Vector2 position, Vector2 size, Color color)
        {
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            part.color = color;
        }
    }
}
