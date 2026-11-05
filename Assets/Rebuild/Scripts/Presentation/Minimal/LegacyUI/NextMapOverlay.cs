using System.Collections.Generic;
using System.Globalization;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class NextMapOverlay : MonoBehaviour
    {
        private static readonly Color LaneColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color RouteColor = new Color(1f, 0.88f, 0.25f);
        private static readonly Color SpawnColor = new Color(0.9f, 0.25f, 0.25f);
        private static readonly Color GoalColor = new Color(0.2f, 0.75f, 0.9f);
        private static readonly Color GoalInk = new Color(0.02f, 0.13f, 0.17f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Tiles between arrowheads on the route line.")] private float arrowSpacing = 3f;
        [SerializeField, Tooltip("Route line thickness in tiles.")] private float routeWidth = 0.3f;
        [SerializeField, Tooltip("Opacity of a lane's fill.")] private float laneFillAlpha = 0.2f;
        [SerializeField, Tooltip("Opacity of a lane's edge line.")] private float laneEdgeAlpha = 1f;
        [SerializeField, Tooltip("World width of a lane's edge line.")] private float laneEdgeWidth = 0.06f;
        [SerializeField, Tooltip("Opacity of a lane's diagonal hatching.")] private float hatchAlpha = 0.55f;
        [SerializeField, Tooltip("Height in pixels of the START and GOAL labels above the center of their cells.")] private float labelLift = 39f;

        private MaterialPropertyBlock block;
        private RectTransform lanes;
        private RectTransform road;
        private RectTransform labels;

        public void Build(NextMapData data, NextMapProjection projection, IReadOnlyList<TowerRangeView> ranges)
        {
            block = new MaterialPropertyBlock();
            lanes = Layer("Lane Hatching");
            road = Layer("Route");
            labels = Layer("Labels");
            foreach (var range in ranges)
            {
                var edge = range.GetComponentInChildren<LineRenderer>(true);
                if (edge != null) edge.widthMultiplier = laneEdgeWidth;
                Tint(range.GetComponent<MeshRenderer>(), new Color(LaneColor.r, LaneColor.g, LaneColor.b, laneFillAlpha));
                Tint(edge, new Color(LaneColor.r, LaneColor.g, LaneColor.b, laneEdgeAlpha));
            }
            foreach (var tower in data.Towers) Hatch(tower, projection);
            BuildRoute(data.Route, projection);
            Pin("Start Pin", "START", projection.ToPanel(data.Spawn), SpawnColor, Color.white);
            Pin("Goal Pin", "GOAL", projection.ToPanel(data.Goal), GoalColor, GoalInk);
            var order = NextMapRules.RoadOrder(data.Route, data.Towers);
            for (var number = 0; number < order.Length; number++) Badge(data.Towers[order[number]], number + 1, projection);
        }

        public static void Legend(Transform parent, Vector2 start)
        {
            var x = start.x;
            var route = LegacyUiKit.CreateImage(parent, "Legend Route", new Vector2(x + 11f, start.y), new Vector2(22f, 5f), RouteColor, 2);
            x = LegendLabel(parent, "ROUTE", route.rectTransform, x, start.y);
            LegacyUiKit.CreateImage(parent, "Legend Lane Edge", new Vector2(x + 11f, start.y), new Vector2(22f, 14f), LaneColor, 3);
            var lane = LegacyUiKit.CreateImage(parent, "Legend Lane", new Vector2(x + 11f, start.y), new Vector2(19f, 11f), LegacyUiKit.PanelColor, 2);
            var hatch = LegacyUiKit.CreateImage(lane.transform, "Hatch", Vector2.zero, new Vector2(19f, 11f), new Color(LaneColor.r, LaneColor.g, LaneColor.b, 0.7f));
            hatch.sprite = NextMapShapes.Hatch;
            hatch.type = Image.Type.Tiled;
            x = LegendLabel(parent, "TOWER LANE", lane.rectTransform, x, start.y);
            var spawn = LegacyUiKit.CreateImage(parent, "Legend Start", new Vector2(x + 11f, start.y), new Vector2(22f, 14f), SpawnColor, 3);
            x = LegendLabel(parent, "START", spawn.rectTransform, x, start.y);
            var goal = LegacyUiKit.CreateImage(parent, "Legend Goal", new Vector2(x + 11f, start.y), new Vector2(22f, 14f), GoalColor, 3);
            LegendLabel(parent, "GOAL", goal.rectTransform, x, start.y);
        }

        private static float LegendLabel(Transform parent, string text, RectTransform swatch, float x, float y)
        {
            var label = LegacyUiKit.CreateText(parent, "Legend " + text, text, Vector2.zero, new Vector2(160f, 20f), 13, LegacyUiKit.MutedColor, FontStyles.Bold, 2);
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.anchoredPosition = new Vector2(x + 30f + 80f, y);
            return x + 30f + label.GetPreferredValues(text).x + 26f;
        }

        private RectTransform Layer(string name)
        {
            var layer = LegacyUiKit.CreateRect(transform, name, Vector2.zero, Vector2.zero);
            LegacyUiKit.Stretch(layer);
            return layer;
        }

        private void Tint(Renderer target, Color color)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            target.SetPropertyBlock(block);
        }

        private void Hatch(NextMapTower tower, NextMapProjection projection)
        {
            var lane = tower.Lane;
            var a = projection.ToPanel(lane.min);
            var b = projection.ToPanel(lane.max);
            var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            var hatch = LegacyUiKit.CreateImage(lanes, "Lane Hatch", (a + b) / 2f, size, new Color(LaneColor.r, LaneColor.g, LaneColor.b, hatchAlpha));
            hatch.sprite = NextMapShapes.Hatch;
            hatch.type = Image.Type.Tiled;
        }

        private void BuildRoute(NextMapRoute route, NextMapProjection projection)
        {
            var thickness = Mathf.Max(3f, routeWidth * projection.PixelsPerUnit);
            var corners = Corners(route);
            for (var i = 1; i < corners.Count; i++)
            {
                var from = projection.ToPanel(corners[i - 1]);
                var to = projection.ToPanel(corners[i]);
                var delta = to - from;
                var segment = LegacyUiKit.CreateImage(road, "Route Segment", (from + to) / 2f, new Vector2(delta.magnitude, thickness), RouteColor);
                segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
            foreach (var corner in corners) LegacyUiKit.CreateCircle(road, "Route Joint", projection.ToPanel(corner), thickness, RouteColor);
            var arrowSize = Mathf.Max(7f, 0.42f * projection.PixelsPerUnit) * 2.1f;
            for (var distance = 2f; distance < route.Length - 1f; distance += arrowSpacing)
            {
                var point = route.Sample(distance);
                var at = projection.ToPanel(point);
                var ahead = projection.ToPanel(point + route.Direction(distance)) - at;
                var arrow = LegacyUiKit.CreateImage(road, "Route Arrow", at, new Vector2(arrowSize, arrowSize), LegacyUiKit.PanelColor);
                arrow.sprite = NextMapShapes.Triangle;
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg);
            }
        }

        private static List<Vector3> Corners(NextMapRoute route)
        {
            var corners = new List<Vector3>();
            for (var i = 0; i < route.Count; i++)
            {
                if (i == 0 || i == route.Count - 1)
                {
                    corners.Add(route[i]);
                    continue;
                }
                var before = (route[i] - route[i - 1]).normalized;
                var after = (route[i + 1] - route[i]).normalized;
                if (Vector3.Dot(before, after) < 0.999f) corners.Add(route[i]);
            }
            return corners;
        }

        private void Pin(string name, string text, Vector2 point, Color fill, Color ink)
        {
            var root = LegacyUiKit.CreateRect(labels, name, point + new Vector2(0f, labelLift), Vector2.zero);
            var label = LegacyUiKit.CreateText(root, "Label", text, Vector2.zero, new Vector2(120f, 26f), 15, ink, FontStyles.Bold, 4);
            var back = LegacyUiKit.CreateImage(root, "Back", Vector2.zero, new Vector2(label.GetPreferredValues(text).x + 20f, 26f), fill, 6);
            back.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
        }

        private void Badge(NextMapTower tower, int number, NextMapProjection projection)
        {
            var definition = tower.Definition;
            var square = definition.Area == AttackArea.Square;
            var text = (square ? "r " : string.Empty) + definition.Range.ToString("0.##", CultureInfo.InvariantCulture);
            var root = LegacyUiKit.CreateRect(labels, "Tower Badge " + number, projection.ToPanel(tower.BadgeAnchor) + new Vector2(0f, 16f), Vector2.zero);
            var value = LegacyUiKit.CreateText(root, "Range", text, Vector2.zero, new Vector2(80f, 24f), 14, LegacyUiKit.TextColor, FontStyles.Bold);
            var width = 54f + value.GetPreferredValues(text).x;
            var left = -width / 2f;
            LegacyUiKit.CreateImage(root, "Outline", Vector2.zero, new Vector2(width + 4f, 30f), LaneColor, 15);
            LegacyUiKit.CreateImage(root, "Back", Vector2.zero, new Vector2(width, 26f), LegacyUiKit.PanelColor, 13);
            LegacyUiKit.CreateCircle(root, "Number Back", new Vector2(left + 13f, 0f), 20f, LaneColor);
            LegacyUiKit.CreateText(root, "Number", number.ToString(CultureInfo.InvariantCulture), new Vector2(left + 13f, 0f), new Vector2(20f, 20f), 13, Color.white, FontStyles.Bold);
            var arrow = LegacyUiKit.CreateImage(root, "Direction", new Vector2(left + 34f, 0f), square ? new Vector2(9f, 9f) : new Vector2(11f, 11f), LegacyUiKit.TextColor);
            if (!square)
            {
                arrow.sprite = NextMapShapes.Triangle;
                var screen = projection.ToPanel(definition.Position + SquadCombatRules.Direction(definition.Area)) - projection.ToPanel(definition.Position);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg);
            }
            value.alignment = TextAlignmentOptions.Left;
            value.rectTransform.anchoredPosition = new Vector2(left + 44f + 40f, 0f);
            value.transform.SetAsLastSibling();
        }
    }
}
