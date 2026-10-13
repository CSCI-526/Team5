using System.Collections.Generic;
using UnityEngine;

namespace ReverseTD.Defense
{
    [CreateAssetMenu(menuName = "ReverseTD/Stage Layout", fileName = "StageLayout")]
    public class StageLayout : ScriptableObject
    {
        [SerializeField, Tooltip("Path corners in walking order, in world coordinates. Needs at least 2.")]
        private Vector2[] waypoints = new Vector2[0];

        [SerializeField]
        private Vector2[] lowerWaypoints = new Vector2[0];

        [SerializeField, Tooltip("Where towers stand, and which tower stands there.")]
        private TowerSlot[] towerSlots = new TowerSlot[0];

        [SerializeField, Min(0.05f), Tooltip("Width of the drawn path, in world units.")]
        private float pathWidth = 0.7f;

        [SerializeField, Tooltip("Color of the drawn path.")]
        private Color pathColor = new Color(0.78f, 0.72f, 0.55f);

        public IReadOnlyList<Vector2> Waypoints => waypoints;
        public IReadOnlyList<Vector2> LowerWaypoints => lowerWaypoints;

        public IReadOnlyList<TowerSlot> TowerSlots => towerSlots;
        public float PathWidth => pathWidth;
        public Color PathColor => pathColor;

        public Bounds CalculateBounds()
        {
            var bounds = new Bounds();
            bool isEmpty = true;

            for (int i = 0; i < waypoints.Length; i++)
            {
                Include(ref bounds, ref isEmpty, new Bounds(waypoints[i], new Vector3(pathWidth, pathWidth, 0f)));
            }
            for (int i = 0; i < lowerWaypoints.Length; i++)
            {
                Include(ref bounds, ref isEmpty,
                    new Bounds(lowerWaypoints[i],
                    new Vector3(pathWidth, pathWidth, 0f)));
            }

            for (int i = 0; i < towerSlots.Length; i++)
            {
                TowerDefinition definition = towerSlots[i].Definition;
                float size = definition != null ? definition.Size : 1f;
                Include(ref bounds, ref isEmpty, new Bounds(towerSlots[i].Position, new Vector3(size, size, 0f)));
            }

            return bounds;
        }

        private static void Include(ref Bounds bounds, ref bool isEmpty, Bounds area)
        {
            if (isEmpty)
            {
                bounds = area;
                isEmpty = false;
            }
            else
            {
                bounds.Encapsulate(area);
            }
        }
    }
}
