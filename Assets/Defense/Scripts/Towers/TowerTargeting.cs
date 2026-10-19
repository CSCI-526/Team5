using System.Collections.Generic;
using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class TowerTargeting : MonoBehaviour
    {
        private const float SameProgress = 0.001f;

        private readonly List<IDamageable> candidates = new List<IDamageable>();
        private TowerDefinition definition;
        private Vector2 direction;
        private float range;
        private bool circular;

        public void Initialize(TowerDefinition towerDefinition, Vector2 attackDirection, float attackRange, bool circularAttack = false)
        {
            definition = towerDefinition;
            direction = attackDirection;
            range = attackRange;
            circular = circularAttack;
        }

        public IDamageable FindTarget()
        {
            CombatRegistry.GetAlive(Team.Attacker, candidates);

            Vector2 origin = transform.position;
            IDamageable nearest = null;
            float nearestSqr = float.MaxValue;
            IDamageable first = null;
            float firstProgress = float.MinValue;
            float firstSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                IDamageable candidate = candidates[i];
                Vector2 offset = (Vector2)candidate.Position - origin;
                float distanceSqr = offset.sqrMagnitude;
                if (!TowerAttackGeometry.Contains(offset, direction, range, definition.AttackWidth, circular))
                {
                    continue;
                }

                if (distanceSqr < nearestSqr)
                {
                    nearest = candidate;
                    nearestSqr = distanceSqr;
                }

                if (candidate is IPathProgress progress)
                {
                    float traveled = progress.DistanceTraveled;
                    bool ahead = traveled > firstProgress + SameProgress;
                    bool level = traveled >= firstProgress - SameProgress;
                    if (ahead || (level && distanceSqr < firstSqr))
                    {
                        first = candidate;
                        firstProgress = traveled;
                        firstSqr = distanceSqr;
                    }
                }
            }

            if (definition.TargetingMode == TargetingMode.First && first != null)
            {
                return first;
            }
            return nearest;
        }
    }
}
