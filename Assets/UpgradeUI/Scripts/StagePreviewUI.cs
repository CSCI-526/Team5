using System.Collections.Generic;
using ReverseTD.Defense;
using ReverseTD.Offense;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public class StagePreviewUI : MonoBehaviour
    {
        private const float PanelRadius = 12f;
        private const float PanelBorder = 2f;
        private const float Inset = 8f;
        private const float HeaderHeight = 24f;
        private const float TitleInset = 8f;
        private const float MapRadius = 6f;
        private const float Padding = 0.5f;
        private const float MinLine = 1.5f;
        private const float MinMark = 16f;
        private const float ActiveAreaAlpha = 0.3f;
        private const float WalkSpeed = 3f;
        private const float EndPause = 0.6f;
        private const float TowerInner = 0.68f;
        private const float TowerShadowAlpha = 0.43f;
        private const int NoTarget = -1;
        private const int Unknown = -2;

        private static readonly Vector2 towerShadowOffset = new Vector2(0.07f, -0.09f);
        private static readonly Color boardColor = new Color(0.12f, 0.14f, 0.17f);

        private readonly List<TowerSlot> slots = new List<TowerSlot>();
        private readonly List<Image> areas = new List<Image>();
        private readonly List<RectTransform> marks = new List<RectTransform>();
        private readonly List<int> targets = new List<int>();
        private readonly UnitIconUI[] icons = new UnitIconUI[SquadRules.CellCount];
        private OffenseData data;
        private StageLayout layout;
        private LanePath lane;
        private RectTransform squad;
        private Vector2 center;
        private float scale;
        private float distance;
        private float pause;

        public static StagePreviewUI Create(Transform parent, Vector2 position, Vector2 size, StageLayout layout, OffenseData data)
        {
            UiKit.CreateImage(parent, "Map Preview Border", position, size + Vector2.one * PanelBorder * 2f, UiKit.PanelLineColor,
                PanelRadius + PanelBorder);
            Image panel = UiKit.CreateImage(parent, "Map Preview", position, size, UiKit.PanelColor, PanelRadius);
            var preview = panel.gameObject.AddComponent<StagePreviewUI>();
            preview.data = data;
            preview.layout = layout;
            preview.Build(panel.rectTransform, size);
            return preview;
        }

        public void Refresh()
        {
            if (lane == null)
            {
                return;
            }

            for (int i = 0; i < SquadRules.CellCount; i++)
            {
                icons[i].SetType(data.squad[i]);
            }
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i] = Unknown;
            }
            Place();
        }

        private void Build(RectTransform panel, Vector2 size)
        {
            var titlePosition = new Vector2(0f, (size.y - HeaderHeight) * 0.5f - Inset);
            var titleSize = new Vector2(size.x - (Inset + TitleInset) * 2f, HeaderHeight);
            TextMeshProUGUI title = UiKit.CreateText(panel, "Title", "NEXT MAP", titlePosition, titleSize, 14f, UiKit.MutedColor,
                FontStyles.Bold, 6f);
            title.alignment = TextAlignmentOptions.Left;

            var mapSize = new Vector2(size.x - Inset * 2f, size.y - HeaderHeight - Inset * 2f);
            RectTransform map = UiKit.CreateImage(panel, "Map", new Vector2(0f, -HeaderHeight * 0.5f), mapSize, boardColor, MapRadius).rectTransform;
            if (layout == null || layout.Waypoints.Count < 2)
            {
                UiKit.CreateText(map, "Missing", "Map preview unavailable", Vector2.zero, mapSize, 16f, UiKit.MutedColor);
                enabled = false;
                return;
            }

            Bounds bounds = layout.CalculateBounds();
            scale = mapSize.y * 0.5f / (Mathf.Max(bounds.extents.y, bounds.extents.x * mapSize.y / mapSize.x) + Padding);
            center = bounds.center;
            lane = new LanePath(layout.Waypoints, SquadRules.CornerRadius);

            RectTransform board = UiKit.CreateRect(map, "Board", Vector2.zero, mapSize);
            board.gameObject.AddComponent<RectMask2D>();
            BuildPath(board);
            BuildTowers(board);
            BuildGoalZone(board);
            BuildSquad(map);

            distance = SquadRules.Spacing;
            pause = EndPause;
            Refresh();
        }

        private void BuildPath(Transform parent)
        {
            IReadOnlyList<Vector2> points = layout.Waypoints;
            Color edge = Palette.Shade(layout.PathColor, BoardView.EdgeShade);
            for (int i = 1; i < points.Count; i++)
            {
                AddSegment(parent, "Path Edge", points[i - 1], points[i], layout.PathWidth + BoardView.EdgeWidth, edge);
            }
            for (int i = 1; i < points.Count; i++)
            {
                AddSegment(parent, "Path", points[i - 1], points[i], layout.PathWidth, layout.PathColor);
            }
        }

        private void AddSegment(Transform parent, string partName, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0f)
            {
                return;
            }

            Image part = UiKit.CreateImage(parent, partName, ToMap((from + to) * 0.5f), new Vector2(length + width, width) * scale, color);
            part.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void BuildTowers(Transform parent)
        {
            IReadOnlyList<TowerSlot> stageSlots = layout.TowerSlots;
            for (int i = 0; i < stageSlots.Count; i++)
            {
                if (stageSlots[i].Definition != null)
                {
                    slots.Add(stageSlots[i]);
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                areas.Add(BuildArea(parent, slots[i]));
            }

            var shadow = new Color(0f, 0f, 0f, TowerShadowAlpha);
            for (int i = 0; i < slots.Count; i++)
            {
                float k = TowerScale(slots[i]);
                UiKit.CreateImage(parent, "Tower Shadow", ToMap(slots[i].Position) + towerShadowOffset * k, Vector2.one * TowerVisual.DesignSize * k, shadow);
            }
            for (int i = 0; i < slots.Count; i++)
            {
                float k = TowerScale(slots[i]);
                UiKit.CreateImage(parent, "Tower", ToMap(slots[i].Position), Vector2.one * TowerVisual.DesignSize * k, Palette.Dark(slots[i].Definition.Color));
            }
            for (int i = 0; i < slots.Count; i++)
            {
                float k = TowerScale(slots[i]);
                UiKit.CreateImage(parent, "Tower Base", ToMap(slots[i].Position), Vector2.one * TowerInner * k, slots[i].Definition.Color);
            }
        }

        private Image BuildArea(Transform parent, TowerSlot slot)
        {
            TowerDefinition definition = slot.Definition;
            Color edge = Palette.WithAlpha(definition.Color, RangeIndicator.EdgeAlpha);
            Color fill = Palette.WithAlpha(definition.Color, RangeIndicator.Alpha);
            float range = slot.AttackRange * scale;
            if (slot.AttackDirection == TowerAttackDirection.Circle)
            {
                Image disc = UiKit.CreateCircle(parent, "Area", ToMap(slot.Position), range * 2f, fill);
                UiKit.CreateImage(parent, "Area Edge", ToMap(slot.Position), Vector2.one * range * 2f, edge).sprite = ShapeSprites.WaveRing;
                return disc;
            }

            Vector2 direction = TowerAttackGeometry.Direction(slot.AttackDirection);
            float width = definition.AttackWidth * scale;
            float line = Line(RangeIndicator.BorderWidth);
            RectTransform strip = UiKit.CreateRect(parent, "Lane", ToMap(slot.Position), Vector2.zero);
            strip.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Image area = UiKit.CreateImage(strip, "Fill", new Vector2(range * 0.5f, 0f), new Vector2(range, width), fill);
            UiKit.CreateImage(strip, "Edge", new Vector2(range * 0.5f, width * 0.5f), new Vector2(range, line), edge);
            UiKit.CreateImage(strip, "Edge", new Vector2(range * 0.5f, -width * 0.5f), new Vector2(range, line), edge);
            UiKit.CreateImage(strip, "Edge", Vector2.zero, new Vector2(line, width), edge);
            UiKit.CreateImage(strip, "Edge", new Vector2(range, 0f), new Vector2(line, width), edge);
            return area;
        }

        private void BuildGoalZone(Transform parent)
        {
            IReadOnlyList<Vector2> points = layout.Waypoints;
            Vector2 end = points[points.Count - 1];
            Vector2 direction = end - points[points.Count - 2];
            Vector2 size = new Vector2(BoardView.GoalZoneLength, BoardView.GoalZoneDepth) * layout.PathWidth * scale;
            float line = Line(BoardView.GoalZoneBorder);
            Color color = BoardView.GoalZoneLine;

            RectTransform zone = UiKit.CreateRect(parent, "Goal Zone", ToMap(end), size);
            zone.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            UiKit.CreateImage(zone, "Fill", Vector2.zero, size, BoardView.GoalZoneFill);
            UiKit.CreateImage(zone, "Top Border", new Vector2(0f, (size.y - line) * 0.5f), new Vector2(size.x, line), color);
            UiKit.CreateImage(zone, "Bottom Border", new Vector2(0f, (line - size.y) * 0.5f), new Vector2(size.x, line), color);
            UiKit.CreateImage(zone, "Left Border", new Vector2((line - size.x) * 0.5f, 0f), new Vector2(line, size.y), color);
            UiKit.CreateImage(zone, "Right Border", new Vector2((size.x - line) * 0.5f, 0f), new Vector2(line, size.y), color);
        }

        private void BuildSquad(Transform parent)
        {
            squad = UiKit.CreateRect(parent, "Squad", Vector2.zero, Vector2.zero);
            squad.gameObject.AddComponent<Canvas>();

            float pixelsPerUnit = scale * SquadRules.UnitSize / UnitLooks.For(UnitType.Attack).OuterSize;
            for (int i = 0; i < SquadRules.CellCount; i++)
            {
                icons[i] = UnitIconUI.Create(squad, SquadRules.CellOffset(i, SquadRules.Spacing) * scale, pixelsPerUnit);
            }

            float markScale = Mathf.Max(TargetMarker.Scale * scale, MinMark / TargetMarker.RingDiameter);
            for (int i = 0; i < slots.Count; i++)
            {
                marks.Add(CreateMark(squad, markScale));
                targets.Add(Unknown);
            }
        }

        private static RectTransform CreateMark(Transform parent, float k)
        {
            Color color = TargetMarker.MarkColor;
            float ring = TargetMarker.RingDiameter * k;
            float tick = (TargetMarker.TickStart + TargetMarker.TickEnd) * 0.5f * k;
            var across = new Vector2(TargetMarker.TickEnd - TargetMarker.TickStart, TargetMarker.TickWidth) * k;
            var upright = new Vector2(across.y, across.x);

            RectTransform mark = UiKit.CreateRect(parent, "Target Mark", Vector2.zero, Vector2.one * ring);
            UiKit.CreateImage(mark, "Ring", Vector2.zero, Vector2.one * ring, color).sprite = ShapeSprites.ThickRing;
            UiKit.CreateImage(mark, "Tick Right", new Vector2(tick, 0f), across, color);
            UiKit.CreateImage(mark, "Tick Left", new Vector2(-tick, 0f), across, color);
            UiKit.CreateImage(mark, "Tick Up", new Vector2(0f, tick), upright, color);
            UiKit.CreateImage(mark, "Tick Down", new Vector2(0f, -tick), upright, color);
            mark.gameObject.SetActive(false);
            return mark;
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (pause > 0f)
            {
                pause -= delta;
                if (pause <= 0f && distance >= lane.TotalLength)
                {
                    distance = SquadRules.Spacing;
                    pause = EndPause;
                    Place();
                }
                return;
            }

            distance = Mathf.Min(distance + WalkSpeed * delta, lane.TotalLength);
            if (distance >= lane.TotalLength)
            {
                pause = EndPause;
            }
            Place();
        }

        private void Place()
        {
            Vector2 middle = lane.Sample(distance, 0f);
            squad.anchoredPosition = ToMap(middle);
            for (int i = 0; i < slots.Count; i++)
            {
                int target = FindTarget(slots[i], middle);
                if (target == targets[i])
                {
                    continue;
                }

                targets[i] = target;
                marks[i].gameObject.SetActive(target != NoTarget);
                if (target != NoTarget)
                {
                    marks[i].anchoredPosition = SquadRules.CellOffset(target, SquadRules.Spacing) * scale;
                }
                areas[i].color = Palette.WithAlpha(slots[i].Definition.Color, target != NoTarget ? ActiveAreaAlpha : RangeIndicator.Alpha);
            }
        }

        private int FindTarget(TowerSlot slot, Vector2 middle)
        {
            Vector2 direction = TowerAttackGeometry.Direction(slot.AttackDirection);
            bool circular = slot.AttackDirection == TowerAttackDirection.Circle;
            int best = NoTarget;
            float bestSqr = float.MaxValue;
            for (int cell = 0; cell < SquadRules.CellCount; cell++)
            {
                if (data.squad[cell] == UnitType.None)
                {
                    continue;
                }

                Vector2 offset = middle + SquadRules.CellOffset(cell, SquadRules.Spacing) - slot.Position;
                if (!TowerAttackGeometry.Contains(offset, direction, slot.AttackRange, slot.Definition.AttackWidth, circular))
                {
                    continue;
                }

                float sqr = offset.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = cell;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        private float TowerScale(TowerSlot slot)
        {
            return slot.Definition.Size * scale / TowerVisual.DesignSize;
        }

        private float Line(float worldWidth)
        {
            return Mathf.Max(worldWidth * scale, MinLine);
        }

        private Vector2 ToMap(Vector2 world)
        {
            return (world - center) * scale;
        }
    }
}
