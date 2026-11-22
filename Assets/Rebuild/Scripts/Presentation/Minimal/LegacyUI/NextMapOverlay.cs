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
        private static readonly Color SentryColor = new Color(0.95f, 0.24f, 0.25f);
        private static readonly Color TrapColor = new Color(1f, 0.57f, 0.16f);
        private const float LegendRowGap = 24f;
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
            var top = start.y + LegendRowGap / 2f;
            var bottom = start.y - LegendRowGap / 2f;
            var x = start.x;
            LegacyUiKit.CreateImage(parent, "Legend Lane Edge", new Vector2(x + 11f, top), new Vector2(22f, 14f), LaneColor, 3);
            var lane = LegacyUiKit.CreateImage(parent, "Legend Lane", new Vector2(x + 11f, top), new Vector2(19f, 11f), LegacyUiKit.PanelColor, 2);
            var hatch = LegacyUiKit.CreateImage(lane.transform, "Hatch", Vector2.zero, new Vector2(19f, 11f), new Color(LaneColor.r, LaneColor.g, LaneColor.b, 0.7f));
            hatch.sprite = NextMapShapes.Hatch;
            hatch.type = Image.Type.Tiled;
            x = LegendLabel(parent, "TOWER COVERAGE", x, top);
            LegacyUiKit.CreateImage(parent, "Legend Start", new Vector2(x + 11f, top), new Vector2(22f, 14f), SpawnColor, 3);
            x = LegendLabel(parent, "START", x, top);
            LegacyUiKit.CreateImage(parent, "Legend Goal", new Vector2(x + 11f, top), new Vector2(22f, 14f), GoalColor, 3);
            LegendLabel(parent, "GOAL", x, top);
            x = start.x;
            LegacyUiKit.CreateCircle(parent, "Legend Sentry", new Vector2(x + 5f, bottom), 11f, SentryColor).raycastTarget = false;
            LegacyUiKit.CreateCircle(parent, "Legend Trap", new Vector2(x + 17f, bottom), 11f, TrapColor).raycastTarget = false;
            x = LegendLabel(parent, "SENTRY / TRAP", x, bottom);
            Marker(parent, MinionKind.Bearer, new Vector2(x + 11f, bottom), 18f);
            x = LegendLabel(parent, "SHIELD BEARER", x, bottom);
            Marker(parent, MinionKind.Lobber, new Vector2(x + 11f, bottom), 18f);
            LegendLabel(parent, "LOBBER", x, bottom);
        }

        private void Minion(MinionPlacement placement, NextMapProjection projection)
        {
            var size = Mathf.Clamp(projection.PixelsPerUnit * 0.62f, 12f, 30f);
            if (placement.Kind == MinionKind.Bearer) GuardRing(placement.TowerPosition, projection);
            foreach (var cell in placement.Cells) Marker(minions, placement.Kind, projection.ToPanel(cell), size);
        }

        private void GuardRing(Vector3 tower, NextMapProjection projection)
        {
            var diameter = Mathf.Max(20f, projection.PixelsPerUnit * 1.55f);
            var sprite = LegacyUiKit.Skin != null ? LegacyUiKit.Skin.ring : null;
            var color = new Color(MinionPalette.Glass.r, MinionPalette.Glass.g, MinionPalette.Glass.b, sprite != null ? 0.95f : 0.3f);
            var ring = sprite != null
                ? LegacyUiKit.CreateImage(minions, "Shield Ring", projection.ToPanel(tower), new Vector2(diameter, diameter), color)
                : LegacyUiKit.CreateCircle(minions, "Shield Ring", projection.ToPanel(tower), diameter, color);
            if (sprite != null) ring.sprite = sprite;
            ring.raycastTarget = false;
        }

        private static void Marker(Transform parent, MinionKind kind, Vector2 point, float size)
        {
            var bearer = kind == MinionKind.Bearer;
            var rim = LegacyUiKit.CreateCircle(parent, bearer ? "Bearer" : "Lobber", point, size, MinionPalette.Brass);
            rim.raycastTarget = false;
            var face = LegacyUiKit.CreateCircle(rim.transform, "Face", Vector2.zero, size * 0.78f, bearer ? MinionPalette.BlueStone : MinionPalette.Gunmetal);
            face.raycastTarget = false;
            var icon = LegacyUiKit.CreateIcon(face.transform, "Icon", bearer ? "shield" : "target", Vector2.zero, size * 0.56f);
            if (icon != null)
            {
                icon.color = bearer ? Color.white : MinionPalette.Glass;
                icon.raycastTarget = false;
                return;
            }
            LegacyUiKit.CreateCircle(face.transform, "Dot", Vector2.zero, size * 0.3f, bearer ? Color.white : MinionPalette.Glass).raycastTarget = false;
        }

        private void ThreatMark(NextMapThreat threat, NextMapProjection projection)
        {
            var point = projection.ToPanel(threat.Position);
            var mark = LegacyUiKit.CreateCircle(minions, threat.IsSentry ? "Sentry" : "Trap", point,
                Mathf.Clamp(projection.PixelsPerUnit * .45f, 9f, 19f), threat.IsSentry ? SentryColor : TrapColor);
            mark.raycastTarget = false;
        }

        private static float LegendLabel(Transform parent, string text, float x, float y)
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
