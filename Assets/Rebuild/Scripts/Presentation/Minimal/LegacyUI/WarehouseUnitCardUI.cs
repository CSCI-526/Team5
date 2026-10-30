using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class WarehouseUnitCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LegacyUpgradeScene owner;
        private RectTransform dragLayer;
        private MemberSpawnDefinition member;
        private LegacyUnitIconUI ghost;
        public void Initialize(LegacyUpgradeScene owner, RectTransform dragLayer, MemberSpawnDefinition member)
        { this.owner = owner; this.dragLayer = dragLayer; this.member = member; }
        public void OnBeginDrag(PointerEventData data)
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = LegacyUnitIconUI.Create(dragLayer, Vector2.zero, 90);
            ghost.SetType(member.Unit.Role);
            ghost.transform.SetAsLastSibling();
            OnDrag(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (ghost != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, data.position, data.pressEventCamera, out var point))
                ghost.Rect.anchoredPosition = point;
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (ghost == null) return;
            Destroy(ghost.gameObject); ghost = null;
            owner.TryPlaceMember(member.MemberId, data);
        }
        private void OnDisable() { if (ghost != null) Destroy(ghost.gameObject); ghost = null; }
    }
}
