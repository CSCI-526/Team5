using System;
using ReverseTD.Offense;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public class SquadGridUI : MonoBehaviour
    {
        private const int TrayIndex = SquadRules.CellCount;
        private const float CellRadius = 8f;
        private const float TrayRadius = 10f;
        private const float IconScale = 1.4f;
        private const float GhostLift = 1.15f;
        private const float DropTint = 0.15f;
        private const float HoverTint = 0.35f;
        private const float TitleGap = 52f;
        private const float TopGap = 22f;
        private const float HintGap = 26f;
        private const float TrayLabelInset = 14f;
        private const float TrayIconDrop = 8f;

        private static readonly Vector2 lineOutline = new Vector2(1.5f, -1.5f);
        private static readonly Vector2 dropOutline = new Vector2(2.5f, -2.5f);

        private readonly Image[] cellImages = new Image[SquadRules.CellCount + 1];
        private readonly Outline[] cellOutlines = new Outline[SquadRules.CellCount + 1];
        private readonly UnitIconUI[] icons = new UnitIconUI[SquadRules.CellCount + 1];
        private OffenseData data;
        private RectTransform rect;
        private RectTransform dragLayer;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI hintLabel;
        private GameObject tray;
        private Action changed;
        private Action<int, int> cellsSwapped;
        private Func<int, bool> isInactive;
        private Action<int> placed;
        private UnitIconUI ghost;
        private UnitType pending = UnitType.None;
        private float cellSize;
        private bool allowSwap;
        private int dragFrom = -1;
        private int hoverCell = -1;
        private int markedCell = -1;

        public RectTransform Rect => rect;

        public static SquadGridUI Create(Transform parent, RectTransform dragLayer, OffenseData data, Vector2 position, float cell, float gap,
            string title, string hint, bool allowSwap, Action changed, Action<int, int> cellsSwapped = null, Func<int, bool> isInactive = null)
        {
            float size = cell * SquadRules.Columns + gap * (SquadRules.Columns - 1);
            RectTransform rect = UiKit.CreateRect(parent, "Squad Grid", position, new Vector2(size, size));
            var grid = rect.gameObject.AddComponent<SquadGridUI>();
            grid.rect = rect;
            grid.dragLayer = dragLayer;
            grid.data = data;
            grid.cellSize = cell;
            grid.allowSwap = allowSwap;
            grid.changed = changed;
            grid.cellsSwapped = cellsSwapped;
            grid.isInactive = isInactive;
            grid.Build(size, gap, title, hint);
            return grid;
        }

        public void Refresh()
        {
            data.EnsureSquad();
            for (int i = 0; i < SquadRules.CellCount; i++)
            {
                UnitType type = data.squad[i];
                icons[i].SetType(type);
                icons[i].gameObject.SetActive(dragFrom != i);
                if (isInactive != null)
                {
                    CanvasGroup group = icons[i].GetComponent<CanvasGroup>();
                    if (group == null) group = icons[i].gameObject.AddComponent<CanvasGroup>();
                    group.alpha = isInactive(i) ? 0.3f : 1f;
                }

                bool dropTarget = pending != UnitType.None && type == UnitType.None;
                bool strong = (dropTarget && hoverCell == i) || markedCell == i;
                cellImages[i].color = dropTarget ? Color.Lerp(UiKit.CellColor, UiKit.DropColor, strong ? HoverTint : DropTint) : UiKit.CellColor;
                cellOutlines[i].effectColor = dropTarget || markedCell == i ? UiKit.DropColor : UiKit.PanelLineColor;
                cellOutlines[i].effectDistance = strong ? dropOutline : lineOutline;
            }

            if (tray != null)
            {
                icons[TrayIndex].SetType(pending);
                icons[TrayIndex].gameObject.SetActive(dragFrom != TrayIndex);
            }
        }

        public void SetTitleVisible(bool visible)
        {
            if (titleLabel != null)
            {
                titleLabel.gameObject.SetActive(visible);
            }
            if (hintLabel != null)
            {
                hintLabel.gameObject.SetActive(visible);
            }
        }

        public void BeginPlacement(UnitType type, Transform trayParent, Vector2 trayPosition, Vector2 traySize, Action<int> onPlaced)
        {
            pending = type;
            placed = onPlaced;
            markedCell = -1;

            Image trayImage = UiKit.CreateImage(trayParent, "Tray", trayPosition, traySize, UiKit.CellColor, TrayRadius);
            tray = trayImage.gameObject;
            SetUpCell(trayImage, TrayIndex);
            UiKit.CreateText(tray.transform, "Label", "NEW", new Vector2(0f, traySize.y * 0.5f - TrayLabelInset), new Vector2(traySize.x, 20f), 14f,
                UiKit.MutedColor, FontStyles.Bold, 4f);
            icons[TrayIndex] = UnitIconUI.Create(tray.transform, new Vector2(0f, -TrayIconDrop), cellSize * IconScale * rect.localScale.x);
            Refresh();
        }

        public void EndPlacement(int placedCell)
        {
            pending = UnitType.None;
            placed = null;
            hoverCell = -1;
            markedCell = placedCell;
            if (tray != null)
            {
                Destroy(tray);
                tray = null;
            }
            Refresh();
        }

        public void HandleClick(int index)
        {
            if (pending != UnitType.None && index < SquadRules.CellCount && data.squad[index] == UnitType.None && placed != null)
            {
                placed(index);
            }
        }

        public void HandleBeginDrag(int index, PointerEventData eventData)
        {
            dragFrom = -1;
            UnitType type = index == TrayIndex ? pending : data.squad[index];
            bool allowed = pending != UnitType.None ? index == TrayIndex : allowSwap && type != UnitType.None;
            if (!allowed)
            {
                return;
            }

            dragFrom = index;
            ghost = UnitIconUI.Create(dragLayer, Vector2.zero, cellSize * IconScale * rect.localScale.x * GhostLift);
            ghost.SetType(type);
            ghost.transform.SetAsLastSibling();
            MoveGhost(eventData);
            Refresh();
        }

        public void HandleDrag(PointerEventData eventData)
        {
            if (dragFrom < 0)
            {
                return;
            }

            MoveGhost(eventData);
            int target = FindCell(eventData);
            if (target != hoverCell)
            {
                hoverCell = target;
                Refresh();
            }
        }

        public void HandleEndDrag(PointerEventData eventData)
        {
            if (dragFrom < 0)
            {
                return;
            }

            int from = dragFrom;
            int target = FindCell(eventData);
            dragFrom = -1;
            hoverCell = -1;
            ClearGhost();

            if (from == TrayIndex)
            {
                if (target >= 0 && data.squad[target] == UnitType.None && placed != null)
                {
                    placed(target);
                }
            }
            else if (target >= 0 && target != from)
            {
                data.SwapCells(from, target);
                cellsSwapped?.Invoke(from, target);
                if (changed != null)
                {
                    changed();
                }
            }
            Refresh();
        }

        private void Build(float size, float gap, string title, string hint)
        {
            float half = size * 0.5f;
            for (int i = 0; i < SquadRules.CellCount; i++)
            {
                Vector2 position = SquadRules.CellOffset(i, cellSize + gap);
                Image cell = UiKit.CreateImage(rect, "Cell " + i, position, new Vector2(cellSize, cellSize), UiKit.CellColor, CellRadius);
                SetUpCell(cell, i);
                icons[i] = UnitIconUI.Create(cell.transform, Vector2.zero, cellSize * IconScale);
            }

            UiKit.CreateText(rect, "Top", "TOP", new Vector2(0f, half + TopGap), new Vector2(size, 18f), 12f, UiKit.MutedColor, FontStyles.Bold, 6f);
            if (!string.IsNullOrEmpty(title))
            {
                titleLabel = UiKit.CreateText(rect, "Title", title, new Vector2(0f, half + TitleGap), new Vector2(size + 120f, 26f), 18f,
                    UiKit.TextColor, FontStyles.Bold, 3f);
            }
            if (!string.IsNullOrEmpty(hint))
            {
                hintLabel = UiKit.CreateText(rect, "Hint", hint, new Vector2(0f, -half - HintGap), new Vector2(size + 120f, 22f), 14f, UiKit.MutedColor);
            }
            Refresh();
        }

        private void SetUpCell(Image cell, int index)
        {
            cell.raycastTarget = true;
            cellImages[index] = cell;
            var outline = cell.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.PanelLineColor;
            outline.effectDistance = lineOutline;
            cellOutlines[index] = outline;
            cell.gameObject.AddComponent<SquadCellUI>().Initialize(this, index);
        }

        private void MoveGhost(PointerEventData eventData)
        {
            Vector2 local;
            if (ghost != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, eventData.position, eventData.pressEventCamera, out local))
            {
                ghost.Rect.anchoredPosition = local;
            }
        }

        private int FindCell(PointerEventData eventData)
        {
            for (int i = 0; i < SquadRules.CellCount; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cellImages[i].rectTransform, eventData.position, eventData.pressEventCamera))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ClearGhost()
        {
            if (ghost != null)
            {
                Destroy(ghost.gameObject);
                ghost = null;
            }
        }

        private void OnDisable()
        {
            ClearGhost();
            dragFrom = -1;
            hoverCell = -1;
        }
    }
}
