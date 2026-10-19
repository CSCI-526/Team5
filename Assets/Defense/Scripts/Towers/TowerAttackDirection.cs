using UnityEngine;

namespace ReverseTD.Defense
{
    public enum TowerAttackDirection
    {
        Right,
        Left,
        Up,
        Down,
        [InspectorName("Circle (圆形)")]
        Circle = 4
    }

    public static class TowerAttackGeometry
    {
        public static Vector2 Direction(TowerAttackDirection direction)
        {
            switch (direction)
            {
                case TowerAttackDirection.Left: return Vector2.left;
                case TowerAttackDirection.Up: return Vector2.up;
                case TowerAttackDirection.Down: return Vector2.down;
                case TowerAttackDirection.Circle: return Vector2.zero;
                default: return Vector2.right;
            }
        }

        public static bool Contains(Vector2 offset, Vector2 direction, float length, float width, bool circular = false)
        {
            if (circular) return length >= 0f && offset.sqrMagnitude <= length * length;
            float forward = Vector2.Dot(offset, direction);
            float sideways = Vector2.Dot(offset, new Vector2(-direction.y, direction.x));
            return forward >= 0f && forward <= length && Mathf.Abs(sideways) <= width * 0.5f;
        }
    }
}
