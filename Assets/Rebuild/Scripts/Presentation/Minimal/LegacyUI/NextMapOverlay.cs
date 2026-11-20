using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class NextMapOverlay : MonoBehaviour
    {
        private static readonly Color LaneColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color SpawnColor = new Color(0.9f, 0.25f, 0.25f);
        private static readonly Color GoalColor = new Color(0.2f, 0.75f, 0.9f);
        private static readonly Color GoalInk = new Color(0.02f, 0.13f, 0.17f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Opacity of a lane's fill.")] private float laneFillAlpha = 0.2f;
        [SerializeField, Tooltip("Opacity of a lane's edge line.")] private float laneEdgeAlpha = 1f;
        [SerializeField, Tooltip("World width of a lane's edge line.")] private float laneEdgeWidth = 0.06f;
        [SerializeField, Tooltip("Height in pixels of the START and GOAL labels above the center of their cells.")] private float labelLift = 39f;

        private MaterialPropertyBlock block;
        private RectTransform minions;
        private RectTransform labels;

        public void Build(NextMapData data, NextMapProjection projection, IReadOnlyList<TowerRangeView> ranges)
        {
            block = new MaterialPropertyBlock();
            minions = Layer("Minions");
            labels = Layer("Labels");
            foreach (var range in ranges)
            {
                var edge = range.GetComponentInChildren<LineRenderer>(true);
                if (edge != null) edge.widthMultiplier = laneEdgeWidth;
                Tint(range.GetComponent<MeshRenderer>(), new Color(LaneColor.r, LaneColor.g, LaneColor.b, laneFillAlpha));
                Tint(edge, new Color(LaneColor.r, LaneColor.g, LaneColor.b, laneEdgeAlpha));
            }
            // No forced-route line or hatch: terrain geometry plus the two endpoint pins is clearer.
            // The scene's physical obstacle islands and the real tower range meshes remain visible.
            foreach (var minion in data.Minions) Minion(minion, projection);
            foreach (var threat in data.Threats) ThreatMark(threat, projection);
            Pin("Start Pin", "START", projection.ToPanel(data.Spawn), SpawnColor, Color.white);
            Pin("Goal Pin", "GOAL", projection.ToPanel(data.Goal), GoalColor, GoalInk);
            // Tower models and their shape-accurate coverage are visible without floating number/range banners.
        }

        public static void Legend(Transform parent, Vector2 start)
        {
            var x = start.x;
            LegacyUiKit.CreateImage(parent, "Legend Lane Edge", new Vector2(x + 11f, start.y), new Vector2(22f, 14f), LaneColor, 3);
            var lane = LegacyUiKit.CreateImage(parent, "Legend Lane", new Vector2(x + 11f, start.y), new Vector2(19f, 11f), LegacyUiKit.PanelColor, 2);
            var hatch = LegacyUiKit.CreateImage(lane.transform, "Hatch", Vector2.zero, new Vector2(19f, 11f), new Color(LaneColor.r, LaneColor.g, LaneColor.b, 0.7f));
            hatch.sprite = NextMapShapes.Hatch;
            hatch.type = Image.Type.Tiled;
            x = LegendLabel(parent, "TOWER COVERAGE", lane.rectTransform, x, start.y);
            var spawn = LegacyUiKit.CreateImage(parent, "Legend Start", new Vector2(x + 11f, start.y), new Vector2(22f, 14f), SpawnColor, 3);
            x = LegendLabel(parent, "START", spawn.rectTransform, x, start.y);
            var goal = LegacyUiKit.CreateImage(parent, "Legend Goal", new Vector2(x + 11f, start.y), new Vector2(22f, 14f), GoalColor, 3);
            x = LegendLabel(parent, "GOAL", goal.rectTransform, x, start.y);
            var minion = LegacyUiKit.CreateImage(parent, "Legend Minion", new Vector2(x + 11f, start.y), new Vector2(14f, 14f), MinionLook.BodyColor, 3);
            LegendLabel(parent, "SENTRY / TRAP", minion.rectTransform, x, start.y);
        }

        private void Minion(MinionPlacement placement, NextMapProjection projection)
        {
            var size = Mathf.Max(8f, 0.72f * projection.PixelsPerUnit);
            var stroke = Mathf.Max(2f, 0.08f * projection.PixelsPerUnit);
            foreach (var cell in placement.Cells)
            {
                var guard = LegacyUiKit.CreateImage(minions, "Minion", projection.ToPanel(cell), new Vector2(size, size), MinionLook.BodyColor, 3);
                for (var stripe = 0; stripe < 2; stripe++)
                {
                    var mark = LegacyUiKit.CreateImage(guard.transform, "Mark", Vector2.zero, new Vector2(size * 0.5f, stroke), Color.white);
                    mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, stripe == 0 ? 45f : -45f);
                }
            }
        }

        // Map preview marks actual placed hazards, not fabricated route enemies.
        private void ThreatMark(NextMapThreat threat, NextMapProjection projection)
        {
            var point = projection.ToPanel(threat.Position);
            var fill = threat.IsSentry ? new Color(.95f, .24f, .25f) : new Color(1f, .57f, .16f);
            var mark = LegacyUiKit.CreateCircle(minions, threat.IsSentry ? "Sentry" : "Trap", point,
                Mathf.Clamp(projection.PixelsPerUnit * .45f, 9f, 19f), fill);
            mark.raycastTarget = false;
        }

        private static float LegendLabel(Transform parent, string text, RectTransform swatch, float x, float y)
        {
            var label = LegacyUiKit.CreateText(parent, "Legend " + text, text, Vector2.zero, new Vector2(160f, 20f), 14, LegacyUiKit.InkMuted, FontStyles.Bold, 2);
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

        private void Pin(string name, string text, Vector2 point, Color fill, Color ink)
        {
            var root = LegacyUiKit.CreateRect(labels, name, point + new Vector2(0f, labelLift), Vector2.zero);
            var label = LegacyUiKit.CreateText(root, "Label", text, Vector2.zero, new Vector2(120f, 26f), 15, ink, FontStyles.Bold, 4);
            var back = LegacyUiKit.CreateImage(root, "Back", Vector2.zero, new Vector2(label.GetPreferredValues(text).x + 20f, 26f), fill, 6);
            back.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
        }


    }
}
