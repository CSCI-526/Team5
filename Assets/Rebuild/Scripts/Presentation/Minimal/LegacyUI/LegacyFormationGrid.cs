using System;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Battle editing uses a slot permutation; preparation delegates to the bounded deployment model.
    // Neither mode changes a live simulation's state directly.
    public sealed class LegacyFormationGrid : MonoBehaviour
    {
        private readonly Image[] cells = new Image[9];
        private readonly LegacyUnitIconUI[] icons = new LegacyUnitIconUI[9];
        private readonly int[] sourceSlots = new int[9];
        private Func<int, UnitRole> roleAtSource;
        private SquadPreparation preparation;
        private RectTransform dragLayer;
        private LegacyUnitIconUI ghost;
        private int dragFrom = -1, selected = -1;
        public int[] CopySourceSlots() => (int[])sourceSlots.Clone();

        public static LegacyFormationGrid Create(Transform parent, RectTransform dragLayer, BattleSimulation simulation)
            => Create(parent, dragLayer, slot => simulation.GetMemberAtSlot(slot)?.Role ?? UnitRole.Test);

        public static LegacyFormationGrid CreateForPreparation(Transform parent, RectTransform dragLayer, SquadPreparation preparation)
        {
            var grid = Create(parent, dragLayer, preparation.RoleAtSlot);
            grid.preparation = preparation;
            preparation.Changed += grid.Refresh;
            grid.transform.Find("Hint").GetComponent<TMPro.TMP_Text>().text = "Drag out to return unit";
            return grid;
        }
        public bool TryPlaceWarehouseMember(int memberId, PointerEventData data)
            => preparation != null && preparation.TryAssign(memberId, FindCell(data));

        public static LegacyFormationGrid Create(Transform parent, RectTransform dragLayer, Func<int, UnitRole> roleAtSource)
        {
            const float cell = 58, gap = 8, size = cell * 3 + gap * 2;
            var rect = LegacyUiKit.CreateRect(parent, "Squad Grid", Vector2.zero, Vector2.one * size);
            var grid = rect.gameObject.AddComponent<LegacyFormationGrid>();
            grid.roleAtSource = roleAtSource; grid.dragLayer = dragLayer;
            for (var slot = 0; slot < 9; slot++)
            {
                grid.sourceSlots[slot] = slot;
                // Native X/Z slots: +Z is the top row. Member IDs are not UI indices.
                var position = new Vector2(slot % 3 - 1, slot / 3 - 1) * (cell + gap);
                var image = LegacyUiKit.CreateImage(rect, "Cell " + slot, position, Vector2.one * cell, LegacyUiKit.CellColor, 8);
                image.raycastTarget = true;
                var outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = LegacyUiKit.PanelLineColor; outline.effectDistance = new Vector2(1.5f, -1.5f);
                image.gameObject.AddComponent<LegacyFormationCellUI>().Initialize(grid, slot);
                grid.cells[slot] = image;
                grid.icons[slot] = LegacyUnitIconUI.Create(image.transform, Vector2.zero, cell * 1.4f);
            }
            LegacyUiKit.CreateText(rect, "Top", "TOP", new Vector2(0, size / 2 + 22), new Vector2(size, 18), 12,
                LegacyUiKit.MutedColor, TMPro.FontStyles.Bold, 6);
            LegacyUiKit.CreateText(rect, "Hint", "Drag to swap", new Vector2(0, -size / 2 - 26), new Vector2(size + 120, 22), 14, LegacyUiKit.MutedColor);
            grid.Refresh(); return grid;
        }
        private UnitRole RoleAt(int slot) => preparation != null ? preparation.RoleAtSlot(slot) : roleAtSource(sourceSlots[slot]);
        private void Refresh()
        {
            for (var slot = 0; slot < 9; slot++)
            {
                icons[slot].SetType(RoleAt(slot));
                icons[slot].gameObject.SetActive(dragFrom != slot);
                cells[slot].GetComponent<Outline>().effectColor = selected == slot ? LegacyUiKit.DropColor : LegacyUiKit.PanelLineColor;
            }
        }
        public void HandleClick(int slot)
        {
            if (selected < 0) selected = slot;
            else { Swap(selected, slot); selected = -1; }
            Refresh();
        }
        private void Swap(int first, int second)
        {
            if (preparation != null) { preparation.TryMove(first, second); return; }
            var value = sourceSlots[first]; sourceSlots[first] = sourceSlots[second]; sourceSlots[second] = value;
        }
        public void HandleBeginDrag(int slot, PointerEventData eventData)
        {
            if (RoleAt(slot) == UnitRole.Test) return;
            dragFrom = slot; selected = -1;
            ghost = LegacyUnitIconUI.Create(dragLayer, Vector2.zero, 58 * 1.4f * 1.15f);
            ghost.SetType(RoleAt(slot)); ghost.transform.SetAsLastSibling();
            MoveGhost(eventData); Refresh();
        }
        public void HandleDrag(PointerEventData eventData) { if (dragFrom >= 0) MoveGhost(eventData); }
        public void HandleEndDrag(PointerEventData eventData)
        {
            if (dragFrom < 0) return;
            var target = FindCell(eventData); var from = dragFrom; dragFrom = -1;
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            if (target >= 0 && target != from) Swap(from, target);
            else if (target < 0 && preparation != null) preparation.TryRemove(from);
            Refresh();
        }
        private void MoveGhost(PointerEventData data)
        {
            if (ghost != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, data.position, data.pressEventCamera, out var point))
                ghost.Rect.anchoredPosition = point;
        }
        private int FindCell(PointerEventData data)
        {
            // Use the UI raycaster's actual hovered cell first (also handles scaled Game Views).
            var hit = data.pointerCurrentRaycast.gameObject;
            if (hit != null)
                for (var slot = 0; slot < 9; slot++)
                    if (hit.transform == cells[slot].transform || hit.transform.IsChildOf(cells[slot].transform)) return slot;
            for (var slot = 0; slot < 9; slot++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[slot].rectTransform, data.position, data.pressEventCamera)) return slot;
            return -1;
        }
        private void OnDisable() { if (ghost != null) Destroy(ghost.gameObject); ghost = null; dragFrom = -1; }
        private void OnDestroy() { if (preparation != null) preparation.Changed -= Refresh; }
    }
}
