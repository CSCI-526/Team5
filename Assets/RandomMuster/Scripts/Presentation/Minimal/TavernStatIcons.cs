using Team5.BasicTowerTrial.Presentation.LegacyUI;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation
{
    internal static class TavernStatIcons
    {
        private static readonly Color Ink = new Color32(28, 22, 21, 255);
        private static readonly Color Ivory = new Color32(233, 223, 201, 255);
        private static readonly Color Gold = new Color32(233, 188, 87, 255);

        internal static void Draw(Transform parent, int stat)
        {
            var icon = LegacyUiKit.CreateRect(parent, "Stat icon", new Vector2(-35, 0), new Vector2(21, 21));
            switch (stat)
            {
                case 0:
                    Shape(icon, Ink, -10, 4, -8, 8, -4, 9, 0, 6, 4, 9, 8, 8, 10, 4, 9, 0, 6, -4, 0, -9, -6, -4, -9, 0);
                    Shape(icon, new Color32(214, 61, 38, 255), -8, 4, -6, 7, -3, 7, 0, 4, 3, 7, 6, 7, 8, 4, 7, 0, 0, -7, -7, 0);
                    Shape(icon, new Color32(255, 154, 97, 255), 4, 6, 6, 5, 6, 3, 4, 3, 3, 5);
                    break;
                case 1:
                    Shape(icon, Ink, 0, 10, 5, 5, 4, -2, 8, -3, 8, -6, 3, -7, 2, -10, -2, -10, -3, -7, -8, -6, -8, -3, -4, -2, -5, 5);
                    Shape(icon, Ivory, 0, 8, 3, 5, 2, -3, -2, -3, -3, 5);
                    Shape(icon, new Color32(152, 174, 183, 255), 0, 7, 3, 5, 2, -3, 0, -3);
                    Shape(icon, Gold, -6, -3, 0, -4, 6, -3, 5, -6, -5, -6);
                    Shape(icon, Gold, -1, -6, 1, -6, 1, -9, -1, -9);
                    break;
                case 2:
                case 3:
                    Shield(icon, stat == 3);
                    break;
                case 4:
                    Shape(icon, Ink, -10, 6, -6, 8, 3, 8, 6, 3, 5, 0, 10, -1, 10, -6, 5, -9, -5, -8, -6, -4, -10, -2);
                    Shape(icon, Gold, -5, 6, 2, 6, 4, 3, 3, -2, 8, -3, 8, -5, 4, -7, -4, -6, -4, -3);
                    Shape(icon, Ivory, -10, 5, -4, 5, 0, 3, -4, 2, -8, 3, -5, 0, -9, 1);
                    break;
                case 5:
                    Shape(icon, Ink, -10, 2, -8, 7, -4, 9, 4, 9, 8, 7, 10, 3, 9, -3, 6, -5, 5, -9, -5, -9, -6, -5, -9, -3);
                    Shape(icon, Ivory, -8, 2, -6, 6, -3, 7, 3, 7, 6, 6, 8, 3, 7, -2, 4, -4, 4, -7, -4, -7, -4, -4, -7, -2);
                    Shape(icon, Ink, -7, 2, -5, 4, -2, 3, -2, 0, -4, -1, -6, 0);
                    Shape(icon, Ink, 2, 3, 5, 4, 7, 2, 6, 0, 4, -1, 2, 0);
                    Shape(icon, Ink, 0, -1, 2, -4, -2, -4);
                    break;
            }
        }

        private static void Shield(RectTransform icon, bool magical)
        {
            Shape(icon, Ink, 0, 10, 9, 6, 8, -2, 5, -7, 0, -10, -5, -7, -8, -2, -9, 6);
            Shape(icon, magical ? new Color32(64, 175, 193, 255) : new Color32(158, 176, 188, 255),
                0, 8, 7, 5, 6, -2, 3, -6, 0, -8, -3, -6, -6, -2, -7, 5);
            Shape(icon, magical ? new Color32(152, 236, 245, 255) : Ivory, 0, 8, 5, 6, -2, -6, -5, -3, -4, 4);
        }

        private static void Shape(Transform parent, Color color, params int[] coordinates)
        {
            var rect = LegacyUiKit.CreateRect(parent, "Glyph layer", Vector2.zero, new Vector2(20, 20));
            var shape = rect.gameObject.AddComponent<TavernPolygonGraphic>();
            var points = new Vector2[coordinates.Length / 2];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]) * .1f;
            shape.color = color;
            shape.raycastTarget = false;
            shape.SetShape(points);
        }
    }
}
