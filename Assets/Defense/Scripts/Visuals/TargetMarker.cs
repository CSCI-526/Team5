using UnityEngine;

namespace ReverseTD.Defense
{
    public class TargetMarker : MonoBehaviour
    {
        private const float RingDiameter = 0.64f;
        private const float TickStart = 0.2f;
        private const float TickEnd = 0.4f;
        private const float TickWidth = 0.06f;

        private static readonly Color markColor = new Color(0.93f, 0.11f, 0.14f);

        public void Initialize()
        {
            int order = SortingOrders.TargetMarker;
            float tickLength = TickEnd - TickStart;
            float tickCenter = (TickStart + TickEnd) * 0.5f;
            var across = new Vector2(tickLength, TickWidth);
            var upright = new Vector2(TickWidth, tickLength);

            ShapeSprites.AddPart(transform, "Ring", ShapeSprites.ThickRing, Vector2.zero, new Vector2(RingDiameter, RingDiameter), markColor, order);
            ShapeSprites.AddPart(transform, "Tick Right", ShapeSprites.Square, new Vector2(tickCenter, 0f), across, markColor, order);
            ShapeSprites.AddPart(transform, "Tick Left", ShapeSprites.Square, new Vector2(-tickCenter, 0f), across, markColor, order);
            ShapeSprites.AddPart(transform, "Tick Up", ShapeSprites.Square, new Vector2(0f, tickCenter), upright, markColor, order);
            ShapeSprites.AddPart(transform, "Tick Down", ShapeSprites.Square, new Vector2(0f, -tickCenter), upright, markColor, order);
            gameObject.SetActive(false);
        }

        public void Show(Vector3 position)
        {
            transform.position = position;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
