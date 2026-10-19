using System;
using UnityEngine;

namespace ReverseTD.Defense
{
    [Serializable]
    public struct TowerSlot
    {
        [SerializeField, Tooltip("Tower center, in world units.")]
        private Vector2 position;

        [SerializeField, Tooltip("The tower that stands here.")]
        private TowerDefinition definition;

        [SerializeField, Tooltip("Right, Left, Up or Down for a fixed firing lane; Circle attacks targets in all directions.")]
        private TowerAttackDirection attackDirection;

        [SerializeField, Min(0f), Tooltip("Attack distance in world units, or radius in Circle mode. 0 uses the Tower Definition's Range.")]
        private float attackRange;

        public Vector2 Position => position;
        public TowerDefinition Definition => definition;
        public TowerAttackDirection AttackDirection => attackDirection;
        public float AttackRange => attackRange > 0f ? attackRange : (definition != null ? definition.Range : 0f);
    }
}
