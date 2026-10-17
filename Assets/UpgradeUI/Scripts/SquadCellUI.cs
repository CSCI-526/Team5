using UnityEngine;
using UnityEngine.EventSystems;

namespace ReverseTD.UpgradeUI
{
    public class SquadCellUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private SquadGridUI grid;
        private int index;

        public void Initialize(SquadGridUI owner, int cellIndex)
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
