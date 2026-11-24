using System;
using Team5.BasicTowerTrial.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Team5.BasicTowerTrial.Presentation
{
    internal sealed class TavernOfferHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TavernUnitHoverCard detail;
        private Func<(UnitRole Role, bool Upgraded)?> readUnit;

        internal RectTransform Rect => (RectTransform)transform;
        internal (UnitRole Role, bool Upgraded)? Unit => readUnit?.Invoke();

        internal void Bind(TavernUnitHoverCard card, Func<(UnitRole Role, bool Upgraded)?> unit)
        {
            detail = card;
            readUnit = unit;
        }

        public void OnPointerEnter(PointerEventData eventData) => detail.Show(this);
        public void OnPointerExit(PointerEventData eventData) => detail.Hide(this);

        private void OnDisable()
        {
            if (detail != null) detail.Hide(this);
        }
    }
}
