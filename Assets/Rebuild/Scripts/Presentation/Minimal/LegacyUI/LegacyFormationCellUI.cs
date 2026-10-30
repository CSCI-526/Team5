using UnityEngine;
using UnityEngine.EventSystems;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public class LegacyFormationCellUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LegacyFormationGrid grid;
        private int index;

        public void Initialize(LegacyFormationGrid owner, int cellIndex)
        {
            grid = owner;
            index = cellIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging)
            {
                grid.HandleClick(index);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            grid.HandleBeginDrag(index, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            grid.HandleDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            grid.HandleEndDrag(eventData);
        }
    }
}
