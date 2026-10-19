using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class BoardView : MonoBehaviour
    {
        private const int GroundMargin = 16;
        private const float EdgeWidth = 0.1f;
        private const float EdgeShade = 0.7f;
        private const float DashSpacing = 0.6f;
        private const float DashLength = 0.26f;
        private const float DashWidth = 0.045f;
        private const float DashTint = 0.4f;
        private const float DashAlpha = 0.55f;

        private static readonly Color groundDotColor = new Color(0.17f, 0.2f, 0.24f);

        [SerializeField, Tooltip("The stage to build: the single source of truth for the path and the towers.")]
        private StageLayout layout;

        [SerializeField, Tooltip("Draw a visual goal zone at the path's final waypoint.")]
        private bool showGoalZone = true;

        private readonly List<Tower> towers = new List<Tower>();
        private StagePath path;

        public static event Action<BoardView> AllTowersDestroyed;

        public StageLayout Layout => layout;

        public StagePath Path
        {
            get
            {
                if (path == null)
                {
                    if (layout == null)
                    {
                        throw new InvalidOperationException("BoardView has no StageLayout, so it has no path.");
                    }
                    path = new StagePath(layout.Waypoints);
                }
                return path;
            }
        }

        // Single-path design: disable lower fork path.
        public StagePath LowerPath => null;

        public IReadOnlyList<Tower> Towers => towers;

        internal void RestoreLivingTowers()
        {
            towers.Clear();
            foreach (Tower tower in GetComponentsInChildren<Tower>(true))
            {
                if (tower.GetComponent<TowerHealth>().IsAlive) towers.Add(tower);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AllTowersDestroyed = null;
        }

        internal void HandleTowerDestroyed(Tower tower)
        {
            if (towers.Remove(tower) && towers.Count == 0)
            {
                AllTowersDestroyed?.Invoke(this);
            }
        }

        private void Awake()
        {
            if (layout == null)
            {
                Debug.LogError("BoardView has no StageLayout, so there's no board to build.", this);
                return;
            }

            EnsureSinglePathLevel2Layout();
            showGoalZone = true;

            BuildGround();
            BuildPath();
            BuildTowers();
            if (showGoalZone)
            {
                BuildGoalZone();
            }
        }

        private void EnsureSinglePathLevel2Layout()
        {
            // If an older Level2Stage asset with lowerWaypoints is loaded, upgrade it at runtime to the harder single-path layout.
            if (layout == null || layout.LowerWaypoints == null || layout.LowerWaypoints.Count == 0)
            {
                return;
            }

            TowerDefinition towerDef = null;
            if (layout.TowerSlots != null && layout.TowerSlots.Count > 0)
            {
                towerDef = layout.TowerSlots[0].Definition;
            }

            var newWaypoints = new Vector2[]
            {
                new Vector2(-8f, 3f),
                new Vector2(-2f, 3f),
                new Vector2(-2f, -3f),
                new Vector2(3f, -3f),
                new Vector2(3f, 3f),
                new Vector2(8f, 3f)
            };

            var newSlots = new TowerSlot[]
            {
                CreateSlot(new Vector2(-0.8f, 3f), towerDef, TowerAttackDirection.Left, 5f),
                CreateSlot(new Vector2(-5f, 4.3f), towerDef, TowerAttackDirection.Down, 2.5f),
                CreateSlot(new Vector2(-2f, -4.3f), towerDef, TowerAttackDirection.Up, 5.5f),
                CreateSlot(new Vector2(-1.64f, 4.3f), towerDef, TowerAttackDirection.Down, 5.5f),
                CreateSlot(new Vector2(4.3f, -3f), towerDef, TowerAttackDirection.Left, 5f),
                CreateSlot(new Vector2(4.3f, -3.36f), towerDef, TowerAttackDirection.Left, 5f),
                CreateSlot(new Vector2(3f, 4.3f), towerDef, TowerAttackDirection.Down, 5.5f),
                CreateSlot(new Vector2(3.36f, -4.3f), towerDef, TowerAttackDirection.Up, 5.5f),
                CreateSlot(new Vector2(5.8f, 1.7f), towerDef, TowerAttackDirection.Up, 2.5f)
            };

            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(StageLayout).GetField("waypoints", flags)?.SetValue(layout, newWaypoints);
            typeof(StageLayout).GetField("lowerWaypoints", flags)?.SetValue(layout, new Vector2[0]);
            typeof(StageLayout).GetField("towerSlots", flags)?.SetValue(layout, newSlots);
            path = null;
        }

        private static TowerSlot CreateSlot(Vector2 pos, TowerDefinition def, TowerAttackDirection dir, float range)
        {
            object boxed = new TowerSlot();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(TowerSlot).GetField("position", flags)?.SetValue(boxed, pos);
            typeof(TowerSlot).GetField("definition", flags)?.SetValue(boxed, def);
            typeof(TowerSlot).GetField("attackDirection", flags)?.SetValue(boxed, dir);
            typeof(TowerSlot).GetField("attackRange", flags)?.SetValue(boxed, range);
            return (TowerSlot)boxed;
        }

        private void BuildGoalZone()
        {
            const float borderWidth = 0.06f;
            Vector2 size = new Vector2(layout.PathWidth * 1.2f, layout.PathWidth * 1.4f);
            IReadOnlyList<Vector2> points = Path.Waypoints;
            Vector2 direction = points[points.Count - 1] - points[points.Count - 2];
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            Transform root = CreateChild("Goal Zone");
            Vector2 offset = new Vector2(0f, 0f);
            root.SetPositionAndRotation(Path.End + offset, rotation);
            ShapeSprites.AddPart(root, "Fill", ShapeSprites.Square, Vector2.zero, size,
                new Color(0.35f, 0.75f, 1f, 0.3f), SortingOrders.GoalZone);

            Color borderColor = new Color(0.05f, 0.4f, 1f, 1f);
            Vector2 horizontal = new Vector2(size.x, borderWidth);
            Vector2 vertical = new Vector2(borderWidth, size.y);
            float halfX = (size.x - borderWidth) * 0.5f;
            float halfY = (size.y - borderWidth) * 0.5f;
            ShapeSprites.AddPart(root, "Top Border", ShapeSprites.Square, new Vector2(0f, halfY), horizontal, borderColor, SortingOrders.GoalZone + 1);
            ShapeSprites.AddPart(root, "Bottom Border", ShapeSprites.Square, new Vector2(0f, -halfY), horizontal, borderColor, SortingOrders.GoalZone + 1);
            ShapeSprites.AddPart(root, "Left Border", ShapeSprites.Square, new Vector2(-halfX, 0f), vertical, borderColor, SortingOrders.GoalZone + 1);
            ShapeSprites.AddPart(root, "Right Border", ShapeSprites.Square, new Vector2(halfX, 0f), vertical, borderColor, SortingOrders.GoalZone + 1);
        }

        private void BuildGround()
        {
            Bounds bounds = layout.CalculateBounds();
            int width = 2 * (Mathf.CeilToInt(bounds.extents.x) + GroundMargin);
            int height = 2 * (Mathf.CeilToInt(bounds.extents.y) + GroundMargin);
            var center = new Vector3(Mathf.Round(bounds.center.x) + 0.5f, Mathf.Round(bounds.center.y) + 0.5f, 0f);

            Transform ground = CreateChild("Ground");
            ground.SetPositionAndRotation(center, Quaternion.identity);

            var groundRenderer = ground.gameObject.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = ShapeSprites.DotTile;
            groundRenderer.drawMode = SpriteDrawMode.Tiled;
            groundRenderer.tileMode = SpriteTileMode.Continuous;
            groundRenderer.size = new Vector2(width, height);
            groundRenderer.color = groundDotColor;
            groundRenderer.sortingOrder = SortingOrders.Ground;
        }

        private void BuildPath()
        {
            Transform root = CreateChild("Path");
            Color edgeColor = Palette.Shade(layout.PathColor, EdgeShade);
            Color dashColor = Palette.WithAlpha(Palette.Tint(layout.PathColor, DashTint), DashAlpha);
            float width = layout.PathWidth;

            DrawPathSegments(
                root,
                layout.Waypoints,
                edgeColor,
                dashColor,
                width
            );
        }

        private void DrawPathSegments(
            Transform root,
            IReadOnlyList<Vector2> points,
            Color edgeColor,
            Color dashColor,
            float width)
        {
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 from = points[i - 1];
                Vector2 to = points[i];
                Vector2 delta = to - from;
                float length = delta.magnitude;

                if (length <= 0f)
                {
                    continue;
                }

                Vector2 middle = (from + to) * 0.5f;
                Quaternion rotation = Quaternion.Euler(
                    0f, 0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg
                );

                AddPathPart(root, "Edge", middle, rotation,
                    new Vector2(length + width + EdgeWidth,
                                width + EdgeWidth),
                    edgeColor, SortingOrders.PathEdge);

                AddPathPart(root, $"Segment {i - 1}", middle, rotation,
                    new Vector2(length + width, width),
                    layout.PathColor, SortingOrders.Path);

                int steps = Mathf.FloorToInt(length / DashSpacing);

                for (int step = 1; step < steps; step++)
                {
                    Vector2 point = from + delta *
                        (step * DashSpacing / length);

                    AddPathPart(root, "Dash", point, rotation,
                        new Vector2(DashLength, DashWidth),
                        dashColor, SortingOrders.PathMark);
                }
            }
        }

        private static void AddPathPart(Transform root, string partName, Vector2 position, Quaternion rotation, Vector2 size, Color color, int sortingOrder)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(root, false);
            part.transform.SetPositionAndRotation(position, rotation);
            part.transform.localScale = new Vector3(size.x, size.y, 1f);

            var partRenderer = part.AddComponent<SpriteRenderer>();
            partRenderer.sprite = ShapeSprites.Square;
            partRenderer.color = color;
            partRenderer.sortingOrder = sortingOrder;
        }

        private void BuildTowers()
        {
            Transform root = CreateChild("Towers");
            IReadOnlyList<TowerSlot> slots = layout.TowerSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                TowerSlot slot = slots[i];
                if (slot.Definition == null)
                {
                    Debug.LogWarning($"Tower slot {i} in {layout.name} has no TowerDefinition, so it was skipped.", layout);
                    continue;
                }

                var towerObject = new GameObject($"Tower {i}");
                towerObject.SetActive(false);
                towerObject.transform.SetParent(root, false);
                towerObject.transform.position = slot.Position;

                var tower = towerObject.AddComponent<Tower>();
                tower.Initialize(slot.Definition, this, slot.AttackDirection, slot.AttackRange);
                towerObject.SetActive(true);
                towers.Add(tower);
            }
        }

        private Transform CreateChild(string childName)
        {
            Transform child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }

        private void OnDrawGizmos()
        {
            if (layout == null)
            {
                return;
            }

            IReadOnlyList<Vector2> points = layout.Waypoints;
            if (points.Count >= 2)
            {
                Gizmos.color = layout.PathColor;
                for (int i = 1; i < points.Count; i++)
                {
                    Gizmos.DrawLine(points[i - 1], points[i]);
                }

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(points[0], 0.2f);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(points[points.Count - 1], 0.2f);
            }

            IReadOnlyList<TowerSlot> slots = layout.TowerSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                TowerDefinition definition = slots[i].Definition;
                if (definition == null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireCube(slots[i].Position, new Vector3(1f, 1f, 0f));
                    continue;
                }

                Color color = definition.Color;
                Gizmos.color = color;
                Gizmos.DrawWireCube(slots[i].Position, new Vector3(definition.Size, definition.Size, 0f));
                Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
                DrawAttackLane(slots[i], definition);
            }
        }

        private static void DrawAttackLane(TowerSlot slot, TowerDefinition definition)
        {
            if (slot.AttackDirection == TowerAttackDirection.Circle)
            {
                const int segments = 64;
                Vector2 previous = slot.Position + Vector2.right * slot.AttackRange;
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector2 point = slot.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * slot.AttackRange;
                    Gizmos.DrawLine(previous, point);
                    previous = point;
                }
                return;
            }
            Vector2 direction = TowerAttackGeometry.Direction(slot.AttackDirection);
            Vector2 side = new Vector2(-direction.y, direction.x) * (definition.AttackWidth * 0.5f);
            Vector2 start = slot.Position;
            Vector2 end = start + direction * slot.AttackRange;
            Gizmos.DrawLine(start - side, end - side);
            Gizmos.DrawLine(start + side, end + side);
            Gizmos.DrawLine(start - side, start + side);
            Gizmos.DrawLine(end - side, end + side);
            Gizmos.DrawLine(start, end);
        }
    }
}
