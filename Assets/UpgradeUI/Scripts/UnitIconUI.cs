using ReverseTD.Defense;
using ReverseTD.Offense;
using UnityEngine;

namespace ReverseTD.UpgradeUI
{
    public class UnitIconUI : MonoBehaviour
    {
        private const float ShadowAlpha = 0.5f;

        private static readonly Vector2 shadowOffset = new Vector2(0.03f, -0.05f);

        private RectTransform rect;
        private float pixelsPerUnit;
        private UnitType shown = UnitType.None;

        public RectTransform Rect => rect;

        public static UnitIconUI Create(Transform parent, Vector2 position, float pixelsPerUnit)
        {
            RectTransform rect = UiKit.CreateRect(parent, "Unit Icon", position, Vector2.one * pixelsPerUnit * 0.5f);
            var icon = rect.gameObject.AddComponent<UnitIconUI>();
            icon.rect = rect;
            icon.pixelsPerUnit = pixelsPerUnit;
            return icon;
        }

        public void SetType(UnitType type)
        {
            if (type == shown)
            {
                return;
            }
            shown = type;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            if (type == UnitType.None)
            {
                return;
            }

            SoldierLook look = UnitLooks.For(type);
            float k = pixelsPerUnit;
            Vector2 outer = Vector2.one * look.OuterSize * k;
            UiKit.CreateImage(transform, "Shadow", shadowOffset * k, outer, new Color(0f, 0f, 0f, ShadowAlpha));
            UiKit.CreateImage(transform, "Outline", Vector2.zero, outer, look.Outline);
            UiKit.CreateImage(transform, "Body", Vector2.zero, Vector2.one * look.InnerSize * k, look.Body);

            switch (look.Mark)
            {
                case SoldierMark.Diamond:
                    UiKit.CreateImage(transform, "Mark", Vector2.zero, Vector2.one * SoldierLook.DiamondSize * k, look.MarkColor)
                        .rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case SoldierMark.Plate:
                    UiKit.CreateImage(transform, "Mark", Vector2.zero, Vector2.one * SoldierLook.PlateSize * k, look.MarkColor);
                    break;
                case SoldierMark.Cross:
                    UiKit.CreateImage(transform, "Mark Across", Vector2.zero, new Vector2(SoldierLook.CrossLength, SoldierLook.CrossWidth) * k, look.MarkColor);
                    UiKit.CreateImage(transform, "Mark Upright", Vector2.zero, new Vector2(SoldierLook.CrossWidth, SoldierLook.CrossLength) * k, look.MarkColor);
                    break;
            }
        }
    }
}
